(() => {
  'use strict';

  const clamp = value => Math.max(0, Math.min(1, Number(value) || 0));

  function clientToNormalized(point, stageRect) {
    return Object.freeze({ x: clamp((point.clientX - stageRect.left) / stageRect.width), y: clamp((point.clientY - stageRect.top) / stageRect.height) });
  }

  function normalizeRectangle(start, end) {
    return Object.freeze({ minX: Math.min(start.x, end.x), maxX: Math.max(start.x, end.x), minY: Math.min(start.y, end.y), maxY: Math.max(start.y, end.y) });
  }

  function selectByCenter(seats = [], start, end, isVisible = () => true) {
    const rectangle = normalizeRectangle(start, end);
    return seats.filter(seat => isVisible(seat) && Number(seat.x) >= rectangle.minX && Number(seat.x) <= rectangle.maxX && Number(seat.y) >= rectangle.minY && Number(seat.y) <= rectangle.maxY);
  }

  const api = { clientToNormalized, normalizeRectangle, selectByCenter };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.RectangleSelectionHelpers = api;
})();
