@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0docker-validate.ps1" %*
