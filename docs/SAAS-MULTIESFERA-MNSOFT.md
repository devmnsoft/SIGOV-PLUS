# SaaS multi-esfera MNSOFT — RC50.87

## Escopo entregue

O núcleo persistente separa a Super Administração MNSOFT dos clientes e modela plano, contrato de módulos e funcionalidades, cobrança, bloqueio justificável e auditoria. `saas_cliente` mantém o contexto institucional, inclusive esfera, tipo, hierarquia, abrangência e unidades responsáveis. CNPJ é normalizado para 14 dígitos, único e validado também na aplicação antes da persistência.

A autoridade continua no PostgreSQL. Uma permissão concedida pelo perfil do cliente somente é efetiva quando o cliente está operacional, o plano contém o módulo e não existe bloqueio MNSOFT de cliente, módulo, funcionalidade ou usuário. A avaliação deve ser *fail-closed* diante de contexto ausente, schema indisponível ou regra ambígua.

## Operação

- Super Admin: administra clientes, planos, cobranças e bloqueios e consulta auditoria de todos os tenants.
- Administrador do cliente: atua somente no `tenant_id` e `entidade_id` autenticados; não cria Super Admin nem altera catálogos globais.
- Usuário: recebe ações por perfil (`visualizar`, `criar`, `editar`, `excluir`, `aprovar`, `cancelar`, `exportar`, `administrar`).
- Mudanças restritivas e liberações exigem justificativa e deixam histórico imutável.
- Cobrança é interna; a RC não declara banco, PIX, protesto ou execução como integrados sem adaptador real.

## Telas e orientação

As superfícies existentes de SaaS (`/SaasAdmin`) são a implementação operacional atualmente conectada aos serviços Dapper. Os nomes `/AdminMNSOFT` e `/Cliente` integram a evolução de navegação planejada, sem duplicar telas decorativas. Ao evoluir uma tela, use o componente “Como usar esta tela” com objetivo, ações, regras, filtros, permissão necessária e aviso LGPD.

## Limites

A migration entrega o contrato persistente e permissões da RC. A troca do avaliador central, o fluxo completo de recuperação de senha e integrações de faturamento dependem de RC própria e não são simulados.
