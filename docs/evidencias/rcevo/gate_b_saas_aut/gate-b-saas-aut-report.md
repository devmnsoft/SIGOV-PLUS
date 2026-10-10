# Gate B (P1) Â· RC-SAAS-AUT â€” runtime em PostgreSQL 16

Gerado em: 
2026-10-10T10:01:13.7237744Z

Aprovados: **26** Â· Pendentes: **0**

- [x] **PASS** [G0/env.pg16-reachable] pg reachable; PostgreSQL 16.14 (Debian 16.14-1.pgdg13+1) on x86_64-pc-linux-gnu, compiled by gcc (Debian 14.2.0-19) 14.2.0, 64-bit
- [x] **PASS** [G0/env.web-up] GET /Auth/Login -> 200
- [x] **PASS** [G0/env.api-up] GET /api/saas/tenant/modulos sem bearer -> 401 (401 esperado)
- [x] **PASS** [G0/fixture.applied] fixture gateb aplicado exit=0
- [x] **PASS** [G0/fixture.cenarios-presentes] linhas cenario=4
- [x] **PASS** [G0/fixture.B-suspensa] tenant 910002 -> 910002|t|SUSPENSA|2027-08-31|10|SUSPENSO
- [x] **PASS** [G0/fixture.C-expirada] tenant 910003 data_fim -> 910003|t|ATIVA|2026-09-30|10|HABILITADO
- [x] **PASS** [G1/auth.login-web] hom.a.admin POST /Auth/Login -> 302|http://localhost:5099/MinhaCentral
- [x] **PASS** [G1/auth.minha-central] GET /MinhaCentral -> 200
- [x] **PASS** [G1/auth.api-token-mint] GET /Auth/ApiToken len=64 (erro='Nenhum exercício ativo' é informativo; o token bruto é válido — verificado em auth.bearer-api-200)
- [x] **PASS** [G1/auth.bearer-api-200] GET /api/saas/tenant/modulos bearer -> 200; {"success":true,"data":[{"codigo":"financeiro","nome":"Financeiro/SIAFIC","contratado":true,"habilitado":true},{"codigo":"educacao","nome":"
- [x] **PASS** [G1/auth.bearer-necessario] cookie cru SIGOV.AUTH como bearer -> 401 (401 esperado; cookie != token bruto)
- [x] **PASS** [G1/auth.anonimo-401] sem bearer -> 401 {"error":"authentication_required","message":"AutenticaÃ§Ã£o obrigatÃ³ria."}
- [x] **PASS** [G2/modulo.A-baseline-200] A hom.a.admin (ATIVA/válida) GET /api/educacao/escolas -> 200
- [x] **PASS** [G2/modulo.B-bloqueado] B hom.b.admin (SUSPENSA, login.ok=True) -> 403 motivo=BLOQUEADO_COMERCIAL | {"success":false,"data":null,"message":"Assinatura comercial do cliente com status SUSPENSA; acesso suspenso.","correlat
- [x] **PASS** [G2/modulo.C-expirado] C hom.c.admin (expirada, login.ok=True) -> 403 motivo=CONTRATO_EXPIRADO | {"success":false,"data":null,"message":"VigÃªncia da assinatura comercial expirada.","correlationId":"0HNP6LP96R9A5:0000
- [x] **PASS** [G2/modulo.D-limite-nao-bloqueia-modulo] D hom.d.admin (ATIVA/limite=1, login.ok=True) -> 200 (limite aplica a criação de usuário, não a módulo)
- [x] **PASS** [G3/forbid.api-envelope] API 403: success=False motivo=BLOQUEADO_COMERCIAL (motivo presente e padronizado)
- [x] **PASS** [G3/forbid.web-403-sem-permissao] gate.nao.admin (login.ok=True) GET /Seguranca/Usuarios -> 403 motivo=SEM_PERMISSAO ok=False
- [x] **PASS** [G3/forbid.web-admin-200] hom.a.admin (admin global, login.ok=True) GET /Seguranca/Usuarios -> 200 len=96000
- [x] **PASS** [G4/revoke.before-valid] antes do bump: api=200 web_MinhaCentral=200
- [x] **PASS** [G4/revoke.web-302] após bump identity: web /MinhaCentral com mesmo cookie -> 302 (redireciona p/ login)
- [x] **PASS** [G4/revoke.api-401] após bump identity: api com token antigo -> 401 (401 esperado)
- [x] **PASS** [G4/revoke.restored] usuario.updated_at restaurado para 'null' (era 'null')
- [x] **PASS** [G5/mns.triplo-no-banco] permissao saas.plataforma.administrar id=1755 ativo=t modulo=saas
- [x] **PASS** [G5/mns.holders-ativos] usuários ativos com o triplo (banco, regra 11) = 8
