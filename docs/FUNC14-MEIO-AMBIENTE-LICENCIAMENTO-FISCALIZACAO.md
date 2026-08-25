# FUNC14 — Meio Ambiente, Licenciamento e Fiscalização

## Escopo entregue
Módulo MVC `/MeioAmbiente` conectado exclusivamente ao PostgreSQL por Dapper/Npgsql, com dashboard, cadastros de empreendedores, empreendimentos, parâmetros, requerimentos, documentos, análises, licenças, condicionantes, vistorias, denúncias, autos, taxas, auditoria e relatórios CSV. Todos os acessos são segregados por `tenant_id` e `entidade_id`; não há catálogo demo, fallback ou integração com GED/Protocolo.

## Persistência
A migration `20260825080000` cria as tabelas `ambiental_empreendedor`, `ambiental_empreendimento`, `ambiental_atividade`, `ambiental_tipo_licenca`, `ambiental_tipo_documento`, `ambiental_requerimento`, `ambiental_documento`, `ambiental_checklist`, `ambiental_analise_tecnica`, `ambiental_parecer`, `ambiental_licenca`, `ambiental_condicionante`, `ambiental_vistoria`, `ambiental_denuncia`, `ambiental_auto`, `ambiental_penalidade`, `ambiental_taxa`, `ambiental_integracao_tributaria`, `ambiental_prazo_alerta` e `ambiental_auditoria`. PKs são `bigint identity`; índices, unicidade contextual de CPF/CNPJ e checks de estados, valores, datas e justificativas são idempotentes.

## Regras
- Deferimento com documento obrigatório pendente exige exceção justificada e gera auditoria.
- Licença exige requerimento deferido; suspensão/cancelamento exigem justificativa.
- Cumprimento de condicionante exige data e evidência/observação; vencidas integram o dashboard.
- Decisões negativas, arquivamentos e cancelamentos exigem justificativa; multas não aceitam valor negativo.
- Listagens comuns não projetam identificação de denunciante sigiloso.
- Documentos armazenam somente metadados/referências; nenhum GED foi implementado ou alterado.
- Tipos, siglas, documentos, taxas, prazos, potencial poluidor e fundamentos são parâmetros administrativos, não afirmações de conformidade legal.

## RBAC e rotas
As 27 permissões `AMBIENTAL_*` solicitadas são persistidas e registradas no catálogo de políticas. Rotas: dashboard e os segmentos `Empreendedores`, `Empreendimentos`, `Parametros`, `Requerimentos`, `Documentos`, `Analises`, `Licencas`, `Condicionantes`, `Vistorias`, `Denuncias`, `Autos`, `Taxas`, `Relatorios` e `Auditoria`, sob `/MeioAmbiente`.

## Relatórios e integração
CSV: empreendedores, empreendimentos, requerimentos, documentos, licenças, condicionantes, vistorias, denúncias, autos, taxas e auditoria. Exportações exigem RBAC, filtram contexto, neutralizam fórmulas e são auditadas. A integração tributária está **preparada/parcial** em tabela própria; não existe emissão falsa de guia ou pagamento e nenhum contrato externo instável foi presumido.

## Limites
A solução não substitui validação jurídica da legislação ambiental local e não contém GIS completo. Coordenadas são persistíveis como valores textuais/estruturados e colunas geográficas simples. Referências de arquivos são metadados; gestão documental permanece fora do FUNC14.
