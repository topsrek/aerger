// Baut aus dist/ eine einzige HTML-Datei (Skript, Stil, Schrift, Mauszeiger und Klänge eingebettet),
// die sich ohne Server per Doppelklick im Browser spielen lässt:  npm run standalone
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, extname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const dist = join(root, 'dist');
const types = { '.woff2': 'font/woff2', '.woff': 'font/woff', '.cur': 'image/x-icon', '.ico': 'image/x-icon', '.svg': 'image/svg+xml' };
const dataUrl = file => `data:${types[extname(file)]};base64,${readFileSync(file).toString('base64')}`;

let html = readFileSync(join(dist, 'index.html'), 'utf8');
const need = (found, what) => { if (!found) throw new Error(`dist/index.html: ${what} nicht gefunden – vorher „npm run build“?`); return found; };

const cssTag = need(html.match(/<link rel="stylesheet"[^>]*href="\.\/(assets\/[^"]+\.css)"[^>]*>/), 'Stylesheet');
const cssFile = join(dist, cssTag[1]);
const css = readFileSync(cssFile, 'utf8').replace(/url\((["']?)(?!data:)([^)"']+)\1\)/g, (_, _q, ref) => `url(${dataUrl(join(dirname(cssFile), ref))})`);

const jsTag = need(html.match(/<script type="module"[^>]*src="\.\/(assets\/[^"]+\.js)"[^>]*><\/script>/), 'Skript');
const js = readFileSync(join(dist, jsTag[1]), 'utf8').replace(/<\/script/gi, '<\/script');
const sounds = Array.from({ length: 14 }, (_, i) => readFileSync(join(dist, 'sounds', `${i + 1}.wav`)).toString('base64'));

html = html
  .replace(cssTag[0], () => `<style>${css}</style>`)
  .replace(jsTag[0], '')
  .replace(/<link rel="manifest"[^>]*>\s*/, '')
  .replace(/<link rel="icon"[^>]*>/, () => `<link rel="icon" href="${dataUrl(join(dist, 'favicon.ico'))}" />`)
  .replace('</body>', () => `<script>globalThis.AERGER_SOUNDS=${JSON.stringify(sounds)}</script>\n    <script type="module">${js}</script>\n  </body>`);
if (/(src|href)="\.\//.test(html)) throw new Error('Es sind noch Verweise auf Dateien übrig.');

const out = join(root, 'dist-standalone');
mkdirSync(out, { recursive: true });
writeFileSync(join(out, 'Aerger.html'), html);
console.log(`dist-standalone/Aerger.html (${Math.round(html.length / 1024)} kB)`);
