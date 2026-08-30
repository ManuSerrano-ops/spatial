(() => {
  'use strict';

  function createCellAppearanceFeature({ cellMetadataHelpers, state, storage, onChanged }) {
    function appearanceForCell(mapId, cellId) {
      const stored = state.gridCellAppearances[cellMetadataHelpers.cellIdentity(mapId, cellId)] || {};
      return { style: 'compact', offsetX: Number(stored.offsetX) || 0, offsetY: Number(stored.offsetY) || 0 };
    }

    function load() {
      try {
        state.gridCellAppearances = JSON.parse(storage.getItem('plano.gridCellAppearances') || '{}') || {};
      } catch {
        state.gridCellAppearances = {};
      }
      return state.gridCellAppearances;
    }

    function save() {
      try {
        storage.setItem('plano.gridCellAppearances', JSON.stringify(state.gridCellAppearances));
      } catch {
        /* Local presentation preference remains best-effort. */
      }
    }

    function updateFor(mapId, cellId, patch) {
      const key = cellMetadataHelpers.cellIdentity(mapId, cellId);
      state.gridCellAppearances = {
        ...state.gridCellAppearances,
        [key]: { ...appearanceForCell(mapId, cellId), ...patch }
      };
      save();
      onChanged();
    }

    function update(patch) {
      const detail = state.cellDetail;
      if (detail) updateFor(detail.mapId, detail.cellId, patch);
    }

    return Object.freeze({ appearanceForCell, load, save, update, updateFor });
  }

  const api = Object.freeze({ createCellAppearanceFeature });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.CellAppearanceFeature = api;
})();
