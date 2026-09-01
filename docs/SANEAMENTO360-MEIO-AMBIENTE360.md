# Saneamento360 e Meio Ambiente360 — RC50.92

## Visão geral

A RC50.92 consolida a gestão pública multi-esfera de água, esgoto, drenagem, resíduos, coleta, licenciamento, fiscalização e denúncias. Todo registro operacional é segregado por `tenant_id`, entidade, exercício, esfera de governo, órgão, unidade gestora, unidade executora e território. A solução atende municípios, estados, União, autarquias, consórcios, agências e fundos ambientais sem presumir prefeitura como autoridade.

## Fluxos e segurança

- Ligações reutilizam referências reais de pessoa, contribuinte e imóvel; a RC não replica esses cadastros nem cria cobrança automática.
- Ocorrências e denúncias possuem protocolo, triagem, responsável, prioridade e histórico. Recusa ou cancelamento requer justificativa.
- Coleta registra rota, quantidade não negativa, unidade, destinação e responsável. Veículo, patrimônio e documento são apenas vínculos reais.
- Licenças preservam empreendimento, tipo, validade, responsável técnico, parecer e licença de origem nas renovações.
- Fiscalização, vistoria, auto, notificação, embargo e recurso mantêm fundamentos e referências reais ao Fiscaliza360/Jurídico360 quando disponíveis.
- Denunciante e documento fiscal são dados protegidos; transparência publica somente indicadores agregados.
- CSV deve neutralizar células iniciadas por `=`, `+`, `-`, `@`, tabulação ou retorno de carro.

## Limites técnicos

Não há simulação de balança, IoT, mapa, GED, SEFAZ ou API ambiental. Integrações somente são executadas quando um adaptador e um vínculo persistido existem. Cobranças, multas e receitas dependem dos fluxos canônicos de Tributos/Financeiro/Contabilidade.
