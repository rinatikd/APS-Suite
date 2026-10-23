# Архитектура APS Expert

```text
AutoCAD DWG
    |
    v
CAD Adapter / DWG Reader
    |
    v
Semantic Extraction
    |
    +--> Geometry Model
    +--> Room Model
    +--> Device Model
    +--> Cable/Connection Model
    |
    v
Engineering Model
    |
    +--> Rule Engine --------> Issue Store
    |                              |
    |                              v
    |                         AutoCAD Markup
    |
    +--> Design Engine ------> Proposed Changes
    |                              |
    |                              v
    |                         Change Preview
    |                              |
    |                              v
    |                         Apply to DWG
    |
    +--> Reference Projects
    +--> Normative Database
    |
    v
AI Engineering Assistant
    |
    +--> Explain issue
    +--> Find analogous solution
    +--> Suggest redesign
    +--> Generate audit report
```

## Слои
### 1. CAD Adapter
Изолирует API AutoCAD от остального приложения.

### 2. Domain
Независимые сущности: Room, FireDetector, FireAlarmPanel, CableRun, Issue, Rule, ProposedChange.

### 3. Rule Engine
Детерминированные проверки. Каждое правило имеет ID, версию, нормативный источник, применимость, severity и вычисление.

### 4. Design Engine
Строит варианты исправлений. Изменение DWG выполняется только после явного подтверждения пользователя в MVP.

### 5. Knowledge Base
Нормативные документы РК, технические паспорта, типовые решения и эталонные проекты с указанием источника.

### 6. AI Layer
Не принимает нормативное решение сам. Работает поверх структурированной модели и результатов Rule Engine.
