using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Npgsql;
using Sigov.Application.Common;
using Sigov.Application.ComprasEmpresariais;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.ComprasEmpresariais;

/// <summary>
/// Jornada executável de aprovação de requisições de compra: fila por alçada, decisão
/// idempotente por ciclo, política persistente multi-esfera e relatório. O banco é a fonte
/// única de autoridade: permissões, alçadas e contexto institucional vêm exclusivamente do
/// schema. Ausência de configuração produz falha explícita e bloqueia a etapa — nunca
/// aprovação automática nem valor inventado.
/// </summary>
public sealed class AprovacaoRequisicaoRepository(NpgsqlConnectionFactory factory) : IAprovacaoRequisicaoRepository
{
    /// <summary>
    /// Cadeia de concessão espelhando exatamente <see cref="Security.PersistentAuthorizationEvaluator"/>:
    /// módulo/recurso/ação com curingas, vigência conferida com now() apenas onde o avaliador
    /// confere, e escopo por tenant/entidade/exercício/unidade apenas onde o avaliador aceita escopo.
    /// </summary>
    private const string CadeiaConcessao = @"from sigov.usuario_grupo ug
join sigov.grupo_acesso ga on ga.id=ug.grupo_acesso_id and ga.ativo and not ga.is_deleted
join sigov.grupo_perfil gp on gp.grupo_acesso_id=ga.id and gp.ativo and not gp.is_deleted
join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
join sigov.perfil_permissao pp on pp.perfil_acesso_id=pa.id and pp.ativo and not pp.is_deleted
join sigov.permissao p on p.id=pp.permissao_id and p.ativo and not p.is_deleted
where ug.usuario_id=u.id and ug.ativo and not ug.is_deleted
and (p.modulo=@Modulo or p.modulo='*')
and (p.recurso=@Recurso or p.recurso=@Modulo||'.'||@Recurso or @Recurso=@Modulo||'.'||p.recurso or p.recurso='*')
and (p.acao=@Acao or p.acao='*')
and (ug.vigencia_inicio is null or ug.vigencia_inicio<=now()) and (ug.vigencia_fim is null or ug.vigencia_fim>=now())
and (gp.vigencia_inicio is null or gp.vigencia_inicio<=now()) and (gp.vigencia_fim is null or gp.vigencia_fim>=now())
and (pp.vigencia_inicio is null or pp.vigencia_inicio<=now()) and (pp.vigencia_fim is null or pp.vigencia_fim>=now())
and (ug.tenant_id is null or ug.tenant_id=@TenantId) and (gp.tenant_id is null or gp.tenant_id=@TenantId) and (pp.tenant_id is null or pp.tenant_id=@TenantId)
and (ug.entidade_id is null or ug.entidade_id=@EntidadeId) and (gp.entidade_id is null or gp.entidade_id=@EntidadeId) and (pp.entidade_id is null or pp.entidade_id=@EntidadeId)
and (ug.exercicio_id is null or ug.exercicio_id=@ExercicioId) and (gp.exercicio_id is null or gp.exercicio_id=@ExercicioId) and (pp.exercicio_id is null or pp.exercicio_id=@ExercicioId)
and (ug.unidade_id is null or ug.unidade_id=@UnidadeId) and (gp.unidade_id is null or gp.unidade_id=@UnidadeId) and (pp.unidade_id is null or pp.unidade_id=@UnidadeId)";

    private static readonly string ElegibilidadeSql = $@"select distinct md5('sigov:usuario:'||u.id::text)::uuid as AprovadorSub
from sigov.usuario u
where u.ativo and not u.is_deleted
and exists(select 1 {CadeiaConcessao} and pp.efeito='PERMITIR' and (pp.alcada_valor is null or pp.alcada_valor>=@Alcada))
and not exists(select 1 {CadeiaConcessao} and pp.efeito='NEGAR');";

    public async Task<PagedResult<AprovacaoFilaResumo>> ListarFilaAsync(ComprasContext context, int pagina, int tamanho, string? busca, string? urgencia, CancellationToken ct)
    {
        var page = Math.Max(1, pagina);
        var size = Math.Clamp(tamanho, 1, 100);
        var termo = string.IsNullOrWhiteSpace(busca) ? null : busca.Trim();
        var urg = string.IsNullOrWhiteSpace(urgencia) ? null : urgencia.Trim().ToUpperInvariant();
        const string sql = @"select count(*)
from sigov.compras_empresarial_aprovacao a
join sigov.compras_empresarial_requisicao r on r.tenant_id=a.tenant_id and r.id=a.requisicao_id and not r.is_deleted
left join sigov.os_tecnico sol on (sol.tenant_id,sol.usuario_id)=(r.tenant_id,r.solicitante_id) and not sol.is_deleted
where a.tenant_id=@t and a.status='PENDENTE' and (a.aprovador_id=@us or a.aprovador_id is null)
and (@busca::text is null or r.numero ilike @term or coalesce(r.setor,'') ilike @term or sol.nome ilike @term)
and (@urgencia is null or r.urgencia=@urgencia);
select a.id EtapaId,a.nivel Nivel,a.limite Limite,a.aprovador_id AprovadorId,(a.aprovador_id is null) Bloqueada,(a.aprovador_id=@us) DecisivelPorMim,a.requisicao_id RequisicaoId,r.numero Numero,r.valor_estimado Total,r.status StatusRequisicao,r.urgencia Urgencia,r.created_at SolicitadaEm,a.created_at CriadaEm,a.version Version,coalesce(sol.nome,r.solicitante_id::text) SolicitanteNome,r.setor Setor,case when a.aprovador_id is null then 'Aguardando configuração institucional' when a.nivel>=(select coalesce(max(b.nivel),0) from sigov.compras_empresarial_aprovacao b where b.tenant_id=a.tenant_id and b.requisicao_id=a.requisicao_id and b.ciclo=a.ciclo) then 'Decisão encerra o ciclo' else 'Próxima etapa: nível '||(a.nivel+1)::text end ProximaAcao
from sigov.compras_empresarial_aprovacao a
join sigov.compras_empresarial_requisicao r on r.tenant_id=a.tenant_id and r.id=a.requisicao_id and not r.is_deleted
left join sigov.os_tecnico sol on (sol.tenant_id,sol.usuario_id)=(r.tenant_id,r.solicitante_id) and not sol.is_deleted
where a.tenant_id=@t and a.status='PENDENTE' and (a.aprovador_id=@us or a.aprovador_id is null)
and (@busca::text is null or r.numero ilike @term or coalesce(r.setor,'') ilike @term or sol.nome ilike @term)
and (@urgencia is null or r.urgencia=@urgencia)
order by r.created_at desc,a.nivel asc,a.id offset @off limit @lim";
        await using var connection = factory.CreateConnection();
        using var reader = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { t = context.TenantId, us = context.UsuarioId, busca = termo, term = $"%{termo}%", urgencia = urg, off = (page - 1) * size, lim = size }, cancellationToken: ct));
        var total = await reader.ReadSingleAsync<long>();
        return new PagedResult<AprovacaoFilaResumo>((await reader.ReadAsync<AprovacaoFilaResumo>()).AsList(), page, size, total);
    }

    public async Task<PagedResult<AprovacaoPainelResumo>> ListarDevolvidasAsync(ComprasContext context, int pagina, int tamanho, CancellationToken ct)
        => await PainelAsync(context, pagina, tamanho, "= 'DEVOLVIDA'", "= 'DEVOLVIDA'", ct);

    public async Task<PagedResult<AprovacaoPainelResumo>> ListarConcluidasAsync(ComprasContext context, int pagina, int tamanho, CancellationToken ct)
        => await PainelAsync(context, pagina, tamanho, "in ('APROVADA','REJEITADA')", "in ('APROVADO','REJEITADA')", ct);

    private async Task<PagedResult<AprovacaoPainelResumo>> PainelAsync(ComprasContext context, int pagina, int tamanho, string filtroRequisicao, string filtroEtapa, CancellationToken ct)
    {
        var page = Math.Max(1, pagina);
        var size = Math.Clamp(tamanho, 1, 100);
        var sql = $@"select count(*)
from sigov.compras_empresarial_requisicao r
where r.tenant_id=@t and not r.is_deleted and r.status {filtroRequisicao}
and (r.solicitante_id=@us or exists(select 1 from sigov.compras_empresarial_aprovacao x where x.tenant_id=r.tenant_id and x.requisicao_id=r.id and (x.aprovador_id=@us or x.aprovador_id is null)));
select r.id RequisicaoId,r.numero Numero,r.valor_estimado Total,r.status StatusRequisicao,r.urgencia Urgencia,r.created_at SolicitadaEm,d.decidido_em DecididaEm,d.motivo MotivoFinal,d.aprovador_id Aprovador
from sigov.compras_empresarial_requisicao r
left join lateral(select d.motivo,d.decidido_em,d.aprovador_id from sigov.compras_empresarial_aprovacao d where d.tenant_id=r.tenant_id and d.requisicao_id=r.id and d.status {filtroEtapa} order by d.id desc limit 1) d on true
where r.tenant_id=@t and not r.is_deleted and r.status {filtroRequisicao}
and (r.solicitante_id=@us or exists(select 1 from sigov.compras_empresarial_aprovacao x where x.tenant_id=r.tenant_id and x.requisicao_id=r.id and (x.aprovador_id=@us or x.aprovador_id is null)))
order by r.created_at desc,r.id offset @off limit @lim";
        await using var connection = factory.CreateConnection();
        using var reader = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { t = context.TenantId, us = context.UsuarioId, off = (page - 1) * size, lim = size }, cancellationToken: ct));
        var total = await reader.ReadSingleAsync<long>();
        return new PagedResult<AprovacaoPainelResumo>((await reader.ReadAsync<AprovacaoPainelResumo>()).AsList(), page, size, total);
    }

    public async Task<AprovacaoEtapaDetalhe?> ObterDetalheAsync(ComprasContext context, Guid etapaId, CancellationToken ct)
    {
        // Fail-closed: o visor precisa ser o aprovador designado da etapa, o solicitante da
        // requisição, o responsável por uma etapa já decidida do mesmo tenant/requisição,
        // ou ver uma etapa bloqueada (aprovador_id NULL), que é pública dentro da fila.
        const string visibilidade = @"and (a.aprovador_id=@us or a.aprovador_id is null or r.solicitante_id=@us or exists(select 1 from sigov.compras_empresarial_aprovacao x where x.tenant_id=a.tenant_id and x.requisicao_id=a.requisicao_id and x.aprovador_id=@us and x.status not in('PENDENTE','CANCELADO')))";
        const string sql = $@"select count(*)
from sigov.compras_empresarial_aprovacao a
join sigov.compras_empresarial_requisicao r on r.tenant_id=a.tenant_id and r.id=a.requisicao_id and not r.is_deleted
where a.tenant_id=@t and a.id=@id {visibilidade};
select a.id EtapaId,a.ciclo Ciclo,a.nivel Nivel,a.limite Limite,a.status StatusEtapa,a.aprovador_id AprovadorId,coalesce(os.nome,a.aprovador_id::text) AprovadorNome,(a.aprovador_id is null) Bloqueada,(a.aprovador_id=@us) DecisivelPorMim,a.version VersionEtapa,a.decidido_em DecididaEm,a.motivo Motivo,r.id RequisicaoId,r.numero Numero,r.status StatusRequisicao,r.urgencia Urgencia,r.setor Setor,coalesce(sol.nome,r.solicitante_id::text) SolicitanteNome,r.created_at SolicitadaEm,r.valor_estimado Total,a.regra_snapshot::text RegraSnapshot
from sigov.compras_empresarial_aprovacao a
join sigov.compras_empresarial_requisicao r on r.tenant_id=a.tenant_id and r.id=a.requisicao_id and not r.is_deleted
left join sigov.os_tecnico sol on (sol.tenant_id,sol.usuario_id)=(r.tenant_id,r.solicitante_id) and not sol.is_deleted
left join sigov.os_tecnico os on (os.tenant_id,os.usuario_id)=(a.tenant_id,a.aprovador_id) and not os.is_deleted
where a.tenant_id=@t and a.id=@id {visibilidade};
select i.id Id,i.ordem Ordem,i.tipo Tipo,i.descricao Descricao,i.especificacao Especificacao,i.unidade Unidade,i.quantidade Quantidade,i.valor_estimado ValorEstimado,i.permite_parcial PermiteParcial,i.exige_inspecao ExigeInspecao
from sigov.compras_empresarial_requisicao_item i
where i.tenant_id=@t and not i.is_deleted and i.requisicao_id=(select requisicao_id from sigov.compras_empresarial_aprovacao where tenant_id=@t and id=@id)
order by i.ordem,i.id;
select b.id EtapaId,b.nivel Nivel,b.limite Limite,b.status Status,b.aprovador_id AprovadorId,coalesce(os.nome,b.aprovador_id::text) AprovadorNome,b.decidido_em DecididaEm,b.motivo Motivo
from sigov.compras_empresarial_aprovacao b
left join sigov.os_tecnico os on (os.tenant_id,os.usuario_id)=(b.tenant_id,b.aprovador_id) and not os.is_deleted
where b.tenant_id=@t and b.requisicao_id=(select requisicao_id from sigov.compras_empresarial_aprovacao where tenant_id=@t and id=@id)
and b.ciclo=(select ciclo from sigov.compras_empresarial_aprovacao where tenant_id=@t and id=@id)
order by b.nivel asc,b.id asc;
select acao,detalhes::text Detalhes,created_at CriadoEm
from sigov.compras_empresarial_historico
where tenant_id=@t and aggregate_type='REQUISICAO' and aggregate_id=(select requisicao_id from sigov.compras_empresarial_aprovacao where tenant_id=@t and id=@id)
order by created_at desc,id desc limit 50";
        await using var connection = factory.CreateConnection();
        using var reader = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { t = context.TenantId, us = context.UsuarioId, id = etapaId }, cancellationToken: ct));
        var visivel = await reader.ReadSingleAsync<long>();
        if (visivel != 1) return null;
        var cab = await reader.ReadSingleAsync<CabecalhoEtapa>();
        var itens = (await reader.ReadAsync<RequisicaoItemDetalhe>()).AsList();
        var etapas = (await reader.ReadAsync<AprovacaoEtapaLinha>()).AsList();
        var historico = (await reader.ReadAsync<RequisicaoHistorico>()).AsList();
        historico.Reverse();
        var proxima = etapas.FirstOrDefault(e => e.Status == "PENDENTE" && e.Nivel >= cab.Nivel);
        return new AprovacaoEtapaDetalhe(cab.EtapaId, cab.Ciclo, cab.Nivel, cab.Limite, cab.StatusEtapa, cab.AprovadorId, cab.AprovadorNome, cab.Bloqueada, cab.DecisivelPorMim, cab.VersionEtapa, cab.DecididaEm, cab.Motivo, cab.RequisicaoId, cab.Numero, cab.StatusRequisicao, cab.Urgencia, cab.Setor, cab.SolicitanteNome, cab.SolicitadaEm, cab.Total, cab.RegraSnapshot, itens, etapas, proxima, historico);
    }

    private sealed record CabecalhoEtapa(Guid EtapaId,int Ciclo,int Nivel,decimal Limite,string StatusEtapa,Guid? AprovadorId,string? AprovadorNome,bool Bloqueada,bool DecisivelPorMim,long VersionEtapa,DateTime? DecididaEm,string? Motivo,Guid RequisicaoId,string Numero,string StatusRequisicao,string Urgencia,string? Setor,string SolicitanteNome,DateTime SolicitadaEm,decimal Total,string? RegraSnapshot);

    public async Task<AprovacaoDecisaoResultado> DecidirAsync(ComprasContext context, Guid etapaId, AprovacaoDecisaoRequest request, CancellationToken ct)
    {
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        try
        {
            await LockAsync(connection, tx, context, $"ETAPA|{etapaId:D}", ct);
            var decisao = (request.Decisao ?? string.Empty).Trim().ToUpperInvariant();
            var motivo = request.Motivo?.Trim();
            var hash = Sha256($"{etapaId:D}|{decisao}|{(decisao == "APROVAR" ? string.Empty : motivo)}|{request.Version}");
            var anterior = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("select request_hash from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='APROVACAO_DECISAO' and chave=@key", new { t = context.TenantId, key = request.IdempotencyKey }, tx, cancellationToken: ct));
            if (anterior is not null)
            {
                if (anterior != hash) throw new ComprasConcurrencyException("A chave de idempotência já foi usada com conteúdo diferente.");
                var estado = await connection.QuerySingleAsync<(string EtapaStatus, string RequisicaoStatus)>(new CommandDefinition("select a.status,r.status from sigov.compras_empresarial_aprovacao a join sigov.compras_empresarial_requisicao r on r.tenant_id=a.tenant_id and r.id=a.requisicao_id where a.tenant_id=@t and a.id=@id", new { t = context.TenantId, id = etapaId }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
                return new AprovacaoDecisaoResultado(etapaId, estado.EtapaStatus, estado.RequisicaoStatus, true);
            }
            var etapa = await connection.QuerySingleOrDefaultAsync<EtapaAtiva>(new CommandDefinition("select status Status,version Version,aprovador_id AprovadorId,ciclo Ciclo,requisicao_id RequisicaoId,nivel Nivel from sigov.compras_empresarial_aprovacao where tenant_id=@t and id=@id for update", new { t = context.TenantId, id = etapaId }, tx, cancellationToken: ct));
            if (etapa is null) throw new KeyNotFoundException("Etapa de aprovação não encontrada no contexto autorizado.");
            if (etapa.Status != "PENDENTE") throw new InvalidOperationException("Esta etapa de aprovação já foi decidida.");
            if (etapa.Version != request.Version) throw new ComprasConcurrencyException("Versão desatualizada; recarregue a fila de aprovações e tente novamente.");
            if (etapa.AprovadorId is null) throw new InvalidOperationException("Esta etapa está bloqueada e aguarda configuração institucional antes de qualquer decisão.");
            if (etapa.AprovadorId != context.UsuarioId) throw new InvalidOperationException("Você não é o aprovador designado para esta etapa.");
            var requisicao = await connection.QuerySingleOrDefaultAsync<(string Status, long Version)?>(new CommandDefinition("select status,version from sigov.compras_empresarial_requisicao where tenant_id=@t and id=@id for update", new { t = context.TenantId, id = etapa.RequisicaoId }, tx, cancellationToken: ct));
            if (requisicao is null || requisicao.Value.Status != "PENDENTE_APROVACAO") throw new InvalidOperationException("A requisição vinculada não está pendente de aprovação.");

            var etapaDestino = decisao switch { "APROVAR" => "APROVADO", "REJEITAR" => "REJEITADA", "DEVOLVER" => "DEVOLVIDA", _ => throw new ArgumentException("Decisão inválida.") };
            var requisicaoDestino = decisao == "APROVAR" ? "PENDENTE_APROVACAO" : decisao == "REJEITAR" ? "REJEITADA" : "DEVOLVIDA";
            var cicloCompleto = false;
            if (decisao == "APROVAR")
            {
                var maximoNivel = await connection.ExecuteScalarAsync<int>(new CommandDefinition("select coalesce(max(nivel),0) from sigov.compras_empresarial_aprovacao where tenant_id=@t and requisicao_id=@rq and ciclo=@ciclo", new { t = context.TenantId, rq = etapa.RequisicaoId, ciclo = etapa.Ciclo }, tx, cancellationToken: ct));
                cicloCompleto = etapa.Nivel >= maximoNivel;
                if (cicloCompleto) requisicaoDestino = "APROVADA";
            }
            var encerraCiclo = decisao != "APROVAR" || cicloCompleto;
            await connection.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_aprovacao set status=@ns,motivo=@motivo,decidido_em=now(),updated_at=now(),updated_by=@us,correlation_id=@corr,version=version+1 where tenant_id=@t and id=@id", new { t = context.TenantId, id = etapaId, ns = etapaDestino, motivo = decisao == "APROVAR" ? (string?)null : motivo, us = context.UsuarioId.ToString(), corr = context.CorrelationId }, tx, cancellationToken: ct));
            await connection.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_aprovacao set status='CANCELADO',updated_at=now(),updated_by=@us,correlation_id=@corr,version=version+1 where tenant_id=@t and requisicao_id=@rq and ciclo=@ciclo and id<>@id and status='PENDENTE' and (@EncerraCiclo or nivel=@nivel)", new { t = context.TenantId, rq = etapa.RequisicaoId, ciclo = etapa.Ciclo, id = etapaId, EncerraCiclo = encerraCiclo, nivel = etapa.Nivel, us = context.UsuarioId.ToString(), corr = context.CorrelationId }, tx, cancellationToken: ct));
            if (requisicaoDestino != "PENDENTE_APROVACAO")
            {
                await connection.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_requisicao set status=@ns,version=version+1,updated_at=now(),updated_by=@us,correlation_id=@corr where tenant_id=@t and id=@id", new { t = context.TenantId, id = etapa.RequisicaoId, ns = requisicaoDestino, us = context.UsuarioId.ToString(), corr = context.CorrelationId }, tx, cancellationToken: ct));
            }
            var evento = decisao == "APROVAR" ? (cicloCompleto ? "APROVADA" : "ETAPA_APROVADA") : decisao == "REJEITAR" ? "REJEITADA" : "DEVOLVIDA";
            await connection.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_historico(tenant_id,aggregate_type,aggregate_id,acao,detalhes,created_by,correlation_id) values(@t,'REQUISICAO',@rq,@evento,jsonb_build_object('ciclo',@ciclo,'etapa',@nivel,'decisao',@decisao,'motivo',@motivo),@us,@corr)", new { t = context.TenantId, rq = etapa.RequisicaoId, evento, ciclo = etapa.Ciclo, nivel = etapa.Nivel, decisao, motivo, us = context.UsuarioId.ToString(), corr = context.CorrelationId }, tx, cancellationToken: ct));
            if (requisicaoDestino != "PENDENTE_APROVACAO")
            {
                var usuarioNucleo = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("select id from sigov.usuario where md5('sigov:usuario:'||id::text)::uuid=@us limit 1", new { us = context.UsuarioId }, tx, cancellationToken: ct));
                await ConcluirPendenciasAsync(connection, tx, context, etapa.RequisicaoId, usuarioNucleo, $"Decisão {evento} registrada pelo aprovador.", etapa.Ciclo, ct);
            }
            await connection.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_idempotencia(tenant_id,operacao,chave,recurso_id,request_hash) values(@t,'APROVACAO_DECISAO',@key,@id,@hash)", new { t = context.TenantId, key = request.IdempotencyKey, id = etapaId, hash }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
            return new AprovacaoDecisaoResultado(etapaId, etapaDestino, requisicaoDestino, false);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PoliticaAtivaResumo?> ObterPoliticaAsync(ComprasContext context, CancellationToken ct)
    {
        await using var connection = factory.CreateConnection();
        var politica = await connection.QuerySingleOrDefaultAsync<PoliticaAtivaRow>(new CommandDefinition("select id Id,nome Nome,esfera_governo EsferaGoverno,tipo_entidade TipoEntidade,unidade_gestora UnidadeGestora,unidade_executora UnidadeExecutora,updated_at AtualizadaEm from sigov.compras_empresarial_aprovacao_politica where tenant_id=@t and ativo and not is_deleted order by id desc limit 1", new { t = context.TenantId }, cancellationToken: ct));
        if (politica is null) return null;
        var niveis = (await connection.QueryAsync<NivelPoliticaResumo>(new CommandDefinition("select ordem Ordem,limite Limite from sigov.compras_empresarial_aprovacao_politica_nivel where tenant_id=@t and politica_id=@pol and not is_deleted order by ordem", new { t = context.TenantId, pol = politica.Id }, cancellationToken: ct))).AsList();
        return new PoliticaAtivaResumo(politica.Id, politica.Nome, politica.EsferaGoverno, politica.TipoEntidade, politica.UnidadeGestora, politica.UnidadeExecutora, politica.AtualizadaEm, niveis);
    }

    public async Task<PoliticaSalvaResultado> SalvarPoliticaAsync(ComprasContext context, SalvarPoliticaRequest request, string key, CancellationToken ct)
    {
        var nome = (request.Nome ?? string.Empty).Trim();
        var limites = request.Niveis!.Select(n => n.Limite).OrderBy(v => v).Distinct().ToList();
        var hash = Sha256($"{nome}|{string.Join(";", limites.Select(v => v.ToString("0.00", CultureInfo.InvariantCulture)))}");
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        try
        {
            await LockAsync(connection, tx, context, "POLITICA|SALVAR", ct);
            var anterior = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("select request_hash from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='APROVACAO_POLITICA_SALVAR' and chave=@key", new { t = context.TenantId, key }, tx, cancellationToken: ct));
            if (anterior is not null)
            {
                if (anterior != hash) throw new ComprasConcurrencyException("A chave de idempotência já foi usada com conteúdo diferente.");
                var reutilizada = await connection.QuerySingleAsync<(long Id, string Nome)>(new CommandDefinition("select id,nome from sigov.compras_empresarial_aprovacao_politica where tenant_id=@t order by id desc limit 1", new { t = context.TenantId }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
                return new PoliticaSalvaResultado(reutilizada.Item1, reutilizada.Item2, true);
            }
            var contextoInstitucional = await DevolucaoCompraRepository.ContextoInstitucionalAsync(connection, tx, context.TenantId, context.UsuarioId, ct);
            if (contextoInstitucional is null) throw new InvalidOperationException("Configuração institucional obrigatória ausente para o contexto autorizado.");
            if (contextoInstitucional.UnidadeGestora.Length > 120 || contextoInstitucional.UnidadeExecutora.Length > 120) throw new InvalidOperationException("O nome da unidade organizacional excede o limite explícito da política de aprovação.");
            var reuso = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("select id from sigov.compras_empresarial_aprovacao_politica where tenant_id=@t order by id desc limit 1", new { t = context.TenantId }, tx, cancellationToken: ct));
            var colisao = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.compras_empresarial_aprovacao_politica where tenant_id=@t and nome=@nome and (@reuso is null or id<>@reuso))", new { t = context.TenantId, nome, reuso }, tx, cancellationToken: ct));
            if (colisao) throw new InvalidOperationException("Já existe outra política de aprovação com o mesmo nome neste contexto.");
            var args = new { t = context.TenantId, nome, esfera = contextoInstitucional.EsferaGoverno, tipo = contextoInstitucional.TipoEntidade, ug = contextoInstitucional.UnidadeGestora, ue = contextoInstitucional.UnidadeExecutora, us = context.UsuarioId.ToString(), corr = context.CorrelationId };
            long politicaId;
            if (reuso.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_aprovacao_politica set nome=@nome,esfera_governo=@esfera,tipo_entidade=@tipo,unidade_gestora=@ug,unidade_executora=@ue,ativo=true,is_deleted=false,version=version+1,updated_at=now(),updated_by=@us,correlation_id=@corr where tenant_id=@t and id=@id", new { args, id = reuso.Value }, tx, cancellationToken: ct));
                politicaId = reuso.Value;
            }
            else
            {
                politicaId = await connection.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.compras_empresarial_aprovacao_politica(tenant_id,nome,esfera_governo,tipo_entidade,unidade_gestora,unidade_executora,ativo,created_by,updated_by,correlation_id) values(@t,@nome,@esfera,@tipo,@ug,@ue,true,@us,@us,@corr) returning id", args, tx, cancellationToken: ct));
            }
            await connection.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_aprovacao_politica_nivel set is_deleted=true,updated_at=now(),updated_by=@us where tenant_id=@t and politica_id=@pol and not is_deleted", new { t = context.TenantId, pol = politicaId, us = context.UsuarioId.ToString() }, tx, cancellationToken: ct));
            for (var ordem = 1; ordem <= limites.Count; ordem++)
            {
                await connection.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_aprovacao_politica_nivel(tenant_id,politica_id,ordem,limite,created_by,updated_by,correlation_id) values(@t,@pol,@ordem,@limite,@us,@us,@corr)", new { t = context.TenantId, pol = politicaId, ordem, limite = limites[ordem - 1], us = context.UsuarioId.ToString(), corr = context.CorrelationId }, tx, cancellationToken: ct));
            }
            await connection.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_idempotencia(tenant_id,operacao,chave,recurso_id,request_hash) values(@t,'APROVACAO_POLITICA_SALVAR',@key,md5('sigov:politica_aprovacao:'||@pol::text)::uuid,@hash)", new { t = context.TenantId, key, pol = politicaId, hash }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
            return new PoliticaSalvaResultado(politicaId, nome, false);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PagedResult<AprovacaoRelatorioLinha>> ListarRelatorioAsync(ComprasContext context, int pagina, int tamanho, CancellationToken ct)
    {
        var page = Math.Max(1, pagina);
        var size = Math.Clamp(tamanho, 1, 100);
        const string sql = @"select count(*)
from sigov.compras_empresarial_aprovacao a
join sigov.compras_empresarial_requisicao r on r.tenant_id=a.tenant_id and r.id=a.requisicao_id and not r.is_deleted
where a.tenant_id=@t;
select r.numero Numero,a.ciclo Ciclo,a.nivel Etapa,a.status SituacaoEtapa,a.limite Alcada,a.aprovador_id::text AprovadorSub,r.status StatusRequisicao,r.valor_estimado Total,a.created_at CriadaEm,a.decidido_em DecididaEm,a.motivo Motivo
from sigov.compras_empresarial_aprovacao a
join sigov.compras_empresarial_requisicao r on r.tenant_id=a.tenant_id and r.id=a.requisicao_id and not r.is_deleted
where a.tenant_id=@t order by a.created_at desc,a.nivel asc,a.id offset @off limit @lim";
        await using var connection = factory.CreateConnection();
        using var reader = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { t = context.TenantId, off = (page - 1) * size, lim = size }, cancellationToken: ct));
        var total = await reader.ReadSingleAsync<long>();
        return new PagedResult<AprovacaoRelatorioLinha>((await reader.ReadAsync<AprovacaoRelatorioLinha>()).AsList(), page, size, total);
    }

    /// <summary>
    /// Fail-closed: o envio/decisão depende de um único vínculo institucional ativo.
    /// Zero ou mais de um vínculo é falha explícita, nunca vínculo inventado.
    /// </summary>
    internal static async Task<long> ResolverTenantNucleoAsync(NpgsqlConnection cn, NpgsqlTransaction tx, Guid tenant, CancellationToken ct)
    {
        var vinculos = await cn.ExecuteScalarAsync<int>(new CommandDefinition("select count(*) from sigov.enterprise_tenant_mapping where enterprise_tenant_id=@t and ativo", new { t = tenant }, tx, cancellationToken: ct));
        if (vinculos != 1) throw new InvalidOperationException("O tenant empresarial não possui um único vínculo institucional ativo; a operação foi cancelada com segurança.");
        return await cn.ExecuteScalarAsync<long>(new CommandDefinition("select core_tenant_id from sigov.enterprise_tenant_mapping where enterprise_tenant_id=@t and ativo", new { t = tenant }, tx, cancellationToken: ct));
    }

    /// <summary>
    /// Aprovadores elegíveis por alçada, espelhando a regra canônica de autorização:
    /// existe PERMITIR para o triplete módulo/recurso/ação (com curingas) sem alçada ou com
    /// alçada suficiente, e não existe NEGAR para o mesmo triplete. Saída: sub projetado MD5.
    /// </summary>
    internal static async Task<IReadOnlyList<Guid>> AprovadoresElegiveisAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long tenantNucleo, decimal limiteAlcada, CancellationToken ct)
    {
        var args = new { Modulo = "compras_empresariais", Recurso = "aprovacoes", Acao = "aprovar", Alcada = limiteAlcada, TenantId = tenantNucleo, EntidadeId = (long?)null, ExercicioId = (long?)null, UnidadeId = (long?)null };
        var subs = await cn.QueryAsync<Guid>(new CommandDefinition(ElegibilidadeSql, args, tx, cancellationToken: ct));
        return subs.AsList();
    }

    /// <summary>
    /// Abre (ou atualiza) a pendência operacional espelhando o padrão da inspeção de recebimento;
    /// afetadas diferente de 1 cancela a operação com segurança.
    /// </summary>
    internal static async Task RegistrarPendenciaAsync(NpgsqlConnection cn, NpgsqlTransaction tx, Guid tenant, Guid requisicaoId, string tipo, string titulo, string descricao, CancellationToken ct)
    {
        var afetadas = await cn.ExecuteAsync(new CommandDefinition(@"insert into sigov.pendencia_operacional(tenant_id,modulo,recurso,tipo,entidade,entidade_id,gravidade,titulo,descricao,rota_acao,status)
select m.core_tenant_id,'COMPRAS_EMPRESARIAIS','APROVACAO',@tipo,'compras_empresarial_requisicao',@rq::text,'ALTA',@titulo,@descricao,'/ComprasEmpresariais/Aprovacoes','ABERTA'
from sigov.enterprise_tenant_mapping m where m.enterprise_tenant_id=@t and m.ativo
on conflict(tenant_id,modulo,tipo,entidade,entidade_id) where status in('ABERTA','EM_TRATAMENTO') do update set titulo=excluded.titulo,descricao=excluded.descricao,rota_acao=excluded.rota_acao", new { t = tenant, rq = requisicaoId, tipo, titulo, descricao }, tx, cancellationToken: ct));
        if (afetadas != 1) throw new InvalidOperationException("A pendência de aprovação não pôde ser registrada no vínculo institucional ativo; a operação foi cancelada com segurança.");
    }

    private static async Task ConcluirPendenciasAsync(NpgsqlConnection cn, NpgsqlTransaction tx, ComprasContext context, Guid requisicaoId, long? usuarioNucleo, string justificativa, int ciclo, CancellationToken ct)
    {
        await cn.ExecuteAsync(new CommandDefinition(@"with atualizada as (update sigov.pendencia_operacional p set status='RESOLVIDA',resolved_at=now(),updated_at=now(),versao=versao+1 from sigov.enterprise_tenant_mapping m where m.enterprise_tenant_id=@t and m.ativo and p.tenant_id=m.core_tenant_id and p.modulo='COMPRAS_EMPRESARIAIS' and p.tipo in('APROVACAO_SEM_POLITICA','APROVACAO_SEM_APROVADOR') and p.entidade='compras_empresarial_requisicao' and p.entidade_id=@rq::text and p.status in('ABERTA','EM_TRATAMENTO') returning p.id,p.tenant_id,p.versao) insert into sigov.governanca_ocorrencia_historico(tenant_id,ocorrencia_tipo,ocorrencia_id,evento,usuario_id,justificativa,dados_depois) select tenant_id,'PENDENCIA',id,'RESOLVIDA_NA_ORIGEM',@usuario,left(@justificativa,1000),jsonb_build_object('requisicao_id',@rq,'ciclo',@ciclo) from atualizada", new { t = context.TenantId, rq = requisicaoId, usuario = usuarioNucleo, justificativa, ciclo }, tx, cancellationToken: ct));
    }

    private static async Task LockAsync(NpgsqlConnection cn, NpgsqlTransaction tx, ComprasContext context, string rotulo, CancellationToken ct)
        => await cn.ExecuteAsync(new CommandDefinition("select pg_advisory_xact_lock(hashtextextended(@chaveLock,0))", new { chaveLock = $"{context.TenantId:D}|APROVACAO|{rotulo}" }, tx, cancellationToken: ct));

    private static string Sha256(string valor) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor))).ToLowerInvariant();

    private sealed record EtapaAtiva(string Status, long Version, Guid? AprovadorId, int Ciclo, Guid RequisicaoId, int Nivel);
    private sealed record PoliticaAtivaRow(long Id, string Nome, string EsferaGoverno, string TipoEntidade, string? UnidadeGestora, string? UnidadeExecutora, DateTime AtualizadaEm);
}
