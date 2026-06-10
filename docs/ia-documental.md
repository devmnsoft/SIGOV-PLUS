# IA SIGOV PLUS — Pós-Build 11

A camada de IA do SIGOV PLUS é tenant-aware, auditável e nasce com provider interno/simulado. Nenhum dado sensível é enviado para provedor externo sem configuração explícita do tenant.

## Habilitação

1. Contratar o módulo `ia_assistente` e, conforme uso, `ia_documental`, `ia_relatorios`, `ia_automacoes` e `ia_predicoes`.
2. Acessar `/IA/Configuracao` ou `PUT /api/ia/configuracao`.
3. Habilitar `ia_habilitada`, definir limites mensais e manter `mascarar_dados_sensiveis=true`.

## Segurança e LGPD

- Toda execução grava `tenant_id`, usuário, módulo, tipo, prompt, resposta, status, tokens, custo estimado, `correlation_id` e datas.
- CPF, CNPJ, e-mail e telefone são mascarados quando a configuração exige.
- Ações críticas como exclusão, baixa financeira, cancelamento, pagamento e alteração crítica exigem confirmação humana.
- O provider inicial é `INTERNO`; provedores externos ficam apenas cadastrados e inativos/configuráveis.

## APIs principais

- `GET/PUT /api/ia/configuracao`
- `POST /api/ia/executar`
- `GET /api/ia/execucoes`
- `GET/POST /api/ia/sugestoes`
- `POST /api/ia/documentos/{documentoId}/resumir|classificar|extrair-campos`
- `POST /api/ia/relatorios/gerar`
- `GET/POST /api/ia/automacoes`
- `GET /api/ia/alertas`
- `POST /api/ia/predicoes/*`
- `GET /api/ia/consumo`

## Provider interno

O provider interno usa heurísticas para resumo, classificação, extração, sugestões, relatórios e predições iniciais. Quando faltarem dados, a resposta deve informar a limitação e solicitar complementação.

## Documental

A IA documental resume documentos, classifica tipos por palavras-chave, extrai campos simples e registra revisão. Contratos, notas de serviço e processos administrativos têm heurísticas iniciais.
