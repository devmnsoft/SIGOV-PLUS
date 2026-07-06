# Segurança — APIs e Integrações SIGOV PLUS

- API externa exige API key por tenant ou JWT interno.
- API key nunca deve ser salva em texto claro; persistir apenas hash e prefixo.
- Escopos controlam permissões finas por recurso.
- Chaves revogadas ou expiradas devem ser bloqueadas.
- Rate limit deve considerar API key, IP, endpoint e janela temporal.
- MVC mantém antiforgery em POST administrativo.
- CORS deve usar allowlist de origens.
- Headers de segurança são aplicados pelo middleware existente.
- Logs não armazenam Authorization, X-Api-Key, secrets ou payload sensível completo.
- Mensagens de erro são amigáveis e com `correlationId`.
