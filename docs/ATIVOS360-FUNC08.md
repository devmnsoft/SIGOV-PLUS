# Ativos360 — FUNC08

O Ativos360 é a experiência integrada para os domínios oficiais de almoxarifado, patrimônio e frotas. A solução **não cria um cadastro paralelo**: o dashboard consulta os serviços Dapper existentes e as rotas operacionais encaminham para as telas reais, preservando autorização, entidade, auditoria, contratos, fornecedores e pessoas.

## Capacidades

- Dashboard em `/Ativos` com patrimônio por situação, estoque crítico, requisições, frota, abastecimentos, manutenção, documentos e inventários.
- Operação de produtos, saldos e movimentações sem saldo negativo pelo serviço oficial do almoxarifado.
- Tombamento, transferência, baixa, inventário, divergências e histórico pelo patrimônio existente.
- Veículos, motoristas, rotas, abastecimentos, documentos e manutenções pelo domínio de frotas existente.
- Extensões aditivas para transferências, depreciação, documentos, vínculos de motorista, rotas e alertas.
- Exportações reutilizam os geradores CSV oficiais, que neutralizam conteúdo interpretável como fórmula.

## Segurança e validação

O contexto `tenant_id`/`entidade_id` é obrigatório. As operações continuam protegidas pelas permissões de cada domínio e as permissões `ATIVOS_*` são persistidas para evolução gradual da matriz. POSTs permanecem nos controladores oficiais, com antiforgery, validação de modelo, recarga de seleções e mensagens operacionais.

## Rotas

A família `/Ativos/*` oferece uma entrada estável e encaminha para as telas oficiais de `/Almoxarifado`, `/Patrimonio` e `/Frotas`. Isso evita duplicação e garante que correções nos fluxos oficiais sejam imediatamente refletidas no Ativos360.
