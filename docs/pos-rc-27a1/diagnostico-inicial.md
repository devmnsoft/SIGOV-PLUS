# Diagnóstico inicial Pós-RC 27A.1

- SHA inicial esperado/main: `086b56d28e424266a0504d101693bed0fe09b6a2`.
- Workflow inicial: run number `292`, run id `30051181611`, resultado `failure`.
- Job com falha: `workflow-integrity`, job id `89353389308`.
- Step com falha: `Install workflow tooling`.
- Versão anterior declarada: `ACTIONLINT_VERSION=1.7.7`.
- Bootstrap anterior: download manual do asset `actionlint_1.7.7_linux_amd64.tar.gz`, validação com `ACTIONLINT_SHA256`, extração manual via `tar` e instalação com `sudo mv`.
- Ferramenta adotada: `go install github.com/rhysd/actionlint/cmd/actionlint@v1.7.7` com `actions/setup-go` fixado por SHA completo.

## Observação sobre log remoto

O ambiente local não possui `gh` instalado e o remoto `origin` não está configurado neste checkout, portanto a captura automática do log completo do GitHub Actions não pôde ser feita a partir do workspace. O erro esperado e documentado do bootstrap anterior está associado ao bloco frágil de `curl`, `sha256sum`, `tar` ou `sudo mv` executado antes do `actionlint`.
