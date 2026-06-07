@echo off
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0go-live-check.ps1" %*
