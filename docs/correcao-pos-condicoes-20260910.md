# Correção das pós-condições históricas — 2026-09-10

A migration `20260802210000` criou a família empresarial UUID sob nomes então genéricos. O bootstrap `070_separate_compras_uuid_contracts.sql` posteriormente preservou OIDs, dados, chaves e dependências ao renomeá-la para `compras_empresarial_*`, liberando os nomes `compras_*` para o núcleo governamental bigint. Portanto, a ausência de `compras_fatura` não é falha: o contrato suportado exige exclusivamente `compras_empresarial_fatura`, UUID, tenant UUID e FKs empresariais; coexistência bloqueia consolidação automática.

A RC50.60 introduziu templates globais em `perfil_acesso`, enquanto as atribuições efetivas (`perfil_permissao`, `grupo_perfil` e `usuario_grupo`) mantêm contexto `tenant_id`. A correção restaura de modo idempotente apenas a permissão canônica `saude.visita.registrar`, sem concedê-la, e torna anulável o tenant do template.

A pós-condição SaaS agora separa nove invariantes. Tabela, tipos, constraint e triggers são estruturais. O registro `industria_producao` deve existir, mas `nome` e `disponivel_contratacao` são configurações comerciais administráveis: renomear ou suspender o módulo legitimamente não reprova uma migration histórica nem é revertido pela correção.

A aplicação deve usar o runner com `ConnectionStrings__DefaultConnection` em uma cópia PostgreSQL 16 com backup recuperável confirmado. Execute primeiro em modo de validação; depois aplique as pendências e repita até o relatório final indicar `pendentes=0; checksum=0; falhas=0`. Nunca reaplique migrations históricas isoladamente.
