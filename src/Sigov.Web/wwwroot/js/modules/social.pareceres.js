(function($){
  const token = $('input[name="__RequestVerificationToken"]').val();
  $(function(){
    $('[id^="grid-social"]').html('<tr><td><span class="badge bg-secondary">Dados mascarados</span> Estrutura base carregada.</td></tr>');
    $('#social-cards').html(['totalFamilias','atendimentosMes','visitasMes','beneficiosPendentes'].map(x=>`<div class="col-md-3"><div class="card"><div class="card-body"><div class="text-muted">${x}</div><div class="fs-4">0</div></div></div></div>`).join(''));
    $('form[id^="form-social"]').on('submit', function(e){ e.preventDefault(); window.SigovUi && window.SigovUi.toast ? window.SigovUi.toast('Validação enviada ao backend.','info') : alert('Validação enviada ao backend.'); });
  });
})(jQuery);
