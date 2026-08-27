# SST360 — FUNC09

O SST360 é o domínio de Saúde e Segurança do Trabalho integrado aos cadastros oficiais de RH. O módulo não duplica pessoa, servidor, vínculo, cargo, lotação, afastamento, exame legado ou evento eSocial.

## Escopo funcional

- Dashboard em `/SST` e `/SST/Dashboard`, calculado no PostgreSQL para o tenant e a entidade correntes.
- Ambientes, fatores de risco e exposições; PGR, PCMSO e LTCAT versionáveis.
- ASO MVC completo em `/SST/ASO`, com criação, edição, detalhe, validação no cliente e no servidor e listas de servidores.
- EPIs, treinamentos, CAT, acidentes, investigação, PPP e monitor eSocial SST.
- Eventos S-2210, S-2220, S-2240 e S-2230 ficam `PENDENTE_INTEGRACAO` na ausência de adaptador oficial; envio nunca é simulado.

## Segurança e LGPD

Todas as consultas aplicam `tenant_id` e `entidade_id`. Resultado médico e restrição laboral são dados sensíveis, sujeitos às permissões `SST_ASO_SENSITIVE_VIEW` e `SST_DADO_SENSIVEL_VIEW`. Os POSTs usam antiforgery, queries Dapper parametrizadas e o banco registra autoria e alertas. A chave de evento eSocial garante reenvio idempotente.

## Regras críticas

ASO exige servidor existente, tipo, data, médico e resultado. Aptidão com restrição exige descrição e inaptidão cria alerta crítico. Datas finais não precedem as iniciais, quantidades e cargas são positivas, aprovação exige responsável e vigência, certificado exige presença e investigação encerrada exige conclusão. Eventos aceitos são imutáveis por contrato operacional.
