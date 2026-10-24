# -*- coding: utf-8 -*-
import os
import json
import math
import win32com.client as win32

EQUIPMENT_DB_PATH = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "database", "aps_equipment_db.json"))

def get_cad_app():
    for prog_id in ["AutoCAD.Application", "ZWCAD.Application"]:
        try:
            app = win32.GetActiveObject(prog_id)
            print(f"[OK] Подключено к САПР: {prog_id}")
            return app
        except Exception:
            pass
    raise RuntimeError("Не найден запущенный экземпляр AutoCAD или ZWCAD.")

def load_equipment_db():
    if os.path.exists(EQUIPMENT_DB_PATH):
        with open(EQUIPMENT_DB_PATH, "r", encoding="utf-8-sig") as f:
            return json.load(f)
    return {"equipment": []}

def point_in_polygon(x, y, poly):
    n = len(poly)
    inside = False
    p1x, p1y = poly[0]
    for i in range(n + 1):
        p2x, p2y = poly[i % n]
        if y > min(p1y, p2y):
            if y <= max(p1y, p2y):
                if x <= max(p1x, p2x):
                    if p1y != p2y:
                        xinters = (y - p1y) * (p2x - p1x) / (p2y - p1y) + p1x
                    if p1x == p2x or x <= xinters:
                        inside = not inside
        p1x, p1y = p2x, p2y
    return inside

def calculate_grid_positions(poly_points, spacing=8.0, margin=4.0):
    min_x = min(p[0] for p in poly_points)
    max_x = max(p[0] for p in poly_points)
    min_y = min(p[1] for p in poly_points)
    max_y = max(p[1] for p in poly_points)

    points = []
    curr_y = min_y + margin
    while curr_y < max_y:
        curr_x = min_x + margin
        while curr_x < max_x:
            if point_in_polygon(curr_x, curr_y, poly_points):
                points.append((curr_x, curr_y, 0.0))
            curr_x += spacing
        curr_y += spacing
    return points

def run():
    app = get_cad_app()
    doc = app.ActiveDocument
    if not doc:
        print("[!] Нет открытого активного чертежа.")
        return

    ms = doc.ModelSpace
    db = load_equipment_db()
    print(f"[OK] Чертёж: {doc.Name}")
    print(f"[OK] Загружено позиций оборудования: {len(db.get('equipment', []))}")

    print("\nИнструкция: перейдите в AutoCAD и выберите замкнутую полилинию помещения...")
    try:
        # Интерактивный запрос выбора одного объекта в AutoCAD
        doc.Utility.Prompt("\n[APS] Выберите полилинию контура помещения: ")
        sel_obj, pick_pt = doc.Utility.GetEntity()
    except Exception as e:
        print(f"[-] Объект не выбран или выбор отменен: {e}")
        return

    entity_name = sel_obj.ObjectName
    print(f"[OK] Выбран объект: {entity_name}")

    if "Polyline" not in entity_name:
        print("[!] Выбранный элемент не является полилинией (Polyline).")
        return

    raw_coords = list(sel_obj.Coordinates)
    # Преобразование плоского списка [x1, y1, x2, y2...] в список точек
    poly_points = []
    if "2d" in entity_name.lower() or "lightweight" in entity_name.lower():
        poly_points = [(raw_coords[i], raw_coords[i+1]) for i in range(0, len(raw_coords), 2)]
    else:
        poly_points = [(raw_coords[i], raw_coords[i+1]) for i in range(0, len(raw_coords), 3)]

    print(f"[OK] Считано вершин контура: {len(poly_points)}")

    # Расчет координат точек установки извещателей по нормам СП 484
    detector_coords = calculate_grid_positions(poly_points, spacing=8.0, margin=4.0)
    print(f"[OK] Рассчитано точек установки извещателей: {len(detector_coords)}")

    # Вставка маркеров/точек в чертеж
    detector_layer = "APS_ИЗВЕЩАТЕЛИ_ПРОЕКТ"
    try:
        doc.Layers.Add(detector_layer)
    except Exception:
        pass

    for pt in detector_coords:
        cad_pt = win32.VARIANT(win32.pythoncom.VT_ARRAY | win32.pythoncom.VT_R8, [pt[0], pt[1], pt[2]])
        # Вставляем окружность радиусом 0.15 м как маркер извещателя на слой
        circle = ms.AddCircle(cad_pt, 0.15)
        circle.Layer = detector_layer

    print(f"[OK] Успешно размещено {len(detector_coords)} извещателей на слое '{detector_layer}'.")

if __name__ == "__main__":
    run()
