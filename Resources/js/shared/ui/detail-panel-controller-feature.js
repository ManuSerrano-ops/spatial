(() => {
  'use strict';

  const detailTargets = Object.freeze(['inspector-detail', 'selection-review', 'cell-detail', 'area-detail']);
  const detailModeClasses = Object.freeze(['inspector-mode', 'selection-review-mode', 'cell-detail-mode', 'area-detail-mode']);

  function createDetailPanelControllerFeature({ state, ui, getElement, headerFor, deriveClosedDetailState, render }) {
    function setHeader(mode, values = {}) {
      const header = headerFor(mode, values);
      getElement('seat-kicker').textContent = header.kicker;
      getElement('title').textContent = header.title;
      getElement('detail').textContent = header.summary;
      return header;
    }

    function show(mode, values = {}) {
      const panel = getElement('detail-panel');
      const header = setHeader(mode, values);
      panel.dataset.mode = header.mode;
      panel.classList.remove('hidden', ...detailModeClasses);
      panel.classList.add(`${header.mode}-mode`);
      const target = header.mode === 'inspector' ? 'inspector-detail' : header.mode;
      detailTargets.forEach(id => getElement(id).classList.toggle('hidden', id !== target));
      return panel;
    }

    function close(options = {}) {
      const closed = deriveClosedDetailState(state);
      const preservedAreaFocus = options.preserveAreaFocus ? state.activeAreaFocus : closed.activeAreaFocus;
      ui.seatId = closed.selectedWorkspace;
      ui.assignmentBaseline = null;
      state.cellDetail = closed.cellDetail;
      state.areaDetail = null;
      state.activeClusterFocus = null;
      state.activeAreaFocus = preservedAreaFocus;
      const panel = getElement('detail-panel');
      panel.removeAttribute('data-mode');
      panel.classList.remove(...detailModeClasses);
      panel.classList.add('hidden');
      detailTargets.forEach(id => getElement(id).classList.add('hidden'));
      if (options.render !== false) render();
    }

    return Object.freeze({ close, setHeader, show });
  }

  const api = Object.freeze({ createDetailPanelControllerFeature });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.DetailPanelControllerFeature = api;
})();
