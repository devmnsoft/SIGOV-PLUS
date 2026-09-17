using System.Globalization;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Sigov.Application.Patrimonio;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Patrimonio;

public sealed partial class PatrimonioService
{
    public async Task MovimentarBemAsync(long tenantId,long usuarioId,string correlationId,long id,PatrimonioMovimentacaoInput input,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(input.Justificativa)) throw new ArgumentException("A justificativa é obrigatória.");
        if(input.UnidadeDestinoId is null&&input.ResponsavelDestinoId is null&&string.IsNullOrWhiteSpace(input.LocalizacaoDestino)) throw new ArgumentException("Informe ao menos um destino para a movimentação.");
        correlationId=VazioNulo(correlationId)??throw new ArgumentException("A chave de idempotência é obrigatória.");
        if(correlationId.Length>100)throw new ArgumentException("A chave de idempotência deve ter no máximo 100 caracteres.");
        await using var c=factory.CreateConnection(); await c.OpenAsync(ct); await using var tx=await c.BeginTransactionAsync(ct);
        var bem=await c.QuerySingleOrDefaultAsync<BemEstado>(new CommandDefinition("select unidade_id UnidadeId,responsavel_usuario_id ResponsavelId,localizacao,situacao from sigov.patrimonio_bem where tenant_id=@TenantId and id=@Id and not is_deleted for update",new{TenantId=tenantId,Id=id},tx,cancellationToken:ct))??throw new KeyNotFoundException("Bem não encontrado.");
        if(bem.Situacao=="BAIXADO") throw new InvalidOperationException("Bem baixado não pode ser movimentado.");
        var repetida=await c.QuerySingleOrDefaultAsync<MovimentoExistente>(new CommandDefinition("select bem_id BemId,unidade_destino_id UnidadeDestinoId,responsavel_destino_id ResponsavelDestinoId,localizacao_destino LocalizacaoDestino,tipo_movimentacao TipoMovimentacao,justificativa from sigov.patrimonio_movimentacao where tenant_id=@TenantId and correlation_id=@CorrelationId order by id limit 1",new{TenantId=tenantId,CorrelationId=correlationId},tx,cancellationToken:ct));
        if(repetida is not null)
        {
            if(repetida.BemId!=id||repetida.UnidadeDestinoId!=input.UnidadeDestinoId||repetida.ResponsavelDestinoId!=input.ResponsavelDestinoId||!Igual(repetida.LocalizacaoDestino,input.LocalizacaoDestino)||!string.Equals(repetida.TipoMovimentacao,input.TipoMovimentacao,StringComparison.OrdinalIgnoreCase)||!string.Equals(repetida.Justificativa.Trim(),input.Justificativa.Trim(),StringComparison.Ordinal)) throw new InvalidOperationException("A chave de idempotência já foi usada em outra movimentação.");
            await tx.CommitAsync(ct); return;
        }
        if(bem.UnidadeId==input.UnidadeDestinoId&&bem.ResponsavelId==input.ResponsavelDestinoId&&Igual(bem.Localizacao,input.LocalizacaoDestino)) throw new InvalidOperationException("O destino informado já é a localização e responsabilidade atual do bem.");
        await c.ExecuteAsync(new CommandDefinition("insert into sigov.patrimonio_movimentacao(tenant_id,bem_id,unidade_origem_id,unidade_destino_id,responsavel_origem_id,responsavel_destino_id,localizacao_origem,localizacao_destino,tipo_movimentacao,justificativa,data_movimentacao,usuario_id,correlation_id) values(@TenantId,@Id,@UnidadeOrigem,@UnidadeDestino,@ResponsavelOrigem,@ResponsavelDestino,@LocalizacaoOrigem,@LocalizacaoDestino,@Tipo,@Justificativa,@Data,@UsuarioId,@CorrelationId)",new{TenantId=tenantId,Id=id,UnidadeOrigem=bem.UnidadeId,UnidadeDestino=input.UnidadeDestinoId,ResponsavelOrigem=bem.ResponsavelId,ResponsavelDestino=input.ResponsavelDestinoId,LocalizacaoOrigem=bem.Localizacao,LocalizacaoDestino=VazioNulo(input.LocalizacaoDestino),Tipo=input.TipoMovimentacao,Justificativa=input.Justificativa.Trim(),Data=input.DataMovimentacao??DateTimeOffset.UtcNow,UsuarioId=usuarioId,CorrelationId=correlationId},tx,cancellationToken:ct));
        await c.ExecuteAsync(new CommandDefinition("update sigov.patrimonio_bem set unidade_id=@Unidade,responsavel_usuario_id=@Responsavel,localizacao=@Localizacao,updated_at=now(),updated_by=@Usuario where tenant_id=@Tenant and id=@Id",new{Unidade=input.UnidadeDestinoId,Responsavel=input.ResponsavelDestinoId,Localizacao=VazioNulo(input.LocalizacaoDestino),Usuario=usuarioId,Tenant=tenantId,Id=id},tx,cancellationToken:ct)); await Auditar(c,tx,tenantId,"patrimonio_bem",id,"MOVIMENTAR",bem,input,usuarioId,correlationId,ct); await tx.CommitAsync(ct);
    }

    public async Task BaixarBemAsync(long tenantId,long usuarioId,string correlationId,long id,PatrimonioBaixaInput input,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(input.Justificativa)) throw new ArgumentException("A justificativa é obrigatória."); await using var c=factory.CreateConnection(); await c.OpenAsync(ct); await using var tx=await c.BeginTransactionAsync(ct);
        var antes=await ObterJson(c,tx,"patrimonio_bem",tenantId,id,ct)??throw new KeyNotFoundException("Bem não encontrado."); var changed=await c.ExecuteAsync(new CommandDefinition("update sigov.patrimonio_bem set situacao='BAIXADO',ativo=false,updated_at=now(),updated_by=@Usuario where tenant_id=@Tenant and id=@Id and situacao<>'BAIXADO' and not is_deleted",new{Usuario=usuarioId,Tenant=tenantId,Id=id},tx,cancellationToken:ct)); if(changed==0) throw new InvalidOperationException("Bem já baixado ou indisponível.");
        await c.ExecuteAsync(new CommandDefinition("insert into sigov.patrimonio_baixa(tenant_id,bem_id,tipo_baixa,justificativa,data_baixa,valor_baixa,autorizado_por_usuario_id) values(@Tenant,@Id,@Tipo,@Justificativa,@Data,@Valor,@Usuario)",new{Tenant=tenantId,Id=id,Tipo=input.TipoBaixa,Justificativa=input.Justificativa.Trim(),Data=input.DataBaixa,Valor=input.ValorBaixa,Usuario=usuarioId},tx,cancellationToken:ct)); await Auditar(c,tx,tenantId,"patrimonio_bem",id,"BAIXAR",antes,input,usuarioId,correlationId,ct); await tx.CommitAsync(ct);
    }
}
