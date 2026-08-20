# RC50.66 — plano de evolução integrada Agro/Geo e todos os módulos

Data: 2026-08-19. Princípio: evolução aditiva; nenhum catálogo, controller, serviço, view, menu ou migration existente será removido ou ocultado. A autoridade continua no backend e toda afirmação de disponibilidade runtime depende do gate.

## Mapa de preservação

| Classificação | Módulos encontrados | Evidência/situação nesta RC |
|---|---|---|
| Estruturais/transversais | Governança, Segurança, LGPD, Auditoria, Observabilidade, Minha Central, Pendências, Alertas, Qualidade de Dados, Integrações Internas, Status Funcional, Catálogo e Matriz de Acesso | menu, controllers, views e núcleo Dapper; preservados |
| Produção funcional, pendente de nova prova runtime | Tributário, Financeiro/SIAFIC, Saneamento, Educação, Saúde, Processos, GED, Ouvidoria/e-SIC, RH/Folha, Compras/Contratos, Almoxarifado/Patrimônio | fluxos persistentes descritos nas RC50.57–63; nenhuma promoção adicional sem banco |
| Em construção | Assinaturas, Legislativo, Diário Oficial, Transparência, Licitações, Frotas, Obras, Assistência Social, Empresarial/SaaS/CRM/OS/Estoque/Industrial | superfícies e estruturas existentes permanecem acessíveis conforme grant; integrações parciais/preparatórias continuam declaradas |
| Eixo consolidado | Agro e Georreferenciamento | dashboard, produtores, propriedades, talhões, culturas, safras, produção, programas, benefícios, insumos, patrulha, mapa, camadas, feições e relatórios existentes; nesta RC, menu Geo explícito, autenticação Web e auditoria da exportação |

Todos os grupos acima possuem pelo menos uma evidência entre catálogo/menu/controller/view/migration/permissão. O inventário detalhado está no documento irmão.

## Cobertura transversal e diagnóstico

- **Menu/controller/view/migration/permissão/dashboard:** Agro possui as seis camadas; Geo é subdomínio contratado de Agro, com Web `/Agro/MapaRural` e `/Agro/CamadasGeo` e API `/api/agro/geo`.
- **Pendências/alertas/qualidade/integrações:** as tabelas transversais RC50.63 aceitam `modulo='agro'`; produtores automáticos por regra continuam P1 até execução persistente, sem dados simulados.
- **Rotas e botões:** a varredura estática será repetida; Mapa e Camadas passam a ter entrada direta. A API existente sustenta criar/listar camadas e feições.
- **501:** nenhuma implementação essencial encontrada na inspeção inicial. **404/500:** não podem ser encerrados sem runtime autenticado.
- **Preservação obrigatória:** todos os módulos enumerados na tabela; alterações desta RC ficam limitadas ao eixo Agro/Geo, catálogo e menu aditivos.

## Execução priorizada

1. Proteger as superfícies Web Agro autenticadas, mantendo o painel público separado.
2. Auditar exportação GeoJSON depois da autorização e sem incluir PII no evento.
3. Explicitar Mapa/Camadas no menu sem retirar entradas existentes.
4. Validar manifest, migrations, rotas, JS, C# 10, build, banco e gate.
5. Manter como bloqueio explícito qualquer etapa sem ferramenta/serviço; não converter ausência de evidência em aprovação.
