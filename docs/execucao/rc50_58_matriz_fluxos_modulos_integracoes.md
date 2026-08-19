# RC50.58 — matriz de fluxos, módulos e integrações

Legenda: **real** = há contrato, serviço e persistência; **parcial** = núcleo existe, faltando prova ponta a ponta; **preparatória** = não declarar integração externa produtiva.

| Módulo | Entrada → saída principal | Superfícies encontradas | Integração | Estado conservador |
|---|---|---|---|---|
| Tributário | contribuinte → lançamento → DAM → dívida/fiscalização | API/Web, services e repositórios Tributário/Avançado | Financeiro/outbox | parcial |
| Financeiro/SIAFIC | obrigação → liquidação → pagamento/estorno | Financeiro e FinanceiroEmpresarial | Tributário, Compras, Saneamento | parcial |
| Educação | escola/aluno → matrícula/turma → frequência/apoio | Educação, Bloco3 e Avançada | transporte, merenda, biblioteca | parcial |
| Saúde | unidade/paciente → campo → vacina/farmácia/regulação | Saúde, ACS, Avançada e retaguarda | e-SUS/SISAB preparatória | parcial |
| Saneamento | consumidor/ligação → leitura/fatura → OS/qualidade | Saneamento Comercial/Faturamento/Operação | Financeiro | parcial |
| Processos Digitais | protocolo → processo → tramitação/encerramento | API Processos e núcleo Bloco8 | Protocolo/GED | real, sem prova runtime |
| GED | documento → classificação/versão | GED, Storage, Processos | assinatura/processo | parcial |
| Assinaturas | documento → hash/código/validação | Assinaturas e ValidaçãoDocumento | GED | parcial |
| Legislativo/Diário/Transparência | ato → aprovação/publicação | DiárioOficial, Legislativo, Transparência | Processos/GED | parcial |
| Ouvidoria/e-SIC | manifestação → resposta/processo | Ouvidoria, Atendimento, Protocolo | Processos | real, sem prova runtime |
| RH/Folha | servidor/vínculo → evento → fechamento | RH e serviços Folha | Financeiro preparatória | parcial |
| Compras/Contratos | requisição → compra → contrato/medição | Compras, Licitações, Contratos | Financeiro | parcial |
| Almoxarifado/Patrimônio | recebimento → saldo/tombamento/baixa | Almoxarifado, Inventário, Patrimônio | Compras | parcial |
| Frotas/Obras | veículo/obra → diário/medição/status | Frotas e Obras | contratos/financeiro preparatória | parcial |
| Assistência Social | pessoa/família → atendimento/benefício | Social | Cadastro/Pessoas | parcial |
| Empresarial SaaS | tenant → comercial/serviço/faturamento | Saas/Enterprise/Empresarial | Financeiro empresarial | parcial |
| Segurança/LGPD/Auditoria | acesso/titular/incidente → trilha | Segurança, Matriz, LGPD, Auditoria | transversal | real, sem prova runtime |

Nenhum padrão executável de HTTP 501 foi encontrado. A rota legada `/ProcessosDigitais/*` ainda usa a visão genérica Bloco8; a API canônica `api/processos` oferece CRUD e ações reais. Menus, botões e dashboards só podem ser dados como homologados após smoke autenticado.
