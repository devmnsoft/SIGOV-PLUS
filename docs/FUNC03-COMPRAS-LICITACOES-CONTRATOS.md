# FUNC03 — Compras, Licitações, Contratos e Atas

O módulo **FUNC03** consolida as operações de compras governamentais, licitações, cotações de preços, contratos e atas de registro de preços de acordo com a **Lei nº 14.133/2021** e normas de transparência pública, integrado nativamente ao ecossistema multi-esfera do SIGOV PLUS.

---

## 1. Ciclo de Vida do Processo de Contratação

Os processos de contratação seguem um rito estrito de fases controladas pelo serviço `IComprasService` com regras fail-closed:

```
[PLANEJAMENTO] ──> [PUBLICADO] ──> [DISPUTA] ──> [JULGAMENTO] ──> [HOMOLOGADO]
      │                 │             │              │
      └───> [CANCELADO / ANULADO / DESERTO / FRACASSADO] <───┘
```

### Regras de Transição e Encerramento
- **Tramitação progressiva:** Apenas processos nas fases vigentes podem avançar para a próxima fase.
- **Encerramento terminativo motivado:** Anulação, cancelamento ou declaração de processo deserto/fracassado exige obrigatoriamente justificativa formal técnica/jurídica, gravada no histórico de auditoria.
- **Catálogo de Parâmetros Persistido:** Modalidades (`PREGAO`, `CONCORRENCIA`, `DISPENSA`, `INEXIGIBILIDADE`, etc.) e Critérios de Julgamento (`MENOR_PRECO`, `MAIOR_DESCONTO`, `TECNICA_PRECO`, etc.) são carregados exclusivamente das tabelas persistentes `sigov.compras_parametro_modalidade` e `sigov.compras_parametro_criterio`.

---

## 2. Regras Operacionais de Fornecedores e LGPD

1. **Bloqueio de Inativos e Suspensos:** Fornecedores com status diferente de `ATIVO` são bloqueados pelo serviço para ingresso em novos processos ou recebimento de cotações.
2. **Proteção LGPD:** Em todas as listagens de tela e exportações CSV, os números de documento (CPF e CNPJ) são mascarados (`***.456.789-**` / `**.123.456/****-**`).
3. **Exportação Sanitizada:** A exportação em `/Compras/Fornecedores/Exportar` neutraliza fórmulas de planilha (`=`, `+`, `-`, `@`, `\t`, `\r`), limita a extração a 5.000 registros e emite codificação UTF-8 com BOM para leitura sem corrupção no Microsoft Excel.

---

## 3. Ponte Operacional de Suprimentos

A jornada de suprimentos conecta as quatro fases operacionais de ponta a ponta sem duplicação de schema:

```
[FUNC02] Almoxarifado / Reposição
   │
   ▼ (Gerar demanda de compra)
[FUNC03] Processo de Compra (Fase: PLANEJAMENTO)
   │
   ▼ (Cotação, Julgamento e Homologação)
[FUNC03] Recebimento Físico (/Compras/Processos/{id}/Recebimento)
   │
   ├──> Material de CONSUMO ──> Entrada física em sigov.almoxarifado_movimentacao / estoque
   │
   └──> Material PERMANENTE ──> Entrada física no Almoxarifado E
                                Pendência em sigov.almoxarifado_pendencia_patrimonial
                                     │
                                     ▼
                                [FUNC01] Tombamento Oficial em /Patrimonio/Pendencias
```

### Detalhes de Integração
- **Geração de Demanda (FUNC02 &rarr; FUNC03):** Na tela `/Almoxarifado/Reposicao`, materiais com saldo abaixo do mínimo e sugestão de reposição calculada dispõem do botão *Gerar demanda de compra*. A operação é idempotente: se já existir processo de compra aberto para o material, nova geração é recusada.
- **Recebimento Parcial ou Total (FUNC03 &rarr; FUNC02):** Na tela `/Compras/Processos/{id}/Recebimento`, os itens do processo homologado são conferidos. A quantidade recebida é validada contra o saldo pendente (`quantidade <= saldo a receber`).
- **Conciliação Patrimonial (FUNC03 &rarr; FUNC01):** Materiais do tipo `PERMANENTE` geram registro com status `PENDENTE` em `sigov.almoxarifado_pendencia_patrimonial`. O módulo de Compras **não tomba o bem patrimonial diretamente**; a homologação física e tombamento oficial ocorrem sob governança do setor de patrimônio.
- **Execução Financeira:** O módulo de Compras não emite empenho orçamentário, preservando a autoridade do módulo contábil (FUNC10).

---

## 4. Rotas e Telas do Módulo

| Rota Web | Finalidade |
|---|---|
| `/Compras` / `/Compras/Dashboard` | Visão executiva de compras, alertas e indicadores |
| `/Compras/Fornecedores` | Cadastro e consulta de fornecedores com LGPD |
| `/Compras/Fornecedores/Novo` | Formulário de habilitação cadastral |
| `/Compras/Fornecedores/Exportar` | Exportação de planilha CSV higienizada |
| `/Compras/Solicitacoes` | Demandas internas das secretarias e unidades |
| `/Compras/Solicitacoes/Nova` | Abertura de solicitação com itens |
| `/Compras/Solicitacoes/Detalhe/{id}` | Visualização da demanda e justificativa |
| `/Compras/Processos` | Listagem geral de licitações e contratações diretas |
| `/Compras/Processos/Novo` | Abertura de processo com catálogo de modalidades e materiais |
| `/Compras/Processos/Detalhe/{id}` | Ficha do processo, stepper de fases, histórico e ações |
| `/Compras/Processos/{id}/Cotacoes` | Propostas de fornecedores ativos |
| `/Compras/Processos/{id}/Julgamento` | Adjudicação e deliberação de homologação |
| `/Compras/Processos/{id}/Recebimento` | Recebimento com entrada no Almoxarifado e pendência patrimonial |
| `/Compras/Contratos` | Gestão de contratos administrativos vigentes |
| `/Compras/Contratos/Detalhe/{id}` | Ficha de vigência e saldo do contrato |
| `/Compras/Atas` | Atas de Registro de Preços vigentes |
| `/Compras/Atas/Detalhe/{id}` | Ficha de vigência e saldo da ata |
| `/Compras/LicitaPro` | Inteligência assistida e radar de oportunidades |

---

## 5. LicitaPro IA Integrado

O LicitaPro é uma jornada do FUNC03, acessível em `/Compras/LicitaPro`, e reutiliza `compras_fornecedor`, `compras_processo`, `compras_contrato`, tenant, entidade, usuário e auditoria. Não constitui módulo paralelo e não duplica cadastros.

- Decisões humanas soberanas: o assistente atua na triagem e cálculo de aderência técnica sem automação decisória desregulada.
- Base auditável: dados consultados exclusivamente do PostgreSQL parametrizado.
