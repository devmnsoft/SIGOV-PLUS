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

