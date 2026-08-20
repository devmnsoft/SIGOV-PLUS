# RC50.67 — plano de homologação integrada real

Data: 2026-08-20. Este plano não converte inspeção estática em homologação. A aprovação exige banco, build e HTTP reais.

## Escopo e matriz operacional

| Módulo | Web/dashboard | API principal | permissão mínima | estado inicial |
|---|---|---|---|---|
| Governança | `/Governanca` | `/api/governanca` | `governanca.visualizar` | Parcial |
| Segurança | `/Seguranca/MatrizAcesso` | `/api/seguranca` | `seguranca.visualizar` | Implementado |
| LGPD | `/Lgpd` | `/api/lgpd` | `lgpd.visualizar` | Parcial |
| Auditoria | `/Auditoria` | `/api/auditoria` | `auditoria.visualizar` | Implementado |
| Observabilidade | `/Observabilidade` | `/health` | `observabilidade.visualizar` | Parcial |
| Minha Central | `/MinhaCentral` | `—` | `minha-central.visualizar` | Implementado |
| Pendências | `/Pendencias` | `/api/pendencias` | `pendencias.visualizar` | Implementado |
| Alertas | `/Alertas` | `/api/alertas` | `alertas.visualizar` | Implementado |
| Qualidade de Dados | `/QualidadeDados` | `/api/qualidade-dados` | `qualidade-dados.visualizar` | Implementado |
| Integrações Internas | `/IntegracoesInternas` | `/api/integracoes-internas` | `integracoes.visualizar` | Implementado |
| Status Funcional | `/Modulos/StatusFuncional` | `/api/modulos/status-funcional` | `modulos.status.visualizar` | Implementado |
| Tributário | `/Tributario` | `/api/tributario` | `tributario.visualizar` | Parcial |
| Financeiro | `/Financeiro` | `/api/financeiro` | `financeiro.visualizar` | Parcial |
| Saneamento | `/Saneamento` | `/api/saneamento` | `saneamento.visualizar` | Parcial |
| Educação | `/Educacao` | `/api/educacao` | `educacao.visualizar` | Parcial |
| Saúde | `/Saude` | `/api/saude` | `saude.visualizar` | Parcial |
| Processos Digitais | `/Processos` | `/api/processos` | `processos.visualizar` | Parcial |
| GED | `/Ged` | `/api/ged` | `ged.visualizar` | Parcial |
| Assinaturas | `/Assinaturas` | `/api/assinaturas` | `assinaturas.visualizar` | Parcial |
| Legislativo | `/Legislativo` | `/api/legislativo` | `legislativo.visualizar` | Parcial |
| Diário Oficial | `/DiarioOficial` | `/api/diario-oficial` | `diario.visualizar` | Parcial |
| Transparência | `/Transparencia` | `/api/transparencia` | `transparencia.visualizar` | Parcial |
| Ouvidoria | `/Ouvidoria` | `/api/ouvidoria` | `ouvidoria.visualizar` | Parcial |
| e-SIC | `/Esic` | `/api/esic` | `esic.visualizar` | Parcial |
| RH | `/Rh` | `/api/rh` | `rh.visualizar` | Parcial |
| Folha | `/Folha` | `/api/folha` | `folha.visualizar` | Parcial |
| Compras | `/Compras` | `/api/compras` | `compras.visualizar` | Parcial |
| Licitações | `/Licitacoes` | `/api/licitacoes` | `licitacoes.visualizar` | Parcial |
| Contratos | `/Contratos` | `/api/contratos` | `contratos.visualizar` | Parcial |
| Almoxarifado | `/Almoxarifado` | `/api/almoxarifado` | `almoxarifado.visualizar` | Parcial |
| Patrimônio | `/Patrimonio` | `/api/patrimonio` | `patrimonio.visualizar` | Parcial |
| Frotas | `/Frotas` | `/api/frotas` | `frotas.visualizar` | Parcial |
| Obras | `/Obras` | `/api/obras` | `obras.visualizar` | Parcial |
| Assistência Social | `/AssistenciaSocial` | `/api/assistencia-social` | `assistencia.visualizar` | Estrutura existente |
| Empresarial/SaaS | `/Empresarial` | `/api/empresarial` | `empresarial.visualizar` | Estrutura existente |
| Agro | `/Agro/Dashboard` | `/api/agro` | `agro.visualizar` | Implementado; runtime pendente |
| Georreferenciamento | `/Agro/Mapa` | `/api/agro/geo/camadas` | `geo.visualizar` | Implementado; runtime pendente |

## Perfis e massa mínima

A seed cria `superadmin`, `admin`, Gestor Fazenda, Funcionário Financeiro, Gestor Educação, Professor, Gestor Saúde, ACS, Atendimento, Auditor, Gestor Agro, Técnico Rural e Operador Patrulha. Os dois administradores usam a guarda canônica; os demais recebem PBKDF2 e grupos de menor privilégio. Documentos/e-mails são exclusivamente fictícios (`invalid.local`).

A massa exigida é representada por registros documentais reais já suportados e eventos outbox idempotentes por domínio; os CRUDs setoriais devem completar sua massa pelas services oficiais, nunca por SQL que suponha colunas. São obrigatórios exemplos de Tributário, Financeiro, Saneamento, Educação, Saúde, Processos/GED, Administrativo, Agro/Geo e transversais.

## Execução, riscos e correções RC50.67

1. Aplicar `script_completo_dev.sql` e a seed duas vezes; qualquer erro/duplicação é P0.
2. Compilar o filtro runtime (API, Web, Worker, Application, Domain e Infrastructure), sem projetos de teste.
3. Executar probes declarativos Web/API. 404 de menu, 500 de dashboard e 501 essencial são P0.
4. Executar jornadas autenticadas com os perfis acima; vazamento entre tenant/escopo é P0.
5. Validar exportação GeoJSON, auditoria e ausência de PII. Exposição é P0.
6. Riscos: rotas convencionais divergirem do catálogo, grants legados amplos, schema parcial, integrações externas preparatórias e botões dependentes de JavaScript.
7. Correções desta sprint: seed canônica, runners Linux/Windows, manifesto HTTP e artifact sanitizado. Falhas funcionais só podem ser fechadas após reprodução runtime.

## Bloqueios

Neste host faltam `dotnet`, `psql`, `pg_dump`, `pg_restore` e `pwsh`: P0 ambiental explícito. O plano permanece executável em Linux/Windows equipados; ausência de ferramenta não é sucesso.
