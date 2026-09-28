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
let short (m:string) = if m.Length <= 100 then m else m.Substring(0,100)
let run (name:string) (f: unit -> string) =
  try printfn "%s: OK %s" name (f ())
  with ex -> printfn "%s: FAIL %s" name (short ex.Message)
type CNew() =
  member val EtapaId = System.Guid.Empty with get,set
  member val Nivel = 0 with get,set
  member val Limite = 0m with get,set
  member val AprovadorId = System.Guid.Empty with get,set
  member val Bloqueada = false with get,set
  member val DecisivelPorMim = false with get,set
  member val RequisicaoId = System.Guid.Empty with get,set
  member val Numero = "" with get,set
  member val Total = 0m with get,set
  member val StatusRequisicao = "" with get,set
  member val Urgencia = "" with get,set
  member val SolicitadaEm = System.DateTimeOffset.MinValue with get,set
  member val CriadaEm = System.DateTimeOffset.MinValue with get,set
  member val Version = 0L with get,set
type CNoParam(etapaId:System.Guid, nivel:int, limite:decimal, aprovadorId:System.Guid, bloqueada:bool, decisivelPorMim:bool, requisicaoId:System.Guid, numero:string, total:decimal, statusRequisicao:string, urgencia:string, solicitadaem:System.DateTimeOffset, criadaem:System.DateTimeOffset, version:int64) =
  member val EtapaId = etapaId with get
  member val Nivel = nivel with get
  member val Limite = limite with get
  member val AprovadorId = aprovadorId with get
  member val Bloqueada = bloqueada with get
  member val DecisivelPorMim = decisivelPorMim with get
  member val RequisicaoId = requisicaoId with get
  member val Numero = numero with get
  member val Total = total with get
  member val StatusRequisicao = statusRequisicao with get
  member val Urgencia = urgencia with get
  member val SolicitadaEm = solicitadaem with get
  member val CriadaEm = criadaem with get
  member val Version = version with get
type COld(etapaId:System.Guid, nivel:int, limite:decimal, aprovadorId:System.Guid, bloqueada:bool, decisivelPorMim:bool, requisicaoId:System.Guid, numero:string, total:decimal, statusRequisicao:string, urgencia:string, solicitadaem:System.DateTime, criadaem:System.DateTime, version:int64) =
  member val EtapaId = etapaId with get
  member val Nivel = nivel with get
  member val Limite = limite with get
  member val AprovadorId = aprovadorId with get
  member val Bloqueada = bloqueada with get
  member val DecisivelPorMim = decisivelPorMim with get
  member val RequisicaoId = requisicaoId with get
  member val Numero = numero with get
  member val Total = total with get
  member val StatusRequisicao = statusRequisicao with get
  member val Urgencia = urgencia with get
  member val SolicitadaEm = solicitadaem with get
  member val CriadaEm = criadaem with get
  member val Version = version with get
run "A record-primary-ctor(newshape)" (fun () -> string (Seq.length (cn.Query<Sigov.Application.ComprasEmpresariais.AprovacaoFilaResumo>(sql, P()))))
run "B class-paramless(newshape)" (fun () -> let rs = (cn.Query<CNew>(sql, P()) |> Seq.toList) in "rows=" + string rs.Length + " total=" + (if rs.IsEmpty then "-" else (string rs.Head.Total)))
run "C class-single-ctor(newshape)" (fun () -> string (Seq.length (cn.Query<CNoParam>(sql, P()))))
run "D class-single-ctor(oldshape)" (fun () -> string (Seq.length (cn.Query<COld>(sql, P()))))