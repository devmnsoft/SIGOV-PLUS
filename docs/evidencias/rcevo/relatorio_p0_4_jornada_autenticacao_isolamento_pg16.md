# RELATORIO P0.4 — Jornada de autenticação e isolamento executada ao vivo em PostgreSQL 16

Data: 2026-10-09 (UTC)
Base commit: `27ae43ef` (HEAD).
App: `Sigov.Web` (build Release do repositório corrente, rodado localmente contra o banco oficial) com `ASPNETCORE_ENVIRONMENT=Development`, conexão `ConnectionStrings__DefaultConnection` → banco `p04_dev`.
Banco oficial: **PostgreSQL 16.14** no container `sigov-postgres` (imagem `postgres:16`).

## O QUE FECHA ESTE RELATÓRIO

P0.4 exigia executar, com banco oficial validado: login por **CPF**, **CNPJ** e **e‑mail**; **Minha Central**; **logout**; **revogação**; **tenant suspenso**; e **dois tenants** isolados. Este relatório prova cada item ao vivo, com o fluxo real do Web (form de login + antiforgery + cookie de sessão), validando o ciclo de vida das sessões na tabela canônica `sigov.identidade_sessao` e o rastro em `sigov.auditoria_evento`.

## NOTA DE AMBIENTE (não mascarada, regra 13/18)

A porta `5432` do host era ocupada por um **PostgreSQL 18 nativo do Windows** (`postgresql-x64-18`), que sombreava o mapeamento do Docker para o container `sigov-postgres`: uma conexão de `localhost:5432` retornava `PostgreSQL 18.0 on x86_64-windows` (sem `p04_dev`). Para garantir que a jornada corresse no **PG16 oficial** — e sem elevar/interromper o serviço do Windows —, um forwarder temporário `alpine/socat` publicou a porta livre `5433` do host para `sigov-postgres:5432` na rede `sigov-plus_default`. O app e a verificação de conexão usaram `Host=localhost;Port=5433`; a conexão confirmou `PostgreSQL 16.14 … db=p04_dev`. O forwarder foi removido ao final.

## FIXTURES (dados fictícios, idempotentes, sem senha em texto puro)

A base dev (`script_completo_dev.sql`) já cria o tenant `sigov-local` (id 6) com `admin`/`superadmin` (senha dev PBKDF2). Foi criado um **segundo tenant** `p04-b` (id 7) espelhando a autorização do guard (`perfil_acesso ADMINISTRADOR_GERAL` → grupo → `perfil_permissao` com todas as permissões ativas), com dois usuários logáveis (mesmo hash PBKDF2 dev — mesma senha dev do guard):

| tenant | slug | usuário | tipo | documento | origem do identificador |
|---|---|---|---|---|---|
| 6 | `sigov-local` | `admin` | PF | (login/e‑mail) | e‑mail / login |
| 7 | `p04-b` | `p04pf` | PF | CPF `11144477735` | `sigov.pessoa_fisica.cpf` |
| 7 | `p04-b` | `p04pj` | PJ | CNPJ `11222333000181` | `entidade.cnpj` (do `entidade_id` do usuário) |

Observação de contrato (lida em `AuthenticationRepository.FindLoginCandidatesAsync`): o lookup por **CPF** lê `sigov.pessoa_fisica.cpf` (não `pessoa.documento`) e o lookup por **CNPJ** lê `entidade.cnpj`/`tenant.documento`. O fixture registra o CPF em `pessoa_fisica` e aponta o PJ para uma entidade com o CNPJ válido; o CPF válido `11144477735` e o CNPJ válido `11222333000181` passam na normalização/validação de dígito verificador (`AuthenticationIdentifierNormalizer`).

## RESULTADO GLOBAL

| # | Item P0.4 | Esperado | Obtido (evidência) | Veredito |
|---|---|---|---|---|
| 1 | Login por **e‑mail** | sessão criada, redirect p/ Minha Central | `admin@sigov.local` → `/MinhaCentral` ("Olá, Administrador…"); `identidade_sessao` id 1 (tenant 6), ativa | **PASS** |
| 2 | Login por **CPF** | candidato resolvido por documento, sessão no tenant certo | `11144477735` → `/MinhaCentral`; sessão id 2 = `p04pf` (tenant 7); log `IdentifierKind=Cpf` | **PASS** |
| 3 | Login por **CNPJ** | candidato resolvido por entidade.cnpj | `11222333000181` → `/MinhaCentral`; sessão id 3 = `p04pj` (tenant 7) | **PASS** |
| 4 | Senha errada | falha explícita, sem sessão | `admin@sigov.local` + senha incorreta → view com "Credenciais inválidas ou usuário bloqueado."; `LOGIN_FALHA`; `IdentifierKind=Email` | **PASS** |
| 5 | **Minha Central** autenticada | página 200 no contexto do tenant do usuário | HTTP 200; contexto exibido é do próprio tenant (menções a `P04`/`p04-b` > 0, zero a `SIGOV Local`) | **PASS** |
| 6 | **Logout** revoga a sessão | `revogada_at` preenchido, motivo LOGOUT, redirect p/ login | botão "Sair" (POST `/Auth/Logout`) → sessão id 1 `revogada_at` setada, `motivo_encerramento='LOGOUT'`; redirect p/ `/Auth/Login` | **PASS** |
| 7 | **Dois tenants** isolados | identidade resolvida dentro do tenant; contexto não vaza entre tenants | e‑mail→tenant 6; CPF/CNPJ→tenant 7; sessões com `tenant_id` distintos; Minha Central do `p04pf` (t7) sem referência a `sigov-local` (t6) | **PASS** |
| 8 | **Tenant suspenso** bloqueia login | login de usuário do tenant inativo falha; religável | `update tenant set ativo=false where slug='p04-b'` → login `p04pf@sigov.local` falha ("…bloqueado.", `LOGIN_FALHA`); religado (`ativo=true`) → login volta a dar `/MinhaCentral` | **PASS** |
| 9 | **Revogação** por alteração de identidade (paridade A5) | cookie já emitido passa a ser rejeitado após mudança de identidade/permissão | bump em `usuario.updated_at` do `p04pj` após o login → próxima requisição a `/MinhaCentral` com o mesmo cookie é rejeitada → redirect `/Auth/Login?ReturnUrl=%2FMinhaCentral` (OnValidatePrincipal: `auth_version` derivada ≠ persistida) | **PASS** |

## RASTRO DE AUDITORIA (`sigov.auditoria_evento`)

Ordem cronológica da jornada (timestamps locais do banco):

```
            ts    |     acao      | usuario_id | tenant_id
------------------+---------------+------------+-----------
 22:18:14 | LOGIN_SUCESSO |          1 |         6   -- e-mail (admin)
 22:27:27 | LOGOUT        |          1 |         6   -- logout revoga
 22:27:54 | LOGIN_FALHA   |            |           -- CPF antes do registro em pessoa_fisica
 22:29:49 | LOGIN_SUCESSO |          3 |         7   -- CPF (p04pf)
 22:30:43 | LOGIN_SUCESSO |          4 |         7   -- CNPJ (p04pj)
 22:31:16 | LOGIN_FALHA   |            |           -- senha errada
 22:32:16 | LOGIN_FALHA   |            |           -- tenant suspenso
 22:34:44 | LOGIN_SUCESSO |          3 |         7   -- reversibilidade (religado)
```

Estado final das sessões (`sigov.identidade_sessao`):

```
id | login | tenant_id | ativa | motivo
----+-------+-----------+-------+---------
 1 | admin |         6 |   f   | LOGOUT
 2 | p04pf |         7 |   t   |
 3 | p04pj |         7 |   t   |
 4 | p04pf |         7 |   t   |
```

Detalhes do log estruturado (extrato): `Model binding de login Development. PasswordProvided=True; PasswordLength=18`; `Falha de login Development. Reason=INVALID_OR_AMBIGUOUS_CREDENTIAL; IdentifierKind=Cpf/Email`; `Tentativa de login SIGOV: sucesso/falha` — correlacionáveis por `CorrelationId`.

## DETALHES POR ITENS

### Itens 1–3 — Login por e‑mail / CPF / CNPJ
Fluxo real do Web: GET `/Auth/Login` (obtem antiforgery + cookie), POST `/Auth/Login` com `Login`/`Senha` (identificador aceito como CPF/CNPJ/e‑mail/login conforme o rótulo "CPF, CNPJ, e-mail institucional ou login"), sucesso → `SignInAsync` (cookie) + redirect local p/ `/MinhaCentral`. Cada login bem-sucedido criou uma linha em `sigov.identidade_sessao` com `usuario_id`/`tenant_id` corretos e `token_hash`/`auth_version`/`expira_at`. A diferenciação entre os três canais fica provada pela resolução distinta: e‑mail casca em `usuario.email/login`; CPF casca apenas via `pessoa_fisica.cpf` (antes do registro em `pessoa_fisica`, o mesmo CPF retornava `CandidateCount=0` — falha registrada a 22:27:54); CNPJ casca via `entidade.cnpj` do `entidade_id` do usuário.

### Item 4 — Senha errada (falha explícita, regra 13)
Mesmo canal e usuário válidos, senha divergente → `VerifyPassword` falha → `valid=false` → `MensagemErro="Credenciais inválidas ou usuário bloqueado."`, sem criar sessão, `LOGIN_FALHA` auditado, sem vazamento de qual campo errou.

### Item 5 — Minha Central autenticada + contexto (item 7)
Com a sessão ativa, `GET /MinhaCentral` → HTTP 200, hero "Olá, …". No usuário do tenant `p04-b`, o conteúdo exibido referencia o próprio tenant (`P04`/`p04-b`) e **nenhuma** menção a `SIGOV Local`/`sigov-local` — o contexto é derivado dos claims do usuário logado (`tenant_id`, `entidade_id`), não de catálogo global.

### Item 6 — Logout
O botão "Sair" (form `POST /Auth/Logout` com antiforgery) invocou `IIdentitySessionService.RevokeAsync(sessionId, userId, "LOGOUT")` + `SignOutAsync`: `identidade_sessao.revogada_at` preenchida com `motivo_encerramento='LOGOUT'` (sessão id 1) e redirect p/ `/Auth/Login`.

### Item 8 — Tenant suspenso (e reversibilidade)
`update sigov.tenant set ativo=false where slug='p04-b'` invalida o tenant para autenticação (`IsActive` exige `TenantAtivo`). Login do `p04pf` (e‑mail válido, senha correta) falhou com "…bloqueado." (`LOGIN_FALHA`). Com `ativo=true` novamente, o mesmo login passou (`/MinhaCentral`) — demonstrando que o bloqueio vem do estado do tenant, não da credencial, e é reversível.

### Item 9 — Revogação por alteração de identidade (paridade Web+API, RC-SAAS-AUT A5)
`IIdentitySessionService.DeriveAuthVersionAsync(userId)` deriva `greatest(updated_at/created_at)` sobre identidade e permissões do usuário (usuario, usuario_grupo, grupo_perfil, perfil_permissao, perfil_acesso, permissao). A sessão persiste a `auth_version` capturada no login; `OnValidatePrincipal` compara a versão derivada corrente com a persistida e, se divergem, faz `RejectPrincipal` + `SignOutAsync`. Bumping `usuario.updated_at` do `p04pj` após o login tornou a `auth_version` da sessão obsoleta: a próxima `GET /MinhaCentral` com o mesmo cookie foi rejeitada → `/Auth/Login?ReturnUrl=%2FMinhaCentral`. Prova de que alteração posterior de identidade revoga a sessão emitida, em paridade com a validação do cookie.

## ESTADO FINAL DO AMBIENTE

- Banco descartável `p04_dev` destruído (`DROP DATABASE ... WITH (FORCE)`); `select count(*) from pg_database where datname='p04_dev'` = 0 (residuo zerado).
- Forwarder `p04fwd` removido (`docker rm -f`); app `Sigov.Web` local finalizado (porta 5099 liberada).
- Serviço Windows `postgresql-x64-18` **não foi alterado** (não tinha elevação para pará-lo; a jornada usou a porta 5433 via socat). Nenhum banco dev/homologação existente foi tocado.

## ARTEFATOS (Temp\opencode\)

- `p04b_fixture.sql` — criação idempotente do tenant `p04-b` + usuários PF/PJ.
- `p04b_fix.sql` — registro do CPF em `pessoa_fisica` e da entidade com CNPJ válido para o PJ.
- `p04_applog_auth.txt` — extrato do log estruturado do Web (binding, falhas com `IdentifierKind`, tentativas).
- `p04_body.html` — corpo HTML de uma resposta de login (diagnóstico).
- `dbtest\` — mini-console Npgsql usado para isolar o artefato da porta 5432 (PG18 windows vs. PG16 do container) e validar a rota 5433.
