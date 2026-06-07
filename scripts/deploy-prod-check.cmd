@echo off
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy-prod-check.ps1" %*
