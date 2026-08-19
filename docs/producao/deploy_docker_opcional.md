# Deploy Docker opcional

Construa imagens versionadas/digest-pinned em CI; não inclua secrets nem backup na imagem. Injete conexão e credenciais por secrets do orquestrador, monte storage persistente, execute como usuário não-root e publique somente o proxy TLS. API, Web e Worker têm processos separados, limites e health checks.

Migration é job único, após `pg_dump`, e deve terminar antes do rollout. Faça rolling deploy apenas com compatibilidade de schema; readiness remove instâncias sem banco. Rollback usa digest anterior e, se necessário, restore separado aprovado. Não exponha PostgreSQL ou Swagger publicamente.
