// captain/sw.js — minimal service worker for the WDPL Captain Portal.
// Strategy:
//   * HTML navigations (the shell)   → network-first, fall back to cache (so style/markup changes appear immediately after a deploy).
//   * API calls (../api/**)          → network-first, fall back to cache for GETs.
//   * Everything else (assets)       → cache-first, fall back to network.
//   * Bump CACHE_VERSION whenever the shell changes so old clients refresh.

const CACHE_VERSION = 'wdpl-captain-v13';
const SHELL = [
  './',
  './index.html',
  './manifest.webmanifest',
];

self.addEventListener('install', function (event) {
  event.waitUntil(
    caches.open(CACHE_VERSION).then(function (cache) {
      return cache.addAll(SHELL).catch(function () { /* offline install OK */ });
    }).then(function () { return self.skipWaiting(); })
  );
});

self.addEventListener('activate', function (event) {
  event.waitUntil(
    caches.keys().then(function (keys) {
      return Promise.all(keys.map(function (k) {
        if (k !== CACHE_VERSION) return caches.delete(k);
      }));
    }).then(function () { return self.clients.claim(); })
  );
});

// Allow the page to ask the SW to skip waiting (instant update on demand).
self.addEventListener('message', function (e) {
  if (e && e.data === 'skipWaiting') self.skipWaiting();
});

self.addEventListener('fetch', function (event) {
  const req = event.request;
  if (req.method !== 'GET') return;

  const url = new URL(req.url);
  const isApi  = url.pathname.indexOf('/api/') !== -1;
  const isHtml = req.mode === 'navigate' ||
                 (req.headers.get('accept') || '').indexOf('text/html') !== -1;

  // HTML navigations: always try the network first so deployed style/markup
  // changes appear without a manual cache clear.
  if (isHtml) {
    event.respondWith(
      fetch(req).then(function (res) {
        if (res && res.ok) {
          const copy = res.clone();
          caches.open(CACHE_VERSION).then(function (c) { c.put(req, copy); }).catch(function(){});
        }
        return res;
      }).catch(function () {
        return caches.match(req).then(function (cached) {
          return cached || caches.match('./index.html');
        });
      })
    );
    return;
  }

  if (isApi) {
    // Network-first for API GETs.
    event.respondWith(
      fetch(req).then(function (res) {
        if (res && res.ok) {
          const copy = res.clone();
          caches.open(CACHE_VERSION).then(function (c) { c.put(req, copy); }).catch(function(){});
        }
        return res;
      }).catch(function () {
        return caches.match(req).then(function (cached) {
          return cached || new Response(JSON.stringify({ error: 'offline' }), {
            status: 503, headers: { 'Content-Type': 'application/json' }
          });
        });
      })
    );
    return;
  }

  // Cache-first for shell + static assets.
  event.respondWith(
    caches.match(req).then(function (cached) {
      return cached || fetch(req).then(function (res) {
        if (res && res.ok) {
          const copy = res.clone();
          caches.open(CACHE_VERSION).then(function (c) { c.put(req, copy); }).catch(function(){});
        }
        return res;
      }).catch(function () { return cached; });
    })
  );
});
