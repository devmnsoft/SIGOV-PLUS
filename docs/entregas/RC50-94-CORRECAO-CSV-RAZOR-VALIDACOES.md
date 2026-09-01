# RC50.94 — correção CSV Razor e validações

## Base

**BASE LOCAL utilizada porque `origin/main` não estava disponível.** O checkout não possui remote configurado; a implementação foi mantida na branch local vigente, sem `reset --hard`.

## Entrega

- Corrigidas as expressões Razor que faziam o compilador interpretar `csv` como membro de `string` nas onze views informadas e na ocorrência adicional encontrada em Relatórios Agro.
- Links MVC agora usam `Url.Action` com os nomes reais das actions e parâmetros de rota; a API de RH usa interpolação integral e segura.
- Filtros correntes de Energia, Royalties e LicitaPro são propagados ao download.
- As páginas revisadas possuem o bloco recolhível **Como usar esta tela**, com finalidade, filtros, permissão, tratamento de dados sensíveis e resultado da exportação.
- Os POSTs existentes no workspace LicitaPro preservam antiforgery, resumo e mensagens por campo; seleções usam opções carregadas pelo controller, sem entrada manual de ID.
- As actions verificadas possuem autorização específica (ou avaliação persistida), isolamento pelo contexto do módulo, filename `.csv` e `text/csv`; os repositórios de exportação existentes mantêm a neutralização contra CSV injection.

## Validação e bloqueios

A varredura estática abrangeu sufixos `.csv` ambíguos, actions/rotas, formulários, antiforgery, entradas de ID, SQL concatenado, `SELECT *`, `catch` vazio e `throw ex`. Achados fora das views alteradas não foram convertidos em catálogo ou fallback.

`BLOCKED: comando dotnet build sigov.sln --no-restore não executado porque o executável dotnet não está instalado no ambiente.`

`BLOCKED: smoke autenticado das rotas de relatório/exportação CSV não executado porque o runtime .NET 10 e uma instância autenticada da aplicação não estão disponíveis no ambiente.`

`BLOCKED: push, abertura de PR, merge e pull final não executados porque não há remote Git configurado neste checkout.`
