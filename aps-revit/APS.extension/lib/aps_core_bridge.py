# -*- coding: utf-8 -*-
"""
aps_core_bridge.py - Мост между pyRevit и независимым ядром APS.Core.dll
"""
import sys
import os
import clr

CORE_DLL_DIR = os.path.abspath(os.path.join(
    os.path.dirname(__file__), "..", "..", "..", "APS-Suite", "src", "APS.Core", "bin", "Debug", "netstandard2.0"
))
CORE_DLL_PATH = os.path.join(CORE_DLL_DIR, "APS.Core.dll")

if os.path.exists(CORE_DLL_PATH):
    clr.AddReferenceToFileAndPath(CORE_DLL_PATH)
    import APS.Core as ApsCore
    HAS_CORE = True
else:
    HAS_CORE = False

def is_core_available():
    return HAS_CORE
