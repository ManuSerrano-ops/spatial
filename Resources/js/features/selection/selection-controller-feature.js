(() => {
  'use strict';

  function createSelectionControllerFeature({ state, ui, getElement, canActivateMode, closeDetailPanel, renderBulkBar, render, deselectWorkspace }) {
    function setMode(active) {
      const enabled = Boolean(active) && canActivateMode();
      ui.selectionMode = enabled;
      const button = getElement('selection-mode');
      button.classList.toggle('active', enabled);
      button.setAttribute('aria-pressed', String(enabled));
      button.textContent = enabled ? '✓ Seleccionando' : 'Seleccionar';
      button.title = enabled ? 'Finalizar selección sin limpiar puestos' : 'Seleccionar puestos';
      return enabled;
    }

    function markBulkSelectionChanged() {
      state.bulk.lastCommitted = null;
      state.bulk.undoRequested = false;
    }

    function clearWorkspaceSelection({ preserveAreaDetail = false, closeAreaFocus = false } = {}) {
      state.selectedWorkspaces.clear();
      state.selectionAnchor = null;
      state.bulk.pendingAction = 'reserved';
      state.bulk.inFlight = null;
      state.bulk.lastCommitted = null;
      state.bulk.undoRequested = false;
      ui.seatId = null;
      getElement('bulk-dialog')?.close();
      if (!preserveAreaDetail) closeDetailPanel({ render: false, preserveAreaFocus: !closeAreaFocus });
      renderBulkBar();
      render();
    }

    function clearBulkSelection() {
      clearWorkspaceSelection();
    }

    function updateMultiSelection(workspaceId, additive = false) {
      markBulkSelectionChanged();
      if (!additive) state.selectedWorkspaces.clear();
      if (additive && state.selectedWorkspaces.has(workspaceId)) state.selectedWorkspaces.delete(workspaceId);
      else state.selectedWorkspaces.add(workspaceId);
      ui.seatId = state.selectedWorkspaces.has(workspaceId) ? workspaceId : [...state.selectedWorkspaces].at(-1) || null;
      if (!state.selectedWorkspaces.size) {
        ui.assignmentBaseline = null;
        closeDetailPanel({ render: false });
      }
      renderBulkBar();
    }

    function deselectSelectedWorkspace(workspaceId) {
      markBulkSelectionChanged();
      const remaining = deselectWorkspace([...state.selectedWorkspaces], workspaceId);
      state.selectedWorkspaces = new Set(remaining);
      ui.seatId = remaining.at(-1) || null;
      if (!remaining.length) {
        clearBulkSelection();
        render();
        return;
      }
      renderBulkBar();
      render();
    }

    return Object.freeze({ clearBulkSelection, clearWorkspaceSelection, deselectSelectedWorkspace, markBulkSelectionChanged, setMode, updateMultiSelection });
  }

  const api = Object.freeze({ createSelectionControllerFeature });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.SelectionControllerFeature = api;
})();
