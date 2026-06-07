@echo off
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0package-release.ps1" %*
