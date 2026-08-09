# Checklist mobile e acessibilidade — RC48

## Gate de homologação

- [x] Login, alteração e redefinição de senha adaptam-se a 320 px sem rolagem horizontal.
- [x] Campos possuem `label`, autocomplete apropriado e erros associados pelos Tag Helpers.
- [x] Checklist de senha usa região `aria-live` e não depende somente de cor no texto.
- [x] Botões usam texto explícito; nenhuma ação principal usa emoji como ícone.
- [x] Antiforgery está presente em todas as alterações de senha.
- [x] Foco de teclado permanece visível pelos estilos canônicos do Design System.
- [ ] Validar leitor de tela NVDA/VoiceOver em ambiente de homologação com navegador real.
- [ ] Executar a matriz completa de sidebar, topbar, drawers, Kanban e relatórios em dispositivos reais.

As pendências acima são gates manuais, não evidências simuladas. Devem ser anexadas à homologação antes da promoção para produção.
