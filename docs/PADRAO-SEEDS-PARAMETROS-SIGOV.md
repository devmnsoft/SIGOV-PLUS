# Padrão de seeds e parâmetros

Seeds devem ser idempotentes, fictícios, multi-esfera e ter o banco como autoridade. Use `ON CONFLICT` ou `NOT EXISTS`, nunca inclua senha em texto puro, token ou dado pessoal real. Credenciais locais devem armazenar somente hash no formato real da aplicação, marcar troca obrigatória e ser documentadas como não produtivas.

Falhas de schema são explícitas: não criar fallback nem simular sucesso. Parâmetros sensíveis vêm do ambiente e a conexão usa `ConnectionStrings__DefaultConnection`.
