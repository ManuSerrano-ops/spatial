(() => {
  'use strict';

  const text = value => String(value ?? '').trim();

  function deriveWorkspaceQuality(input = {}) {
    const effectiveState = String(input.effectiveState || 'free').toLowerCase();
    const assignment = input.assignment || {};
    const currentPerson = text(input.currentPerson ?? assignment.personId ?? input.seat?.personId);
    if (effectiveState === 'free') return Object.freeze({ qualityState: 'valid', missingFields: [], reason: 'free-workspace' });
    if (effectiveState === 'reserved') return Object.freeze({ qualityState: 'valid', missingFields: [], reason: 'reserved-workspace' });
    const missingFields = [
      ['person', currentPerson],
      ['device', assignment.deviceId ?? input.seat?.deviceId ?? input.seat?.deviceName],
      ['location', assignment.locationId ?? input.seat?.location],
      ['network', assignment.roseta ?? input.seat?.roseta]
    ].filter(([, value]) => !text(value)).map(([field]) => field);
    return Object.freeze({ qualityState: missingFields.length ? 'incomplete' : 'valid', missingFields, reason: missingFields.length ? 'occupied-missing-required-fields' : 'occupied-required-fields-complete' });
  }

  const api = { deriveWorkspaceQuality };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.WorkspaceQualityHelpers = api;
})();
