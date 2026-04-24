@echo off
setlocal

set "ROOT=%~dp0"
set "PUBLISH=%ROOT%publish"

cd /d "%ROOT%FuckingGreatAdvice"

taskkill /IM FuckingGreatAdvice.exe /F >nul 2>&1
ping -n 2 127.0.0.1 >nul

rem Чистим промежуточные артефакты перед publish (иногда файлы в bin блокируются).
if exist "bin" rmdir /s /q "bin" >nul 2>&1
if exist "obj" rmdir /s /q "obj" >nul 2>&1

rem Не запускать этот же target снова внутри publish (иначе рекурсия).
dotnet publish "%ROOT%FuckingGreatAdvice\FuckingGreatAdvice.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DeleteExistingFiles=true -p:RunReleaseBatAfterBuild=false --nologo -v q -o "%PUBLISH%"
if errorlevel 1 (
    echo BUILD FAILED
    exit /b 1
)

set "EXE=%PUBLISH%\FuckingGreatAdvice.exe"

if not exist "%EXE%" (
    echo ERROR: %EXE% not found
    exit /b 1
)

start "" "%EXE%"
