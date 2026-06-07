# sigov — onboarding tenant

Este documento acompanha código real desta sprint e orienta evolução comercial e técnica do sigov.

## Padrões aplicados

- Identidade visual própria em SVG, sem brasão oficial ou imagem licenciada.
- Bootstrap 5, Razor MVC, jQuery e JavaScript comum nos arquivos `sigov.*`.
- Forms com antiforgery, validação amigável, loading e mapeamento de erros HTTP.
- Grids com filtros, paginação estrutural, empty state e saved filters.
- Catálogo comercial de módulos, onboarding, painel executivo, central de ajuda e demo mode controlado.
- Catálogo central de regras de negócio por módulo.
- Schema físico único `sigov` nas tabelas novas.

## Boas práticas para novas telas

1. Usar `_Layout`, `_Navbar`, `_Sidebar`, `_Footer`, `_Alerts`, `_ToastContainer` e `_ConfirmModal`.
2. Incluir breadcrumb e page header.
3. Usar forms padronizados com `data-sigov-form="true"` e `@Html.AntiForgeryToken()`.
4. Nunca exibir stack trace ao usuário final.
5. Mascarar dados pessoais por padrão e registrar acessos sensíveis conforme LGPD.
