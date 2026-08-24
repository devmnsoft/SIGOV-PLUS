# FUNC06 — Saúde, Atenção Básica e Regulação

## Escopo entregue

FUNC06 fecha a jornada municipal de unidades, pacientes/responsáveis, profissionais/equipes, agenda, acolhimento, atendimento e prontuário SOAP, procedimentos, vacinação, medicamentos/dispensação e encaminhamentos. A migration corretiva reaproveita as tabelas `sigov.saude_*` publicadas e adiciona somente contratos ausentes, sempre com PK `bigint identity`, tenant e entidade.

O dashboard `/Saude` e as telas MVC usam as APIs persistentes; não há catálogo ou sucesso em memória. Listagens e CSV não apresentam queixa, SOAP, justificativa clínica nem documentos completos. CPF/CNS são mascarados pela camada LGPD existente, e acesso/retificação de prontuário exige permissão e auditoria com finalidade.

## Regras defendidas

* Agenda impede conflito de profissional/horário e bloqueia unidade ou profissional inativo; cancelamento exige motivo.
* Acolhimento finalizado exige classificação AZUL, VERDE, AMARELO, LARANJA ou VERMELHO; o dashboard destaca LARANJA/VERMELHO sem revelar conteúdo clínico.
* Evolução finalizada é imutável; correção cria retificação vinculada, justificada e auditada.
* Vacinação rejeita lote vencido, cancelamento sem justificativa e dose duplicada sem motivo.
* Dispensação exige quantidade positiva e medicamento ativo. Quando `material_id` está vinculado ao Almoxarifado, o saldo é obrigatório; sem vínculo seguro a aplicação/dispensação fica registrada sem baixa automática e a integração permanece explicitamente pendente.
* Regulação exige histórico de status, justificativa na devolução e destino, profissional e data no agendamento.

## Banco e autorização

A migration `20260825000000_func06_saude_atencao_basica.sql` completa `saude_unidade`, `saude_paciente`, `saude_profissional`, `saude_agenda`, `saude_atendimento` e `saude_vacinacao`, e cria responsáveis, equipes/vínculos, acolhimento, evolução, medicamento, dispensação, encaminhamento, histórico e auditoria. Índices cobrem escopo, atores, status e datas; CPF, CNS e CNES opcionais são únicos no escopo.

São criadas idempotentemente as 27 permissões `saude.*` solicitadas, incluindo `saude.prontuario.visualizar`, `saude.prontuario.retificar` e `saude.exportar`. A decisão continua persistida e fail-closed.

## Rotas

MVC: `/Saude`, `/Saude/Unidades[/Nova]`, `/Saude/Pacientes[/Novo|/Detalhe/{id}]`, `/Saude/Profissionais[/Novo]`, `/Saude/Equipes`, `/Saude/Agenda[/Nova]`, `/Saude/Acolhimentos[/Novo]`, `/Saude/Atendimentos[/Novo|/Detalhe/{id}]`, `/Saude/Vacinacao[/Nova]`, `/Saude/Farmacia[/Dispensar]` e `/Saude/Regulacao[/Novo]`.

API: dashboard, unidades, pacientes, profissionais, agenda, atendimentos/prontuário, vacinação, farmácia, regulação e exportações sob `/api/saude`; os contratos preexistentes foram reaproveitados. Operações sem autorização persistida retornam falha e nenhuma coleção demonstrativa.

GED/InovaGED permanece adiado para a etapa final. FUNC06 não promove a RC50.68, que continua **BLOCKED**, e não inicia nem marca RC50.69.
