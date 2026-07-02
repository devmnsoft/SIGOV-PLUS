# Governança de IA no SIGOV PLUS

A IA pode apoiar Protocolo, GED/OCR, Jurídico, Tributário, Contratos, Financeiro, Workflow, Tarefas, Dashboard e Minha Central. Ela não toma decisões automáticas, não substitui parecer humano e não executa tramitações sem confirmação.

Dados permitidos: metadados administrativos, descrições operacionais, prazos, status, textos já autorizados pelo usuário e informações minimizadas para a finalidade. Dados a mascarar antes de envio externo: CPF, CNPJ, e-mail, telefone, endereço, dados de saúde, dados de crianças/adolescentes, dados financeiros individualizados e qualquer segredo.

Todo uso deve registrar usuário, módulo, ação, prompt mascarado, resposta resumida, data/hora, status, tokens/custo quando disponível e `CorrelationId`. A justificativa/consentimento deve ser coletada na UI sempre que o usuário informar contexto sensível.

Quando IA não estiver configurada, a resposta oficial é: “Assistente inteligente não configurado neste ambiente.” Fallback local só pode ser marcado como “Sugestão demonstrativa” ou “Regra do sistema”.
