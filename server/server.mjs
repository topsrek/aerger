// Kleiner Webserver ohne Abhängigkeiten: liefert dist/ aus (gehashte Dateien unbegrenzt
// cachebar, Rest immer frisch) und /healthz für die Betriebsüberwachung.
import { createReadStream } from 'node:fs';
import { stat } from 'node:fs/promises';
import { createServer } from 'node:http';
import { extname, join, normalize, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../dist/', import.meta.url));
const port = Number(process.env.PORT ?? 8080);
const types = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml', '.ico': 'image/x-icon', '.png': 'image/png', '.wav': 'audio/wav', '.cur': 'image/x-icon',
  '.woff2': 'font/woff2', '.woff': 'font/woff', '.webmanifest': 'application/manifest+json', '.json': 'application/json',
};

async function file(path) {
  try { const s = await stat(path); return s.isFile() ? s : null; } catch { return null; }
}

createServer(async (req, res) => {
  let url;
  try { url = new URL(req.url ?? '/', 'http://localhost'); } catch { res.writeHead(400).end(); return; }
  if (url.pathname === '/healthz') { res.writeHead(200, { 'content-type': 'text/plain' }).end('ok'); return; }
  if (req.method !== 'GET' && req.method !== 'HEAD') { res.writeHead(405).end(); return; }
  let pathname;
  try { pathname = decodeURIComponent(url.pathname); } catch { res.writeHead(400).end(); return; }   // z. B. „/%“
  if (pathname.includes('\0')) { res.writeHead(400).end(); return; }
  let path = normalize(join(root, pathname));
  if (!path.startsWith(root.endsWith(sep) ? root : root + sep) && path !== root) { res.writeHead(403).end(); return; }
  let info = await file(path);
  if (!info) { path = join(root, 'index.html'); info = await file(path); }   // App-Routen
  if (!info) { res.writeHead(404).end(); return; }
  const immutable = url.pathname.startsWith('/assets/');
  res.writeHead(200, {
    'content-type': types[extname(path)] ?? 'application/octet-stream',
    'content-length': info.size,
    'cache-control': immutable ? 'public, max-age=31536000, immutable' : 'no-cache',
    'x-content-type-options': 'nosniff',
    'referrer-policy': 'no-referrer',
  });
  if (req.method === 'HEAD') { res.end(); return; }
  createReadStream(path).on('error', () => res.destroy()).pipe(res);
}).listen(port, () => console.log(`Ärger läuft auf Port ${port}`));
