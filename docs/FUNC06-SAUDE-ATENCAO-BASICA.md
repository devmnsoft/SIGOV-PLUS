# FUNC06 — Saúde, Atenção Básica e Regulação

## Escopo entregue

FUNC06 fecha a jornada municipal de atenção básica no padrão server-first estabelecido nas trilhas FUNC01–05 (unidades, pacientes/responsáveis, profissionais/equipes, agenda, acolhimento com classificação de risco Manchester, atendimento e prontuário SOAP, procedimentos, vacinação, medicamentos/dispensação e regulação/encaminhamentos).

A persistência utiliza estritamente o PostgreSQL 16+ com Dapper e o schema `sigov.saude_*` preexistente, com PKs `bigint generated ... as identity`, tenant e entidade. Não há catalogos mock nem coleções em memória como autoridade.

O dashboard `/Saude` e as telas MVC renderizam server-first com degradação graciosa em empty states. Listagens e exportações CSV não apresentam queixa clínica, notas de evolução SOAP, justificativa clínica nem documentos completos. CPF/CNS são mascarados pela camada LGPD existente, e o prontuário eletrônico é protegido com autorização persistida e auditoria com finalidade (`saude.prontuario.visualizar`).

## Regras defendidas no serviço

* **B1. Unidades e Profissionais:** Unidades ou profissionais inativos não podem ser agendados, não acolhem, não atendem e não realizam dispensação. Listas e seletores operam por nome (unidade, profissional, equipe), eliminando IDs técnicos digitados.
* **B2. Pacientes e LGPD:** CPF e Cartão Nacional de Saúde (CNS) são validados e mascarados em listagens, detalhes e exportações CSV.
* **B3. Agenda:** Validação estrita de sobreposição de horário no serviço impedindo agendamento concorrente do mesmo profissional no mesmo intervalo; recusa unidade/profissional inativo; cancelamento exige motivo obrigatório e confirmação. Horário início deve anteceder o horário fim.
* **B4. Acolhimento e Risco:** Finalização exige classificação de risco Manchester (AZUL, VERDE, AMARELO, LARANJA ou VERMELHO). O dashboard exibe contagens agregadas de LARANJA e VERMELHO sem revelar queixa ou conteúdo clínico.
* **B5. Atendimento e SOAP:** Evolução com status `ATENDIDO` é estritamente imutável no serviço. Correções exigem retificação vinculada com justificativa formal registrada, usuário, correlation ID e auditoria (`saude.prontuario.retificar`). Visualização do prontuário exige `saude.prontuario.visualizar`.
* **B6. Vacinação:** Recusa lotes vencidos (`validade < hoje`), recusa cancelamentos sem justificativa e rejeita registro duplicado da mesma dose e imunizante para o paciente sem motivo justificado. Confirmação explícita com auditoria.
* **B7. Farmácia e Dispensação:** Quantidade deve ser estritamente maior que zero; medicamento deve estar ativo. Quando o medicamento está vinculado a material do almoxarifado (`material_id`), há validação de saldo no estoque; caso não haja saldo suficiente, a dispensação é recusada. Medicamentos sem vínculo de material são registrados sem baixa automática no estoque, e a interface exibe aviso persistente explícito alertando que não houve baixa no almoxarifado.
* **B8. Regulação:** Toda alteração de status gera registro histórico auditável. Devolução e cancelamento exigem justificativa formal e confirmação via diálogo. Agendamento de regulação exige unidade, profissional e data de destino.
* **B9. Exportação CSV:** Permissão `saude.exportar` requerida e auditada. Anonimização e proteção contra injeção de fórmulas (`=+-@\t\r`), sem inclusão de queixa, SOAP ou CID descritivo livre, e codificação UTF-8 com BOM.
* **B10. Permissões:** Respeita estritamente as 27 permissões `saude.*` persistidas no PostgreSQL com comportamento fail-closed.

## Sistema Transversal de Alertas e Mensagens

Implementado no layout compartilhado (`_Alerts.cshtml`, `sigov-alerts.js`, `sigov-components.css`):
1. **Banners de Página (TempData):** `ToastOk`/`Success` (verde), `ToastAviso`/`Warning` (âmbar) e `ToastErro`/`Error` (vermelho) após padrão POST-Redirect-Get. Acessíveis com `role="status"` e `role="alert"`, auto-dismiss em 8 segundos para sucesso e permanência para erros.
2. **Toast Stack:** Notificações flutuantes empilháveis (máximo 3 visíveis simultaneamente), `aria-live="polite"`.
3. **Diálogos de Confirmação (`data-confirm`):** Interceptação global de formulários e botões destrutivos ou irreversíveis, exibindo modal acessível do design system com fallback seguro.
4. **Proteção LGPD:** Mensagens e toasts jamais exibem dados clínicos, queixas, SOAP ou documentos completos.

## O que esta entrega NÃO faz

* Não promove a RC50.68 (permanece **BLOCKED**) e não inicia a RC50.69.
* Não cria novas migrações DDL nem tabelas/colunas adicionais no banco.
* Não abre nem integra GED/InovaGED.
* Não reabre FUNC01–FUNC05 como novas features.
* Não realiza baixa no almoxarifado para dispensações sem vínculo seguro de `material_id`.

## Rotas e Telas MVC

* `/Saude` (Dashboard operacional com cards reais, contagens de risco sem exposição clínica e regras vigentes)
* `/Saude/Unidades` e `/Saude/Unidades/Nova` (Gestão de Unidades de Saúde)
* `/Saude/Pacientes`, `/Saude/Pacientes/Novo` e `/Saude/Pacientes/Detalhe/{id}` (Cadastro e ficha do paciente)
* `/Saude/Profissionais` e `/Saude/Profissionais/Novo` (Gestão de profissionais de saúde e especialidades)
* `/Saude/Agenda` e `/Saude/Agenda/Nova` (Agendamento ambulatorial e cancelamento com motivo)
* `/Saude/Atendimentos`, `/Saude/Atendimentos/Novo` e `/Saude/Atendimentos/Prontuario/{id}` (Acolhimento, Atendimento SOAP e Prontuário Eletrônico com retificação auditada)
* `/Saude/Vacinacao` e `/Saude/Vacinacao/Nova` (Registro de imunização e conferência de lote)
* `/Saude/Farmacia` e `/Saude/Farmacia/Dispensar` (Dispensação de medicamentos e monitoramento de estoque)
* `/Saude/Regulacao` e `/Saude/Regulacao/Nova` (Regulação, encaminhamentos e devoluções justificadas)

