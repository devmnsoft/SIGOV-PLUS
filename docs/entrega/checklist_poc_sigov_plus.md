# Checklist POC SIGOV+

## Banco
- [ ] PostgreSQL acessível
- [ ] schema `sigov` criado
- [ ] `script_completo_dev.sql` aplicado
- [ ] `schema_migrations` sem falha
- [ ] seeds admin/superadmin OK

## Build
- [ ] restore OK
- [ ] Domain / Shared / Application / Infrastructure OK
- [ ] Api / Web / Worker OK

## Runtime
- [ ] API e Swagger sobem
- [ ] Web sobe
- [ ] Login admin e superadmin funcionam
- [ ] MinhaCentral abre

## Módulos
- [ ] Tributário / Educação / Saúde / Saneamento
- [ ] Processos / GED / Legislativo / Transparência
- [ ] Empresarial
- [ ] Governança / Segurança / LGPD / Auditoria

## Demonstração e segurança
- [ ] menus sem 404; dashboards sem 500
- [ ] CSVs principais; ProjectStatus e Observabilidade sem erro
- [ ] permissões básicas e exportação auditada
- [ ] dados pessoais mascarados; logs sem senha
- [ ] connection string protegida
