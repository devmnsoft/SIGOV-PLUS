const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value;
export async function request(path, options = {}) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 30000);
  const headers = new Headers(options.headers || {});
  headers.set('Accept', 'application/json');
  if (options.body) headers.set('Content-Type', 'application/json');
  if (!headers.has('Idempotency-Key')) headers.set('Idempotency-Key', crypto.randomUUID());
  const anti = token(); if (anti) headers.set('RequestVerificationToken', anti);
  try {
    const response = await fetch(path, { ...options, headers, signal: controller.signal, credentials: 'same-origin' });
    if (!response.ok) {
      const problem = await response.json().catch(() => ({}));
      throw new Error(problem.detail || problem.title || 'Não foi possível concluir a operação.');
    }
    return response.status === 204 ? null : response.json();
  } finally { clearTimeout(timeout); }
}
export function lock(button, locked) { button.disabled = locked; button.setAttribute('aria-busy', String(locked)); }
