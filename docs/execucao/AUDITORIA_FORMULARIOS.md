# Auditoria de formulários — checkpoint 2026-09-15

## Escopo, método e limite da evidência

Este checkpoint iniciou o inventário diretamente em `src/Sigov.Web`, sem promover documentação histórica a evidência. A unidade de inventário é um arquivo Razor ou JavaScript que contém ao menos um indício de jornada: formulário, requisição assíncrona, modal, upload, linha dinâmica, ação de ciclo de vida, filtro, paginação, impressão ou exportação. Assim, um arquivo pode conter mais de um formulário e aparecer em mais de uma categoria; a próxima passagem deve decompor esses candidatos em jornadas individuais.

Fluxo exigido para aprovação: **tela → requisição → binding → validação → autorização → serviço → SQL → commit → nova leitura independente → tela**. Resposta 2xx, toast e fechamento de modal não são evidência de persistência.

O ambiente desta execução não possui `dotnet`, `psql`, Docker nem `ConnectionStrings__DefaultConnection` de banco isolado. Nenhuma operação foi promovida a `aprovado` ponta a ponta e nenhum banco habitual ou de produção foi acessado.

## Cobertura real

| Medida | Quantidade | Estado |
|---|---:|---|
| Arquivos candidatos identificados | 591 (459 Razor; 132 JavaScript) | não verificado |
| Contêm `<form>` | 278 | não verificado |
| Contêm `fetch`, AJAX ou `XMLHttpRequest` | 96 | não verificado |
| Contêm modal | 59 | não verificado |
| Contêm linhas/conteúdo dinâmico | 36 | não verificado |
| Contêm upload | 4 | não verificado |
| Contêm ação de excluir/inativar/cancelar/aprovar/estornar | 150 | não verificado |
| Contêm consulta/filtro/paginação/impressão/exportação | 303 | não verificado |
| Jornadas aprovadas ponta a ponta | 0 | bloqueado |
| Candidatos pendentes de decomposição e prova específica | 591 | não verificado |

Os números acima não devem ser somados para obter formulários, pois as categorias se sobrepõem. O inventário reproduzível usa busca de conteúdo, não apenas `<form>`.

## Distribuição dos 459 candidatos Razor

| Módulo/pasta | Qtde. | Módulo/pasta | Qtde. | Módulo/pasta | Qtde. |
|---|---:|---|---:|---|---:|
| Educação | 59 | Tributário | 32 | Agro | 30 |
| Saúde | 26 | RH | 26 | Shared | 22 |
| Financeiro | 18 | Saneamento | 18 | Social | 16 |
| Integrações | 10 | Frotas | 9 | Almoxarifado | 8 |
| Compras Empresariais | 8 | LGPD | 8 | WhiteLabel B2B | 7 |
| Obras | 7 | Processos | 6 | Patrimônio | 6 |
| GED | 6 | SaaS | 5 | Segurança | 5 |
| Pessoas | 5 | SaaS Admin | 5 | Executivo | 4 |
| Fiscalização | 4 | Compras LicitaPro | 4 | Auth | 4 |
| Meio Ambiente | 4 | Atendimento Cidadão | 4 | Royalties | 4 |
| Cidadão | 4 | Convênios | 4 | demais 34 pastas | 72 |

Há ainda 132 candidatos em `wwwroot/js`. Esta distribuição é descoberta, não homologação. Em especial, Educação, Patrimônio, Compras/Almoxarifado e Indústria continuam pendentes de contratos e provas próprios, na ordem solicitada; GED não foi usado para abrir funcionalidade nova.

## Jornada auditada nesta passagem: template Enterprise compartilhado

| Campo | Evidência atual |
|---|---|
| Módulo e tela | `Enterprise/ModulePage`, compartilhada por Ordem de Serviço, Indústria, Estoque/Compras, Comercial, Varejo e Atacado |
| Rota Web | actions declaradas em `EnterprisePagesControllers`; 38 usos de `ModulePage` identificados |
| Endpoint/API | rota informada por `EnterprisePageViewModel.ApiRoute`; CRUD canônico também expõe `GET/POST /api/enterprise/{area}`, `GET/PUT/DELETE /api/enterprise/{area}/{id}` e `POST .../{id}/restaurar` |
| DTO/ViewModel | `EnterprisePageViewModel`, `EnterpriseMutationRequest`, `EnterpriseListItem`, `EnterpriseActionResult` e envelope `ApiResponse<T>` |
| Serviço/repositório | `IEnterpriseCrudService`; persistência Dapper por implementação existente (prova runtime bloqueada) |
| Tabelas/views | catálogo por área do CRUD Enterprise, incluindo `enterprise_cliente`, `enterprise_ordem_servico`, `enterprise_produto`, saldos/movimentos e `enterprise_auditoria_operacional`; deve ser confirmado por área em banco isolado |
| Operações permitidas | consultar, criar, editar, inativar e restaurar no template; ações operacionais variam por metadado. Exclusão física não foi criada |
| Permissões | permissão específica passada por cada action Web, filtro de contexto Enterprise e autorização da API; decisão runtime ainda bloqueada |

### Resultado por operação

| Operação | Estado | Evidência/bloqueio | Correção efetuada |
|---|---|---|---|
| Create | bloqueado | Sem PostgreSQL/.NET. A análise encontrou sucesso exibido após 2xx e listagem genérica, sem reler o identificador criado | Cliente agora extrai o ID da resposta e executa `GET /{id}` antes de fechar o modal ou anunciar sucesso |
| Read | bloqueado | Sem sessão, tenant e banco isolado; contrato estático de detalhe existe | Respostas redirecionadas, HTML de login e conteúdo não JSON deixam de ser interpretados como sucesso |
| Update | bloqueado | Sem prova de concorrência e persistência; ID já é conhecido | Cliente relê exatamente o ID atualizado antes do sucesso |
| Inativação/restauração | bloqueado | Sem prova runtime e sem validação de repetição | Cliente relê o registro e confere `INATIVO` (ou sua remoção na restauração) antes do sucesso |
| Exclusão física | não aplicável | O template modela ciclo de vida administrativo e preserva histórico; não há justificativa de domínio para remoção física | Nenhuma exclusão física adicionada |
| Negativos/autorização | bloqueado | Exigem identidades, contratação, suspensão, revogação e dois tenants reais no banco isolado | Resposta HTML/redirect de sessão não produz sucesso fictício; demais cenários permanecem pendentes |

### Interface e acessibilidade corrigidas

- Campos gerados têm `id` único e associação `label[for]`; obrigatórios recebem indicação textual.
- O modal possui nome acessível, ação primária explícita, ajuda curta “Como usar”, resumo de erro com foco e confirmação antes de descartar alterações.
- Dados permanecem no formulário quando a gravação ou sua releitura falha.
- A correção está no cliente compartilhado, em vez de ser copiada para cada view consumidora.

## Defeitos encontrados e causas

1. **Falso positivo de persistência:** o cliente tratava qualquer resposta HTTP bem-sucedida como gravação e chamava uma listagem paginada genérica. O registro podia não estar na página, ter resposta sem ID ou divergir do estado persistido.
2. **Sessão expirada em AJAX:** `fetch` segue redirect por padrão; HTML de autenticação podia satisfazer `response.ok` e gerar toast de sucesso.
3. **Ciclo de vida não comprovado:** inativar/restaurar validava somente o status HTTP, não o estado relido.
4. **Formulário gerado sem associação explícita de label e sem aviso de descarte:** prejudicava teclado/leitor e permitia perda silenciosa da edição.

## Validação e evidências bloqueadas

- Restore locked, build Release/Razor, testes .NET, Swagger e testes de endpoints: **bloqueados pela ausência do SDK .NET 10**.
- PostgreSQL vazio, upgrade, reexecução de migrations e CRUD com releitura independente: **bloqueados pela ausência de PostgreSQL/psql e de conexão isolada**.
- Navegador em 360, 390, 768, 1024, 1366 e 1920 px, teclado, Escape, foco, zoom 200%, impressão/PDF e capturas: **bloqueados porque a aplicação não pode ser iniciada sem o runtime**. Não há evidência visual produzida nesta passagem.
- A inspeção estática e os testes existentes foram ampliados sem criar nova classe de teste. JavaScript alterado deve passar `node --check`; o gate .NET permanece obrigatório antes de promover qualquer linha para `aprovado`.

## Pendências por prioridade

1. **Educação:** decompor os 59 candidatos, começando por aluno/responsável, e provar Create/Read/Update com dois tenants; depois matrícula/enturmação, transferência, frequência e impressão.
2. **Patrimônio:** decompor 6 candidatos e provar bem, movimentação concorrente, responsável, inventário e divergência sem baixa automática.
3. **Compras e Almoxarifado:** decompor 19 candidatos (Compras, Compras Empresariais e Almoxarifado) e provar requisição até entrada/estorno, saldo e repetição.
4. **Indústria:** decompor a tela e clientes compartilhados, validar estados, reserva, apontamento, consumo e integração canônica de estoque.
5. **SaaS/Segurança/Pessoas:** provar catálogo/contratação/perfil/contexto, suspensão persistente, revogação e separação entre administração SaaS e cliente.
6. Demais módulos, relatórios e exportações; GED permanece por último para evolução.

## Próximo item exato do backlog

Disponibilizar SDK .NET 10.0.100 e PostgreSQL 16 isolado via `ConnectionStrings__DefaultConnection`; executar o gate P0 e, se verde, auditar **Educação → cadastro/edição de aluno e responsável**, registrando rota, DTO, permissão, SQL/tabelas e releitura independente para cada operação e cada tenant.

## Comandos reproduzíveis deste checkpoint

```bash
git branch --show-current
git rev-parse HEAD
git remote -v
git status --short --branch
find src/Sigov.Web -type f -name '*.cshtml'
rg -l '<form|fetch\(|\.ajax\(|XMLHttpRequest|type="file"|data-bs-toggle="modal"|excluir|inativ|cancel|aprov|estorn|filtr|pagina|export|window\.print' src/Sigov.Web
dotnet --info
command -v psql docker node
```

Checkpoint de continuidade: **591 arquivos candidatos; 0 jornadas aprovadas ponta a ponta; correção compartilhada Enterprise pronta para gate; próximo fluxo específico é Educação/aluno-responsável após P0.**

## Continuidade — manutenção de frota (2026-09-15)

A inspeção posterior confirmou uma jornada parcial em Frotas: a gravação de manutenção era transacional e isolada por `tenant_id`/`entidade_id`, porém o POST descartava o identificador retornado e não oferecia consulta individual; além disso, qualquer falha de validação retornava a view sem recarregar os veículos, quebrando a renderização e perdendo a seleção.

Foram corrigidos o bloqueio por `ModelState` antes do serviço, a recomposição dos catálogos autorizados em retornos inválidos e a preservação dos valores pelo binding Razor. A criação agora redireciona ao ID persistido e a consulta independente filtra ID, tenant e entidade. A conclusão existente ganhou formulário com antiforgery, permissão canônica `frotas.manutencao.concluir`, transação e releitura do detalhe. Não foram declaradas edição, cancelamento, idempotência de criação ou homologação: esses pontos seguem pendentes, assim como a prova em PostgreSQL isolado, pois o ambiente continua sem .NET, `psql` e navegador executável.

Próximo item exato desta jornada: introduzir idempotência persistida para a abertura de manutenção, com migration corretiva sincronizada e cenários de mesma chave/mesmo conteúdo e mesma chave/conteúdo divergente em PostgreSQL 16 isolado.
