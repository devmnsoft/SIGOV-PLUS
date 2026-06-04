window.sigovUi = (() => {
  function notify(message, type = 'info') {
    const host = document.getElementById('sigov-alerts');
    if (!host) return;
    host.innerHTML = `<div class="alert alert-${type}" role="alert">${message}</div>`;
  }

  return { notify };
})();
