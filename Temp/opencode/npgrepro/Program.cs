using Dapper;
using Npgsql;

var tenant = Guid.Parse("b0000001-0000-4000-8000-000000000001");
const string host = "172.18.0.2";
var cs = Environment.GetEnvironmentVariable("REPRO_CS") ?? $"Host={host};Port=5432;Database=postgres;Username=postgres;Password=123456;Pooling=true;Maximum Pool Size=50;Command Timeout=60;Application Name=sigov.npgrepro";
Console.WriteLine($"cs={cs.Replace("Password=**","Password=<hidden>").Substring(0, Math.Min(120, 200))}");

const string sql = "update sigov.compras_empresarial_aprovacao_politica set nome=@nome,esfera_governo=@esfera,tipo_entidade=@tipo,unidade_gestora=@ug,unidade_executora=@ue,ativo=true,is_deleted=false,version=version+1,updated_at=now(),updated_by=@us,correlation_id=@corr where tenant_id=@t and id=@id";

var anin = new { t = tenant, nome = "PolÃ­tica institucional RC50-68A (demo municipal)", esfera = "municipal", tipo = "secretaria_municipal", ug = "Sede da Secretaria Municipal de Compras (FictÃ­cia)", ue = "Sede da Secretaria Municipal de Compras (FictÃ­cia)", us = "usuario-repro", corr = "correlacao-repro" };
var reuso = 7L;

// Teste 1: forma atual do codigo (aninhado)
try
{
    await using var c1 = new NpgsqlConnection(cs);
    await c1.OpenAsync();
    await using var tx1 = await c1.BeginTransactionAsync();
    var n1 = await c1.ExecuteAsync(new CommandDefinition(sql, new { anin, id = reuso }, tx1));
    Console.WriteLine($"T1 nested: OK rows={n1}");
    await tx1.RollbackAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"T1 nested: EXCEPTION");
    Console.WriteLine(ex);
}

// Teste 2: forma achatada a partir dos membros de args
try
{
    await using var c2 = new NpgsqlConnection(cs);
    await c2.OpenAsync();
    await using var tx2 = await c2.BeginTransactionAsync();
    var n2 = await c2.ExecuteAsync(new CommandDefinition(sql, new { anin.t, anin.nome, anin.esfera, anin.tipo, anin.ug, anin.ue, anin.us, anin.corr, id = reuso }, tx2));
    Console.WriteLine($"T2 flat: OK rows={n2}");
    await tx2.RollbackAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"T2 flat: EXCEPTION");
    Console.WriteLine(ex);
}
Console.WriteLine("fim");



