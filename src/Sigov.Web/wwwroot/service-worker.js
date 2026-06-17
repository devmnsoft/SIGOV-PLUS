const CACHE_NAME = 'sigov-plus-campo-v14';
const STATIC_ASSETS = [
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
const BLOCKED_PROTOCOLS = ['chrome-extension:', 'moz-extension:', 'edge-extension:', 'data:', 'blob:', 'about:'];
const BLOCKED_PATHS = ['/Dashboard', '/Auth/Login', '/swagger', '/api', '/api/health'];

function isCacheableRequest(request) {
  try {
    if (!request || request.method !== 'GET') return false;

    var url = new URL(request.url);

    if (BLOCKED_PROTOCOLS.indexOf(url.protocol) >= 0) return false;
    if (url.protocol !== 'http:' && url.protocol !== 'https:') return false;

    return !BLOCKED_PATHS.some(function (path) {
      return url.pathname === path || url.pathname.indexOf(path + '/') === 0;
    });
  } catch (e) {
    return false;
  }
}

function safeCacheAdd(cache, asset) {
  var request = new Request(asset);

  if (!isCacheableRequest(request)) return Promise.resolve();

  return fetch(request)
    .then(function (response) {
      if (!response || !response.ok) return;
      return cache.put(request, response).catch(function () { });
    })
    .catch(function () { });
}

self.addEventListener('install', function (event) {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(function (cache) {
        return Promise.all(STATIC_ASSETS.map(function (asset) {
          return safeCacheAdd(cache, asset);
        })).catch(function () { });
      })
      .then(function () {
        return self.skipWaiting();
      })
      .catch(function () { })
  );
});

self.addEventListener('activate', function (event) {
  event.waitUntil(
    caches.keys()
      .then(function (keys) {
        return Promise.all(keys.filter(function (key) {
          return key !== CACHE_NAME;
        }).map(function (key) {
          return caches.delete(key).catch(function () { });
        })).catch(function () { });
      })
      .then(function () {
        return self.clients.claim();
      })
      .catch(function () { })
  );
});

self.addEventListener('fetch', function (event) {
  if (!event.request || !isCacheableRequest(event.request)) {
    return;
  }

  event.respondWith(
    caches.match(event.request)
      .then(function (cachedResponse) {
        if (cachedResponse) return cachedResponse;

        return fetch(event.request)
          .then(function (networkResponse) {
            if (networkResponse && networkResponse.ok && isCacheableRequest(event.request)) {
              var clone = networkResponse.clone();

              caches.open(CACHE_NAME)
                .then(function (cache) {
                  return cache.put(event.request, clone).catch(function () { });
                })
                .catch(function () { });
            }

            return networkResponse;
          })
          .catch(function () {
            return cachedResponse || Response.error();
          });
      })
      .catch(function () {
        return fetch(event.request).catch(function () {
          return Response.error();
        });
      })
  );
});

self.addEventListener('message', function (event) {
  if (event.data === 'SIGOV_CLEAR_SENSITIVE_CACHE') {
    event.waitUntil(caches.delete(CACHE_NAME).catch(function () { }));
  }
});
