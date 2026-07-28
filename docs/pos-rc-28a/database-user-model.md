# Modelo de usuários

A role PostgreSQL autentica a conexão ao servidor e recebe privilégios mínimos de banco. O usuário da aplicação é uma linha de `sigov.usuario` autenticada pelo algoritmo de hash da aplicação. As credenciais são independentes e nunca devem ser reutilizadas, registradas ou passadas como argumento de processo.
