# Administração SaaS e autorização canônica — RC-SAAS-AUT

Entrega própria de release candidate (regra 20: avaliação de autorização, troca de contexto e dashboard SuperAdmin exigem RC própria). Base `74f763` + commits locais em `main` sem push. GED está fora do escopo.

## Escopo

1. **Avaliador canônico** (`PersistentAuthorizationEvaluator`): fonte única e somente do banco para autorização efetiva conjunta de identidade, tenant ativo (`tenant_id` da claim `tenant_id`, sem fallback), módulo contratado/habilitado, permissão e escopo. Falha ou ausência de schema/configuração nega explicitamente (fail-closed); precedência NEGAR > PERMITIR. Menu oculto nunca é proteção.
2. **Admin global MNSOFT**: triplo `('saas','plataforma','administrar')` verificado no banco, sem catálogo mock, demo, fallback ou coleção hardcoded (regras 11–13).
3. **Comercial canônico (família B)**: plano, assinatura, vigência e limites vivem em `saas_plano*`/`saas_assinatura*`/`saas_addon`; contrato por módulo (família C) é espelho gravado na mesma transação. Invariáveis operacionais completas na `docs/adr/ADR-SaaS-Catalogo-Entitlements.md`.
4. **403 padronizado** com `motivo ∈ SEM_PERMISSAO | FORA_ESCOPO | MODULO_NAO_CONTRATADO | LIMITE_ATINGIDO | BLOQUEADO_COMERCIAL | CONTRATO_EXPIRADO`, escrito na resposta tanto na API quanto na Web (helper único `ForbiddenResponse`; controle informativo continua liberado quando a permissão existe).
5. **Revocação de sessão em paridade Web+API**: versão de autorização derivada dos timestamps persistidos (`DeriveAuthVersionAsync`); mudança de identidade/permissão/senha invalida sessão e token antigos; `OnValidatePrincipal` rejeita na Web e o handler API responde 401 com mensagem estável. Sessão vive 8 horas (`SessionLifetime`).
6. **Gestão de usuários do cliente** com envelope de delegação: último administrador não pode ser inativado/bloqueado ("não é possível inativar ou bloquear o último administrador do cliente"); anti-autopromoção impede salvar perfil que contenha permissões não delegáveis pelo usuário atual, salvo isenção explícita de `saas.plataforma.administrar`; violação grava auditoria e faz rollback.
7. **Troca de contexto auditada**: alternar contexto atualiza a sessão ativa para o tenant alvo e volta ao contexto original com trilha `usuario_contexto_global_log` (ALTERAR/RETORNO finalizados).
8. Multi-esfera (regras 21–24): regras, telas e nomenclaturas valem para municipal, estadual e federal; nada de comportamento municipal hardcoded.

## Camadas e persistência

- Camadas preservadas: Domain, Application, Infrastructure, Api, Web (regra 3); .NET 10 + C# 14 + Dapper sobre PostgreSQL 16 (regras 1–2).
- Migration PostgreSQL idempotente `013` (guard `unique_violation`); `manifest.json` 211/211 sincronizado com os seis consolidados (regras 6–8). Migration publicada não é alterada; correções entram como migration nova.
- Autorização, perfis, permissões e parâmetros têm o banco como fonte de autoridade (regra 11).

## Mudanças de comportamento relevantes

- Usuário autenticado sem permissão recebe **página de erro 403** na Web (StatusPagesWithReExecute + `Home/Error` renderizando `Motivo: <code>...</code>`) em vez de redirect para `/Auth/Login`; rotas de controle com check explícito devolvem o corpo JSON canônico `{ok:false,motivo,detalhe}`.
- Bloqueio comercial não apaga dados do cliente; limites são verificados transacionalmente (lock + contagem de ativos).

## Seeds de homologação (§6)

Script idempotente: `database/postgres/seeds/homologacao/20261005_rcsaas_aut_seed.sql`.

- Guarda de ambiente: não executa quando `current_setting('sigov.environment')` indica produção; pré-condições explícitas (plano ESSENCIAL id=1 e perfil ADMIN_TENANT id=50 devem existir, senão `RAISE`).
- Dados fictícios (regras 9–10): tenants `hom_a_contrato_saudavel` … `hom_d_limite_usuarios` (ids 910001–910004, ambiente `HOMOLOGACAO`), assinaturas MENSAL/BRL (plano ESSENCIAL), usuários `hom.a.admin`…`hom.d.admin` (OPERADOR, perfis via grupos) e histórico append-only com UUIDs fictícios.
- Cenários cobertos: A = ATIVA/HABILITADO (acesso normal); B = SUSPENSA → `BLOQUEADO_COMERCIAL`; C = contrato vencido → `CONTRATO_EXPIRADO`; D = limite de usuários esgotado → `LIMITE_ATINGIDO`.
- Credencial fictícia documentada para os quatro usuários: `Homologacao!2026` (PBKDF2-SHA256, 100.000 iterações; hash e salt constantes do script — nenhum dado pessoal real, nenhuma senha de ambiente).
- Idempotência demonstrada ao vivo: duas execuções consecutivas em PostgreSQL 16 (`sigov-postgres`) com exit 0 e saída idêntica.

## Validação (§9)

- Suite de testes existente estendida apenas em classes já presentes (regra 14): **763/763 verdes** (régua mínima 758).
- Stack de desenvolvimento Docker (`sigov-web`:8080, `sigov-api`:5001→8080, `sigov-postgres`:5432); deploy por `dotnet publish -c Release -r linux-x64` + `docker cp` + verificação SHA-256 dos artefatos em paridade local×container + `docker restart`.
- Gates ao vivo (scripts em `Temp/opencode`, fora do repositório; sem senhas literais — regra 18 — via `-AdminSenha`/env `SIGOV_DEV_ADMIN_SENHA`):
  - `gates_saas_aut.ps1` — critérios A (Web 403 com motivo `SEM_PERMISSAO` + controle informativo liberado), B (API 200 admin / 403 não-admin com motivo), N (predicado multi-tenant do seed 51), A3 (último administrador recusado via tela SaasAdmin + usuário permanece ativo), A4 (troca de contexto: sessão única do admin muda para o destino e volta ao contexto original, trilhas ALTERAR/RETORNO finalizadas), A5 (bump de `updated_at` invalida sessão Web → redirect login e token API antigo → 401; estado restaurado). Resultado 2026-10-06: **32/32 PASS**, zero resíduo após cleanup.
  - `gates_saas_comercial.ps1` — Etapa B: E (downgrade preserva dados e suspende excedentes), F (suspender/reativar/cancelar idempotentes sem apagar dados), G (upgrade concorrente serializado, assinatura única consistente), H (limite 20 usuários: criação 21 → 403 `LIMITE_ATINGIDO`), I (addon `USUARIOS_EXTRAS` eleva limite efetivo a 30; 31 → 403), A8 (`BLOQUEADO_COMERCIAL` pré-permissão com assinatura suspensa, liberação após reativação). Resultado 2026-10-06: **48/48 PASS** (`GATES_COMERCIAIS_TODOS_VERDES`).

## Fora desta RC

- GED/anexos; conversão destrutiva de PKs UUID legado (regra 5); mudanças de schema além da migration 013.
