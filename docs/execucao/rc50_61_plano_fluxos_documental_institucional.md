# RC50.61 — plano dos fluxos documental e institucional

Data do inventário: 2026-08-19. O inventário é estático; homologação requer banco, build e jornadas autenticadas.

## Superfícies existentes

| Eixo | API controllers | Web controllers | Serviços/repositórios e tabelas principais | Estado observado |
|---|---|---|---|---|
| Processos/Protocolo | `ProcessosDigitaisController`, `ProcessosDigitaisBloco8Controller`, `ProtocolosController`, `ProtocoloExternoController` | `ProcessosController`, `ProcessosDigitaisController`, `ProtocolosController`, `ProtocoloController` | `ProcessosServices`, `ProcessosRepositories`; `processo`, `protocolo`, movimentação/anexo | CRUD e ações persistentes; cancelamento justificado já endurecido |
| GED | `GedController`, `DocumentosApiController` | `GedController` | Dapper no controller/API e serviço operacional Web; `ged_documento`, `ged_anexo`, `ged_indice`, `ged_historico`, `fluxo_tramitacao` | dashboard, lista, criação, anexo, índice, tramitação e histórico reais; storage/ICP externo preparatório |
| Assinaturas | `AssinaturasController`, `AssinaturasApiController` | `AssinaturasController`, `AssinaturasDigitaisController`, `ValidacaoDocumentoController` | Bloco 8; `assinatura_documento`, `ged_assinatura`, validação | hash e trilha existem; integração ICP-Brasil permanece preparatória |
| Legislativo | `LegislativoController` | `LegislativoController` | Bloco 8; proposição, sessão, pauta, votação e ata | núcleo persistente; provar jornada completa e publicação |
| Diário Oficial | `DiarioOficialController` | `DiarioOficialController` | serviço de Processos; publicação e ato oficial | criar/alterar/publicar/incluir ato existentes |
| Transparência | `TransparenciaController` | `TransparenciaController` | serviço operacional; publicações/validação pública | superfícies públicas e internas existem; revisar segregação em runtime |
| Ouvidoria/e-SIC | `OuvidoriaController`, `ProtocolosController`, `ProtocoloExternoController` | `OuvidoriaController`, `ProtocolosController` | serviços de Processos; ouvidoria/protocolo/processo | criação, resposta, conversão em processo e arquivo existem; prazo/recurso exigem prova |

## Endpoints, UI e integrações

Os endpoints principais são os dashboards/listagens, detalhe, POST de criação e ações especializadas (`tramitar`, `responder`, `publicar`, `converter-em-processo`, anexar e validar). A busca por `NotImplemented`, `NotImplementedException` e HTTP 501 deve permanecer parte do gate; não foi identificado 501 essencial no inventário anterior. As views específicas e a view operacional compartilhada oferecem menus e formulários, mas botões, `fetch` e rotas precisam de smoke autenticado antes de serem declarados homologados.

Integrações observadas: protocolo→processo, GED→processo/contrato, assinatura→GED e Legislativo→publicação. Integrações oficiais de assinatura avançada, storage e publicação externa são preparatórias. Não devem ser apresentadas como ICP-Brasil ou entrega externa homologada.

## Regras presentes e lacunas priorizadas

Já existem filtro por tenant nas consultas canônicas, auditoria de mutações, cancelamento de processo justificado e persistência Dapper. Nesta entrega, o GED passa a exigir título, tipo e origem; sigilo exige justificativa; tramitação exige destino e despacho; projeção explícita substitui `d.*`; permissões granulares novas coexistem com as chaves legadas durante a transição.

Ainda requer homologação: escopo por unidade/setor em cada perfil, mascaramento autenticado de interessado, imutabilidade concorrente pós-assinatura, temporalidade/descarte, recurso e-SIC, retificação pós-publicação, exportações com finalidade e todos os fluxos ponta a ponta.

## Perfis e permissões

Perfis necessários: SuperAdmin, Admin Tenant, Gestor Documental, Atendimento/Protocolo, Servidor Setorial, Coordenador, Assinador, Publicador, Gestor Legislativo, Ouvidoria, e-SIC, Auditor e Cidadão. A migration RC50.61 cria os templates funcionais ausentes e o catálogo granular solicitado para `processos`, `ged`, `assinatura`, `legislativo`, `diario`, `transparencia` e `atendimento`; concessões continuam tenant-scoped e módulos contratados continuam sendo pré-condição.
