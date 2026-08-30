(() => {
  'use strict';

  function createCellDetailFeature({ state, ui, getElement, document, mapCells, showDetailMode, workspacePresentation, plannerState, plannerAvailability, getWorkspaceMaxSeverity, severityLabel, escapeHtml }) {
    function render(cell = null) {
      const detail = cell || (state.cellDetail && mapCells(state.cellDetail.mapId).find(item => item.cellId === state.cellDetail.cellId));
      const panel = getElement('detail-panel');
      const section = getElement('cell-detail');
      if (!detail) {
        state.cellDetail = null;
        panel.classList.remove('cell-detail-mode');
        section.classList.add('hidden');
        return;
      }

      state.cellDetail = { mapId: detail.mapId, cellId: detail.cellId };
      showDetailMode('cell-detail', {
        title: detail.customName || detail.cellId,
        summary: detail.customName ? `${detail.cellId} · ${detail.members.length} puestos` : `${detail.members.length} puestos en esta celda`
      });
      getElement('cell-detail-name').value = detail.customName || '';
      getElement('cell-detail-clear-name').disabled = !detail.customName;
      getElement('cell-detail-select-all').textContent = detail.members.every(workspace => state.selectedWorkspaces.has(workspace.id)) ? 'Quitar zona' : 'Seleccionar zona';
      getElement('cell-detail-counts').replaceChildren(...[['Total', detail.composition.total], ['Ocupados', detail.composition.occupied], ['Libres', detail.composition.free], ['Reservados', detail.composition.reserved], ['Problemas', detail.composition.problems]].map(([label, count]) => {
        const item = document.createElement('span');
        item.innerHTML = `<strong>${count}</strong>${escapeHtml(label)}`;
        return item;
      }));
      getElement('cell-detail-list').replaceChildren(...detail.members.map(workspace => {
        const presentation = workspacePresentation(workspace);
        const availability = plannerState().destinationMode ? plannerAvailability(workspace.id) : 'none';
        const plannerLabel = availability === 'available' ? '✓ Destino disponible' : availability === 'unavailable' ? '× No disponible' : '';
        const severity = getWorkspaceMaxSeverity(workspace.id);
        const changed = ui.touchedSeats.has(workspace.id) || ui.changes.some(item => (item.seatId || item.entityId || item.after?.seatId || item.before?.seatId) === workspace.id);
        const row = document.createElement('article');
        row.className = 'cell-detail-workspace';
        const focus = document.createElement('button');
        focus.type = 'button';
        focus.dataset.cellAction = 'focus';
        focus.dataset.workspaceId = workspace.id;
        focus.setAttribute('aria-label', `Abrir ${presentation.displayLocation}`);
        focus.innerHTML = `<span><strong>${escapeHtml(presentation.displayLocation)} · ${escapeHtml(presentation.currentPerson || 'Sin asignar')}</strong><small>${escapeHtml(presentation.assignmentStatusLabel)} · ${escapeHtml(presentation.equipment || 'Sin equipo')}</small><small>${escapeHtml([presentation.networkOutlet || 'Sin roseta', presentation.workstationReference || 'Sin referencia', plannerLabel, severity !== 'None' ? `! ${severityLabel(severity)}` : '', changed ? 'Cambio de escenario' : ''].filter(Boolean).join(' · '))}</small></span>`;
        const select = document.createElement('button');
        select.type = 'button';
        select.className = 'cell-select';
        select.dataset.cellAction = 'select';
        select.dataset.workspaceId = workspace.id;
        select.textContent = state.selectedWorkspaces.has(workspace.id) ? 'Quitar de selección' : 'Añadir a selección';
        select.setAttribute('aria-label', `${state.selectedWorkspaces.has(workspace.id) ? 'Quitar' : 'Añadir'} ${presentation.displayLocation} de la selección`);
        row.append(focus, select);
        return row;
      }));
    }

    return Object.freeze({ render });
  }

  const api = Object.freeze({ createCellDetailFeature });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.CellDetailFeature = api;
})();
