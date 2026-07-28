# Diagnóstico inicial — Pós-RC 27B.6

Base auditada: `200eb8e416efc8fcda2af4b2836750a6c56351de`. O defeito reproduzível por inspeção era `EnterpriseModulesController` implementar `IAsyncActionFilter`, expondo `OnActionExecutionAsync` ao application model. A migration operacional também compilava um `UPDATE` com nomes de colunas legadas mesmo quando ausentes. Por fim, os adaptadores Web gravavam colunas não canônicas e encaminhavam uma operação chamada outbox à tabela de eventos operacionais.

O artefato TRX do run 30365430804 não está disponível no checkout. A importação nominal dos 45 FQNs depende da disponibilização desse artefato; nenhum nome foi inventado neste documento.
