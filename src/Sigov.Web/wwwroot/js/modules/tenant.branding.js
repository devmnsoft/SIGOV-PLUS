(() => {
  'use strict';
  const file = document.getElementById('logo');
  const image = document.getElementById('logoPreview');
  const empty = document.getElementById('emptyPreview');
  const width = document.getElementById('LogoWidthPx');
  const height = document.getElementById('LogoHeightPx');
  const fit = document.getElementById('LogoFit');
  if (!file || !image) return;
  const update = () => { image.style.width = `${width.value}px`; image.style.height = `${height.value}px`; image.style.objectFit = fit.value; };
  [width, height, fit].forEach(x => x.addEventListener('input', update));
  document.getElementById('resetLogoSize').addEventListener('click', () => { width.value = 240; height.value = 80; fit.value = 'contain'; update(); });
  file.addEventListener('change', () => {
    const selected = file.files[0]; if (!selected) return;
    if (selected.size > 2 * 1024 * 1024 || !['image/png','image/jpeg','image/webp'].includes(selected.type)) { file.setCustomValidity('Selecione PNG, JPG/JPEG ou WEBP com até 2 MB.'); file.reportValidity(); return; }
    file.setCustomValidity(''); const source = new Image();
    source.onload = () => { const scale = Math.min(1, 1200/source.width, 600/source.height); const canvas = document.createElement('canvas'); canvas.width = Math.max(1, Math.round(source.width*scale)); canvas.height = Math.max(1, Math.round(source.height*scale)); canvas.getContext('2d').drawImage(source,0,0,canvas.width,canvas.height); canvas.toBlob(blob => { if (!blob) return; const transfer = new DataTransfer(); transfer.items.add(new File([blob], selected.name, {type:selected.type,lastModified:Date.now()})); file.files=transfer.files; image.src=URL.createObjectURL(blob); image.classList.remove('d-none'); empty.classList.add('d-none'); update(); }, selected.type, .9); };
    source.src = URL.createObjectURL(selected);
  }); update();
})();
