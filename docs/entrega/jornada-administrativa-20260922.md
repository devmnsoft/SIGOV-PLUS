# Jornada administrativa — auditoria e incremento de segurança (2026-09-22)

## Baseline e escopo auditado

- Branch auditada: `work`; base: `b6f8b406af740bbf4fd88ba417914a9b594f627b`.
- A árvore estava limpa no início. Runtime declarado: SDK .NET `10.0.100`, `net10.0`, C# 14; persistência PostgreSQL/Dapper.
- Autenticação Web: cookie `SIGOV.AUTH`, sessão persistida em `sigov.identidade_sessao`, validada a cada principal autenticado. Autorização: políticas resolvidas pelo avaliador persistido; tenant, entidade e exercício são claims/contextos distintos.
- O ambiente não contém `dotnet`, `docker` nem `psql`. Restore, build, testes, inicialização, instalação/upgrade de banco e navegação real ficaram **BLOQUEADOS**; nenhum deles é registrado como aprovado.
- Auditoria limitada das entregas anteriores: recebimento de Compras, tramitação de Processo e matrícula/boletim de Educação foram conferidos da rota ao repositório e possuem implementação persistida. A execução integrada continuou bloqueada pela ausência do runtime e do PostgreSQL.

## Jornada e classificação

| Capacidade | Classificação | Evidência e observação |
|---|---|---|
| Listar/consultar clientes | IMPLEMENTADA COM EVIDÊNCIA ESTÁTICA | `SaasAdminController.Tenants/TenantDetalhe` → `SaasTenantAdministrationService.ListAsync/GetAsync` → consultas Dapper em `sigov.tenant`, entidades, usuários e contratos. |
| Configurar entidades/unidades | PARCIAL | Consulta entidades vinculadas; criação/configuração ocorre em superfícies Core/SaaS separadas e não foi executada ponta a ponta. |
| Contratar/suspender/reativar módulo | IMPLEMENTADA COM EVIDÊNCIA ESTÁTICA | Comandos transacionais, dependências do catálogo persistido, vigência, concorrência otimista e auditoria antes/depois. |
| Criar usuário | QUEBRADA, CORRIGIDA NESTE INCREMENTO | Usava hash literal compartilhado. Agora gera segredo aleatório não revelado pelo `IPasswordHashService`, exige definição pelo fluxo canônico de recuperação e mantém criação/auditoria atômicas. |
| Atribuir perfil inicial | QUEBRADA, CORRIGIDA NESTE INCREMENTO | A busca aceitava perfil/grupo sem escopo. Agora ambos precisam pertencer ao tenant alvo e uma falha reverte também o usuário recém-criado. |
| Bloquear/inativar usuário | PARCIAL, CORRIGIDA NESTE INCREMENTO | O cadastro mudava, mas sessões abertas sobreviviam. Agora as sessões do usuário são revogadas na mesma transação. Desbloquear não recria perfil ou sessão. |
| Bloquear/suspender/inativar cliente | PARCIAL, CORRIGIDA NESTE INCREMENTO | O status e a auditoria já eram persistidos. Agora todas as sessões abertas do tenant são revogadas atomicamente; reativar não reabre sessões nem permissões. |
| Recuperar acesso | IMPLEMENTADA COM EVIDÊNCIA ESTÁTICA | Token criptográfico, hash persistido, validade de 30 minutos, uso único e destinatário obtido da conta; envio falho revoga o token. Depende de SMTP configurado. |
| Permissões efetivas | IMPLEMENTADA COM EVIDÊNCIA ESTÁTICA | Catálogo e decisões vêm do banco; handlers não dependem apenas da visibilidade do menu. Execução real não verificada neste ambiente. |
| Troca de contexto | NÃO VERIFICADA NESTE CICLO | Existe implementação dedicada e cookie administrativo, mas é mudança P0/P1 fora desta RC e requer validação própria, inclusive múltiplas abas. |
| Auditoria administrativa | PARCIAL | As mutações auditadas gravam ator, tenant, ação, antes/depois e correlação. Filtros/exportação existem em superfícies próprias, sem ensaio no navegador. |
| Ajuda contextual/template | PARCIAL | O detalhe do cliente explica módulos e impactos. O cadastro de usuário agora explica senha inicial, pré-requisito do perfil e próximo passo; não foi declarada cobertura global. |

## Regras efetivamente aplicadas

1. **Requisito confirmado:** não publicar credencial universal. Todo usuário administrativo novo recebe hash PBKDF2 de material aleatório criptográfico que não é retornado nem registrado.
2. **Comportamento canônico existente:** a senha inicial é definida por `Auth/EsqueciMinhaSenha`, cujo token é temporário, de uso único e vinculado à conta destinatária.
3. **Requisito confirmado e schema atual:** perfil e grupo inicial devem ter `tenant_id` igual ao cliente alvo. Perfil inexistente ou fora do escopo aborta a transação completa.
4. **Requisito confirmado:** bloqueio não exclui dados. Bloqueio/inativação encerra sessões abertas imediatamente na confirmação transacional; reativação futura não restaura sessão nem concessão revogada.
5. **Comportamento existente preservado:** autorização da operação administrativa continua em `IAuthorizationAdminService`; não foi criado bypass, catálogo paralelo ou remoção de filtro comum.

## Matriz de validação deste incremento

| Pré-condição → ação | Resultado esperado | Estado / evidência |
|---|---|---|
| SuperAdmin autorizado → cria usuário sem perfil | Conta persistida sem senha conhecida; recuperação obrigatória | PASSOU em revisão estática; teste de banco BLOQUEADO. |
| Perfil de outro tenant/inexistente → cria usuário | Comando recusado e transação revertida | PASSOU em revisão SQL; teste de banco BLOQUEADO. |
| Usuário com sessão → bloquear/inativar | Cadastro alterado, sessão encerrada e evento auditado atomicamente | PASSOU em revisão SQL; teste HTTP/banco BLOQUEADO. |
| Tenant com sessões → bloquear/suspender/inativar | Todas as sessões do tenant encerradas; dados preservados | PASSOU em revisão SQL; teste HTTP/banco BLOQUEADO. |
| Reativar usuário/tenant | Nenhuma sessão ou perfil revogado é recriado | PASSOU em revisão do comando; integração BLOQUEADA. |
| Compras → confirmar recebimento repetido | Idempotência e releitura do resultado persistido | IMPLEMENTADA COM EVIDÊNCIA ESTÁTICA; NÃO EXECUTADO. |
| Processos → movimentar com estado esperado | Permissão, transição e histórico persistido | IMPLEMENTADA COM EVIDÊNCIA ESTÁTICA; NÃO EXECUTADO. |
| Educação → matrícula e boletim | Concorrência de vaga, persistência e consulta de resultado | IMPLEMENTADA COM EVIDÊNCIA ESTÁTICA; NÃO EXECUTADO. |

## Compatibilidade, recuperação e pendências

Não houve alteração de schema; portanto não há migration ou consolidados a sincronizar. A mudança usa colunas já exigidas pelas migrations publicadas (`deve_alterar_senha`, `tenant_id` e `identidade_sessao`). Em falha, cada comando executa rollback da transação local; não se promete rollback de efeitos externos do envio SMTP.

Próximo incremento recomendado: RC própria para troca de contexto/múltiplas abas e avaliação de autorização; depois, ensaio integral em PostgreSQL descartável (instalação limpa, upgrade, reaplicação, isolamento e concorrência) e inventário navegável da ajuda contextual. Também permanecem pendentes o fluxo de revogação explícita de vínculo e a validação no navegador em celular, tablet e desktop.
