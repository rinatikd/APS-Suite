@echo off
chcp 65001 >nul
title APS Expert — Установка автозагрузки в AutoCAD
color 0B

echo.
echo ╔══════════════════════════════════════════════════════════╗
echo ║    APS Expert — Установка автозагрузки в AutoCAD       ║
echo ╚══════════════════════════════════════════════════════════╝
echo.
echo Этот скрипт установит плагин в автозагрузку AutoCAD.
echo После этого плагин будет загружаться автоматически
echo при каждом запуске AutoCAD 2026 (команда NETLOAD не нужна).
echo.

:: Проверяем, что сборка уже выполнена
set BUILD_DIR=%USERPROFILE%\Desktop\APS_Expert_Build
if not exist "%BUILD_DIR%\ApsExpert.dll" (
    echo [ОШИБКА] Плагин не собран!
    echo Сначала запустите build.bat
    echo.
    pause
    exit /b 1
)

:: Определяем папку ApplicationPlugins для AutoCAD 2026
set APP_PLUGINS=%APPDATA%\Autodesk\ApplicationPlugins
if not exist "%APP_PLUGINS%" mkdir "%APP_PLUGINS%"

:: Имя нашего бандла
set BUNDLE_NAME=APS_Expert.bundle
set BUNDLE_DIR=%APP_PLUGINS%\%BUNDLE_NAME%

:: Удаляем старую версию, если есть
if exist "%BUNDLE_DIR%" (
    echo [i] Удаление старой версии плагина...
    rmdir /S /Q "%BUNDLE_DIR%"
)

echo [1/3] Создание структуры .bundle...
mkdir "%BUNDLE_DIR%"
mkdir "%BUNDLE_DIR%\Contents"

echo [2/3] Копирование файлов плагина...
copy /Y "%BUILD_DIR%\ApsExpert.dll" "%BUNDLE_DIR%\Contents\" >nul
copy /Y "%BUILD_DIR%\ApsExpert.pdb" "%BUNDLE_DIR%\Contents\" >nul
copy /Y "%BUILD_DIR%\QuestPDF.dll" "%BUNDLE_DIR%\Contents\" >nul
xcopy /Y /E /I "%BUILD_DIR%\RulePack" "%BUNDLE_DIR%\Contents\RulePack" >nul

echo [3/3] Создание PackageContents.xml...

:: Создаём PackageContents.xml — это манифест, который AutoCAD читает при загрузке
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<ApplicationPackage SchemaVersion="1.0" AppVersion="0.6-RK"
echo     ProductType="Application" ProductCode="{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
echo     Name="APS Expert" Description="Интеллектуальный экспертный модуль для АПС"^>
echo   ^<CompanyDetails Name="Rinat" /^^>
echo   ^<Components Description="APS Expert Core"^>
echo     ^<ComponentEntry LoadOnAutoCADStartup="True"
echo         ModuleName="./Contents/ApsExpert.dll"
echo         AppName="APS_Expert"^>
echo     ^</ComponentEntry^>
echo   ^</Components^>
echo ^</ApplicationPackage^>
) > "%BUNDLE_DIR%\PackageContents.xml"

echo.
echo ╔══════════════════════════════════════════════════════════╗
echo ║  УСПЕШНО! Плагин установлен в автозагрузку.            ║
echo ║                                                        ║
echo ║  Расположение:                                         ║
echo ║  %BUNDLE_DIR%
echo ║                                                        ║
echo ║  Теперь при запуске AutoCAD 2026 плагин будет          ║
echo ║  загружаться автоматически.                            ║
echo ║                                                        ║
echo ║  Команды плагина:                                      ║
echo ║    APS_SET_ROOM_TYPE, APS_SET_INTEGRATION,             ║
echo ║    APS_AUDIT, APS_PANEL, APS_MARK, APS_FIX,            ║
echo ║    APS_EXPORT                                            ║
echo ╚══════════════════════════════════════════════════════════╝
echo.
echo Для удаления плагина просто удалите папку:
echo %BUNDLE_DIR%
echo.

:: Открываем папку ApplicationPlugins
explorer "%APP_PLUGINS%"

pause