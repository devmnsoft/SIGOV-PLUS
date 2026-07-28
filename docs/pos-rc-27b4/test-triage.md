# Triagem de testes

Nenhum TRX está presente no checkout e o cliente GitHub (`gh`) não está instalado. Assim, não é possível afirmar FQNs falhos, jobs verdes/vermelhos/skipped ou importar artefatos do run 302 sem inventar evidência. A lista de FQNs localmente atingidos pela ambiguidade foi encerrada pela busca de todas as ocorrências de `WebApplicationFactory<Program>` nos projetos API e Integration; após a migração, a busca retorna zero.
