#r @"C:\MNSOFT\SIGOV-PLUS\src\Sigov.Api\bin\Debug\net10.0\Dapper.dll"
#r "C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App\10.0.12\Microsoft.Extensions.Logging.Abstractions.dll"
#r @"C:\MNSOFT\SIGOV-PLUS\src\Sigov.Api\bin\Debug\net10.0\Npgsql.dll"
#r @"C:\MNSOFT\SIGOV-PLUS\src\Sigov.Api\bin\Debug\net10.0\Sigov.Application.dll"
open Dapper
type P() =
  member val t = System.Guid.Parse("b0000001-0000-4000-8000-000000000001") with get, set
  member val us = System.Guid.Parse("700ef0a9-9a4c-4ba2-7156-d2a5edcc5f26") with get, set
let sql = "select a.id EtapaId,a.nivel Nivel,a.limite Limite,a.aprovador_id AprovadorId,(a.aprovador_id is null) Bloqueada,(a.aprovador_id=@us) DecisivelPorMim,a.requisicao_id RequisicaoId,r.numero Numero,r.valor_estimado Total,r.status StatusRequisicao,r.urgencia Urgencia,r.created_at SolicitadaEm,a.created_at CriadaEm,a.version Version from sigov.compras_empresarial_aprovacao a join sigov.compras_empresarial_requisicao r on r.tenant_id=a.tenant_id and r.id=a.requisicao_id and not r.is_deleted where a.tenant_id=@t and a.status='PENDENTE' and (a.aprovador_id=@us or a.aprovador_id is null) order by r.created_at desc,a.nivel asc,a.id offset 0 limit 10"
let cn = new Npgsql.NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=sigov_rc_validation;Username=postgres;Password=123456;Search Path=sigov")
do cn.Open()
try
  let rows = cn.Query<Sigov.Application.ComprasEmpresariais.AprovacaoFilaResumo>(sql, P())
  let list = Seq.toList rows
  printfn "ROWS=%d" list.Length
  for r in list do
    printfn "row: etapa=%O nivel=%d limite=%s aprovador=%O bloqueada=%b total=%s" r.EtapaId r.Nivel (string r.Limite) r.AprovadorId r.Bloqueada (string r.Total)
with ex -> printfn "EXN: %s" ex.Message
