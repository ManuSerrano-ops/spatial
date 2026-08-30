(() => {
  'use strict';

  const array = value => Array.isArray(value) ? value : [];
  const text = (value, fallback) => String(value ?? '').trim() || fallback;

  function selectionReviewMode(selectedIds = []) {
    const count = array(selectedIds).length;
    return count > 1 ? 'selection' : count === 1 ? 'inspector' : 'empty';
  }

  function buildSelectionReviewItems(selectedIds = [], workspacesById = {}, options = {}) {
    const bulkByWorkspace = options.bulkByWorkspace || {};
    const plannerByWorkspace = options.plannerByWorkspace || {};
    return Object.freeze(array(selectedIds).map(workspaceId => {
      const workspace = workspacesById[workspaceId] || {};
      const bulk = bulkByWorkspace[workspaceId] || null;
      const planner = plannerByWorkspace[workspaceId] || null;
      return Object.freeze({
        workspaceId,
        mapId: workspace.mapId || null,
        displayLocation: text(workspace.displayLocation, 'Ubicación no indicada'),
        person: text(workspace.person, 'Sin asignar'),
        effectiveState: text(workspace.effectiveStateLabel, 'Estado no disponible'),
        device: text(workspace.device, 'Sin equipo'),
        roseta: text(workspace.roseta, 'Sin roseta'),
        reference: text(workspace.reference, 'Sin referencia'),
        location: text(workspace.location, 'Sin zona'),
        bulk: bulk ? Object.freeze({ eligible: Boolean(bulk.eligible), outcome: bulk.outcome, reason: bulk.reason || '' }) : null,
        planner: planner ? Object.freeze({ movable: Boolean(planner.movable), reason: planner.reason || '' }) : null
      });
    }));
  }

  function deriveSelectionReviewSummary(items = [], bulkSummary = null) {
    const values = array(items);
    const movable = values.filter(item => item.planner?.movable).length;
    const plannerKnown = values.filter(item => item.planner).length;
    return Object.freeze({
      count: values.length,
      mode: selectionReviewMode(values.map(item => item.workspaceId)),
      movable: plannerKnown ? movable : null,
      nonMovable: plannerKnown ? plannerKnown - movable : null,
      bulk: bulkSummary ? Object.freeze({ eligibleCount: bulkSummary.eligibleCount || 0, excludedCount: bulkSummary.excludedCount || 0, detail: bulkSummary.detail || '' }) : null
    });
  }

  function deselectWorkspace(selectedIds = [], workspaceId) {
    return Object.freeze(array(selectedIds).filter(id => id !== workspaceId));
  }

  function clearSelection() { return Object.freeze([]); }

  const api = { selectionReviewMode, buildSelectionReviewItems, deriveSelectionReviewSummary, deselectWorkspace, clearSelection };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.SelectionReviewHelpers = api;
})();
