# Backlog de verificadores tipados da governança

A fila operacional de compras não transforma observações legadas em evidência. A revalidação automática permanece indisponível até que cada regra abaixo possua consulta tipada, autorização equivalente à origem e data de leitura verificável.

| Regra | Origem autoritativa | Pré-requisitos para implementar |
|---|---|---|
| Integridade cadastral | Entidade produtora da ocorrência em `qualidade_dados_ocorrencia` | contrato tipado por regra, recorte de tenant/entidade/exercício, permissão persistida e teste PostgreSQL |
| Consistência referencial | FK ou consulta de domínio do módulo produtor | definição formal da condição, tratamento de legado, relógio/fuso e resultado auditável |
| Atualidade do dado | evento ou coluna temporal publicada pelo módulo produtor | SLA parametrizado por esfera/contexto, origem temporal confiável e teste de concorrência |

Até esses pré-requisitos existirem, `VERIFICADOR_INDISPONIVEL` é o resultado correto: ele preserva o estado e orienta a correção na origem sem simular sucesso.
