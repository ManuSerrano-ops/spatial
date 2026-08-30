(() => {
  'use strict';

  const problemRank = { none: 0, info: 1, warning: 2, critical: 3 };
  const scenarioKinds = new Set(['added', 'removed', 'moved', 'modified']);
  const plannerStates = new Set(['none', 'source', 'destination', 'blocked']);

  const normalizeBusinessState = value => ['free', 'occupied', 'reserved'].includes(String(value || '').toLowerCase())
    ? String(value).toLowerCase()
    : 'free';
  const normalizeQualityState = value => ['partial', 'incomplete'].includes(String(value || '').toLowerCase()) ? 'incomplete' : 'complete';
  const normalizeProblemSeverity = value => problemRank[String(value || '').toLowerCase()] !== undefined
    ? String(value || '').toLowerCase()
    : 'none';
  const normalizeScenarioState = value => scenarioKinds.has(String(value || '').toLowerCase()) ? String(value).toLowerCase() : 'none';
  const normalizePlannerState = value => plannerStates.has(String(value || '').toLowerCase()) ? String(value).toLowerCase() : 'none';

  const businessLabel = state => ({ free: 'libre', occupied: 'ocupado', reserved: 'reservado' })[state];
  const problemLabel = state => ({ critical: 'problema crítico', warning: 'advertencia', info: 'información' })[state];
  const scenarioSymbol = state => ({ added: '+', removed: '−', moved: '→', modified: '~' })[state] || '';
  const plannerSymbol = state => ({ source: '●', destination: '◎', blocked: '×' })[state] || '';

  function derivePinPresentation(input = {}) {
    const businessState = normalizeBusinessState(input.businessState);
    const qualityState = normalizeQualityState(input.qualityState);
    const problemSeverity = normalizeProblemSeverity(input.problemSeverity);
    const scenarioState = input.isScenario ? normalizeScenarioState(input.scenarioState) : 'none';
    const plannerState = normalizePlannerState(input.plannerState);
    const interaction = {
      selected: Boolean(input.selected),
      multiSelected: Boolean(input.multiSelected),
      searchHit: Boolean(input.searchHit),
      dimmed: Boolean(input.dimmed)
    };
    const problemCount = Math.max(0, Number(input.problemCount) || 0);
    const displayLocation = String(input.displayLocation || 'ubicación no indicada');
    const personName = String(input.personName || '').trim();
    const aria = [`Puesto ${displayLocation}`, businessLabel(businessState)];
    if (personName) aria.push(personName);
    if (qualityState === 'incomplete') aria.push('datos incompletos');
    if (problemSeverity !== 'none') aria.push(`${problemCount || 1} ${problemLabel(problemSeverity)}`);
    if (scenarioState !== 'none') aria.push(`escenario: ${scenarioState}`);
    if (plannerState !== 'none') aria.push(`planificador: ${plannerState}`);

    return Object.freeze({
      businessState,
      qualityState,
      problemSeverity,
      scenarioState,
      plannerState,
      interaction: Object.freeze(interaction),
      problemSymbol: problemSeverity === 'critical' ? '×' : problemSeverity === 'warning' ? '!' : problemSeverity === 'info' ? 'i' : '',
      scenarioSymbol: scenarioSymbol(scenarioState),
      plannerSymbol: plannerSymbol(plannerState),
      ariaLabel: aria.join(', '),
      title: `${displayLocation} · ${businessLabel(businessState)}${personName ? ` · ${personName}` : ''}${problemSeverity !== 'none' ? ` · ${problemCount || 1} ${problemLabel(problemSeverity)}` : ''}`,
      zIndex: plannerState === 'blocked' ? 60 : plannerState !== 'none' ? 50 : interaction.selected ? 40 : interaction.searchHit ? 35 : problemSeverity !== 'none' ? 30 : interaction.multiSelected ? 25 : 10
    });
  }

  const api = { derivePinPresentation, problemRank };
  if (typeof module !== 'undefined') module.exports = api;
  if (typeof window !== 'undefined') window.PinStateHelpers = api;
})();
