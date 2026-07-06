# Matriz de aderência, editais e POC - SIGOV PLUS

## Objetivo

A matriz organiza editais, termos de referência, requisitos, módulos SIGOV PLUS, evidências, roteiro de POC e relatório técnico de atendimento. Ela evita respostas falsas: um requisito só deve ser tratado como **Atende** quando houver evidência verificável e auditável.

## Como cadastrar edital

Acesse `/Editais/Novo` e informe nome, órgão, município, UF, modalidade, número, ano, objeto, datas, status, observações, link de origem, valor estimado, responsável interno e tenant. Se a tabela `sigov.edital` não existir, a tela informa fallback e não simula salvamento.

## Como cadastrar requisitos

Acesse `/Editais/{editalId}/Requisitos/Novo`. Campos mínimos: código, item, descrição, módulo relacionado, categoria, criticidade, obrigatório, eliminatório, percentual, status de aderência, observação técnica e ordem. CSV simples pode ser recebido pela rota de importação, mas a persistência depende do schema real.

## Como vincular requisito a módulo

Na matriz (`/MatrizAderencia/Edital/{editalId}`), vincule o requisito a um módulo do catálogo real: SaaS/Admin, Segurança, RH, Protocolo, GED, Tributário, Portal, Transparência, Saúde, Educação, Saneamento, BI, API, Integrações e demais módulos. O vínculo deve indicar rota principal, documentação, limitações e próxima evolução.

## Como registrar evidências

Acesse `/Editais/{editalId}/Requisitos/{requisitoId}/Evidencias`. Evidências aceitas: tela, rota, API, relatório, CSV, documento, print, vídeo, log, teste, manual, código ou configuração. Se não houver storage, URL/texto é aceito. Não incluir dados sensíveis em evidências públicas.

## Como gerar roteiro da POC

Acesse `/Poc/Editais/{editalId}/Roteiro` e gere roteiro com requisitos críticos e obrigatórios, ordenados por módulo e rota de demonstração. A execução usa critério binário **Atende/Não Atende** quando o edital exigir. A POC não pode ser aprovada se houver requisito crítico não atendido.

## Como gerar relatório técnico

Acesse `/Editais/{editalId}/RelatorioAderencia`. HTML, CSV e JSON são saídas reais. PDF pode ficar como exportação em implantação até existir gerador. O relatório inclui dados do edital, resumo executivo, percentual, requisitos por status, críticos, módulos, evidências, riscos, plano de ação e conclusão.

## Critérios Atende/Não Atende

- **Atende**: funcionalidade existente, demonstrável, com evidência validada e sem impedimento crítico.
- **Não atende**: requisito não implementado ou incompatível.
- **Parcial**: há cobertura parcial, com lacunas descritas.
- **Em implantação**: existe plano/execução em andamento, separado do funcional entregue.
- **Não avaliado**: padrão seguro para requisito novo ou sem evidência.

## Critérios críticos

Requisito obrigatório, eliminatório, LGPD, segurança, auditoria, SIAFIC ou integração essencial deve ser classificado como crítico. Crítico sem evidência não deve entrar em proposta como atendido.

## Como evitar resposta falsa

- Não alterar automaticamente para **Atende**.
- Exigir evidência validada para atendimento.
- Separar funcionalidade entregue de roadmap.
- Usar fallback honesto quando tabela, storage ou gerador não existir.
- Auditar mudança de status, aprovação/reprovação de evidência e exportação.

## Auditoria

Ações críticas chamam `IAuditTrailService`, que persiste em `sigov.auditoria_evento` quando disponível ou registra aviso em log quando a tabela não existe.
