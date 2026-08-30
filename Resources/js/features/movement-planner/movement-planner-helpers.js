(() => {
  'use strict';

  const states = new Set(['idle', 'selectingSources', 'selectingDestinations', 'planning', 'review', 'creatingScenario', 'error']);
  const locationOf = (id, locations = {}) => locations[id]?.displayLocation || locations[id]?.location || id || '';
  const compareWorkspaceIds = (left, right, locations = {}) =>
    locationOf(left, locations).localeCompare(locationOf(right, locations), 'es', { numeric: true }) || String(left).localeCompare(String(right));
  const sortedIds = (ids = [], locations = {}) => [...new Set(ids.filter(Boolean))].sort((left, right) => compareWorkspaceIds(left, right, locations));

  function createPlannerState() {
    return {
      status: 'idle', step: 'idle', sourceIds: [], destinationIds: [], requestPairs: [],
      plan: null, selectedProposalId: null, destinationMode: false, overrideSourceId: null,
      excludedSourceIds: [], sourceIssues: [], error: null
    };
  }

  function classifySources(selectedIds = [], assignmentsByWorkspace = {}, locations = {}) {
    const movable = [];
    const unavailable = [];
    sortedIds(selectedIds, locations).forEach(workspaceId => {
      if (assignmentsByWorkspace[workspaceId]?.workstationId === workspaceId) movable.push(workspaceId);
      else unavailable.push({ workspaceId, code: 'source-unassigned', message: 'El puesto no tiene una asignación para mover.' });
    });
    return { movable, unavailable };
  }

  function classifyEffectiveSources(selectedIds = [], workspaces = {}, locations = {}) {
    const movable = [];
    const unavailable = [];
    sortedIds(selectedIds, locations).forEach(workspaceId => {
      const workspace = workspaces[workspaceId] || {};
      if (workspace.effectiveState === 'free') unavailable.push({ workspaceId, code: 'source-free', message: 'Puesto libre.' });
      else if (workspace.effectiveState === 'reserved') unavailable.push({ workspaceId, code: 'source-reserved', message: 'Puesto reservado.' });
      else if (workspace.assignment?.workstationId === workspaceId && workspace.assignment?.personId) movable.push(workspaceId);
      else if (workspace.legacyPersonId) {
        if (!workspace.legacyPersonResolved) unavailable.push({ workspaceId, code: 'source-person-unresolved', message: 'No se puede determinar el ocupante.' });
        else if (!workspace.legacyDeviceResolved) unavailable.push({ workspaceId, code: 'source-device-unresolved', message: 'No se puede determinar el equipo.' });
        else movable.push(workspaceId);
      } else unavailable.push({ workspaceId, code: 'source-insufficient-data', message: 'Datos insuficientes para mover.' });
    });
    return { movable, unavailable };
  }

  function buildPairs(sourceIds = [], destinationIds = [], excludedSourceIds = [], locations = {}) {
    const excluded = new Set(excludedSourceIds);
    const sources = sortedIds(sourceIds.filter(id => !excluded.has(id)), locations);
    const destinations = sortedIds(destinationIds, locations);
    const paired = sources.slice(0, destinations.length).map((sourceWorkspaceId, index) => ({ sourceWorkspaceId, destinationWorkspaceId: destinations[index] }));
    return { pairs: paired, unassigned: sources.slice(destinations.length), excluded: sortedIds([...excluded], locations) };
  }

  function overridePair(pairs = [], sourceWorkspaceId, destinationWorkspaceId) {
    return pairs.map(pair => pair.sourceWorkspaceId === sourceWorkspaceId ? { sourceWorkspaceId, destinationWorkspaceId } : { ...pair });
  }

  function serializeCreationRequest(name, pairs = []) {
    return { name: String(name || '').trim(), requests: pairs.map(pair => ({ sourceWorkspaceId: pair.sourceWorkspaceId, destinationWorkspaceId: pair.destinationWorkspaceId })) };
  }

  function reviewSummary(plan, unassigned = [], excluded = []) {
    const proposals = Array.isArray(plan?.proposals) ? plan.proposals : [];
    const issues = Array.isArray(plan?.issues) ? plan.issues : [];
    const problems = proposals.flatMap(proposal => proposal.relatedProblems || []);
    return {
      planned: proposals.length,
      blocked: issues.length,
      unassigned: unassigned.length,
      excluded: excluded.length,
      critical: problems.filter(problem => problem.severity === 'Critical').length,
      warning: problems.filter(problem => problem.severity === 'Warning').length,
      info: problems.filter(problem => problem.severity === 'Info').length
    };
  }

  const api = { states, createPlannerState, compareWorkspaceIds, sortedIds, classifySources, classifyEffectiveSources, buildPairs, overridePair, serializeCreationRequest, reviewSummary };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else window.MovementPlannerHelpers = api;
})();
