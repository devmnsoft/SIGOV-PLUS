# RC50.49 — Saúde Avançada

Implementação incremental de ACS offline/campo, vacinação, farmácia, regulação e retaguarda. Quatro migrations idempotentes criam 48 estruturas multi-tenant com soft delete, auditoria JSON, CorrelationId e índices seguros. A camada Dapper persiste operações reais com lista branca de tabelas, parâmetros SQL, filtros por tenant, validações de campo e exportação CSV.

A API cobre dispositivos/lotes/sincronização, domicílios/indivíduos/visitas/ocorrências, calendário vacinal, estoque/dispensação, fila regulatória, indicadores, lotes e-SUS preparatórios e suporte/SLA. As telas usam painel responsivo institucional com estado vazio real, badge LGPD, filtros, KPIs e navegação dedicada.

Integrações e-SUS/SISAB, mapa e GED permanecem explicitamente preparatórias; não existe afirmação de transmissão oficial. Dados clínicos, documentos e localização devem continuar restritos pela autorização do produto. Pendente para RC50.50: cliente cartográfico, storage protegido de evidências, transmissão externa homologada e testes automatizados (não criados nesta sprint por requisito).

Validações: manifest JSON e conflitos estáticos de rotas aprovados; verificação de índices aprovou as quatro migrations RC50.49. PostgreSQL, build, Swagger e login ficaram pendentes porque `psql` e `dotnet` não estão instalados no contêiner de execução.
