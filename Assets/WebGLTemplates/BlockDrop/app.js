// Block Drop web loader: starts the Unity build, shows progress, registers the offline service worker
// and offers install. No inline scripts, so the page's Content-Security-Policy can stay strict.
(function () {
  "use strict";

  var cfg = document.body.dataset;
  var canvas = document.getElementById("game");
  var splash = document.getElementById("splash");
  var fill = document.getElementById("fill");
  var bar = splash.querySelector(".bar");
  var statusEl = document.getElementById("status");
  var toolbar = document.getElementById("toolbar");
  var installBtn = document.getElementById("install");
  var fullscreenBtn = document.getElementById("fullscreen");
  var toastEl = document.getElementById("toast");
  var wide = window.matchMedia("(min-aspect-ratio: 3/4)");

  // Read by the CI smoke test.
  var state = window.__blockDrop = { ready: false, error: null, offlineReady: false };

  function setStatus(text, isError) {
    statusEl.textContent = text;
    statusEl.classList.toggle("error", !!isError);
  }

  var toastTimer = 0;
  function toast(text, actionLabel, action, ms) {
    toastEl.textContent = text + " ";
    if (actionLabel) {
      var b = document.createElement("button");
      b.type = "button";
      b.textContent = actionLabel;
      b.style.cssText = "margin-left:8px;border:0;border-radius:999px;padding:6px 12px;font-weight:700;background:#FFC24B;color:#2A0E61;cursor:pointer";
      b.addEventListener("click", function () { toastEl.hidden = true; action(); });
      toastEl.appendChild(b);
    }
    toastEl.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.hidden = true; }, ms || 5000);
  }

  function fail(message) {
    state.error = message;
    setStatus(message, true);
    splash.classList.remove("done");
  }

  // ---------- Unity ----------

  function start() {
    var script = document.createElement("script");
    script.src = cfg.loader;
    script.onerror = function () {
      fail(navigator.onLine ? "Couldn't load the game. Please refresh." : "You're offline. Open Block Drop once while online to play it offline.");
    };
    script.onload = function () {
      createUnityInstance(canvas, {
        dataUrl: cfg.data,
        frameworkUrl: cfg.framework,
        codeUrl: cfg.code,
        streamingAssetsUrl: "StreamingAssets",
        companyName: cfg.company,
        productName: cfg.product,
        productVersion: cfg.version,
        // Sharp on phones without rendering 9x the pixels on 3x screens.
        devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2),
        showBanner: function (msg, type) { if (type === "error") fail(msg); else console.warn(msg); }
      }, function (progress) {
        var pct = Math.round(progress * 100);
        fill.style.width = pct + "%";
        bar.setAttribute("aria-valuenow", String(pct));
        setStatus(pct < 100 ? "Loading… " + pct + "%" : "Starting…");
      }).then(function (instance) {
        window.unityInstance = instance;
        state.ready = true;
        splash.classList.add("done");
        toolbar.hidden = false;
        canvas.focus();
        offerInstallOnPhone();
      }).catch(function (err) {
        fail("Couldn't start the game: " + (err && err.message ? err.message : err));
      });
    };
    document.body.appendChild(script);
  }

  // Touches on the game never scroll, zoom or pull-to-refresh the page.
  ["touchstart", "touchmove"].forEach(function (type) {
    canvas.addEventListener(type, function (e) { if (e.cancelable) e.preventDefault(); }, { passive: false });
  });
  document.addEventListener("gesturestart", function (e) { e.preventDefault(); });
  canvas.addEventListener("contextmenu", function (e) { e.preventDefault(); });

  // ---------- full screen (desktop) ----------

  var root = document.documentElement;
  if (!(root.requestFullscreen || root.webkitRequestFullscreen)) fullscreenBtn.hidden = true;
  fullscreenBtn.addEventListener("click", function () {
    if (document.fullscreenElement || document.webkitFullscreenElement) {
      (document.exitFullscreen || document.webkitExitFullscreen).call(document);
    } else {
      (root.requestFullscreen || root.webkitRequestFullscreen).call(root);
    }
    canvas.focus();
  });

  // ---------- install ----------

  var deferredInstall = null;
  function install() {
    if (!deferredInstall) return;
    deferredInstall.prompt();
    deferredInstall.userChoice.finally(function () { deferredInstall = null; installBtn.hidden = true; });
  }
  window.addEventListener("beforeinstallprompt", function (e) {
    e.preventDefault();
    deferredInstall = e;
    installBtn.hidden = !wide.matches;
    if (state.ready) offerInstallOnPhone();
  });
  window.addEventListener("appinstalled", function () { deferredInstall = null; installBtn.hidden = true; });
  installBtn.addEventListener("click", install);

  var offeredInstall = false;
  function offerInstallOnPhone() {
    // On phones the toolbar would cover the HUD, so the offer is a one-time toast after the game loads.
    if (offeredInstall || !deferredInstall || wide.matches) return;
    offeredInstall = true;
    toast("Play offline from your home screen.", "Install", install, 8000);
  }

  // ---------- offline ----------

  if ("serviceWorker" in navigator && (location.protocol === "https:" || location.hostname === "localhost" || location.hostname === "127.0.0.1")) {
    window.addEventListener("load", function () {
      navigator.serviceWorker.register("service-worker.js").then(function (reg) {
        if (reg.waiting && navigator.serviceWorker.controller) offerUpdate(reg.waiting);
        reg.addEventListener("updatefound", function () {
          var sw = reg.installing;
          if (!sw) return;
          sw.addEventListener("statechange", function () {
            if (sw.state !== "installed") return;
            if (navigator.serviceWorker.controller) offerUpdate(sw);
            else { state.offlineReady = true; toast("Ready to play offline."); }
          });
        });
      }).catch(function (e) { console.warn("Service worker not registered:", e); });
      if (navigator.storage && navigator.storage.persist) navigator.storage.persist();
    });

    // Only reload when the player pressed Update. The first install also changes the controller
    // (clients.claim), and that must not restart a game that just loaded.
    navigator.serviceWorker.addEventListener("controllerchange", function () {
      if (!updateRequested || reloading) return;
      reloading = true;
      location.reload();
    });
  }

  var updateRequested = false, reloading = false;
  function offerUpdate(worker) {
    // Never reload mid-run on our own; the player chooses when.
    toast("A new version of Block Drop is ready.", "Update", function () {
      updateRequested = true;
      worker.postMessage("skipWaiting");
    }, 15000);
  }

  window.addEventListener("offline", function () { if (state.ready) toast("You're offline. The game keeps going; rankings sync later."); });

  start();
})();
