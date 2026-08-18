# RC50.51 — Matriz de pendências P0/P1/P2

## Resultado desta execução

| Prioridade | Item | Estado |
|---|---|---|
| P0 | Aplicação PostgreSQL 16 do script development | Bloqueado pelo ambiente: `psql` ausente; não declarado como aprovado |
| P0 | Build Release e Swagger | Bloqueado pelo ambiente: SDK `dotnet` ausente; não declarado como aprovado |
| P0 | Login admin/superadmin | Bloqueado pelo ambiente: runtime e PostgreSQL ausentes; seeds foram preservados |
| P1 | APIs transversais anteriormente anônimas | Corrigido: Segurança, LGPD e Auditoria exigem autenticação |
| P1 | Ausência de Observabilidade canônica | Corrigido: API e Web usam `ProjectStatusProvider` e preflight do inspector |
| P1 | Links canônicos de governança | Corrigido com controllers/actions/views existentes ou adicionados |
| P1 | Persistência integral dos POSTs granulares/LGPD | Aberto para RC50.52; contratos não simulam sucesso de banco |
| P2 | Instrumentação de toda consulta pessoal/exportação legada | Registrado para RC50.52 |
| P2 | Policies item a item no menu | Registrado para RC50.52 |
| P2 | CSVs de todos os domínios transversais | Registrado para RC50.52 |
| P3 | Testes automatizados, integrações oficiais, IA/OCR avançado | Futuro conforme escopo |

Nenhuma tabela foi descartada, nenhum dado foi apagado e nenhum warning foi ocultado. A migration é somente incremental e o descarte LGPD permanece preparatório.
