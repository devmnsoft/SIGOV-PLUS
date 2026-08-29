# Fechamento geral do SIGOV PLUS — RC50.80

## Objetivo e critério de promoção

A RC50.80 consolida o ciclo funcional existente sem criar um novo grande
módulo. A promoção exige, cumulativamente, build .NET 10, validação em
PostgreSQL 16+, smoke HTTP e o gate estático `scripts/validate-rc50-80.py`.
Ausência de ferramenta, banco, segredo ou integração é **BLOCKED**, nunca PASS.

## Escopo revisado

- Administração/Segurança e os módulos Educação360, Saúde360/ACS360,
  Saneamento360/SIGCOS, Cidadão360, Jurídico360, Obras360, DefesaCivil360,
  Ativos360, SST360, Carbono360, Energia360 e Royalties360;
- BI360, integrações, transparência, Financeiro, Protocolo/Ouvidoria e
  Fiscaliza360;
- GED somente por referência persistida e integração real configurada.

## Contratos preservados

- ASP.NET Core MVC/Razor, Dapper/Npgsql e PostgreSQL continuam sendo a pilha;
- tenant, entidade, exercício e unidade permanecem contextuais e fail-closed;
- permissões, perfis, parâmetros e catálogos têm o banco como autoridade;
- POSTs Razor exigem antiforgery; validação no servidor não pode depender de
  JavaScript; relacionamentos devem usar seleção contextual, não ID digitado;
- CSV deve neutralizar fórmulas, aplicar o mesmo escopo/filtro da tela, mascarar
  dados protegidos e auditar exportações autorizadas;
- CPF, CNS, telefone, e-mail e endereço não podem aparecer integralmente em
  listagens públicas, logs ou payloads sem finalidade e permissão específicas.

## Banco e scripts

O manifest é a fonte da ordem e dos checksums. Os quatro baselines de produção
devem ser byte a byte equivalentes e autônomos; os checksums das migrations são
verificados diretamente contra o manifest. Migrations publicadas são imutáveis; correções
são sempre aditivas e idempotentes.

No fechamento, dois checksums de metadados que divergiam do conteúdo publicado
foram reconciliados no manifest e nos scripts distribuíveis, sem modificar as
migrations. Os quatro baselines de produção também foram ressincronizados.

## Correções transversais

Foram protegidos com token antiforgery os POSTs Razor remanescentes de
Almoxarifado, Atendimento, Compras/LicitaPro, Editais, Fiscalização, Frotas,
Habitação, Meio Ambiente e Tributário. O gate percorre todas as views para
impedir a reintrodução dessa falha.

## Validação executável

```bash
python3 scripts/validate-rc50-80.py
dotnet build
psql -X "$ConnectionStrings__DefaultConnection" -v ON_ERROR_STOP=1 \
  -f database/postgres/script_completo_dev.sql
bash scripts/smoke-production-like.sh
```

O gate local valida JSON, unicidade de versão/arquivo, SHA-256 normalizado,
compatibilidades, sincronização dos scripts completos e proteção antiforgery.
Build valida C#/Razor e o smoke valida rotas; portanto um não substitui o outro.

## Limites e pendências reais deste checkout

- **BLOCKED:** `dotnet build` não executado porque o SDK `dotnet` não está
  instalado no ambiente.
- **BLOCKED:** aplicação por `psql` não executada porque `psql` não está
  disponível no ambiente.
- **BLOCKED:** smoke MVC/Razor não executado porque runtime e banco não estão
  disponíveis.
- **BASE LOCAL:** implementação feita sobre branch `work` porque `origin/main`
  não existe no checkout.

Não houve conflito Git. Integrações ministeriais, provedores externos e GED
continuam condicionados a contrato real, configuração e homologação.
