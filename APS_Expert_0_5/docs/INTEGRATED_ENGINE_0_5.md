# APS Expert 0.5 — Integrated Engineering Engine

## Principle
The analyzer separates four things:
1. **Fact** — what is actually present in DWG/R-CAD.
2. **Mapping** — whether the R-CAD object is reliably linked to the DWG object.
3. **Criterion** — a configurable engineering criterion from a Rule Pack.
4. **Conclusion** — PASS/INFO/WARNING/ERROR with evidence and suggested action.

No geometry threshold is treated as a Kazakhstan regulatory requirement unless it is explicitly loaded into a verified Rule Pack.

## Pipeline

`DWG + R-CAD → extraction → handle mapping → unified context → rules → evidence → report`

## Next mandatory stage
The next Rule Pack must contain the exact applicable editions and clauses for:
- detector placement and coverage;
- room classification;
- manual call points;
- sounder/voice alarm coverage;
- cable fire resistance and routing;
- loops/ALS and control lines;
- interfaces with fire alarm panels;
- equipment compatibility.

Each normative rule should have: `RuleId`, `Edition`, `Clause`, `Applicability`, `Inputs`, `Calculation`, `Severity`, `Evidence`.
