FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore sigov.sln
RUN dotnet publish src/Sigov.Api/Sigov.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY database/postgres/migrations /app/database/postgres/migrations
ENTRYPOINT ["dotnet", "Sigov.Api.dll"]
