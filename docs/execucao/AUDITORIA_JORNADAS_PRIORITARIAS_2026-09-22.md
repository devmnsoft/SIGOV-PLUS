# Auditoria das jornadas prioritárias — 2026-09-22

## 1. Escopo auditado

Esta auditoria leu as regras do repositório, `README.md`, o status real dos módulos,
`CHANGELOG.md`, o manifesto de migrations, os scripts PostgreSQL consolidados e os
inventários existentes. Também inspecionou estaticamente as superfícies de Domain,
Application, Infrastructure, Api, Web e testes: controllers, contratos/DTOs, serviços,
repositórios Dapper, views Razor, layouts, partials, menus e políticas.

A mera presença de um artefato não foi aceita como prova de funcionamento. A
classificação abaixo exige evidência conjunta de build, banco, persistência,
autorização, isolamento, navegação e regra de negócio.

## 2. Gate obrigatório e decisão

**Resultado: BLOCKED. O ciclo não avançou para implementação funcional.**

O preflight registrou:

- SDK .NET 10: `MISSING_TOOL`;
- PowerShell: `MISSING_TOOL`;
- `psql`/PostgreSQL 16+: `MISSING_TOOL`;
- `ConnectionStrings__DefaultConnection`: `MISSING_ENV`;
- navegador e sessão autenticada: indisponíveis neste ambiente.

Consequentemente, não é possível afirmar que `dotnet restore`, build, testes .NET,
clean install, reaplicação, upgrade, startup, login, menu, rotas autenticadas e
isolamento A/B funcionam. Pela ordem explícita do ciclo, iniciar Educação, Saúde/ACS,
Protocolo, Jurídico, integrações, dashboards ou template antes desse gate seria uma
violação. Não houve alteração de schema, migration, script consolidado, controller,
serviço, view ou menu.

## 3. Classificação conservadora por área

| Área | Funcionalidade | Estado anterior | Estado final | Evidência estática | Pendência para promoção |
|---|---|---|---|---|---|
| SaaS/Administração | tenants, entidades, usuários, planos e módulos | Parcial | Parcial | migrations, serviços, controllers e páginas existentes | login e operação real, tenant inativo, entitlement e auditoria em PostgreSQL |
| Segurança/Permissões | autenticação, políticas e contexto institucional | Parcial | Parcial | avaliador persistido, policies e contratos de contexto existentes | acesso direto negado e isolamento tenant/entidade/exercício em runtime |
| Auditoria/LGPD | trilhas, mascaramento e exportação | Parcial | Parcial | tabelas, serviços e rotas existentes | provar cobertura das ações críticas e segregação dos dados sensíveis |
| Educação | ingresso, matrícula, transferência, diário e boletim | Parcial | Parcial | serviço e UI cobrem partes da jornada; transferência é transacional | concorrência da última vaga, calendário, período fechado e perfil responsável |
| Saúde/ACS | território, famílias, cidadãos e visitas | Parcial | Parcial | migrations, serviços, APIs e telas existentes | território ACS, CPF/CNS, visita/pendência, histórico e exportação auditada |
| Protocolo Digital | abertura, tramitação, anexos e prazos | Parcial | Parcial | estruturas persistidas e rotas existentes | conclusão/reabertura, escopo setorial, histórico e alerta em runtime |
| Jurídico | processos, pareceres, movimentos e prazos | Parcial | Parcial | estrutura e políticas persistidas existentes | imutabilidade/histórico do parecer, reabertura e escopo dos relatórios |
| Financeiro | empenho, liquidação e pagamento | Parcial | Parcial | contratos, serviços, APIs e migrations existentes | exercício encerrado, limites e encadeamento contábil em transação real |
| Compras/Contratos | fornecedor, homologação, ata e contrato | Parcial | Parcial | jornadas e migrations corretivas existentes | sanção, vencimento, permissão e rollback concorrente |
| Almoxarifado | entrada, saída, transferência e inventário | Parcial | Parcial | serviço com locks e telas existentes | provar saldo não negativo, retry e inventário concorrente |
| Patrimônio | tombamento, custódia, transferência e baixa | Parcial | Parcial | serviços, histórico e views existentes | baixa motivada e bloqueio de transferência do bem baixado em runtime |
| Frotas | veículos, utilização, abastecimento e manutenção | Parcial | Parcial | regras de hodômetro/OS e views existentes | concorrência, justificativa e veículo inativo em PostgreSQL |
| RH/Folha/Portal do Servidor | servidor, vínculo, frequência, rubricas, folha e autoatendimento | Parcial | Parcial | migrations, contratos, serviços, controllers e páginas existentes | fechamento/reabertura, segregação de dado pessoal e acesso exclusivo do servidor em runtime |
| Saneamento | unidades, ligações, hidrômetros, leituras, faturamento e OS | Parcial | Parcial | migrations FUNC07/RC50.50 e superfícies Application/API/Web existentes | leitura regressiva tratada, competência, inatividade e troca de hidrômetro em PostgreSQL |
| Meio Ambiente | requerimentos, licenças, condicionantes, fiscalização e pareceres | Parcial | Parcial | migration FUNC14, serviços, rotas e páginas existentes | alertas, bloqueio por condicionante, histórico de parecer e auditoria da fiscalização |
| Relatórios | filtros, paginação, CSV e totalizadores | Parcial | Parcial | exportações distribuídas entre módulos | matriz completa de escopo, empty/error state e auditoria sensível |
| Dashboards | SuperAdmin, entidade e pendências | Parcial | Parcial | consultas e views existentes | origem real de cada KPI, falha explícita e segregação por perfil |
| Template/Menu | layout, navegação, formulários e responsividade | Parcial | Parcial | layouts, partials, assets e menus existentes | navegador real, entitlement, rotas, mobile e mensagens obrigatórias |

Nenhuma das dezoito áreas obrigatórias foi classificada como **pronto com evidência**,
**quebrado** ou **ausente** sem execução capaz de sustentar essa conclusão. Os
artefatos existem, mas as jornadas permanecem **parciais**.

## 4. O que estava quebrado e o que foi corrigido

Nenhum novo defeito de produto foi confirmado nesta execução. Foi corrigida uma
lacuna do próprio gate: a governança estática do catálogo de migrations só podia ser
executada por PowerShell, indisponível neste host. O novo verificador Bash valida JSON,
ordenação e unicidade das versões, dependências declaradas, flags, nomes seguros,
checksums normalizados, exceções de prefixo e as dez migrations históricas excluídas
sob governança explícita. Ele não tenta substituir o lexer C# nem a execução no banco
e termina registrando `BLOCKED` para a etapa semântica PostgreSQL.

Os problemas de gate
identificados na auditoria imediatamente anterior (portas, endpoint de health, contrato
da connection string e aceitação de PostgreSQL posterior ao 16) já estão corrigidos no
checkout. Não foi aplicada correção de produto especulativa para contornar ferramenta,
banco ou credencial ausente.

## 5. Validações realizadas

Passaram as verificações que independem do runtime ausente:

- sintaxe de todos os scripts Bash;
- sintaxe JSON do manifesto;
- catálogo com 195 arquivos SQL, 185 migrations registradas e dez órfãs históricas
  classificadas e protegidas por checksum;
- 630 rotas API sem conflito direto detectável pelo gate;
- ausência de artefatos proibidos rastreados;
- colisões estáticas do SQL consolidado;
- suíte JavaScript existente (um teste, uma suíte).

Ficaram bloqueados: restore/build/test .NET, aplicação e reaplicação das migrations,
comparação runtime dos consolidados, startup, login SuperAdmin e tenant, usuário
bloqueado, tenant inativo, módulo não contratado, acesso direto, isolamento entre dois
tenants e navegação manual.

## 6. Entrega e riscos

1. **Auditado:** documentação normativa, catálogo de banco e superfícies das camadas.
2. **Quebrado:** nenhum defeito novo confirmado; o ambiente do gate está incompleto.
3. **Corrigido:** gate estático do catálogo agora executável também sem PowerShell.
4. **Implementado:** verificador Bash fail-closed e este registro auditável do ciclo.
5. **Parcial:** todas as dezoito áreas obrigatórias da matriz.
6. **Regras consolidadas:** nenhuma regra foi promovida sem execução.
7. **Arquivos alterados:** gate Bash, documentação de auditoria, status real e changelog.
8. **Migrations criadas:** nenhuma.
9. **Scripts consolidados atualizados:** nenhum; não houve DDL.
10. **Telas alteradas:** nenhuma.
11. **Menus alterados:** nenhum.
12. **Testes executados:** gates estáticos e JavaScript; testes .NET bloqueados.
13. **Build:** BLOCKED por ausência do SDK 10.0.100.
14. **Migrations:** BLOCKED por ausência de PostgreSQL/`psql` e connection string.
15. **Login/autorização:** não verificados em runtime.
16. **Navegação:** não verificada em navegador autenticado.
17. **Riscos:** críticos para segregação/autorização; altos para banco e concorrência.
18. **Próximo ciclo:** provisionar o Gate A e executar a matriz ponta a ponta antes de
    escolher uma única jornada vertical prioritária.

O critério de conclusão não foi atingido. A decisão correta é manter o ciclo
**BLOCKED**, sem simular sucesso nem ampliar código sobre uma fundação não validada.
