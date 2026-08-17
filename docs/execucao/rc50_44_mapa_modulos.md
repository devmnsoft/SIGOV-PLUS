# RC50.44 — Mapa operacional de módulos

> Inventário estático em 17/08/2026. “Fechado” exige validação ponta a ponta; portanto o mapa usa estados conservadores.

## Núcleo/Plataforma

### Tenants
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/014_saas_tenants_planos_assinaturas.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** `src/Sigov.Api/Controllers/SaasTenantsController.cs`.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Parceiros/Tenants.cshtml`, `src/Sigov.Web/Views/Saas/Tenants.cshtml`, `src/Sigov.Web/Views/MonitoramentoB2B/Tenants.cshtml`.
- **Rotas principais:** `/api/...` e `/Tenants` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Usuários
- **Status:** Não iniciado.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Definir domínio, persistência, API e experiência.
- **Riscos:** Alto: escopo ainda sem evidência executável.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Perfis
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260608090000_saas_parametrizacao_perfis_modulos.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Seguranca/Perfis.cshtml`, `src/Sigov.Web/Views/SaasConfiguracao/Perfis.cshtml`.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Permissões
- **Status:** Não iniciado.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Definir domínio, persistência, API e experiência.
- **Riscos:** Alto: escopo ainda sem evidência executável.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### LGPD
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/004_create_sigov_security_audit_lgpd.sql`.
- **Services existentes:** `src/Sigov.Application/Abstractions/ILgpdMaskingService.cs`, `src/Sigov.Application/Lgpd/ILgpdClassificationService.cs`, `src/Sigov.Infrastructure/Lgpd/LgpdClassificationService.cs`.
- **Repositories existentes:** —.
- **Controllers API:** `src/Sigov.Api/Controllers/LgpdController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/LgpdController.cs`.
- **Views:** `src/Sigov.Web/Views/Shared/_OperationalLgpdAlert.cshtml`, `src/Sigov.Web/Views/Shared/Sectors/_SectorLgpdAlert.cshtml`, `src/Sigov.Web/Views/Shared/Assets/_AssetLgpdAlert.cshtml`.
- **Rotas principais:** `/api/...` e `/LGPD` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Auditoria
- **Status:** Parcial.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** `src/Sigov.Api/Controllers/AuditoriaController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/AuditoriaController.cs`.
- **Views:** `src/Sigov.Web/Views/Integracoes/_AuditoriaIntegracoes.cshtml`, `src/Sigov.Web/Views/Agro/_AuditoriaAgro.cshtml`, `src/Sigov.Web/Views/Financeiro/_AuditoriaFinanceira.cshtml`.
- **Rotas principais:** `/api/...` e `/Auditoria` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Parâmetros
- **Status:** Não iniciado.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Definir domínio, persistência, API e experiência.
- **Riscos:** Alto: escopo ainda sem evidência executável.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Notificações
- **Status:** Não iniciado.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Definir domínio, persistência, API e experiência.
- **Riscos:** Alto: escopo ainda sem evidência executável.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Governança
- **Status:** Não iniciado.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Definir domínio, persistência, API e experiência.
- **Riscos:** Alto: escopo ainda sem evidência executável.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Qualidade de Dados
- **Status:** Parcial.
- **Migrations existentes:** —.
- **Services existentes:** `src/Sigov.Application/Agro/Dicionario/AgroDicionarioDadosService.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/Agro/Repositories/AgroDicionarioDadosRepository.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/AgroDadosAbertosController.cs`.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Agro/DicionarioDados.cshtml`, `src/Sigov.Web/Views/Auditoria/AcessosDadosPessoais.cshtml`, `src/Sigov.Web/Views/Industria/Qualidade.cshtml`.
- **Rotas principais:** `/api/...` e `/QualidadedeDados` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

## Gestão Pública

### Educação
- **Status:** Não iniciado.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Definir domínio, persistência, API e experiência.
- **Riscos:** Alto: escopo ainda sem evidência executável.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### RH
- **Status:** Não iniciado.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Definir domínio, persistência, API e experiência.
- **Riscos:** Alto: escopo ainda sem evidência executável.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Folha
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260813120000_rc50_24_educacao_rh_folha_produto_core.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Rh/FolhaDetalhe.cshtml`, `src/Sigov.Web/Views/Rh/FolhaCriar.cshtml`, `src/Sigov.Web/Views/Rh/_AuditoriaFolha.cshtml`.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Ponto
- **Status:** Parcial.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Agro/_GridPontosCriticos.cshtml`, `src/Sigov.Web/Views/Agro/PontosCriticos.cshtml`, `src/Sigov.Web/Views/Agro/_FormPontoCritico.cshtml`.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Férias/Afastamentos
- **Status:** Parcial.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Rh/FeriasAfastamentosRelatorios.cshtml`, `src/Sigov.Web/Views/Rh/Afastamentos.cshtml`, `src/Sigov.Web/Views/Rh/PortalAfastamentos.cshtml`.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Portal do Servidor
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/20260813232000_rc50_34_educacao_portal_responsavel_bloco3_core.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** `src/Sigov.Web/Controllers/PortalController.cs`, `src/Sigov.Web/Controllers/PortalContribuinteController.cs`, `src/Sigov.Web/Controllers/PortalCidadaoController.cs`.
- **Views:** `src/Sigov.Web/Views/Rh/_PortalResumoServidor.cshtml`, `src/Sigov.Web/Views/Rh/PortalAtualizacaoCadastral.cshtml`, `src/Sigov.Web/Views/Rh/PortalContracheques.cshtml`.
- **Rotas principais:** `/api/...` e `/PortaldoServidor` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Secretaria Escolar
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260813230000_rc50_34_educacao_secretaria_bloco3_core.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Educacao/DocumentosEscolares.cshtml`, `src/Sigov.Web/Views/Educacao/Secretaria.cshtml`, `src/Sigov.Web/Views/Educacao/HistoricoEscolar.cshtml`.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Diário de Classe
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260813231000_rc50_34_educacao_diario_classe_bloco3_core.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Educacao/_DiarioClasseResumo.cshtml`, `src/Sigov.Web/Views/Educacao/DiarioClasse.cshtml`, `src/Sigov.Web/Views/Educacao/DiarioClasseDetalhe.cshtml`.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Portal Aluno/Responsável
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/20260813232000_rc50_34_educacao_portal_responsavel_bloco3_core.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** `src/Sigov.Web/Controllers/PortalController.cs`, `src/Sigov.Web/Controllers/PortalContribuinteController.cs`, `src/Sigov.Web/Controllers/PortalCidadaoController.cs`.
- **Views:** `src/Sigov.Web/Views/Rh/_PortalResumoServidor.cshtml`, `src/Sigov.Web/Views/Rh/PortalAtualizacaoCadastral.cshtml`, `src/Sigov.Web/Views/Rh/PortalContracheques.cshtml`.
- **Rotas principais:** `/api/...` e `/PortalAlunoResponsável` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Financeiro/SIAFIC
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/20260730180000_pos_rc_30_financeiro_empresarial_real.sql`, `database/postgres/migrations/20260610120000_pos_build_05_comercio_varejo_atacado_pdv_caixa_financeiro.sql`, `database/postgres/migrations/20260814120000_rc50_36_financeiro_siafic_bloco5_core.sql`.
- **Services existentes:** `src/Sigov.Application/Financeiro/FinanceiroServices.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/FinanceiroEmpresarial/FinanceiroEmpresarialRepository.cs`, `src/Sigov.Infrastructure/Financeiro/FinanceiroRepositories.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/FinanceiroComercialController.cs`, `src/Sigov.Api/Controllers/FinanceiroControllers.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/FinanceiroController.cs`, `src/Sigov.Web/Controllers/SiaficController.cs`.
- **Views:** `src/Sigov.Web/Views/Financeiro/FinanceiroEmpresarial.cshtml`, `src/Sigov.Web/Views/Rh/_ModalIntegrarFinanceiro.cshtml`.
- **Rotas principais:** `/api/...` e `/FinanceiroSIAFIC` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Tributário/Dívida Ativa
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260814121000_rc50_36_tributario_divida_ativa_bloco5_core.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Tributario/DividaAtiva.cshtml`, `src/Sigov.Web/Views/Rh/PontoJustificativas.cshtml`.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Compras/Licitações
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/20260815120000_rc50_37_compras_licitacoes_bloco6_core.sql`, `database/postgres/migrations/007_create_sigov_compras_rh_educacao.sql`, `database/postgres/migrations/20260802210000_pos_rc_37b_compras_empresariais_fullstack.sql`.
- **Services existentes:** `src/Sigov.Application/ComprasEmpresariais/ComprasApplicationServices.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/AgroComprasAgriculturaFamiliarController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/ComprasEmpresariaisController.cs`, `src/Sigov.Web/Controllers/ComprasController.cs`.
- **Views:** `src/Sigov.Web/Views/Agro/_GridComprasAgriculturaFamiliar.cshtml`, `src/Sigov.Web/Views/Agro/ComprasAgriculturaFamiliar.cshtml`, `src/Sigov.Web/Views/ComprasEmpresariais/Shared/_ComprasNav.cshtml`.
- **Rotas principais:** `/api/...` e `/ComprasLicitações` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Contratos
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/20260815121000_rc50_37_contratos_bloco6_core.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** `src/Sigov.Web/Controllers/ContratosController.cs`, `src/Sigov.Web/Controllers/ContratosB2BController.cs`.
- **Views:** `src/Sigov.Web/Views/Ged/Contratos.cshtml`.
- **Rotas principais:** `/api/...` e `/Contratos` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Almoxarifado
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260815122000_rc50_37_almoxarifado_patrimonio_bloco6_core.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** `src/Sigov.Web/Controllers/AlmoxarifadoController.cs`.
- **Views:** —.
- **Rotas principais:** `/api/...` e `/Almoxarifado` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Patrimônio
- **Status:** Não iniciado.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Definir domínio, persistência, API e experiência.
- **Riscos:** Alto: escopo ainda sem evidência executável.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Saúde
- **Status:** Não iniciado.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Definir domínio, persistência, API e experiência.
- **Riscos:** Alto: escopo ainda sem evidência executável.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Assistência Social
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/008_create_sigov_saude_social_saneamento.sql`, `database/postgres/migrations/024_assistencia_social_base.sql`, `database/postgres/migrations/20260816121000_rc50_38_assistencia_social_bloco7_core.sql`.
- **Services existentes:** `src/Sigov.Application/Social/SocialServices.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/Social/SocialRepository.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/SocialControllers.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/SocialController.cs`.
- **Views:** `src/Sigov.Web/Views/Rh/_GridEsocialEventos.cshtml`, `src/Sigov.Web/Views/Rh/Esocial.cshtml`, `src/Sigov.Web/Views/Social/_AuditoriaSocial.cshtml`.
- **Rotas principais:** `/api/...` e `/AssistênciaSocial` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Saneamento
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/023_saneamento_base.sql`, `database/postgres/migrations/20260816122000_rc50_38_saneamento_bloco7_core.sql`, `database/postgres/migrations/008_create_sigov_saude_social_saneamento.sql`.
- **Services existentes:** `src/Sigov.Application/Saneamento/SaneamentoServices.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/Saneamento/SaneamentoRepository.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/SaneamentoControllers.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/SaneamentoController.cs`.
- **Views:** `src/Sigov.Web/Views/Saneamento/_AuditoriaSaneamento.cshtml`.
- **Rotas principais:** `/api/...` e `/Saneamento` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Frotas
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260816123000_rc50_38_frotas_obras_bloco7_core.sql`.
- **Services existentes:** `src/Sigov.Application/Frotas/FrotasObrasServices.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/Frotas/FrotasObrasRepository.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/FrotasController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/FrotasController.cs`.
- **Views:** —.
- **Rotas principais:** `/api/...` e `/Frotas` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Obras
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260816123000_rc50_38_frotas_obras_bloco7_core.sql`.
- **Services existentes:** `src/Sigov.Application/Frotas/FrotasObrasServices.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/Frotas/FrotasObrasRepository.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/ObrasController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/ObrasController.cs`.
- **Views:** —.
- **Rotas principais:** `/api/...` e `/Obras` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

## Digital/Governança Pública

### Processos Digitais
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/20260817120000_rc50_42_processos_digitais_core.sql`, `database/postgres/migrations/017_processos_digitais_protocolo_ged.sql`.
- **Services existentes:** `src/Sigov.Application/Processos/ProcessosServices.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/Processos/ProcessosRepositories.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/ProcessosControllerBase.cs`, `src/Sigov.Api/Controllers/ProcessosDigitaisController.cs`, `src/Sigov.Api/Controllers/ProcessosDigitaisBloco8Controller.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/AssinaturasDigitaisController.cs`, `src/Sigov.Web/Controllers/ProcessosDigitaisController.cs`, `src/Sigov.Web/Controllers/ProcessosController.cs`.
- **Views:** `src/Sigov.Web/Views/Lgpd/ProcessosTratamento.cshtml`, `src/Sigov.Web/Views/Processos/_GridProcessos.cshtml`.
- **Rotas principais:** `/api/...` e `/ProcessosDigitais` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Protocolo
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260706153000_pos_rc_protocolo_ged_workflow_api_outbox.sql`, `database/postgres/migrations/017_processos_digitais_protocolo_ged.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** `src/Sigov.Api/Controllers/ProtocolosController.cs`, `src/Sigov.Api/Controllers/ProtocoloExternoController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/ProtocolosController.cs`, `src/Sigov.Web/Controllers/ProtocoloExternoController.cs`, `src/Sigov.Web/Controllers/ProtocoloController.cs`.
- **Views:** —.
- **Rotas principais:** `/api/...` e `/Protocolo` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### GED
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260706153000_pos_rc_protocolo_ged_workflow_api_outbox.sql`, `database/postgres/migrations/20260610220000_pos_build_09_ged_ocr_assinatura_automacao.sql`, `database/postgres/migrations/20260817121000_rc50_42_ged_assinaturas_core.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** `src/Sigov.Api/Controllers/GedController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/GedController.cs`.
- **Views:** —.
- **Rotas principais:** `/api/...` e `/GED` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Assinaturas
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/014_saas_tenants_planos_assinaturas.sql`, `database/postgres/migrations/20260817121000_rc50_42_ged_assinaturas_core.sql`.
- **Services existentes:** `src/Sigov.Application/Saas/Comercial/SaasAssinaturaService.cs`, `src/Sigov.Application/Saas/Comercial/ISaasAssinaturaService.cs`.
- **Repositories existentes:** —.
- **Controllers API:** `src/Sigov.Api/Controllers/AssinaturasController.cs`, `src/Sigov.Api/Controllers/SaasAssinaturasController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/AssinaturasDigitaisController.cs`, `src/Sigov.Web/Controllers/AssinaturasController.cs`.
- **Views:** `src/Sigov.Web/Views/SaasComercial/Assinaturas.cshtml`, `src/Sigov.Web/Views/Saas/Assinaturas.cshtml`, `src/Sigov.Web/Views/SaasAdmin/Assinaturas.cshtml`.
- **Rotas principais:** `/api/...` e `/Assinaturas` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Legislativo/Câmara
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260817122000_rc50_42_legislativo_camara_core.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** `src/Sigov.Api/Controllers/LegislativoController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/LegislativoController.cs`.
- **Views:** —.
- **Rotas principais:** `/api/...` e `/LegislativoCâmara` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Transparência
- **Status:** Não iniciado.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Definir domínio, persistência, API e experiência.
- **Riscos:** Alto: escopo ainda sem evidência executável.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Diário Oficial
- **Status:** Parcial.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** `src/Sigov.Api/Controllers/DiarioOficialController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/DiarioOficialController.cs`.
- **Views:** —.
- **Rotas principais:** `/api/...` e `/DiárioOficial` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### e-SIC
- **Status:** Parcial.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Social/_FormComposicaoFamiliar.cshtml`.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Ouvidoria
- **Status:** Parcial.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** `src/Sigov.Api/Controllers/OuvidoriaController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/OuvidoriaController.cs`.
- **Views:** —.
- **Rotas principais:** `/api/...` e `/Ouvidoria` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Atendimento Digital
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/20260817123000_rc50_42_transparencia_atendimento_core.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** `src/Sigov.Api/Controllers/AtendimentoPublicoController.cs`, `src/Sigov.Api/Controllers/AtendimentoDigitalController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/AtendimentoDigitalController.cs`, `src/Sigov.Web/Controllers/AtendimentoController.cs`.
- **Views:** `src/Sigov.Web/Views/Saude/_FormAtendimento.cshtml`, `src/Sigov.Web/Views/Saude/_GridAtendimentos.cshtml`, `src/Sigov.Web/Views/Saude/Atendimentos.cshtml`.
- **Rotas principais:** `/api/...` e `/AtendimentoDigital` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

## Empresarial SaaS

### Comercial/CRM
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/20260608120000_saas_comercial_white_label_planos.sql`, `database/postgres/migrations/20260730120000_pos_rc_29_comercial_operacional.sql`, `database/postgres/migrations/20260730210000_pos_rc_31_consolidacao_comercial_financeiro.sql`.
- **Services existentes:** `src/Sigov.Application/Agro/Comercial/AgroPainelComercialService.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/Agro/Repositories/AgroPainelComercialRepository.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/AgroPainelComercialController.cs`, `src/Sigov.Api/Controllers/SaasTenantComercialController.cs`, `src/Sigov.Api/Controllers/IndustriaComercialController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/ComercialController.cs`, `src/Sigov.Web/Controllers/AgroPainelComercialController.cs`, `src/Sigov.Web/Controllers/SaasComercialController.cs`.
- **Views:** `src/Sigov.Web/Views/Agro/PainelComercial.cshtml`, `src/Sigov.Web/Views/Agro/_PainelComercialAgro.cshtml`.
- **Rotas principais:** `/api/...` e `/ComercialCRM` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Ordem de Serviço
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260730090000_pos_rc_32_ordem_servico.sql`.
- **Services existentes:** `src/Sigov.Application/OrdemServico/OrdemServicoApplicationService.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/OrdemServico/OrdemServicoRepository.cs`.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Saneamento/_ModalExecutarOrdemServico.cshtml`, `src/Sigov.Web/Views/Saneamento/_FormOrdemServico.cshtml`, `src/Sigov.Web/Views/Saneamento/OrdemServicoDetalhe.cshtml`.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Estoque Empresarial
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260730180000_pos_rc_30_financeiro_empresarial_real.sql`, `database/postgres/migrations/20260730170000_financeiro_empresarial_legacy_columns_compat.sql`.
- **Services existentes:** `src/Sigov.Application/Comercio/IComercioEstoqueService.cs`, `src/Sigov.Infrastructure/Comercio/ComercioEstoqueService.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/FinanceiroEmpresarial/FinanceiroEmpresarialRepository.cs`.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** `src/Sigov.Web/Views/Financeiro/FinanceiroEmpresarial.cshtml`.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Compras Empresariais
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/20260815120000_rc50_37_compras_licitacoes_bloco6_core.sql`, `database/postgres/migrations/007_create_sigov_compras_rh_educacao.sql`, `database/postgres/migrations/20260802210000_pos_rc_37b_compras_empresariais_fullstack.sql`.
- **Services existentes:** `src/Sigov.Application/ComprasEmpresariais/ComprasApplicationServices.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/AgroComprasAgriculturaFamiliarController.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/ComprasEmpresariaisController.cs`, `src/Sigov.Web/Controllers/ComprasController.cs`.
- **Views:** `src/Sigov.Web/Views/Agro/_GridComprasAgriculturaFamiliar.cshtml`, `src/Sigov.Web/Views/Agro/ComprasAgriculturaFamiliar.cshtml`, `src/Sigov.Web/Views/ComprasEmpresariais/Shared/_ComprasNav.cshtml`.
- **Rotas principais:** `/api/...` e `/ComprasEmpresariais` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Manutenção Industrial
- **Status:** Não iniciado.
- **Migrations existentes:** —.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Definir domínio, persistência, API e experiência.
- **Riscos:** Alto: escopo ainda sem evidência executável.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Produção Base
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/023_saneamento_base.sql`, `database/postgres/migrations/022_saude_acs_base.sql`, `database/postgres/migrations/024_assistencia_social_base.sql`.
- **Services existentes:** —.
- **Repositories existentes:** `src/Sigov.Infrastructure/Persistence/Repositories/BaseRepository.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/ProcessosControllerBase.cs`.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** `/api/...` e `/ProduçãoBase` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Varejo
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260610120000_pos_build_05_comercio_varejo_atacado_pdv_caixa_financeiro.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Atacado
- **Status:** Parcial.
- **Migrations existentes:** `database/postgres/migrations/20260610120000_pos_build_05_comercio_varejo_atacado_pdv_caixa_financeiro.sql`.
- **Services existentes:** —.
- **Repositories existentes:** —.
- **Controllers API:** —.
- **Controllers Web:** —.
- **Views:** —.
- **Rotas principais:** Planejada; sem rota canônica confirmada.
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

### Financeiro Empresarial Preparatório
- **Status:** Núcleo implementado.
- **Migrations existentes:** `database/postgres/migrations/20260730180000_pos_rc_30_financeiro_empresarial_real.sql`, `database/postgres/migrations/20260610120000_pos_build_05_comercio_varejo_atacado_pdv_caixa_financeiro.sql`, `database/postgres/migrations/20260814120000_rc50_36_financeiro_siafic_bloco5_core.sql`.
- **Services existentes:** `src/Sigov.Application/Financeiro/FinanceiroServices.cs`.
- **Repositories existentes:** `src/Sigov.Infrastructure/FinanceiroEmpresarial/FinanceiroEmpresarialRepository.cs`, `src/Sigov.Infrastructure/Financeiro/FinanceiroRepositories.cs`.
- **Controllers API:** `src/Sigov.Api/Controllers/FinanceiroComercialController.cs`, `src/Sigov.Api/Controllers/FinanceiroControllers.cs`.
- **Controllers Web:** `src/Sigov.Web/Controllers/FinanceiroController.cs`.
- **Views:** `src/Sigov.Web/Views/Financeiro/FinanceiroEmpresarial.cshtml`, `src/Sigov.Web/Views/Rh/_ModalIntegrarFinanceiro.cshtml`.
- **Rotas principais:** `/api/...` e `/FinanceiroEmpresarialPreparatório` (confirmar ações no mapa de rotas).
- **Pendências:** Fechar fluxo, relatório/exportação e aceite operacional.
- **Riscos:** Médio: cobertura heterogênea e validação runtime pendente.
- **Próximo avanço recomendado:** estabilizar a rota principal, persistir um fluxo real, registrar auditoria/LGPD e fechar uma tela responsiva.

