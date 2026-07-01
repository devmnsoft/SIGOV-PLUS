# Testes manuais — módulos operacionais

Checklist: Protocolo, GED/OCR, Tributário, Contratos, Jurídico, Financeiro, Busca, Relatórios, POC, Minha Central, LGPD, Auditoria e Mobile.

1. Acessar as rotas principais e confirmar que não retornam 404.
2. Confirmar badge de status operacional e mensagem de fallback/schema detectado.
3. Verificar que documentos pessoais aparecem mascarados nas listagens e CSV.
4. Acionar ações críticas apenas via modal/POST com antiforgery.
5. Validar que POST sem schema não exibe sucesso falso de salvamento.
