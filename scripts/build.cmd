@echo off
dotnet restore sigov.sln && dotnet build sigov.sln --no-restore
