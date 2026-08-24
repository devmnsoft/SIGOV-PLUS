# FUNC07 — Saneamento Comercial e Operacional

## Escopo entregue

O FUNC07 consolida os fluxos persistidos de cadastro comercial, ligações, hidrômetros, rotas e leituras, faturamento, arrecadação, inadimplência, parcelamento, atendimento e ordens de serviço já iniciados pela RC50.50. A migration corretiva é exclusivamente aditiva e mantém compatibilidade com os contratos publicados.

As telas MVC em `/Saneamento` consomem os endpoints autenticados `/api/saneamento/*`; ausência de tenant, autorização, schema ou configuração resulta em erro explícito. Não existe catálogo ou fallback demonstrativo. O campo de linha digitável permanece nulo: não há geração de código de barras sem convênio e regra bancária reais.

## Rotas funcionais

- `/Saneamento`, `/Saneamento/Clientes`, `/Saneamento/UnidadesConsumidoras`, `/Saneamento/Ligacoes` e `/Saneamento/Hidrometros`;
- `/Saneamento/Leituras`, `/Saneamento/Faturamento` e `/Saneamento/Arrecadacao`;
- `/Saneamento/OrdensServico`, `/Saneamento/Atendimento` e `/Saneamento/Relatorios`.

## Regras essenciais

- isolamento por `tenant_id` e `entidade_id`, PK `bigint identity` e Dapper/Npgsql;
- matrícula única, competências mensais e sequência única por rota;
- no máximo um hidrômetro `INSTALADO` por ligação, assegurado por índice parcial;
- valores, quantidades, vigências e estados protegidos por constraints;
- trilha cadastral com correlação, usuário e snapshots JSON;
- RBAC persistido: permissões não são concedidas implicitamente; somente `SUPERADMIN` sistêmico recebe o conjunto inicial;
- dados pessoais são limitados à finalidade de prestação do serviço e não são incluídos em logs ou seeds.

## Operação

Aplique o manifest com `psql` e `ON_ERROR_STOP=1`. Parâmetros tarifários e de serviços devem ser cadastrados pela entidade antes do faturamento. Não foram inseridos clientes, imóveis, leituras, tarifas ou pagamentos fictícios.
