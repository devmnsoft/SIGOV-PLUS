# Agenda e SLA Pós-RC 12


## Escopo consolidado

- Minha Central, Dashboard, Busca, Relatórios, Agenda e auditoria como rotas principais.
- Enterprise com CRUD real por tenant, importação CSV com prévia/confirmção, ações em lote e anexos GED com fallback honesto.
- Segurança LGPD: mascaramento de documento/e-mail/telefone, CSV anti-fórmula, ausência de secrets e bloqueio 403 quando permissão faltar.
- Evidências devem distinguir validação real de limitação ambiental. Não declarar homologação sem runtime.

## Pendências honestas

- OCR/assinatura/download de GED dependem de provedor/storage configurado.
- Docker, banco e CI precisam de runtime disponível para confirmação completa.
- SLA amplo depende de schemas setoriais quando campos ainda não existirem.
