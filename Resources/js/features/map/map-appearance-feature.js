(() => {
  "use strict";

  function createMapAppearanceFeature({ appearanceHelpers, state, storage, document }) {
    function loadPreference() {
      try { return appearanceHelpers.normalizeMode(storage.getItem("plano.mapAppearance")); } catch { return "dark"; }
    }

    function apply(mode) {
      const presentation = appearanceHelpers.deriveMapAppearance(mode);
      state.mapAppearance = presentation.mode;
      document.documentElement.dataset.mapAppearance = presentation.mode;
      Object.entries(presentation.tokens).forEach(([key, value]) => {
        const cssName = "--map-" + key.replace(/[A-Z]/g, letter => "-" + letter.toLowerCase());
        document.documentElement.style.setProperty(cssName, value);
      });
      document.querySelectorAll("[data-map-appearance]").forEach(button => {
        button.setAttribute("aria-pressed", String(button.dataset.mapAppearance === presentation.mode));
      });
      return presentation.mode;
    }

    function savePreference(mode) {
      try { storage.setItem("plano.mapAppearance", mode); } catch { /* Local visual preference remains best-effort. */ }
    }

    return Object.freeze({ loadPreference, apply, savePreference });
  }

  const api = Object.freeze({ createMapAppearanceFeature });
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  if (typeof window !== "undefined") window.MapAppearanceFeature = api;
})();
