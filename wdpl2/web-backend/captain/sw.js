// captain/sw.js — minimal service worker for the WDPL Captain Portal.
// Strategy:
//   * Static shell (index.html, manifest) → cache-first, fall back to network.
//   * API calls (../api/**) → network-first (always try fresh, cache as fallback for GET).
//   * Bump CACHE_VERSION whenever the shell changes so old clients refresh.

const CACHE_VERSION = 'wdpl-captain-v3';
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

self.addEventListener('fetch', function (event) {
  const req = event.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  const isApi = url.pathname.indexOf('/api/') !== -1;

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

  // Cache-first for shell + assets.
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
