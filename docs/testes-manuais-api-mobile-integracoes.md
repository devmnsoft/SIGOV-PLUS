# Testes manuais — API, Mobile, Assinatura, BI e Integrações

1. Abrir `/swagger` e conferir tags da API v1.
2. Acessar `/Seguranca/ApiKeys` e validar fallback sem exibir token claro persistido.
3. Acessar `/Integracoes/Webhooks` e conferir eventos suportados.
4. Executar pull/push mobile com payload versionado.
5. Criar solicitação de assinatura eletrônica simples e verificar mensagem de não simulação ICP-Brasil.
6. Validar `/ValidarDocumento` com código inexistente e confirmar que não expõe dados pessoais.
7. Abrir `/Bi/Fluxos` e conferir fallback/indicadores.
8. Abrir `/Operacao/ApiLogs` e `/Operacao/Outbox`.
9. Conferir console do navegador sem erros JS próprios.
