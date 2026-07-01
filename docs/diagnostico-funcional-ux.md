# Diagnóstico funcional e UX — Sprint SaaS comercial

## Antes

Planos eram majoritariamente catálogo visual; assinaturas, marketplace, notificações, busca e portal tinham cobertura parcial ou estática.

## Depois

- Planos usam dados reais quando `sigov.plano_saas` existe e deixam claro quando são demonstrativos.
- Assinaturas passam a ter rota operacional com fallback explícito.
- Marketplace organiza módulos para venda e gestão modular.
- Notificações exibem dados reais ou recomendações úteis derivadas.
- Busca global consulta áreas disponíveis com inspeção de schema.
- Portal do Cliente concentra assinatura, módulos, suporte e faturas com limitações honestas.

## Pendências UX

- Persistir permissões finas por ação quando a matriz definitiva de permissões estiver consolidada.
- Aplicar white label dinâmico por tenant em todas as telas após confirmação das colunas/metadados.
