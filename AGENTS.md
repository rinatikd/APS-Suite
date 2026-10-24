# APS-Suite: Architectural Context for AI Agents

## Domain & Standards
- Automated design of fire alarm systems (APS/ПС/СОУЭ) according to Russian fire safety code **SP 484.1311500.2020**.
- Multi-CAD/BIM platform: AutoCAD, ZWCAD, Autodesk Revit (via pyRevit).

## Clean Architecture
1. **APS-Suite/src/APS.Core**: Independent class library targeting \
etstandard2.0\ and \
et8.0\.
   - Pure math, regulatory rules (SP 484 Table 1), and electrical loop calculations.
   - Zero dependencies on Revit or AutoCAD/ZWCAD SDKs.
2. **APS-Suite/database/aps_equipment_db.json**: Single source of truth for equipment specs, CAD block names, and Revit family types.
3. **APS-Suite/src/APS.CAD**: Platform adapter connecting via COM/ActiveX (\win32com\) to active AutoCAD/ZWCAD instances.
4. **aps-revit/APS.extension**: pyRevit bridge loading \APS.Core.dll\ dynamically via \clr.AddReference\.

## Contribution Rules for AI
- Always keep \APS.Core\ pure and free of UI or CAD runtime references.
- Verify building with \dotnet build APS-Suite/src/APS.Core/APS.Core.csproj\.
