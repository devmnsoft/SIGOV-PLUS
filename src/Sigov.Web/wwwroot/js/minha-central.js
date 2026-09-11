(function () {
  'use strict';
  const box = document.querySelector('[data-sigov-recent-list]');
  const empty = document.querySelector('[data-sigov-recent-empty]');
  if (!box || !empty || !window.SigovNavigationState) return;

  const rows = window.SigovNavigationState.getRecent().slice(0, 5);
  empty.hidden = rows.length > 0;
  rows.forEach(function (row) {
    const link = document.createElement('a');
    link.className = 'sigov-action-card';
    link.href = row.url;
    const iconBox = document.createElement('span');
    iconBox.className = 'sigov-action-card__icon';
    const icon = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    icon.setAttribute('width', '18'); icon.setAttribute('height', '18'); icon.setAttribute('aria-hidden', 'true');
    const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
    use.setAttribute('href', '#sigov-icon-recent'); icon.appendChild(use); iconBox.appendChild(icon);
    const text = document.createElement('span');
    const title = document.createElement('strong'); title.textContent = row.title;
    const route = document.createElement('small'); route.textContent = row.url;
    text.append(title, route); link.append(iconBox, text); box.appendChild(link);
  });
})();
