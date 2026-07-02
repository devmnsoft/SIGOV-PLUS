# Runtime smoke tests

## Validação inicial/final neste ambiente

- `dotnet restore`: não executado com sucesso porque o binário `dotnet` não está instalado no container do agente.
- `dotnet build`: bloqueado pelo mesmo motivo.
- `docker compose down` / `docker compose ps`: não executados com sucesso porque o binário `docker` não está instalado no container do agente.
- Rotas HTTP locais: não executadas porque Docker/.NET não estão disponíveis neste ambiente.

Os comandos obrigatórios permanecem documentados para execução local/CI com SDK .NET 6 e Docker disponíveis.

## Tentativa de rotas HTTP

As rotas `Auth/Login`, `Dashboard`, `MinhaCentral`, `Workflow`, `Tarefas`, `Notificacoes`, `Agenda`, `Integracoes`, `Bi`, `MobileCampo`, `Busca`, `Relatorios`, `Operacao/Health` e `api/health/live` foram tentadas via `curl -I`, mas não havia servidor local em execução porque Docker/.NET não estão disponíveis no container.
