@echo off
chcp 65001 >nul
title APS Expert — Сборка плагина
color 0A

echo.
echo ╔══════════════════════════════════════════════════════════╗
echo ║         APS Expert — Автоматическая сборка             ║
echo ╚══════════════════════════════════════════════════════════╝
echo.

:: Проверка наличия .NET SDK
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [ОШИБКА] .NET SDK не найден!
    echo.
    echo Скачайте и установите .NET 8 SDK:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

echo [1/4] Проверка .NET SDK... OK
dotnet --version

:: Проверка наличия AutoCAD 2026
if not exist "C:\Program Files\Autodesk\AutoCAD 2026\AcCoreMgd.dll" (
    echo.
    echo [ПРЕДУПРЕЖДЕНИЕ] AutoCAD 2026 не найден в стандартной папке.
    echo Если AutoCAD установлен в другом месте, задайте переменную AUTOCAD_2026_DIR
    echo и повторите сборку.
    echo.
    set /p CUSTOM_PATH="Введите путь к AutoCAD 2026 (или Enter для продолжения): "
    if not "%CUSTOM_PATH%"=="" set "AUTOCAD_2026_DIR=%CUSTOM_PATH%"
)

echo.
echo [2/4] Сборка проекта...
echo.

:: Сборка
dotnet build src\ApsExpert\ApsExpert.csproj -c Release
if %errorlevel% neq 0 (
    echo.
    echo [ОШИБКА] Сборка не удалась!
    pause
    exit /b 1
)

echo.
echo [3/4] Копирование результата...

:: Создаём папку для результата на рабочем столе
set OUTPUT_DIR=%USERPROFILE%\Desktop\APS_Expert_Build
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

:: Копируем все нужные файлы
set SOURCE_DIR=src\ApsExpert\bin\Release\net8.0-windows
copy /Y "%SOURCE_DIR%\ApsExpert.dll" "%OUTPUT_DIR%\" >nul
copy /Y "%SOURCE_DIR%\ApsExpert.pdb" "%OUTPUT_DIR%\" >nul
copy /Y "%SOURCE_DIR%\QuestPDF.dll" "%OUTPUT_DIR%\" >nul
xcopy /Y /E /I "%SOURCE_DIR%\RulePack" "%OUTPUT_DIR%\RulePack" >nul

:: Копируем нормативный JSON из docs (если есть)
if exist "docs\rule-pack\APS_Expert_Design_Criteria_0_6.json" (
    if not exist "%OUTPUT_DIR%\RulePack" mkdir "%OUTPUT_DIR%\RulePack"
    copy /Y "docs\rule-pack\APS_Expert_Design_Criteria_0_6.json" "%OUTPUT_DIR%\RulePack\" >nul
)

echo.
echo [4/4] Готово!
echo.
echo ╔══════════════════════════════════════════════════════════╗
echo ║  УСПЕШНО! Плагин собран.                               ║
echo ║                                                        ║
echo ║  Папка с результатом:                                  ║
echo ║  %OUTPUT_DIR%
echo ║                                                        ║
echo ║  Файлы:                                                ║
echo ║    - ApsExpert.dll        (основной плагин)            ║
echo ║    - QuestPDF.dll         (для PDF-отчётов)            ║
echo ║    - RulePack\            (нормативная база)           ║
echo ╚══════════════════════════════════════════════════════════╝
echo.
echo Далее запустите install-autoloader.bat для автоматической
echo загрузки плагина в AutoCAD при каждом запуске.
echo.

:: Открываем папку с результатом
explorer "%OUTPUT_DIR%"

pause