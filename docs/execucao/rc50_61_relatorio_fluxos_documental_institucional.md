# RC50.61 — relatório dos fluxos documental e institucional

Data: 2026-08-19. Decisão: **não apto para produção até banco, build, runtime e jornadas por perfil ficarem verdes**.

1. **Processos/Protocolo:** controllers API/Web, serviços, repositórios e tabelas foram inventariados; o cancelamento justificado da RC50.58 foi preservado.
2. **GED:** dashboard, listagem, criação, anexos, indexação, tramitação e histórico persistentes foram inventariados.
3. **Assinaturas:** superfícies canônica, Bloco 8 e validação foram encontradas; ICP-Brasil continua preparatória.
4. **Legislativo:** proposições, sessões, pautas, votações, atas e relatórios têm superfícies existentes.
5. **Diário Oficial:** API oferece listar, obter, criar, atualizar, publicar e administrar atos.
6. **Transparência:** API/Web e consulta institucional foram inventariadas; runtime deve provar separação público/interno/sigiloso.
7. **Ouvidoria:** criação, resposta, conversão em processo e arquivamento existem na API.
8. **e-SIC:** usa o núcleo de protocolo/atendimento; prazo, prorrogação e recurso permanecem pendentes de prova funcional completa.
9. **Endpoints corrigidos:** GED aceita as permissões granulares de dashboard, consulta, criação, versão/anexo e tramitação, mantendo compatibilidade com grants legados.
10. **Services corrigidos:** nenhuma abstração paralela foi criada; os serviços existentes foram preservados.
11. **Repositories corrigidos:** a listagem GED deixou de projetar `d.*` e mantém parâmetros/filtro `tenant_id`.
12. **Views corrigidas:** nenhuma mudança visual foi necessária; nenhuma função foi ocultada para mascarar pendência.
13. **Menus corrigidos:** o catálogo persistido recebeu as permissões granulares usadas para composição por perfil.
14. **Negócio:** criação GED exige título, tipo e origem; documento sigiloso exige justificativa.
15. **Permissões:** 77 chaves granulares dos sete grupos foram adicionadas de modo idempotente.
16. **Atendimento/Protocolo:** template funcional criado; concessão real permanece limitada ao tenant.
17. **Servidor Setorial:** template criado para tramitação, despacho e anexação autorizados.
18. **Coordenador:** perfil existente continua dependente de grants explícitos para encerrar/reabrir.
19. **Assinador:** template criado; assinatura/rejeição permanecem segregadas.
20. **Publicador:** template criado; publicação continua separada de alteração de matéria.
21. **Ouvidoria/e-SIC:** templates separados criados para impedir concessão implícita entre operações.
22. **Auditor:** preservado como leitura/trilha, sem alteração operacional implícita.
23. **Processos/GED:** anexação e tramitação usam persistência existente; tramitação agora exige unidade ou usuário de destino e despacho.
24. **Assinaturas/GED:** vínculo/hash existente preservado; provedor avançado continua explicitamente preparatório.
25. **Legislativo/Diário/Transparência:** superfícies persistentes existentes foram preservadas; prova E2E permanece necessária.
26. **Ouvidoria/e-SIC/Protocolo:** conversão de ouvidoria em processo existe; e-SIC completo permanece pendência real.
27. **Auditoria:** trilhas GED existentes foram preservadas sem conteúdo integral de arquivo.
28. **LGPD:** justificativa de sigilo virou regra de entrada; validação de ocultação por perfil ainda requer runtime.
29. **501:** busca estática obrigatória registra o estado; nenhum 501 novo foi introduzido.
30. **Botões:** nenhum botão foi removido; smoke autenticado continua necessário.
31. **Dashboards:** permissões granulares foram catalogadas; ausência de 500 só pode ser afirmada após runtime.
32. **Banco:** registrar o resultado real do apply no fechamento; falha ambiental não é aprovação.
33. **Build:** executar Release com warnings como erros; falha ou ferramenta ausente bloqueia.
34. **Gate:** executar o smoke production-like e registrar bloqueios ambientais explicitamente.
35. **RC50.62:** concluir e-SIC/recurso, temporalidade/descarte, retificação, exportações auditadas e jornadas autenticadas por unidade/tenant.

Nenhuma classe, fixture, mock ou projeto de teste foi criado.
