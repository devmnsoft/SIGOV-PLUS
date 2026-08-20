# RC50.67 — checklist de não regressão global

Data: 2026-08-20. Marcação: `[x]` apenas para inspeção comprovada; `[ ]` exige runtime autenticado.

Critérios por módulo: catálogo; SuperAdmin; permissão; menu; dashboard; listagem; vazio seguro; seed; botões; bloqueio API; exportação auditada; máscara; sem 501; sem 500; sem 404; integrações.

| Módulo | preservado no inventário | catálogo/permissão estáticos | 16 critérios runtime |
|---|---:|---:|---|
| Governança | [x] | [x] | [ ] pendente de smoke autenticado |
| Segurança | [x] | [x] | [ ] pendente de smoke autenticado |
| LGPD | [x] | [x] | [ ] pendente de smoke autenticado |
| Auditoria | [x] | [x] | [ ] pendente de smoke autenticado |
| Observabilidade | [x] | [x] | [ ] pendente de smoke autenticado |
| Minha Central | [x] | [x] | [ ] pendente de smoke autenticado |
| Pendências | [x] | [x] | [ ] pendente de smoke autenticado |
| Alertas | [x] | [x] | [ ] pendente de smoke autenticado |
| Qualidade de Dados | [x] | [x] | [ ] pendente de smoke autenticado |
| Integrações Internas | [x] | [x] | [ ] pendente de smoke autenticado |
| Status Funcional | [x] | [x] | [ ] pendente de smoke autenticado |
| Tributário | [x] | [x] | [ ] pendente de smoke autenticado |
| Financeiro | [x] | [x] | [ ] pendente de smoke autenticado |
| Saneamento | [x] | [x] | [ ] pendente de smoke autenticado |
| Educação | [x] | [x] | [ ] pendente de smoke autenticado |
| Saúde | [x] | [x] | [ ] pendente de smoke autenticado |
| Processos Digitais | [x] | [x] | [ ] pendente de smoke autenticado |
| GED | [x] | [x] | [ ] pendente de smoke autenticado |
| Assinaturas | [x] | [x] | [ ] pendente de smoke autenticado |
| Legislativo | [x] | [x] | [ ] pendente de smoke autenticado |
| Diário Oficial | [x] | [x] | [ ] pendente de smoke autenticado |
| Transparência | [x] | [x] | [ ] pendente de smoke autenticado |
| Ouvidoria | [x] | [x] | [ ] pendente de smoke autenticado |
| e-SIC | [x] | [x] | [ ] pendente de smoke autenticado |
| RH | [x] | [x] | [ ] pendente de smoke autenticado |
| Folha | [x] | [x] | [ ] pendente de smoke autenticado |
| Compras | [x] | [x] | [ ] pendente de smoke autenticado |
| Licitações | [x] | [x] | [ ] pendente de smoke autenticado |
| Contratos | [x] | [x] | [ ] pendente de smoke autenticado |
| Almoxarifado | [x] | [x] | [ ] pendente de smoke autenticado |
| Patrimônio | [x] | [x] | [ ] pendente de smoke autenticado |
| Frotas | [x] | [x] | [ ] pendente de smoke autenticado |
| Obras | [x] | [x] | [ ] pendente de smoke autenticado |
| Assistência Social | [x] | [x] | [ ] pendente de smoke autenticado |
| Empresarial/SaaS | [x] | [x] | [ ] pendente de smoke autenticado |
| Agro | [x] | [x] | [ ] pendente de smoke autenticado |
| Georreferenciamento | [x] | [x] | [ ] pendente de smoke autenticado |

## Agro/Geo

- [x] catálogo, menu, mapa, camadas e manifesto de probes preservados.
- [ ] produtor, propriedade, talhão, cultura, safra, produção, programas, benefícios e insumos em runtime.
- [ ] máquinas, implementos, agenda e serviços em runtime.
- [ ] criação/listagem de camada e feição, coordenadas inválidas rejeitadas.
- [ ] exportação GeoJSON válida, sem PII e auditada.
- [ ] estado vazio, pendências, alertas, qualidade, integrações e status funcional.

## Resultado honesto

Nenhum módulo foi removido ou ocultado. A inspeção estática não comprova dashboards/menus/API; todos permanecem abertos até o artifact runtime. Não foi criada classe, pasta ou projeto de teste.
