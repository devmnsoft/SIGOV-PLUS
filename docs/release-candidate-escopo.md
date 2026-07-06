# Escopo da Release Candidate SIGOV PLUS

- Versão: 1.0.0-rc.2
- Data: 2026-07-06
- Objetivo: homologação técnica/comercial sem criação de módulos novos.

## Módulos incluídos
Dashboard, Minha Central, Pessoas, RH, Protocolo, GED, Workflow, Tarefas, Notificações, Agenda, Compras, Licitações, Contratos, SIAFIC, Patrimônio, Obras, Portal Cidadão, Ouvidoria, Relatórios, POC, Tributário, Jurídico, Financeiro, Educação, Saúde, Saneamento, Social, Agro, IA, Assinatura, Integrações.

## Módulos fora do escopo
Novos módulos, troca de arquitetura MVC/Razor, troca de Dapper, Entity Framework, migrations destrutivas e funcionalidades reais sem persistência/auditoria comprovadas.

## Classificação real

### Funcionais
Dashboard, Minha Central, POC.

### Parciais
Pessoas, RH, Workflow, Tarefas, Notificações, Compras, Licitações, SIAFIC, Patrimônio, Obras, Ouvidoria, Relatórios, Tributário, Financeiro, Educação, Saúde, Saneamento, Social, Agro.

### Em fallback honesto / implantação
Protocolo, GED, Contratos, Jurídico, Assinatura, Integrações.

### Demonstrativos
Agenda, Portal Cidadão, IA.

### Indisponíveis
Nenhum item principal foi classificado como indisponível nesta revisão documental; ações dependentes de provedor/configuração permanecem indisponíveis dentro dos módulos até configuração.

## Riscos conhecidos
- Ambiente atual do agente não possui .NET SDK nem Docker, portanto build/test/Docker foram registrados como limitação de ambiente.
- Módulos parciais não devem ser vendidos como fluxo integral de produção.
- IA, assinatura, OCR, SMTP e integrações dependem de chaves/provedores.

## Pendências aceitas
- Homologação manual de navegador, mobile e console em ambiente com Docker.
- Cobertura automatizada incremental além da suíte mínima existente.

## Pendências bloqueantes
- Executar build/test/Docker em ambiente com .NET SDK e Docker.
- Corrigir qualquer 404/erro JS identificado pelo smoke test real.

## Matriz resumida

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

