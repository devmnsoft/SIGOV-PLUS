# SIGOV Indústria e Produção

O módulo `industria_producao` atende fábricas, oficinas técnicas, prestadores industriais e produção sob ordem. Todos os cadastros e movimentos operacionais são isolados por `tenant_id`, exigem módulo contratado e gravam auditoria nas alterações.

## Fluxo industrial

1. Cadastrar centros de trabalho e recursos produtivos.
2. Configurar produtos industriais e vínculo opcional com produto comercial/estoque.
3. Criar ficha técnica (BOM) e roteiro de produção.
4. Abrir ordem de produção (OP), liberar, iniciar e apontar execução.
5. Consumir materiais, registrar produção acabada, refugo e inspeções.
6. Concluir a OP somente com apontamento e sem bloqueio de qualidade obrigatório.
7. Calcular custos por material, horas/recurso e refugo.

## Jornada operacional verificada no código

| Etapa | Implementação existente | Lacuna preservada | Regra aplicada | Evidência automatizada |
|---|---|---|---|---|
| Ordem | API cria a OP vinculada ao produto e preserva os identificadores da ficha e do roteiro | formulário operacional especializado | produto ativo, quantidade positiva e ficha obrigatória quando configurada | `PostBuild06IndustriaTests` e build com nullable habilitado |
| Disponibilidade e reserva | `IIndustriaEstoqueService` consulta disponibilidade e reserva no estoque canônico | transação única entre OP e estoque | saldo físico, reservado e disponível permanecem distintos | testes existentes do serviço de integração |
| Apontamento e consumo | endpoints registram apontamento e consumo vinculados à OP | idempotência por conteúdo para todo apontamento | ordem e produto pertencem ao tenant; reserva não é consumo | testes existentes de API/integração |
| Qualidade | inspeções ficam vinculadas à OP e podem bloquear a conclusão | versionamento dos critérios | nenhum resultado é presumido como aprovado | testes de contrato da API |
| Encerramento | conclusão exige apontamento e verifica qualidade obrigatória | reconciliação transacional integral e custo final | ordens concluídas/canceladas não retornam a estados mutáveis | testes de estados da API |

## Transições e responsabilidades

- **Atores:** PCP cria/libera; operador inicia, pausa e aponta; qualidade julga inspeções; perfil autorizado conclui ou cancela.
- **Pré-condições:** tenant, usuário, contratação e permissão válidos; produto ativo; quantidade positiva; ficha técnica quando exigida.
- **Transições:** `PLANEJADA → LIBERADA → EM_PRODUCAO ↔ PAUSADA → CONCLUIDA`; cancelamento encerra a ordem conforme a permissão vigente.
- **Efeitos:** reservas, consumos, produção acabada e inspeções são registros próprios; mudar o estado não os simula.
- **Cancelamento e correção:** estados terminais não podem ser reabertos; efeitos consolidados exigem estorno/compensação rastreável.
- **Concorrência:** os serviços de estoque protegem o saldo; a reconciliação integral concorrente da OP permanece no backlog e impede promover o módulo além de `PARCIAL`.

Na abertura de manutenção a partir de uma parada, `entidade_id` e `usuario_id` nulos significam contexto operacional incompleto. A API retorna erro de validação antes de consultar ou gravar, sem escolher entidade padrão nem fabricar identidade. A parada inexistente ou pertencente a outro tenant continua sendo tratada como não encontrada; esfera inválida continua sendo inconsistência de cadastro.

## Segurança e SaaS

O acesso exige `industria_producao` em `tenant_modulo_contratado` e permissões `industria.*`. Perfis sugeridos: GERENTE_INDUSTRIAL, PCP, OPERADOR_PRODUCAO, QUALIDADE e MANUTENCAO.
