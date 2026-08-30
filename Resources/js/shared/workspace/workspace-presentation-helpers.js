(() => {
  'use strict';

  const workspaceStateHelpers = typeof module !== 'undefined' && module.exports ? require('./workspace-state-helpers.js') : window.WorkspaceStateHelpers;
  const text = value => String(value ?? '').trim();
  const stateLabel = state => ({ occupied: 'Ocupado', reserved: 'Reservado', free: 'Libre' })[String(state || '').toLowerCase()] || 'Libre';
  const problemLabel = severity => ({ critical: 'crítico', warning: 'advertencia', info: 'información' })[String(severity || '').toLowerCase()] || '';

  function buildWorkspacePresentation(input = {}) {
    const seat = input.seat || {};
    const assignment = input.assignment || {};
    const displayLocation = text(input.displayLocation) || 'Ubicación no indicada';
    const workstationReference = text(seat.reference) || text(seat.code) || text(seat.workstation) || text(seat.name);
    const effectiveState = input.effectiveState || workspaceStateHelpers.deriveEffectiveWorkspaceState({ seat, assignment });
    const currentPersonId = effectiveState.currentPersonId ?? assignment.personId ?? seat.personId ?? null;
    const currentPerson = text(input.personName) || text(currentPersonId);
    const assignmentStatus = String(effectiveState.state).toLowerCase();
    const equipment = text(input.equipmentName) || text(assignment.deviceId ?? seat.deviceId ?? seat.deviceName);
    const networkOutlet = text(assignment.roseta ?? seat.roseta);
    const problemSeverity = String(input.problemSeverity || 'none').toLowerCase();
    const problemCount = Math.max(0, Number(input.problemCount) || 0);
    const ariaParts = [`Puesto ${displayLocation}`, stateLabel(assignmentStatus)];
    if (currentPerson) ariaParts.push(currentPerson);
    if (workstationReference) ariaParts.push(`referencia ${workstationReference}`);
    if (problemSeverity !== 'none') ariaParts.push(`${problemCount || 1} problema${problemCount === 1 ? '' : 's'} ${problemLabel(problemSeverity)}`);
    return Object.freeze({
      displayLocation,
      workstationReference,
      currentPersonId: currentPersonId || null,
      currentPerson: currentPerson || null,
      assignmentStatus,
      assignmentStatusLabel: stateLabel(assignmentStatus),
      stateMode: effectiveState.mode,
      stateModeLabel: effectiveState.modeLabel,
      equipment: equipment || null,
      networkOutlet: networkOutlet || null,
      problemSummary: Object.freeze({ count: problemCount, severity: problemSeverity }),
      ariaLabel: ariaParts.join(', '),
      title: [displayLocation, workstationReference && `Ref. ${workstationReference}`, currentPerson || 'Sin asignar'].filter(Boolean).join(' · ')
    });
  }

  const api = { buildWorkspacePresentation, stateLabel, problemLabel };
  if (typeof module !== 'undefined') module.exports = api;
  if (typeof window !== 'undefined') window.WorkspacePresentationHelpers = api;
})();
