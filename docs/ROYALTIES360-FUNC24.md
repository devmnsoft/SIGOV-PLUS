# Royalties360 — FUNC24

## Finalidade

O Royalties360 governa previsão, ingresso, conciliação, aplicação e transparência de royalties e participações governamentais no contexto obrigatório de tenant e entidade. O módulo referencia os cadastros financeiros, orçamentários, organizacionais, contratuais e de pessoas jurídicas existentes; não cria empenhos, liquidações, pagamentos, contratos ou obras paralelos.

## Jornadas e controles

- **Normas e origem:** regra versionável com fonte normativa, fórmula textual, vigência, índice e fonte do dado. Regra vigente somente recebe nova versão; revogação requer justificativa.
- **Previsão:** competência, exercício, fonte, cenário e premissas, sem valores negativos e com revisão motivada.
- **Realização e repasse:** bruto, deduções, líquido, data do crédito e conciliação com arrecadação persistida. A chave natural impede repetição operacional e divergências permanecem explícitas.
- **Aplicação e projetos:** reserva por fonte, programa/ação e projeto; acompanhamento físico-financeiro referencia Obras360, Energia360, Carbono360 e contratos quando houver vínculo real.
- **Transparência:** somente publicações marcadas como públicas, publicáveis e aprovadas; CSV neutraliza prefixos interpretáveis por planilhas e nunca inclui atributos pessoais.
- **Alertas:** queda de arrecadação, divergência, atraso, prestação pendente, vínculo ausente, fonte/parâmetro vencido e execução órfã, com providência ou justificativa para encerramento.

## Limite de integração externa

Esta entrega **não simula** ANP, STN ou Tesouro. Sem adaptador contratado e configurado, o ingresso é manual controlado, identificado pela fonte e auditável. Falhas de schema, contexto ou catálogo financeiro são exibidas como erro; não existe catálogo alternativo.

## Rotas

A raiz é `/Royalties`; dashboard, parâmetros, fontes, áreas/campos, previsões, repasses/conciliação, planos, projetos/acompanhamento, execução/saldos, transparência, alertas, governança e relatórios são MVC/Razor reais. `/Transparencia/Royalties` é a entrada pública de publicações aprovadas.
