# Etapa 2: Módulo Pessoa e Endereço

## Entrega

- CRUD REST `/api/pessoas` com listagem paginada, detalhe, inclusão, alteração, exclusão lógica e exportação CSV/JSON/XML.
- Sub-recurso `/api/pessoas/{id}/enderecos` para inclusão, atualização e exclusão lógica de endereços vinculados.
- Tela CSHTML `/Pessoas` com Bootstrap 5, jQuery e Ajax para consulta, cadastro, edição e detalhe.
- Repositório Dapper isolado por `tenant_id`, com logs estruturados e auditoria de inclusão, alteração, exclusão, consultas e exportações.
- Regras de domínio para normalização e validação de CPF/CNPJ conforme tipo de pessoa.
- Migração `019_core_pessoas_enderecos_operacional.sql` com índices e permissões granulares.

## Segurança e auditoria

Toda operação exige tenant resolvido por cabeçalho/domínio e registra trilha em `sigov.trilha_auditoria`. Consultas e detalhes são tratados como acesso a dado pessoal para apoiar relatórios LGPD.

## Próxima etapa

Etapa concluída: Pessoa e Endereço – Próxima etapa: Contratos e Obras.
