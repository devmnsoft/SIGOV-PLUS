# RC50.66 — checklist obrigatório de não regressão

Data: 2026-08-19. Aplicar cada linha a: Governança, Segurança, LGPD, Auditoria, Observabilidade, Minha Central, Tributário, Financeiro, Saneamento, Educação, Saúde, Processos, GED, Assinaturas, Legislativo, Diário, Transparência, Ouvidoria/e-SIC, RH/Folha, Compras/Licitações/Contratos, Almoxarifado/Patrimônio, Frotas/Obras, Social, Empresarial e Agro/Geo. Itens runtime ficam desmarcados até evidência real.

- [x] Continua no catálogo/estrutura quando contratado ou habilitado; nenhum módulo foi removido nesta RC.
- [x] Código preserva bypass existente de SuperAdmin e grants dos demais perfis.
- [x] Nenhuma view, controller, serviço ou migration foi removido/comentado.
- [x] Geo permanece dentro do módulo Agro e ganhou links Web para Mapa e Camadas.
- [x] Exportação GeoJSON exige permissão e agora registra auditoria sem PII.
- [x] Repositories Agro/Geo revisados usam Dapper, parâmetros, projeções explícitas e `tenant_id`.
- [ ] Catálogo/menu por todos os perfis confirmado em jornada autenticada.
- [ ] Todos os menus respondem sem 404 no runtime.
- [ ] Todos os dashboards respondem sem 500 com banco limpo e parcial.
- [ ] Botões principais confirmados por jornada; inspeção estática não substitui clique real.
- [ ] Exportações legadas de todos os módulos confirmadas com grant, máscara e auditoria.
- [ ] PII histórica confirmada mascarada por perfil e escopo.
- [ ] Integrações existentes confirmadas ponta a ponta e sem duplicação.
- [ ] Pendências, alertas e qualidade Agro confirmados com dados persistidos.
- [ ] Banco, build Release e production gate verdes.

## Matriz mínima por módulo

Para cada família: catálogo habilitado; SuperAdmin; perfil permitido; perfil negado/403; dashboard/lista; vazio seguro; criar/editar/transicionar/cancelar; exportar; trilha; isolamento de tenant; integração. Anexar HTTP, SQL sanitizado e correlation id. Nenhum item desmarcado autoriza ocultar menu, remover botão ou declarar homologação.
