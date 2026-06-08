@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0docker-reset.ps1" %*
