window.carregarFolhas = window.carregarFolhas || function () { return window.SigovRh?.loadGrid?.('folhas'); };
window.criarFolha = window.criarFolha || function (payload) { return window.SigovRh?.postTyped?.('/api/rh/folhas-tipado', payload); };
window.adicionarLancamentoFolha = window.adicionarLancamentoFolha || function (folhaId, payload) { return window.SigovRh?.postTyped?.('/api/rh/folhas-tipado/' + folhaId + '/lancamentos', payload); };
window.integrarFolhaFinanceiro = window.integrarFolhaFinanceiro || function (payload) { return window.SigovRh?.postTyped?.('/api/rh/folhas/integrar-financeiro', payload); };
