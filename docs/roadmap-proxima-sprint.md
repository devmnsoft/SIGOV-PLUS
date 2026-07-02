# Roadmap da próxima sprint

1. Homologar colunas reais por tabela operacional e trocar listagens demonstrativas por consultas Dapper coluna-a-coluna.
2. Implementar upload GED real com storage configurado e metadados em `sigov.arquivo`/`sigov.ged_documento`.
3. Ativar motores fiscais/financeiros reais antes de emitir guias ou lançamentos oficiais.
4. Expandir testes automatizados MVC para rotas operacionais e POST antiforgery.

## Próxima sprint — aprofundamento operacional SIGOV PLUS

1. Persistência real por módulo após homologação das colunas obrigatórias de cada tabela operacional.
2. Agregação da Busca integrada usando os novos services operacionais por fonte.
3. CSVs operacionais específicos em `/Relatorios` para protocolos, GED, contribuintes, débitos, contratos, jurídico e financeiro.
4. Minha Central com pendências reais por usuário/tenant.
5. POC com status calculado por service, indicação de fallback e última validação.
6. Storage provider real para GED/OCR, sem expor caminho físico e com auditoria de visualização/download.
