# Triagem dos testes

O run informado contabiliza 533 execuções e 54 falhas. Os artefatos não existem no checkout e não há acesso ao GitHub pelo ambiente; por isso, a lista de FQNs permanece pendente de importação do TRX. Alterações realizadas nesta etapa atacam duas causas objetivas: ambiente `Testing` explícito e resolução de paths independente do diretório corrente.

| Contrato anterior | Problema | Contrato atual | Evidência |
|---|---|---|---|
| Host dependente da configuração padrão | Pode tentar banco/migration | `appsettings.Testing.json` desliga migrations e demo seed | Configurações API/Web |
| Paths `../../../../` | Dependem do runner | Busca ascendente por `sigov.sln` | `RepositoryPathResolverTests` |
