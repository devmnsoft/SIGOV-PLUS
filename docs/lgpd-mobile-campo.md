# SIGOV Mobile/PWA e Campo — Pós-Build 12

## Visão geral
A evolução Pós-Build 12 entrega a base para operação mobile em campo sem criar aplicativo nativo. O PWA é instalável, responsivo, usa Bootstrap mobile-first e possui manifesto, service worker, página offline e navegação inferior em celular.

## Instalação PWA
1. Acesse `/Mobile/Home` em navegador compatível.
2. Use a ação do navegador para instalar/adicionar à tela inicial.
3. O `manifest.json` define `start_url`, ícones base, tema e atalhos para Agenda, Sync e Offline.

## Dispositivo e sessão
- Registre o dispositivo em `POST /api/mobile/dispositivos/registrar`.
- Toda sincronização registra tenant, usuário, dispositivo, data/hora, status e `correlationId`.
- Dispositivo bloqueado/inativo não pode sincronizar.

## Offline-first e cache
- O service worker cacheia recursos estáticos e rotas mobile principais.
- Dados sensíveis offline devem ser mínimos e mascarados quando possível.
- Logout deve chamar limpeza de cache sensível via `sigovMobile.clearSensitiveCache()`.
- Ações críticas feitas offline entram como `SINCRONIZACAO_PENDENTE` ou pendentes de aprovação.

## Segurança e LGPD
- Toda coleta tem `tenant_id`.
- Evidências têm classificação LGPD e flag de mascaramento offline.
- Localização exige consentimento/regra operacional clara.
- Senhas não são armazenadas offline.

## Política de dados offline
Listagens mobile devem mascarar documento, paciente, família e outros dados sensíveis quando o cache offline estiver habilitado. O dispositivo pode ser bloqueado por `/api/mobile/dispositivos/{id}/status`.
