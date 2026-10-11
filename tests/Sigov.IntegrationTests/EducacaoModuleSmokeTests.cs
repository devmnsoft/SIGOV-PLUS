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

        // #5 Schema ausente: diagnostico operacional explicito, nao lista vazia silenciosa
        code.Should().Contain("Indisponivel(p,t,");

        // #7 Auditoria: transicao de status registra origem, destino e motivo sem sobrescrever o historico
        code.Should().Contain("jsonb_build_array(jsonb_build_object('acao','STATUS','de',@De,'para',@Status,'motivo'")
            .And.Contain("select status,coalesce(entidade_id,-1) as LinhaEntidade from sigov.{recurso} where tenant_id=@TenantId and id=@Id and is_deleted=false");

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
}
