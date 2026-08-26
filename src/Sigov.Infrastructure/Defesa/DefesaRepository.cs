using System.Text;
using Dapper;
using Npgsql;
using Sigov.Application.Defesa;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Defesa;

public sealed class DefesaRepository(DapperContext db):IDefesaRepository
{
 private sealed record Spec(string Table,string Code,string Description,string Status);
 private static readonly IReadOnlyDictionary<string,Spec> Specs=new Dictionary<string,Spec>(StringComparer.OrdinalIgnoreCase){
  ["Agentes"]=new("defesa_agente","matricula","nome","status"),["Equipes"]=new("defesa_equipe","id::text","nome","status"),["Recursos"]=new("defesa_recurso_operacional","coalesce(placa_viatura,patrimonio_codigo,id::text)","descricao","situacao"),["AreasRisco"]=new("defesa_area_risco","id::text","nome","status"),["Ocorrencias"]=new("defesa_ocorrencia","numero_ocorrencia","descricao","status"),["Acionamentos"]=new("defesa_acionamento","id::text","observacao","status"),["Vistorias"]=new("defesa_vistoria","numero_vistoria","situacao_encontrada","status"),["Abrigos"]=new("defesa_abrigo","id::text","nome","status"),["Atendimentos"]=new("defesa_atendimento_emergencial","id::text","nome_responsavel_familiar","status"),["Rondas"]=new("defesa_ronda","numero_ronda","area_atuacao","status"),["OrdensServico"]=new("defesa_ordem_servico","numero_os","descricao","status"),["Notificacoes"]=new("defesa_notificacao_preventiva","numero_notificacao","destinatario_nome","status"),["PlanosContingencia"]=new("defesa_plano_contingencia","id::text","titulo","status"),["Auditoria"]=new("defesa_auditoria","id::text","tabela||' — '||acao","acao")};
 private static readonly IReadOnlyDictionary<string,HashSet<string>> Allowed=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase){
  ["Agentes"]=["cpf","telefone","email","tipo_agente","cargo_funcao","data_inicio","data_fim","observacao","active"],
  ["Equipes"]=["tipo_equipe","turno","responsavel_agente_id","observacao","active"],
  ["Recursos"]=["tipo","patrimonio_codigo","placa_viatura","localizacao","observacao","active"],
  ["AreasRisco"]=["tipo_risco","nivel_risco","endereco","bairro","latitude","longitude","populacao_estimada"],
  ["Ocorrencias"]=["tipo_ocorrencia","origem_chamado","data_hora_abertura","data_hora_fechamento","endereco","bairro","latitude","longitude","area_risco_id","equipe_responsavel_id","prioridade","providencias","resultado"],
  ["Acionamentos"]=["ocorrencia_id","equipe_id","agente_responsavel_id","data_hora_acionamento","data_hora_chegada","data_hora_encerramento"],
  ["Vistorias"]=["tipo_vistoria","area_risco_id","ocorrencia_id","agente_responsavel_id","data_vistoria","endereco","bairro","recomendacoes","prazo_regularizacao"],
  ["Abrigos"]=["tipo","endereco","bairro","capacidade_total","capacidade_ocupada","responsavel_nome","responsavel_telefone","observacao"],
  ["Atendimentos"]=["ocorrencia_id","abrigo_id","documento_responsavel","telefone","quantidade_pessoas","quantidade_criancas","quantidade_idosos","necessidade_especial","tipo_apoio","descricao_apoio","data_atendimento","observacao"],
  ["Rondas"]=["equipe_id","agente_responsavel_id","data_hora_inicio","data_hora_fim","tipo_ronda","resumo"],
  ["OrdensServico"]=["tipo_servico","solicitante","equipe_id","agente_responsavel_id","data_abertura","data_previsao","data_conclusao","prioridade","resultado"],
  ["Notificacoes"]=["vistoria_id","area_risco_id","destinatario_documento","endereco","data_emissao","prazo_atendimento","motivo","recomendacao"],
  ["PlanosContingencia"]=["tipo_evento","area_risco_id","responsavel_agente_id","vigencia_inicio","vigencia_fim","resumo","medidas_preventivas","fluxos_acionamento"],["Auditoria"]=[]};

 public async Task<DefesaDashboard> DashboardAsync(DefesaContexto c,CancellationToken ct){using var cn=db.CreateConnection();const string sql="""select
 (select count(*) from sigov.defesa_ocorrencia where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status='ABERTA') OcorrenciasAbertas,
 (select count(*) from sigov.defesa_ocorrencia where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status='ABERTA' and prioridade='CRITICA') OcorrenciasCriticas,
 (select count(*) from sigov.defesa_ocorrencia where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status='FECHADA' and data_hora_fechamento>=date_trunc('month',now())) OcorrenciasFechadasMes,
 (select count(*) from sigov.defesa_acionamento where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status in('ACIONADO','EM_DESLOCAMENTO','NO_LOCAL')) AcionamentosAndamento,
 (select count(*) from sigov.defesa_vistoria where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status='PENDENTE') VistoriasPendentes,
 (select count(*) from sigov.defesa_area_risco where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and nivel_risco='CRITICO') AreasCriticas,
 (select count(*) from sigov.defesa_abrigo where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status='ATIVO') AbrigosAtivos,
 (select coalesce(sum(capacidade_total),0) from sigov.defesa_abrigo where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status='ATIVO') CapacidadeAbrigos,
 (select coalesce(sum(capacidade_ocupada),0) from sigov.defesa_abrigo where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status='ATIVO') OcupacaoAbrigos,
 (select count(*) from sigov.defesa_ronda where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status='EM_ANDAMENTO') RondasAndamento,
 (select count(*) from sigov.defesa_ordem_servico where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status in('ABERTA','EM_ANDAMENTO')) OrdensAbertas,
 (select count(*) from sigov.defesa_notificacao_preventiva where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status='PENDENTE') NotificacoesPendentes""";return await cn.QuerySingleAsync<DefesaDashboard>(new CommandDefinition(sql,c,cancellationToken:ct));}
 public async Task<DefesaPagina> ListarAsync(DefesaContexto c,string recurso,DefesaFiltro f,CancellationToken ct){var s=SpecFor(recurso);using var cn=db.CreateConnection();var page=Math.Max(1,f.Pagina);var size=Math.Clamp(f.Tamanho,1,10000);var deleted=s.Table=="defesa_auditoria"?"true":"deleted_at is null";var where=$"tenant_id=@TenantId and entity_id=@EntityId and {deleted} and (@Busca is null or cast({s.Code} as text) ilike '%'||@Busca||'%' or cast({s.Description} as text) ilike '%'||@Busca||'%') and (@Status is null or cast({s.Status} as text)=@Status) and (@Inicio is null or created_at::date>=@Inicio) and (@Fim is null or created_at::date<=@Fim)";var p=new{c.TenantId,c.EntityId,f.Busca,f.Status,f.Inicio,f.Fim,Limit=size,Offset=(page-1)*size};var total=await cn.ExecuteScalarAsync<long>($"select count(*) from sigov.{s.Table} where {where}",p);var items=(await cn.QueryAsync<DefesaRegistro>($"select id,cast({s.Code} as text) Codigo,coalesce(cast({s.Description} as text),'') Descricao,cast({s.Status} as text) Status,created_at CriadoEm from sigov.{s.Table} where {where} order by created_at desc limit @Limit offset @Offset",p)).AsList();return new(items,page,size,total);}
 public async Task<DefesaRegistroRequest?> ObterAsync(DefesaContexto c,string recurso,long id,CancellationToken ct)
 {
  var s=SpecFor(recurso);if(recurso.Equals("Auditoria",StringComparison.OrdinalIgnoreCase))return null;
  var columns=Allowed[recurso].OrderBy(x=>x,StringComparer.Ordinal).ToArray();
  var code=!s.Code.Contains("::",StringComparison.Ordinal)&&!s.Code.Contains('(')?s.Code:"id::text";
  var description=s.Description.Split("||",StringSplitOptions.None)[0];
  using var cn=db.CreateConnection();
  var row=await cn.QuerySingleOrDefaultAsync($"select cast({code} as text) Codigo,coalesce(cast({description} as text),'') Descricao,cast({s.Status} as text) Status,{string.Join(',',columns.Select(x=>$"cast({x} as text) as {x}"))} from sigov.{s.Table} where id=@id and tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null",new{id,c.TenantId,c.EntityId});
  if(row is null)return null;var values=(IDictionary<string,object>)row;
  string Value(string name)=>values.FirstOrDefault(x=>x.Key.Equals(name,StringComparison.OrdinalIgnoreCase)).Value?.ToString()??"";
  var request=new DefesaRegistroRequest{Codigo=Value("Codigo"),Descricao=Value("Descricao"),Status=Value("Status")};
  foreach(var column in columns)request.Campos[column]=Value(column);
  if(recurso.Equals("Equipes",StringComparison.OrdinalIgnoreCase))request.AgentesSelecionados=(await cn.QueryAsync<long>("select agente_id from sigov.defesa_equipe_agente where tenant_id=@TenantId and entity_id=@EntityId and equipe_id=@id and data_saida is null",new{id,c.TenantId,c.EntityId})).AsList();
  return request;
 }
 public async Task<IReadOnlyList<DefesaOpcao>> OpcoesAsync(DefesaContexto c,string tipo,bool somenteAtivos,CancellationToken ct){var s=SpecFor(tipo);using var cn=db.CreateConnection();var active=tipo.Equals("Agentes",StringComparison.OrdinalIgnoreCase)?"and active=true and status='ATIVO'":tipo.Equals("Equipes",StringComparison.OrdinalIgnoreCase)?"and active=true and status='ATIVA'":tipo.Equals("Abrigos",StringComparison.OrdinalIgnoreCase)?"and status='ATIVO'":"";var label=tipo.ToUpperInvariant() switch{"AGENTES"=>"matricula||' — '||nome","EQUIPES"=>"nome||' — '||tipo_equipe||' — '||coalesce(turno,'Sem turno')","AREASRISCO"=>"nome||' — '||bairro||' — '||nivel_risco","OCORRENCIAS"=>"numero_ocorrencia||' — '||left(descricao,80)","ABRIGOS"=>"nome||' — '||endereco","VISTORIAS"=>"numero_vistoria||' — '||left(situacao_encontrada,80)",_=>$"cast({s.Code} as text)||' — '||cast({s.Description} as text)"};return(await cn.QueryAsync<DefesaOpcao>($"select id,{label} Rotulo from sigov.{s.Table} where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null {(somenteAtivos?active:"")} order by 2 limit 500",c)).AsList();}
 public async Task<long> SalvarAsync(DefesaContexto c,string recurso,DefesaRegistroRequest r,long? id,CancellationToken ct)
 {
  var s=SpecFor(recurso);if(recurso.Equals("Auditoria",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Recurso somente para consulta.");
  if(recurso.Equals("Equipes",StringComparison.OrdinalIgnoreCase)&&r.AgentesSelecionados.Distinct().Count()==0)throw new ArgumentException("Selecione ao menos um agente ativo para a equipe.");
  var values=new Dictionary<string,object?>();
  if(!s.Code.Contains("::",StringComparison.Ordinal)&&!s.Code.Contains('('))values[s.Code]=r.Codigo.Trim();
  values[s.Description.Split("||",StringSplitOptions.None)[0]]=r.Descricao.Trim();
  values[s.Status]=r.Status;
  foreach(var (key,value) in r.Campos){if(!Allowed[recurso].Contains(key))throw new ArgumentException($"Campo não permitido: {key}.");values[key]=string.IsNullOrWhiteSpace(value)?null:value.Trim();}
  using var cn=db.CreateConnection();cn.Open();using var tx=cn.BeginTransaction();
  foreach(var (key,value) in r.Campos.Where(x=>x.Key.EndsWith("_id",StringComparison.Ordinal)&&!string.IsNullOrWhiteSpace(x.Value))){if(!long.TryParse(value,out var relationId))throw new ArgumentException("Seleção de relacionamento inválida.");var table=RelationTable(key);var activeAgent=table=="defesa_agente"?"and active=true and status='ATIVO'":"";var valid=await cn.ExecuteScalarAsync<bool>(new CommandDefinition($"select exists(select 1 from sigov.{table} where id=@relationId and tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null {activeAgent})",new{relationId,c.TenantId,c.EntityId},tx,cancellationToken:ct));if(!valid)throw new ArgumentException("A seleção não pertence ao contexto atual ou não está ativa.");}
  await ValidateOperationalRules(c,recurso,r,cn,tx,ct);
  if(recurso.Equals("Recursos",StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrWhiteSpace(r.Campos.GetValueOrDefault("patrimonio_codigo"))){var duplicate=await cn.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.defesa_recurso_operacional where tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and patrimonio_codigo=@codigo and (@id is null or id<>@id))",new{c.TenantId,c.EntityId,codigo=r.Campos["patrimonio_codigo"],id},tx,cancellationToken:ct));if(duplicate)throw new ArgumentException("O código patrimonial já está em uso neste contexto.");}
  var parameters=new DynamicParameters(new{c.TenantId,c.EntityId,id});var assignments=new List<string>();var columns=new List<string>{"tenant_id","entity_id"};var expressions=new List<string>{"@TenantId","@EntityId"};var i=0;
  foreach(var pair in values){var name=$"v{++i}";parameters.Add(name,pair.Value);var expression=SqlValue(pair.Key,name);columns.Add(pair.Key);expressions.Add(expression);assignments.Add($"{pair.Key}={expression}");}
  try{
   long savedId;
   if(id.HasValue){var updated=await cn.ExecuteScalarAsync<long?>(new CommandDefinition($"update sigov.{s.Table} set {string.Join(',',assignments)},updated_at=now() where id=@id and tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null returning id",parameters,tx,cancellationToken:ct));if(!updated.HasValue)throw new KeyNotFoundException("Registro não encontrado neste contexto.");savedId=updated.Value;}
   else savedId=await cn.ExecuteScalarAsync<long>(new CommandDefinition($"insert into sigov.{s.Table}({string.Join(',',columns)}) values({string.Join(',',expressions)}) returning id",parameters,tx,cancellationToken:ct));
   if(recurso.Equals("Equipes",StringComparison.OrdinalIgnoreCase)){await cn.ExecuteAsync(new CommandDefinition("update sigov.defesa_equipe_agente set data_saida=current_date where tenant_id=@TenantId and entity_id=@EntityId and equipe_id=@savedId and data_saida is null",new{c.TenantId,c.EntityId,savedId},tx,cancellationToken:ct));foreach(var agenteId in r.AgentesSelecionados.Distinct()){var ativo=await cn.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.defesa_agente where id=@agenteId and tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and active=true and status='ATIVO')",new{agenteId,c.TenantId,c.EntityId},tx,cancellationToken:ct));if(!ativo)throw new ArgumentException("Todos os integrantes devem ser agentes ativos do contexto atual.");await cn.ExecuteAsync(new CommandDefinition("insert into sigov.defesa_equipe_agente(tenant_id,entity_id,equipe_id,agente_id,data_entrada) values(@TenantId,@EntityId,@savedId,@agenteId,current_date)",new{c.TenantId,c.EntityId,savedId,agenteId},tx,cancellationToken:ct));}}
   await cn.ExecuteAsync(new CommandDefinition("insert into sigov.defesa_auditoria(tenant_id,entity_id,tabela,registro_id,acao,usuario,dados_novos) values(@TenantId,@EntityId,@Tabela,@savedId,@Acao,@Usuario,jsonb_build_object('identificacao',@Codigo,'descricao',@Descricao))",new{c.TenantId,c.EntityId,Tabela=s.Table,savedId,Acao=id.HasValue?"ALTERAR":"CRIAR",c.Usuario,r.Codigo,r.Descricao},tx,cancellationToken:ct));tx.Commit();return savedId;
  }catch(PostgresException ex){throw new ArgumentException("Os dados violam uma regra operacional: "+ex.ConstraintName,ex);}
 }
 public async Task ExcluirAsync(DefesaContexto c,string recurso,long id,CancellationToken ct){var s=SpecFor(recurso);using var cn=db.CreateConnection();cn.Open();using var tx=cn.BeginTransaction();var n=await cn.ExecuteAsync(new CommandDefinition($"update sigov.{s.Table} set deleted_at=now(),updated_at=now() where id=@id and tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null",new{id,c.TenantId,c.EntityId},tx,cancellationToken:ct));if(n==0)throw new KeyNotFoundException("Registro não encontrado neste contexto.");await cn.ExecuteAsync(new CommandDefinition("insert into sigov.defesa_auditoria(tenant_id,entity_id,tabela,registro_id,acao,usuario) values(@TenantId,@EntityId,@Tabela,@id,'EXCLUIR',@Usuario)",new{c.TenantId,c.EntityId,Tabela=s.Table,id,c.Usuario},tx,cancellationToken:ct));tx.Commit();}
 public async Task<byte[]> CsvAsync(DefesaContexto c,string recurso,DefesaFiltro f,CancellationToken ct){string[] allowed=["Ocorrencias","Acionamentos","Vistorias","AreasRisco","Abrigos","Atendimentos","Rondas","OrdensServico","Notificacoes"];if(!allowed.Contains(recurso,StringComparer.OrdinalIgnoreCase))throw new ArgumentException("Relatório não permitido.");var page=await ListarAsync(c,recurso,f with{Pagina=1,Tamanho=10000},ct);var b=new StringBuilder("Identificação;Descrição;Status;Criado em\r\n");foreach(var x in page.Itens)b.AppendLine($"{Csv(x.Codigo)};{Csv(x.Descricao)};{Csv(x.Status)};{x.CriadoEm:O}");return new UTF8Encoding(true).GetBytes(b.ToString());}
 private static async Task ValidateOperationalRules(DefesaContexto c,string recurso,DefesaRegistroRequest r,System.Data.IDbConnection cn,System.Data.IDbTransaction tx,CancellationToken ct){if(recurso.Equals("Acionamentos",StringComparison.OrdinalIgnoreCase)&&long.TryParse(r.Campos.GetValueOrDefault("ocorrencia_id"),out var ocorrencia)){var aberta=await cn.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.defesa_ocorrencia where id=@ocorrencia and tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status<>'FECHADA')",new{ocorrencia,c.TenantId,c.EntityId},tx,cancellationToken:ct));if(!aberta)throw new ArgumentException("Não é permitido acionar uma ocorrência fechada.");}if(recurso.Equals("Atendimentos",StringComparison.OrdinalIgnoreCase)&&long.TryParse(r.Campos.GetValueOrDefault("abrigo_id"),out var abrigo)){var ativo=await cn.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.defesa_abrigo where id=@abrigo and tenant_id=@TenantId and entity_id=@EntityId and deleted_at is null and status='ATIVO' and capacidade_ocupada<capacidade_total)",new{abrigo,c.TenantId,c.EntityId},tx,cancellationToken:ct));if(!ativo)throw new ArgumentException("O abrigo selecionado está inativo ou lotado.");}}
 private static Spec SpecFor(string r)=>Specs.TryGetValue(r,out var s)?s:throw new ArgumentException("Recurso de Defesa inválido.");
 private static string RelationTable(string key)=>key switch{"responsavel_agente_id" or "agente_responsavel_id"=>"defesa_agente","equipe_id" or "equipe_responsavel_id"=>"defesa_equipe","area_risco_id"=>"defesa_area_risco","ocorrencia_id"=>"defesa_ocorrencia","abrigo_id"=>"defesa_abrigo","vistoria_id"=>"defesa_vistoria",_=>throw new ArgumentException("Relacionamento não permitido.")};
 private static string SqlValue(string column,string parameter)
 {
  if(column.EndsWith("_id",StringComparison.Ordinal))return $"nullif(@{parameter},'')::bigint";
  if(column is "active")return $"coalesce(nullif(@{parameter},'')::boolean,false)";
  if(column.StartsWith("data_hora_",StringComparison.Ordinal)||column is "data_atendimento" or "data_abertura" or "data_previsao" or "data_conclusao")return $"nullif(@{parameter},'')::timestamptz";
  if(column.StartsWith("data_",StringComparison.Ordinal)||column.StartsWith("vigencia_",StringComparison.Ordinal)||column.StartsWith("prazo_",StringComparison.Ordinal))return $"nullif(@{parameter},'')::date";
  if(column.StartsWith("quantidade_",StringComparison.Ordinal)||column.StartsWith("capacidade_",StringComparison.Ordinal)||column is "populacao_estimada")return $"nullif(@{parameter},'')::integer";
  if(column is "latitude" or "longitude")return $"nullif(@{parameter},'')::numeric";
  return $"@{parameter}";
 }
 private static string Csv(string? value){var s=value??"";if(s.Length>0&&"=+-@".Contains(s[0]))s="'"+s;return '"'+s.Replace("\"","\"\"")+'"';}
}
