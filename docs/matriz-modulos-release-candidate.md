# Matriz de módulos da Release Candidate

Versão: 1.0.0-rc.2
Data: 2026-07-06

| Módulo | URL | Status | Persistência | Fallback | Auditoria | LGPD | Relatório | Busca | Observação |
|---|---|---|---|---|---|---|---|---|---|
| Dashboard | /Dashboard | Funcional | real/fallback | Não | Técnica | Baixa | Sim | Entrada executiva |
| Minha Central | /MinhaCentral | Funcional | real/fallback | Não | Técnica | Baixa | Sim | Central operacional |
| Pessoas | /Pessoas | Parcial | Dapper/Postgres | Honesto | Sim | Mascaramento | CSV | Sim | Cadastro base |
| RH | /Rh | Parcial | Dapper/Postgres | Honesto | Sim | Mascaramento | CSV | Sim | Cadastros e folha inicial |
| Protocolo | /Protocolo | Em implantação | Schema dependente | Sim | Prevista | Mascaramento | Parcial | Sim | Não prometer fluxo completo |
| GED | /Ged | Em implantação | Schema/storage dependente | Sim | Prevista | Mascaramento | Parcial | Sim | OCR/assinatura condicionais |
| Workflow | /Workflow | Parcial | Dapper/Postgres | Honesto | Sim | Baixa | Parcial | Sim | Tarefas e etapas |
| Tarefas | /Tarefas | Parcial | Dapper/Postgres | Honesto | Sim | Baixa | Parcial | Sim | Operacional |
| Notificações | /Notificacoes | Parcial | Dapper/Postgres | Honesto | Sim | Baixa | Parcial | Sim | Outbox/notificações |
| Agenda | /Agenda | Demonstrativo | Não garantida | Sim | Técnica | Baixa | Não | Não | Tela de apoio |
| Compras | /Compras | Parcial | Dapper/Postgres | Honesto | Sim | Baixa | Parcial | Sim | Fluxos básicos |
| Licitações | /Licitacoes | Parcial | Dapper/Postgres | Honesto | Sim | Baixa | Parcial | Sim | Fluxos básicos |
| Contratos | /Contratos | Em implantação | Schema dependente | Sim | Prevista | Baixa | Parcial | Sim | Gestão contratual condicionada |
| SIAFIC | /Siafic | Parcial | Dapper/Postgres | Honesto | Sim | Baixa | Parcial | Sim | Orçamento/financeiro inicial |
| Patrimônio | /Patrimonio | Parcial | Dapper/Postgres | Honesto | Sim | Baixa | Parcial | Sim | Bens/inventário |
| Obras | /Obras | Parcial | Dapper/Postgres | Honesto | Sim | Baixa | Parcial | Sim | Acompanhamento inicial |
| Portal Cidadão | /PortalCidadao | Demonstrativo | Limitada | Sim | Técnica | Mascaramento | Não | Sim | Área pública controlada |
| Ouvidoria | /Ouvidoria | Parcial | Dapper/Postgres | Honesto | Sim | Mascaramento | Parcial | Sim | Manifestações |
| Relatórios | /Relatorios | Parcial | Consultas limitadas | Honesto | Sim | Mascaramento | Sim | Sim | Sem exposição indevida |
| POC | /Poc | Funcional | Catálogo/health | Não | Técnica | Baixa | Sim | Sim | Roteiro demonstrável |
| Tributário | /Tributario | Parcial | Dapper/Postgres | Honesto | Sim | Mascaramento | Parcial | Sim | Receitas/dívida inicial |
| Jurídico | /Juridico | Em implantação | Schema dependente | Sim | Prevista | Baixa | Não | Sim | Pareceres/processos |
| Financeiro | /Financeiro | Parcial | Dapper/Postgres | Honesto | Sim | Baixa | Parcial | Sim | Orçamento/execução |
| Educação | /Educacao | Parcial | Dapper/Postgres | Honesto | Sim | Mascaramento | Parcial | Sim | Cadastros setoriais |
| Saúde | /Saude | Parcial | Dapper/Postgres | Honesto | Sim | Forte | Parcial | Sim | Dados sensíveis |
| Saneamento | /Saneamento | Parcial | Dapper/Postgres | Honesto | Sim | Baixa | Parcial | Sim | Indicadores |
| Social | /Social | Parcial | Dapper/Postgres | Honesto | Sim | Forte | Parcial | Sim | Dados sociais sensíveis |
| Agro | /Agro | Parcial | Dapper/Postgres | Honesto | Sim | Baixa | Parcial | Sim | Produtores/programas |
| IA | /IA | Demonstrativo | Config dependente | Sim | Sim | Não enviar PII | Não | Não | Desabilitar sem chave |
| Assinatura | /AssinaturasDigitais | Em implantação | Config dependente | Sim | Sim | Baixa | Não | Sim | Depende provedor |
| Integrações | /Integracoes | Em implantação | Config dependente | Sim | Sim | Baixa | Não | Sim | APIs/webhooks |

## Atualização Pós-RC

- Funcional real: schema homologável de API key, webhook, outbox, protocolo, GED, workflow, tarefas, notificações e validação pública.
- Parcial: telas e actions ainda não conectadas ao serviço de persistência devem manter fallback honesto.
- Dependente de provedor/configuração: assinatura oficial, OCR, storage externo e entregas HTTP reais.
- Não disponível: simulação de assinatura oficial, OCR, pagamento/empenho ou exposição de dados sensíveis sem máscara.

## Pós-RC 02 — persistência real operacional

- Funcional real: API v1 com API key/tenant/escopos, Protocolo e GED persistindo nas tabelas Pós-RC, Outbox worker consumindo sigov.outbox_evento.
- Parcial: telas MVC administrativas continuam com fallback honesto quando ação/formulário não possui todos os dados reais.
- Dependente de provedor: OCR, ICP/Gov.br e entrega externa oficial de webhooks.
- LGPD: respostas e logs não devem expor dados pessoais completos nem token claro.


## Pós-RC 03 — homologação Web real

- **Funcional real:** Protocolo e GED Web passam a acionar serviços Dapper para `sigov.protocolo`, `sigov.protocolo_movimento`, `sigov.workflow_instancia`, `sigov.tarefa`, `sigov.notificacao`, `sigov.documento`, `sigov.documento_versao`, `sigov.protocolo_anexo`, `sigov.portal_validacao_documento` e `sigov.outbox_evento` quando o schema existe.
- **Parcial:** Dashboard, Minha Central, Busca e Relatórios mantêm fallback honesto e devem priorizar dados reais detectados no schema.
- **Em implantação/fallback:** PDF/DOCX da POC, OCR, ICP-Brasil e Gov.br não são simulados.
- **Dependente de provedor:** envio externo de webhook e validações oficiais dependem de infraestrutura configurada.
- **Não disponível:** exposição de path físico de storage e dados pessoais completos em listagens/exports.

## Matriz final Pós-RC 05

| Módulo/Capacidade | Status Pós-RC 05 | Observação honesta |
|---|---|---|
| Login/Auth | Funcional real se CI/homologação validar | Requer execução em ambiente com .NET/Docker |
| Dashboard | Funcional real se validado com seed/dados reais | KPIs dependem de dados persistidos |
| Minha Central | Funcional real se validada com tarefas/notificações reais | Conferir permissões por usuário |
| Protocolo básico | Funcional real se criação/tramitação Web/API passar | Sem remover fallback honesto |
| GED básico | Funcional real se upload/download permitido passar | Storage local deve estar montado |
| Busca Global | Funcional real se encontrar dados reais com LGPD | Mascaramento obrigatório |
| Relatórios CSV | Funcional real se exportar CSV real com LGPD | Não expor storage path/segredos |
| API key/API v1 | Funcional real se sem key=401 e com key=200 | Chaves nunca completas em logs |
| Seed demo | Funcional real se aplicar duas vezes sem duplicar | Bloqueado em Production |
| Smoke test | Funcional real quando executado e anexado | Gera Markdown/JSON |
| Workflow avançado | Parcial | Fluxos complexos seguem em evolução |
| Tarefas avançadas | Parcial | Básico validável; automações avançadas pendentes |
| Webhooks externos | Parcial | Entrega real depende de endpoint externo |
| POC | Parcial | Evidências disponíveis; exportação avançada pode depender de evolução |
| Assinatura/OCR/IA/Mobile offline | Parcial/Dependente de provedor | Não simular ICP, Gov.br ou OCR |
| ICP/Gov.br | Dependente de provedor | Exige integração oficial |
| SMTP/WhatsApp | Dependente de provedor | Exige credenciais e contrato |
| Integrações oficiais externas | Dependente de provedor | Homologar por conector oficial |

## Atualização Pós-RC 06

A classificação funcional não abre módulos novos. A mudança desta sprint é operacional: CI executável, smoke autenticado, seed demo compatível, package sanitizado e evidências Go-Live.

## Pós-RC 07 — homologação real multi-tenant e Go-Live

| Área | Classificação | Evidência / limitação honesta |
|---|---|---|
| Dashboard | Funcional real se CI/homologação passar | Resolve tenant por contexto; sem tenant opera como Admin Global agregado; fallback não apresenta número fake. |
| Minha Central | Parcial | Depende de schema operacional e autenticação de homologação. |
| Protocolo | Funcional real se CI/homologação passar | API/Web usam tabelas reais, workflow/tarefa/notificação/outbox quando schema existe. |
| GED | Funcional real se CI/homologação passar | Upload e metadados sem expor storage path; validação pública respeita classificação. |
| Workflow, Tarefas, Notificações | Funcional real se CI/homologação passar | Acionados por protocolo real e validados por smoke/SQL em CI. |
| Busca e Relatórios | Funcional real se CI/homologação passar | Consultas tenant-aware e mascaramento LGPD; Admin Global apenas quando sem tenant. |
| API v1 e API Key | Funcional real se CI/homologação passar | `X-Api-Key` + `X-Tenant-Id`, hash SHA-256 e escopos plurais. |
| Outbox | Funcional real se CI/homologação passar | Evidencia pendentes/falhos; entrega externa depende de endpoint configurado. |
| Webhooks | Dependente de provedor | Configuração e falhas rastreadas; entrega real não é simulada. |
| Package release | Funcional real se CI/homologação passar | Bloqueia `.env`, certificados/chaves, dumps, storage e senha trivial. |
| Smoke E2E | Funcional real se CI/homologação passar | Usa key demo apenas com `SIGOV_SMOKE_USE_DEMO_KEY=true` e mascara token. |
| Go-Live Check | Funcional real se CI/homologação passar | Gera Markdown/JSON com bloqueios, warnings e versão RC. |
| ICP-Brasil, Gov.br, OCR, SMTP, WhatsApp | Dependente de provedor | Sem simulação de funcionamento oficial; fallback honesto/documentado. |

## Pós-RC 07 — Enterprise CRUD funcional

- Incluídas tabelas `sigov.enterprise_*` idempotentes para Comercial, OS, Estoque/Compras, Industrial/Manutenção, Indústria Produção, eventos e auditoria.
- Telas Enterprise existentes passam a usar template operacional com listagem real, formulário, detalhes, exportação CSV e avisos LGPD/fallback.
- Jornadas mínimas funcionais: proposta aprovada gera pedido; pedido gera OS; OS consome estoque; saldo negativo é bloqueado; plano preventivo gera OS.

## Pós-RC 08 — Enterprise
| Bloco | Situação | Evidência |
|---|---|---|
| Comercial/OS/Estoque/Compras/Industrial | CRUD e ações operacionais revisados | `docs/matriz-crud-enterprise-pos-rc-08.md` |
| Indústria Produção | MVP CRUD homologável, regras avançadas pendentes | `docs/jornadas-enterprise-pos-rc-08.md` |


## Pós-RC 09 — QA funcional Enterprise

- Diagnóstico criado em `docs/diagnostico-enterprise-pos-rc-09.md`.
- Evidências de homologação registradas em `docs/evidencias-enterprise-pos-rc-09.md` e `docs/evidencias-enterprise-pos-rc-09.json`.
- Manual de usuário e checklist QA criados para a jornada Enterprise navegável.
- UX Enterprise refinada com filtros, paginação, loading, detalhes, edição, inativação, restauração, CSV com tenant, toasts e fallback honesto.

## Pós-RC 10 — Enterprise seguro

- Endurecimento de API Enterprise com `[Authorize]`, tenant obrigatório, permissões por ação e respostas 401/403/503 coerentes.
- UX Enterprise com metadata de formulário por entidade e ações operacionais sem botões placeholder.
- CSV seguro com mascaramento LGPD, sanitização de separador/quebras de linha e proteção contra fórmulas.
- Documentação de diagnóstico, jornadas, QA, matriz CRUD e segurança LGPD Pós-RC 10 adicionada.

## Pós-RC 16

- Auditoria de DI e interfaces Enterprise registrada em `docs/matriz-di-pos-rc-16.md` e `docs/matriz-interfaces-pos-rc-16.md`.
- Evidências e limitações de ambiente registradas em `docs/evidencias-pos-rc-16.md`.

## Pós-RC 17 — validação técnica

A trilha Pós-RC 17 centraliza as correções de build, DI Enterprise, migrations/seed PostgreSQL, Docker/Docker Compose, smoke estático/E2E, empacotamento de release e go-live. A evidência operacional deve vir dos comandos do CI e dos artifacts gerados, não de declaração manual.

## Pós-RC 20 — infraestrutura operacional

O escopo corrente preserva os módulos existentes e concentra a estabilização em PostgreSQL standalone, manifest de migrations, separação de seeds e versionamento `1.0.0-rc20`.
