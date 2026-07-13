# Diagnóstico de consolidação Pós-RC 11

Base analisada localmente em 2026-07-13 a partir do branch de trabalho. A classificação abaixo separa funcionalidade real, parcial, fallback honesto, pendência de runtime, dependência de provedor e indisponibilidade.

| Área | Funcionalidade | Status atual | Falha/Risco | Evolução necessária | Prioridade |
|---|---|---|---|---|---|
| Protocolo | Rotas, criação e anexos | Funcional real | Depende de seed/migration local | Validar fluxo no smoke com tenant e auditoria | Alta |
| GED | Documentos, anexos, OCR/assinatura controlada | Parcial | Provedores externos podem não estar configurados | Manter fallback honesto e bloquear restritos sem permissão | Alta |
| Enterprise | CRUD Dapper, ciclo de vida e ações | Funcional real | Algumas telas usam endpoint genérico e schema precisa estar aplicado | Consolidar formulário, importação, lote, anexos e evidências | Alta |
| Dashboard | Cards SaaS e módulos | Parcial | Alguns indicadores são fallback/demo fora do runtime PostgreSQL | Identificar fonte real/fallback/demo e linkar listagens | Alta |
| Minha Central | Entrada operacional | Parcial | Depende de dados reais por perfil e permissões | Completar atalhos e pendências por tenant | Alta |
| Busca Global | Busca transversal | Parcial | Cobertura Enterprise/GED depende de schemas | Expandir tipos e LGPD mascarado | Média |
| Relatórios | CSVs por áreas | Funcional real | Exportações precisam manter neutralização de fórmula | Padronizar nomes e auditoria por exportação | Alta |
| Notificações/Tarefas | Workflow existente | Parcial | Eventos Enterprise não cobrem todos os casos | Integrar eventos operacionais prioritários | Alta |
| Auditoria | Trilhas e auditoria operacional | Funcional real | Visualização Enterprise precisa timeline detalhada | Expor timeline nos detalhes e manter IP mascarado | Alta |
| Outbox | Worker e eventos | Funcional real | Webhooks falham se provedor ausente | Separar falha de provedor de erro de negócio | Média |
| API Key | Autenticação API v1 | Funcional real | Chaves reais não podem aparecer em logs/evidências | Usar mascaramento no smoke | Alta |
| Seeds | Demo/homologação | Parcial | Deve ser idempotente e proibido em produção sem flag | Rodar duas vezes no pipeline | Alta |
| Smoke | Rotas e API | Parcial | Ambiente local pode não estar no ar | Registrar falhas bloqueantes e limitações | Alta |
| CI/CD | Workflows esperados | Pendente de runtime | Sem consulta remota neste ambiente | Garantir comandos locais e checklist | Média |
| Docker | Compose build/up | Pendente de runtime | Pode depender de daemon local | Registrar limitação se daemon indisponível | Alta |
| Menus/Layout | Sidebar e Razor | Parcial | Links placeholder históricos podem existir | Remover botões mortos nas telas Enterprise tocadas | Alta |
| Permissões | API bloqueia sem claim | Funcional real | Front precisa ocultar ações conforme claims quando disponíveis | Não expor botão sem rota real | Alta |
| Importação CSV | Prévia segura Enterprise | Parcial | Confirmação real depende de endpoint por área | Validar colunas, rejeitar inválidos e auditar | Média |
| Ações em lote | Inativar/restaurar por tela | Parcial | Resultado parcial precisa transparência | Exibir contagem OK/falha e não simular sucesso | Média |
| Anexos Enterprise/GED | Vínculo enterprise_anexo | Parcial | Download depende de permissão GED | Aplicar tabela idempotente e auditoria de acesso | Alta |

## Pendências honestas

- A validação completa E2E requer aplicação das migrations e serviços Docker ativos.
- Integrações com provedores externos permanecem dependentes de configuração segura.
- A prévia CSV no front não confirma persistência; confirmação deve chamar endpoint real em ambiente homologado.
