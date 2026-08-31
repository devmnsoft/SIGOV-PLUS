# eSocial, previdência e consignações no RH360

`rh_esocial_evento` registra estado pendente, enviado, retornado ou rejeitado, recibo e erro sanitizado. O produto não declara envio quando não existe adaptador externo configurado e contratado. Divergências cadastrais e de folha permanecem em pendências operacionais.

Parâmetros previdenciários são versionados por tenant, entidade, exercício e regime, com percentuais entre zero e cem. Consignações usam valor decimal não negativo e respeitam a margem parametrizada. Operações exigem `RH_ESOCIAL_VIEW` ou `RH_ESOCIAL_MANAGE`; informações sensíveis seguem as permissões LGPD do RH.
