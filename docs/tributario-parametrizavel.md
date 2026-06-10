# Tributário parametrizável inicial

## Objetivo

Preparar o módulo Tributário do SIGOV para municípios com configuração inicial por tenant, tipos de cadastro e campos dinâmicos, sem implementar ainda IPTU, ISS, taxas, dívida ativa ou arrecadação completa.

## Configurações iniciais

A tabela `sigov.tributario_configuracao` guarda máscaras de inscrição imobiliária/mobiliária e flags de georreferenciamento, NFS-e e protesto.

## Tipos de cadastro

`sigov.tributario_tipo_cadastro` nasce com seeds por tenant:

- `CONTRIBUINTE`.
- `IMOVEL`.
- `ECONOMICO`.

## Campos dinâmicos

`sigov.tributario_campo_dinamico` permite configurar campos por tipo de cadastro com tipo, obrigatoriedade, ordem e opções JSON.

## Contribuintes

`sigov.tributario_contribuinte` contém nome, documento, e-mail, telefone, tipo de pessoa e `dados_json`. Listagens mascaram dados pessoais em conformidade LGPD.

## Imóveis

`sigov.tributario_imovel` contém inscrição única por tenant, contribuinte relacionado, endereço JSON e áreas.

## Econômicos

`sigov.tributario_economico` contém inscrição única por tenant, contribuinte, nome fantasia, atividade principal e dados complementares JSON.

## Próximas etapas

- Regras de inscrição por máscara.
- Cadastro avançado de imóveis e econômicos.
- Lançamento de IPTU, ISS e taxas.
- Dívida ativa e protesto.
- Arrecadação, DAM/boletos e PIX real.
- Relatórios gerenciais e BI tributário.
