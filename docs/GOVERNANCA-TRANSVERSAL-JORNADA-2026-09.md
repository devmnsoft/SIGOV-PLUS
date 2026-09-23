# Governança transversal — jornada de tratamento (2026-09)

## Baseline e diagnóstico

Baseline auditado no branch `work`, commit `97e6e6b`. A correção anterior mantém a defesa `Tenant()` no serviço e passou a interromper as telas de Pendências e Qualidade antes da consulta quando não existe contexto. O contexto canônico é request-scoped e combina `ITenantContext` (API) com o snapshot persistido autorizado (Web), sem aceitar tenant de query string.

| Capacidade | Situação inicial | Evidência / problema | Mudança e regra preservada | Aceite deste ciclo |
|---|---|---|---|---|
| Tenant obrigatório | IMPLEMENTADA COM EVIDÊNCIA | Controller Web evita a consulta; serviço ainda exige tenant | Defesa mantida em todos os métodos novos | Sem contexto mostra seleção e não consulta |
| Isolamento | IMPLEMENTADA COM EVIDÊNCIA | SQL existente filtra `tenant_id`; detalhe/comandos não existiam | Todos os detalhes, elegibilidade, updates e histórico exigem o tenant canônico | ID de outro tenant resulta em não encontrado/conflito |
| Atribuição | AUSENTE APÓS BUSCA | Havia coluna de responsável, sem comando/histórico/concurrency token | Comando autorizado, responsável ativo do tenant, justificativa, transação e `versao` otimista | Redistribuição preserva prazo e gera evento |
| Detalhe e histórico | AUSENTE APÓS BUSCA | Somente listagem e URL persistida eram exibidas | Detalhe autorizado, rota relativa validada e histórico de negócio persistente | Jornada navegável a partir das duas listas |
| Revalidação | AUSENTE APÓS BUSCA | Abrir a origem não verificava nem concluía | Revalidação consome apenas sinal novo publicado pela origem; bloqueio/indisponibilidade não vira correção | Resultado antigo não sobrepõe versão nova |
| Recorrência | PARCIAL | Índice aberto evita duplicação ativa, mas não havia evento próprio | Episódios resolvidos são preservados; produtores podem abrir novo episódio pela identidade existente | Histórico antigo não é apagado; automação de recorrência segue pendente |
| Lote | AUSENTE APÓS BUSCA | Nenhum contrato seguro existente | Não implementado para evitar seleção implícita ou SQL genérico | NÃO EXECUTADO neste ciclo |
| Notificações/comentários/anexos | PARCIAL | Infraestrutura transversal existe, mas não há vínculo canônico com essas ocorrências | Não foi criada segunda autoridade | Integração específica permanece pendente |

## Modelo e transições

A fonte continua responsável pelo estado do registro de domínio. `pendencia_operacional` e `qualidade_dados_ocorrencia` acompanham o trabalho. `governanca_ocorrencia_historico` registra eventos de tratamento, não logs técnicos.

Transições permitidas neste ciclo:

- `ABERTA -> EM_TRATAMENTO` (pendência) ou `ABERTA -> EM_CORRECAO` (qualidade) somente por atribuição autorizada;
- redistribuição mantém situação e prazo, exige justificativa e incrementa `versao`;
- revalidação de qualidade somente aceita uma observação da origem posterior à última verificação;
- `condicao_presente=true` mantém `EM_CORRECAO`; `false` conclui como `RESOLVIDA`; sinal ausente resulta em `REGISTRO_NAO_DISPONIVEL`; sinal não renovado resulta em `VERIFICACAO_BLOQUEADA`;
- nenhuma ação do navegador escreve diretamente no módulo de origem.

A integração produtora deve publicar atomicamente `condicao_presente` e `origem_verificada_em` depois de consultar sua autoridade real. O token `versao` impede que atribuições ou resultados atrasados sobrescrevam estado mais recente.

## Segurança e interface

As rotas persistidas são aceitas apenas quando relativas, iniciadas por uma única `/`; valores absolutos ou protocol-relative não são renderizados. Abrir a origem reexecuta a autorização do controller de destino. O retorno aceita somente caminho local. Todos os comandos Web têm antiforgery; a API depende do pipeline autenticado e das permissões persistidas.

A tela explica propósito, funcionamento, pré-requisitos, diferença entre corrigir e revalidar, falha/bloqueio e recorrência. Ausência de prazo aparece como **Sem prazo definido**.

## Matriz de aceite executável

| Cenário | Resultado esperado / método | Estado |
|---|---|---|
| Sem tenant | Controller não chama serviço; serviço mantém exceção defensiva | PASSOU por inspeção; execução bloqueada sem SDK |
| Isolamento A/B | Toda chave e mutação combina `tenant_id + id` | PASSOU por inspeção; integração PostgreSQL não executada |
| Atribuição | Usuário ativo do tenant; não altera permissões | PASSOU por inspeção |
| Concorrência | `versao` divergente retorna `CONFLITO` | PASSOU por inspeção |
| Correção | Somente observação nova da origem com condição falsa resolve | PASSOU por inspeção |
| Falha técnica | Sem observação nova retorna bloqueada, nunca resolvida | PASSOU por inspeção |
| Recorrência | Índice ativo permite episódio novo após resolução | PARCIAL; produtor não alterado |
| Resultado atrasado | Token de versão rejeita comando antigo | PASSOU por inspeção |
| Lote | Não disponibilizado | NÃO EXECUTADO |
| Indicadores | Fora do recorte implementado | NÃO EXECUTADO |
| Troca de contexto | Serviço request-scoped e consultas usam snapshot atual | PASSOU por inspeção; smoke bloqueado |
| Template | Lista/detalhe responsivos usam componentes existentes | NÃO EXECUTADO em navegador |
| Regressão | Build/testes | BLOQUEADO: `dotnet` ausente |
| Banco | Migration idempotente e artefatos sincronizados | PASSOU validação estática; PostgreSQL não disponível |

## Limitações conhecidas

Não foram implementados lote, comentários/anexos, indicador agregado, seletor pesquisável por nome, “Minhas pendências”, setor, notificações ou verificador específico de cada módulo. Esses itens exigem contratos de setor, anexos e produtores de origem que não estavam presentes; inventar fallback ou regras seria incompatível com a autoridade persistida. Nenhuma homologação ou cobertura total é declarada.
