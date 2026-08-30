(() => {
  'use strict';

  const modes = new Set(['inspector', 'selection-review', 'cell-detail', 'area-detail']);

  function normalizeMode(value) {
    return modes.has(String(value || '')) ? String(value) : 'inspector';
  }

  function deriveClosedDetailState(source = {}) {
    return Object.freeze({
      selectedWorkspace: null,
      selectedWorkspaces: source.selectedWorkspaces,
      cellDetail: null,
      activeAreaFocus: null,
      mode: null
    });
  }

  function headerFor(mode, values = {}) {
    const resolved = normalizeMode(mode);
    const defaults = {
      inspector: ['UBICACIÓN', 'Selecciona un puesto', ''],
      'selection-review': ['SELECCIÓN', 'Puestos seleccionados', ''],
      'cell-detail': ['CELDA DEL PLANO', 'Celda', ''],
      'area-detail': ['ÁREA GESTIONADA', 'Área', '']
    }[resolved];
    return Object.freeze({
      mode: resolved,
      kicker: String(values.kicker ?? defaults[0]),
      title: String(values.title ?? defaults[1]),
      summary: String(values.summary ?? defaults[2])
    });
  }

  const api = { normalizeMode, deriveClosedDetailState, headerFor };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.DetailPanelHelpers = api;
})();
