# RC50.63 — jornadas por perfil

Todas as jornadas exigem tenant, módulo contratado e grant no backend. A tela não concede acesso.

| Perfil | Jornada funcional | Negativas obrigatórias |
|---|---|---|
| SuperAdmin | Minha Central → status funcional → alertas técnicos → negativas/exportações → tenants → matriz | operação sem contexto rastreável |
| AdminTenant | módulos contratados → usuários/perfis → pendências/alertas → LGPD/auditoria | outro tenant |
| Secretário | central → dashboard da área → indicadores → aprovações → exportação permitida | módulo/área não concedida |
| Coordenador | pendências da área → alerta → equipe/setor → aprovar/cancelar | ação sem grant |
| Funcionário Financeiro | baixas/pagamentos → DAM/fatura → relatório permitido | estorno/configuração sem grant |
| Professor | turmas vinculadas → frequência → diário | turma alheia |
| ACS | visitas da microárea → visita/desfecho → ocorrência | área alheia/dado clínico amplo |
| Atendimento | protocolo → andamento → Ouvidoria/e-SIC → encaminhamento | financeiro amplo |
| Auditor | trilhas → exportações → negativas → relatórios | qualquer mutação operacional |
| Fiscal Contrato/Obra | vínculos → medição → ocorrência → anexo GED | contrato/obra alheio |
| Almoxarifado | estoque crítico → requisição → entrada/saída | aprovação ou depósito não concedido |

## Evidência esperada

Para cada passo registrar URL, status HTTP, tenant/usuário mascarados, alteração persistida e evento de auditoria. Estrutura ausente deve resultar em estado vazio/“estrutura pendente”, nunca 500. As jornadas não foram declaradas homologadas sem runtime.
