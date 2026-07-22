import { readFileSync, existsSync } from 'node:fs';
const candidates = [
  'src/Sigov.Web/wwwroot/css/sigov-tokens.css',
  'src/Sigov.Web/wwwroot/css/site.css'
];
const found = candidates.filter(existsSync);
if (found.length === 0) {
  console.error('Nenhum CSS de tokens/site encontrado para validação de contraste.');
  process.exit(1);
}
const css = found.map((file) => readFileSync(file, 'utf8')).join('\n');
const required = ['--sigov-primary', '--sigov-bg-page', '--sigov-bg-surface', '--sigov-text-primary', '--sigov-text-on-primary', '--sigov-focus'];
const missing = required.filter((token) => !css.includes(token));
if (missing.length) {
  console.error(`Tokens de contraste ausentes: ${missing.join(', ')}`);
  process.exit(1);
}
console.log('ui-contrast: PASS');
