# Diagnóstico inicial Pós-RC 24

- SHA inicial: 037d9a425265182826b73141a547183be182177b
- Branch inicial no ambiente: work
- Branch de trabalho criada: codex/pos-rc-24-premium-central-trabalho
- Base solicitada: main (indisponível no clone local; branch work usada como baseline operacional)
- SDK: {   "sdk": {     "version": "6.0.428",     "rollForward": "latestPatch"   } } 
- PostgreSQL alvo: 16 conforme CI/scripts
- PowerShell: scripts existentes em scripts/*.ps1
- Docker: docker-compose.yml e Dockerfiles existentes no repositório
- Build: validação executada durante este PR
- Migrations: manifest e scripts revisados para execução parametrizada segura
- Package/go-live: scripts existentes revisados
- Temas/menus/rotas: inventariados nos documentos complementares deste PR
