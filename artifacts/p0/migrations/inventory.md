# Inventário de migrations P0

SQLs: 184; manifesto: 174; órfãs: 10.

Gate estático: PASS. P0: BLOCKED; validação semântica exige PostgreSQL 16.

| Ordem | Versão | Arquivo | Classificação | Automática | Baseline | SHA-256 normalizado |
|---|---|---|---|---|---|---|
| 1 | 001 | 001_create_sigov_schema.sql | registered_automatic | True | True | db158b31ab57993385a55081ac7740f3398ac930e5b48daaebf5f208c99422f8 |
| 2 | 002 | 002_create_sigov_infrastructure.sql | registered_automatic | True | True | 82d4c047824f1f19e58776c141de07b17bea5987755d5a9465d57b9fcd980130 |
| 3 | 003 | 003_create_sigov_core.sql | registered_automatic | True | True | 915e1855f476c7718b21fb28045f7e925f4c47a71be025ba837047e4c06e2b1e |
| 4 | 004 | 004_create_sigov_security_audit_lgpd.sql | registered_automatic | True | True | 5120fd5bf64a70822392203de23d181eb47c67963c01c06b970889473931a4b2 |
| 5 | 005 | 005_create_sigov_bi_workflow.sql | registered_automatic | True | True | 4cbb06c5506fde45f36b0f2bbbc1787a58655282013d6b2a89a7a0e1cb1132fb |
| 6 | 006 | 006_create_sigov_financeiro_tributario.sql | registered_automatic | True | True | 4cbb06c5506fde45f36b0f2bbbc1787a58655282013d6b2a89a7a0e1cb1132fb |
| 7 | 007 | 007_create_sigov_compras_rh_educacao.sql | registered_automatic | True | True | 4cbb06c5506fde45f36b0f2bbbc1787a58655282013d6b2a89a7a0e1cb1132fb |
| 8 | 008 | 008_create_sigov_saude_social_saneamento.sql | registered_automatic | True | True | 4cbb06c5506fde45f36b0f2bbbc1787a58655282013d6b2a89a7a0e1cb1132fb |
| 9 | 009 | 009_create_sigov_suporte_integracao_geo.sql | registered_automatic | True | True | be7ca9bbfdd434e1a34e5ebb3f1c991c76e457060d04c01b2562d99a5d19d441 |
| 10 | 010 | 010_create_sigov_indexes_constraints.sql | registered_automatic | True | True | ff6b20cdc7fbc111b653c2e9236cde4ad6d55fac63f1d51b4b0579747f4179f3 |
| 11 | 011 | 011_seed_sigov_dev.sql | registered_excluded | False | False | c7beb5fd376638f7aa6ee9096e28d9bd21be342e7182b10e60d992c9bc348606 |
| 12 | 012 | 012_compat_move_legacy_schemas_to_sigov.sql | registered_automatic | True | True | cdcfb4c2be906034aa3f84989580f2603f34dbaef3a9d67ca3353c2abd87f699 |
| 13 | 013 | 013_sigov_foundation_refactor_fixups.sql | registered_automatic | True | True | 83feb1dd51b0ab70a4c75bb1f0a15630ff6a63ff8763ef4b747b82dfbecf9367 |
| 14 | 014 | 014_saas_tenants_planos_assinaturas.sql | registered_automatic | True | True | b6617e6857b29d8d2cc10deb00d7a2b5a96e2b10767c17a3462d72562028fce0 |
| 15 | 015 | 015_saas_tenant_id_operational_tables.sql | registered_automatic | True | True | 0bc8dffc1bd1c554cccd87b5bd29fd2653c0f2b78b7ce05dc1f77a2ad7d4801f |
| 16 | 016 | 016_saas_rls_indexes_usage_operacao.sql | registered_automatic | True | True | b2828db9342edf4bba6bf3a0dde03525a473662f51594ab4dbb2cf0fc054de55 |
| 17 | 017 | 017_processos_digitais_protocolo_ged.sql | registered_automatic | True | True | 591ffe8c5a2504063251aeafbcbd3f142a5cf9d88ac59270263c7ab03f8c2fea |
| 18 | 018 | 018_financeiro_siafic_base.sql | registered_automatic | True | True | 6fa173ea8ade1c3a96d0c3fda5e15a5d9e6a638ed85eca6d0bfe023252f1b75c |
| 19 | 019 | 019_core_pessoas_enderecos_operacional.sql | registered_automatic | True | True | b613b596f93c034a9d86079e2dde1d66b7646c27db9c80678e7735a2145f669e |
| 20 | 020 | 020_rh_completo.sql | registered_automatic | True | True | 614bf707d78e59d5dcd949376db64bbf76a33e4737bcd4395de26a9fc13aaf8e |
| 21 | 021 | 021_educacao_base.sql | registered_automatic | True | True | 3a3cd3d06f45eea9d539e92379dc1fe939d32d7ec5b5a5ccb0b89fe83261acd6 |
| 22 | 022 | 022_saude_acs_base.sql | registered_automatic | True | True | 4f8fe4ff99b9d2051d413d0dce9dddfaba22ca89e2fb1d0db80edd219953226c |
| 23 | 023 | 023_saneamento_base.sql | registered_automatic | True | True | a1c7814c95e8fba11c34eaff902ffcc9a06c55f24ebe49b3dd076fe1541119ff |
| 24 | 024 | 024_assistencia_social_base.sql | registered_automatic | True | True | cb4117e8390fbd7fe281d678178ae57943f803d30d4052a18c953f4d875aabba |
| 25 | 025 | 025_integracoes_outbox_webhooks_base.sql | registered_automatic | True | True | f490df56436be82750d17a4901be21d95604f2582a1d19976b3f1006ceb35a3c |
| 26 | 026 | 026_agro_fundacao_geo_dashboard.sql | registered_automatic | True | True | 00e6d35ad5207c2358536fa3c0a1537b1731e7d81147eee04468eb8085352bbc |
| 27 | 20260607090000 | 20260607090000_ui_commercial_finish.sql | registered_automatic | True | True | 5fe564a1d24f3c1adeebc1ab9395a64d3fc7142af2dcff1acbedaff122d35847 |
| 28 | 20260608090000 | 20260608090000_saas_parametrizacao_perfis_modulos.sql | registered_automatic | True | True | 3a71972e2de1bd3875f3f41f6b29a67b1c395fb70ce0d702494b8d42c0728ecb |
| 29 | 20260608100000 | 20260608100000_agro_produtores_propriedades_producao.sql | registered_automatic | True | True | 64223a637d52be3be16fdd93f7855c59490bd818282005dd66ed7b7dc3b5af10 |
| 30 | 20260608110000 | 20260608110000_agro_programas_beneficios_patrulha_mecanizada.sql | registered_automatic | True | True | eb27031762fffc1af84b0910cc4db9be0ca5ce4217c031c16c9ce2c490afccbe |
| 31 | 20260608120000 | 20260608120000_plantao_pro_white_label_b2b_launch.sql | registered_automatic | True | True | 935bde3c972969869739d269275045fe7a0ccd43c4728c72e052b15446b373f1 |
| 32 | 20260608120001 | 20260608120000_saas_comercial_white_label_planos.sql | registered_automatic | True | True | 3ca35507736fc13725c048dbdf9c5e503d289ff89408c850fbf083f8a6eb792b |
| 33 | 20260608130000 | 20260608130000_agro_estradas_feiras_agroindustrias.sql | registered_automatic | True | True | c0dc03b5c58683df9bf2b3432dc3074741765022bf19aa69641db8d6df9fe2c9 |
| 34 | 20260608140000 | 20260608140000_agro_relatorios_bi_transparencia.sql | registered_automatic | True | True | 48b1f45de1305ae799b70163c09a648367ea38fa33bb8a26299afe8e02a3fdad |
| 35 | 20260609090000 | 20260609090000_pos_build_dashboard_saas.sql | registered_automatic | True | True | e5a8b9cfc0881ed708c6e5d9a638af77fb51fb74ac5f18f9b52c88bad906b63d |
| 36 | 20260609120000 | 20260609120000_pos_build_03_saas_implantacao_tributario.sql | registered_automatic | True | True | 81e7afaa86c5852db55c2b5571a12c976acfb3b86bc2a5bdb071f6295739b26c |
| 37 | 20260610100000 | 20260610100000_pos_build_04_enterprise_modules.sql | registered_automatic | True | True | ac5d58103dfda6a16e9fbd9a1e0a794d02f4a0a213e9272ceb9a140c660cc593 |
| 38 | 20260610120000 | 20260610120000_pos_build_05_comercio_varejo_atacado_pdv_caixa_financeiro.sql | registered_automatic | True | True | 884048fd17bac8daf747bf72712aae0e7f1fd58c93ce842cd50d78be97282d3b |
| 39 | 20260610150000 | 20260610150000_pos_build_06_industria_producao.sql | registered_automatic | True | True | 2c1e598b342cfdfb0e16dd23eb43e87c9e9f670a1f62d13ac50f6d4f7c7296b5 |
| 40 | 20260610180000 | 20260610180000_pos_build_07_financeiro_integrado.sql | registered_automatic | True | True | 7524a57b97313f65ca623dfbb1865d20588fbc55d23d69835e550c8837c291fe |
| 41 | 20260610200000 | 20260610200000_pos_build_08_tributario_avancado_iptu_iss_dam.sql | registered_automatic | True | True | 3c1245b6fb26c48f79c03488630dc4cc23702d5697f0bd86754626ba3f8b4cf2 |
| 42 | 20260610220000 | 20260610220000_pos_build_09_ged_ocr_assinatura_automacao.sql | registered_automatic | True | True | f3507d6d32bd90255fe06308619888dfc0f8a0f04ae8589d4234b29ebd90f860 |
| 43 | 20260611110000 | 20260611110000_pos_build_11_ia_automacao_assistentes.sql | registered_automatic | True | True | 8695b37f308d73f762be4d2f3cce64bda4543d6b7bbd35bd202ba4c6723f66b8 |
| 44 | 20260611130000 | 20260611130000_pos_build_12_mobile_pwa_campo_offline_geo.sql | registered_automatic | True | True | 2ac4af881a6129de92870b76b7436e83d4a6386a32f0f140215d89463fbf8c71 |
| 45 | 20260706120000 | 20260706120000_consolidacao_modulos_transversais.sql | registered_automatic | True | True | 5cafdb6965470bb9680a73dd15b0f31a259a75616891c36f1c9b9a6a2f8b805d |
| 46 | 20260706153000 | 20260706153000_pos_rc_protocolo_ged_workflow_api_outbox.sql | registered_automatic | True | True | a8b4347d56e0a9debcbe6ae15c254a5650e638c30454c0b6ada9fb062102ce39 |
| 47 | 20260709120000 | 20260709120000_enterprise_funcional_crud.sql | registered_automatic | True | True | db364a5699b6d8c2a679eda8cc9f545fd4bea7333ec420be2c03fa64dc7c6784 |
| 48 | 20260713120000 | 20260713120000_pos_rc_11_enterprise_anexos_release.sql | registered_automatic | True | True | 50c5782933e893879759ed0e2f1bcc8b309eca3526cec1e85415b8adc3c39dc7 |
| 49 | 20260721120000 | 20260721120000_pos_rc_17_runtime_nucleo_operacional.sql | registered_automatic | True | True | 3a6e2e05531dd3d6a725a2a31c86592d6ce1c8ad5f06142ce0d9bec4e55b387f |
| 50 | 20260722120000 | 20260722120000_enterprise_tenant_mapping.sql | registered_automatic | True | False | 5de9eef9b5ef879f374093df3e78db27dd4528fb9e5badae9b5c59680642d0a7 |
| 51 | 20260727120000 | 20260727120000_pos_rc_27b2_tarefa_canonica.sql | registered_automatic | True | True | 177f36a01775fbc78039e1f77555def37ed0e15442946530bd55f5bd9539c62d |
| 52 | 20260727160000 | 20260727160000_pos_rc_27b3_operacional_canonico.sql | registered_automatic | True | True | bac96f2ba1336226e29c46746a046b672b9588e1f9611fb3ff316b95f0fdf9b8 |
| 53 | 20260730090000 | 20260730090000_pos_rc_32_ordem_servico.sql | registered_automatic | True | True | cbd5cac3058c6483df9e17fe20ebb37ca8c2bebeac2115d8b4c956b74b56193c |
| 54 | 20260730110000 | 20260730110000_pos_rc_36b_permissao_chave_canonica.sql | registered_automatic | True | True | 740ef5203f9c67ee5c1519cd8baa971638bea21dd2d4c3a9371ea117308731c3 |
| 55 | 20260730120000 | 20260730120000_pos_rc_29_comercial_operacional.sql | registered_automatic | True | True | 7559bf14cf6fa13e79ed12dd1ae4b8bb8e232ee3b67f17658e334dd6256c1fb5 |
| 56 | 20260730170000 | 20260730170000_financeiro_empresarial_legacy_columns_compat.sql | registered_automatic | True | True | 8f2d255f366624b68e0d813ed3799cf1da15a90249c3892bbb1015022344a67e |
| 57 | 20260730180000 | 20260730180000_pos_rc_30_financeiro_empresarial_real.sql | registered_automatic | True | True | 148baa107e4ba06c51c55ca09905b47555ed14d4459a667127375cc7e1922042 |
| 58 | 20260730210000 | 20260730210000_pos_rc_31_consolidacao_comercial_financeiro.sql | registered_automatic | True | True | 15d98661c71910d5d9c0fe62b886d0efbb714982eded82742042707d54642cbf |
| 59 | 20260731120000 | 20260731120000_pos_rc_33_operacao_servicos_campo.sql | registered_automatic | True | True | 56970917a32ca0a69809717b4d009f6ed7071c000c50fec56b44351ac1acdb04 |
| 60 | 20260802210000 | 20260802210000_pos_rc_37b_compras_empresariais_fullstack.sql | registered_automatic | True | True | d434c10831003b2324b127455cbef1ccd1dafb0d8346e52803e0e770f52b6331 |
| 61 | 20260807120000 | 20260807120000_rc46_operacao_integrada.sql | registered_automatic | True | True | a30d8f7595b84c27fd786b9076a2c4e556e9d6b701e01ff0111e249ff2cc6be6 |
| 62 | 20260809120000 | 20260809120000_rc48_authentication_hardening.sql | registered_automatic | True | True | 77889bb6bad806646473e6309ac07e58b911d42430e985323676177f3cd01821 |
| 63 | 20260809160000 | 20260809160000_rc49_workflow_platform.sql | registered_automatic | True | True | c9879e5a853fe2b42ccd38e756fa8b4cdae858ead423faf3c0eb46fbe9d907ae |
| 64 | 20260813223000 | 20260813223000_rc50_32_bloco2_rh_core.sql | registered_automatic | True | True | e7cfa03b784033b79a4da64a3050022641bcd460c894302937c94ef0d987cfc1 |
| 65 | 20260813230000 | 20260813230000_rc50_34_educacao_secretaria_bloco3_core.sql | registered_automatic | True | True | 717441d428e6451c358adcbbfc1b726e623f6003990414f9c636d17995505bc5 |
| 66 | 20260813231000 | 20260813231000_rc50_34_educacao_diario_classe_bloco3_core.sql | registered_automatic | True | True | 52f6cb9773f5d26322d1715aa7434b65c384ac28d167a4164922328b7cb5d3a5 |
| 67 | 20260813232000 | 20260813232000_rc50_34_educacao_portal_responsavel_bloco3_core.sql | registered_automatic | True | True | 48aaaebb1ea4db55f15e3f7d6212bdc5c40cdbc02a10f1a1dcb2ea605207d27f |
| 68 | 20260814120000 | 20260814120000_rc50_36_financeiro_siafic_bloco5_core.sql | registered_automatic | True | True | 21525439b3ddeafd72d2d53029f3d2cd16272d7648387eff2de4c3c04fa506a3 |
| 69 | 20260814121000 | 20260814121000_rc50_36_tributario_divida_ativa_bloco5_core.sql | registered_automatic | True | True | 36b821f45a5b9d78212c9d287d889628ff7ae7a92a9d47c0bde7920b171c1cf6 |
| 70 | 20260814122000 | 20260814122000_rc50_36_relatorios_executivos_bloco5_core.sql | registered_automatic | True | True | b4c74ede176a48826069e0ece3d9b8ddd301f453083d86eb5f5ee8a443cc6ec2 |
| 71 | 20260816120000 | 20260816120000_rc50_38_saude_bloco7_core.sql | registered_automatic | True | True | cd96345171144560bdfb9db14fe0f5e713f9525b219110583202eee00a0d40e7 |
| 72 | 20260816121000 | 20260816121000_rc50_38_assistencia_social_bloco7_core.sql | registered_automatic | True | True | a6554696f26233a432e3cfad41a9714227648d4f99d012f1e45f205836c5c39b |
| 73 | 20260816122000 | 20260816122000_rc50_38_saneamento_bloco7_core.sql | registered_automatic | True | True | cc24ac60670d2459ae7611a3d784c087eac41493df995bc480ba40c6374a34a8 |
| 74 | 20260816123000 | 20260816123000_rc50_38_frotas_obras_bloco7_core.sql | registered_automatic | True | True | e58ce5160ec7573d45ab7c1d4f4d7d4dc3ef18474e7b86792e7653a2f87554fa |
| 75 | 20260817120000 | 20260817120000_rc50_42_processos_digitais_core.sql | registered_automatic | True | True | 0e9e2f92a8fe6d24d572578414194631b6086d5cc68a6293538ffafa07332853 |
| 76 | 20260817121000 | 20260817121000_rc50_42_ged_assinaturas_core.sql | registered_automatic | True | True | f01c2ff0a7ad334082bc3f24eb9752a4122f58b984b102373d4315a72c43101b |
| 77 | 20260817122000 | 20260817122000_rc50_42_legislativo_camara_core.sql | registered_automatic | True | True | 71bb8284719919e8cc3b4d1e0396cdb95d8969a65b400d112b6797cca322f80a |
| 78 | 20260817123000 | 20260817123000_rc50_42_transparencia_atendimento_core.sql | registered_automatic | True | True | 3cf5feb4c6b5a595dc32f2f2bdcaaf3d824f6f773c61b21ca6fcd6ed6e01fff9 |
| 79 | 20260817130000 | 20260817130000_rc50_43_empresarial_comercial_core.sql | registered_automatic | True | True | c73ac0207d7a12c625391bda98f60da76f8229c69520bfed01dd6a22052f0a4e |
| 80 | 20260817131000 | 20260817131000_rc50_43_empresarial_ordem_servico_core.sql | registered_automatic | True | True | 44340d33b6b58b501d86871748cf00ea803d68d8336e3f21d03fcd930e6bf9d8 |
| 81 | 20260817132000 | 20260817132000_rc50_43_empresarial_estoque_core.sql | registered_automatic | True | True | 80bb1e2e18ae68c0999c077983aebdc819f24a3d1a96798eea2663cf5423e658 |
| 82 | 20260817133000 | 20260817133000_rc50_43_empresarial_industrial_core.sql | registered_automatic | True | True | f851b5d3855a6aed8fdd78692b8f50395fab52f19b588e83c704d9e31824faf4 |
| 83 | 20260817140000 | 20260817140000_rc50_45_processos_digitais_core.sql | registered_automatic | True | True | aec28f0c3cd4778778acf04fadf7035247b3d3d5fbc7926c409f3c548bbf595c |
| 84 | 20260817141000 | 20260817141000_rc50_45_ged_assinaturas_core.sql | registered_automatic | True | True | a613ad05f8cb8bc9f2304ac5d875d3aa1ce5a9caee769382bdb20554a973d38b |
| 85 | 20260817142000 | 20260817142000_rc50_45_legislativo_camara_core.sql | registered_automatic | True | True | 595a5a64c98ab84c4397d08c2326264573bf903c2aeaa55735614f7872abdd95 |
| 86 | 20260817143000 | 20260817143000_rc50_45_transparencia_atendimento_core.sql | registered_automatic | True | True | 8adc0a87f963cf8842b0b5d0917c0c3a36c5a3f583157d36613489bf0cee3a74 |
| 87 | 20260817150000 | 20260817150000_rc50_47_tributario_carnes_boletos_core.sql | registered_automatic | True | True | c422ba944d98fedecd96874d095c9c066f946c1414aecdfa84fa6e5010c81783 |
| 88 | 20260817151000 | 20260817151000_rc50_47_tributario_portal_contribuinte_core.sql | registered_automatic | True | True | fc7c6f223c6b8b6299101edc48a6140a19292945f182f2fa453cfc2b505688b7 |
| 89 | 20260817152000 | 20260817152000_rc50_47_tributario_fiscalizacao_issqn_core.sql | registered_automatic | True | True | 97fcd4df32ab7237ad2e0008f45540cb4b43738ddad974bbb5d4198bdc069ba9 |
| 90 | 20260817153000 | 20260817153000_rc50_47_tributario_nfse_desif_core.sql | registered_automatic | True | True | 99f7a3aea02ffe6beb69b9ce0571206dde7967dbf873c885439586a5ef973b8f |
| 91 | 20260818120000 | 20260818120000_rc50_48_educacao_transporte_escolar_core.sql | registered_automatic | True | True | 5b79e8564806fbee9d9644920157c638ad686c5631b2a7eb61e2b1957430c99f |
| 92 | 20260818121000 | 20260818121000_rc50_48_educacao_merenda_cardapio_core.sql | registered_automatic | True | True | 1ec8fd32b0eb99fd632772c4d0a34234f1861b7d969a2aeb0f562393eba7377f |
| 93 | 20260818122000 | 20260818122000_rc50_48_educacao_biblioteca_digital_core.sql | registered_automatic | True | True | 3acf39181f7f1da2075dcd57a932336a783657eb8b9ba71ddd0b267af788c38f |
| 94 | 20260818123000 | 20260818123000_rc50_48_educacao_fundeb_custos_indicadores_core.sql | registered_automatic | True | True | 1e891bd1ab0a5b5a88c38d4923afb0ac394e9fbffc12c4bb8cf5fad74d173bd3 |
| 95 | 20260818130000 | 20260818130000_rc50_49_saude_acs_offline_georreferencia_core.sql | registered_automatic | True | True | 646efcef3c105612a6f4a5f2889338f979e96dfcb1c97a0fe53264cf20f1c1a0 |
| 96 | 20260818131000 | 20260818131000_rc50_49_saude_cadastros_visitas_core.sql | registered_automatic | True | True | 0904e625ab3de379c1868f8ea35c8f309b0ebae44a2f43470f7a015502983d2e |
| 97 | 20260818132000 | 20260818132000_rc50_49_saude_vacinacao_farmacia_regulacao_core.sql | registered_automatic | True | True | 5397cbc32104bf1c11c2b51b29eab374e1a3208b7ed3cd69fb2f07a14a0f9a49 |
| 98 | 20260818133000 | 20260818133000_rc50_49_saude_retaguarda_sla_indicadores_core.sql | registered_automatic | True | True | 600ab03eb8ddb0df813e1018c37670852ce3a0d951a3c72ba1f187e09d149842 |
| 99 | 20260818140000 | 20260818140000_rc50_50_saneamento_comercial_atendimento_core.sql | registered_automatic | True | True | 50ed25704f7a746d5f7e49ad217e313da9431fd98af35f72e7b6e28525d1339a |
| 100 | 20260818141000 | 20260818141000_rc50_50_saneamento_leitura_faturamento_arrecadacao_core.sql | registered_automatic | True | True | 792e1423928b022612fd777925f5016d97882fd09986650f8ab9e12f44d9a7bd |
| 101 | 20260818142000 | 20260818142000_rc50_50_saneamento_operacao_campo_core.sql | registered_automatic | True | True | b03968468dc02a4a1fbf2c39fe4c6a01c89951250bc80edac201393b36b867e3 |
| 102 | 20260818143000 | 20260818143000_rc50_50_saneamento_gis_laboratorio_qualidade_core.sql | registered_automatic | True | True | a9f081bbebb8fe346436fb86a8d45b065310f861f12c85521de9c0fc1df4b330 |
| 103 | 20260818150000 | 20260818150000_rc50_51_governanca_seguranca_lgpd_auditoria_core.sql | registered_automatic | True | True | 493f16894822bbe7f1409f71e590a2f4eac29bbc368390989e8311699c8efe63 |
| 104 | 20260818160000 | 20260818160000_rc50_52_lgpd_operacional.sql | registered_automatic | True | True | e06f60193ea76d270d602536c791744fd51c335b6b7cb8ee3c77144725a5b3ee |
| 105 | 20260819120000 | 20260819120000_rc50_60_fluxos_educacao_saude_core.sql | registered_automatic | True | True | 5c4a26e582e967309c817bebecf3c089d69059d1a219c38a30aee8f56e94e41d |
| 106 | 20260819130000 | 20260819130000_rc50_61_fluxos_documental_institucional_core.sql | registered_automatic | True | True | 0892a363f7cd04eb5e6231eb8afcdf25770fb243a78cfbd6d495dbd650193617 |
| 107 | 20260819140000 | 20260819140000_rc50_62_fluxos_administrativos_internos_core.sql | registered_automatic | True | True | 6880555e3aceb537dc6ebc94a68ae81e060a63ff761a66a76381befc0923f926 |
| 108 | 20260819150000 | 20260819150000_rc50_63_consolidacao_transversal_core.sql | registered_automatic | True | True | 136f95b3fd388e640ec0312e8da7d4fb618bf45916a1207f1fc6365a2fcd74be |
| 109 | 20260820120000 | 20260820120000_rc50_68a_autorizacao_persistente.sql | registered_automatic | True | True | 15c33ca8593c7dfc0daa01a57e02800fa71c75139267b94502f3a7d93d612571 |
| 110 | 20260820160000 | 20260820160000_rc50_68b_avaliador_efetivo.sql | registered_automatic | True | True | cdf95bcec5cf04cd80299bb93a6cfe0936cede30ddda9210c2f75b6eb867e10c |
| 111 | 20260820200000 | 20260820200000_rc50_68c_contexto_operacional_seguro.sql | registered_automatic | True | True | b349733d0bbdbe6fbfdd33b90b87b703439e6aca417734b45cc03eca506abe09 |
| 112 | 20260820230000 | 20260820230000_rc50_68d_superadmin_dashboard_operacional.sql | registered_automatic | True | True | e96bfa078a1ee0fb6fc5d1aaf3b6202ad4aceb0c91ed6b409f4ed42ce72c700c |
| 113 | 20260821120000 | 20260821120000_rc50_68f_admin_autorizacao_contextual.sql | registered_automatic | True | True | 3908cb29fcff7c550ed99176f3503b1fdbfffcd88cf40490ac685d6431d26784 |
| 114 | 20260824120000 | 20260824120000_func01_patrimonio_inventario.sql | registered_automatic | True | True | d1e705d8727ad182ce18d0820147edf47bc92c9a3af8d889943a69c59036373f |
| 115 | 20260824180000 | 20260824180000_func02_almoxarifado_estoque_requisicoes.sql | registered_automatic | True | True | 15a775c2ee963a62dfc3923e33d17dabc0fee768f954b269418ca4b4cd666007 |
| 116 | 20260824200000 | 20260824200000_func03_compras_licitacoes_contratos.sql | registered_automatic | True | True | 407e376e0e11e60b2a55d480c3b4060a0f55a1fa63e604e78b8754dbe184cff0 |
| 117 | 20260824220000 | 20260824220000_func04_frotas_abastecimento_manutencao.sql | registered_automatic | True | True | a51f54ebe93fc03d3f280ba195e3545282d68d80ea300d0b91b17007b6c14587 |
| 118 | 20260824230000 | 20260824230000_func05_educacao_gestao_escolar.sql | registered_automatic | True | True | f8311ea4f3eb7b49dae7d7b84d88a3238e97fb473e3bb1cb2fba24b119be7c86 |
| 119 | 20260825000000 | 20260825000000_func06_saude_atencao_basica.sql | registered_automatic | True | True | 69cf4c6ffaa1069f38d33676c732b5515a27a0f6597f01ddd0a70d1141789003 |
| 120 | 20260825010000 | 20260825010000_func07_saneamento_comercial_operacional.sql | registered_automatic | True | True | 2ed8027063d864eccd79b6524f6052acbcbe578187cc9c0cdd9cc2376ed5a90c |
| 121 | 20260825020000 | 20260825020000_func08_assistencia_social_cras_beneficios.sql | registered_automatic | True | True | fad32bffa62a91cf8d7dcb05ee6a02b6f22cf23378a6060cab91c94d5f0cb247 |
| 122 | 20260825030000 | 20260825030000_func09_tributario_receita_municipal.sql | registered_automatic | True | True | f0ca8c1df6014f66e45fa1125536b5a05cf3f8f1a167308f5f87014c080a35f6 |
| 123 | 20260825040000 | 20260825040000_func10_financeiro_siafic_tesouraria.sql | registered_automatic | True | True | abf22f0cc71c188ed73592739be04f360c6b9ea84de92dfa739b24b14cd2f5c9 |
| 124 | 20260825050000 | 20260825050000_func11_agro_desenvolvimento_rural.sql | registered_automatic | True | True | d5a269a1812ff2e0f04e9e61661fc8e89e4042653c341b0c71a1c43c341ced85 |
| 125 | 20260825060000 | 20260825060000_func12_rh_folha_pagamento.sql | registered_automatic | True | True | 2a2fbfe6a03cdd016b7d1928400f821c812427acb095c0b8d4f14ce3230b413f |
| 126 | 20260825070000 | 20260825070000_func13_obras_engenharia_fiscalizacao.sql | registered_automatic | True | True | b237a71a486d5c77b41f2680dba742540d59626efb6658b092fc8b018d4ec111 |
| 127 | 20260825080000 | 20260825080000_func14_meio_ambiente_licenciamento_fiscalizacao.sql | registered_automatic | True | True | c17bc4c1a670874f6a3834e5e9ef5ccc5e243ce7a93402bf4dd185bcbcf13e64 |
| 128 | 20260825090000 | 20260825090000_func15_ouvidoria_atendimento_esic.sql | registered_automatic | True | True | f3f67e18a94a43bbffde349c34cbb680ad07b6bf5019ad6fd4e18100128d2364 |
| 129 | 20260825100000 | 20260825100000_func16_habitacao_regularizacao_fundiaria.sql | registered_automatic | True | True | e38ec92173de44ce68ac074afe7853a00e5e8f1a60cfc923c1d0afab0c464f82 |
| 130 | 20260825110000 | 20260825110000_func17_procuradoria_juridica_contencioso.sql | registered_automatic | True | True | 4f4ffbd1a1881f26cf8330898c4cca995441c26d968688a1290b96015872f4ab |
| 131 | 20260825120000 | 20260825120000_func18_transito_mobilidade_fiscalizacao.sql | registered_automatic | True | True | e06ecde9b7d37b6d49c158ca6fe362f8b08b7ac814e23177a547971f729ad397 |
| 132 | 20260825121000 | 20260825121000_corr18_transito_validacoes_indices.sql | registered_automatic | True | True | d26ede93088c94c26405f02297f93404ab2e64f8e3593826ce1559697ef36c60 |
| 133 | 20260825130000 | 20260825130000_func19_defesa_civil_guarda_municipal.sql | registered_automatic | True | True | 22245d99f38a4897d366c05e801f45500433fa5ccbcce00be700b989e8485ec1 |
| 134 | 20260825131000 | 20260825131000_corr19_defesa_indices.sql | registered_automatic | True | True | 49478a74cc5411268608c4a45f5021ebe1156e5ba3b7a6e0b38a7b23f5ae7a32 |
| 135 | 20260826100000 | 20260826100000_func20_convenios_emendas_prestacao_contas.sql | registered_automatic | True | True | 1cee70b65cb7f175edc557bde6d6dbbceac918aeb7915e81f43b75f409f8ab4d |
| 136 | 20260826110000 | 20260826110000_corr20_convenios_integridade_indices.sql | registered_automatic | True | True | 785151a081efcac02a48502696352e17ad3cde39a2982ec2c8a49e982b243bb2 |
| 137 | 20260826120000 | 20260826120000_rc50_68_fundacao_transversal.sql | registered_automatic | True | True | 1b94531a4a583b62400e7c70e9858eb09c749a42ce25fc2473bd73e5772b754c |
| 138 | 20260826130000 | 20260826130000_exp03_licitapro_func03.sql | registered_automatic | True | True | 6cebafe4e884b6105b6fee01715b412809035599b964179f4b42a9c9fc66adeb |
| 139 | 20260826140000 | 20260826140000_corr03_licitapro_integridade.sql | registered_automatic | True | True | 0191865793bc9845ec077ed2c78964e7866a80acbdd61521bd06e6272aad5e9a |
| 140 | 20260826150000 | 20260826150000_exp_fiscaliza360_transversal.sql | registered_automatic | True | True | a982e8e363ac82f152431eae21e1985d25d9e4535288e4fccdaa158fdb48d967 |
| 141 | 20260826160000 | 20260826160000_exp13_obras360_func13.sql | registered_automatic | True | True | 1dae1d0fbff3a7129faa68d47d9260387ca9d96b315d33a453142f8e4b3c114d |
| 142 | 20260826170000 | 20260826170000_corr13_obras360_validacoes.sql | registered_automatic | True | True | 2ebc8a069d82319703eb0a7e3beb0fde4064fb64b558dec44a6bb8d6492b3c0c |
| 143 | 20260826180000 | 20260826180000_exp19_defesacivil360_func19.sql | registered_automatic | True | True | f98bf85a739f3d01c653be3e8b1a4f92be0c66dd2b8838ffa662e6f1045b5af6 |
| 144 | 20260826210000 | 20260826210000_corr19_defesacivil360_integridade.sql | registered_automatic | True | True | e3784f95cd33b338228ac35b7b0174fffdf66397731175590182e4adce5c7d60 |
| 145 | 20260826220000 | 20260826220000_exp08_ativos360_integrado.sql | registered_automatic | True | True | b56aeb80754344edea1525ec65573d73fe6da09ab8d603c2dda06d98bf46048e |
| 146 | 20260827100000 | 20260827100000_exp04_cidadao360_portal_servicos.sql | registered_automatic | True | True | 33c687629eae15da672c8b561291a0a5bf161bdf4a204c1be3a19526a6774385 |
| 147 | 20260827120000 | 20260827120000_exp06_juridico360_integrado.sql | registered_automatic | True | True | 8c0614359146b95bec5707a077423d507d848755244859add5cb1042dca6416c |
| 148 | 20260827150000 | 20260827150000_exp09_sst360.sql | registered_automatic | True | True | ae14c7f27643124d4b3543c701c9f218113f596fc70289746ffdd98c0e6a0810 |
| 149 | 20260828100000 | 20260828100000_exp23_energia360.sql | registered_automatic | True | True | f4dc697da5b0583387ab7e70a51690aa1de71fe65eb9d9be7d36c3f367677e0e |
| 150 | 20260828120000 | 20260828120000_exp24_royalties360.sql | registered_automatic | True | True | 4fcb530a70993cf564ad436cafeb626ab84c291c18a7b425dce0c455ce1baade |
| 151 | 20260829100000 | 20260829100000_exp13_saneamento360_sigcos.sql | registered_automatic | True | True | f15578a8a66c3b957280f8bef5549cf08feea44bb9fe78d295160f2cb9b3eb8b |
| 152 | 20260829120000 | 20260829120000_exp11_saude360_acs360_func11.sql | registered_automatic | True | True | e7def5cdb39deecba61f6587b67a64eae4386d5176f9ea42cff3d2572aa53fda |
| 153 | 20260829140000 | 20260829140000_exp25_ged360_inovaged.sql | registered_automatic | True | True | bad5e86f75925fb350a27b492fc7573b5058cd1753aaee6b14e7541db7a09bf3 |
| 154 | 20260829160000 | 20260829160000_corr25_ged360_integridade_lgpd.sql | registered_automatic | True | True | 2cdecc8c7242414505418407aafcc4cc128fd82ac83de252f3ffbe4e9f8aa324 |
| 155 | 20260829180000 | 20260829180000_rc50_81_homologacao_enterprise.sql | registered_automatic | True | True | db5ac25de448849f4a1b51df99f9b6e8b5b1186109dcbc4616f71ee0e400f3bf |
| 156 | 20260831120000 | 20260831120000_rc50_82_qualidade_sistema.sql | registered_automatic | True | True | 300d2d0763a30cb1fa2db64b3cee6ee6f93268a686dba098874bb554506da369 |
| 157 | 20260831180000 | 20260831180000_rc50_83_central_executiva_360.sql | registered_automatic | True | True | 07bdec3d5c9d85b1fc174e06102173519434a20571db378518af23a76d73963c |
| 158 | 20260831210000 | 20260831210000_rc50_84_modulos_estruturantes_multiesfera.sql | registered_automatic | True | True | cce1f72a6f44bce40382c08711da547eb7acf90be595b8de53ea22ede81203c9 |
| 159 | 20260831230000 | 20260831230000_rc50_85_compras_contratos_multiesfera.sql | registered_automatic | True | True | bf9bc005b5455e4cb086d41c72e7165af7cf1b725101e8d7bfd073b67f666c39 |
| 160 | 20260901000000 | 20260901000000_rc50_86_financeiro_orcamento_contabilidade_multiesfera.sql | registered_automatic | True | True | 376ba32ef0f38254dc0a0b1f0e3f88f544709c2d1fbe522b645b4cd27c264a16 |
| 161 | 20260901030000 | 20260901030000_rc50_87_saas_iam_tributos_multiesfera.sql | registered_automatic | True | True | 324d4bac90745f2b87238701aa750ad52d821449cfcfc73afbc76c4d25cc3487 |
| 162 | 20260901060000 | 20260901060000_rc50_88_rh360_folha_portal.sql | registered_automatic | True | True | 40d72efc3d4ff7db7bfa83af2b5b660659fe369b9eb6e45c7910653dc53c4a6a |
| 163 | 20260901090000 | 20260901090000_rc50_89_patrimonio_almoxarifado_frotas.sql | registered_automatic | True | True | 8de4e5af823eaa015b509a5c1927f8310b0c4a378eafca88fc1db7ea1a04b7a9 |
| 164 | 20260901120000 | 20260901120000_rc50_90_saude360_ubs_acs_regulacao.sql | registered_automatic | True | True | 79994e71582be89f98a632ba41e58a2a28f566d819376896550761dc95b579c0 |
| 165 | 20260901150000 | 20260901150000_rc50_91_assistencia_social360_suas.sql | registered_automatic | True | True | b7fae30e648b53ba82ebc71a8f9778ed0f9942d8cf054dc8a1c06a4b80c938c1 |
| 166 | 20260901180000 | 20260901180000_rc50_92_saneamento_meio_ambiente360.sql | registered_automatic | True | True | 8b0505dd904536f0d6864b8488fd4964e1790367fd9b1b318ceff4fdc441d282 |
| 167 | 20260901210000 | 20260901210000_rc50_95_base_restauravel.sql | registered_automatic | True | True | a69325e24a44dc1f48ca06a3e05874a972a2674359e9f61dbdf4b298eb1c15cd |
| 168 | 20260902000000 | 20260902000000_rc50_98_ged_workflow_branding_logo.sql | registered_automatic | True | True | f5a4370460581e6b3e78a1f0ab30e2487fc272e66c53634be46541d470e8b007 |
| 169 | 20260902010000 | 20260902010000_corr_compras_checksum_schema.sql | registered_excluded | False | False | 19a074dcde84a8057f38e38808452d4bfb6e0551f5ff0975e73811e8edc65e0f |
| 170 | 20260903100000 | 20260903100000_corr_postconditions_permissions_schema.sql | registered_automatic | True | True | 7da07d5f1aad97993d7ae43ae9cf2f9ea0e93e6d728cf8ef4383d6ee0ea7c8ed |
| 171 | 20260903130000 | 20260903130000_corr_licitapro_postconditions_schema.sql | registered_excluded | False | False | c237332d2878958e55a6a535208c77ded73521be5c805a52e06a01493b347a6b |
| 172 | 20260903173000 | 20260903173000_corr_licitapro_schema_history.sql | registered_automatic | True | True | ea2e34fb9909c44f2e2a66a9e80de887d31717ca9fe20bbed0a714242bfbc528 |
| 173 | 20260903230000 | 20260903230000_corr_compras_bigint_postconditions_final.sql | registered_automatic | True | True | 2d132eb414ccd2352b302a6d50206f735f2995cc73f02e10bc6eacb3991f0f70 |
| 174 | 20260908120000 | 20260908120000_evolucao_saas_industria_360.sql | registered_automatic | True | True | c7e27f2942419883e6b7123c5ed16e0b574c520a6deabe4ce71b4f375ba272e7 |
|  | 20260813120000 | 20260813120000_rc50_24_educacao_rh_folha_produto_core.sql | necessaria | False | False | 0fb6232a00fbad99380179bad080a3fd7c3c50beb1d186dcce4938665a95aa0a |
|  | 20260813120000 | 20260813120000_rc50_27_operacoes_inteligentes.sql | necessaria | False | False | c9747b7f93c1e376cc644219f5e78b1a299d9b94477255422c318163e258f6cb |
|  | 20260813160000 | 20260813160000_rc50_25_educacao_fluxo_diario.sql | necessaria | False | False | c3d778299e5d0a9c3c037a532c7bc18e4a73dc599930bd74c21da9d3e8ad9b7e |
|  | 20260813190000 | 20260813190000_rc50_28_gestao_avancada.sql | necessaria | False | False | 8a4aa20a054942b3f8df29d03f27a5751a8dc4aaf2a39045f186583a543a31de |
|  | 20260813193000 | 20260813193000_rc50_29_parametros_modulos.sql | necessaria | False | False | 755a77f67431091f9e83a86703816e5d8f50c20c5eae6091fd1e3fe765fe6f2d |
|  | 20260813195500 | 20260813195500_rc50_30_governanca_executiva.sql | necessaria | False | False | c0233bd72365fc085975efd6765ce0368f2608868eff417d8df58b21c229effc |
|  | 20260813210000 | 20260813210000_rc50_31_bloco1_fechamento.sql | necessaria | False | False | 7501e0ec38ec97dc89c7109cde137e07d6c32ba1294bfaa69fbb6890ed7dafee |
|  | 20260815120000 | 20260815120000_rc50_37_compras_licitacoes_bloco6_core.sql | incompativel | False | False | 8ffead4bbb888ca24417581632c71b3fe5a504fde502964e3b993ea994f196c4 |
|  | 20260815121000 | 20260815121000_rc50_37_contratos_bloco6_core.sql | incompativel | False | False | d056111de82837602f315eec9912688d715764ae57b8f6c253a9b8f618a9c075 |
|  | 20260815122000 | 20260815122000_rc50_37_almoxarifado_patrimonio_bloco6_core.sql | incompativel | False | False | 4c197288ee0c486bf0fe6f3404c9e3eedb92bea86daba7d0022dadfcc9c3df97 |

## Falhas estáticas


Campos completos, dependências declaradas, compatibilidades, knownChecksums e pós-condições estão em inventory.json. Ausência de dependência declarada não comprova independência SQL.
