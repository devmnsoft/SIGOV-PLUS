# Workflow run 292

- Run number: `292`.
- Run id: `30051181611`.
- Job id analisado: `89353389308`.
- Resultado: `failure`.
- Job verde: `release-context`.
- Job vermelho: `workflow-integrity`.
- Step vermelho: `Install workflow tooling`.
- Comando vulnerável removido:

```bash
ACTIONLINT_VERSION=1.7.7
ACTIONLINT_SHA256=023070a287cd8c6e348edb582c297280c079569ac8079378f671d2cc3d9f90b7
curl -fsSLO "https://github.com/rhysd/actionlint/releases/download/v${ACTIONLINT_VERSION}/actionlint_${ACTIONLINT_VERSION}_linux_amd64.tar.gz"
echo "${ACTIONLINT_SHA256}  actionlint_${ACTIONLINT_VERSION}_linux_amd64.tar.gz" | sha256sum -c -
tar -xzf "actionlint_${ACTIONLINT_VERSION}_linux_amd64.tar.gz" actionlint
sudo mv actionlint /usr/local/bin/actionlint
actionlint -version | tee workflow-integrity.log
```

- Exit code: não recuperável neste workspace sem `gh` e sem remoto configurado; tratado como falha não-zero do step.
- Jobs downstream: ficaram `skipped` por dependência do gate `workflow-integrity`.
