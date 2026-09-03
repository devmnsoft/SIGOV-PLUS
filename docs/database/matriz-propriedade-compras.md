# Matriz de propriedade física — Compras

Inventário concluído antes da implementação. `PK/tenant/entidade` registra o contrato físico esperado; “—” significa que o modelo histórico não possui a coluna. As FKs detalhadas e seus tipos são consultáveis em `database/postgres/diagnostics/validate_schema_contracts.sql`.

| Tabela/família | Proprietário e consumidores | PK | tenant | entidade | FKs principais | Contrato C#/Dapper | Migration criadora |
|---|---|---:|---:|---:|---|---|---|
| `compras_fornecedor`, `compras_solicitacao[_item]`, `compras_processo[_item]`, `compras_cotacao`, `compras_julgamento`, `compras_contrato`, `compras_ata_*`, `compras_recebimento[_item]`, históricos/auditoria | Compras governamentais; LicitaPro, Frotas, Contratos, Almoxarifado, Patrimônio e Financeiro | bigint identity | bigint | bigint | processo→solicitação; cotação/julgamento/contrato/ata→fornecedor e processo; recebimento→contrato/ata/processo | `long`; `Sigov.Infrastructure/Compras`, `LicitaProService`, `FrotasService` | `20260824200000_func03_compras_licitacoes_contratos.sql` |
| `compras_licitapro_*` | LicitaPro | bigint identity | bigint | bigint | documento/checklist/agenda/alerta→fornecedor; oportunidade/análise/agenda→processo; agenda→contrato | `long` | `20260826130000_exp03_licitapro_func03.sql`; convergência `20260903173000_corr_licitapro_schema_history.sql` |
| `compras_empresarial_numeracao`, `fornecedor[_contato/_endereco/_documento/_avaliacao]`, `requisicao[_item]`, `aprovacao`, `cotacao[_convite/_resposta_item]`, `pedido`, `recebimento`, `fatura`, `devolucao`, `historico`, `idempotencia` | ComprasEmpresariais | uuid (numeracao: composta) | uuid | — | família interna UUID, preservada por rename | `Guid`; `Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs` | originalmente `20260802210000_pos_rc_37b_compras_empresariais_fullstack.sql`; separação `070_separate_compras_uuid_contracts.sql` |
| `bloco6_compras_solicitacao[_item]`, `cotacao[_item]`, `mapa_comparativo`, `processo`, `modalidade`, `julgamento[_item]`, `autorizacao`, `ordem_compra[_item]`, `integracao_financeira`, `evento` | Bloco6 legado | uuid | uuid | uuid | referências lógicas UUID mantidas no domínio | `Guid`; `Sigov.Infrastructure/Bloco6/Bloco6Repositories.cs` | originalmente `20260815120000_rc50_37_compras_licitacoes_bloco6_core.sql`; separação `070_separate_compras_uuid_contracts.sql` |
| `emp_compras_*` | Empresarial existente | uuid | uuid | conforme tabela | família interna, sem compartilhamento com Compras | `Guid`; `Sigov.Infrastructure/Empresarial` | `20260610100000_pos_build_04_enterprise_modules.sql` e `20260817132000_rc50_43_empresarial_estoque_core.sql` |

## Colisões encontradas

* `compras_fornecedor`, `compras_cotacao` e `compras_recebimento`: declaradas UUID em ComprasEmpresariais e bigint no FUNC03.
* `compras_solicitacao`, `compras_solicitacao_item`, `compras_processo`, `compras_cotacao` e `compras_julgamento`: declaradas UUID no Bloco6 e bigint no FUNC03.
* `CREATE TABLE IF NOT EXISTS` mascarava as divergências e deixava serviços `Guid` e `long` sobre o mesmo nome.

A separação usa somente renomeação transacional, nunca cast UUID/bigint, cópia, `DROP` ou alteração manual do ledger. Se origem e destino coexistirem, a operação para explicitamente para exigir reconciliação e comparação de contagens.
