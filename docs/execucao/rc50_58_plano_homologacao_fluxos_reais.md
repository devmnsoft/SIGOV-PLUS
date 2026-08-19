# RC50.58 — plano de homologação dos fluxos reais

Data: 2026-08-19. O trabalho usa evidência conservadora: código existente não equivale a homologação runtime. Nenhum módulo novo e nenhuma classe/projeto de teste são criados.

## Método e ordem

1. Preservar a árvore e inventariar controllers, services, repositories, views, rotas, permissões, operações de status e exportações.
2. Aplicar os validadores estáticos e eliminar 501 essenciais, rotas sem destino e transições sensíveis sem justificativa/auditoria encontradas.
3. Homologar, com PostgreSQL, os caminhos criar → listar → detalhar → editar → status/cancelar → exportar, sempre no mesmo tenant.
4. Repetir por perfil permitido e proibido, comprovando 403 no backend, menu coerente, máscara e trilha.
5. Somente marcar um item aprovado com evidência de banco e HTTP. Integração oficial sem provedor permanece explicitamente preparatória.

## Lotes funcionais

| Lote | Fluxo | Evidência mínima |
|---|---|---|
| A | Tributário/SIAFIC e Saneamento/Financeiro | lançamento/fatura, baixa idempotente, cancelamento/estorno justificado, trilha |
| B | Educação e Saúde | vínculo ativo, estoque/lote válido, máscara de documento, exportação auditada |
| C | Processos/GED/Assinaturas | protocolo, autuação, tramitação, versão/assinatura, encerramento e consulta pública |
| D | Compras/Contratos/Estoque/Patrimônio | aprovação, medição, recebimento, saldo/tombamento e origem financeira |
| E | RH/Folha e Frotas/Obras | vínculos/períodos, fechamento/reabertura, veículo/obra/status e relatórios |
| F | LGPD/Auditoria/Segurança | titular/incidente, negativa, exportação e segregação dos 11 perfis |

## Critério de saída

Build Release com warnings como erros, apply limpo do banco, smoke production-like, zero 501 essencial, e prova de que menus/dashboards críticos não produzem 404/500. Ausência de SDK, banco ou PowerShell é bloqueio ambiental, jamais aprovação.
