# RC50.58 — checklist funcional por módulo

Use uma cópia deste bloco para cada um dos 17 grupos abaixo. Marque somente com evidência HTTP, SQL/auditoria e perfil; “não se aplica” requer motivo registrado.

## Checklist padrão

- [ ] Módulo aparece no catálogo e no menu autorizado.
- [ ] Perfil sem acesso não vê o item e recebe 403 por URL direta, com negativa auditada.
- [ ] Dashboard e listagem abrem; vazio possui mensagem segura.
- [ ] Cadastro grava e o detalhe recupera o mesmo registro no tenant.
- [ ] Edição altera registro real e respeita estado/concorrência.
- [ ] Status segue transições válidas; cancelamento/reabertura/estorno exige justificativa.
- [ ] Aprovação exige permissão segregada.
- [ ] Exportação exige permissão, mascara PII e registra módulo/recurso/filtros.
- [ ] Acesso sensível e ação crítica geram trilha sem senha, token ou documento completo.
- [ ] Integração funciona transacionalmente ou está identificada como preparatória.
- [ ] Nenhum botão principal morto, endpoint essencial 501, menu 404 ou dashboard 500.

## Grupos a executar

1. Tributário
2. Financeiro/SIAFIC
3. Educação
4. Saúde
5. Saneamento
6. Processos Digitais
7. GED
8. Assinaturas
9. Legislativo/Diário Oficial/Transparência
10. Ouvidoria/e-SIC
11. RH/Folha
12. Compras/Licitações/Contratos
13. Almoxarifado/Patrimônio
14. Frotas/Obras
15. Assistência Social
16. Empresarial SaaS
17. Segurança/LGPD/Auditoria/Observabilidade

## Perfis a repetir

SUPERADMIN, ADMIN_TENANT, GESTOR_MUNICIPAL, COORDENADOR_AREA, OPERACIONAL, FINANCEIRO, AUDITOR, ATENDIMENTO, GESTOR_MODULO, LEITURA e CIDADAO (quando a superfície externa existir).
