(() => {
  'use strict';

  const metricModes = Object.freeze(['occupancy', 'availability', 'problems', 'scenarioChanges']);
  const modeAliases = Object.freeze({
    occupancy: 'occupancy',
    availability: 'availability',
    problems: 'problems',
    scenariochanges: 'scenarioChanges',
    'scenario-changes': 'scenarioChanges'
  });
  const layerByMode = Object.freeze({
    occupancy: 'occupancy',
    availability: 'availability',
    problems: 'problems',
    scenarioChanges: 'scenario-changes'
  });
  const legendDefinitions = Object.freeze({
    occupancy: Object.freeze({ label: 'Ocupación', description: 'Porcentaje de puestos ocupados.', unit: '%' }),
    availability: Object.freeze({ label: 'Disponibilidad', description: 'Porcentaje de puestos libres.', unit: '%' }),
    problems: Object.freeze({ label: 'Problemas', description: 'Problemas de integridad asociados al plano.', unit: 'problemas' }),
    scenarioChanges: Object.freeze({ label: 'Cambios del escenario', description: 'Cambios de escenario asociados al plano.', unit: 'cambios' })
  });

  const read = (source, name) => source?.[name] ?? source?.[`${name[0].toUpperCase()}${name.slice(1)}`];
  const array = value => Array.isArray(value) ? value : [];
  const number = value => {
    const parsed = typeof value === 'number' ? value : typeof value === 'string' && value.trim() ? Number(value) : NaN;
    return Number.isFinite(parsed) ? parsed : 0;
  };
  const nonNegative = value => Math.max(0, number(value));
  const count = value => Math.round(nonNegative(value));
  const rate = value => Math.min(100, nonNegative(value));

  function selectMetricMode(mode, fallback = 'occupancy') {
    const normalized = String(mode ?? '').trim().toLowerCase();
    if (modeAliases[normalized]) return modeAliases[normalized];
    const fallbackMode = modeAliases[String(fallback ?? '').trim().toLowerCase()];
    return fallbackMode || 'occupancy';
  }

  function getLegendMetadata(mode, scale = {}) {
    const selected = selectMetricMode(mode);
    const definition = legendDefinitions[selected];
    const minimum = nonNegative(read(scale, 'min'));
    const maximum = Math.max(minimum, nonNegative(read(scale, 'max')));
    const minLabel = `${minimum}${definition.unit === '%' ? '%' : ` ${definition.unit}`}`;
    const maxLabel = `${maximum}${definition.unit === '%' ? '%' : ` ${definition.unit}`}`;
    return {
      id: `spatial-legend-${selected}`,
      mode: selected,
      label: definition.label,
      description: definition.description,
      unit: definition.unit,
      min: minimum,
      max: maximum,
      minLabel,
      maxLabel,
      ariaLabel: `${definition.label}. ${definition.description} Escala de ${minLabel} a ${maxLabel}.`
    };
  }

  function findMapMetrics(analytics = {}, mapId) {
    if (mapId === null || mapId === undefined) return null;
    return array(read(analytics, 'maps')).find(map => String(read(map, 'mapId') ?? read(map, 'id') ?? '') === String(mapId)) || null;
  }

  function validationTotal(validation = {}) {
    const explicit = read(validation, 'total');
    return explicit === undefined
      ? count(read(validation, 'critical')) + count(read(validation, 'warning')) + count(read(validation, 'info'))
      : count(explicit);
  }

  function heatmapValues(analytics = {}, mapId, layer) {
    return array(read(analytics, 'heatmapPoints'))
      .filter(point => String(read(point, 'mapId') ?? '') === String(mapId) && String(read(point, 'layer') ?? '') === layer)
      .map(point => nonNegative(read(point, 'value')));
  }

  function getMapMetric(analytics = {}, mapId, mode = 'occupancy') {
    const selected = selectMetricMode(mode);
    const map = findMapMetrics(analytics, mapId);
    if (selected === 'scenarioChanges') {
      const direct = map && (read(map, 'scenarioChanges') ?? read(map, 'changes'));
      if (direct !== undefined) return count(direct);
      return heatmapValues(analytics, mapId, layerByMode[selected]).reduce((total, value) => total + value, 0);
    }
    if (!map) return 0;
    if (selected === 'occupancy') return rate(read(read(map, 'seats') || {}, 'occupancyRate'));
    if (selected === 'availability') return rate(read(read(map, 'seats') || {}, 'availabilityRate'));
    return validationTotal(read(map, 'validation') || {});
  }

  function valuesFrom(source, valueOf) {
    return array(source).map(valueOf).map(nonNegative);
  }

  function calculateSharedScale(left = [], right = [], valueOf = value => typeof value === 'object' && value !== null ? read(value, 'value') : value) {
    const leftValues = valuesFrom(left, valueOf);
    const rightValues = valuesFrom(right, valueOf);
    const leftMax = leftValues.reduce((maximum, value) => Math.max(maximum, value), 0);
    const rightMax = rightValues.reduce((maximum, value) => Math.max(maximum, value), 0);
    const max = Math.max(leftMax, rightMax);
    return { min: 0, max, globalMax: max, leftMax, rightMax };
  }

  function emptyHeatmapLayerVisibility() {
    return metricModes.reduce((visibility, mode) => ({ ...visibility, [mode]: false }), {});
  }

  function createHeatmapLayerVisibility(activeMode = 'occupancy') {
    const selected = selectMetricMode(activeMode);
    return { ...emptyHeatmapLayerVisibility(), [selected]: true };
  }

  function setHeatmapLayerVisibility(visibility = {}, mode, visible = true) {
    const key = String(mode ?? '').trim().toLowerCase();
    if (!modeAliases[key]) return { ...emptyHeatmapLayerVisibility(), ...visibility };
    return { ...emptyHeatmapLayerVisibility(), ...visibility, [modeAliases[key]]: Boolean(visible) };
  }

  function isHeatmapLayerVisible(visibility = {}, mode) {
    return Boolean(visibility[selectMetricMode(mode)]);
  }

  function normalizeAnalyticsSummary(analytics = {}) {
    const totals = read(analytics, 'totals') || analytics;
    const validation = read(analytics, 'validation') || {};
    const scenario = read(analytics, 'scenario') || {};
    return {
      total: count(read(totals, 'total')),
      occupied: count(read(totals, 'occupied')),
      free: count(read(totals, 'free')),
      reserved: count(read(totals, 'reserved')),
      occupancyRate: rate(read(totals, 'occupancyRate')),
      availabilityRate: rate(read(totals, 'availabilityRate')),
      critical: count(read(validation, 'critical')),
      warning: count(read(validation, 'warning')),
      info: count(read(validation, 'info')),
      problems: validationTotal(validation),
      scenarioChanges: count(read(scenario, 'totalChanges')),
      mappedScenarioChanges: count(read(scenario, 'mappedChanges'))
    };
  }

  function percentagePointsDelta(current, baseline) {
    return Math.round((number(current) - number(baseline)) * 100) / 100;
  }

  const api = {
    metricModes,
    layerByMode,
    selectMetricMode,
    getLegendMetadata,
    findMapMetrics,
    getMapMetric,
    calculateSharedScale,
    createHeatmapLayerVisibility,
    setHeatmapLayerVisibility,
    isHeatmapLayerVisible,
    getHeatmapLayerVisibility: createHeatmapLayerVisibility,
    normalizeAnalyticsSummary,
    percentagePointsDelta,
    percentagePointDelta: percentagePointsDelta,
    getSharedScale: calculateSharedScale,
    normalizeMetricMode: selectMetricMode,
    getAccessibleLegendMetadata: getLegendMetadata,
    lookupMapMetric: getMapMetric
  };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else if (typeof window !== 'undefined') window.SpatialAnalyticsHelpers = api;
})();
