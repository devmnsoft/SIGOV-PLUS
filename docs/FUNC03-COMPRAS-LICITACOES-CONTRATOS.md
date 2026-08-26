# FUNC03 — Compras, Licitações, Contratos e Atas

## LicitaPro IA integrado

O LicitaPro é uma jornada do FUNC03, acessível em `/Compras/LicitaPro`, e reutiliza `compras_fornecedor`, `compras_processo`, `compras_contrato`, tenant, entidade, usuário e auditoria. Não constitui módulo paralelo e não duplica cadastros.

### Entregas e rotas

- dashboard real: `/Compras/LicitaPro`;
- fontes e importações versionadas: `/Fontes` e `/Importacoes`;
- radar, detalhe, cadastro e vínculo com processo: `/Oportunidades`;
- portal, documentos/certidões e checklist: `/Portal`, `/Documentos`, `/Checklists`;
- análise assistida explicável: `/Analises`;
- agenda, alertas e contrato conquistado: `/Agenda`, `/Alertas`;
- auditoria: `/Auditoria`;
- CSV filtrável: `/Relatorios/{oportunidades|documentos|checklists|agenda|auditoria}.csv`.

As fontes não configuradas são explicitamente apresentadas assim. Falhas recebem status e erro sanitizado; não existe fallback fictício. Relacionamentos são selecionados em listas do banco. Toda sugestão exige revisão humana.

### Persistência e regras

A migration `20260826130000_exp03_licitapro_func03.sql` cria tabelas `compras_licitapro_*` para fonte, importação, oportunidade, documentos, checklist/itens, análise/critérios, agenda, alertas, sincronização e auditoria. Todas têm PK identity `bigint`, escopo obrigatório por tenant e entidade, constraints de estado/data/percentual/score e índices operacionais. A oportunidade é idempotente por fonte e identificador externo; documentos aprovados exigem validade e referência; checklist obrigatório pendente não pode ser dispensado sem justificativa; agenda bloqueia oportunidade vencida ou indisponível. CSV neutraliza `=`, `+`, `-` e `@`.

### Segurança

As 17 permissões `COMPRAS_LICITAPRO_*` são persistidas e avaliadas fail-closed. Queries são Dapper parametrizadas; seleção de área e exportação usam whitelist de rota/switch. CNPJ/CPF não são projetados nas novas listagens e erros externos devem ser sanitizados.

## CORR03 — fechamento LicitaPro IA

O LicitaPro permanece uma capacidade integrada ao FUNC03. Dashboard, fontes, importações, oportunidades, portal do fornecedor, documentos, checklists, análises, agenda, alertas, auditoria e CSV operam com contexto tenant/entidade e autorização persistida. Seleções relacionais vêm do banco e validações críticas são aplicadas no servidor. Consulte `docs/entregas/CORR03-FECHAMENTO-LICITAPRO-FUNC03.md`.
