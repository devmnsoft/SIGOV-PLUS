(() => {
  document.querySelectorAll('[data-api]').forEach((form) => {
    form.addEventListener('submit', (event) => {
      event.preventDefault();
      const api = form.getAttribute('data-api');
      const params = new URLSearchParams(new FormData(form));
      console.info('SIGOV Tributário Avançado', `${api}?${params.toString()}`);
    });
  });
})();
