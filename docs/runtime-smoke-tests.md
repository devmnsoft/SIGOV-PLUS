# Runtime smoke tests

## Sprint setorial — 2026-07-02

### Validação inicial obrigatória

- `dotnet restore`: não executado com sucesso neste container porque o comando `dotnet` não está instalado.
- `dotnet build`: não executado com sucesso neste container porque o comando `dotnet` não está instalado.
- `docker compose down`: não executado com sucesso neste container porque o comando `docker` não está instalado.
- `docker compose up -d --build`: não executado com sucesso neste container porque o comando `docker` não está instalado.
- `docker compose ps`: não executado com sucesso neste container porque o comando `docker` não está instalado.

### Rotas a validar em ambiente completo

`/Auth/Login`, `/Dashboard`, `/MinhaCentral`, `/Educacao`, `/Saude`, `/Acs`, `/Saneamento`, `/Social`, `/Agro`, `/PortalCidadao`, `/PortalContribuinte`, `/Ouvidoria`, `/MobileCampo`, `/Gis`, `/BiSetorial`, `/Busca?q=teste`, `/Relatorios`, `/Operacao/Health` e `http://localhost:5001/api/health/live`.
