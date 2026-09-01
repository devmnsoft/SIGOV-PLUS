# Padrão de formulários, mensagens e mini manuais

## Formulários

- Use Tag Helpers MVC, `@Html.AntiForgeryToken()`, `asp-validation-summary="ModelOnly"` e
  `asp-validation-for` em todos os campos editáveis.
- Identificadores técnicos pertencem à rota ou a campos ocultos gerados pelo servidor; vínculos
  devem ser escolhidos em listas carregadas do banco e limitadas ao contexto vigente.
- O `POST` inválido deve reconstruir todas as opções da tela e devolver o mesmo modelo, sem
  perder o que o usuário digitou.
- Campos obrigatórios devem ter indicação textual e visual. Valores monetários usam `decimal`;
  datas devem explicitar o fuso ou representar apenas uma data civil.
- Ações de escrita permanecem protegidas por autorização no servidor, validação antiforgery e
  isolamento por tenant, entidade, exercício e unidades administrativas aplicáveis.

## Mensagens

As chaves padronizadas de `TempData` são `Success`, `Error`, `Warning` e `Info`, renderizadas pelo
host compartilhado de notificações. A mensagem apresentada deve ser objetiva e não pode conter
stack trace, SQL, segredo ou dado pessoal. Detalhes técnicos vão apenas para log sanitizado.

- **Sucesso:** descreve o objeto e a operação concluída.
- **Erro:** explica que a operação não terminou e oferece uma próxima ação segura.
- **Atenção:** informa requisito ou validação pendente.
- **Informação:** comunica estado sem exigir correção.
- **Bloqueio:** distingue permissão insuficiente de recurso indisponível no plano SaaS.
- **Confirmação:** precede exclusão lógica, cancelamento, bloqueio, aprovação, rejeição e
  exportação de dado sensível; descreve consequência e permite desistir.

## “Como usar esta tela”

Páginas principais devem usar um `details`/accordion recolhível, navegável por teclado e com o
título exato **Como usar esta tela**. Em texto curto, o bloco informa finalidade, ações, filtros,
campos obrigatórios, permissões, presença de dados sensíveis e efeito da operação. O conteúdo
deve ser específico da página, responsivo e multi-esfera, sem tratar prefeitura como único tipo
de entidade pública.

## Checklist de revisão

1. GET e POST apontam para actions existentes e autorizadas.
2. POST possui antiforgery, resumo e mensagens por campo.
3. Listas são recarregadas no retorno inválido e vêm da fonte persistida.
4. Não existe entrada manual de ID técnico.
5. Tabela está em contêiner responsivo e o estado vazio orienta a próxima ação.
6. Ações perigosas pedem confirmação; erros técnicos não chegam ao navegador.
7. Mini manual cobre filtros, permissões, sensibilidade e resultado da ação.

