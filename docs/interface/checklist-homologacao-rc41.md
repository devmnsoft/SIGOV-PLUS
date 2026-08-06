# Checklist de homologação — RC41

## Fluxos e rotas

- [ ] Abrir `/Protocolo`, filtrar e criar em `/Protocolo/Novo`.
- [ ] Validar campos obrigatórios; conferir número gerado e detalhe; tramitar com perfil autorizado.
- [ ] Abrir GED, enviar arquivo permitido de até 25 MB e conferir fallback quando o schema/worker não estiver configurado.
- [ ] Abrir worklists Minhas/Equipe, notificações e busca global.
- [ ] Acionar todos os sete itens de criação rápida; validar bloqueio de Novo usuário fora dos perfis administrativos.
- [ ] Validar usuários, perfis, permissões, implantação, Auditoria e LGPD.

## Perfis e dispositivos

- [ ] `ADMIN_GERAL`: administração e saúde do ambiente.
- [ ] `ADMIN_TENANT`: usuário do tenant e implantação.
- [ ] Operador: Protocolo/GED/Tarefas conforme claims de permissão.
- [ ] Sem permissão: ação escondida ou resposta amigável, e bloqueio no backend.
- [ ] Desktop (>= 1280 px), tablet (768 px) e mobile (360 px).
- [ ] Navegação por teclado, foco do modal e movimento reduzido.

## Limitações conhecidas

- OCR depende de worker externo e não é simulado.
- Upload e persistência operacional exigem as tabelas correspondentes no schema `sigov`.
- Homologação HTTP autenticada requer ambiente com tenant e usuário configurados.
