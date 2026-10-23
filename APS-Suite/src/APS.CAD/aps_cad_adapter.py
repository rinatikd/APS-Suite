# -*- coding: utf-8 -*-
"""
APS CAD COM Adapter — интеграционный модуль для подключения к AutoCAD и ZWCAD.
Осуществляет:
1. Автоподключение к активному экземпляру САПР через COM (AutoCAD.Application / ZWCAD.Application).
2. Извлечение замкнутых полилиний помещений (с выбранного слоя или интерактивным выбором).
3. Передачу геометрии в APS.Core (Sp484DetectorPlanner) для расчёта точек расстановки по СП 484.
4. Вставку блоков извещателей в ModelSpace с записью атрибутов из aps_equipment_db.json.
"""

import os
import sys
import json
import win32com.client
from win32com.client import VARIANT
import pythoncom

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.abspath(os.path.join(HERE, "..", ".."))
DB_PATH = os.path.join(ROOT_DIR, "database", "aps_equipment_db.json")

DLL_CANDIDATES = [
    os.path.join(ROOT_DIR, "src", "APS.Core", "bin", "Debug", "netstandard2.0", "APS.Core.dll"),
    os.path.join(ROOT_DIR, "src", "APS.Core", "bin", "Release", "netstandard2.0", "APS.Core.dll"),
    os.path.join(ROOT_DIR, "src", "APS.Core", "bin", "Debug", "net8.0", "APS.Core.dll"),
    os.path.join(ROOT_DIR, "src", "APS.Core", "bin", "Release", "net8.0", "APS.Core.dll"),
]

_core_available = False
CoreGeom = None
CorePlan = None
NetList = None

try:
    import clr
    loaded = False
    for dll_path in DLL_CANDIDATES:
        if os.path.exists(dll_path):
            clr.AddReferenceToFileAndPath(dll_path)
            loaded = True
            break
    if not loaded:
        clr.AddReference("APS.Core")
        loaded = True

    if loaded:
        import APS.Core.Geometry as CoreGeom
        import APS.Core.Planning as CorePlan
        from System.Collections.Generic import List as NetList
        _core_available = True
except Exception as ex:
    _core_available = False
    sys.stderr.write(f"[WARN] Не удалось подключить APS.Core.dll через clr: {ex}\n")


class CadComAdapter:
    """Адаптер для взаимодействия с AutoCAD / ZWCAD через COM API."""

    def __init__(self):
        self.app = None
        self.doc = None
        self.cad_brand = None
        self.db = self._load_equipment_db()

    def _load_equipment_db(self):
        if os.path.exists(DB_PATH):
            with open(DB_PATH, "r", encoding="utf-8") as f:
                return json.load(f)
        return {}

    def connect(self):
        """Подключается к запущенному экземпляру AutoCAD или ZWCAD."""
        prog_ids = [
            ("AutoCAD", "AutoCAD.Application"),
            ("ZWCAD", "ZWCAD.Application")
        ]

        for brand, prog_id in prog_ids:
            try:
                self.app = win32com.client.GetActiveObject(prog_id)
                self.doc = self.app.ActiveDocument
                self.cad_brand = brand
                print(f"[INFO] Подключено к САПР: {brand} ({self.doc.Name})")
                return True
            except pythoncom.com_error:
                continue

        raise RuntimeError("Не удалось обнаружить активный экземпляр AutoCAD или ZWCAD. Запустите САПР с открытым чертежом.")

    def get_polylines_from_layer(self, layer_name):
        """Возвращает список замкнутых полилиний со слоя layer_name."""
        if not self.doc:
            raise RuntimeError("Нет активного документа САПР.")

        model_space = self.doc.ModelSpace
        polylines = []

        for entity in model_space:
            try:
                obj_name = entity.ObjectName.lower()
                if ("polyline" in obj_name) and entity.Layer.lower() == layer_name.lower():
                    if getattr(entity, "Closed", False):
                        polylines.append(entity)
            except Exception:
                continue

        return polylines

    def select_polylines_interactive(self):
        """Запрашивает выбор замкнутых полилиний пользователем в САПР."""
        if not self.doc:
            raise RuntimeError("Нет активного документа САПР.")

        selection_set_name = "APS_ROOM_SELECTION"
        try:
            self.doc.SelectionSets.Item(selection_set_name).Delete()
        except Exception:
            pass

        sset = self.doc.SelectionSets.Add(selection_set_name)
        print("[PROMPT] Выберите замкнутые полилинии контуров помещений в окне САПР...")
        sset.SelectOnScreen()

        polylines = []
        for entity in sset:
            try:
                obj_name = entity.ObjectName.lower()
                if "polyline" in obj_name and getattr(entity, "Closed", False):
                    polylines.append(entity)
            except Exception:
                continue

        sset.Delete()
        return polylines

    @staticmethod
    def extract_polyline_coordinates(polyline):
        """Извлекает список вершин [(x, y), ...] из AcDbPolyline / AcDb2dPolyline."""
        coords = polyline.Coordinates
        pts = []
        total = len(coords)

        obj_name = polyline.ObjectName.lower()
        step = 3 if "3d" in obj_name else 2

        for i in range(0, total, step):
            pts.append((float(coords[i]), float(coords[i + 1])))

        return pts

    def plan_room_detectors(self, outer_poly, holes=None, norm_key="SP_484_2020", detector_type="smoke", min_per_room=1):
        """Вызывает ядро Sp484DetectorPlanner из APS.Core для расчёта точек установки."""
        norm_info = self.db.get("normative_standards", {}).get(norm_key, {})
        detector_tables = norm_info.get("detectors", {}).get(detector_type, [])
        table_row = detector_tables[0] if detector_tables else {
            "h_max": 12000,
            "area": 81.0,
            "spacing": 9050,
            "wall_max": 4525
        }

        if not _core_available:
            raise RuntimeError("APS.Core.dll не загружена. Проверьте сборку решения в bin/Debug/netstandard2.0.")

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

        result = CorePlan.Sp484DetectorPlanner.Plan(
            poly_list,
            net_holes if (holes and len(holes) > 0) else None,
            criteria,
            config
        )

        return [(p.X, p.Y) for p in result.Points]

    def insert_detector_block(self, point, block_name, attributes=None, scale=1.0):
        """
        Вставляет блок извещателя в ModelSpace и заполняет его атрибуты.
        point: (x, y) или (x, y, z)
        attributes: dict {Tag: Value}
        """
        model_space = self.doc.ModelSpace
        ins_pt = VARIANT(
            pythoncom.VT_ARRAY | pythoncom.VT_R8,
            (float(point[0]), float(point[1]), 0.0)
        )

        try:
            block_ref = model_space.InsertBlock(ins_pt, block_name, scale, scale, scale, 0.0)
        except pythoncom.com_error:
            circle = model_space.AddCircle(ins_pt, 150.0)
            circle.Layer = "APS_DETECTORS"
            return circle

        if attributes and block_ref.HasAttributes:
            for att in block_ref.GetAttributes():
                tag = att.TagString.upper()
                if tag in attributes:
                    att.TextString = str(attributes[tag])
                    att.Update()

        return block_ref

    def process_room_polylines(self, polylines, vendor_key="rubezh", detector_type="smoke", block_name="APS_SMOKE"):
        """
        Полный цикл: расчет точек по полилиниям и расстановка блоков с атрибутами оборудования.
        """
        vendor_info = self.db.get("vendors", {}).get(vendor_key, {})
        devices = vendor_info.get("electrical", {}).get("devices", {})
        dev_meta = devices.get(detector_type, {})
        model_name = dev_meta.get("model", "ИП-212")

        total_inserted = 0
        for pline in polylines:
            coords = self.extract_polyline_coordinates(pline)
            if len(coords) < 3:
                continue

            points = self.plan_room_detectors(coords, detector_type=detector_type)

            attrs = {
                "MODEL": model_name,
                "VENDOR": vendor_info.get("title", vendor_key),
                "TYPE": detector_type.upper(),
                "NORM": "СП 484.1311500.2020"
            }

            for pt in points:
                self.insert_detector_block(pt, block_name=block_name, attributes=attrs)
                total_inserted += 1

        self.doc.Regen(1)
        print(f"[SUCCESS] Успешно расставлено извещателей: {total_inserted}")
        return total_inserted


def main():
    """Точка входа скрипта CAD-адаптера."""
    adapter = CadComAdapter()
    adapter.connect()

    layer = input("Введите имя слоя с контурами помещений (оставьте пустым для ручного выбора на экране): ").strip()
    if layer:
        polylines = adapter.get_polylines_from_layer(layer)
    else:
        polylines = adapter.select_polylines_interactive()

    if not polylines:
        print("[WARN] Не найдено подходящих замкнутых полилиний.")
        return

    print(f"[INFO] Найдено полилиний для обработки: {len(polylines)}")
    adapter.process_room_polylines(polylines)


if __name__ == "__main__":
    main()
