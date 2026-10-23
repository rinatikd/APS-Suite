# -*- coding: utf-8 -*-
"""
APS.CAD Adapter - COM/ActiveX интеграция с AutoCAD и ZWCAD.
Подключается к активному чертежу, считывает контуры помещений и расставляет извещатели по СП 484.
"""
import os
import json
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
        with open(EQUIPMENT_DB_PATH, "r", encoding="utf-8") as f:
            return json.load(f)
    return {"equipment": []}

def insert_detector_block(ms, point, block_name, attributes=None):
    import array
    insertion_pt = win32.VARIANT(win32.pythoncom.VT_ARRAY | win32.pythoncom.VT_R8, point)
    block_ref = ms.InsertBlock(insertion_pt, block_name, 1.0, 1.0, 1.0, 0.0)
    if attributes and block_ref.HasAttributes:
        for att in block_ref.GetAttributes():
            tag = att.TagString.upper()
            if tag in attributes:
                att.TextString = str(attributes[tag])
                att.Update()
    return block_ref

def run():
    app = get_cad_app()
    doc = app.ActiveDocument
    if not doc:
        print("[!] Нет активного открытого чертежа.")
        return

    ms = doc.ModelSpace
    db = load_equipment_db()
    print(f"[OK] Чертёж: {doc.Name}")
    print(f"[OK] Загружено позиций оборудования из базы: {len(db.get('equipment', []))}")

if __name__ == "__main__":
    run()
