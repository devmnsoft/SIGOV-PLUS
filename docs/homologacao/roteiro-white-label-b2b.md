# Roteiro de homologação — white label B2B

1. Abrir `/WhiteLabelB2B` autenticado no tenant.
2. Alterar nome, cores, domínio, subdomínio, textos e remetente.
3. Salvar e confirmar toast.
4. Publicar configuração.
5. Consultar `GET /api/white-label/configuracao` e `GET /api/white-label/mobile/config`.
6. Restaurar padrão e validar histórico/eventos.

## Pendências reais
- Conectar middleware de branding global para refletir assets em todo layout após migração aplicada.
