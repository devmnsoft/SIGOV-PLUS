@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0docker-stop.ps1" %*
