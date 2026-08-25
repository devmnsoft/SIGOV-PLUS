# Fechamento FUNC16

## Problemas encontrados e correções

- Corrigida a expressão Razor inválida do formulário e substituídas URLs montadas à mão por Tag Helpers.
- Criada navegação interna para todas as áreas; dashboard, lista, formulário e relatórios agora a exibem.
- Recursos desconhecidos retornam resposta controlada. Auditoria é somente leitura; relatórios e auditoria mantêm políticas próprias; exclusão exige justificativa.
- Falhas funcionais e inesperadas são registradas com `correlationId`, sem expor detalhes internos ao usuário.
- Consultas Dapper usam apenas tabelas da whitelist e filtram `tenant_id`, `entidade_id`, `ativo=true` e `is_deleted=false`.
- Inclusões preenchem os campos relacionais obrigatórios das tabelas a partir do objeto JSON validado; JSON vazio é normalizado para `{}` e validado antes do cast. Coordenadas, datas, pontuação, justificativas e vínculo de entrega recebem validação adicional.
- Escritas, exclusões, exportação e respectivas auditorias são transacionais. A auditoria de escrita não replica o JSON complementar; CSV neutraliza fórmulas.
- Corrigida a soma SQL dos cards de visitas e disponibilidade, que era removida em runtime e produzia SQL inválido.

## Rotas revisadas

Foram revisadas as rotas `/Habitacao`, `/Habitacao/Dashboard`, `/Habitacao/Familias`, `/Habitacao/Membros`, `/Habitacao/Domicilios`, `/Habitacao/Programas`, `/Habitacao/Inscricoes`, `/Habitacao/Classificacao`, `/Habitacao/Visitas`, `/Habitacao/Regularizacao`, `/Habitacao/Nucleos`, `/Habitacao/Lotes`, `/Habitacao/Unidades`, `/Habitacao/Beneficiarios`, `/Habitacao/Relatorios` e `/Habitacao/Auditoria`, além de `Novo`, `Salvar`, `Excluir` e CSV aplicáveis.

## Banco e scripts

A migration publicada `20260825100000` não foi alterada. Seu SHA-256 coincide com o `manifest.json` (`c38ae4...e53d`) e o bloco FUNC16 permanece presente nos consolidados `script_completo.sql`, `script_completo_dev.sql`, `database/script_completo.sql` e `script_completop.sql`. A execução real com `psql -v ON_ERROR_STOP=1` ficou **BLOCKED**, pois o ambiente não possui cliente/servidor PostgreSQL nem contrato `ConnectionStrings__DefaultConnection` configurado.

## Validação e bloqueios

- Manifest validado como JSON.
- Verificações estáticas de diff e scripts do repositório executadas conforme registrado no PR.
- `dotnet restore` e `dotnet build`: **BLOCKED**, pois o SDK .NET 10 não está instalado no ambiente (`dotnet: command not found`).
- Smoke HTTP autenticado, salvamento, exclusão, CSV e bloqueio RBAC: **BLOCKED** pela ausência do runtime e de PostgreSQL; não foi declarado PASS sem execução.

## Limites confirmados

InovaGED, GED e Protocolo não foram alterados. FUNC17 não foi iniciado. Não foram criados mocks, dados oficiais alternativos, secrets, projetos ou classes de teste.
