@echo off
setlocal
:loop
if "%~1"=="" goto end
echo [ARG] %~1
shift
goto loop
:end
