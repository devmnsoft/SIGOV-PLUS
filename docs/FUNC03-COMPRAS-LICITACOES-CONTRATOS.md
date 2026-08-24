# FUNC03 — Compras Públicas, Licitações, Contratos e Atas

## Entrega funcional
O módulo cobre o cadastro de fornecedores (documento mascarado em consultas e CSV), solicitações com origem manual/Almoxarifado/Patrimônio, processos com catálogo persistente de modalidade e critério, histórico de fases, cotações, julgamento, homologação, contratos e atas com vigência.

A contratação exige processo `HOMOLOGADO` e fornecedor `ATIVO`. Propostas vencidas não são classificadas; desclassificação exige justificativa; homologação exige todos os itens julgados ou fracassados. O dashboard consulta exclusivamente o PostgreSQL.

## Persistência e integração
A migration `20260824200000` cria as tabelas `compras_fornecedor`, `compras_solicitacao` e itens/histórico, `compras_processo` e itens/histórico, `compras_cotacao`, `compras_julgamento`, `compras_contrato` e histórico, `compras_ata_registro_preco`, itens/consumos, `compras_recebimento`, itens, parâmetros e auditoria.

O recebimento referencia processo e exatamente um contrato ou ata. Itens referenciam material do Almoxarifado e a pendência patrimonial criada pelo fluxo seguro de FUNC02. Unicidades de documento e item impedem duplicação; não há tombamento incompleto. A gravação operacional do recebimento fica disponível no contrato persistente e deve usar a permissão `compras.recebimento.executar`.

## RBAC e LGPD
São criadas as 19 permissões `compras.*` solicitadas. Controllers Web/API consultam o avaliador persistente e negam por padrão. Auditorias registram antes/depois, usuário e correlação. CPF/CNPJ é retornado mascarado em listagens e exportações.

## Interface e documentos
As rotas `/Compras`, fornecedores, solicitações, processos/cotações/julgamento, contratos e atas possuem páginas Razor administrativas e estados vazios. Metadados de documentos são persistidos em JSONB; binários não são aceitos porque não foi identificada infraestrutura segura específica para anexos nesta trilha.

## Estado de release
FUNC03 é trilha paralela e não promove release. RC50.68 continua **BLOCKED** por ambiente/CI/runtime/PostgreSQL oficiais. RC50.69 não foi iniciada.
