# Troubleshooting de produção

1. Capture horário UTC, versão, ambiente, endpoint e correlation id; nunca copie token, senha, documento ou conexão completa.
2. **Migration:** preserve SQLSTATE/arquivo/linha, pare rollout, compare manifest/checksum e use backup/rollback; não edite controle manualmente.
3. **API/Web 5xx:** consulte log pelo correlation id, readiness e banco/schema; confirme secret, DNS/TLS e permissões do usuário.
4. **Login:** valide relógio, cookie Secure/proxy HTTPS, usuário/tenant/perfis e bloqueio; não redefina senha em log ou SQL improvisado.
5. **404/menu:** confronte rota publicada, base path e autorização; não remova controller para ocultar falha.
6. **Worker:** desligue a instância em loop, preserve mensagem/tentativas, confira tenant e dependência; reative após correção idempotente.
7. **Performance:** identifique query/tenant/paginação, plano e locks; não crie índice sem os três validadores.

Escale P0 imediatamente. Após correção, repita smoke e registre causa raiz, evidência e prevenção.
