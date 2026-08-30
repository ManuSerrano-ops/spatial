(() => {
  'use strict';

  const finite = (value, fallback = 0) => Number.isFinite(Number(value)) ? Number(value) : fallback;
  const clamp = (value, minimum, maximum) => Math.min(maximum, Math.max(minimum, value));

  function snapshotViewport(source = {}) {
    return Object.freeze({
      scale: finite(source.targetScale ?? source.scale, 1),
      x: finite(source.targetX ?? source.x),
      y: finite(source.targetY ?? source.y)
    });
  }

  function calculateInitialFit(viewport = {}, content = {}, options = {}) {
    const viewportWidth = Math.max(1, finite(viewport.width, 1));
    const viewportHeight = Math.max(1, finite(viewport.height, 1));
    const contentWidth = Math.max(1, finite(content.width, viewportWidth));
    const contentHeight = Math.max(1, finite(content.height, viewportHeight));
    const minimum = Math.max(.05, finite(options.minimumScale, .1));
    const maximum = Math.max(minimum, finite(options.maximumScale, 1));
    const scale = clamp(Math.min(viewportWidth / contentWidth, viewportHeight / contentHeight), minimum, maximum);
    return Object.freeze({
      scale,
      x: (viewportWidth - contentWidth * scale) / 2,
      y: (viewportHeight - contentHeight * scale) / 2
    });
  }

  function sameViewport(left, right, tolerance = .0001) {
    const first = snapshotViewport(left); const second = snapshotViewport(right);
    return Math.abs(first.scale - second.scale) <= tolerance && Math.abs(first.x - second.x) <= tolerance && Math.abs(first.y - second.y) <= tolerance;
  }

  const api = { snapshotViewport, calculateInitialFit, sameViewport };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.MapViewportHelpers = api;
})();
