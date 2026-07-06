# Auditoria de menu e rotas

| Menu | URL | Controller | Action | Status HTTP esperado | Funcional/Parcial/Fallback | Observação |
|---|---|---|---|---|---|---|
| Dashboard | /Dashboard | Conforme rota MVC | Index/Hub | 200 ou 302 | Funcional | Entrada executiva |
| Minha Central | /MinhaCentral | Conforme rota MVC | Index/Hub | 200 ou 302 | Funcional | Central operacional |
| Pessoas | /Pessoas | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Cadastro base |
| RH | /Rh | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Cadastros e folha inicial |
| Protocolo | /Protocolo | Conforme rota MVC | Index/Hub | 200 ou 302 | Em implantação | Não prometer fluxo completo |
| GED | /Ged | Conforme rota MVC | Index/Hub | 200 ou 302 | Em implantação | OCR/assinatura condicionais |
| Workflow | /Workflow | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Tarefas e etapas |
| Tarefas | /Tarefas | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Operacional |
| Notificações | /Notificacoes | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Outbox/notificações |
| Agenda | /Agenda | Conforme rota MVC | Index/Hub | 200 ou 302 | Demonstrativo | Tela de apoio |
| Compras | /Compras | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Fluxos básicos |
| Licitações | /Licitacoes | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Fluxos básicos |
| Contratos | /Contratos | Conforme rota MVC | Index/Hub | 200 ou 302 | Em implantação | Gestão contratual condicionada |
| SIAFIC | /Siafic | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Orçamento/financeiro inicial |
| Patrimônio | /Patrimonio | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Bens/inventário |
| Obras | /Obras | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Acompanhamento inicial |
| Portal Cidadão | /PortalCidadao | Conforme rota MVC | Index/Hub | 200 ou 302 | Demonstrativo | Área pública controlada |
| Ouvidoria | /Ouvidoria | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Manifestações |
| Relatórios | /Relatorios | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Sem exposição indevida |
| POC | /Poc | Conforme rota MVC | Index/Hub | 200 ou 302 | Funcional | Roteiro demonstrável |
| Tributário | /Tributario | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Receitas/dívida inicial |
| Jurídico | /Juridico | Conforme rota MVC | Index/Hub | 200 ou 302 | Em implantação | Pareceres/processos |
| Financeiro | /Financeiro | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Orçamento/execução |
| Educação | /Educacao | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Cadastros setoriais |
| Saúde | /Saude | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Dados sensíveis |
| Saneamento | /Saneamento | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Indicadores |
| Social | /Social | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Dados sociais sensíveis |
| Agro | /Agro | Conforme rota MVC | Index/Hub | 200 ou 302 | Parcial | Produtores/programas |
| IA | /IA | Conforme rota MVC | Index/Hub | 200 ou 302 | Demonstrativo | Desabilitar sem chave |
| Assinatura | /AssinaturasDigitais | Conforme rota MVC | Index/Hub | 200 ou 302 | Em implantação | Depende provedor |
| Integrações | /Integracoes | Conforme rota MVC | Index/Hub | 200 ou 302 | Em implantação | APIs/webhooks |
## Evidência desta execução
O ambiente de agente em 2026-07-06 não possui `dotnet` nem `docker`; por isso comandos finais foram tentados e classificados como limitação operacional, não como aprovação técnica. A validação deve ser repetida em runner/estação com SDK .NET e Docker.
