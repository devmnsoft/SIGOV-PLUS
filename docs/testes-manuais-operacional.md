# Testes manuais — módulos operacionais

Checklist: Protocolo, GED/OCR, Tributário, Contratos, Jurídico, Financeiro, Busca, Relatórios, POC, Minha Central, LGPD, Auditoria e Mobile.

1. Acessar as rotas principais e confirmar que não retornam 404.
2. Confirmar badge de status operacional e mensagem de fallback/schema detectado.
3. Verificar que documentos pessoais aparecem mascarados nas listagens e CSV.
4. Acionar ações críticas apenas via modal/POST com antiforgery.
5. Validar que POST sem schema não exibe sucesso falso de salvamento.

## Checklist sprint operacional real — 2026-07-02

- Protocolo: validar dashboard, listagem, detalhes, novo, tramitar, arquivar e reabrir; confirmar aviso LGPD e auditoria.
- GED/OCR: validar documentos, pastas, novo documento, detalhes, nova versão, arquivar, visualizar/download; confirmar que upload/OCR não são simulados sem storage.
- Tributário: validar contribuintes, novo contribuinte, detalhes, imóveis, débitos, guias, dívida ativa e CSV mascarado.
- Contratos: validar listagem, novo, detalhes, vencimentos e arquivamento com modal/antiforgery.
- Jurídico: validar processos, prazos, pareceres, audiências e detalhes com auditoria de visualização.
- Financeiro: validar contas a receber, contas a pagar, caixa, categorias e relatórios com valores em pt-BR quando houver dados.
- Busca, Relatórios, Minha Central e POC: validar fallback honesto e ausência de 404.
- Mobile/console: validar responsividade Bootstrap e ausência de erro JavaScript próprio nas telas operacionais.
