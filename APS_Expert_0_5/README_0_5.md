# APS Expert 0.5

**Integrated Project Analyzer for AutoCAD + RubezhCAD/R-CAD**

## Main command
`APS_PROJECT_AUDIT`

The command builds one analysis context from:
- AutoCAD geometry;
- R-CAD project data;
- R-CAD ↔ DWG handle mapping;
- configurable engineering criteria.

Then it runs integrated rules and produces evidence-backed findings.

## Important engineering policy
APS Expert does **not** call a geometry threshold a Kazakhstan regulatory requirement unless the applicable standard/edition/clause is explicitly verified and placed into a versioned Rule Pack.

The included Rule Pack is a technical baseline only. Its 250 devices / 3000 m values are criteria carried forward from the 0.4 reference analysis, not a substitute for verified current regulatory documentation.

## Commands
- `APS_PROJECT_AUDIT` — full integrated analysis
- `APS_SCAN` — DWG model extraction
- `APS_MAP_RUBEZH` — R-CAD/DWG mapping
- `APS_GEOMETRY_AUDIT` — geometry-only analysis
- `APS_ISSUES` — detailed findings
- `APS_EXPORT` — JSON evidence report
- `APS_MARK` — placeholder for visual QA
- `APS_FIX` — intentionally disabled until transactional redesign exists

## Build
AutoCAD 2026 Managed API references are expected through `AUTOCAD_2026_DIR` or the default installation path.
