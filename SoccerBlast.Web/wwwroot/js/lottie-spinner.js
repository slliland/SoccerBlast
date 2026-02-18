// Global Lottie spinner helper for Blazor JS interop.
// Depends on lottie-web loaded before this script (e.g. from CDN).
(function () {
  var instances = {};

  window.soccerBlastLottie = {
    init: function (containerId, animationPath, width, height) {
      var el = document.getElementById(containerId);
      if (!el) return null;
      if (!window.lottie) {
        console.warn('[soccerBlastLottie] lottie-web not loaded');
        return null;
      }
      el.innerHTML = '';
      var anim = window.lottie.loadAnimation({
        container: el,
        renderer: 'svg',
        loop: true,
        autoplay: true,
        path: animationPath
      });
      if (width != null && width > 0) el.style.width = width + 'px';
      if (height != null && height > 0) el.style.height = height + 'px';
      instances[containerId] = anim;
      return containerId;
    },
    destroy: function (containerId) {
      var anim = instances[containerId];
      if (anim) {
        try { anim.destroy(); } catch (e) { }
        delete instances[containerId];
      }
      var el = document.getElementById(containerId);
      if (el) el.innerHTML = '';
    }
  };
})();
