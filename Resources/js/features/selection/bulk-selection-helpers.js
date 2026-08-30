(() => {
  'use strict';

  const array = value => Array.isArray(value) ? value : [];
  const text = value => String(value ?? '').trim();
  const freezeArray = values => Object.freeze([...values]);
  const actionDefinitions = Object.freeze({
    reserved: Object.freeze({ id: 'reserved', label: 'Reservar', verb: 'reservar', completed: 'reservados', status: 'reserved' }),
    confirmed: Object.freeze({ id: 'confirmed', label: 'Quitar reserva', verb: 'quitar la reserva de', completed: 'con la reserva retirada', status: 'confirmed' })
  });

  function eligibilityFor(workspace, actionId) {
    const workspaceId = text(workspace?.workspaceId);
    const effectiveState = text(workspace?.effectiveState).toLowerCase();
    if (!workspaceId) return Object.freeze({ workspaceId: '', eligible: false, outcome: 'blocked', reason: 'Puesto no identificable.' });
    if (actionId === 'reserved') {
      if (effectiveState === 'free') return Object.freeze({ workspaceId, eligible: true, outcome: 'eligible', reason: '' });
      if (effectiveState === 'reserved') return Object.freeze({ workspaceId, eligible: false, outcome: 'noop', reason: 'Ya reservado.' });
      return Object.freeze({ workspaceId, eligible: false, outcome: 'blocked', reason: 'Puesto ocupado.' });
    }
    if (actionId === 'confirmed') {
      if (effectiveState === 'reserved') return Object.freeze({ workspaceId, eligible: true, outcome: 'eligible', reason: '' });
      return Object.freeze({ workspaceId, eligible: false, outcome: 'noop', reason: 'No está reservado.' });
    }
    return Object.freeze({ workspaceId, eligible: false, outcome: 'blocked', reason: 'Selecciona una acción válida.' });
  }

  function deriveBulkActionEligibility(workspaces = [], actionId = '') {
    const targets = array(workspaces).map(workspace => eligibilityFor(workspace, actionId));
    const eligible = targets.filter(target => target.eligible);
    const excluded = targets.filter(target => !target.eligible);
    const reasons = [];
    excluded.forEach(target => {
      let group = reasons.find(item => item.reason === target.reason && item.outcome === target.outcome);
      if (!group) { group = { reason: target.reason, outcome: target.outcome, count: 0, workspaceIds: [] }; reasons.push(group); }
      group.count += 1;
      group.workspaceIds.push(target.workspaceId);
    });
    return Object.freeze({
      action: actionDefinitions[actionId] || null,
      selectedCount: targets.length,
      eligible: freezeArray(eligible),
      excluded: freezeArray(excluded),
      eligibleCount: eligible.length,
      excludedCount: excluded.length,
      reasons: freezeArray(reasons.map(reason => Object.freeze({ ...reason, workspaceIds: freezeArray(reason.workspaceIds) })))
    });
  }

  function buildBulkActionSummary(eligibility) {
    const selectedCount = Number(eligibility?.selectedCount) || 0;
    const eligibleCount = Number(eligibility?.eligibleCount) || 0;
    const excludedCount = Number(eligibility?.excludedCount) || 0;
    const action = eligibility?.action;
    const applyLabel = action && eligibleCount ? `Aplicar a ${eligibleCount}` : 'Aplicar';
    const ariaLabel = action && eligibleCount ? `Aplicar ${action.label.toLowerCase()} a ${eligibleCount} puestos` : 'Aplicar acción masiva';
    const detail = !action
      ? 'Selecciona una acción.'
      : excludedCount
        ? `${action.label}: ${eligibleCount} aplicables · ${excludedCount} no aplicables.`
        : `${action.label}: ${eligibleCount} aplicables.`;
    return Object.freeze({ selectedCount, eligibleCount, excludedCount, applyLabel, ariaLabel, detail, canApply: Boolean(action && eligibleCount) });
  }

  function buildBulkSelectionCommand(eligibility) {
    if (!eligibility?.action) return null;
    const workstationIds = array(eligibility.eligible).map(target => target.workspaceId).filter(Boolean);
    if (!workstationIds.length) return null;
    return Object.freeze({ workstationIds: freezeArray(workstationIds), status: eligibility.action.status });
  }

  const api = { actionDefinitions, deriveBulkActionEligibility, buildBulkActionSummary, buildBulkSelectionCommand };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.BulkSelectionHelpers = api;
})();
