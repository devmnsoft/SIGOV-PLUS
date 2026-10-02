using Dapper;
using Npgsql;
using NpgsqlTypes;

var host = Environment.GetEnvironmentVariable("REPRO_HOST") ?? "localhost";
string cs = $"Host={host};Port=5432;Database=postgres;Username=postgres;Password=123456;Search Path=sigov";

var g1 = Guid.NewGuid(); var g2 = Guid.NewGuid();
async Task Run(string nome, Func<Task> f)
{
    try { await f(); Console.WriteLine($"[OK]   {nome}"); }
    catch (Exception ex) { Console.WriteLine($"[FAIL] {nome} => {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); var full = ex.ToString().Split('\n'); for (int i = 0; i < Math.Min(4, full.Length); i++) if (full[i].Contains("POSITION") || full[i].Contains("MessageText") || full[i].Contains("Schema")) Console.WriteLine($"        {full[i].Trim()}"); if (ex.InnerException is not null) Console.WriteLine($"        inner: {ex.InnerException.Message.Split('\n')[0]}"); }
}

await using var cn = new NpgsqlConnection(cs);
await cn.OpenAsync();
var ident = await cn.ExecuteScalarAsync<string>(new CommandDefinition("select current_database()||'/'||current_user||'/'||inet_server_port()::text||'/'||(select count(*) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='sigov')::text", null));
Console.WriteLine("DB identity: " + ident);

// T1: positional record com nullables, aliases PascalCase, 0 linhas (espelha ListarPedidosAsync)
var t1sql = "select p.id Id,p.numero Numero,p.fornecedor_id FornecedorId,coalesce(fo.nome_fantasia,fo.razao_social) FornecedorNome,p.status Status,p.valor_total ValorTotal,p.previsao Previsao,p.created_at CriadoEm,p.requisicao_id RequisicaoId,p.cotacao_id CotacaoId,(select count(*) from sigov.compras_empresarial_pedido_item pi where pi.pedido_id=p.id and pi.tenant_id=p.tenant_id)::int Itens,p.version Version from sigov.compras_empresarial_pedido p left join sigov.compras_empresarial_fornecedor fo on fo.id=p.fornecedor_id and fo.tenant_id=p.tenant_id where p.tenant_id=@t and not p.is_deleted order by p.created_at desc,p.id desc offset 0 limit 10";
await Run("T1 positional+nullables PascalCase 0rows", async () => { await using var c2 = new NpgsqlConnection(cs); await c2.OpenAsync(); var m = await c2.QueryMultipleAsync(new CommandDefinition(t1sql, new { t = Guid.Parse("b0000001-0000-4000-8000-000000000001") })); await m.ReadAsync<PedidoR>(); });

// T2: mesmos aliases em minusculo
var t2sql = t1sql.Replace(" Id,", " id,").Replace(" Numero,", " numero,").Replace(" FornecedorId,", " fornecedorid,").Replace(" FornecedorNome,", " fornecedornome,").Replace(" Status,", " status,").Replace(" ValorTotal,", " valortotal,").Replace(" Previsao,", " previsao,").Replace(" CriadoEm,", " criadoem,").Replace(" RequisicaoId,", " requisicaoid,").Replace(" CotacaoId,", " cotacaoid,").Replace(" Itens,", " itens,").Replace(" Version from", " version from");
await Run("T2 positional+nullables lowercase 0rows", async () => { await using var c2 = new NpgsqlConnection(cs); await c2.OpenAsync(); var m = await c2.QueryMultipleAsync(new CommandDefinition(t2sql, new { t = Guid.Parse("b0000001-0000-4000-8000-000000000001") })); await m.ReadAsync<PedidoR>(); });

// T3: init-based record, aliases PascalCase
await Run("T3 init-props PascalCase 0rows", async () => { await using var c2 = new NpgsqlConnection(cs); await c2.OpenAsync(); var m = await c2.QueryMultipleAsync(new CommandDefinition(t1sql, new { t = Guid.Parse("b0000001-0000-4000-8000-000000000001") })); await m.ReadAsync<PedidoRInit>(); });

// T4: sem nullables PascalCase (espelha CotacaoResumo - funcionou ao vivo)
var t4sql = "select co.id Id,co.numero Numero,co.rodada Rodada,co.requisicao_id RequisicaoId,'x' NumeroRequisicao,co.status Status,co.prazo Prazo,co.created_at CriadoEm,co.version Version,(select count(*) from sigov.compras_empresarial_cotacao_item ci where ci.cotacao_id=co.id)::int Itens,(select count(*) from sigov.compras_empresarial_cotacao_convite cv where cv.cotacao_id=co.id)::int ConvitesAtivos,0::int Respostas from sigov.compras_empresarial_cotacao co where co.tenant_id=@t offset 0 limit 10";
await Run("T4 sem-nullables PascalCase 0rows", async () => { await using var c2 = new NpgsqlConnection(cs); await c2.OpenAsync(); var m = await c2.QueryMultipleAsync(new CommandDefinition(t4sql, new { t = Guid.Parse("b0000001-0000-4000-8000-000000000001") })); await m.ReadAsync<SemNull>(); });
Console.WriteLine("T4SQL[150..260]: " + t4sql.Substring(150, Math.Min(110, t4sql.Length - 150)));
await Run("T4b raw Npgsql mesma SQL", async () => { await using var c2 = new NpgsqlConnection(cs); await c2.OpenAsync(); await using var cmd = new NpgsqlCommand(t4sql.Replace("@t","@p")){ Connection = c2 }; cmd.Parameters.Add("@p", NpgsqlDbType.Uuid).Value = Guid.Parse("b0000001-0000-4000-8000-000000000001"); await cmd.ExecuteScalarAsync(); });

// T5: unnest(@ids) com Guid[] de 2 elementos
await Run("T5 unnest(@ids) Guid[2]", async () => { var q = "select count(*) from (select distinct id from unnest(@ids)) u where not exists(select 1 from sigov.estoque_produto ep where ep.tenant_id=@t and ep.id=u.id and ep.ativo)"; await cn.ExecuteScalarAsync<int>(new CommandDefinition(q, new { ids = new[] { g1, g2 }, t = Guid.NewGuid() })); });

// T5b: alias na set function (fix proposto)
await Run("T5b unnest(@ids) as id Guid[2]", async () => { var q = "select count(*) from (select distinct unnest(@ids) as id) u where not exists(select 1 from sigov.estoque_produto ep where ep.tenant_id=@t and ep.id=u.id and ep.ativo)"; await cn.ExecuteScalarAsync<int>(new CommandDefinition(q, new { ids = new[] { g1, g2 }, t = Guid.NewGuid() })); });

// T5c: alias com 1 elemento
await Run("T5c unnest(@ids) as id Guid[1]", async () => { var q = "select count(*) from (select distinct unnest(@ids) as id) u where not exists(select 1 from sigov.estoque_produto ep where ep.tenant_id=@t and ep.id=u.id and ep.ativo)"; await cn.ExecuteScalarAsync<int>(new CommandDefinition(q, new { ids = new[] { g1 }, t = Guid.NewGuid() })); });

// T6: any(@ids) com Guid[] de 2 elementos
await Run("T6 any(@ids) Guid[2]", async () => { var q = "select count(*) from sigov.compras_empresarial_fornecedor where tenant_id=@t and id=any(@ids)"; await cn.ExecuteScalarAsync<int>(new CommandDefinition(q, new { ids = new[] { g1, g2 }, t = Guid.NewGuid() })); });

// T7: statement 1 exato de recebimentos com filtros null (DateTime? nulos)
const string whereR = @"r.tenant_id=@t and (@fornecedor is null or fo.razao_social ilike @termFornecedor or fo.nome_fantasia ilike @termFornecedor) and (@pedido is null or p.numero ilike @termPedido) and (@status is null or r.status=@status) and (@almox is null or r.almoxarifado_id=@almox) and (@responsavel is null or r.created_by ilike @termResponsavel) and (@inicio is null or r.data_operacao>=@inicio) and (@fim is null or r.data_operacao<(@fim::date+1))";
object RArgs() => new { t = Guid.Parse("b0000001-0000-4000-8000-000000000001"), fornecedor = (string?)null, termFornecedor = "%%", pedido = (string?)null, termPedido = "%%", status = (string?)null, almox = (Guid?)null, responsavel = (string?)null, termResponsavel = "%%", inicio = (DateTime?)null, fim = (DateTime?)null };
string RecSql(string w) => $"select count(*) from sigov.compras_empresarial_recebimento r join sigov.compras_empresarial_pedido p on p.id=r.pedido_id and p.tenant_id=r.tenant_id join sigov.compras_empresarial_fornecedor fo on fo.id=p.fornecedor_id and fo.tenant_id=p.tenant_id where {w}";
await Run("T7 receb count filters null", async () => { await cn.ExecuteScalarAsync<long>(new CommandDefinition(RecSql(whereR), RArgs())); });

// T8: idem com casts explicitos
await Run("T8 receb count filters null + casts", async () => { await cn.ExecuteScalarAsync<long>(new CommandDefinition(RecSql(@"r.tenant_id=@t and (@fornecedor::text is null or fo.razao_social ilike @termFornecedor::text or fo.nome_fantasia ilike @termFornecedor::text) and (@pedido::text is null or p.numero ilike @termPedido::text) and (@status::text is null or r.status=@status::text) and (@almox::uuid is null or r.almoxarifado_id=@almox::uuid) and (@responsavel::text is null or r.created_by ilike @termResponsavel::text) and (@inicio::timestamp is null or r.data_operacao>=@inicio::timestamp) and (@fim::date is null or r.data_operacao<(@fim::date+1))"), RArgs())); });

// T9: positional record com SOMENTE string? (espelha FornecedorResumo - funcionou ao vivo)
await Run("T9 positional+string? PascalCase 0rows", async () => { var q = "select f.id Id,f.codigo Codigo,f.razao_social RazaoSocial,f.nome_fantasia NomeFantasia,f.documento_mascarado DocumentoMascarado,f.status Status,coalesce(f.score,0) Score,f.version Version from sigov.compras_empresarial_fornecedor f where f.tenant_id=@t and f.codigo='ZZZ-NAO-EXISTE'"; await cn.QueryAsync<FornecedorR>(new CommandDefinition(q, new { t = Guid.NewGuid() })); });

Console.WriteLine("DONE");

record PedidoR(Guid Id, string Numero, Guid? FornecedorId, string? FornecedorNome, string Status, decimal ValorTotal, DateTime? Previsao, DateTime CriadoEm, Guid? RequisicaoId, Guid? CotacaoId, int Itens, long Version);
record PedidoRInit { public Guid Id { get; set; } public string Numero { get; set; } = ""; public Guid? FornecedorId { get; set; } public string? FornecedorNome { get; set; } public string Status { get; set; } = ""; public decimal ValorTotal { get; set; } public DateTime? Previsao { get; set; } public DateTime CriadoEm { get; set; } public Guid? RequisicaoId { get; set; } public Guid? CotacaoId { get; set; } public int Itens { get; set; } public long Version { get; set; } }
record SemNull(Guid Id, string Numero, int Rodada, Guid RequisicaoId, string NumeroRequisicao, string Status, DateTime Prazo, DateTime CriadoEm, long Version, int Itens, int ConvitesAtivos, int Respostas);
record FornecedorR(Guid Id, string Codigo, string RazaoSocial, string? NomeFantasia, string DocumentoMascarado, string Status, decimal Score, long Version);
