# Auditoria de experiência — Pós-RC 27B.5

## Escopo e método

Auditoria estática do shell, login, Minha Central, Dashboard, navegação, páginas operacionais, estados vazios e tema. As capturas executáveis permanecem pendentes porque o ambiente local não contém o runtime .NET nem navegador Playwright; nenhum screenshot foi fabricado.

## Achados do estado inicial

- A barra superior apresentava organização, entidade e perfil fixos, que não representavam a sessão autenticada.
- A navegação era organizada pela estrutura interna do produto, continha prefixos alfabéticos e módulos duplicados.
- Termos técnicos e atalhos de diagnóstico apareciam no fluxo operacional.
- O CSS especializado era carregado pelo layout e novamente importado por `site.css`.
- O botão de menu utilizava um caractere funcional sem nome visual consistente.

## Resultado desta revisão

O shell passa a priorizar Minha Central, trabalho, processos, gestão, administração e ajuda; a identidade do usuário deriva da autenticação; busca, notificações, ajuda e tema possuem nomes acessíveis; breadcrumbs são exibidos no conteúdo. A validação visual nas quatro resoluções exigidas deve ser executada em CI antes de homologação.
