# RC50.57 — homologação funcional por perfil

## Evidência implementada

- SuperAdmin: catálogo completo e bypass modular explícito.
- Demais perfis: Meu Acesso lista somente módulos concedidos; catálogo pode mostrar bloqueio e motivo.
- Segurança/Auditoria: itens sensíveis da sidebar são renderizados pelo serviço de permissão.
- URL direta de detalhe modular sem grant: 403 com auditoria; item inexistente: 404.
- Matriz: seleção de 11 perfis, ações liberadas/bloqueadas e motivo; CSV protegido e auditado.
- LGPD: cards de Saúde, Educação, Social, RH e Auditoria/LGPD exibem aviso de dados sensíveis.

## Roteiro de aceite pendente de ambiente

Para cada SUPERADMIN, ADMIN_TENANT, COORDENADOR_AREA, FINANCEIRO, AUDITOR e ATENDIMENTO: autenticar; capturar menu; abrir Catálogo/Meu Acesso; tentar rota permitida e proibida; confirmar 403 e `auditoria_evento`; conferir isolamento por tenant; exportar matriz com/sem grant; verificar dashboard sem dados de área indevida. Não declarar aprovado com fallback, HTTP 000 ou ausência de banco.

Estado: homologação estática concluída; homologação autenticada/persistente pendente do runtime e PostgreSQL. Nenhuma classe de teste foi criada.
