(() => {
  const root = document.querySelector('[data-import-module]'); if (!root) return;
  const file = root.querySelector('[data-import-file]'), preview = root.querySelector('[data-import-preview]'), confirm = root.querySelector('[data-import-confirm]'); let csv = '';
  file.addEventListener('change', async () => { csv = file.files[0] ? await file.files[0].text() : ''; preview.disabled = !csv; confirm.disabled = true; });
  async function send(action) { const url = `/api/${root.dataset.importModule}/importacoes/${root.dataset.importResource}/${action}`; const response = await fetch(url,{method:'POST',headers:{'Content-Type':'application/json','X-Correlation-ID':crypto.randomUUID()},body:JSON.stringify({csv,delimiter:';'})}); const payload=await response.json(); if(!response.ok) throw new Error(payload.message || 'Não foi possível processar o arquivo.'); return payload.data; }
  preview.addEventListener('click', async () => {
    try {
      const data = await send('preview');
      const summary = root.querySelector('[data-import-summary]');
      summary.hidden = false;
      summary.textContent = `${data.valid} válidas · ${data.invalid} rejeitadas · ${data.total} linhas`;
      const body = root.querySelector('[data-import-body]');
      body.replaceChildren();
      (data.rows || []).forEach(r => {
        const tr = document.createElement('tr');
        const tdLine = document.createElement('td');
        tdLine.textContent = String(r.line ?? '');
        const tdStatus = document.createElement('td');
        const badge = document.createElement('span');
        badge.className = `sigov-risk-badge ${r.valid ? 'is-safe' : 'is-critical'}`;
        badge.textContent = r.valid ? 'Válida' : 'Rejeitada';
        tdStatus.appendChild(badge);
        const tdVal = document.createElement('td');
        if (r.issues && r.issues.length) {
          r.issues.forEach((i, idx) => {
            if (idx > 0) tdVal.appendChild(document.createElement('br'));
            const fieldSpan = document.createElement('strong');
            fieldSpan.textContent = `${i.field || 'Campo'}: `;
            const errorText = document.createTextNode(String(i.error || ''));
            tdVal.appendChild(fieldSpan);
            tdVal.appendChild(errorText);
          });
        } else {
          tdVal.textContent = 'Pronta para importar';
        }
        tr.append(tdLine, tdStatus, tdVal);
        body.appendChild(tr);
      });
      const head = root.querySelector('[data-import-head]');
      if (head) {
        head.replaceChildren();
        const trHead = document.createElement('tr');
        ['Linha', 'Status', 'Validação'].forEach(title => {
          const th = document.createElement('th');
          th.textContent = title;
          trHead.appendChild(th);
        });
        head.appendChild(trHead);
      }
      confirm.disabled = !data.valid;
    } catch (e) {
      alert(e.message);
    }
  });
  confirm.addEventListener('click', async () => { if (!window.confirm('Confirmar a persistência das linhas válidas?')) return; try { const data = await send('confirmar'); alert(`${data.persisted} linhas importadas. Relatório #${data.reportId}.`); confirm.disabled = true; } catch (e) { alert(e.message); } });
})();
