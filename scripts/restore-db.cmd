@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0restore-db.ps1" %*
