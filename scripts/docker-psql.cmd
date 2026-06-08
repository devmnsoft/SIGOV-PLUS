@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0docker-psql.ps1" %*
