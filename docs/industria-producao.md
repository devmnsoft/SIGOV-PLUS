# SIGOV Indústria e Produção

O módulo `industria_producao` atende fábricas, oficinas técnicas, prestadores industriais e produção sob ordem. Todos os cadastros e movimentos operacionais são isolados por `tenant_id`, exigem módulo contratado e gravam auditoria nas alterações.

## Fluxo industrial

1. Cadastrar centros de trabalho e recursos produtivos.
2. Configurar produtos industriais e vínculo opcional com produto comercial/estoque.
3. Criar ficha técnica (BOM) e roteiro de produção.
4. Abrir ordem de produção (OP), liberar, iniciar e apontar execução.
5. Consumir materiais, registrar produção acabada, refugo e inspeções.
6. Concluir a OP somente com apontamento e sem bloqueio de qualidade obrigatório.
7. Calcular custos por material, horas/recurso e refugo.

## Segurança e SaaS

O acesso exige `industria_producao` em `tenant_modulo_contratado` e permissões `industria.*`. Perfis sugeridos: GERENTE_INDUSTRIAL, PCP, OPERADOR_PRODUCAO, QUALIDADE e MANUTENCAO.
