// Offline support for the web build. WebBuild.cs fills in the build id and the exact file list after
// each build, so the first visit stores everything needed to play and a new deploy replaces the old cache.
"use strict";

var BUILD_ID = "__BUILD_ID__";
var CACHE = "blockdrop-" + BUILD_ID;
var PRECACHE = [/*__PRECACHE__*/];

self.addEventListener("install", function (event) {
  event.waitUntil(caches.open(CACHE).then(function (cache) { return cache.addAll(PRECACHE); }));
});

self.addEventListener("activate", function (event) {
  event.waitUntil(
    caches.keys().then(function (keys) {
      return Promise.all(keys.map(function (k) {
        if (k.indexOf("blockdrop-") === 0 && k !== CACHE) return caches.delete(k);
      }));
    }).then(function () { return self.clients.claim(); })
  );
});

self.addEventListener("message", function (event) {
  if (event.data === "skipWaiting") self.skipWaiting();
});

self.addEventListener("fetch", function (event) {
  var req = event.request;
  if (req.method !== "GET") return;
  var url = new URL(req.url);
  if (url.origin !== self.location.origin) return; // Unity Gaming Services calls always go to the network

  // Pages: network first so a fresh deploy shows up, cache when offline.
  if (req.mode === "navigate") {
    event.respondWith(
      fetch(req).catch(function () {
        return caches.match(req, { ignoreSearch: true }).then(function (hit) { return hit || caches.match("./"); });
      })
    );
    return;
  }

  // Build files have hashed names, so a cached copy is always the right one.
  event.respondWith(
    caches.match(req, { ignoreSearch: true }).then(function (hit) {
      if (hit) return hit;
      return fetch(req).then(function (res) {
        if (res.ok && res.type === "basic") {
          var copy = res.clone();
          caches.open(CACHE).then(function (cache) { cache.put(req, copy); });
        }
        return res;
      });
    })
  );
});
