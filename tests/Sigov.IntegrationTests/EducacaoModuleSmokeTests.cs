using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class EducacaoModuleSmokeTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Migration_Cria_Tabelas_E_Views_No_Schema_Sigov_Com_Tenant()
    {
        var sql = File.ReadAllText(Path.Combine(Root, "database/postgres/migrations/021_educacao_base.sql")).ToLowerInvariant();
        foreach (var table in new[] { "escola", "ano_letivo", "curso", "serie_ano", "turma", "aluno", "responsavel_aluno", "matricula", "professor", "professor_turma", "diario_frequencia", "avaliacao", "nota", "pre_matricula_inscricao", "educacenso_registro", "portal_educacao_acesso", "educacao_evento" }) sql.Should().Contain("sigov." + table);
        sql.Should().Contain("tenant_id bigint not null references sigov.tenant(id)");
        sql.Should().Contain("entidade_id bigint not null references sigov.entidade(id)");
        sql.Should().Contain("create or replace view sigov.vw_educacao_dashboard");
        sql.Should().NotContain("create schema educacao");
        sql.Should().NotContain("create schema educ");
    }

    [Fact]
    public void Repository_Usa_Dapper_Parametrizado_Tenant_Entidade_Auditoria_Outbox()
    {
        var code = File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Educacao/EducacaoRepository.cs"));
        code.Should().Contain("tenant_id = @TenantId");
        code.Should().Contain("entidade_id = @EntidadeId");
        code.Should().Contain("sigov.educacao_evento");
        code.Should().Contain("cast(@DadosSensiveisJson as jsonb)");
        code.Should().Contain("vagas_ocupadas = vagas_ocupadas + 1");
        code.IndexOf("vagas_ocupadas = vagas_ocupadas + 1", StringComparison.Ordinal)
            .Should().BeLessThan(code.IndexOf("var sql = InsertSql(recurso)", StringComparison.Ordinal),
                "a vaga deve ser reservada atomicamente antes de inserir a matrícula");
        code.Should().Contain("reservadas != 1")
            .And.Contain("a.tenant_id = t.tenant_id")
            .And.Contain("t.escola_id = @EscolaId")
            .And.Contain("@DataAula between greatest(m.data_matricula,coalesce(m.data_enturmacao,m.data_matricula),l.data_inicio) and l.data_fim")
            .And.Contain("pt.professor_id=@ProfessorId")
            .And.Contain("upper(pt.componente_curricular)=upper(@ComponenteCurricular)")
            .And.Contain("l.status <> 'ENCERRADO'")
            .And.Contain("a.status='ABERTA' and @Valor between 0 and a.valor_maximo")
            .And.Contain("m.data_matricula<=a.data_avaliacao")
            .And.Contain("ExecuteScalarAsync<long?>")
            .And.Contain("Frequência rejeitada: aluno sem matrícula elegível");
        // Regressão S6: cast Postgres com :: no SELECT dispara o detector de tokens legados do Dapper
        // (o primeiro colon de :: fica isolado apos ']' ), desliga o filtro de parâmetros e a reflexão sobre
        // o Dictionary falha no membro Comparer (NotSupportedException) no QueryMultiple de /api/educacao/alunos.
        // O GridReader tambem exige correspondencia exata coluna x construtor: sem coluna extra no select de aluno.
        code.Should().NotContain("array[]::json[]")
            .And.Contain("\"aluno\" => \"id, pessoa_id as PessoaId, codigo_aluno as CodigoAluno, nis, cartao_sus as CartaoSus, necessidade_especial as NecessidadeEspecial, situacao\",");
    }

    [Fact]
    public void Ingresso_E_Vagas_Preserva_Concorrencia_Origem_E_Enturmacao_Opcional()
    {
        var repository = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "EducacaoRepository.cs"));
        var migration = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20260916210000_educacao_ingresso_vagas.sql"));

        repository.Should().Contain("pg_advisory_xact_lock")
            .And.Contain("versao=@Versao")
            .And.Contain("valida_ate>now()")
            .And.Contain("fn_educacao_converter_oferta");
        repository.Should().Contain("pg_advisory_xact_lock(hashtextextended")
            .And.Contain("nextval('sigov.educacao_numero_seq')")
            .And.Contain("ExecuteScalarAsync<string?>")
            .And.Contain("string.IsNullOrWhiteSpace(numero)")
            .And.Contain("A sequência obrigatória de Educação não produziu um número válido.")
            .And.Contain("Somente matrícula ativa ou confirmada pode ser cancelada.")
            .And.NotContain("DateTime.UtcNow.Ticks % 1000000")
            .And.Contain("'PRE-' || @AnoLetivo");
        migration.Should().Contain("ux_matricula_origem_prematricula")
            .And.Contain("alter column turma_id drop not null")
            .And.Contain("vagas_ocupadas<capacidade")
            .And.Contain("situacao='PENDENTE'");
    }

    [Fact]
    public void Boletim_Nao_Inventa_Regra_De_Aprovacao_Sem_Politica()
    {
        var code = File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Educacao/EducacaoRepository.cs"));
        code.Should().Contain("new BoletimResponse(alunoId, null, itens)")
            .And.Contain("'NAO_LANCADO'")
            .And.NotContain("a.valor_maximo * 0.6")
            .And.NotContain("notas.Average()");
    }

    [Fact]
    public void Api_E_Web_Exposicoes_Estruturais_Existem()
    {
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoControllers.cs")).Should().Contain("api/educacao/escolas").And.Contain("api/educacao/dashboard").And.Contain("api/educacao/export").And.Contain("api/educacao/rematriculas").And.Contain("NotFound(ApiResponse");
        File.Exists(Path.Combine(Root, "src/Sigov.Web/Views/Educacao/Dashboard.cshtml")).Should().BeTrue();
        File.Exists(Path.Combine(Root, "src/Sigov.Web/wwwroot/js/modules/educacao.dashboard.js")).Should().BeTrue();
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Web/Views/Educacao/MatriculaDetalhe.cshtml"))
            .Should().Contain("Para que serve:").And.Contain("Próximo passo:").And.Contain("data-matricula-id");
        File.ReadAllText(Path.Combine(Root, "database/postgres/migrations/20260922160000_educacao_transicao_ano_letivo.sql"))
            .Should().Contain("origem_matricula_id").And.Contain("fn_educacao_confirmar_rematricula")
            .And.Contain("for update").And.Contain("vagas_ocupadas>=v_t.capacidade")
            .And.Contain("request_hash").And.Contain("educacao_resultado_final");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Web/Views/Educacao/Rematriculas.cshtml"))
            .Should().Contain("Prévia sem efeitos acadêmicos").And.Contain("6. Resultado").And.Contain("itens marcados");
    }

    [Fact]
    public void EducacaoAvancada_Correcoes_Comprovadas_Dashboard_Csv_Schema_Auditoria()
    {
        var code = File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Educacao/Avancada/EducacaoAvancadaRepository.cs"));

        // #1 Dashboard: contagens sobre o conjunto completo autorizado (aggregate FILTER), nao sobre 6 itens recentes
        code.Should().Contain("count(1) filter (where status in ('ATIVA','ATIVO','PUBLICADO','DISPONIVEL','ABERTO'))")
            .And.Contain("order by id desc limit 6");

        // #2/#3 CSV: exportacao integral (sem teto de 100), escape/protecao de formula via helper, BOM p/ planilha pt-BR
        code.Should().Contain("EducacaoCsv.Campo(x.Codigo)")
            .And.Contain("Encoding.UTF8.GetPreamble().Concat")
            .And.NotContain("EducacaoAvancadaFiltro(Tamanho:100)");

        // #5 Schema ausente (C2S2 BR-2.6): 404 RECURSO_INDISPONIVEL uniforme em listar/criar/status/dashboard (era 200 + diagnóstico)
        code.Should().Contain("Math.Clamp(f.Tamanho,1,100);if(!await _inspector.TableExistsAsync(\"sigov\",recurso,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(recurso)")
            .And.NotContain("Indisponivel(p,t,");

        // #7 Auditoria: transicao de status registra origem, destino e motivo sem sobrescrever o historico
        code.Should().Contain("jsonb_build_array(jsonb_build_object('acao','STATUS','de',@De,'para',@Status,'motivo'")
            .And.Contain("select status,coalesce(entidade_id,-1) as LinhaEntidade,updated_at as LinhaUpdatedAt from sigov.{recurso} where tenant_id=@TenantId and id=@Id and is_deleted=false");

        // #4 Maquina de estados + guarda de versao: transicao validada com recusa nomeada, registro ausente
        // e tabela ausente viram falhas explicitas, e a escrita e otimista (coalesce(updated_at,created_at))
        code.Should().Contain("EducacaoStatusMaquina.TransicaoPermitida(linhaDe,alvo)")
            .And.Contain("TransicaoDeStatusInvalidaException")
            .And.Contain("RegistroNaoEncontradoException")
            .And.Contain("RecursoNaoDisponivelException")
            .And.Contain("ConflitoDeVersaoEducacaoException")
            .And.Contain("coalesce(updated_at,created_at)=@VersaoAtual");

        // #6 Escopo institucional: escola e ano letivo validados contra tenant/entidade do contexto antes do insert;
        // comando sobre registro de outra entidade falha com CONTEXTO_INSTITUCIONAL_DIVERGENTE antes de gravar
        code.Should().Contain("await ValidarEscopoInstitucionalAsync(x.TenantId,x.EntidadeId,r.EscolaId,r.AnoLetivo,ct)")
            .And.Contain("select entidade_id from sigov.escola where id=@Id and tenant_id=@TenantId and is_deleted=false")
            .And.Contain("select exists(select 1 from sigov.ano_letivo where tenant_id=@TenantId and ano=@AnoLetivo")
            .And.Contain("EscolaInexistenteException")
            .And.Contain("EscolaForaDoEscopoException")
            .And.Contain("AnoLetivoInexistenteException")
            .And.Contain("ContextoInstitucionalDivergenteException");
    }

    [Fact]
    public void EducacaoAvancada_Ciclo2Sprint2_MaquinaComEfeitos_Bypass_Tempo_E_Schema_Uniformes()
    {
        var repo = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "Avancada", "EducacaoAvancadaRepository.cs"));
        var maquina = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Application", "Educacao", "Avancada", "EducacaoStatusMaquina.cs"));
        var baseController = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "Controllers", "EducacaoAvancadaControllerBase.cs"));
        var filtro = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "Filters", "EducacaoFalhasNomeadasFiltro.cs"));
        var clock = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Common", "DateTimeProvider.cs"));

        // BR-2.1/BR-2.4: origem desconhecida vira LEGADO_NAO_REGULARIZADO ('_ => true' eliminado);
        // efeitos fora do registro sao declarados em matriz e a guarda executavel chama a matriz
        maquina.Should().Contain("LEGADO_NAO_REGULARIZADO")
            .And.NotContain("_ => true");
        repo.Should().Contain("EducacaoEfeitosTransicao.ExigeCompensacaoAntesDeGravar(recurso,alvo)")
            .And.Contain("RegularizacaoLegadaExigeJustificativaException(linhaDe)");

        // BR-2.2: mapa de recursos com invariante tipada (criacao generica => 409 COMANDO_TIPADO_OBRIGATORIO)
        // e porta legado de estoque restrita; recurso livre segue genérico
        repo.Should().Contain("RecursosComComandoTipado")
            .And.Contain("\"educacao_merenda_movimento\"]=\"POST /api/educacao/merenda/estoques/lancar\"")
            .And.Contain("\"educacao_merenda_distribuicao\"]=\"POST /api/educacao/merenda/distribuicoes/confirmar\"")
            .And.Contain("\"educacao_biblioteca_emprestimo\"]=\"POST /api/educacao/biblioteca/emprestimos/lancar\"")
            .And.Contain("\"educacao_biblioteca_reserva\"]=\"POST /api/educacao/biblioteca/reservas/lancar\"")
            .And.Contain("\"educacao_indicador_valor\"]=\"POST /api/educacao/indicadores/valores/lancar\"")
            .And.Contain("\"educacao_fundeb_aplicacao\"]=\"POST /api/educacao/fundeb/aplicacoes/lancar\"")
            .And.Contain("PortaLegadaEstoqueAberta")
            .And.Contain("ComandoTipadoObrigatorioException");
        baseController.Should().Contain("catch (ComandoTipadoObrigatorioException ex)")
            .And.Contain("catch (RegularizacaoLegadaExigeJustificativaException ex)");

        // BR-2.3: registro já atualizado exige updatedAt — omitido/obsoleto vira CONFLITO_VERSAO antes do UPDATE
        repo.Should().Contain("if(de.LinhaUpdatedAt is not null && versaoAtual is null)throw new ConflitoDeVersaoEducacaoException(recurso,id)")
            .And.Contain("coalesce(updated_at,created_at)=@VersaoAtual");

        // BR-2.5: relógio único via DI (configurável); repositório avançado sem DateTime.Today próprio
        repo.Should().Contain("_clock.DataAtual")
            .And.NotContain("DateTime.Today");
        clock.Should().Contain("SIGOV_CLOCK_DATAFIXA")
            .And.Contain("public DateOnly DataAtual => _dataFixa ?? DateOnly.FromDateTime(DateTime.Today);");

        // BR-2.6: filtro de falhas nomeadas herdado na família de controllers — 404 idêntico em qualquer caminho
        baseController.Should().Contain("[TypeFilter(typeof(EducacaoFalhasNomeadasFiltro))]");
        filtro.Should().Contain("RecursoNaoDisponivelException => StatusCodes.Status404NotFound");
    }

    [Fact]
    public void EducacaoCicloEscolar_Sprint3_UnicidadeOferta_E_CorrecaoAuditadaDeFrequencia()
    {
        // Sprint 3 banco: indice unico parcial aluno x turma (oferta) por migration corretiva idempotente,
        // com falha explicita se houver duplicados vigentes preexistentes (regra 13); coluna situacao
        // restaurada no contrato de frequencia e indice estreito legado (sem componente) removido.
        var migration = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261011120000_evo_c1s3_educacao_matricula_unicidade_oferta.sql"));
        migration.Should().Contain("create unique index if not exists ux_matricula_tenant_aluno_turma_vigencia")
            .And.Contain("status in ('ATIVA', 'CONFIRMADA')")
            .And.Contain("raise exception")
            .And.Contain("add column if not exists situacao varchar(20) not null default 'PRESENTE'")
            .And.Contain("drop index if exists sigov.ux_diario_frequencia_turma_aluno_data");

        // Sprint 3 correção auditada: trava a linha (for update), bloqueia quando o diário de classe da
        // turma está FECHADO, grava situação/presença/justificativa e registra FrequenciaCorrigida c/ de+para.
        var repo = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "EducacaoRepository.cs"));
        repo.Should().Contain("where id=@Id and tenant_id=@TenantId and entidade_id=@EntidadeId and not is_deleted for update")
            .And.Contain("d.status='FECHADO'")
            .And.Contain("update sigov.diario_frequencia set situacao=@Status, presente=@Presente, justificativa=@Justificativa")
            .And.Contain("\"FrequenciaCorrigida\"");

        // Permissão canônica (banco é autoridade, regra 11): frequencia/justificar; rota pública de correção.
        var service = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Application", "Educacao", "EducacaoServices.cs"));
        service.Should().Contain("GuardAsync(\"frequencia\", \"justificar\"");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoControllers.cs")).Should().Contain("{id:long}/correcao");

        // Regra 7: migration registrada no manifest com pós-condição nos artefatos sincronizados.
        var manifest = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "manifest.json"));
        manifest.Should().Contain("20261011120000_evo_c1s3_educacao_matricula_unicidade_oferta.sql")
            .And.Contain("ux_matricula_tenant_aluno_turma_vigencia");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "sigov.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    [Fact]
    public void EducacaoNotasBoletim_Sprint4_PublicacaoHistoricoEscolar_Versionada()
    {
        // Sprint 4 banco: permissão granular educacao.historico_escolar.publicar com concessão aos
        // perfis administrativos (padrão RC50.29; banco é autoridade, regra 11) e pós-condição explícita.
        var migration = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261011130000_evo_c1s4_educacao_historico_escolar_publicar.sql"));
        migration.Should().Contain("'educacao.historico_escolar.publicar'")
            .And.Contain("on conflict (chave) do update set ativo=true,is_deleted=false")
            .And.Contain("ADMINISTRADOR_TENANT")
            .And.Contain("S4-POSTCOND");

        // Publicação agrega notas das avaliações do diário (dados->Alunos por componente curricular)
        // e frequência do diário, com união por componente e histórico versionado (dados->versao).
        var repo = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "Bloco3", "EducacaoBloco3Repository.cs"));
        repo.Should().Contain("jsonb_array_elements(coalesce(a.dados->'Alunos','[]'::jsonb))")
            .And.Contain("(x->>'AlunoId')::bigint=@AlunoId")
            .And.Contain("full outer join fr on fr.comp=av.comp")
            .And.Contain("jsonb_set(coalesce(dados,'{}'::jsonb),'{versao}'")
            .And.Contain("Sem avaliações ou frequência registrada para as turmas da matrícula.")
            .And.Contain("Matrícula fora do escopo institucional do contexto autenticado.");

        // Guarda de autorização canônica + rota pública de publicação (admin da secretaria).
        var service = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Application", "Educacao", "Bloco3", "EducacaoBloco3Service.cs"));
        service.Should().Contain("Pode(\"educacao.historico_escolar.publicar\")")
            .And.Contain("Permissão para publicar histórico escolar é obrigatória.");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoBloco3Controllers.cs"))
            .Should().Contain("admin/historico-escolar/{alunoId:long}/publicar");

        // Avaliação passa a carregar componente curricular e notas por aluno (dados jsonb versionado).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/Bloco3/EducacaoBloco3Contracts.cs"))
            .Should().Contain("string? ComponenteCurricular = null")
            .And.Contain("IReadOnlyCollection<EducacaoDiarioAvaliacaoItemRequest>? Alunos = null")
            .And.Contain("public sealed record EducacaoDiarioAvaliacaoItemRequest(long AlunoId, decimal Nota)")
            .And.Contain("Task<Result<EducacaoPublicarHistoricoEscolarDto>> PublicarHistoricoEscolarAsync(long alunoId, EducacaoPublicarHistoricoEscolarRequest request, CancellationToken ct)");

        // Regra 7: migration registrada no manifest com pós-condição sobre permissão + concessão.
        var manifest = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "manifest.json"));
        manifest.Should().Contain("20261011130000_evo_c1s4_educacao_historico_escolar_publicar.sql")
            .And.Contain("p.chave='educacao.historico_escolar.publicar' and pp.ativo and not pp.is_deleted and pp.efeito='PERMITIR'");
    }

    [Fact]
    public void MerendaBase_Sprint5_CardapioComProdutos_UnicidadePeriodo()
    {
        // Sprint 5 banco: unicidade de período vigente de cardápio por escola × data de início
        // (estados terminais liberam o período), com pós-condição explícita (regra 13).
        var migration = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261011140000_evo_c1s5_merenda_cardapio_unicidade_periodo.sql"));
        migration.Should().Contain("create unique index if not exists ux_merenda_cardapio_tenant_escola_inicio")
            .And.Contain("on sigov.educacao_merenda_cardapio (tenant_id, escola_id, data_inicio)")
            .And.Contain("status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')")
            .And.Contain("S5-POSTCOND");

        // Comando tipado: escopo institucional da escola + produtos válidos do tenant + checagem
        // de período antes do insert + itens transacionais referenciando o cardápio (regra 13).
        var repo = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "Avancada", "EducacaoAvancadaRepository.cs"));
        repo.Should().Contain("public async Task<long> CriarCardapioComItensAsync(EducacaoAvancadaContext x,EducacaoMerendaCriarCardapioRequest r,CancellationToken ct)")
            .And.Contain("throw new CardapioProdutoDuplicadoException(p)")
            .And.Contain("throw new CardapioProdutoInexistenteException(p)")
            .And.Contain("throw new CardapioPeriodoExistenteException(r.EscolaId,r.Inicio)")
            .And.Contain("educacao_merenda_cardapio_item(tenant_id,entidade_id,escola_id,referencia_id,produto_id");

        // Contrato e rota públicos (cadastro base de merenda multi-esfera, regra 24).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs"))
            .Should().Contain("Task<long> CriarCardapioComItensAsync(EducacaoAvancadaContext contexto,EducacaoMerendaCriarCardapioRequest request,CancellationToken ct)");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoMerendaController.cs"))
            .Should().Contain("[HttpPost(\"cardapios/completo\")]");

        // Regra 7: migration registrada no manifest com pós-condição sobre o índice.
        var manifest = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "manifest.json"));
        manifest.Should().Contain("20261011140000_evo_c1s5_merenda_cardapio_unicidade_periodo.sql")
            .And.Contain("indexname='ux_merenda_cardapio_tenant_escola_inicio'");
    }

    [Fact]
    public void MerendaOperacao_Sprint6_DistribuicaoComConsumoEstoque_UnicidadeDia()
    {
        // Sprint 6 banco: uma distribuição vigente por cardápio × data de atendimento
        // (estados terminais liberam o dia), com pós-condição explícita (regra 13).
        var migration = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261011150000_evo_c1s6_merenda_distribuicao_consumo_estoque.sql"));
        migration.Should().Contain("create unique index if not exists ux_merenda_distribuicao_tenant_cardapio_data")
            .And.Contain("on sigov.educacao_merenda_distribuicao (tenant_id, referencia_id, competencia)")
            .And.Contain("status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')")
            .And.Contain("S6-POSTCOND");

        // Operações tipadas: registro de estoque por produto e distribuição com
        // cardápio ATIVO no período, itens restritos ao menu, lock do estoque em ordem
        // determinística, movimentos de saída + baixa de saldo na mesma transação.
        var repo = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "Avancada", "EducacaoAvancadaRepository.cs"));
        repo.Should().Contain("public async Task<long> RegistrarEstoqueAsync(EducacaoAvancadaContext x,EducacaoMerendaRegistrarEstoqueRequest r,CancellationToken ct)")
            .And.Contain("throw new MerendaProdutoInexistenteException(r.ProdutoId)")
            .And.Contain("public async Task<long> DistribuirRefeicaoAsync(EducacaoAvancadaContext x,EducacaoMerendaDistribuirRefeicaoRequest r,CancellationToken ct)")
            .And.Contain("order by id limit 1 for update")
            .And.Contain("throw new EstoqueInsuficienteException(it.ProdutoId,disponivel,it.Quantidade)")
            .And.Contain("throw new DistribuicaoDiaExistenteException(r.CardapioId,r.Data)")
            .And.Contain("insert into sigov.educacao_merenda_movimento");

        // Contratos e rotas públicas (operação de merenda multi-esfera, regra 24).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs"))
            .Should().Contain("Task<long> RegistrarEstoqueAsync(EducacaoAvancadaContext contexto,EducacaoMerendaRegistrarEstoqueRequest request,CancellationToken ct)")
            .And.Contain("Task<long> DistribuirRefeicaoAsync(EducacaoAvancadaContext contexto,EducacaoMerendaDistribuirRefeicaoRequest request,CancellationToken ct)");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoMerendaController.cs"))
            .Should().Contain("[HttpPost(\"estoques\")]")
            .And.Contain("[HttpPost(\"distribuicoes/confirmar\")]");

        // Regra 7: migration registrada no manifest com pós-condição sobre o índice.
        var manifest = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "manifest.json"));
        manifest.Should().Contain("20261011150000_evo_c1s6_merenda_distribuicao_consumo_estoque.sql")
            .And.Contain("indexname='ux_merenda_distribuicao_tenant_cardapio_data'");
    }

    [Fact]
    public void BibliotecaCirculacao_Sprint7_EmprestimoDevolucaoReserva_UnicidadeVigenciaExemplar()
    {
        // Sprint 7 banco: um emprestimo vigente e uma reserva vigente por exemplar × tenant
        // (estados terminais liberam), com pós-condição explícita (regra 13).
        var migration = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261011160000_evo_c1s7_biblioteca_circulacao.sql"));
        migration.Should().Contain("create unique index if not exists ux_biblioteca_emprestimo_tenant_exemplar_vigencia")
            .And.Contain("create unique index if not exists ux_biblioteca_reserva_tenant_exemplar_vigencia")
            .And.Contain("status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')")
            .And.Contain("S7-POSTCOND");

        // Operações tipadas: empréstimo com tomador único (aluno/servidor do tenant),
        // prazo >= data de lançamento, devolução encerra o empréstimo e registra a
        // devolução na mesma transação; reserva com fila de tamanho 1 por exemplar.
        var repo = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "Avancada", "EducacaoAvancadaRepository.cs"));
        repo.Should().Contain("public async Task<long> CriarEmprestimoAsync(EducacaoAvancadaContext x,EducacaoBibliotecaCriarEmprestimoRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> DevolverEmprestimoAsync(EducacaoAvancadaContext x,long emprestimoId,EducacaoBibliotecaDevolverRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> CriarReservaAsync(EducacaoAvancadaContext x,EducacaoBibliotecaCriarReservaRequest r,CancellationToken ct)")
            .And.Contain("throw new ExemplarIndisponivelException(r.ExemplarId)")
            .And.Contain("throw new EmprestimoJaDevolvidoException(emprestimoId")
            .And.Contain("insert into sigov.educacao_biblioteca_devolucao");

        // Contratos e rotas públicas (circulação de biblioteca multi-esfera, regra 24).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs"))
            .Should().Contain("Task<long> CriarEmprestimoAsync(EducacaoAvancadaContext contexto,EducacaoBibliotecaCriarEmprestimoRequest request,CancellationToken ct)")
            .And.Contain("Task<long> DevolverEmprestimoAsync(EducacaoAvancadaContext contexto,long emprestimoId,EducacaoBibliotecaDevolverRequest request,CancellationToken ct)")
            .And.Contain("Task<long> CriarReservaAsync(EducacaoAvancadaContext contexto,EducacaoBibliotecaCriarReservaRequest request,CancellationToken ct)");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoBibliotecaController.cs"))
            .Should().Contain("[HttpPost(\"emprestimos/lancar\")]")
            .And.Contain("[HttpPost(\"emprestimos/{id:long}/devolver\")]")
            .And.Contain("[HttpPost(\"reservas/lancar\")]");

        // Regra 7: migration registrada no manifest com pós-condição sobre os índices.
        var manifest = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "manifest.json"));
        manifest.Should().Contain("20261011160000_evo_c1s7_biblioteca_circulacao.sql")
            .And.Contain("indexname='ux_biblioteca_emprestimo_tenant_exemplar_vigencia'");
    }

    [Fact]
    public void TransporteEscolar_Sprint8_RotaPontoAluno_UnicidadeOrdemEVinculo()
    {
        // Sprint 8 banco: uma ordem vigente por ponto × rota × tenant + reuso do índice
        // rc50_48 (aluno × rota × turno) como backstop do vínculo, com pós-condição explícita (regra 13).
        var migration = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261011170000_evo_c1s8_transporte_ponto_ordem.sql"));
        migration.Should().Contain("create unique index if not exists ux_transporte_ponto_tenant_rota_ordem")
            .And.Contain("status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')")
            .And.Contain("S8-POSTCOND")
            .And.Contain("ux_educacao_transporte_aluno_rota_turno");

        // Operações tipadas: rota com exatamente uma de escola/região (multi-esfera, regra 24),
        // ponto com ordem única por rota vigente e vínculo de aluno com turno herdado da rota.
        var repo = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "Avancada", "EducacaoAvancadaRepository.cs"));
        repo.Should().Contain("public async Task<long> LancarRotaAsync(EducacaoAvancadaContext x,EducacaoTransporteLancarRotaRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> LancarPontoAsync(EducacaoAvancadaContext x,long rotaId,EducacaoTransporteLancarPontoRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> VincularAlunoAsync(EducacaoAvancadaContext x,long rotaId,EducacaoTransporteVincularAlunoRequest r,CancellationToken ct)")
            .And.Contain("throw new TransportePontoOrdemExistenteException(rotaId,r.Ordem)")
            .And.Contain("throw new TransporteAlunoJaVinculadoARotaException(r.AlunoId,rotaId")
            .And.Contain("insert into sigov.educacao_transporte_rota_ponto");

        // Contratos e rotas públicas (transporte escolar multi-esfera, regra 24).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs"))
            .Should().Contain("Task<long> LancarRotaAsync(EducacaoAvancadaContext contexto,EducacaoTransporteLancarRotaRequest request,CancellationToken ct)")
            .And.Contain("Task<long> LancarPontoAsync(EducacaoAvancadaContext contexto,long rotaId,EducacaoTransporteLancarPontoRequest request,CancellationToken ct)")
            .And.Contain("Task<long> VincularAlunoAsync(EducacaoAvancadaContext contexto,long rotaId,EducacaoTransporteVincularAlunoRequest request,CancellationToken ct)")
            .And.Contain("record EducacaoTransporteDashboardDto(long RotasAtivas,long PontosAtivos,long AlunosVinculados,long EscolasAtendidas)");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoTransporteEscolarController.cs"))
            .Should().Contain("[HttpPost(\"rotas/lancar\")]")
            .And.Contain("[HttpPost(\"rotas/{id:long}/pontos/lancar\")]")
            .And.Contain("[HttpPost(\"rotas/{id:long}/alunos/vincular\")]")
            .And.Contain("EducacaoTransporteDashboardDto");

        // Regra 7: migration registrada no manifest com pós-condição sobre os índices.
        var manifest = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "manifest.json"));
        manifest.Should().Contain("20261011170000_evo_c1s8_transporte_ponto_ordem.sql")
            .And.Contain("indexname='ux_transporte_ponto_tenant_rota_ordem'");
    }

    [Fact]
    public void FundebCustos_Sprint9_ParametroReceitaAplicacao_UnicidadeEDashboard()
    {
        // Sprint 9 banco: um parâmetro e um centro por código vigente por tenant + uma
        // receita por competência/fonte vigente por tenant, com pós-condição explícita (regra 13).
        var migration = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261011180000_evo_c1s9_fundeb_custos_unicidade.sql"));
        migration.Should().Contain("create unique index if not exists ux_fundeb_parametro_tenant_codigo")
            .And.Contain("create unique index if not exists ux_fundeb_receita_tenant_competencia_fonte")
            .And.Contain("create unique index if not exists ux_custo_centro_tenant_codigo")
            .And.Contain("S9-POSTCOND");

        // Operações tipadas: parâmetro/receita/aplicação/centro de custo e dashboard dedicado
        // com taxa mínima de aplicação vinda do banco (multi-esfera, regra 24).
        var repo = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "Avancada", "EducacaoAvancadaRepository.cs"));
        repo.Should().Contain("public async Task<long> LancarParametroAsync(EducacaoAvancadaContext x,EducacaoFundebLancarParametroRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> LancarReceitaAsync(EducacaoAvancadaContext x,EducacaoFundebLancarReceitaRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> LancarAplicacaoAsync(EducacaoAvancadaContext x,EducacaoFundebLancarAplicacaoRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> LancarCentroCustoAsync(EducacaoAvancadaContext x,EducacaoCustoLancarCentroRequest r,CancellationToken ct)")
            .And.Contain("throw new FundebReceitaDuplicadaException(r.Competencia.ToString(\"yyyy-MM-dd\"),fonte)")
            .And.Contain("insert into sigov.educacao_fundeb_receita");

        File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs"))
            .Should().Contain("Task<long> LancarReceitaAsync(EducacaoAvancadaContext contexto,EducacaoFundebLancarReceitaRequest request,CancellationToken ct)")
            .And.Contain("Task<EducacaoIndicadoresDashboardDto> IndicadoresDashboardAsync(long tenantId,CancellationToken ct)")
            .And.Contain("record EducacaoIndicadoresDashboardDto(decimal ReceitasFundeb,decimal AplicacoesFundeb,decimal TaxaAplicacao,decimal? TaxaMinima,bool EmConformidade,decimal CustoPorAluno,long Inconsistencias)");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoIndicadoresController.cs"))
            .Should().Contain("[HttpPost(\"fundeb/receitas/lancar\")]")
            .And.Contain("[HttpPost(\"fundeb/aplicacoes/lancar\")]")
            .And.Contain("[HttpPost(\"custos/centros/lancar\")]")
            .And.Contain("EducacaoIndicadoresDashboardDto");

        // Regra 7: migration registrada no manifest com pós-condição sobre os índices.
        var manifest = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "manifest.json"));
        manifest.Should().Contain("20261011180000_evo_c1s9_fundeb_custos_unicidade.sql")
            .And.Contain("indexname='ux_fundeb_receita_tenant_competencia_fonte'");
    }

    [Fact]
    public void IndicadoresEducacenso_Sprint10_CatalogoValorProjecaoLote_UnicidadeEDashboard()
    {
        // Sprint 10 banco: um indicador por código vigente, um valor por indicador x ano,
        // uma projeção por escola x ano e um lote Educacenso por ano — todos por tenant,
        // com pós-condição explícita (regra 13).
        var migration = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261011190000_evo_c1s10_indicadores_educacenso_unicidade.sql"));
        migration.Should().Contain("create unique index if not exists ux_indicador_tenant_codigo")
            .And.Contain("create unique index if not exists ux_indicador_valor_tenant_ref_ano")
            .And.Contain("create unique index if not exists ux_projecao_matricula_tenant_escola_ano")
            .And.Contain("create unique index if not exists ux_educacenso_lote_tenant_ano")
            .And.Contain("S10-POSTCOND");

        // Operações tipadas: indicador/valor/projeção/lote/inconsistência e dashboard censo.
        var repo = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "Avancada", "EducacaoAvancadaRepository.cs"));
        repo.Should().Contain("public async Task<long> LancarIndicadorAsync(EducacaoAvancadaContext x,EducacaoIndicadorLancarRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> LancarIndicadorValorAsync(EducacaoAvancadaContext x,EducacaoIndicadorValorLancarRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> LancarProjecaoMatriculaAsync(EducacaoAvancadaContext x,EducacaoProjecaoMatriculaLancarRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> LancarEducacensoLoteAsync(EducacaoAvancadaContext x,EducacaoEducacensoLoteLancarRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> LancarEducacensoInconsistenciaAsync(EducacaoAvancadaContext x,EducacaoEducacensoInconsistenciaLancarRequest r,CancellationToken ct)")
            .And.Contain("throw new LoteEducacensoNaoVigenteException(r.LoteId,lote.Status ?? \"INDEFINIDO\")")
            .And.Contain("insert into sigov.educacao_educacenso_inconsistencia");

        File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs"))
            .Should().Contain("Task<long> LancarIndicadorValorAsync(EducacaoAvancadaContext contexto,EducacaoIndicadorValorLancarRequest request,CancellationToken ct)")
            .And.Contain("Task<EducacaoEducacensoDashboardDto> EducacensoDashboardAsync(long tenantId,CancellationToken ct)")
            .And.Contain("record EducacaoEducacensoDashboardDto(long LotesAtivos,long InconsistenciasAbertas,long GravidadeAlta,long Eventos,DateTime? UltimaSincronizacao=null)");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoIndicadoresController.cs"))
            .Should().Contain("[HttpPost(\"indicadores/valores/lancar\")]")
            .And.Contain("[HttpPost(\"educacenso/inconsistencias/lancar\")]")
            .And.Contain("[HttpGet(\"educacenso/dashboard\")]")
            .And.Contain("EducacaoEducacensoDashboardDto");

        // Regra 7: migration registrada no manifest com pós-condição sobre os índices.
        var manifest = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "manifest.json"));
        manifest.Should().Contain("20261011190000_evo_c1s10_indicadores_educacenso_unicidade.sql")
            .And.Contain("indexname='ux_projecao_matricula_tenant_escola_ano'");
    }

    [Fact]
    public void MerendaEstoque_Ciclo2Sprint1_LancamentoEstornoIdempotencia_EIntegridadeTransacional()
    {
        // Ciclo 2 Sprint 1 banco: coluna de idempotência + índices de unicidade de estorno e de
        // linha de estoque por tenant x produto, com pós-condição explícita (regra 13).
        var migration = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261012110000_evo_c2s1_merenda_estoque_idempotencia.sql"));
        migration.Should().Contain("add column if not exists chave_idempotencia text")
            .And.Contain("create unique index if not exists ux_merenda_movimento_tenant_chave")
            .And.Contain("create unique index if not exists ux_merenda_movimento_tenant_estorno_ref")
            .And.Contain("create unique index if not exists ux_merenda_estoque_tenant_produto")
            .And.Contain("C2S1-POSTCOND");

        // Comandos tipados: lançamento ABERTURA/ENTRADA/AJUSTE com movimento+saldo na mesma
        // transação, estorno de movimento e de distribuição com compensação única, revalidação
        // da distribuição dentro da transação e cancelamento genérico bloqueado com efeitos.
        var repo = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "Avancada", "EducacaoAvancadaRepository.cs"));
        repo.Should().Contain("public async Task<long> LancarEstoqueAsync(EducacaoAvancadaContext x,EducacaoMerendaEstoqueLancarRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> EstornarMovimentoAsync(EducacaoAvancadaContext x,long movimentoId,EducacaoMerendaEstornoRequest r,CancellationToken ct)")
            .And.Contain("public async Task<long> EstornarDistribuicaoAsync(EducacaoAvancadaContext x,long distribuicaoId,EducacaoMerendaEstornoRequest r,CancellationToken ct)")
            .And.Contain("throw new DistribuicaoEstornoObrigatorioException(id)")
            .And.Contain("SqlState is \"23505\"")
            .And.Contain("for update");

        File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs"))
            .Should().Contain("record EducacaoMerendaEstoqueLancarRequest(long ProdutoId,string Operacao,decimal Quantidade")
            .And.Contain("record EducacaoMerendaEstornoRequest(string Motivo)")
            .And.Contain("Task<long> LancarEstoqueAsync(EducacaoAvancadaContext contexto,EducacaoMerendaEstoqueLancarRequest request,CancellationToken ct)");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoMerendaController.cs"))
            .Should().Contain("[HttpPost(\"estoques/lancar\")]")
            .And.Contain("[HttpPost(\"distribuicoes/{id:long}/estornar\")]")
            .And.Contain("[HttpPost(\"movimentos/{id:long}/estornar\")]");

        // Regra 7: migration registrada no manifest com pós-condição sobre os artefatos.
        var manifest = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "manifest.json"));
        manifest.Should().Contain("20261012110000_evo_c2s1_merenda_estoque_idempotencia.sql")
            .And.Contain("indexname='ux_merenda_estoque_tenant_produto'");
    }

    [Fact]
    public void BibliotecaCirculacao_Ciclo2Sprint3_ParametroPorTenant_Renovacao_Ocorrencia_Inventario_E_Historico()
    {
        // Ciclo 2 Sprint 3 banco: prazo original + renovacoes no emprestimo, origem no acervo (BR-3.8),
        // inventario fisico com snapshot e desfecho por item, dedupe de devolucao por emprestimo,
        // indice de reserva que libera CONSUMIDA e parametros por tenant no catalogo EDUCACAO (regra 11),
        // com pos-condicao explicita (regra 13).
        var migration = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261012150000_evo_c2s3_biblioteca_circulacao_completa.sql"));
        migration.Should().Contain("add column if not exists prazo_original date")
            .And.Contain("add column if not exists renovacoes integer not null default 0")
            .And.Contain("add column if not exists origem varchar(40)")
            .And.Contain("create table if not exists sigov.educacao_biblioteca_inventario")
            .And.Contain("create table if not exists sigov.educacao_biblioteca_inventario_item")
            .And.Contain("ux_biblioteca_inventario_item_tenant_inv_exemplar")
            .And.Contain("ux_biblioteca_devolucao_tenant_emprestimo")
            .And.Contain("array['ENCERRADA','ENCERRADO','CANCELADA','CANCELADO','CONSUMIDA']")
            .And.Contain("('EDUCACAO','EMPRESTIMO_MAX_VIGENTES_TOMADOR'")
            .And.Contain("('EDUCACAO','RENOVACAO_MAX_POR_EMPRESTIMO'")
            .And.Contain("('EDUCACAO','RENOVACAO_DIAS_ESTENDIMENTO'")
            .And.Contain("C2S3-POSTCOND");

        // Operacoes tipadas sobre parametros por tenant: maximo de emprestimos vigentes por tomador (BR-3.1),
        // renovacao limitada e baseada no prazo original (BR-3.3), devolucao idempotente pela data efetiva
        // com transferencia automatica da reserva para novo emprestimo na mesma transacao (BR-3.2).
        var repo = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Educacao", "Avancada", "EducacaoAvancadaRepository.cs"));
        repo.Should().Contain("LireParametroEducaoAsync(c,tx,x.TenantId,\"EMPRESTIMO_MAX_VIGENTES_TOMADOR\",ct)")
            .And.Contain("coalesce(aluno_id,-1)=coalesce(@AlunoId,-1) and coalesce(servidor_id,-1)=coalesce(@ServidorId,-1)")
            .And.Contain("throw new EmprestimoTomadorLimiteExcedidoException(r.AlunoId,r.ServidorId,maxTomador)")
            .And.Contain("data_inicio,data_fim,prazo_original,renovacoes,status,dados")
            .And.Contain("select coalesce(min(id),-1) from sigov.educacao_biblioteca_devolucao where tenant_id=@TenantId and referencia_id=@Id and tipo='DEVOLUCAO'")
            .And.Contain("if(efetiva>hoje)throw new DataDevolucaoInvalidaException(efetiva,hoje)")
            .And.Contain("'TRANSFERIR_RESERVA_EMPRESTIMO'")
            .And.Contain("'CONSUMIR_RESERVA'")
            .And.Contain("LireParametroEducaoAsync(c,tx,x.TenantId,\"RENOVACAO_DIAS_ESTENDIMENTO\",ct)")
            .And.Contain("basePrazo.AddDays((em.Renovacoes+1)*dias)")
            .And.Contain("throw new RenovacaoLimiteExcedidoException(emprestimoId,em.Renovacoes,maxR)");

        // Ocorrencias tipadas com compensacao unica (BR-3.4): PERDA/DANO/EXTRAVIO encerra o emprestimo
        // vigente, BLOQUEIA trava o exemplar; estorno restaura exemplar/reabre emprestimo so se encerrado
        // pela ocorrencia; desbloqueio exige justificativa. Bloqueio impede emprestimo/reserva.
        repo.Should().Contain("if(tipo is not (\"PERDA\" or \"DANO\" or \"EXTRAVIO\" or \"BLOQUEIO\"))throw new OcorrenciaTipoInvalidoException(r.Tipo ?? \"\")")
            .And.Contain("ESTORNAR_OCORRENCIA")
            .And.Contain("REABRIR_POR_ESTORNO_OCORRENCIA")
            .And.Contain("if(ex.Status!=\"BLOQUEADO\")throw new ExemplarNaoBloqueadoException(exemplarId,ex.Status ?? \"INDEFINIDO\")")
            .And.Contain("if(ex.Status==\"BLOQUEADO\")throw new ExemplarBloqueadoException(exemplarId)")
            .And.NotContain("DateTime.Today");

        // Inventario fisico (BR-3.5): snapshot com posicao, contagem idempotente sem conflito de indice,
        // divergencia justificada (AJUSTE/DISPENSA) com efeito do AJUSTE no exemplar e fechamento
        // bloqueado enquanto existir divergencia aberta.
        repo.Should().Contain("SNAPSHOT_INVENTARIO")
            .And.Contain("where not exists(select 1 from sigov.educacao_biblioteca_inventario_item i2 where i2.tenant_id=@TenantId and i2.referencia_id=@InvId")
            .And.Contain("JUSTIFICAR_DIVERGENCIA")
            .And.Contain("AJUSTE_INVENTARIO")
            .And.Contain("FECHAR_INVENTARIO");

        // Historico append-only (BR-3.7): leitura unificada das tipadas (emprestimo/devolucao/reserva/
        // ocorrencia/evento) ordenada pelo mais recente, teto de 200 eventos.
        repo.Should().Contain("union all select dv.created_at,'DEVOLUCAO'")
            .And.Contain("union all select ev.created_at,'EVENTO'")
            .And.Contain("order by 1 desc limit 200");

        // Maquina de estados: CONSUMIDA e terminal da reserva (libera a vaga de reserva vigente).
        var maquina = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Application", "Educacao", "Avancada", "EducacaoStatusMaquina.cs"));
        maquina.Should().Contain("CONSUMIDA");

        // Contratos e rotas publicas (circulacao multi-esfera, regra 24).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs"))
            .Should().Contain("Task<long> RenovarEmprestimoAsync(EducacaoAvancadaContext contexto,long emprestimoId,CancellationToken ct)")
            .And.Contain("Task<long> LancarOcorrenciaAsync(EducacaoAvancadaContext contexto,EducacaoBibliotecaOcorrenciaLancarRequest request,CancellationToken ct)")
            .And.Contain("Task<long> FecharInventarioAsync(EducacaoAvancadaContext contexto,long inventarioId,string? justificativa,CancellationToken ct)")
            .And.Contain("Task<List<EducacaoBibliotecaHistoricoItemDto>> HistoricoExemplarAsync(EducacaoAvancadaContext contexto,long exemplarId,CancellationToken ct)");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoBibliotecaController.cs"))
            .Should().Contain("[HttpPost(\"emprestimos/{id:long}/renovar\")]")
            .And.Contain("[HttpPost(\"exemplares/{id:long}/ocorrencia\")]")
            .And.Contain("[HttpPost(\"ocorrencias/{id:long}/estornar\")]")
            .And.Contain("[HttpPost(\"exemplares/{id:long}/desbloquear\")]")
            .And.Contain("[HttpPost(\"inventarios/lancar\")]")
            .And.Contain("[HttpPost(\"inventarios/{id:long}/itens/contar\")]")
            .And.Contain("[HttpPost(\"inventarios/{id:long}/itens/justificar\")]")
            .And.Contain("[HttpPost(\"inventarios/{id:long}/fechar\")]")
            .And.Contain("[HttpGet(\"historico/exemplar/{id:long}\")]")
            .And.Contain("[HttpGet(\"historico/tomador\")]");

        // Falhas nomeadas mapeadas explicitamente no caminho dos comandos e no filtro de leituras (regra 13).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoAvancadaControllerBase.cs"))
            .Should().Contain("catch (InventarioDivergenciasAbertasException ex)")
            .And.Contain("catch (DataDevolucaoInvalidaException ex)")
            .And.Contain("catch (ExemplarBloqueadoException ex)");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Filters/EducacaoFalhasNomeadasFiltro.cs"))
            .Should().Contain("InventarioDivergenciasAbertasException => StatusCodes.Status409Conflict")
            .And.Contain("BibliotecaInventarioItemInexistenteException => StatusCodes.Status404NotFound");

        // Regra 7: migration registrada no manifest.
        var manifest = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "manifest.json"));
        manifest.Should().Contain("20261012150000_evo_c2s3_biblioteca_circulacao_completa.sql");

        // Ciclo 2 Sprint 4 banco (transporte da rota a viagem, BR-4.1 a BR-4.8): colunas dedicadas de
        // veiculo/motorista/viagem, backstops parciais de sobreposicao (veiculo x data / motorista x data),
        // presenca unica por viagem x aluno nao estornado, ocorrencia ALTA unica vigente por viagem,
        // custo REALIZADO unico por viagem, catalogo configuravel de compatibilidade e 4 parametros.
        var migrationS4 = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261012160000_evo_c2s4_transporte_viagens_completa.sql"));
        migrationS4.Should()
            .Contain("add column if not exists placa varchar(20)")
            .And.Contain("add column if not exists situacao_documental varchar(40) not null default 'REGULAR'")
            .And.Contain("add column if not exists habilitacao_validade date")
            .And.Contain("create unique index if not exists ux_transporte_viagem_tenant_veiculo_data")
            .And.Contain("status in ('PROGRAMADA','EM_ANDAMENTO')")
            .And.Contain("create unique index if not exists ux_transporte_presenca_tenant_viagem_aluno")
            .And.Contain("create unique index if not exists ux_transporte_ocorrencia_tenant_viagem_grave")
            .And.Contain("create unique index if not exists ux_transporte_custo_tenant_viagem_realizado")
            .And.Contain("create table if not exists sigov.transporte_categoria_compatibilidade")
            .And.Contain("'TRANSPORTE_INSPECCAO_MODO'")
            .And.Contain("'TRANSPORTE_OCORRENCIA_GRAVE_CARONA_HORAS'")
            .And.Contain("C2S4-POSTCOND");
        manifest.Should().Contain("20261012160000_evo_c2s4_transporte_viagens_completa.sql");

        // Comandos tipados expostos no controller de transporte (BR-4.3 a BR-4.7) e falhas nomeadas no filtro (regra 13).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoTransporteEscolarController.cs"))
            .Should().Contain("[HttpPost(\"veiculos/lancar\")]")
            .And.Contain("[HttpPost(\"motoristas/lancar\")]")
            .And.Contain("[HttpPost(\"viagens/lancar\")]")
            .And.Contain("[HttpPost(\"viagens/{id:long}/iniciar\")]")
            .And.Contain("[HttpPost(\"viagens/{id:long}/concluir\")]")
            .And.Contain("[HttpPost(\"viagens/{id:long}/cancelar\")]")
            .And.Contain("[HttpPost(\"viagens/{id:long}/substituir\")]")
            .And.Contain("[HttpPost(\"viagens/{id:long}/presencas/lancar\")]")
            .And.Contain("[HttpPost(\"viagens/{id:long}/presencas/fechar\")]")
            .And.Contain("[HttpPost(\"viagens/{id:long}/presencas/{presencaId:long}/estornar\")]")
            .And.Contain("[HttpPost(\"viagens/{id:long}/ocorrencias/lancar\")]")
            .And.Contain("[HttpPost(\"viagens/{id:long}/ocorrencias/{ocorrenciaId:long}/desfechar\")]")
            .And.Contain("[HttpPost(\"viagens/{id:long}/custos/realizar\")]")
            .And.Contain("[HttpGet(\"viagens/{id:long}/relatorio\")]");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Filters/EducacaoFalhasNomeadasFiltro.cs"))
            .Should().Contain("TransporteVeiculoInaptoException => StatusCodes.Status409Conflict")
            .And.Contain("TransporteMotoristaHabilitacaoVencidaException => StatusCodes.Status409Conflict")
            .And.Contain("TransporteViagemVeiculoSobreposicaoException => StatusCodes.Status409Conflict")
            .And.Contain("TransportePresencasFechadasEstornoObrigatorioException => StatusCodes.Status409Conflict")
            .And.Contain("TransporteCustoRealizadoExistenteException => StatusCodes.Status409Conflict")
            .And.Contain("TransporteViagemInexistenteException => StatusCodes.Status404NotFound");

        // Ciclo 2 Sprint 5 banco (FUNDEB/custos/rateio + avaliacao do magisterio + Educacenso, BR-5.1 a BR-5.7):
        // competencia fechada por tenant x data, aplicacao unica por competencia x centro (sentinela
        // 2999-12-31 para legado sem competencia), rateio unico por origem x criterio x competencia,
        // resultado de avaliacao unico por avaliacao x servidor e parametro canonico do minimo de
        // conformidade FUNDEB (LC 114/2021, default 70%, parametrizavel - regra 11).
        var migrationS5 = File.ReadAllText(Path.Combine(Root, "database", "postgres", "migrations", "20261013120000_evo_c2s5_fundeb_rateio_magisterio_educacenso.sql"));
        migrationS5.Should()
            .Contain("create table if not exists sigov.educacao_fundeb_competencia(id bigint generated by default as identity primary key")
            .And.Contain("create unique index if not exists ux_fundeb_competencia_tenant_date on sigov.educacao_fundeb_competencia(tenant_id, competencia)")
            .And.Contain("create unique index if not exists ux_fundeb_aplicacao_tenant_comp_centro")
            .And.Contain("coalesce(competencia,'2999-12-31'::date), coalesce(centro_custo_id,(0)::bigint)")
            .And.Contain("create unique index if not exists ux_custo_rateio_tenant_origem_criterio_comp")
            .And.Contain("tipo in ('MATRICULAS','RECEITA','PESO_MANUAL')")
            .And.Contain("create unique index if not exists ux_avaliacao_resultado_tenant_avaliacao_servidor")
            .And.Contain("'EDUCACAO','FDEB_CONFORMIDADE_MINIMO_PCT'")
            .And.Contain("'INTEGER','70',false,210,true")
            .And.Contain("C2S5-POSTCOND");
        manifest.Should().Contain("20261013120000_evo_c2s5_fundeb_rateio_magisterio_educacenso.sql");

        // Comandos tipados expostos no controller de indicadores (BR-5.2 a BR-5.6) e falhas nomeadas no filtro (regra 13).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoIndicadoresController.cs"))
            .Should().Contain("[HttpPost(\"fundeb/competencias/fechar\")]")
            .And.Contain("[HttpPost(\"fundeb/competencias/reabrir\")]")
            .And.Contain("[HttpGet(\"fundeb/conformidade\")]")
            .And.Contain("[HttpPost(\"custos/lancamentos/lancar\")]")
            .And.Contain("[HttpPost(\"custos/rateios/lancar\")]")
            .And.Contain("[HttpPost(\"custos/rateios/{id:long}/estornar\")]")
            .And.Contain("[HttpPost(\"avaliacao-magisterio/lancar\")]")
            .And.Contain("[HttpPost(\"avaliacao-magisterio/{id:long}/resultados/lancar\")]")
            .And.Contain("[HttpPost(\"avaliacao-magisterio/{id:long}/fechar\")]")
            .And.Contain("[HttpPost(\"avaliacao-magisterio/{id:long}/reabrir\")]")
            .And.Contain("[HttpPost(\"educacenso/inconsistencias/{id:long}/decidir\")]")
            .And.Contain("[HttpPost(\"educacenso/lotes/{id:long}/encerrar\")]")
            .And.Contain("[HttpPost(\"educacenso/eventos/sincronizar\")]");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Filters/EducacaoFalhasNomeadasFiltro.cs"))
            .Should().Contain("FundebCompetenciaFechadaException => StatusCodes.Status409Conflict")
            .And.Contain("FundebAplicacaoDuplicadaException => StatusCodes.Status409Conflict")
            .And.Contain("CustoRateioDuplicadoException => StatusCodes.Status409Conflict")
            .And.Contain("AvaliacaoResultadoExistenteException => StatusCodes.Status409Conflict")
            .And.Contain("AvaliacaoFechadaResultadosBloqueadosException => StatusCodes.Status409Conflict")
            .And.Contain("EducacensoLoteAltaAbertaException => StatusCodes.Status409Conflict")
            .And.Contain("CustoRateioInexistenteException => StatusCodes.Status404NotFound")
            .And.Contain("EducacensoMotivoObrigatorioException => StatusCodes.Status400BadRequest");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/Avancada/EducacaoEscopoExceptions.cs"))
            .Should().Contain("COMPETENCIA_FUNDEB_FECHADA: a competência")
            .And.Contain("RATEIO_DUPLICADO: já existe rateio vigente por critério")
            .And.Contain("EDUCACENSO_LOTE_ALTA_ABERTA: o lote");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Educacao/Avancada/EducacaoAvancadaRepository.cs"))
            .Should().Contain("ESTORNO_RATEIO:")
            .And.Contain("'RATEIO_DERIVADO'")
            .And.Contain("jsonb_build_object('consolidado'");
    }

    [Fact]
    public void TelasReais_Ciclo2Sprint6_FachadaEstaticaSubstituida_KpisComOrigem_AcoesTipadas_E_AcessoNomeado()
    {
        // Ciclo 2 Sprint 6 (BR-6.1 a BR-6.6): as quatro áreas da Educação Avançada deixam de
        // renderizar a fachada estatica Avancada.cshtml; cada controller renderiza sua view de
        // area ligada aos endpoints tipados, o acesso e governado pelo modulo 'educacao' do
        // banco (regra 11) com estado nomeado 'sem acesso', KPIs com origem (metrica+endpoint+
        // timestamp), acoes reais com confirmacao/justificativa e componentes compartilhados.

        foreach (var (file, areaView) in new[]
        {
            ("src/Sigov.Web/Controllers/EducacaoMerendaController.cs", "~/Views/Educacao/Avancada/Merenda.cshtml"),
            ("src/Sigov.Web/Controllers/EducacaoBibliotecaController.cs", "~/Views/Educacao/Avancada/Biblioteca.cshtml"),
            ("src/Sigov.Web/Controllers/EducacaoTransporteEscolarController.cs", "~/Views/Educacao/Avancada/TransporteEscolar.cshtml"),
            ("src/Sigov.Web/Controllers/EducacaoIndicadoresController.cs", "~/Views/Educacao/Avancada/Indicadores.cshtml")
        })
        {
            File.ReadAllText(Path.Combine(Root, file))
                .Should()
                .Contain("CanSeeModule(User,\"educacao\")")
                .And.Contain(areaView)
                .And.Contain("~/Views/Educacao/Avancada/SemAcesso.cshtml")
                .And.NotContain("~/Views/Educacao/Avancada.cshtml");
        }

        File.ReadAllText(Path.Combine(Root, "src/Sigov.Web/Views/Educacao/Avancada/_AreaShell.cshtml"))
            .Should()
            .Contain("data-edu-area=\"@eduArea\"")
            .And.Contain("data-edu-page=\"@eduPagina\"")
            .And.Contain("_FilterBar")
            .And.Contain("_Loading")
            .And.Contain("_EmptyState")
            .And.Contain("_ModuleHelpBox")
            .And.Contain("zero ≠ indisponível");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Web/Views/Educacao/Avancada/SemAcesso.cshtml"))
            .Should().Contain("_AccessDeniedState");

        foreach (var (view, apiBase) in new[]
        {
            ("src/Sigov.Web/Views/Educacao/Avancada/Merenda.cshtml", "api/educacao/merenda"),
            ("src/Sigov.Web/Views/Educacao/Avancada/Biblioteca.cshtml", "api/educacao/biblioteca"),
            ("src/Sigov.Web/Views/Educacao/Avancada/TransporteEscolar.cshtml", "api/educacao/transporte"),
            ("src/Sigov.Web/Views/Educacao/Avancada/Indicadores.cshtml", "\"api/educacao\",")
        })
        {
            File.ReadAllText(Path.Combine(Root, view))
                .Should()
                .Contain(apiBase)
                .And.Contain("_AreaShell")
                .And.Contain("~/js/modules/educacao-area.js")
                .And.Contain("window.EduArea.init();")
                .And.NotContain("Painel atualizado");
        }

        // Motor das telas reais: API via bearer do Auth/ApiToken, origem em cada KPI, falhas
        // nomeadas exibidas como retornadas, confirmacao com justificativa (BR-6.2/6.4).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Web/wwwroot/js/modules/educacao-area.js"))
            .Should()
            .Contain("getAuthToken")
            .And.Contain("kpi-src")
            .And.Contain("EDU_AREA_PAGINA_SEM_ENDPOINT")
            .And.Contain("SigovConfirm.show")
            .And.Contain("SigovToast")
            .And.Contain("edu-confirm-motivo");

        // CSS compartilhado do design system: responsivo 360/768/1440 + foco visivel por teclado (BR-6.3).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Web/wwwroot/css/sigov-components.css"))
            .Should()
            .Contain("@media(max-width:360px)")
            .And.Contain("@media(min-width:768px) and (max-width:1439px)")
            .And.Contain("@media(min-width:1440px)")
            .And.Contain(":focus-visible");

        // Listagens tipadas novas que as telas consomem (mesmo padrao generico ListarAsync).
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoIndicadoresController.cs"))
            .Should()
            .Contain("[HttpGet(\"custos/rateios\")]")
            .And.Contain("[HttpGet(\"educacenso/inconsistencias\")]");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoBibliotecaController.cs"))
            .Should().Contain("[HttpGet(\"digital\")]");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoTransporteEscolarController.cs"))
            .Should().Contain("[HttpGet(\"alunos\")]");
    }
}
