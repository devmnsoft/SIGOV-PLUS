# Arquitetura standalone

API, Web, Worker e AdminBootstrap devem ser publicados para win-x64 e linux-x64, em variantes framework-dependent e self-contained. Produção usa configuração por ambiente, migrations em `ValidateOnly`, processos separados, health checks reais e graceful shutdown. Docker é uma opção de implantação, não evidência de execução standalone.
