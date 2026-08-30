(() => {
  'use strict';

  const supportedThemes = Object.freeze(['professional-light', 'penpot-dark', 'high-contrast', 'projector']);

  function createUiThemeFeature({ document, themeSelect }) {
    function apply(theme) {
      const selected = supportedThemes.includes(theme) ? theme : 'professional-light';
      document.documentElement.dataset.theme = selected;
      themeSelect.value = selected;
      return selected;
    }

    return Object.freeze({ apply, supportedThemes });
  }

  const api = Object.freeze({ createUiThemeFeature, supportedThemes });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.UiThemeFeature = api;
})();
