# FUNC11 — Agro, Desenvolvimento Rural e Assistência Técnica

## Escopo entregue
Módulo MVC/Razor próprio, conectado exclusivamente ao PostgreSQL por Dapper/Npgsql, com dashboard filtrável, produtores, propriedades com múltiplos responsáveis, atividades, visitas, programas/beneficiários, insumos/distribuições, patrulha, feiras/comercialização, agroindústrias/orientações e fila de solicitações. Exclusões são lógicas e operações relevantes alimentam `agro_auditoria`.

## Banco
A migration idempotente `20260825050000_func11_agro_desenvolvimento_rural.sql` cria: `agro_produtor`, `agro_propriedade`, `agro_propriedade_produtor`, `agro_atividade_produtiva`, `agro_cultura`, `agro_criacao`, `agro_visita_tecnica`, `agro_recomendacao_tecnica`, `agro_programa`, `agro_programa_beneficiario`, `agro_insumo`, `agro_distribuicao_insumo`, `agro_patrulha_maquina`, `agro_servico_maquina`, `agro_feira`, `agro_feirante`, `agro_comercializacao`, `agro_agroindustria`, `agro_inspecao_orientacao`, `agro_solicitacao` e `agro_auditoria`. Todas são segregadas por tenant/entidade; cadastros operacionais têm trilha e soft delete. O gatilho de distribuição recusa saldo insuficiente e o índice de agenda impede conflito de máquina/data.

## RBAC e rotas
As 23 permissões `AGRO_*` solicitadas são persistidas e registradas no catálogo de policies. Cada GET, mutação e exportação exige policy específica (fail-closed). Rotas: `/Agro`, `/Agro/Produtores`, `/Agro/Propriedades`, `/Agro/Atividades`, `/Agro/AssistenciaTecnica`, `/Agro/Programas`, `/Agro/Insumos`, `/Agro/Patrulha`, `/Agro/Feiras`, `/Agro/Agroindustrias`, `/Agro/Solicitacoes` e `/Agro/Relatorios`.

## Relatórios
CSV real para produtores, propriedades, visitas, programas, distribuições, serviços, solicitações e auditoria. A exportação é limitada ao escopo, registrada em auditoria, inclui BOM UTF-8 e neutraliza CSV injection.

## Validações e LGPD
CPF/CNPJ é normalizado e validado por tamanho/tipo; documento é único no escopo. Áreas, coordenadas, quantidades e status têm validações de aplicação/HTML e checks no banco. Indeferimento/cancelamento exige justificativa. A interface informa finalidade pública dos dados pessoais e não os replica em seeds.

## Integrações e limites reais
`referencia_almoxarifado_id` e `frota_veiculo_id` preparam vínculos opcionais. Não há contrato estável acoplado nesta RC, logo a integração é parcial e não altera estoque/frota externos. O módulo não implementa processo legal completo SIM/SIE/SIF nem integração fiscal/tributária; inspeções são registros municipais de orientação, sem declaração automática de conformidade. Valores de comercialização são estimativos informados, nunca calculados ficticiamente.
