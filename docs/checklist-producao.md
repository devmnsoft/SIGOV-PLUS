# Checklist de produção

- [ ] Build e testes passam.
- [ ] Docker sobe e health responde.
- [ ] Login validado.
- [ ] Secrets fora do repositório.
- [ ] HTTPS/reverse proxy ativo.
- [ ] Backup e restore testados.
- [ ] Observabilidade com correlação.
- [ ] LGPD, auditoria e mascaramento revisados.
- [ ] IA/OCR/assinatura/conectores somente com providers reais configurados.

## Complemento Release Candidate 1.0.0-rc.2

Antes da publicação/homologação: confirmar secrets trocados, HTTPS/reverse proxy, CORS restrito, cookies seguros, antiforgery, health API/Web, backup/restore testado, LGPD/auditoria revisadas, CI verde, smoke test sem 404 principal e plano de rollback documentado.
