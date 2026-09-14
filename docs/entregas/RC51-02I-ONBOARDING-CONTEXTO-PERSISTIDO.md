# RC51.02I — onboarding contextual e persistido

Data: 2026-09-11. Estado: **IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED**.

## Inspeção

| Jornada | Estado real | Lacuna encontrada | Dependência | Arquivos canônicos | Aceite desta fatia |
|---|---|---|---|---|---|
| Fundação/MinhaCentral | implementada sem runtime | PostgreSQL 16 e sessões reais indisponíveis | SDK 10.0.100, PostgreSQL 16 | `MinhaCentralService`, autenticação persistida | regressão runtime permanece bloqueada |
| Implantação SaaS | parcial | API e Web devolviam 12 passos hardcoded, marcavam 3 concluídos e a Web fixava tenant 1 | `onboarding_jornada`, `onboarding_etapa`, `onboarding_tarefa` | Application/Onboarding, Infrastructure/Onboarding, controllers API/Web | somente o tenant autenticado é consultado; ausência de jornada retorna 404; nenhum progresso fictício |
| Planejamento/produtividade industrial | parcial/estrutura | pré-requisitos e Gate A/B sem evidência | Gate A e B | serviços Industria existentes | não iniciado nesta fatia |
| Avaliações acadêmicas | parcial | frequência e critérios não homologados | Gate A–C | serviços Educacao existentes | não iniciado nesta fatia |
| Contratos/Frotas/Saúde/Atendimento | parcial | jornadas integradas sem evidência runtime | gates anteriores | serviços existentes por módulo | não iniciado nesta fatia |
| GED | bloqueada | deve permanecer por último | conclusão das frentes anteriores | estrutura GED existente | nenhuma alteração |

## Correção entregue

O serviço de onboarding deixou de fabricar catálogo e progresso. A consulta agora é assíncrona, Dapper, parametrizada por `tenant_id` e `jornada_id`, e usa as tabelas já publicadas. API e MVC exigem autenticação e o tenant resolvido pelo contexto; um identificador diferente do contexto recebe `403`. Quando não existe jornada persistida, retorna `404`, sem roteiro padrão ou sucesso aparente.

Não houve migration: a correção reutiliza o schema publicado em `20260607090000_ui_commercial_finish.sql`. Nenhum catálogo, sessão ou mecanismo de autorização adicional foi criado.

## Evidência e bloqueios

- Inspeção inicial: branch `work`, HEAD `75bdee0d096d6c948f4fea9e21757eb7f6c20420`, árvore limpa, sem remoto/upstream.
- SDK normativo: .NET `10.0.100`; projetos Domain, Application, Infrastructure, Api, Web, Worker e três suítes existentes.
- `dotnet`, `pwsh`, `psql`, PostgreSQL 16, Docker e navegador não estão disponíveis. A tentativa de obter o instalador oficial do .NET foi bloqueada por HTTP 403.
- Portanto build, testes, runtime, PostgreSQL e screenshots reais permanecem **BLOCKED**, sem promoção da jornada a funcional ou homologada.

## Próximo item exato

Disponibilizar .NET SDK 10.0.100 e PostgreSQL 16; executar os gates obrigatórios e provar, com duas sessões/tenants, que `/api/onboarding` e `/Onboarding` nunca retornam jornada cruzada e que ausência de configuração permanece 404. Critério: build/testes verdes, consultas reais isoladas e screenshot real em 390, 768, 1366 e 1920 px. Depois retomar convites persistidos, concorrência de aceite e invalidação de autorização; indústria continua aguardando Gate B.
