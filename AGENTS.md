# Regras do repositório SIGOV PLUS

1. O runtime oficial é .NET 10 (`global.json`) e a linguagem é C# 14.
2. A persistência oficial é PostgreSQL 16+ com Dapper; não introduza EF Core.
3. Preserve as camadas existentes: Domain, Application, Infrastructure, Api e Web.
4. Novas entidades persistidas usam PK `bigint generated ... as identity`; não crie PK UUID.
5. UUID legado permanece compatível e não deve ser convertido destrutivamente.
6. Toda alteração de schema exige migration PostgreSQL idempotente.
7. Migration, `manifest.json`, `script_completo.sql`, `script_completo_dev.sql`,
   `database/script_completo.sql` e `script_completop.sql` devem ficar sincronizados.
8. Não altere migration publicada; acrescente uma migration corretiva.
9. Seeds de desenvolvimento/homologação usam somente dados fictícios e são idempotentes.
10. Seeds nunca contêm senha, token, chave ou dado pessoal real.
11. Autorização, perfis, permissões e parâmetros têm o banco como fonte de autoridade.
12. Não crie catálogos mock, demo, fallback ou coleção hardcoded como autoridade.
13. Falha ou ausência de schema/configuração deve ser explícita; não simule sucesso.
14. Não adicione novas classes de teste; execute e preserve os testes existentes.
15. Use o contrato `ConnectionStrings__DefaultConnection`; segredos vêm do ambiente.
16. `.env` é local, ignorado e nunca versionado; documente somente `.env.example`.
17. Workflows devem instalar exatamente o SDK definido em `global.json`.
18. Gates não podem conter senhas literais e devem registrar BLOCKED quando faltar ferramenta.
19. YAML, JSON, shell e PowerShell alterados devem passar validação sintática disponível.
20. Mudanças P0/P1, avaliador de autorização, troca de contexto e dashboard SuperAdmin
    exigem RC própria e estão fora da RC50.68A.
