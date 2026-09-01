# RC50.93 — build, validações, mensagens e design

## Base e objetivo

Foi utilizada a **BASE LOCAL**, pois o checkout disponibilizado não possui remote `origin` nem
referência `origin/main`. A correção estabiliza os pontos P0 identificáveis no ambiente e registra
o padrão único para a continuidade da auditoria de telas.

## Correções aplicadas

- Os controllers API/Web e os serviços afetados agora resolvem explicitamente
  `Sigov.Application.Authorization.IAuthorizationEvaluator`. A avaliação persistida e o
  comportamento fail-closed foram preservados; a interface do ASP.NET Core não a substitui.
- Corrigido o fechamento prematuro do método de transição de ordem de serviço em Frotas. O bloco
  extra encerrava o método antes da atualização/auditoria e provocava erros de sintaxe em cascata.
- Revisados visualmente os repositórios Ambiental, Atendimento, Convênios, Defesa, Fiscalização e
  Habitação quanto a delimitadores, raw strings, inicializadores e `CommandDefinition`. Nenhuma
  alteração de schema foi necessária nesta correção; portanto, migrations e scripts agregados
  permanecem inalterados e sincronizados com seus checksums publicados.
- Documentado o padrão de formulário, notificação, confirmação e mini manual para adoção uniforme.

## Validação e limitações reais

Comandos executados: `dotnet build`, buscas `rg` direcionadas, `git diff --check`, inspeção do
manifest e inspeção dos arquivos apontados. O SDK definido em `global.json` não está instalado no
contêiner (`dotnet: command not found`) e o endpoint de instalação respondeu HTTP 403.

BLOCKED: comando `dotnet build` não executado porque o executável `dotnet` não está instalado no ambiente e a instalação do SDK .NET 10 foi recusada pelo endpoint externo com HTTP 403.

BLOCKED: comandos de validação PostgreSQL e smoke das rotas não executados porque não há instância PostgreSQL nem aplicação compilada disponível neste ambiente.

BLOCKED: revisão visual com screenshot não executada porque a aplicação não pode ser iniciada sem o runtime .NET 10.

BLOCKED: comandos `git fetch origin`, push, abertura/merge de PR e pull final não executados porque o repositório não possui remote configurado.

## Continuidade

Em ambiente com SDK 10.0.100, executar `dotnet build`, validadores SQL/checksum do repositório e
smoke autenticado. A auditoria integral das mais de mil views deve seguir o checklist documentado,
sem substituição por conteúdo genérico ou decorativo.

