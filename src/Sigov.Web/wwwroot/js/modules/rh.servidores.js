window.carregarServidores = window.carregarServidores || function () { return window.SigovRh?.loadGrid?.('servidores'); };
window.salvarServidor = window.salvarServidor || function (payload) { return window.SigovRh?.postTyped?.('/api/rh/servidores-tipado', payload); };
window.editarServidor = window.editarServidor || function (id) { return window.SigovRh?.getTyped?.('/api/rh/servidores-tipado/' + id); };
window.excluirServidor = window.excluirServidor || function (id) { return window.SigovRh?.deleteTyped?.('/api/rh/servidores-tipado/' + id); };
