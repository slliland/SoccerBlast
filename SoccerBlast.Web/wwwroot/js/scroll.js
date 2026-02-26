// Scroll helpers for All Matches page
window.soccerBlastScrollToDay = function (elementId) {
  var el = document.getElementById(elementId);
  if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};

window.soccerBlastScrollToTop = function () {
  window.scrollTo({ top: 0, behavior: 'smooth' });
};

// When user scrolls to top (without clicking Top), hide the button by notifying Blazor
window.soccerBlastRegisterScrollToTopListener = function (dotNetRef) {
  var handler = function () {
    if (window.scrollY < 80) {
      dotNetRef.invokeMethodAsync('OnScrolledToTop');
      window.removeEventListener('scroll', handler);
    }
  };
  window.addEventListener('scroll', handler);
};
