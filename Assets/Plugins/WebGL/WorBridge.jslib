mergeInto(LibraryManager.library, {
  // Sets how many screen pixels per CSS pixel the game renders at (sharper text and
  // edges vs. speed). Remembered for the next visit and applied right away.
  WorSetPixelRatio: function (ratio) {
    try { localStorage.setItem("wor_dpr", String(ratio)); } catch (e) {}
    var r = Math.min(ratio, window.devicePixelRatio || 1);
    if (typeof Module !== "undefined") Module.devicePixelRatio = r;
    if (window.worConfig) window.worConfig.devicePixelRatio = r;
    if (window.worResize) window.worResize();
  },

  // Home screen install (see index.html): 0 nothing to offer, 1 the browser can
  // install it now, 2 iPhone/iPad steps, 3 Android menu steps.
  WorInstallState: function () {
    try { return window.worInstall ? window.worInstall.state() : 0; } catch (e) { return 0; }
  },

  // Opens the browser's own install sheet. Called from a button press, which the
  // browser requires; Unity forwards it within the same tap.
  WorInstallPrompt: function () {
    try { if (window.worInstall) window.worInstall.prompt(); } catch (e) {}
  }
});
