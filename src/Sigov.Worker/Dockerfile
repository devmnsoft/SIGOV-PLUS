FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY sigov.sln ./
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Sigov.Domain/Sigov.Domain.csproj src/Sigov.Domain/
COPY src/Sigov.Application/Sigov.Application.csproj src/Sigov.Application/
COPY src/Sigov.Infrastructure/Sigov.Infrastructure.csproj src/Sigov.Infrastructure/
COPY src/Sigov.Api/Sigov.Api.csproj src/Sigov.Api/
COPY src/Sigov.Web/Sigov.Web.csproj src/Sigov.Web/
COPY src/Sigov.Worker/Sigov.Worker.csproj src/Sigov.Worker/
COPY tests/Sigov.UnitTests/Sigov.UnitTests.csproj tests/Sigov.UnitTests/
COPY tests/Sigov.IntegrationTests/Sigov.IntegrationTests.csproj tests/Sigov.IntegrationTests/
COPY tests/Sigov.ApiTests/Sigov.ApiTests.csproj tests/Sigov.ApiTests/

RUN dotnet restore sigov.sln

COPY . .
RUN dotnet publish src/Sigov.Worker/Sigov.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app

ENV DOTNET_RUNNING_IN_CONTAINER=true

RUN adduser --disabled-password --gecos "" --uid 10001 sigov \
    && mkdir -p /app/storage \
    && chown -R sigov:sigov /app

COPY --from=build --chown=sigov:sigov /app/publish .

USER sigov
ENTRYPOINT ["dotnet", "Sigov.Worker.dll"]
