# Checklist LGPD e auditoria por módulo

| Item | Critério | Situação |
|---|---|---|
| CPF | Mascarar em tela, busca, relatório e logs | Obrigatório |
| CNPJ | Mascarar quando associado a pessoa/fornecedor sensível | Obrigatório |
| E-mail/telefone | Mascarar em listagens e exportações públicas | Obrigatório |
| Saúde/social | Proteger por permissão fina e não exportar sem máscara | Obrigatório |
| Criação/alteração/exclusão lógica | Registrar auditoria com tenant, usuário, IP, user-agent e correlationId | Obrigatório |
| Consulta/exportação/download | Auditar acesso a dado pessoal/documento | Obrigatório |
| Assinatura/aprovação/tramitação | Auditar ação, recurso, id e resultado funcional | Obrigatório |

## Complemento Release Candidate 1.0.0-rc.2

A RC exige validação por módulo de CPF/CNPJ/e-mail/telefone/CNS/endereço mascarados quando aplicável, alerta forte para saúde e assistência social, exportações sem dados sensíveis completos sem justificativa, logs sem PII e auditoria para consulta/exportação de dados pessoais.
