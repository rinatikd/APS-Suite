# APS Expert — состояние работы

**Дата последней сессии:** 2026-10-05
**Версия в разработке:** 0.9
**Статус:** код готов, прогон не выполнен

## Что сделано

### Core (ApsExpert.Core)

- Domain: AuditIssue, EngineeringModel, CadHandle, CadPoint, ProposedChange.
- Analysis: RulePack, RuleDefinition, CheckDefinition, JsonProjectRule,
  IntegratedRuleEngine, RulePackLoader, RulePackValidator, RulePackSanity,
  RulePackDiff, DataValidator, EngineVersion.
- Reference: RubezhProjectSnapshot.
- Geometry: GeometrySnapshot.
- Services: HtmlReportBuilder, CsvReportBuilder, AuditSnapshot, AuditDiff,
  ExpertSettings.
- Независимо от AutoCAD. Собирается на `net8.0` на любой ОС.

### Тесты (ApsExpert.Core.Tests)

- ~125 тестов.
- Не требуют AutoCAD.
- Команда: `dotnet test src/ApsExpert.Core.Tests/ApsExpert.Core.Tests.csproj`

### AutoCAD-слой (ApsExpert)

- Commands: APS_SCAN, APS_AUDIT, APS_ISSUES, APS_LIST, APS_GOTO, APS_STATUS,
  APS_DASHBOARD, APS_RULES, APS_RULES_RELOAD, APS_VALIDATE, APS_VALIDATE_DATA,
  APS_COMPARE_RULEPACKS, APS_SETTINGS, APS_SETTINGS_INIT, APS_SET_ROOM_HEIGHT,
  APS_SHOW_ROOM_HEIGHT, APS_FIX_PREVIEW, APS_FIX_APPLY, APS_FIX_APPLY_SELECT,
  APS_FIX_ROLLBACK, APS_REPORT_HTML, APS_REPORT_CSV, APS_SNAPSHOT, APS_DIFF,
  APS_DIFF_LAST, APS_SELFTEST, APS_EXPORT.
- Adapters: AcadAdapters (ObjectId ↔ CadHandle, Point3d ↔ CadPoint).
- Services: CadModelExtractor, UnifiedModelBuilder, AuditMarkerService,
  FixJournal, RoomHeightStore.
- Reference: RubezhProjectReader, RubezhCadMapper.
- Rules: MappingIntegrityRule, DetectorRoomIntegrityRule, RoomDetectorCoverageRule.
- Geometry: CadGeometryExtractor.

### Нормативные правила

- СП РК 2.02-102-2023 (табл. 14, 17 и др.).
- СН РК 2.02-02-2023.
- R3-Рубеж-2ОП (технические пределы).

### Документация

- USER_GUIDE.md, DEVELOPER_GUIDE.md, ARCHITECTURE.md, JSON_SCHEMA.md,
  NORMATIVE_PACKS.md, ROADMAP.md, API_STABILITY.md, KNOWN_LIMITATIONS.md.

### CI/CD

- test.yml: dotnet format + dotnet test для Core.
- validate.yml: JSON Schema + markdownlint + links.
- build-core.yml: собирает ApsExpert.Core.dll + nupkg.
- build-plugin.yml: собирает плагин (требует self-hosted runner с AutoCAD).

### Сборка

- Локально: `.\build.ps1` → папка `publish/` (ApsExpert.dll + RulePack + docs).
- Важно: плагин AutoCAD — это всегда .dll, не .exe.

## Что НЕ сделано и требует прогона

### Критично

- Ни разу не запускался ни `dotnet build`, ни `dotnet test`, ни AutoCAD.
- Возможны ошибки компиляции в Core (проверить первым делом).
- Возможны ошибки в AutoCAD-слое при первом NETLOAD.

### Проверка при первом запуске

1. `dotnet build src/ApsExpert.Core/ApsExpert.Core.csproj`
   — должно собраться без ошибок.

2. `dotnet test src/ApsExpert.Core.Tests`
   — ~125 тестов должны быть зелёными.

3. `.\build.ps1`
   — должен собрать плагин, если AutoCAD 2026 установлен.

4. В AutoCAD: NETLOAD → `APS_SELFTEST`
   — должно показать 20 проверок, все OK.

5. `APS_AUDIT` на реальном проекте RC_ОПС45
   — сравнить с ожиданиями.

## Известные ограничения

- `Room.HeightM` может быть 0 (не заполняется). Решение: `APS_SET_ROOM_HEIGHT`
  или доработка R-CAD reader.
- Правила Табл. 14, 17 зависят от высоты помещения — без неё не сработают.
- Часть правил отключена (`Enabled: false`) — см. KNOWN_LIMITATIONS.md.

## Что делать при возвращении

1. Открыть репозиторий на компьютере с AutoCAD 2026 и .NET 8 SDK.
2. `git pull` — забрать последние изменения.
3. Прочитать этот файл + CHANGELOG.md + KNOWN_LIMITATIONS.md.
4. Сначала Core: `dotnet build` + `dotnet test` — 2 минуты.
5. Если Core OK — `.\build.ps1` → NETLOAD → `APS_SELFTEST`.
6. Скинуть вывод `APS_SELFTEST` и любые ошибки сборки.
7. По результатам — чинить баги.

## Как восстановить контекст в новом чате

При открытии нового чата с ассистентом (Claude/ChatGPT/Copilot) —
приложить:

- Этот файл (`docs/SESSION_NOTES.md`).
- `CHANGELOG.md`.
- `README.md`.
- `docs/ARCHITECTURE.md`.
- Вывод `dotnet build` и `APS_SELFTEST` (если уже есть).

Этого достаточно, чтобы ассистент за минуту понял, где мы находимся.
