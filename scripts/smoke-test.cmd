@echo off
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0smoke-test.ps1" %*
