using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sigov.Application.Compras;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Compras;

public sealed class ComprasService(NpgsqlConnectionFactory factory, ILogger<ComprasService> log) : IComprasService
{
    public async Task<ComprasDashboard> DashboardAsync(long t, long e, CancellationToken ct)
    {
        await using var c = factory.CreateConnection();
        var r = await c.QuerySingleAsync<R>(new CommandDefinition(
            """
            select 
                (select count(*) from sigov.compras_solicitacao where tenant_id=@T and entidade_id=@E and status in('ENVIADA','EM_ANALISE','APROVADA')) Sol,
                (select count(*) from sigov.compras_processo p where p.tenant_id=@T and p.entidade_id=@E and p.status not in('CONTRATADO','CANCELADO','FRACASSADO','DESERTO') and exists(select 1 from sigov.compras_processo_item i where i.processo_id=p.id and (select count(*) from sigov.compras_cotacao q where q.processo_item_id=i.id and q.status<>'DESCLASSIFICADA')<3)) Sem,
                (select count(*) from sigov.compras_contrato where tenant_id=@T and entidade_id=@E and status='VIGENTE' and data_fim between current_date and current_date+30) Contr,
                (select count(*) from sigov.compras_ata_registro_preco where tenant_id=@T and entidade_id=@E and status='VIGENTE' and data_fim between current_date and current_date+30) Atas,
                (select coalesce(sum(valor_estimado),0) from sigov.compras_processo where tenant_id=@T and entidade_id=@E and status not in('CONTRATADO','CANCELADO','FRACASSADO','DESERTO')) Valor
            """, new { T = t, E = e }, cancellationToken: ct));

        var ps = (await c.QueryAsync<CompraLinhaDto>(new CommandDefinition(
            "select id,numero,objeto Descricao,status,null Fornecedor,valor_estimado Valor,data_abertura Inicio,data_limite Fim from sigov.compras_processo where tenant_id=@T and entidade_id=@E order by updated_at desc limit 8",
            new { T = t, E = e }, cancellationToken: ct))).AsList();

        var ev = (await c.QueryAsync<CompraEventoDto>(new CommandDefinition(
            "select 'PROCESSO' Tipo,numero||' — '||status Descricao,updated_at Data from sigov.compras_processo where tenant_id=@T and entidade_id=@E order by updated_at desc limit 10",
            new { T = t, E = e }, cancellationToken: ct))).AsList();

        return new(r.Sol, r.Sem, r.Contr, r.Atas, r.Valor, ps, ev);
    }

    public async Task<IReadOnlyList<FornecedorDto>> FornecedoresAsync(long t, long e, CompraFiltro f, CancellationToken ct)
    {
        await using var c = factory.CreateConnection();
        return (await c.QueryAsync<FornecedorDto>(new CommandDefinition(
            """
            select id,nome,tipo_pessoa TipoPessoa,
                   case when length(documento)<=4 then '***' else repeat('*',length(documento)-4)||right(documento,4) end DocumentoMascarado,
                   email,telefone,endereco_resumido Endereco,status,observacoes 
            from sigov.compras_fornecedor 
            where tenant_id=@T and entidade_id=@E and (@S is null or status=@S) and (@B is null or nome ilike '%'||@B||'%') 
            order by nome
            """, new { T = t, E = e, S = N(f.Status), B = N(f.Busca) }, cancellationToken: ct))).AsList();
    }

    public async Task<long> CriarFornecedorAsync(long t, long u, string corr, FornecedorInput i, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(i.Nome) || string.IsNullOrWhiteSpace(i.Documento) || !new[] { "FISICA", "JURIDICA" }.Contains(i.TipoPessoa))
            throw new ArgumentException("Nome, documento e tipo de pessoa válido são obrigatórios.");

        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        try
        {
            var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.compras_fornecedor(tenant_id,entidade_id,nome,tipo_pessoa,documento,email,telefone,endereco_resumido,observacoes,created_by,updated_by) 
                values(@T,@EntidadeId,@Nome,@TipoPessoa,@Documento,@Email,@Telefone,@Endereco,@Observacoes,@U,@U) returning id
                """,
                new { T = t, U = u, i.EntidadeId, Nome = i.Nome.Trim(), i.TipoPessoa, Documento = Digits(i.Documento), Email = N(i.Email), Telefone = N(i.Telefone), Endereco = N(i.Endereco), Observacoes = N(i.Observacoes) },
                tx, cancellationToken: ct));

            await Audit(c, tx, t, i.EntidadeId, "FORNECEDOR", id, "CRIAR", null, i, u, corr, ct);
            await tx.CommitAsync(ct);
            log.LogInformation("Fornecedor {FornecedorId} criado no tenant {TenantId} e entidade {EntidadeId}", id, t, i.EntidadeId);
            return id;
        }
        catch (PostgresException x) when (x.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException("Documento já cadastrado para esta entidade.", x);
        }
    }

    public async Task<byte[]> ExportarFornecedoresAsync(long t, long e, long u, string corr, CancellationToken ct)
    {
        var x = (await FornecedoresAsync(t, e, new(), ct)).Take(5000).ToList();
        var b = new StringBuilder("Nome;Tipo;Documento;Status;Email\n");
        foreach (var i in x)
        {
            var nomeSanitizado = SanitizeCsv(i.Nome);
            var statusSanitizado = SanitizeCsv(i.Status);
            var emailSanitizado = SanitizeCsv(i.Email ?? "");
            b.AppendLine($"\"{nomeSanitizado}\";{i.TipoPessoa};{i.DocumentoMascarado};{statusSanitizado};{emailSanitizado}");
        }
        await using var c = factory.CreateConnection();
        await Audit(c, null, t, e, "FORNECEDOR", 0, "EXPORTAR", null, new { Quantidade = x.Count }, u, corr, ct);
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(b.ToString())).ToArray();
    }

    public async Task<IReadOnlyList<ParametroOpcaoDto>> ModalidadesAsync(CancellationToken ct)
    {
        await using var c = factory.CreateConnection();
        return (await c.QueryAsync<ParametroOpcaoDto>(new CommandDefinition(
            "select id, codigo, nome from sigov.compras_parametro_modalidade where ativo order by nome", cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<ParametroOpcaoDto>> CriteriosAsync(CancellationToken ct)
    {
        await using var c = factory.CreateConnection();
        return (await c.QueryAsync<ParametroOpcaoDto>(new CommandDefinition(
            "select id, codigo, nome from sigov.compras_parametro_criterio where ativo order by nome", cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<SolicitacaoDto>> SolicitacoesAsync(long t, long e, CompraFiltro f, CancellationToken ct)
    {
        await using var c = factory.CreateConnection();
        return (await c.QueryAsync<SolicitacaoDto>(new CommandDefinition(
            """
            select s.id,s.unidade_solicitante UnidadeSolicitante,s.justificativa,s.prioridade,s.origem,s.status,
                   coalesce(sum(i.quantidade*i.valor_estimado),0) ValorEstimado,s.created_at CreatedAt 
            from sigov.compras_solicitacao s 
            left join sigov.compras_solicitacao_item i on i.solicitacao_id=s.id 
            where s.tenant_id=@T and s.entidade_id=@E and (@S is null or s.status=@S) 
            group by s.id order by s.created_at desc
            """, new { T = t, E = e, S = N(f.Status) }, cancellationToken: ct))).AsList();
    }

    public async Task<long> CriarSolicitacaoAsync(long t, long u, string corr, SolicitacaoInput i, CancellationToken ct)
    {
        if (i.Itens.Count == 0 || i.Itens.Any(x => x.Quantidade <= 0 || x.ValorEstimado < 0) || string.IsNullOrWhiteSpace(i.Justificativa))
            throw new ArgumentException("Justificativa e itens com quantidades válidas são obrigatórios.");

        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);

        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_solicitacao(tenant_id,entidade_id,unidade_solicitante,justificativa,prioridade,origem,origem_referencia,observacao,created_by,updated_by) 
            values(@T,@EntidadeId,@UnidadeSolicitante,@Justificativa,@Prioridade,@Origem,@OrigemReferencia,@Observacao,@U,@U) returning id
            """,
            new { T = t, U = u, i.EntidadeId, i.UnidadeSolicitante, i.Justificativa, i.Prioridade, i.Origem, i.OrigemReferencia, i.Observacao },
            tx, cancellationToken: ct));

        foreach (var x in i.Itens)
        {
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.compras_solicitacao_item(tenant_id,entidade_id,solicitacao_id,descricao,quantidade,unidade,tipo_material_servico,valor_estimado,gera_pendencia_patrimonial) 
                values(@T,@E,@Id,@Descricao,@Quantidade,@Unidade,@Tipo,@ValorEstimado,@GeraPendenciaPatrimonial)
                """,
                new { T = t, E = i.EntidadeId, Id = id, x.Descricao, x.Quantidade, x.Unidade, x.Tipo, x.ValorEstimado, x.GeraPendenciaPatrimonial },
                tx, cancellationToken: ct));
        }

        await Audit(c, tx, t, i.EntidadeId, "SOLICITACAO", id, "CRIAR", null, i, u, corr, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    public Task AlterarSolicitacaoAsync(long t, long e, long u, string corr, long id, string acao, string? just, CancellationToken ct) => Tx(async (c, tx) =>
    {
        var atual = await c.ExecuteScalarAsync<string?>(new CommandDefinition(
            "select status from sigov.compras_solicitacao where tenant_id=@T and entidade_id=@E and id=@Id for update",
            new { T = t, E = e, Id = id }, tx, cancellationToken: ct)) ?? throw new KeyNotFoundException();

        var novo = acao.ToLowerInvariant() switch
        {
            "enviar" when atual == "RASCUNHO" => "ENVIADA",
            "aprovar" when atual is "ENVIADA" or "EM_ANALISE" => "APROVADA",
            "rejeitar" when atual is "ENVIADA" or "EM_ANALISE" => "REJEITADA",
            _ => throw new InvalidOperationException("Transição de solicitação inválida.")
        };

        if (novo == "REJEITADA" && string.IsNullOrWhiteSpace(just))
            throw new ArgumentException("Justificativa é obrigatória.");

        await c.ExecuteAsync(new CommandDefinition(
            """
            update sigov.compras_solicitacao set status=@N,updated_at=now(),updated_by=@U where id=@Id; 
            insert into sigov.compras_solicitacao_historico(tenant_id,entidade_id,solicitacao_id,status_anterior,status_novo,justificativa,usuario_id,correlation_id) 
            values(@T,@E,@Id,@A,@N,@J,@U,@C)
            """,
            new { T = t, E = e, Id = id, A = atual, N = novo, J = N(just), U = u, C = corr }, tx, cancellationToken: ct));

        await Audit(c, tx, t, e, "SOLICITACAO", id, "STATUS", new { Status = atual }, new { Status = novo }, u, corr, ct);
    }, ct);

    public async Task<IReadOnlyList<ProcessoDto>> ProcessosAsync(long t, long e, CompraFiltro f, CancellationToken ct)
    {
        await using var c = factory.CreateConnection();
        return (await c.QueryAsync<ProcessoDto>(new CommandDefinition(
            """
            select p.id,p.numero,p.exercicio,m.nome Modalidade,j.nome Criterio,p.objeto,p.status,p.valor_estimado ValorEstimado,p.data_limite DataLimite 
            from sigov.compras_processo p 
            join sigov.compras_parametro_modalidade m on m.id=p.modalidade_id 
            join sigov.compras_parametro_criterio j on j.id=p.criterio_id 
            where p.tenant_id=@T and p.entidade_id=@E and (@S is null or p.status=@S) 
            order by p.exercicio desc,p.numero
            """, new { T = t, E = e, S = N(f.Status) }, cancellationToken: ct))).AsList();
    }

    public async Task<ProcessoDetalhe?> ProcessoAsync(long t, long e, long id, CancellationToken ct)
    {
        var p = (await ProcessosAsync(t, e, new(), ct)).SingleOrDefault(x => x.Id == id);
        if (p is null) return null;
        await using var c = factory.CreateConnection();
        var itens = (await c.QueryAsync<CompraLinhaDto>(new CommandDefinition(
            "select id,numero::text Numero,descricao Descricao,status,null Fornecedor,quantidade*valor_estimado Valor,null Inicio,null Fim from sigov.compras_processo_item where tenant_id=@T and entidade_id=@E and processo_id=@Id order by numero",
            new { T = t, E = e, Id = id }, cancellationToken: ct))).AsList();

        var h = (await c.QueryAsync<CompraEventoDto>(new CommandDefinition(
            "select 'FASE' Tipo,status_anterior||' → '||status_novo||coalesce(' ('||justificativa||')','') Descricao,ocorrido_em Data from sigov.compras_processo_fase_historico where tenant_id=@T and entidade_id=@E and processo_id=@Id order by ocorrido_em desc",
            new { T = t, E = e, Id = id }, cancellationToken: ct))).AsList();

        return new(p, itens, h);
    }

    public async Task<long> CriarProcessoAsync(long t, long u, string corr, ProcessoInput i, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(i.Numero) || string.IsNullOrWhiteSpace(i.Objeto) || string.IsNullOrWhiteSpace(i.Justificativa))
            throw new ArgumentException("Número, objeto e justificativa são obrigatórios.");

        if (i.Itens is { Count: > 0 } && i.Itens.Any(x => x.Quantidade <= 0 || string.IsNullOrWhiteSpace(x.Descricao)))
            throw new ArgumentException("Todos os itens devem possuir descrição e quantidade estritamente positiva.");

        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);

        var valorEstimadoInicial = 0m;
        if (i.SolicitacaoId is not null)
        {
            valorEstimadoInicial = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
                "select coalesce(sum(quantidade*valor_estimado),0) from sigov.compras_solicitacao_item where solicitacao_id=@S",
                new { S = i.SolicitacaoId }, tx, cancellationToken: ct));
        }
        else if (i.Itens is { Count: > 0 })
        {
            valorEstimadoInicial = i.Itens.Sum(x => x.Quantidade * x.ValorEstimado);
        }

        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_processo(tenant_id,entidade_id,exercicio,numero,modalidade_id,criterio_id,objeto,justificativa,data_abertura,data_limite,solicitacao_id,valor_estimado,created_by,updated_by) 
            select @T,@EntidadeId,@Exercicio,@Numero,m.id,j.id,@Objeto,@Justificativa,@DataAbertura,@DataLimite,@SolicitacaoId,@ValorEstimado,@U,@U 
            from sigov.compras_parametro_modalidade m 
            cross join sigov.compras_parametro_criterio j 
            where m.codigo=@ModalidadeCodigo and j.codigo=@CriterioCodigo and m.ativo and j.ativo returning id
            """,
            new { T = t, U = u, i.EntidadeId, i.Exercicio, i.Numero, i.ModalidadeCodigo, i.CriterioCodigo, i.Objeto, i.Justificativa, i.DataAbertura, i.DataLimite, i.SolicitacaoId, ValorEstimado = valorEstimadoInicial },
            tx, cancellationToken: ct));

        if (id == 0) throw new ArgumentException("Modalidade ou critério não encontrado no catálogo persistente ativo.");

        if (i.SolicitacaoId is not null)
        {
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.compras_processo_item(tenant_id,entidade_id,processo_id,numero,descricao,quantidade,unidade,tipo_material_servico,valor_estimado,gera_pendencia_patrimonial) 
                select tenant_id,entidade_id,@Id,row_number() over(order by id),descricao,quantidade,unidade,tipo_material_servico,valor_estimado,gera_pendencia_patrimonial 
                from sigov.compras_solicitacao_item where solicitacao_id=@S; 
                update sigov.compras_solicitacao set status='CONVERTIDA_EM_PROCESSO' where tenant_id=@T and entidade_id=@EntidadeId and id=@S
                """,
                new { T = t, i.EntidadeId, Id = id, S = i.SolicitacaoId }, tx, cancellationToken: ct));
        }
        else if (i.Itens is { Count: > 0 })
        {
            for (var idx = 0; idx < i.Itens.Count; idx++)
            {
                var item = i.Itens[idx];
                await c.ExecuteAsync(new CommandDefinition(
                    """
                    insert into sigov.compras_processo_item(tenant_id,entidade_id,processo_id,numero,descricao,quantidade,unidade,tipo_material_servico,valor_estimado,gera_pendencia_patrimonial)
                    values(@T,@E,@ProcessoId,@Numero,@Descricao,@Quantidade,@Unidade,@Tipo,@ValorEstimado,@GeraPendencia)
                    """,
                    new { T = t, E = i.EntidadeId, ProcessoId = id, Numero = idx + 1, item.Descricao, item.Quantidade, item.Unidade, Tipo = item.TipoMaterialServico, item.ValorEstimado, GeraPendencia = item.GeraPendenciaPatrimonial },
                    tx, cancellationToken: ct));
            }
        }

        await c.ExecuteAsync(new CommandDefinition(
            "insert into sigov.compras_processo_fase_historico(tenant_id,entidade_id,processo_id,status_anterior,status_novo,usuario_id,correlation_id,justificativa) values(@T,@E,@Id,'INICIAL','PLANEJAMENTO',@U,@C,'Abertura do processo de contratação')",
            new { T = t, E = i.EntidadeId, Id = id, U = u, C = corr }, tx, cancellationToken: ct));

        await Audit(c, tx, t, i.EntidadeId, "PROCESSO", id, "CRIAR", null, i, u, corr, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    public Task AvancarAsync(long t, long e, long u, string corr, long id, string fase, CancellationToken ct) => Tx(async (c, tx) =>
    {
        var a = await c.QuerySingleOrDefaultAsync<P>(new CommandDefinition(
            "select status,objeto,data_abertura,data_limite from sigov.compras_processo where tenant_id=@T and entidade_id=@E and id=@Id for update",
            new { T = t, E = e, Id = id }, tx, cancellationToken: ct)) ?? throw new KeyNotFoundException();

        var ordem = new[] { "PLANEJAMENTO", "PESQUISA_PRECO", "EDITAL_TR", "PUBLICADO", "RECEBENDO_PROPOSTAS", "JULGAMENTO", "ADJUDICADO", "HOMOLOGADO", "CONTRATADO" };
        if (Array.IndexOf(ordem, fase) != Array.IndexOf(ordem, a.Status) + 1)
            throw new InvalidOperationException("A fase deve avançar uma etapa por vez.");

        if (fase == "PUBLICADO" && (a.DataAbertura is null || a.DataLimite is null))
            throw new InvalidOperationException("Abertura e prazo são obrigatórios antes da publicação.");

        if (fase == "JULGAMENTO" && await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select count(*) from sigov.compras_cotacao q join sigov.compras_processo_item i on i.id=q.processo_item_id where i.processo_id=@Id",
            new { Id = id }, tx, cancellationToken: ct)) == 0)
            throw new InvalidOperationException("É necessária ao menos uma cotação.");

        await Phase(c, tx, t, e, id, a.Status, fase, u, corr, null, ct);
    }, ct);

    public Task FinalizarProcessoAsync(long t, long e, long u, string corr, long id, string statusFinal, string justificativa, CancellationToken ct) => Tx(async (c, tx) =>
    {
        var statusPermitidos = new[] { "ANULADO", "DESERTO", "FRACASSADO", "CANCELADO" };
        var normStatus = statusFinal.ToUpperInvariant().Trim();
        if (!statusPermitidos.Contains(normStatus))
            throw new ArgumentException($"Situação final '{statusFinal}' inválida. Situações permitidas: ANULADO, DESERTO, FRACASSADO ou CANCELADO.");

        if (string.IsNullOrWhiteSpace(justificativa))
            throw new ArgumentException("Justificativa formal é obrigatória para anular, desertar, fracassar ou cancelar o processo.");

        var a = await c.QuerySingleOrDefaultAsync<P>(new CommandDefinition(
            "select status,objeto,data_abertura,data_limite from sigov.compras_processo where tenant_id=@T and entidade_id=@E and id=@Id for update",
            new { T = t, E = e, Id = id }, tx, cancellationToken: ct)) ?? throw new KeyNotFoundException("Processo não encontrado no contexto atual.");

        if (statusPermitidos.Contains(a.Status) || a.Status == "CONTRATADO")
            throw new InvalidOperationException($"Processo com status '{a.Status}' não pode ser alterado para '{normStatus}'.");

        await Phase(c, tx, t, e, id, a.Status, normStatus, u, corr, justificativa.Trim(), ct);
    }, ct);

    public async Task<long> CotarAsync(long t, long e, long u, string corr, long id, CotacaoInput i, CancellationToken ct)
    {
        if (i.ValorUnitario <= 0 || i.Quantidade <= 0)
            throw new ArgumentException("Valores da cotação devem ser positivos.");

        if (i.Status == "CLASSIFICADA" && i.Validade < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new InvalidOperationException("Proposta vencida não pode ser classificada.");

        if (i.Status == "DESCLASSIFICADA" && string.IsNullOrWhiteSpace(i.JustificativaDesclassificacao))
            throw new ArgumentException("Justificativa de desclassificação é obrigatória.");

        await using var c = factory.CreateConnection();

        // Validação estrita: fornecedor inativo não participa de cotações
        var fornecedorAtivo = await c.ExecuteScalarAsync<bool>(new CommandDefinition(
            "select exists(select 1 from sigov.compras_fornecedor where id=@F and tenant_id=@T and entidade_id=@E and status='ATIVO')",
            new { T = t, E = e, F = i.FornecedorId }, cancellationToken: ct));

        if (!fornecedorAtivo)
            throw new InvalidOperationException("Fornecedor inativo ou inexistente não pode participar de cotações.");

        var idq = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_cotacao(tenant_id,entidade_id,processo_id,processo_item_id,fornecedor_id,valor_unitario,quantidade,prazo_entrega_dias,validade,observacao,status,justificativa_desclassificacao,created_by) 
            select @T,@E,@P,@I,@F,@V,@Q,@Prazo,@Validade,@Obs,@S,@J,@U 
            where exists(select 1 from sigov.compras_processo_item where id=@I and processo_id=@P and tenant_id=@T and entidade_id=@E) returning id
            """,
            new { T = t, E = e, P = id, I = i.ProcessoItemId, F = i.FornecedorId, V = i.ValorUnitario, Q = i.Quantidade, Prazo = i.PrazoEntregaDias, i.Validade, Obs = N(i.Observacao), S = i.Status, J = N(i.JustificativaDesclassificacao), U = u },
            cancellationToken: ct));

        if (idq == 0) throw new KeyNotFoundException("Item do processo não encontrado.");
        return idq;
    }

    public Task JulgarAsync(long t, long e, long u, string corr, long id, JulgmentoInputCompat i, CancellationToken ct) => Tx(async (c, tx) =>
    {
        var s = await c.ExecuteScalarAsync<string?>(new CommandDefinition(
            "select status from sigov.compras_processo where tenant_id=@T and entidade_id=@E and id=@Id for update",
            new { T = t, E = e, Id = id }, tx, cancellationToken: ct));

        if (s != "JULGAMENTO")
            throw new InvalidOperationException("Somente processos em JULGAMENTO podem ser julgados.");

        var q = await c.QuerySingleOrDefaultAsync<Q>(new CommandDefinition(
            "select q.fornecedor_id FornecedorId,q.valor_unitario*q.quantidade Valor,q.validade,q.status from sigov.compras_cotacao q where q.id=@Q and q.processo_id=@P and q.processo_item_id=@I",
            new { Q = i.CotacaoId, P = id, I = i.ProcessoItemId }, tx, cancellationToken: ct)) ?? throw new KeyNotFoundException("Cotação não encontrada.");

        if (q.Validade < DateOnly.FromDateTime(DateTime.UtcNow) || q.Status == "DESCLASSIFICADA")
            throw new InvalidOperationException("Cotação vencida ou desclassificada.");

        await c.ExecuteAsync(new CommandDefinition(
            """
            insert into sigov.compras_julgamento(tenant_id,entidade_id,processo_id,processo_item_id,cotacao_id,fornecedor_id,valor_final,status,justificativa,usuario_id) 
            values(@T,@E,@P,@I,@Q,@F,@V,@S,@J,@U) 
            on conflict(processo_item_id) do update set cotacao_id=excluded.cotacao_id,fornecedor_id=excluded.fornecedor_id,valor_final=excluded.valor_final,status=excluded.status,justificativa=excluded.justificativa,usuario_id=excluded.usuario_id,julgado_em=now(); 
            update sigov.compras_processo_item set status=@S where id=@I
            """,
            new { T = t, E = e, P = id, I = i.ProcessoItemId, Q = i.CotacaoId, F = q.FornecedorId, V = q.Valor, S = i.Status, J = i.Justificativa, U = u },
            tx, cancellationToken: ct));

        await Audit(c, tx, t, e, "JULGAMENTO", i.ProcessoItemId, "JULGAR", null, i, u, corr, ct);
    }, ct);

    public Task HomologarAsync(long t, long e, long u, string corr, long id, CancellationToken ct) => Tx(async (c, tx) =>
    {
        var s = await c.ExecuteScalarAsync<string?>(new CommandDefinition(
            "select status from sigov.compras_processo where tenant_id=@T and entidade_id=@E and id=@Id for update",
            new { T = t, E = e, Id = id }, tx, cancellationToken: ct));

        if (s is not ("JULGAMENTO" or "ADJUDICADO"))
            throw new InvalidOperationException("Processo não está apto à homologação.");

        if (await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select count(*) from sigov.compras_processo_item where processo_id=@Id and status not in('JULGADO','FRACASSADO')",
            new { Id = id }, tx, cancellationToken: ct)) > 0)
            throw new InvalidOperationException("Todos os itens devem estar julgados ou fracassados.");

        await Phase(c, tx, t, e, id, s, "HOMOLOGADO", u, corr, "Homologação do resultado da licitação", ct);
    }, ct);

    public async Task<long> GerarDemandaReposicaoAsync(long t, long e, long u, string corr, long almoxarifadoId, long materialId, CancellationToken ct)
    {
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);

        // 1. Obter material e validar pertinência ao contexto ativo
        var mat = await c.QuerySingleOrDefaultAsync<MaterialBasico>(new CommandDefinition(
            "select id, codigo, descricao, unidade_medida UnidadeMedida, tipo from sigov.almoxarifado_material where tenant_id=@T and entidade_id=@E and id=@M and ativo",
            new { T = t, E = e, M = materialId }, tx, cancellationToken: ct))
            ?? throw new KeyNotFoundException("Material não encontrado ou inativo no almoxarifado.");

        var almoxNome = await c.ExecuteScalarAsync<string?>(new CommandDefinition(
            "select nome from sigov.almoxarifado_local where tenant_id=@T and entidade_id=@E and id=@L and ativo",
            new { T = t, E = e, L = almoxarifadoId }, tx, cancellationToken: ct))
            ?? throw new KeyNotFoundException("Almoxarifado de origem não encontrado ou inativo.");

        // 2. Idempotência estrita: verificar se já existe processo em aberto para este material/reposição
        var processoAberto = await c.QuerySingleOrDefaultAsync<ProcessoResumoAberto>(new CommandDefinition(
            """
            select p.id, p.numero, p.status 
            from sigov.compras_processo p 
            join sigov.compras_processo_item i on i.processo_id = p.id 
            where p.tenant_id = @T and p.entidade_id = @E 
              and p.status not in ('CONTRATADO','CANCELADO','FRACASSADO','DESERTO') 
              and (i.descricao ilike '%' || @Codigo || '%' or p.justificativa ilike '%[REPOSICAO:' || @L || ':' || @M || ']%') 
            limit 1
            """,
            new { T = t, E = e, Codigo = mat.Codigo, L = almoxarifadoId, M = materialId }, tx, cancellationToken: ct));

        if (processoAberto is not null)
        {
            throw new InvalidOperationException($"Já existe um processo de compra em aberto (#{processoAberto.Numero}, status {processoAberto.Status}) para o material {mat.Descricao}. Segunda geração recusada.");
        }

        // 3. Obter dados da política e saldo para projetar a quantidade da demanda
        var pol = await c.QuerySingleOrDefaultAsync<PoliticaValores>(new CommandDefinition(
            "select estoque_minimo EstoqueMinimo, estoque_alvo EstoqueAlvo, multiplo_compra MultiploCompra, quantidade_minima_pedido QuantidadeMinimaPedido from sigov.almoxarifado_politica_reposicao where tenant_id=@T and entidade_id=@E and almoxarifado_id=@L and material_id=@M and ativa",
            new { T = t, E = e, L = almoxarifadoId, M = materialId }, tx, cancellationToken: ct));

        var saldoAtual = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "select coalesce(quantidade,0) from sigov.almoxarifado_estoque where tenant_id=@T and entidade_id=@E and almoxarifado_id=@L and material_id=@M",
            new { T = t, E = e, L = almoxarifadoId, M = materialId }, tx, cancellationToken: ct));

        var necessidade = Math.Max(0, (pol?.EstoqueAlvo ?? 0) - saldoAtual);
        var baseCompra = Math.Max(necessidade, pol?.QuantidadeMinimaPedido ?? 0);
        var qtdSugerida = (pol?.MultiploCompra is > 0)
            ? Math.Ceiling(baseCompra / pol.MultiploCompra.Value) * pol.MultiploCompra.Value
            : baseCompra;

        if (qtdSugerida <= 0) qtdSugerida = 1;

        // 4. Modalidade e Critério padrão do catálogo persistente
        var modalidadeCodigo = await c.ExecuteScalarAsync<string>(new CommandDefinition(
            "select codigo from sigov.compras_parametro_modalidade where ativo order by case when codigo in ('DISPENSA','PREGAO') then 0 else 1 end, id limit 1", tx, cancellationToken: ct)) ?? "DISPENSA";

        var criterioCodigo = await c.ExecuteScalarAsync<string>(new CommandDefinition(
            "select codigo from sigov.compras_parametro_criterio where ativo order by case when codigo='MENOR_PRECO' then 0 else 1 end, id limit 1", tx, cancellationToken: ct)) ?? "MENOR_PRECO";

        var ano = DateTime.Today.Year;
        var seq = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000;
        var numeroProc = $"REP-{ano}-{mat.Id}-{seq}";
        var objeto = $"Aquisição de reposição de estoque: {mat.Descricao} ({mat.Codigo}) para o almoxarifado {almoxNome}";
        var justificativa = $"Demanda originada automaticamente pelo planejamento de reposição de estoque. [REPOSICAO:{almoxarifadoId}:{materialId}]";

        // 5. Inserir processo em PLANEJAMENTO (sem gerar empenho e sem baixar estoque)
        var idProc = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_processo(tenant_id,entidade_id,exercicio,numero,modalidade_id,criterio_id,objeto,justificativa,valor_estimado,status,created_by,updated_by)
            select @T,@E,@Exercicio,@Numero,m.id,j.id,@Objeto,@Justificativa,0,'PLANEJAMENTO',@U,@U
            from sigov.compras_parametro_modalidade m
            cross join sigov.compras_parametro_criterio j
            where m.codigo=@ModalidadeCodigo and j.codigo=@CriterioCodigo and m.ativo and j.ativo returning id
            """,
            new { T = t, E = e, Exercicio = ano, Numero = numeroProc, Objeto = objeto, Justificativa = justificativa, ModalidadeCodigo = modalidadeCodigo, CriterioCodigo = criterioCodigo, U = u },
            tx, cancellationToken: ct));

        // 6. Inserir item no processo
        await c.ExecuteAsync(new CommandDefinition(
            """
            insert into sigov.compras_processo_item(tenant_id,entidade_id,processo_id,numero,descricao,quantidade,unidade,tipo_material_servico,valor_estimado,gera_pendencia_patrimonial,status)
            values(@T,@E,@IdProc,1,@Descricao,@Qtd,@Unidade,@Tipo,0,@GeraPendencia,'PENDENTE')
            """,
            new { T = t, E = e, IdProc = idProc, Descricao = $"{mat.Codigo} — {mat.Descricao}", Qtd = qtdSugerida, Unidade = mat.UnidadeMedida, Tipo = mat.Tipo, GeraPendencia = (mat.Tipo == "PERMANENTE") },
            tx, cancellationToken: ct));

        // 7. Histórico e auditoria
        await c.ExecuteAsync(new CommandDefinition(
            "insert into sigov.compras_processo_fase_historico(tenant_id,entidade_id,processo_id,status_anterior,status_novo,usuario_id,correlation_id,justificativa) values(@T,@E,@Id,'INICIAL','PLANEJAMENTO',@U,@C,'Demanda autogerada por reposição de estoque')",
            new { T = t, E = e, Id = idProc, U = u, C = corr }, tx, cancellationToken: ct));

        await Audit(c, tx, t, e, "PROCESSO", idProc, "GERAR_REPOSICAO", null, new { materialId, almoxarifadoId, qtdSugerida }, u, corr, ct);
        await tx.CommitAsync(ct);

        log.LogInformation("Processo de reposição {ProcessoId} gerado para material {MaterialId} no almoxarifado {AlmoxId}", idProc, materialId, almoxarifadoId);
        return idProc;
    }

    public Task<IReadOnlyList<CompraLinhaDto>> ContratosAsync(long t, long e, CompraFiltro f, CancellationToken ct) => Lines("compras_contrato", t, e, f, ct);
    public Task<IReadOnlyList<CompraLinhaDto>> AtasAsync(long t, long e, CompraFiltro f, CancellationToken ct) => Lines("compras_ata_registro_preco", t, e, f, ct);

    public async Task<long> CriarContratoAsync(long t, long u, string corr, ContratoInput i, CancellationToken ct)
    {
        if (i.Fim <= i.Inicio) throw new ArgumentException("Fim da vigência deve ser posterior ao início.");
        return await CriarInstrumento("compras_contrato", t, u, corr, i.EntidadeId, i.ProcessoId, i.FornecedorId, i.Numero, i.Objeto, i.Valor, i.Inicio, i.Fim, i.Gestor, i.Fiscal, i.Observacoes, ct);
    }

    public Task<long> CriarAtaAsync(long t, long u, string corr, AtaInput i, CancellationToken ct)
    {
        if (i.Fim <= i.Inicio) throw new ArgumentException("Fim da vigência deve ser posterior ao início.");
        return CriarInstrumento("compras_ata_registro_preco", t, u, corr, i.EntidadeId, i.ProcessoId, i.FornecedorId, i.Numero, i.Objeto, i.ValorGlobal, i.Inicio, i.Fim, null, null, null, ct);
    }

    public async Task<long> RegistrarRecebimentoAsync(long t, long e, long u, string corr, RecebimentoInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Documento))
            throw new ArgumentException("Número do documento fiscal de entrega é obrigatório.");

        if (input.Itens.Count == 0)
            throw new ArgumentException("Ao menos um item deve ser informado no recebimento.");

        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);

        // Validar processo sob lock
        var proc = await c.QuerySingleOrDefaultAsync<ProcessoHeader>(new CommandDefinition(
            "select id, numero, status from sigov.compras_processo where tenant_id=@T and entidade_id=@E and id=@P for update",
            new { T = t, E = e, P = input.ProcessoId }, tx, cancellationToken: ct))
            ?? throw new KeyNotFoundException("Processo de compra não encontrado no contexto atual.");

        // Validar almoxarifado de destino
        var almoxNome = await c.ExecuteScalarAsync<string?>(new CommandDefinition(
            "select nome from sigov.almoxarifado_local where tenant_id=@T and entidade_id=@E and id=@L and ativo",
            new { T = t, E = e, L = input.AlmoxarifadoId }, tx, cancellationToken: ct))
            ?? throw new InvalidOperationException("Almoxarifado de destino não encontrado ou inativo.");

        // Validar itens e saldos a receber (qtd <= saldo pendente)
        foreach (var item in input.Itens)
        {
            if (item.Quantidade <= 0)
                throw new ArgumentException("A quantidade recebida de cada item deve ser positiva.");

            var procItem = await c.QuerySingleOrDefaultAsync<ProcessoItemSaldoInfo>(new CommandDefinition(
                """
                select i.id, i.quantidade TotalContratado, i.descricao Descricao,
                       coalesce((select sum(ri.quantidade) from sigov.compras_recebimento_item ri where ri.tenant_id=@T and ri.entidade_id=@E and ri.processo_item_id=i.id),0) JaRecebido
                from sigov.compras_processo_item i 
                where i.id=@ItemId and i.processo_id=@P and i.tenant_id=@T and i.entidade_id=@E 
                for update
                """,
                new { T = t, E = e, P = input.ProcessoId, ItemId = item.ProcessoItemId }, tx, cancellationToken: ct))
                ?? throw new KeyNotFoundException($"Item #{item.ProcessoItemId} não pertence a este processo de compra.");

            var saldoDisponivel = procItem.TotalContratado - procItem.JaRecebido;
            if (item.Quantidade > saldoDisponivel)
                throw new InvalidOperationException($"A quantidade informada ({item.Quantidade:0.####}) supera o saldo a receber ({saldoDisponivel:0.####}) do item '{procItem.Descricao}'.");
        }

        // Criar cabeçalho do recebimento
        long? lastMovId = null;
        var recebimentoId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_recebimento(tenant_id,entidade_id,processo_id,contrato_id,ata_id,documento,data_recebimento,status,created_by) 
            values(@T,@E,@P,@C,@A,@Doc,@Data,'PENDENTE',@U) returning id
            """,
            new { T = t, E = e, P = input.ProcessoId, C = input.ContratoId, A = input.AtaId, Doc = input.Documento.Trim(), Data = input.DataRecebimento, U = u },
            tx, cancellationToken: ct));

        // Integrar cada item no Almoxarifado (Estoque, Movimentação e Pendência se PERMANENTE)
        foreach (var item in input.Itens)
        {
            var mat = await c.QuerySingleOrDefaultAsync<MaterialBasico>(new CommandDefinition(
                "select id, codigo, descricao, unidade_medida UnidadeMedida, tipo from sigov.almoxarifado_material where tenant_id=@T and entidade_id=@E and id=@M and ativo",
                new { T = t, E = e, M = item.MaterialId }, tx, cancellationToken: ct))
                ?? throw new InvalidOperationException($"Material #{item.MaterialId} não encontrado ou inativo no Almoxarifado.");

            // 1. Saldo anterior e novo saldo no almoxarifado
            var saldoAnterior = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
                "select coalesce(quantidade,0) from sigov.almoxarifado_estoque where tenant_id=@T and entidade_id=@E and almoxarifado_id=@L and material_id=@M for update",
                new { T = t, E = e, L = input.AlmoxarifadoId, M = mat.Id }, tx, cancellationToken: ct));

            var saldoPosterior = saldoAnterior + item.Quantidade;

            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.almoxarifado_estoque(tenant_id,entidade_id,almoxarifado_id,material_id,quantidade) 
                values(@T,@E,@L,@M,@Q) 
                on conflict(tenant_id,entidade_id,almoxarifado_id,material_id) 
                do update set quantidade=sigov.almoxarifado_estoque.quantidade+excluded.quantidade, updated_at=now()
                """,
                new { T = t, E = e, L = input.AlmoxarifadoId, M = mat.Id, Q = item.Quantidade }, tx, cancellationToken: ct));

            // 2. Gravar movimentação de entrada/compra no Almoxarifado
            var movId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.almoxarifado_movimentacao(tenant_id,entidade_id,almoxarifado_id,material_id,tipo_movimentacao,subtipo,quantidade,saldo_anterior,saldo_posterior,documento_referencia,justificativa,created_by) 
                values(@T,@E,@L,@M,'ENTRADA','COMPRA',@Q,@Ant,@Post,@Doc,@Just,@U) returning id
                """,
                new { T = t, E = e, L = input.AlmoxarifadoId, M = mat.Id, Q = item.Quantidade, Ant = saldoAnterior, Post = saldoPosterior, Doc = input.Documento, Just = $"Recebimento de compra Doc {input.Documento} — Processo {proc.Numero}", U = u },
                tx, cancellationToken: ct));

            lastMovId = movId;

            // 3. Se for PERMANENTE: gerar pendência patrimonial no FUNC02 (sem tombamento no Compras!)
            long? pendenciaId = null;
            if (mat.Tipo == "PERMANENTE" || item.TipoMaterial == "PERMANENTE")
            {
                pendenciaId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                    """
                    insert into sigov.almoxarifado_pendencia_patrimonial(tenant_id,entidade_id,movimentacao_id,material_id,quantidade,status,observacao) 
                    values(@T,@E,@MovId,@M,@Q,'PENDENTE',@Obs) returning id
                    """,
                    new { T = t, E = e, MovId = movId, M = mat.Id, Q = item.Quantidade, Obs = $"Recebimento de compra NF {input.Documento} — Processo {proc.Numero}. Aguardando tombamento em Patrimônio." },
                    tx, cancellationToken: ct));
            }

            // 4. Inserir item do recebimento com referência à pendência
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.compras_recebimento_item(tenant_id,entidade_id,recebimento_id,processo_item_id,material_id,quantidade,valor_unitario,tipo_material,pendencia_patrimonial_id) 
                values(@T,@E,@RecId,@ProcItemId,@M,@Q,@V,@Tipo,@PendId)
                """,
                new { T = t, E = e, RecId = recebimentoId, ProcItemId = item.ProcessoItemId, M = mat.Id, Q = item.Quantidade, V = item.ValorUnitario, Tipo = mat.Tipo, PendId = pendenciaId },
                tx, cancellationToken: ct));
        }

        // Concluir status do recebimento para INTEGRADO
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_recebimento set almoxarifado_movimentacao_id=@LastMovId, status='INTEGRADO' where id=@RecId",
            new { LastMovId = lastMovId, RecId = recebimentoId }, tx, cancellationToken: ct));

        await Audit(c, tx, t, e, "RECEBIMENTO", recebimentoId, "REGISTRAR", null, input, u, corr, ct);
        await tx.CommitAsync(ct);

        log.LogInformation("Recebimento {RecebimentoId} do processo {ProcessoId} integrado ao almoxarifado com sucesso", recebimentoId, input.ProcessoId);
        return recebimentoId;
    }

    public async Task<IReadOnlyList<ProcessoRecebimentoSaldoDto>> ObterSaldosRecebimentoAsync(long t, long e, long processoId, CancellationToken ct)
    {
        await using var c = factory.CreateConnection();
        return (await c.QueryAsync<ProcessoRecebimentoSaldoDto>(new CommandDefinition(
            """
            select i.id ProcessoItemId, i.descricao Descricao, i.unidade Unidade, i.tipo_material_servico TipoMaterial,
                   i.quantidade QuantidadeContratada,
                   coalesce((select sum(ri.quantidade) from sigov.compras_recebimento_item ri where ri.tenant_id=@T and ri.entidade_id=@E and ri.processo_item_id=i.id),0) QuantidadeRecebida,
                   i.quantidade - coalesce((select sum(ri.quantidade) from sigov.compras_recebimento_item ri where ri.tenant_id=@T and ri.entidade_id=@E and ri.processo_item_id=i.id),0) SaldoPendente,
                   i.valor_estimado ValorUnitario,
                   (select m.id from sigov.almoxarifado_material m where m.tenant_id=@T and m.entidade_id=@E and (m.codigo ilike split_part(i.descricao, ' ', 1) or m.descricao ilike '%' || i.descricao || '%') and m.ativo limit 1) MaterialId
            from sigov.compras_processo_item i 
            where i.tenant_id=@T and i.entidade_id=@E and i.processo_id=@P 
            order by i.numero
            """, new { T = t, E = e, P = processoId }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<RecebimentoDto>> RecebimentosAsync(long t, long e, long? processoId, CancellationToken ct)
    {
        await using var c = factory.CreateConnection();
        var recs = (await c.QueryAsync<RecebimentoLinha>(new CommandDefinition(
            """
            select r.id, r.processo_id ProcessoId, p.numero ProcessoNumero, r.documento, r.data_recebimento DataRecebimento,
                   r.status, r.contrato_id ContratoId, r.ata_id AtaId, r.almoxarifado_movimentacao_id AlmoxarifadoMovimentacaoId
            from sigov.compras_recebimento r
            join sigov.compras_processo p on p.id = r.processo_id
            where r.tenant_id=@T and r.entidade_id=@E and (@P is null or r.processo_id=@P)
            order by r.data_recebimento desc, r.id desc
            """, new { T = t, E = e, P = processoId }, cancellationToken: ct))).AsList();

        var result = new List<RecebimentoDto>();
        foreach (var r in recs)
        {
            var itens = (await c.QueryAsync<RecebimentoItemDto>(new CommandDefinition(
                """
                select ri.id, ri.processo_item_id ProcessoItemId, coalesce(m.descricao, pi.descricao) MaterialDescricao,
                       ri.quantidade, ri.valor_unitario ValorUnitario, ri.tipo_material TipoMaterial, ri.pendencia_patrimonial_id PendenciaPatrimonialId
                from sigov.compras_recebimento_item ri
                join sigov.compras_processo_item pi on pi.id = ri.processo_item_id
                left join sigov.almoxarifado_material m on m.id = ri.material_id
                where ri.recebimento_id = @R and ri.tenant_id = @T and ri.entidade_id = @E
                """, new { R = r.Id, T = t, E = e }, cancellationToken: ct))).AsList();

            result.Add(new(r.Id, r.ProcessoId, r.ProcessoNumero, r.Documento, r.DataRecebimento, r.Status, r.ContratoId, r.AtaId, r.AlmoxarifadoMovimentacaoId, itens));
        }

        return result;
    }

    async Task<long> CriarInstrumento(string table, long t, long u, string corr, long e, long p, long f, string n, string o, decimal v, DateOnly ini, DateOnly fim, string? gestor, string? fiscal, string? obs, CancellationToken ct)
    {
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);

        var ok = await c.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            select exists(
                select 1 from sigov.compras_processo p 
                join sigov.compras_fornecedor f on f.id=@F and f.tenant_id=p.tenant_id and f.entidade_id=p.entidade_id 
                where p.id=@P and p.tenant_id=@T and p.entidade_id=@E and p.status in ('HOMOLOGADO','ADJUDICADO') and f.status='ATIVO')
            """,
            new { T = t, E = e, P = p, F = f }, tx, cancellationToken: ct));

        if (!ok) throw new InvalidOperationException("Processo deve estar homologado/adjudicado e fornecedor ativo.");

        var sql = table == "compras_contrato"
            ? $"insert into sigov.{table}(tenant_id,entidade_id,processo_id,fornecedor_id,numero,objeto,valor_contratado,data_inicio,data_fim,gestor,fiscal,observacoes,created_by,updated_by) values(@T,@E,@P,@F,@N,@O,@V,@Ini,@Fim,@Gestor,@Fiscal,@Obs,@U,@U) returning id"
            : $"insert into sigov.{table}(tenant_id,entidade_id,processo_id,fornecedor_id,numero,objeto,valor_global,data_inicio,data_fim,created_by,updated_by) values(@T,@E,@P,@F,@N,@O,@V,@Ini,@Fim,@U,@U) returning id";

        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { T = t, E = e, P = p, F = f, N = n, O = o, V = v, Ini = ini, Fim = fim, Gestor = N(gestor), Fiscal = N(fiscal), Obs = N(obs), U = u }, tx, cancellationToken: ct));
        await Audit(c, tx, t, e, table, id, "CRIAR", null, new { p, f, n, v, ini, fim }, u, corr, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    async Task<IReadOnlyList<CompraLinhaDto>> Lines(string table, long t, long e, CompraFiltro f, CancellationToken ct)
    {
        var valor = table == "compras_contrato" ? "valor_contratado" : "valor_global";
        await using var c = factory.CreateConnection();
        return (await c.QueryAsync<CompraLinhaDto>(new CommandDefinition(
            $"select x.id,x.numero,x.objeto Descricao,x.status,f.nome Fornecedor,x.{valor} Valor,x.data_inicio Inicio,x.data_fim Fim from sigov.{table} x join sigov.compras_fornecedor f on f.id=x.fornecedor_id where x.tenant_id=@T and x.entidade_id=@E and (@S is null or x.status=@S) and (@F is null or x.fornecedor_id=@F) order by x.data_fim",
            new { T = t, E = e, S = N(f.Status), F = f.FornecedorId }, cancellationToken: ct))).AsList();
    }

    async Task Phase(NpgsqlConnection c, NpgsqlTransaction tx, long t, long e, long id, string a, string n, long u, string corr, string? just, CancellationToken ct)
    {
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_processo set status=@N,updated_at=now(),updated_by=@U where id=@Id;insert into sigov.compras_processo_fase_historico(tenant_id,entidade_id,processo_id,status_anterior,status_novo,justificativa,usuario_id,correlation_id) values(@T,@E,@Id,@A,@N,@J,@U,@C)",
            new { T = t, E = e, Id = id, A = a, N = n, J = N(just), U = u, C = corr }, tx, cancellationToken: ct));
        await Audit(c, tx, t, e, "PROCESSO", id, "FASE", new { Status = a }, new { Status = n, Justificativa = just }, u, corr, ct);
    }

    async Task Tx(Func<NpgsqlConnection, NpgsqlTransaction, Task> fn, CancellationToken ct)
    {
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await fn(c, tx);
        await tx.CommitAsync(ct);
    }

    static Task Audit(NpgsqlConnection c, NpgsqlTransaction? tx, long t, long e, string ent, long id, string op, object? antes, object? depois, long u, string corr, CancellationToken ct) =>
        c.ExecuteAsync(new CommandDefinition(
            "insert into sigov.compras_auditoria(tenant_id,entidade_id,entidade,registro_id,operacao,antes,depois,usuario_id,correlation_id) values(@T,@E,@Ent,@Id,@Op,cast(@Antes as jsonb),cast(@Depois as jsonb),@U,@C)",
            new { T = t, E = e, Ent = ent, Id = id, Op = op, Antes = antes is null ? null : JsonSerializer.Serialize(antes), Depois = depois is null ? null : JsonSerializer.Serialize(depois), U = u, C = corr },
            tx, cancellationToken: ct));

    private static string SanitizeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var val = value.Replace("\"", "\"\"");
        if (val.StartsWith('=') || val.StartsWith('+') || val.StartsWith('-') || val.StartsWith('@') || val.StartsWith('\t') || val.StartsWith('\r'))
            return "\t" + val;
        return val;
    }

    static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    static string Digits(string s) => new(s.Where(char.IsDigit).ToArray());

    sealed record R(long Sol, long Sem, long Contr, long Atas, decimal Valor);
    sealed record P(string Status, string Objeto, DateOnly? DataAbertura, DateOnly? DataLimite);
    sealed record Q(long FornecedorId, decimal Valor, DateOnly Validade, string Status);
    sealed record MaterialBasico(long Id, string Codigo, string Descricao, string UnidadeMedida, string Tipo);
    sealed record ProcessoResumoAberto(long Id, string Numero, string Status);
    sealed record PoliticaValores(decimal EstoqueMinimo, decimal EstoqueAlvo, decimal? MultiploCompra, decimal? QuantidadeMinimaPedido);
    sealed record ProcessoHeader(long Id, string Numero, string Status);
    sealed record ProcessoItemSaldoInfo(long Id, decimal TotalContratado, string Descricao, decimal JaRecebido);
    sealed record RecebimentoLinha(long Id, long ProcessoId, string ProcessoNumero, string Documento, DateOnly DataRecebimento, string Status, long? ContratoId, long? AtaId, long? AlmoxarifadoMovimentacaoId);
}
