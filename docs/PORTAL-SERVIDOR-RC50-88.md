# Portal do Servidor — RC50.88

O portal resolve o servidor pela identidade autenticada e pelo tenant; nunca recebe um identificador técnico digitado. O usuário acessa apenas seus dados, contracheques fechados e liberados, informe de rendimentos, férias, requerimentos e documentos autorizados.

Visualização e download de conteúdo protegido geram `rh_auditoria_sensivel`, com finalidade e identificadores técnicos mínimos. CPF, dados bancários, remuneração detalhada e informações médicas não são registrados em log. Atualização cadastral pode ficar pendente de aprovação do RH. Protocolo só é associado quando existe módulo interno contratado e disponível.

Todas as páginas explicam seu uso e exibem aviso LGPD. A autorização mínima é `PORTAL_SERVIDOR_ACCESS`; criar requerimento exige `PORTAL_SERVIDOR_REQUERIMENTO_CREATE`.
