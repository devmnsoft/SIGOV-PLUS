const CACHE_NAME = 'sigov-plus-campo-v13';
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

function isCacheableRequest(request) {
  try {
    var url = new URL(request.url);

    if (url.protocol !== 'http:' && url.protocol !== 'https:') return false;
    if (request.method !== 'GET') return false;
    if (url.pathname.startsWith('/swagger')) return false;
    if (url.pathname.startsWith('/api/health')) return false;
    if (url.pathname.startsWith('/Auth/Login')) return false;

    return true;
  } catch (e) {
    return false;
  }
}

self.addEventListener('install', function (event) {
  event.waitUntil(caches.open(CACHE_NAME).then(function (cache) {
    return cache.addAll(STATIC_ASSETS);
  }).then(function () {
    return self.skipWaiting();
  }));
});

self.addEventListener('activate', function (event) {
  event.waitUntil(caches.keys().then(function (keys) {
    return Promise.all(keys.filter(function (key) {
      return key !== CACHE_NAME;
    }).map(function (key) {
      return caches.delete(key);
    }));
  }).then(function () {
    return self.clients.claim();
  }));
});

self.addEventListener('fetch', function (event) {
  if (!event.request || !isCacheableRequest(event.request)) {
    return;
  }

  event.respondWith(
    caches.match(event.request).then(function (cachedResponse) {
      if (cachedResponse) return cachedResponse;

      return fetch(event.request).then(function (networkResponse) {
        if (networkResponse && networkResponse.ok && isCacheableRequest(event.request)) {
          var clone = networkResponse.clone();

          caches.open(CACHE_NAME).then(function (cache) {
            cache.put(event.request, clone).catch(function () {
              // ignora falhas de cache
            });
          });
        }

        return networkResponse;
      }).catch(function () {
        return cachedResponse || Response.error();
      });
    })
  );
});

self.addEventListener('message', function (event) {
  if (event.data === 'SIGOV_CLEAR_SENSITIVE_CACHE') {
    event.waitUntil(caches.delete(CACHE_NAME));
  }
});
