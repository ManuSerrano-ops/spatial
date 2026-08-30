(() => {
  'use strict';

  const validationHelpers = typeof module !== 'undefined' && module.exports ? require('../../shared/validation/validation-helpers.js') : window.ValidationHelpers;
  const severityOrder = Object.freeze({ Critical: 0, Warning: 1, Info: 2, None: 3 });
  const severityDefinitions = Object.freeze({
    Critical: Object.freeze({ label: 'Críticos', singular: 'crítico' }),
    Warning: Object.freeze({ label: 'Advertencias', singular: 'advertencia' }),
    Info: Object.freeze({ label: 'Información', singular: 'información' })
  });

  const read = (source, name) => source?.[name] ?? source?.[`${name[0].toUpperCase()}${name.slice(1)}`];
  const array = value => Array.isArray(value) ? value : [];
  const text = value => value === null || value === undefined ? '' : String(value).trim();
  const number = value => {
    const parsed = typeof value === 'number' ? value : typeof value === 'string' && value.trim() ? Number(value) : NaN;
    return Number.isFinite(parsed) ? parsed : 0;
  };
  const count = value => Math.max(0, Math.round(number(value)));
  const rate = value => Math.min(100, Math.max(0, number(value)));
  const severityOf = value => {
    const normalized = text(value).toLowerCase();
    return normalized === 'critical' ? 'Critical' : normalized === 'warning' ? 'Warning' : normalized === 'info' ? 'Info' : 'None';
  };
  const compareText = (left, right) => String(left).localeCompare(String(right), 'en', { numeric: true, sensitivity: 'base' });

  function formatNumber(value) {
    const normalized = Math.round(number(value) * 100) / 100;
    const sign = normalized < 0 ? '-' : '';
    const [whole, fraction] = String(Math.abs(normalized)).split('.');
    const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
    return `${sign}${grouped}${fraction ? `,${fraction}` : ''}`;
  }

  function formatPercent(value) {
    return `${formatNumber(rate(value))} %`;
  }

  function listTarget(quick, label) {
    return { kind: 'list', label, filters: { quick } };
  }

  function problemsTarget(severity = '', mapId = '', label = 'Ver problemas') {
    return { kind: 'problems', label, filters: { severity, mapId } };
  }

  function mapTarget(mapId, label) {
    return { kind: 'map', label: label || `Ver plano ${mapId}`, mapId };
  }

  function resultsSummary(results) {
    return validationHelpers.operationalResults(array(results)).reduce((summary, result) => {
      summary.total += 1;
      const severity = severityOf(read(result, 'severity'));
      if (severity === 'Critical') summary.critical += 1;
      else if (severity === 'Warning') summary.warning += 1;
      else if (severity === 'Info') summary.info += 1;
      return summary;
    }, { total: 0, critical: 0, warning: 0, info: 0 });
  }

  function normalizeSummary(summary, fallback = {}) {
    const source = summary || {};
    const critical = count(read(source, 'critical') ?? read(fallback, 'critical'));
    const warning = count(read(source, 'warning') ?? read(fallback, 'warning'));
    const info = count(read(source, 'info') ?? read(fallback, 'info'));
    const explicitTotal = read(source, 'total') ?? read(fallback, 'total');
    return { total: explicitTotal === undefined ? critical + warning + info : count(explicitTotal), critical, warning, info };
  }

  function normalizeMap(metric, results) {
    const mapId = text(read(metric, 'mapId') ?? read(metric, 'id'));
    const mapName = text(read(metric, 'mapName') ?? read(metric, 'name')) || mapId;
    const seats = read(metric, 'seats') || {};
    const validation = read(metric, 'validation');
    const fallback = resultsSummary(array(results).filter(result => text(read(result, 'mapId')) === mapId));
    const problems = normalizeSummary(validation, fallback);
    return {
      mapId,
      mapName,
      total: count(read(seats, 'total')),
      occupied: count(read(seats, 'occupied')),
      free: count(read(seats, 'free')),
      reserved: count(read(seats, 'reserved')),
      occupancyRate: rate(read(seats, 'occupancyRate')),
      availabilityRate: rate(read(seats, 'availabilityRate')),
      occupancyLabel: formatPercent(read(seats, 'occupancyRate')),
      availabilityLabel: formatPercent(read(seats, 'availabilityRate')),
      problems,
      target: mapTarget(mapId, `Ver plano ${mapName}`),
      problemsTarget: problemsTarget('', mapId, `Ver problemas de ${mapName}`)
    };
  }

  function normalizeProblem(result) {
    const severity = severityOf(read(result, 'severity'));
    const mapId = text(read(result, 'mapId'));
    const id = text(read(result, 'id'));
    const title = text(read(result, 'title')) || text(read(result, 'message')) || id || 'Problema sin título';
    return {
      id,
      severity,
      ruleId: text(read(result, 'ruleId')),
      mapId,
      entityId: text(read(result, 'entityId')),
      title,
      message: text(read(result, 'message')),
      target: problemsTarget(severity === 'None' ? '' : severity, mapId, `Ver ${title}`)
    };
  }

  function normalizeImpact(source) {
    const summary = read(source, 'impactSummary') || {};
    const validation = read(source, 'validationImpact') || {};
    const byMap = read(summary, 'byMap') || {};
    const mapChanges = Object.keys(byMap)
      .map(mapId => ({ mapId, changes: count(byMap[mapId]), target: mapTarget(mapId) }))
      .sort((left, right) => compareText(left.mapId, right.mapId));
    return {
      available: Boolean(source && (Object.keys(summary).length || Object.keys(validation).length)),
      total: count(read(summary, 'total')),
      added: count(read(summary, 'added')),
      removed: count(read(summary, 'removed')),
      moved: count(read(summary, 'moved')),
      modified: count(read(summary, 'modified')),
      assignments: count(read(summary, 'assignments')),
      workspaces: count(read(summary, 'workspaces')),
      changedFields: count(read(summary, 'changedFields')),
      validation: {
        introduced: array(read(validation, 'introduced')).length,
        resolved: array(read(validation, 'resolved')).length,
        persistent: array(read(validation, 'persistent')).length
      },
      mapChanges
    };
  }

  function attention(summary) {
    const severity = summary.critical ? 'Critical' : summary.warning ? 'Warning' : summary.info ? 'Info' : 'None';
    const amount = severity === 'Critical' ? summary.critical : severity === 'Warning' ? summary.warning : severity === 'Info' ? summary.info : 0;
    if (severity === 'None') return { state: 'clear', severity, count: 0, label: 'No hay problemas que requieran atención.', target: null };
    const definition = severityDefinitions[severity];
    const noun = amount === 1 ? definition.singular : definition.label.toLowerCase();
    return {
      state: severity.toLowerCase(),
      severity,
      count: amount,
      label: `${amount} ${noun} requiere${amount === 1 ? '' : 'n'} atención.`,
      target: problemsTarget(severity, '', `Ver ${definition.label.toLowerCase()}`)
    };
  }

  function deepFreeze(value) {
    if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value;
    Object.keys(value).forEach(key => deepFreeze(value[key]));
    return Object.freeze(value);
  }

  /**
   * Builds a presentation-only dashboard model from reports already derived by the backend.
   * Occupancy and availability rates are copied from analytics; they are never recalculated.
   */
  function buildDashboardModel({ analytics, validation, scenario, scenarioDiff } = {}) {
    const analyticsReport = read(analytics, 'result') || analytics || {};
    const totals = read(analyticsReport, 'totals') || analyticsReport;
    const validationResults = validationHelpers.operationalResults(array(read(validation, 'results')));
    const resultFallback = resultsSummary(validationResults);
    const validationSummary = normalizeSummary(read(validation, 'summary') || validation, read(analyticsReport, 'validation') || resultFallback);
    const scenarioId = text(read(scenario, 'id') ?? read(scenario, 'scenarioId'));
    const scenarioName = text(read(scenario, 'name')) || scenarioId;
    const isScenario = Boolean(scenarioId);
    const maps = array(read(analyticsReport, 'maps'))
      .map(metric => normalizeMap(metric, validationResults))
      .sort((left, right) => compareText(left.mapName, right.mapName) || compareText(left.mapId, right.mapId));
    const availabilityRanking = maps
      .map(map => ({ mapId: map.mapId, mapName: map.mapName, availabilityRate: map.availabilityRate, availabilityLabel: map.availabilityLabel, target: map.target }))
      .sort((left, right) => right.availabilityRate - left.availabilityRate || compareText(left.mapName, right.mapName) || compareText(left.mapId, right.mapId));
    const problemItems = validationResults
      .map(normalizeProblem)
      .sort((left, right) => severityOrder[left.severity] - severityOrder[right.severity] || compareText(left.mapId, right.mapId) || compareText(left.entityId, right.entityId) || compareText(left.ruleId, right.ruleId) || compareText(left.id, right.id));
    const freeTarget = listTarget('free', 'Ver puestos libres');
    const reservedTarget = listTarget('reserved', 'Ver puestos reservados');
    const impact = normalizeImpact(scenarioDiff);
    const model = {
      context: {
        mode: isScenario ? 'scenario' : 'reality',
        label: isScenario ? `ESCENARIO · ${scenarioName}` : 'REALIDAD',
        scenarioId: isScenario ? scenarioId : null,
        scenarioName: isScenario ? scenarioName : null
      },
      kpiCards: [
        { id: 'total', label: 'Puestos totales', value: count(read(totals, 'total')), displayValue: formatNumber(read(totals, 'total')), target: null },
        { id: 'occupied', label: 'Ocupados', value: count(read(totals, 'occupied')), displayValue: formatNumber(read(totals, 'occupied')), target: listTarget('occupied', 'Ver puestos ocupados') },
        { id: 'free', label: 'Libres', value: count(read(totals, 'free')), displayValue: formatNumber(read(totals, 'free')), target: freeTarget },
        { id: 'reserved', label: 'Reservados', value: count(read(totals, 'reserved')), displayValue: formatNumber(read(totals, 'reserved')), target: reservedTarget },
        { id: 'occupancy', label: 'Ocupación', value: rate(read(totals, 'occupancyRate')), displayValue: formatPercent(read(totals, 'occupancyRate')), target: null },
        { id: 'availability', label: 'Disponibilidad', value: rate(read(totals, 'availabilityRate')), displayValue: formatPercent(read(totals, 'availabilityRate')), target: freeTarget },
        { id: 'problems', label: 'Problemas', value: validationSummary.total, displayValue: formatNumber(validationSummary.total), target: problemsTarget('', '', 'Ver problemas') }
      ],
      problems: {
        ...validationSummary,
        items: problemItems,
        bySeverity: ['Critical', 'Warning', 'Info'].map(severity => ({
          severity,
          label: severityDefinitions[severity].label,
          count: validationSummary[severity.toLowerCase()],
          target: problemsTarget(severity, '', `Ver ${severityDefinitions[severity].label.toLowerCase()}`)
        })),
        target: problemsTarget('', '', 'Ver problemas')
      },
      maps,
      availabilityRanking,
      scenarioImpact: impact,
      navigation: {
        free: freeTarget,
        reserved: reservedTarget,
        problems: {
          all: problemsTarget('', '', 'Ver problemas'),
          critical: problemsTarget('Critical', '', 'Ver críticos'),
          warning: problemsTarget('Warning', '', 'Ver advertencias'),
          info: problemsTarget('Info', '', 'Ver información')
        },
        maps: maps.map(map => ({ mapId: map.mapId, target: map.target, problemsTarget: map.problemsTarget }))
      },
      attention: attention(validationSummary),
      emptyStates: {
        maps: maps.length ? '' : 'No hay planos con analítica disponible.',
        availability: availabilityRanking.length ? '' : 'No hay planos para clasificar por disponibilidad.',
        problems: validationSummary.total ? '' : 'No hay problemas de validación.',
        scenario: isScenario ? (impact.total ? '' : 'Este escenario no tiene cambios pendientes.') : 'Selecciona un escenario para ver su impacto.',
        attention: validationSummary.total ? '' : 'No hay acciones pendientes.'
      }
    };
    return deepFreeze(model);
  }

  const api = { buildDashboardModel, formatNumber, formatPercent };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else if (typeof window !== 'undefined') window.DashboardHelpers = api;
})();
