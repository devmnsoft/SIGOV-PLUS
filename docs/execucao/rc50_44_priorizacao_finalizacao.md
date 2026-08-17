# RC50.44 — Priorização para finalização

## Matriz executável
| Prioridade | Pendência | Esforço | Risco | Dependências |
|---|---|---|---|---|
| P0 | Aplicar `script_completo_dev.sql` no PostgreSQL real e reconciliar 48 avisos de legado | M | Alto | PostgreSQL local, backup e manifest |
| P0 | Confirmar presença/identidade da entrega RC50.43 no manifest | P | Alto | histórico da release e SQL aprovado |
| P0 | Build Release `-warnaserror` não aferido neste ambiente | P | Alto | SDK .NET e restore locked |
| P0 | Swagger e logins admin/superadmin não aferidos | P | Alto | build, banco, seeds e processos Web/API |
| P1 | Smoke autenticado de menus e ações convencionais | M | Médio | runtime e perfis seeded |
| P1 | Fechar fluxo persistido Bloco 8 | G | Alto | migrations/rotas Bloco 8 |
| P1 | Fechar cadeia comercial Bloco 9 | G | Alto | estoque, OS, compras e indústria |
| P1 | Validar `/SystemHealth/ProjectStatus` com banco disponível | P | Baixo | login e `schema_migrations` |
| P2 | Relatórios/exportações nos fluxos prioritários | M | Médio | dados e autorização |
| P2 | Uniformizar trilha LGPD/auditoria | G | Alto | catálogo de dados e permissões |
| P2 | Padronizar empty/loading/toast/responsividade | M | Baixo | fluxos estabilizados |
| P2 | Evidências e termos de aceite por módulo | M | Médio | smoke e responsáveis |
| P3 | App mobile nativo | G | Alto | APIs fechadas |
| P3 | Integrações oficiais, OCR e ICP-Brasil real | G | Alto | contratos/credenciais externos |
| P3 | IA/copiloto | G | Alto | governança e dados |
| P3 | Suíte automatizada final | G | Médio | escopo funcional congelado; RC50.52 |

## Ordem recomendada
1. Disponibilizar .NET/PostgreSQL, executar build/migrations e corrigir P0 pela causa.
2. Subir API/Web e comprovar Swagger, seeds, login, MinhaCentral e ProjectStatus.
3. Percorrer menu/rotas e resolver P1 simples.
4. Executar RC50.45 e RC50.46, fechando demonstração dos blocos 8/9.
5. Executar verticais RC50.47–50 conforme prioridade comercial.
6. Executar RC50.51 transversalmente; fechar P2 de relatório/design/aceite.
7. Executar RC50.52, testes finais, pacote, observabilidade e go/no-go.

## Critério de promoção
Uma pendência só sai de P0/P1 com comando reproduzível e evidência no ambiente `Database=postgres`, `Schema/Search Path=sigov`; revisão estática isolada não equivale a fechamento ponta a ponta.
