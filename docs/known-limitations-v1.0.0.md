# Limitações conhecidas - sigov v1.0.0

- Testes E2E autenticados por todos os perfis devem ser ampliados após homologação assistida.
- Alguns módulos estruturais/parciais exigem evolução funcional em backlog específico.
- Rollback de migrations é manual e depende de avaliação da janela de implantação.
- Publicação de imagens em registry depende de secrets configurados fora do repositório.

## Pós-RC 17 — validação técnica

A trilha Pós-RC 17 centraliza as correções de build, DI Enterprise, migrations/seed PostgreSQL, Docker/Docker Compose, smoke estático/E2E, empacotamento de release e go-live. A evidência operacional deve vir dos comandos do CI e dos artifacts gerados, não de declaração manual.
