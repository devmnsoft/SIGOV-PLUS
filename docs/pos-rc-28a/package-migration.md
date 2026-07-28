# Migração de pacotes

Central package management remains in `Directory.Packages.props`. The direct runtime boundary was moved from Npgsql 6 to Npgsql 10, Serilog.AspNetCore to 10, and every direct Microsoft.AspNetCore/Microsoft.Extensions 6.x reference to 10.0.0. Dapper remains unchanged and Entity Framework was not introduced.

A dependency report and lock-file regeneration require the unavailable .NET SDK. They must not be represented as completed until `dotnet restore`, outdated/vulnerable reports and locked restore succeed.
