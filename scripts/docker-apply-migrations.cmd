@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0docker-apply-migrations.ps1" %*
