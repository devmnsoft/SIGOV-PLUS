# Matriz de erros

| Erro | Causa | Correção |
| --- | --- | --- |
| CS1503 FluentAssertions | Overloads com `StringComparison` indisponíveis | Usar `string.Contains(..., StringComparison)` e assert booleano |
| Parser PowerShell | Cercas Markdown com crases em strings duplas | Usar strings literais e array explícito |
| PGSSLMODE vazio | Variável de ambiente exportada como string vazia | Remover/unset quando SSL mode ausente |
| Baseline não versionado | Cabeçalho hardcoded | Ler `eng/version.json` e remover data/hora |
