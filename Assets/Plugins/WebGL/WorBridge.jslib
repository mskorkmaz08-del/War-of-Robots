mergeInto(LibraryManager.library, {
  // Sets how many screen pixels per CSS pixel the game renders at (sharper text and
  // edges vs. speed). Remembered for the next visit and applied right away.
  WorSetPixelRatio: function (ratio) {
    try { localStorage.setItem("wor_dpr", String(ratio)); } catch (e) {}
    var r = Math.min(ratio, window.devicePixelRatio || 1);
    if (typeof Module !== "undefined") Module.devicePixelRatio = r;
    if (window.worConfig) window.worConfig.devicePixelRatio = r;
    if (window.worResize) window.worResize();
  }
});
