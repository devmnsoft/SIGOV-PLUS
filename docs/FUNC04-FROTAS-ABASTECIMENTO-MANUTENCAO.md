# FUNC04 — Frotas, Abastecimento e Manutenção

## Visão Geral e Arquitetura Multi-Esfera

O módulo FUNC04 gerencia o ciclo operacional e patrimonial de transportes da administração pública (municipal, estadual e federal).
Alinhado aos padrões estabelecidos em FUNC01 (Patrimônio), FUNC02 (Almoxarifado) e FUNC03 (Compras), as operações de frotas são orientadas a nomes amigáveis (sem IDs expostos ou digitados), validação fail-closed no serviço, integridade atômica com o PostgreSQL e proteção LGPD.

## Pontes e Integrações Operacionais

1. **Vínculo Patrimonial (FUNC01 &larr; Veículo):**
   - O veículo pode ser associado a um bem patrimonial ativo (`patrimonio_bem_id`), selecionado em catálogo filtrado por ente e tenant.
   - Bens com status `BAIXADO` ou pertencentes a outro ente são terminantemente recusados pelo serviço.
   - Um bem patrimonial ativo não pode ser vinculado a mais de um veículo ativo simultaneamente.
   - Na ficha detalhada do veículo (`/Frotas/Veiculos/Detalhe/{id}`), o número e a descrição do bem tombado são exibidos com atalho direto para a ficha patrimonial.

2. **Utilizações, Hodômetro e Bloqueio de Manutenção:**
   - **Unique Parcial de Uso:** Cada veículo pode ter no máximo uma utilização em aberto (`status = 'ABERTO'`) por vez (`ux_frotas_util_aberta`).
   - **Condição Operacional:** Para abrir nova utilização, o veículo deve estar estritamente com status `ATIVO`. Veículos em `EM_MANUTENCAO`, `INATIVO` ou `BAIXADO` têm a abertura recusada no serviço e desabilitada na interface.
   - **Regularidade do Condutor:** O motorista deve estar cadastrado como `ATIVO` e com CNH válida (`validade >= hoje`). CNH vencida impede a abertura de utilização.
   - **Progressão de Quilometragem:** A quilometragem de saída não pode ser inferior à atual do veículo. Na finalização (`/Frotas/Utilizacoes/{id}/Finalizar`), exige-se `km_retorno >= km_saida` e data de retorno posterior à de saída, atualizando a quilometragem do veículo atomicamente.

3. **Abastecimento:**
   - Litros > 0, valor unitário &ge; 0 e valor total calculado atomicamente.
   - Atualiza o hodômetro do veículo se a quilometragem informada for superior à atual (nunca retroage).
   - Referencia fornecedores e contratos do módulo de Compras (FUNC03) por nome amigável.
   - **O que NÃO faz:** O abastecimento não gera baixa implícita de combustível no almoxarifado (sem contrato seguro de consumo em estoque nesta evolução).

4. **Manutenções e Ordens de Serviço (Ponte Atômica com FUNC02):**
   - **Manutenção:** Ao abrir manutenção, o status do veículo é alterado para `EM_MANUTENCAO` na mesma transação. Ao concluir, o status só retorna para `ATIVO` se não houver outras manutenções ou OS abertas.
   - **Peças e Materiais de Consumo:** Cada item lançado na OS como peça/material é vinculado a um material de `CONSUMO` ativo do Almoxarifado (FUNC02) e respectivo almoxarifado. Materiais do tipo permanente são terminantemente rejeitados como peças de manutenção veicular.
   - **Conclusão com Baixa Atômica:** Na conclusão da OS (`AlterarOrdemAsync` com ação `concluir`):
     - Executa lock pessimista (`SELECT FOR UPDATE`) nas linhas correspondentes em `almoxarifado_estoque`;
     - Verifica se `saldo >= quantidade` para cada item; se faltar saldo, a conclusão é abortada e a transação revertida (rollback total);
     - Grava o débito em `almoxarifado_movimentacao` com tipo `SAIDA`, atualiza o saldo físico em estoque e associa o ID da movimentação na linha da OS (`movimentacao_almoxarifado_id`);
     - Na interface (`/Frotas/OrdensServico/Detalhe/{id}`), o botão "Concluir OS e Baixar Estoque" é desabilitado com aviso explicativo (`title`) se qualquer peça estiver com saldo insuficiente.
   - **Aprovação e Cancelamento:** Ações de cancelamento e recusa exigem justificativa formal gravada no histórico auditável (`sigov.frotas_ordem_servico_historico`).

## Proteção de Dados e LGPD

- O CPF de condutores é exibido mascarado (`***.123.456-**`) em consultas, formulários de detalhe e exportações CSV.
- Exportações CSV em `/Frotas/Exportar` utilizam codificação UTF-8 com BOM, proteção sanitária contra injeção de fórmulas (`=`, `+`, `-`, `@`, `\t`, `\r`) e limite de segurança de 5.000 registros.
- Documentos de veículos armazenam estritamente metadados operacionais e prazos de validade; não há upload ou trânsito de binários ou arquivos desprotegidos.

## Rotas MVC e Endpoints

- `/Frotas` — Painel Operacional (Dashboard) com métricas reais, CNHs/documentos a vencer (30 dias) e resumo de regras vigentes.
- `/Frotas/Veiculos` — Listagem com filtros, status badges e vínculo patrimonial.
- `/Frotas/Veiculos/Novo` e `/Frotas/Veiculos/Editar/{id}` — Cadastro com seleção de bem patrimonial ativo por nome (sem ID digitado).
- `/Frotas/Veiculos/Detalhe/{id}` — Ficha do veículo com dados técnicos, hodômetro, link ao bem patrimonial e atalhos operacionais.
- `/Frotas/Motoristas` e `/Frotas/Motoristas/Novo` — Gestão de condutores autorizados com controle de validade de habilitação.
- `/Frotas/Utilizacoes`, `/Frotas/Utilizacoes/Nova` e `/Frotas/Utilizacoes/{id}/Finalizar` — Registro de saídas, bloqueios por manutenção/CNH e encerramento de percurso.
- `/Frotas/Abastecimentos` e `/Frotas/Abastecimentos/Novo` — Registro de consumo e avanço de hodômetro.
- `/Frotas/Manutencoes` e `/Frotas/Manutencoes/Nova` — Abertura de serviços mecânicos com transição para `EM_MANUTENCAO`.
- `/Frotas/OrdensServico`, `/Frotas/OrdensServico/Nova` e `/Frotas/OrdensServico/Detalhe/{id}` — Emissão de OS, verificação visual de saldo e baixa atômica de peças no almoxarifado.
- `/Frotas/OrdensServico/{id}/Acao` — Transição de estado (Aprovar, Concluir com baixa atômica, Cancelar com justificativa).
- `/Frotas/Documentos` — Controle de prazos de vencimento de licenciamento, seguros e certificados.
- `/Frotas/Exportar` — Exportação CSV em conformidade com LGPD e auditoria.

## Limites Explícitos do Módulo (O que NÃO faz)

- **Não gera empenho contábil:** A execução financeira e orçamentária é restrita ao módulo de Contabilidade e Empenhos (FUNC10).
- **Não baixa combustível do estoque implicitamente:** Apenas peças e materiais de consumo aplicados em OS realizam baixa física no estoque do almoxarifado.
- **Não promove release:** A RC50.68 permanece formalmente mantida como **BLOCKED** e a RC50.69 não foi iniciada.
