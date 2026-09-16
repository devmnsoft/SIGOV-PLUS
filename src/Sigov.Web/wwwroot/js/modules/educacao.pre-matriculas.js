(function ($) {
  'use strict';
  const endpoint = '/api/educacao/pre-matriculas'; let page = 1; const size = 20;
  const encode = value => $('<div>').text(value == null ? '' : String(value)).html();
  function toast(type, msg) { $('#educacao-toast').removeClass('d-none alert-success alert-danger alert-warning').addClass('alert-' + type).text(msg); }
  function query() { const p = new URLSearchParams({ page, pageSize: size }); $('#filtros-pre-matricula').serializeArray().forEach(x => { if (x.value) p.set(x.name, x.value); }); return p; }
  function load() {
    const body = $('#grid-pre-matriculas').html('<tr><td colspan="6">Carregando dados reais…</td></tr>');
    $.getJSON(endpoint + '?' + query()).done(function (r) {
      const data = r.data || {}; const items = data.items || [];
      body.html(items.length ? items.map(x => '<tr><td><a href="/Educacao/PreMatriculaDetalhe/' + x.id + '">' + encode(x.protocolo) + '</a></td><td>' + encode(x.anoLetivo) + ' · ' + encode(x.etapaEnsino) + '</td><td>' + encode(x.turno || 'Sem preferência') + '</td><td><span class="badge bg-primary">' + encode(x.status) + '</span></td><td>' + encode(x.versao) + '</td><td><button class="btn btn-sm btn-outline-primary" data-enviar="' + x.id + '" data-versao="' + x.versao + '"' + (x.status !== 'RASCUNHO' ? ' disabled' : '') + '>Enviar para análise</button></td></tr>').join('') : '<tr><td colspan="6" class="text-muted">Nenhuma solicitação no recorte.</td></tr>');
      const total = data.totalCount || 0; $('#pagina-resumo').text('Página ' + page + ' · ' + total + ' registro(s)'); $('#pagina-anterior').prop('disabled', page === 1); $('#pagina-proxima').prop('disabled', page * size >= total);
    }).fail(x => { body.html('<tr><td colspan="6" class="text-danger">Falha ao carregar; zero não foi presumido.</td></tr>'); toast('danger', x.responseJSON?.error || 'Não foi possível consultar a fila.'); });
  }
  $(function () {
    $('#form-pre-matricula').on('submit', function (e) { e.preventDefault(); if (this.checkValidity() === false) { this.reportValidity(); return; } const button = $(this).find(':submit').prop('disabled', true); const data = {}; $(this).serializeArray().forEach(x => { if (x.name !== '__RequestVerificationToken') data[x.name] = /Id$|AnoLetivo/.test(x.name) && x.value ? Number(x.value) : (x.value || null); }); $.ajax({ url:endpoint, method:'POST', contentType:'application/json', data:JSON.stringify(data), headers:{ RequestVerificationToken:$(this).find('[name=__RequestVerificationToken]').val() } }).done(() => { toast('success','Rascunho persistido. Envie-o quando estiver pronto.'); this.reset(); page=1; load(); }).fail(x => toast('danger',x.responseJSON?.error || 'Falha ao persistir o rascunho.')).always(() => button.prop('disabled',false)); });
    $('#grid-pre-matriculas').on('click','[data-enviar]',function(){ const b=$(this).prop('disabled',true); $.ajax({url:endpoint+'/'+b.data('enviar')+'/transicoes/EM_ANALISE',method:'POST',contentType:'application/json',data:JSON.stringify({versao:b.data('versao')}),headers:{RequestVerificationToken:$('#form-pre-matricula [name=__RequestVerificationToken]').val()}}).done(()=>{toast('success','Solicitação enviada para análise.');load();}).fail(x=>{toast('danger',x.responseJSON?.error||'Conflito ao enviar. Recarregue a fila.');b.prop('disabled',false);}); });
    $('#filtros-pre-matricula').on('submit',e=>{e.preventDefault();page=1;load();}).on('reset',()=>setTimeout(()=>{page=1;load();},0)); $('#pagina-anterior').on('click',()=>{page--;load();}); $('#pagina-proxima').on('click',()=>{page++;load();}); load();
  });
})(jQuery);
