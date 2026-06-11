const SIGOV_CACHE = 'sigov-plus-campo-v12';
const STATIC_ASSETS = [
  '/',
  '/Mobile',
  '/Mobile/Home',
  '/Mobile/Agenda',
  '/Mobile/Atividades',
  '/Mobile/Sync',
  '/Mobile/Offline',
  '/Campo/Dashboard',
  '/offline',
  '/manifest.json',
  '/css/site.css',
  '/css/sigov-layout.css',
  '/css/sigov-components.css',
  '/lib/bootstrap/css/bootstrap.min.css',
  '/lib/bootstrap/js/bootstrap.bundle.min.js',
  '/js/sigov.mobile.js'
];

self.addEventListener('install', event => {
  event.waitUntil(caches.open(SIGOV_CACHE).then(cache => cache.addAll(STATIC_ASSETS)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key !== SIGOV_CACHE).map(key => caches.delete(key)))).then(() => self.clients.claim()));
});

self.addEventListener('fetch', event => {
  const request = event.request;
  if (request.method !== 'GET') return;
  if (request.url.includes('/api/')) {
    event.respondWith(fetch(request).catch(() => new Response(JSON.stringify({ success: false, message: 'Offline: dado será sincronizado depois.' }), { headers: { 'Content-Type': 'application/json' } })));
    return;
  }
  event.respondWith(fetch(request).then(response => {
    const copy = response.clone();
    caches.open(SIGOV_CACHE).then(cache => cache.put(request, copy));
    return response;
  }).catch(() => caches.match(request).then(cached => cached || caches.match('/offline') || caches.match('/Mobile/Offline'))));
});

self.addEventListener('message', event => {
  if (event.data === 'SIGOV_CLEAR_SENSITIVE_CACHE') {
    event.waitUntil(caches.delete(SIGOV_CACHE));
  }
});
