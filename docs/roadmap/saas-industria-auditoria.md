# Auditoria técnica — SaaS multi-tenant e Indústria 360

Data de corte: 2026-09-08. Branch: `codex/evolucao-saas-industria-360`.

## Inventário inicial

| Área | Evidência encontrada | Classificação inicial |
| --- | --- | --- |
| Tenancy | `tenant`, contexto por claims/headers e filtros por `tenant_id` já existiam; a Web não preenchia `ICurrentTenant` de modo uniforme | Parcial |
| Contratação | `tenant_modulo`, `tenant_modulo_contratado` e `saas_cliente_modulo` coexistiam; filtros de API consultavam a estrutura legada | Duplicado/inconsistente |
| Catálogo | `modulo_saas` coexistia com dois catálogos estáticos de aplicação | Parcial; banco não era autoridade em todos os fluxos |
| Autorização | avaliador persistido existia, mas políticas Web enviavam recurso qualificado incorreto; APIs industriais tinham bypass para requisição anônima | Crítico |
| Login | apenas login/e-mail; duplicidade escolhia um registro por ordenação | Inseguro para multiempresa |
| Cookie | correção recente já mantinha permissões fora do ticket e recarregava por requisição | Adequado, com falha de contexto corrigida nesta branch |
| SaaS Admin | dashboard operacional existente; telas de tenants, módulos, planos, assinaturas e uso continham números e ações estáticas | Placeholder |
| Indústria | schema, endpoints e regras essenciais já existiam; páginas canônicas exibiam linha `DEMO` e botões sem operação | Backend parcial; frontend placeholder |
| Integrações | estoque, comercial e OS possuíam pontos de integração; não havia saga/outbox industrial consolidada | Parcial |
| Testes | regressões estáticas, unidade e contrato Swagger já existiam | Parcial; sem banco real nesta estação até validar a instância |

## Autoridade canônica adotada

- Catálogo comercial: `sigov.modulo_saas`.
- Contratação por organização: `sigov.tenant_modulo_contratado`.
- `sigov.tenant_modulo` permanece somente como compatibilidade unidirecional, alimentada pela autoridade canônica.
- Permissões: `sigov.permissao` + vínculos persistidos de grupos, perfis e escopos.
- Contexto operacional: `tenant_id`, `entidade_id` e `exercicio_id` originados da sessão autenticada; headers não substituem identidade confiável.
- Identidade: `sigov.usuario`, com CPF/CNPJ relacionados às estruturas oficiais já existentes.

## Corrigido nesta entrega

- Normalização e validação de CPF/CNPJ, e-mail e login legado antes da consulta.
- Resolução determinística de ambiguidade: credenciais são validadas antes de revelar organizações e a organização é selecionada explicitamente.
- Logs e auditoria deixaram de registrar o identificador bruto; usam tipo e SHA-256.
- Alias globais foram normalizados para `ADMINISTRADOR_GERAL`, preservando compatibilidade sem transformar nome de perfil em autorização.
- Claims de permissão/módulo são carregadas uma vez por requisição e nunca persistidas no cookie compacto.
- A transformação de claims usa o tenant da identidade autenticada, eliminando o contexto nulo da Web.
- Licenciamento e `RequireModule` consultam `tenant_modulo_contratado`, com vigência, status do tenant e estados comerciais fail-closed.
- O menu mostra Indústria 360 apenas com contrato e permissão; Administração SaaS aparece somente no contexto global autorizado.
- O avaliador de políticas Web separa corretamente módulo, recurso e ação.
- APIs industriais deixaram de autorizar usuário anônimo e passaram a consultar o avaliador persistido para leituras e comandos.
- Rotas reais de listagem de paradas e custos foram incluídas; a tela industrial removeu a linha `DEMO` e passou a tratar carregamento, vazio, erro, filtro e paginação.
- A migration corretiva adiciona preço, trial, limites, vigência, desconto, renovação, dependências, incompatibilidades e histórico imutável de contratação.
- O catálogo canônico foi atualizado para `industria_producao` / **Indústria 360**, sem prometer MRP avançado já entregue.

## Dívida técnica remanescente

| Prioridade | Item | Consequência | Direção |
| --- | --- | --- | --- |
| P0 | A API ainda não possui um esquema de autenticação completo e uniforme para todos os controllers | Endpoints fora do gateway podem não receber principal autenticado | Definir JWT/OIDC ou autenticação interna e testar 401/403 ponta a ponta |
| P0 | Telas SaaS Admin secundárias ainda são placeholders | Operação comercial não está completa | Substituir por CRUDs Dapper reais, com autorização global e auditoria |
| P1 | Catálogos estáticos duplicados permanecem para metadados de UI | Risco de divergência com o banco | Trocar por projeção cacheada de `modulo_saas`; falhar explicitamente se o schema faltar |
| P1 | Sidebar de módulos públicos ainda possui grupos históricos incondicionais | Menu não reflete integralmente contratos | Migrar cada grupo para metadados/permissões persistidos |
| P1 | Não existe sessão persistida/revogável; `session_id` é somente claim | Logout global e revogação imediata não estão completos | Criar sessão com hash de token, expiração, revogação e trilha |
| P1 | CRUD industrial Web cobre consulta, mas formulários especializados continuam pendentes | Jornada operacional exige API/Swagger para escrita | Implementar telas por recurso conforme Sprint 05–08 |
| P1 | Pausar/cancelar OP reutilizam permissões existentes (`iniciar`/`concluir`) | Granularidade inferior ao desejado | Criar permissões próprias em migration corretiva futura |
| P2 | Estruturas enterprise UUID e core bigint coexistem | Mapeamento eleva complexidade | Isolar legado e migrar consumidores, sem conversão destrutiva de UUID |
| P2 | Limites comerciais foram modelados, mas enforcement de usuários/armazenamento/requisições é parcial | Contrato não bloqueia todos os excessos | Medição transacional e alertas antes do bloqueio configurável |

## Bloqueios e validações dependentes do ambiente

- O primeiro build local encontrou artefatos `obj` bloqueados por processos `dotnet` preexistentes; a validação deve usar diretório de artifacts isolado.
- A aplicação da migration e os testes de integração exigem PostgreSQL 16+ acessível por `ConnectionStrings__DefaultConnection`. Ausência de banco é `BLOCKED`, nunca sucesso simulado.
- Não foram usados segredos, dados pessoais reais ou seeds demonstrativos nesta entrega.

## Evidências de validação da branch

- `dotnet restore sigov.sln --artifacts-path .artifacts/codex-saas-industria`: concluído.
- `dotnet build sigov.sln --no-restore --artifacts-path .artifacts/codex-saas-industria`: concluído com 0 erros e 0 avisos.
- Regressões focadas de API/autenticação/módulos/Indústria: 19 de 19 aprovadas.
- Testes unitários focados de licenciamento: 5 de 5 aprovados.
- Geração e verificação dos scripts PostgreSQL completos: aprovadas, com checksum do manifest conferido.
- Suite unitária completa: 364 aprovados e 7 falhas preexistentes, fora desta alteração.
- Suite de API completa: 80 aprovados e 18 falhas preexistentes, fora desta alteração.
- Aplicação e reaplicação em PostgreSQL real: `BLOCKED`, pois a estação não fornece `ConnectionStrings__DefaultConnection` nem senha da instância local. Nenhum sucesso de banco foi simulado.

## Próximo sprint recomendado

1. Autenticação ponta a ponta da API e testes negativos 401/403 entre tenants.
2. CRUD real do SaaS Admin para tenants, catálogo, contratação, suspensão, trial, preço e histórico.
3. Sessão persistida e revogável, com troca de contexto global auditada.
4. Formulários especializados da Indústria 360, começando por cadastros, BOM e roteiro.
5. Eliminação progressiva dos catálogos estáticos e menus incondicionais.

## Fora do escopo desta entrega

- MRP avançado, MES completo, manutenção preditiva, NF-e/NFS-e real, folha oficial completa, integrações bancárias reais e contabilidade industrial avançada.
- Conversão destrutiva das chaves UUID legadas.
- Alteração de migrations publicadas ou criação de novas classes de teste.
