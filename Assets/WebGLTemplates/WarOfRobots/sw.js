// War of Robots offline cache.
//
// Lets the installed app (home screen icon) start quickly and without a signal.
// Every deploy stamps a new BUILD id below (GitHub Actions replaces it), which
// gives a new cache: the game files of one build are always used together, and
// old builds are deleted once the new one takes over.
var BUILD = "__WOR_BUILD__";
var CACHE = "wor-" + BUILD;

self.addEventListener("install", function (event) {
  // The page shell is tiny, so keep it ready for offline starts. The big game files
  // are cached the first time the game downloads them (no double download).
  event.waitUntil(
    caches.open(CACHE).then(function (cache) {
      return cache.addAll(["./", "manifest.webmanifest", "TemplateData/logo-title.webp"]).catch(function () {});
    }).then(function () { return self.skipWaiting(); })
  );
});

self.addEventListener("activate", function (event) {
  event.waitUntil(
    caches.keys().then(function (keys) {
      return Promise.all(keys.map(function (k) {
        if (k.indexOf("wor-") === 0 && k !== CACHE) return caches.delete(k);
      }));
    }).then(function () { return self.clients.claim(); })
  );
});

function fromNetwork(request, cache) {
  // "no-cache" asks the server if the file changed (cheap when it hasn't), so a
  // stale copy in the browser's own cache can't mix with a newer build.
  return fetch(request, { cache: "no-cache" }).then(function (response) {
    if (response && response.ok && response.type === "basic") {
      cache.put(request, response.clone()).catch(function () {});
    }
    return response;
  });
}

self.addEventListener("fetch", function (event) {
  var request = event.request;
  if (request.method !== "GET") return;
  var url = new URL(request.url);
  if (url.origin !== self.location.origin) return; // online play, fonts etc. go straight out
  if (request.headers.has("range")) return;

  if (request.mode === "navigate") {
    // The page itself: newest when online, saved copy when offline.
    event.respondWith(caches.open(CACHE).then(function (cache) {
      return fromNetwork(new Request("./"), cache).catch(function () {
        return cache.match("./").then(function (hit) { return hit || Response.error(); });
      });
    }));
    return;
  }

  // Game files (Build/, TemplateData/, StreamingAssets/): from this build's cache,
  // otherwise download once and keep.
  event.respondWith(caches.open(CACHE).then(function (cache) {
    return cache.match(request, { ignoreSearch: true }).then(function (hit) {
      return hit || fromNetwork(request, cache);
    });
  }));
});
