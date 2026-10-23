# -*- coding: utf-8 -*-
"""
aps_core_bridge — тонкий адаптер между Revit (pyRevit / IronPython) и C#-ядром APS.Core.
Позволяет использовать высокопроизводительную расчетную математику APS.Core.dll,
а при её отсутствии прозрачно переключается на встроенный fallback (apsgeom.py).
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
DB_PATH = os.path.join(ROOT_DIR, "APS-Suite", "database", "aps_equipment_db.json")

# Приоритетный поиск сборки APS.Core.dll
DLL_SEARCH_PATHS = [
    os.path.join(ROOT_DIR, "APS-Suite", "src", "APS.Core", "bin", "Debug", "netstandard2.0", "APS.Core.dll"),
    os.path.join(ROOT_DIR, "APS-Suite", "src", "APS.Core", "bin", "Release", "netstandard2.0", "APS.Core.dll"),
    os.path.join(ROOT_DIR, "APS-Suite", "src", "APS.Core", "bin", "Debug", "net8.0", "APS.Core.dll"),
    os.path.join(ROOT_DIR, "APS-Suite", "src", "APS.Core", "bin", "Release", "net8.0", "APS.Core.dll"),
    os.path.join(HERE, "APS.Core.dll"),
]

_core_available = False
CoreGeom = None
CorePlan = None
CoreElec = None
CorePower = None
NetList = None

try:
    import clr
    loaded = False
    for p in DLL_SEARCH_PATHS:
        if os.path.exists(p):
            clr.AddReferenceToFileAndPath(p)
            loaded = True
            break
    if not loaded:
        try:
            clr.AddReference("APS.Core")
            loaded = True
        except Exception:
            pass

    if loaded:
        import APS.Core.Geometry as CoreGeom
        import APS.Core.Planning as CorePlan
        import APS.Core.Electrical as CoreElec
        import APS.Core.Power as CorePower
        from System.Collections.Generic import List as NetList
        _core_available = True
except Exception:
    _core_available = False


def is_core_available():
    """Возвращает True, если APS.Core.dll успешно загружена в среду выполнения."""
    return _core_available


def load_equipment_db():
    """Загружает характеристики из единой базы данных aps_equipment_db.json."""
    path = DB_PATH
    if not os.path.exists(path):
        fallback_path = os.path.join(HERE, "aps_rules.json")
        if os.path.exists(fallback_path):
            with open(fallback_path, "r") as f:
                return json.load(f)
        return {}
    with open(path, "r") as f:
        return json.load(f)


def _ft_to_mm(val_ft):
    """Преобразует внутренние единицы Revit (футы) в миллиметры."""
    return float(val_ft) * 304.8


def plan_detectors_for_room(outer_poly, holes=None, norm_key="SP_484_2020", detector_type="smoke", min_per_room=1):
    """
    Планирует расстановку пожарных извещателей в помещении.
    outer_poly: [(x, y), ...] координаты контура в мм
    holes: [[(x, y), ...], ...] внутренние вырезы (колонны, шахты) в мм
    """
    db = load_equipment_db()
    norm_info = db.get("normative_standards", {}).get(norm_key, {})
    detector_tables = norm_info.get("detectors", {}).get(detector_type, [])
    table_row = detector_tables[0] if detector_tables else {
        "h_max": 12000,
        "area": 81.0,
        "spacing": 9050,
        "wall_max": 4525
    }

    if _core_available and CoreGeom is not None and CorePlan is not None and NetList is not None:
        try:
            poly_list = NetList[CoreGeom.Point2D]()
            for pt in outer_poly:
                poly_list.Add(CoreGeom.Point2D(float(pt[0]), float(pt[1])))

            net_holes = NetList[NetList[CoreGeom.Point2D]]()
            if holes:
                for h in holes:
                    cur_h = NetList[CoreGeom.Point2D]()
                    for pt in h:
                        cur_h.Add(CoreGeom.Point2D(float(pt[0]), float(pt[1])))
                    net_holes.Add(cur_h)

            criteria = CorePlan.DetectorNormCriteria()
            criteria.MaxCeilingHeightMm = float(table_row.get("h_max", 12000))
            criteria.ProtectedAreaM2 = float(table_row.get("area", 81.0))
            criteria.MaxSpacingMm = float(table_row.get("spacing", 9050))
            criteria.MaxDistanceToWallMm = float(table_row.get("wall_max", 4525))

            config = CorePlan.PlanningConfig()
            config.MinPerRoom = int(min_per_room)
            config.WallMinMm = 100.0
            config.MaxShiftMm = 1500.0

            res = CorePlan.Sp484DetectorPlanner.Plan(
                poly_list,
                net_holes if (holes and len(holes) > 0) else None,
                criteria,
                config
            )

            points = [(p.X, p.Y) for p in res.Points]
            issues = [str(iss) for iss in res.Issues]
            return {
                "points": points,
                "shifted": res.ShiftedCount,
                "added": res.AddedCount,
                "need": res.RequiredMin,
                "issues": issues,
                "engine": "APS.Core (.NET)"
            }
        except Exception:
            pass

    # Fallback на встроенную реализацию Python
    import apsgeom
    cfg = {"wall_min": 100.0, "min_per_room": min_per_room, "max_shift": 1500.0, "sample_step": 500.0}
    res = apsgeom.plan_room(outer_poly, holes or [], float(table_row.get("h_max", 12000)), table_row, [], [], cfg)
    res["engine"] = "apsgeom (Python Fallback)"
    return res


def extract_revit_room_polygons(room, spatial_boundary_options=None):
    """
    Извлекает внешний контур и отверстия из Revit Room/Space в координатах мм.
    Возвращает (outer_poly, holes).
    """
    try:
        from Autodesk.Revit import DB
    except ImportError:
        DB = None

    if DB is not None and spatial_boundary_options is None:
        spatial_boundary_options = DB.SpatialElementBoundaryOptions()

    outer_poly = []
    holes = []

    boundaries = room.GetBoundarySegments(spatial_boundary_options)
    if not boundaries or len(boundaries) == 0:
        return outer_poly, holes

    def _loop_to_pts(boundary_loop):
        pts = []
        for seg in boundary_loop:
            c = seg.GetCurve()
            p = c.GetEndPoint(0)
            pts.append((_ft_to_mm(p.X), _ft_to_mm(p.Y)))
        return pts

    def _poly_area(pts):
        n = len(pts)
        if n < 3:
            return 0.0
        a = 0.0
        for i in range(n):
            j = (i + 1) % n
            a += pts[i][0] * pts[j][1] - pts[j][0] * pts[i][1]
        return abs(a) * 0.5

    loops_pts = [_loop_to_pts(loop) for loop in boundaries]
    if not loops_pts:
        return outer_poly, holes

    loops_pts.sort(key=lambda p: _poly_area(p), reverse=True)
    outer_poly = loops_pts[0]
    if len(loops_pts) > 1:
        holes = loops_pts[1:]

    return outer_poly, holes


def plan_detectors_for_revit_room(room, spatial_boundary_options=None, norm_key="SP_484_2020", detector_type="smoke", min_per_room=1):
    """
    Выполняет полный цикл: чтение геометрии помещения Revit Room/Space,
    преобразование в Geometry2D ядра APS.Core и вызов Sp484DetectorPlanner.
    """
    outer_poly, holes = extract_revit_room_polygons(room, spatial_boundary_options)
    if not outer_poly:
        return {
            "points": [],
            "shifted": 0,
            "added": 0,
            "need": 0,
            "issues": ["Помещение не имеет замкнутого контура границы"],
            "engine": "none"
        }
    return plan_detectors_for_room(
        outer_poly=outer_poly,
        holes=holes,
        norm_key=norm_key,
        detector_type=detector_type,
        min_per_room=min_per_room
    )
