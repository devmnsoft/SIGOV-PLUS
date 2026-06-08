@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0docker-logs.ps1" %*
