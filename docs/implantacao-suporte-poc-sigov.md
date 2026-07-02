# Implantação, migração, treinamento, suporte, SLA, POC e aceite — SIGOV PLUS

Esta sprint cria a camada contratual do SIGOV PLUS em MVC/Razor, Dapper e schema-safe. O ciclo cobre implantação guiada, migração validada, parametrização, treinamentos, portal de suporte, SLA, prova de conceito, evidências, aceite formal e operação contratual integrada aos módulos SaaS.

## Fluxo operacional

1. **Implantação:** projetos por tenant, etapas, evidências e termo de aceite. Se `sigov.implantacao*` existir, a tela consulta dados reais; caso contrário informa implantação em andamento.
2. **Migração:** lotes, logs e validações. Importação real só deve ocorrer quando schema de destino existir e após confirmação.
3. **Treinamentos:** turmas, participantes e certificados visuais. Certificados não são simulados quando não há persistência.
4. **Suporte/SLA:** chamados, interações, satisfação, regras e eventos de SLA. O portal informa fallback quando `sigov.suporte_chamado` não existe.
5. **POC:** roteiros, requisitos binários Atende/Não Atende, execuções e evidências.
6. **Aceite formal:** consolida implantação, migração, treinamento, suporte e POC com status Pendente, Aceito, Recusado, Em revisão ou Cancelado.

## Integrações

As áreas registram auditoria via `IAuditTrailService`, tentam evento operacional/outbox para notificações e agenda, exibem aviso LGPD e usam modal de confirmação nas ações críticas. Dashboard, Minha Central, Notificações, Agenda e Relatórios passam a ter pontos documentados para consolidação da operação contratual.
