# Auditoria de rotas

A action acidental `OnActionExecutionAsync` foi removida do controller. O contexto Enterprise passou a ser aplicado por `ServiceFilter`. A validação integral dos itens de navegação permanece condicionada à suíte host Web no CI.
