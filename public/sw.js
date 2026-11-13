// Offline-Betrieb: App-Hülle und Klänge zwischenspeichern, sonst Netz zuerst.
// Alle Pfade relativ zum Ort dieser Datei, damit das Spiel auch in einem Unterverzeichnis läuft.
const CACHE = 'aerger-v2';
const BASE = new URL('./', self.location.href).pathname;
self.addEventListener('install', e => { self.skipWaiting(); e.waitUntil(caches.open(CACHE).then(c => c.addAll(['./', './manifest.webmanifest', './icon.svg']))); });
self.addEventListener('activate', e => {
  e.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))).then(() => self.clients.claim()));
});
self.addEventListener('fetch', e => {
  const url = new URL(e.request.url);
  if (e.request.method !== 'GET' || url.origin !== location.origin || !url.pathname.startsWith(BASE)) return;
  const path = url.pathname.slice(BASE.length);
  // Gehashte Bundles und Klänge ändern sich nie: Cache zuerst. Alles andere: Netz zuerst.
  const immutable = path.startsWith('assets/') || path.startsWith('sounds/') || path.startsWith('cursors/');
  e.respondWith(immutable
    ? caches.match(e.request).then(hit => hit ?? fetch(e.request).then(r => { const copy = r.clone(); caches.open(CACHE).then(c => c.put(e.request, copy)); return r; }))
    : fetch(e.request).then(r => { const copy = r.clone(); caches.open(CACHE).then(c => c.put(e.request, copy)); return r; }).catch(() => caches.match(e.request).then(hit => hit ?? caches.match('./'))));
});
