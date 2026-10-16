// Browser bridges for the web build. Each call is a no-op where the browser lacks the feature.
mergeInto(LibraryManager.library, {
  BlockDropVibrate: function (ms) {
    try { if (navigator.vibrate) navigator.vibrate(ms); } catch (e) { }
  }
});
