using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Tributario.TributarioAvancado;

namespace Sigov.Api.Controllers;

[ApiController, Route("api/portal-contribuinte")]
public sealed class PortalContribuinteController : TributarioAvancadoControllerBase
{
    private readonly IPortalContribuinteService _s;

    public PortalContribuinteController(IPortalContribuinteService s, ICurrentTenant t, ICurrentUser u) : base(t, u) => _s = s;

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<TributarioDashboardDto>>> Dashboard(CancellationToken ct)
    {
        var tenantId = TenantId();
        if (TemPermissaoConsultaAdmin())
        {
            return Resposta(await _s.DashboardAsync(tenantId, "portal_contribuinte_protocolo", ct));
        }

        var usuarioId = CurrentUser.UsuarioId ?? 0;
        var autorizados = await _s.ObterContribuintesAutorizadosAsync(tenantId, usuarioId, CurrentUser.Email, ct);
        var ids = autorizados.Select(a => a.ContribuinteId).ToArray();
        return Resposta(await _s.DashboardAutoatendimentoAsync(tenantId, ids, usuarioId, ct));
    }

    [HttpPost("consulta")]
    public async Task<ActionResult<ApiResponse<PagedResult<TributarioRegistroDto>>>> Consulta(CancellationToken ct)
    {
        var tenantId = TenantId();
        if (TemPermissaoConsultaAdmin())
        {
            return Resposta(await _s.ListarAsync(tenantId, "portal_contribuinte_solicitacao", 1, 20, ct));
        }

        var usuarioId = CurrentUser.UsuarioId ?? 0;
        var autorizados = await _s.ObterContribuintesAutorizadosAsync(tenantId, usuarioId, CurrentUser.Email, ct);
        var ids = autorizados.Select(a => a.ContribuinteId).ToArray();
        return Resposta(await _s.ListarSolicitacoesAutoatendimentoAsync(tenantId, ids, usuarioId, 1, 20, ct));
    }

    [HttpGet("contribuintes/{codigo}/debitos")]
    public async Task<ActionResult<ApiResponse<PagedResult<ContribuinteDebitoDto>>>> Debitos(string codigo, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 50, CancellationToken ct = default)
    {
        var cid = GetCorrelationId();
        var tenantId = TenantId();
        var contribuinte = await ResolverContribuinteAutorizadoAsync(tenantId, codigo, ct);
        if (contribuinte is null)
        {
            return NotFound(ApiResponse<PagedResult<ContribuinteDebitoDto>>.Fail("Contribuinte não localizado ou sem autorização.", cid));
        }

        var result = await _s.ListarDebitosAsync(tenantId, contribuinte.ContribuinteId, pagina, tamanho, ct);
        return Resposta(result);
    }

    [HttpGet("contribuintes/{codigo}/pagamentos")]
    public async Task<ActionResult<ApiResponse<PagedResult<ContribuintePagamentoDto>>>> Pagamentos(string codigo, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 50, CancellationToken ct = default)
    {
        var cid = GetCorrelationId();
        var tenantId = TenantId();
        var contribuinte = await ResolverContribuinteAutorizadoAsync(tenantId, codigo, ct);
        if (contribuinte is null)
        {
            return NotFound(ApiResponse<PagedResult<ContribuintePagamentoDto>>.Fail("Contribuinte não localizado ou sem autorização.", cid));
        }

        var result = await _s.ListarPagamentosAsync(tenantId, contribuinte.ContribuinteId, pagina, tamanho, ct);
        return Resposta(result);
    }

    [HttpPost("guias/emitir")]
    public async Task<ActionResult<ApiResponse<long>>> Guia(TributarioOperacaoRequest r, CancellationToken ct)
    {
        var tenantId = TenantId();
        if (!TemPermissaoGuiaAdmin())
        {
            r = await ValidarTitularidadeOperacaoAsync(tenantId, r, ct);
        }
        return Resposta(await _s.CriarAsync(Contexto(), "portal_contribuinte_guia_emitida", r with { Status = "PREPARATORIA" }, ct));
    }

    [HttpGet("guias/{id:long}")]
    public async Task<ActionResult<ApiResponse<TributarioRegistroDto>>> Guia(long id, CancellationToken ct)
    {
        var cid = GetCorrelationId();
        var tenantId = TenantId();
        var guia = await _s.ObterAsync(tenantId, "portal_contribuinte_guia_emitida", id, ct);
        if (guia is null) return NotFound(ApiResponse<TributarioRegistroDto>.Fail("Guia não localizada.", cid));

        if (!TemPermissaoConsultaAdmin())
        {
            var usuarioId = CurrentUser.UsuarioId ?? 0;
            var autorizados = await _s.ObterContribuintesAutorizadosAsync(tenantId, usuarioId, CurrentUser.Email, ct);
            long? guiaContribId = guia.ContribuinteId;
            bool autorizado = guiaContribId.HasValue && autorizados.Any(a => a.ContribuinteId == guiaContribId.Value);
            if (!autorizado)
            {
                return NotFound(ApiResponse<TributarioRegistroDto>.Fail("Guia não localizada.", cid));
            }
        }

        return Resposta(guia);
    }

    [HttpPost("certidoes/emitir")]
    public async Task<ActionResult<ApiResponse<long>>> Certidao(TributarioOperacaoRequest r, CancellationToken ct)
    {
        var tenantId = TenantId();
        if (!TemPermissaoCertidaoAdmin())
        {
            r = await ValidarTitularidadeOperacaoAsync(tenantId, r, ct);
        }
        return Resposta(await _s.CriarAsync(Contexto(), "portal_contribuinte_certidao", r with { Status = "PREPARATORIA" }, ct));
    }

    [AllowAnonymous, HttpGet("certidoes/validar/{codigo}")]
    public async Task<ActionResult<ApiResponse<CertidaoValidacaoPublicaDto>>> Validar(string codigo, CancellationToken ct)
    {
        var cid = GetCorrelationId();
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Trim().Length < 4)
        {
            return BadRequest(ApiResponse<CertidaoValidacaoPublicaDto>.Fail("Código de autenticação inválido.", cid));
        }
        long? tenantId = Tenant.TenantId;
        var result = await _s.ValidarCertidaoPublicaAsync(tenantId, codigo.Trim(), ct);
        if (result is null)
        {
            return NotFound(ApiResponse<CertidaoValidacaoPublicaDto>.Fail("Certidão não localizada ou código de autenticação inválido.", cid));
        }
        return Resposta(result);
    }

    [HttpPost("parcelamentos/solicitar")]
    public async Task<ActionResult<ApiResponse<long>>> Parcelar(TributarioOperacaoRequest r, CancellationToken ct)
    {
        var tenantId = TenantId();
        if (!TemPermissaoParcelamentoAdmin())
        {
            r = await ValidarTitularidadeOperacaoAsync(tenantId, r, ct);
        }
        return Resposta(await _s.CriarAsync(Contexto(), "portal_contribuinte_parcelamento_solicitacao", r, ct));
    }

    [HttpGet("protocolos/{codigo}")]
    public async Task<ActionResult<ApiResponse<TributarioRegistroDto>>> Protocolo(string codigo, CancellationToken ct)
    {
        var cid = GetCorrelationId();
        var tenantId = TenantId();
        long.TryParse(codigo, out var id);
        var item = await _s.ObterAsync(tenantId, "portal_contribuinte_protocolo", id, ct);
        if (item is null) return NotFound(ApiResponse<TributarioRegistroDto>.Fail("Protocolo não localizado.", cid));

        if (!TemPermissaoConsultaAdmin())
        {
            var usuarioId = CurrentUser.UsuarioId ?? 0;
            var autorizados = await _s.ObterContribuintesAutorizadosAsync(tenantId, usuarioId, CurrentUser.Email, ct);
            long? itemContribId = item.ContribuinteId;
            bool autorizado = itemContribId.HasValue && autorizados.Any(a => a.ContribuinteId == itemContribId.Value);
            if (!autorizado)
            {
                return NotFound(ApiResponse<TributarioRegistroDto>.Fail("Protocolo não localizado.", cid));
            }
        }

        return Resposta(item);
    }

    [HttpPost("solicitacoes/atualizacao-cadastral")]
    public async Task<ActionResult<ApiResponse<long>>> Atualizacao(TributarioOperacaoRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Justificativa)) throw new ArgumentException("Justificativa é obrigatória.");
        var tenantId = TenantId();
        if (!TemPermissaoCadastroAdmin())
        {
            r = await ValidarTitularidadeOperacaoAsync(tenantId, r, ct);
        }
        return Resposta(await _s.CriarAsync(Contexto(), "portal_contribuinte_solicitacao", r, ct));
    }

    private bool IsServidorAdministrativo() =>
        CurrentUser.Permissions.Contains("tributario.admin");

    private bool TemPermissaoGuiaAdmin() =>
        IsServidorAdministrativo() ||
        CurrentUser.Permissions.Contains("tributario.guia.gerenciar") ||
        CurrentUser.Permissions.Contains("TRIBUTARIO_GUIA_MANAGE");

    private bool TemPermissaoCertidaoAdmin() =>
        IsServidorAdministrativo() ||
        CurrentUser.Permissions.Contains("tributario.certidao.gerenciar") ||
        CurrentUser.Permissions.Contains("TRIBUTARIO_CERTIDAO_MANAGE");

    private bool TemPermissaoParcelamentoAdmin() =>
        IsServidorAdministrativo() ||
        CurrentUser.Permissions.Contains("tributario.parcelamento.gerenciar") ||
        CurrentUser.Permissions.Contains("TRIBUTARIO_PARCELAMENTO_MANAGE");

    private bool TemPermissaoCadastroAdmin() =>
        IsServidorAdministrativo() ||
        CurrentUser.Permissions.Contains("tributario.contribuinte.gerenciar") ||
        CurrentUser.Permissions.Contains("TRIBUTARIO_CONTRIBUINTE_MANAGE");

    private bool TemPermissaoConsultaAdmin() =>
        IsServidorAdministrativo() ||
        CurrentUser.Permissions.Contains("tributario.contribuinte") ||
        CurrentUser.Permissions.Contains("tributario.contribuinte.visualizar") ||
        CurrentUser.Permissions.Contains("TRIBUTARIO_CONTRIBUINTE_VIEW");

    private async Task<ContribuinteAutorizadoInfo?> ResolverContribuinteAutorizadoAsync(long tenantId, string codigo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        if (TemPermissaoConsultaAdmin())
        {
            return await _s.BuscarContribuintePorCodigoAsync(tenantId, codigo, ct);
        }

        var usuarioId = CurrentUser.UsuarioId;
        if (usuarioId is null) return null;

        var autorizados = await _s.ObterContribuintesAutorizadosAsync(tenantId, usuarioId.Value, CurrentUser.Email, ct);
        long.TryParse(codigo, out var codNumeric);

        return autorizados.FirstOrDefault(a =>
            string.Equals(a.Inscricao, codigo, StringComparison.OrdinalIgnoreCase) ||
            (codNumeric > 0 && a.ContribuinteId == codNumeric));
    }

    private async Task<TributarioOperacaoRequest> ValidarTitularidadeOperacaoAsync(long tenantId, TributarioOperacaoRequest r, CancellationToken ct)
    {
        var usuarioId = CurrentUser.UsuarioId;
        if (usuarioId is null) throw new UnauthorizedAccessException("Usuário autenticado é obrigatório.");

        var autorizados = await _s.ObterContribuintesAutorizadosAsync(tenantId, usuarioId.Value, CurrentUser.Email, ct);
        if (autorizados.Count == 0) throw new UnauthorizedAccessException("Nenhum contribuinte vinculado ao usuário autenticado.");

        long contribuinteId;
        if (r.ContribuinteId.HasValue && r.ContribuinteId.Value > 0)
        {
            var match = autorizados.FirstOrDefault(a => a.ContribuinteId == r.ContribuinteId.Value);
            if (match is null) throw new UnauthorizedAccessException("Operação não autorizada para o contribuinte informado.");
            contribuinteId = match.ContribuinteId;
        }
        else if (r.ReferenciaId.HasValue && r.ReferenciaId.Value > 0)
        {
            var match = autorizados.FirstOrDefault(a => a.ContribuinteId == r.ReferenciaId.Value);
            if (match is null) throw new UnauthorizedAccessException("Operação não autorizada para o contribuinte informado.");
            contribuinteId = match.ContribuinteId;
        }
        else if (autorizados.Count == 1)
        {
            contribuinteId = autorizados[0].ContribuinteId;
        }
        else
        {
            throw new ArgumentException("Selecione o contribuinte desejado para a operação.");
        }

        var dadosSanitizados = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (r.Dados != null)
        {
            foreach (var (k, v) in r.Dados)
            {
                if (k is "contribuinte_id" or "tenant_id" or "usuario_id" or "roles" or "permissoes" or "admin" or "is_deleted" or "auditoria" or "id")
                    continue;
                dadosSanitizados[k] = v;
            }
        }
        dadosSanitizados["contribuinte_id"] = contribuinteId;

        long? refNegocio = (r.ReferenciaId == contribuinteId) ? null : r.ReferenciaId;

        return r with { ContribuinteId = contribuinteId, ReferenciaId = refNegocio, Dados = dadosSanitizados };
    }
}
