# RC51.02O — governança estática do manifesto

Data: 2026-09-14. Estado: **CORRIGIDA ESTATICAMENTE / GATE A RUNTIME BLOCKED**.

## Estado inicial

- Branch `work`, HEAD `b710b05b188edbacee1cb9b30a8e9428fdc00cce`, sem remoto e sem upstream; árvore inicialmente limpa.
- SDK normativo: .NET `10.0.100`. `dotnet`, `pwsh`, `psql`, Docker e navegador não estão instalados no ambiente.
- O CS8629 de `IndustriaController.GerarOsAsync` já estava corrigido: `null` continua significando contexto operacional incompleto (HTTP 422), e os valores validados são capturados por pattern matching em `entidadeId` e `usuarioId`, sem supressão nullable ou identidade fictícia.

## Matriz curta

| Jornada | Evidência atual | Lacuna | Dependência | Alteração | Critério de aceite |
|---|---|---|---|---|---|
| Gate A / manifesto | Runner canônico valida arquivos, ledger e pós-condições | Última migration tinha checksum calculado sem a quebra final; validadores não rejeitavam metadados históricos/probes malformados no modo estático | PowerShell e PostgreSQL 16 para prova runtime | Checksum canônico sincronizado, checksum anterior conhecido e validação fail-closed nos dois aplicadores | `ValidateOnly`, banco vazio, reaplicação, histórico conhecido/desconhecido e pós-condições verdes |
| Login e isolamento | Código e testes estáticos existentes | Evidência PostgreSQL 16 ainda ausente | Gate A runtime | Preservado | login/logout, revogação, suspensão e dois tenants em runtime |
| SaaS | Catálogo/entitlement/Admin existentes como AGUARDA_GATE | Gate B não liberado | Gate A completo | Preservado | fluxo administrativo runtime isolado e auditado |
| Indústria/Compras/Estoque/Central | Fluxos parciais existentes; CS8629 corrigido | Gate C e transação industrial ainda pendentes | Gates A e B | Preservado | simulação/confirmação concorrente e integrações provadas |
| GED | Último no contrato | Fases anteriores incompletas | Gates A–D | Nenhuma alteração | iniciar somente após promoção formal das fases anteriores |

## Defeito e correção

O checksum publicado para `20260914120000_corr_industria_manutencao_integracao.sql` era o SHA-256 do texto sem a quebra de linha final, enquanto o runner canônico normaliza BOM e quebras de linha, mas preserva a quebra final. O arquivo da migration não foi alterado. O manifesto e os seis scripts consolidados agora registram o checksum calculado pelo contrato efetivo; o valor anterior foi preservado em `knownChecksums`, coberto pela pós-condição estrutural já existente.

Os validadores Bash e PowerShell agora recusam, antes de qualquer DDL: checksum fora do formato SHA-256 minúsculo, `knownChecksums` duplicado ou malformado, repetição do checksum atual, checksum histórico sem `postConditionSql` e probes vazios ou com nomes duplicados.

Não houve alteração visual ou de schema e, portanto, não há nova screenshot nem migration corretiva. A validação runtime permanece bloqueada pela ausência das ferramentas listadas acima.

## Próximo item exato

Executar `pwsh -NoProfile -File scripts/apply-migrations-manifest.ps1 -ValidateOnly`; depois, em PostgreSQL 16, provar banco vazio/reaplicação, rejeição de versão e checksum desconhecidos, aceitação do checksum histórico de `20260914120000` com pós-condição e upgrade legado formal. Somente após o Gate A, retomar login/isolamento e liberar o Gate B SaaS.
