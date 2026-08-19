# RC50.59 — relatório dos fluxos de arrecadação e financeiro

Data: 2026-08-19. Decisão: **não apto para produção enquanto banco, build e smoke runtime não estiverem verdes**.

1. **Tributário:** inventariados os controllers canônicos/avançados, contribuinte, lançamento, guia/DAM, pagamento, dívida ativa, fiscalização e carnês.
2. **Financeiro:** inventariados orçamento, receitas, pagamentos, dashboard, exportação, tesouraria e financeiro empresarial.
3. **Saneamento:** inventariados consumidor, imóvel/unidade, ligação, hidrômetro, leitura, fatura, arrecadação, inadimplência, OS e dashboards.
4. **Endpoints:** o serviço de Saneamento agora nega contexto sem usuário, módulo ou grant; nenhuma superfície 501 foi adicionada.
5. **Services:** pagamento exige valor positivo/forma, estorno exige justificativa e existência; Saneamento valida série, leitura regressiva, fatura e arrecadação.
6. **Repositories:** preservados Dapper parametrizado e filtros `tenant_id`/`entidade_id`; regras persistentes existentes continuam sendo a autoridade transacional.
7. **Views:** inventariadas; nenhuma alteração visual foi necessária nesta entrega.
8. **Menus:** catálogo canônico ganhou as permissões granulares dos três módulos; smoke autenticado continua obrigatório.
9. **Negócio:** bloqueados pagamento não positivo, estorno vazio, leitura regressiva sem ocorrência, hidrômetro sem série, fatura negativa e arrecadação não positiva.
10. **Permissões:** adicionadas ao catálogo canônico as 31 chaves solicitadas de Tributário, Financeiro e Saneamento.
11. **Funcionário Financeiro:** baixa/registro e estorno permanecem grants separados; configuração não é implícita.
12. **Operador Tributário:** catálogo separa cadastro/lançamento/DAM das permissões de baixa e estorno financeiro.
13. **Operador Saneamento:** catálogo separa operação de consumidor/ligação/hidrômetro/leitura das permissões financeiras.
14. **Tributário → Financeiro:** tabelas/serviços de guia, arrecadação e ponte existentes foram preservados; prova E2E permanece pendente.
15. **Saneamento → Financeiro:** fatura/pagamento/eventos existentes foram preservados; liquidação bancária externa permanece preparatória.
16. **Auditoria:** estorno financeiro registra antes/depois e motivo; negativas por módulo/grant em Saneamento registram recurso, ação e motivo.
17. **LGPD:** consultas pessoais existentes permanecem auditadas e mascaradas; nenhuma PII foi incluída nos novos eventos.
18. **501:** busca estática obrigatória registra o estado real; nenhum endpoint essencial foi convertido em simulação.
19. **Botões:** nenhum botão foi removido para ocultar pendência; homologação HTTP permanece necessária.
20. **Dashboards:** serviços persistentes e protegidos foram preservados; ausência de runtime impede afirmar ausência de 500.
21. **Banco:** resultado registrado pelos comandos desta execução; falha ambiental, se houver, não será tratada como aprovação.
22. **Build:** resultado registrado pelos comandos desta execução, com warnings como erros.
23. **Gate:** resultado registrado pelo smoke production-like; PowerShell depende da ferramenta do host.
24. **RC50.60:** homologar concorrência/idempotência de baixas, elegibilidade dívida/corte/religação, substituição de hidrômetro e jornadas autenticadas por perfil.

Nenhuma classe, fixture, mock ou projeto de teste foi criado.
