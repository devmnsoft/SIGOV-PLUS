window.carregarPortalServidor = window.carregarPortalServidor || function (servidorId) { return window.SigovRh?.getTyped?.('/api/rh/portal-tipado/servidores/' + servidorId); };
window.programarFerias = window.programarFerias || function (payload) { return window.SigovRh?.postTyped?.('/api/rh/ferias-tipado', payload); };
window.registrarAfastamento = window.registrarAfastamento || function (payload) { return window.SigovRh?.postTyped?.('/api/rh/afastamentos-tipado', payload); };
