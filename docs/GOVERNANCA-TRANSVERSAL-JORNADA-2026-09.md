# Governança transversal — jornada operacional (2026-09)

## Baseline reconfirmada

A implementação partiu de `be76aff38f03cdad5834c520673dfa37f5695ddd` no branch `work`. A evidência remota informada para o commit foi: workflow .NET aprovado e CI ampla bloqueada pelo preflight de banco e pela ausência do upload obrigatório no job `tracked-artifacts`. Localmente, o SDK .NET 10 e PostgreSQL não estão instalados; portanto build, Razor e testes transacionais permanecem **BLOCKED**, e não são descritos como homologados.

## Causas e decisões

* Listagens tratavam tabela ausente como lista vazia. Agora schema ausente é falha explícita e registrada sem connection string; uma consulta válida sem linhas continua sendo estado vazio.
* Alertas, integrações e status funcional consultavam sem a mesma barreira de contexto das pendências e qualidade. Todas as telas e endpoints agora exigem o contexto canônico request-scoped antes de consultar.
* Atribuição aceitava ID manual, alcançava estados terminais e validava apenas tenant. A tela usa nomes vindos de usuários persistidos; o servidor revalida usuário ativo, desbloqueado e seus vínculos de entidade/exercício, limita a justificativa e atualiza apenas estados tratáveis com versão otimista.
* A revalidação genérica confiava em campos sem um verificador tipado comprovado e podia reabrir uma ocorrência terminal. Até existir um verificador por regra, ela retorna `VERIFICADOR_INDISPONIVEL`, preserva estado/versão, e terminais são idempotentes.
* A conclusão da inspeção resolvia a pendência sem versão nem histórico. O encerramento agora ocorre na mesma transação, exige exatamente um mapping empresarial ativo, incrementa a versão e grava `RESOLVIDA_NA_ORIGEM`. Retry de recebimento terminal retorna o estado persistido antes de repetir estoque/eventos.
* Presença de tabela era apresentada como funcionalidade. O painel agora informa apenas `ESTRUTURA_DETECTADA` ou `NAO_VERIFICADO`, sem fabricar capacidades ou percentual.
* O gate de integridade exigia upload por job, mas `tracked-artifacts` não publicava seu log. O upload foi acrescentado sem remover a exigência; o preflight continua registrando `BLOCKED` quando o secret não existe.

## Matriz antes → depois → cenário → resultado → pendência

| Antes | Depois | Cenário | Resultado | Pendência |
|---|---|---|---|---|
| Contexto desigual | Web e API barram antes do serviço | contexto ausente | erro estável `CONTEXTO_OBRIGATORIO` na API e orientação na Web | execução por perfil BLOCKED |
| Schema ausente = vazio | exceção operacional explícita | tabela removida | não simula ausência de registros | inspeção visual do erro não executada |
| Campo numérico de usuário | seleção nominal persistida | atribuir/redistribuir | somente vínculo ativo do tenant/entidade/exercício; concorrência por `versao` | busca incremental além dos 100 primeiros fica no backlog |
| Terminal podia reabrir | terminal é no-op idempotente | revalidar resolvida/aceita | preserva estado, versão e histórico | nenhum verificador tipado comprovado nesta baseline |
| Sinal legado podia resolver | verificador indisponível é explícito | regra sem autoridade tipada | não resolve nem altera o negócio | implementar produtores tipados por regra |
| Inspeção fechava só status | fechamento canônico transacional | concluir conferência | versão + histórico + referência/ator no mesmo commit | prova PostgreSQL BLOCKED |
| `TableExists` virava “funcional” | somente estrutura/não verificado | status de módulo | leitura honesta | catálogo granular de capacidades continua necessário |
| Job sem artifact | log publicado em `always()` | integridade do workflow | validador estático passa | execução GitHub Actions não realizada localmente |

## Evidências desta execução

### PASS

* `python3 scripts/validate-workflow-integrity.py .github/workflows/ci.yml` — estrutura e exigência de artifacts aprovadas.
* `git diff --check` — patch sem erros de whitespace.

### BLOCKED

* `dotnet build sigov.runtime.slnf --configuration Release --no-restore --nologo -warnaserror` — **BLOCKED**: executável `dotnet` ausente no ambiente.
* Banco limpo, reaplicação, upgrade e cenários concorrentes PostgreSQL 16 — **BLOCKED**: ferramenta/instância/credencial não disponíveis localmente.

### NÃO EXECUTADO

* Navegação por perfis e inspeção visual em 1440/768/390 px, pois o runtime não pôde ser iniciado.
* Evidência de execução remota da CI após o patch; nenhuma publicação, push, merge ou deploy foi realizado.

## Backlog deliberadamente não implementado

Demais produtores tipados de qualidade, comentários/anexos, lote seguro, busca nominal incremental e indicadores avançados permanecem no backlog. Nenhum fallback, catálogo mock ou autoridade paralela foi introduzido.
