FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Sigov.Domain/Sigov.Domain.csproj src/Sigov.Domain/
COPY src/Sigov.Application/Sigov.Application.csproj src/Sigov.Application/
COPY src/Sigov.Infrastructure/Sigov.Infrastructure.csproj src/Sigov.Infrastructure/
COPY src/Sigov.Api/Sigov.Api.csproj src/Sigov.Api/
COPY src/Sigov.Web/Sigov.Web.csproj src/Sigov.Web/
COPY src/Sigov.Worker/Sigov.Worker.csproj src/Sigov.Worker/

RUN dotnet restore src/Sigov.Web/Sigov.Web.csproj

COPY . .
RUN dotnet publish src/Sigov.Web/Sigov.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && adduser --disabled-password --gecos "" --uid 10001 sigov \
    && mkdir -p /app/storage \
    && chown -R sigov:sigov /app

COPY --from=build --chown=sigov:sigov /app/publish .

USER sigov
EXPOSE 8080

ENTRYPOINT ["dotnet", "Sigov.Web.dll"]
