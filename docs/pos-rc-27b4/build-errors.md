# Erros de build

## CS0433

Causa confirmada: os assemblies `Sigov.Api` e `Sigov.Web` exportavam `Program`, e o projeto compartilhado de testing referenciava ambos. Arquivos atingidos: factories em `tests/Sigov.Testing`, 11 classes em `tests/Sigov.ApiTests` e `WebRuntimeSmokeTests`.

Correção aplicada: marcadores exclusivos, factories tipadas e migração dos fixtures. Validação local bloqueada porque `dotnet` não está instalado.
