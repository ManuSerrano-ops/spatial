(() => {
  'use strict';

  const text = value => String(value ?? '').trim();
  const normalized = value => text(value).toLowerCase();
  const stateLabels = Object.freeze({ free: 'Libre', occupied: 'Ocupado', reserved: 'Reservado' });
  const modeLabels = Object.freeze({ automatic: 'Automático', manual: 'Manual' });

  function deriveEffectiveWorkspaceState(input = {}) {
    const seat = input.seat || {};
    const assignment = input.assignment || {};
    const configured = normalized(input.configuredState ?? assignment.configuredState ?? assignment.status);
    const currentPersonId = text(assignment.personId ?? seat.personId);
    let state = 'free';
    let mode = 'automatic';

    if (configured === 'reserved' || configured === 'manual-reserved') {
      state = 'reserved';
      mode = 'manual';
    } else if (configured === 'free' || configured === 'manual-free') {
      state = 'free';
      mode = 'manual';
    } else if (configured === 'occupied' || configured === 'manual-occupied') {
      state = 'occupied';
      mode = 'manual';
    } else if (currentPersonId) {
      state = 'occupied';
    }

    return Object.freeze({
      state,
      mode,
      configuredState: mode === 'manual' ? configured : 'automatic',
      currentPersonId: currentPersonId || null,
      hasCurrentAssignment: Boolean(currentPersonId) || configured === 'reserved' || configured === 'manual-reserved',
      stateLabel: stateLabels[state],
      modeLabel: modeLabels[mode]
    });
  }

  const api = { deriveEffectiveWorkspaceState, stateLabels, modeLabels };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.WorkspaceStateHelpers = api;
})();
