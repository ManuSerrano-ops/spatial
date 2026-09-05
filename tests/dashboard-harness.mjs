import test from 'node:test';
import assert from 'node:assert/strict';
import dashboardHelpers from '../Resources/js/features/dashboard/dashboard-helpers.js';

const { buildDashboardModel, formatNumber, formatPercent } = dashboardHelpers;
const equal = (actual, expected, message) => {
  if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}; expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`);
};
const card = (model, id) => model.kpiCards.find(item => item.id === id);

function fixture(overrides = {}) {
  const base = {
    analytics: {
      totals: { total: 10, occupied: 5, free: 3, reserved: 2, occupancyRate: 73.5, availabilityRate: 29.25 },
      maps: [
        { mapId: 'sur', mapName: 'Sur', seats: { total: 4, occupied: 3, free: 1, reserved: 0, occupancyRate: 75, availabilityRate: 25 }, validation: { critical: 0, warning: 1, info: 0 } },
        { mapId: 'norte', mapName: 'Norte', seats: { total: 6, occupied: 2, free: 2, reserved: 2, occupancyRate: 33.33, availabilityRate: 33.33 }, validation: { critical: 1, warning: 0, info: 1 } }
      ]
    },
    validation: {
      summary: { total: 3, critical: 1, warning: 1, info: 1 },
      results: [
        { id: 'info|sur', severity: 'Info', ruleId: 'historical', entityId: 'S-02', mapId: 'sur', title: 'Marca histórica' },
        { id: 'critical|norte', severity: 'Critical', ruleId: 'coordinate', entityId: 'N-02', mapId: 'norte', title: 'Coordenada inválida' },
        { id: 'warning|sur', severity: 'Warning', ruleId: 'person', entityId: 'S-01', mapId: 'sur', title: 'Persona duplicada' }
      ]
    },
    scenario: null,
    scenarioDiff: null
  };
  return { ...base, ...overrides };
}

test('reality context uses the effective reality label', () => {
  const model = buildDashboardModel(fixture());
  equal(model.context, { mode: 'reality', label: 'REALIDAD', scenarioId: null, scenarioName: null }, 'reality has no scenario metadata');
  assert(model.scenarioImpact.available === false, 'reality does not invent a scenario impact');
});

test('scenario context retains supplied metadata and impact', () => {
  const model = buildDashboardModel(fixture({
    scenario: { id: 'scenario-17', name: 'Traslado Norte' },
    scenarioDiff: {
      impactSummary: { total: 4, added: 1, removed: 0, moved: 2, modified: 1, assignments: 2, workspaces: 2, changedFields: 6, byMap: { sur: 1, norte: 3 } },
      validationImpact: { introduced: [{ id: 'new' }], resolved: [{ id: 'old' }], persistent: [{ id: 'same' }, { id: 'same-2' }] }
    }
  }));
  equal(model.context, { mode: 'scenario', label: 'ESCENARIO · Traslado Norte', scenarioId: 'scenario-17', scenarioName: 'Traslado Norte' }, 'scenario context is explicit');
  equal(model.scenarioImpact.validation, { introduced: 1, resolved: 1, persistent: 2 }, 'scenario validation impact is consumed');
  equal(model.scenarioImpact.mapChanges.map(item => item.mapId), ['norte', 'sur'], 'scenario map impact is stable');
});

test('KPI cards consume supplied values without recalculating rates', () => {
  const model = buildDashboardModel(fixture());
  equal(card(model, 'total').value, 10, 'total is retained');
  equal(card(model, 'free').value, 3, 'free is retained');
  equal(card(model, 'occupancy').value, 73.5, 'provided occupancy rate is retained instead of calculated from occupied/total');
  equal(card(model, 'availability').value, 29.25, 'provided availability rate is retained instead of calculated from free/total');
});

test('validation summary and normalized problem inputs drive the problem model', () => {
  const model = buildDashboardModel(fixture());
  equal({ total: model.problems.total, critical: model.problems.critical, warning: model.problems.warning, info: model.problems.info }, { total: 3, critical: 1, warning: 1, info: 1 }, 'summary is retained');
  equal(model.problems.items.map(item => item.id), ['critical|norte', 'warning|sur', 'info|sur'], 'results are ordered by severity and stable fields');
  equal(model.attention.severity, 'Critical', 'critical validation becomes attention');
});

test('historical diagnostics are excluded from Dashboard totals and map counts', () => {
  const model = buildDashboardModel({
    analytics: {
      totals: { total: 2, occupied: 0, free: 2, reserved: 0, occupancyRate: 0, availabilityRate: 100 },
      maps: [{ mapId: 'norte', mapName: 'Norte', seats: { total: 2, occupied: 0, free: 2, reserved: 0, occupancyRate: 0, availabilityRate: 100 } }]
    },
    validation: {
      results: [
        { id: 'history', severity: 'Info', ruleId: 'historical-occupied-without-assignment', classification: 'Historical', operational: false, entityId: 'N-01', mapId: 'norte', title: 'Marca histórica' },
        { id: 'active', severity: 'Critical', ruleId: 'invalid-coordinate', classification: 'Operational', operational: true, entityId: 'N-02', mapId: 'norte', title: 'Coordenada inválida' }
      ]
    }
  });
  equal({ total: model.problems.total, critical: model.problems.critical, warning: model.problems.warning, info: model.problems.info }, { total: 1, critical: 1, warning: 0, info: 0 }, 'Dashboard summary excludes historical diagnostics');
  equal(model.problems.items.map(item => item.id), ['active'], 'Dashboard problem list excludes historical diagnostics');
  equal(model.maps[0].problems.total, 1, 'Dashboard by-map count excludes historical diagnostics');
});

test('per-map summary and availability ranking are deterministic', () => {
  const model = buildDashboardModel(fixture());
  equal(model.maps.map(map => map.mapId), ['norte', 'sur'], 'maps sort by displayed name');
  equal(model.availabilityRanking.map(map => map.mapId), ['norte', 'sur'], 'availability ranks highest supplied rate first');
  equal(model.maps[0].availabilityRate, 33.33, 'map availability uses supplied analytics rate');
});

test('free navigation target uses the existing quick filter', () => {
  const model = buildDashboardModel(fixture());
  equal(model.navigation.free, { kind: 'list', label: 'Ver puestos libres', filters: { quick: 'free' } }, 'free target is actionable');
  equal(card(model, 'free').target, model.navigation.free, 'free KPI exposes the same target');
});

test('problem navigation targets filter severity and map', () => {
  const model = buildDashboardModel(fixture());
  equal(model.navigation.problems.critical.filters, { severity: 'Critical', mapId: '' }, 'critical target filters severity');
  equal(model.maps[1].problemsTarget.filters, { severity: '', mapId: 'sur' }, 'map problem target filters map');
  equal(model.problems.items[0].target.filters, { severity: 'Critical', mapId: 'norte' }, 'problem item target retains both filters');
});

test('map navigation targets identify the selected map', () => {
  const model = buildDashboardModel(fixture());
  equal(model.maps[0].target, { kind: 'map', label: 'Ver plano Norte', mapId: 'norte' }, 'map summary provides a direct map target');
  equal(model.navigation.maps[1].target.mapId, 'sur', 'navigation exposes each map target');
});

test('scenario impact consumes summary values and never creates an impact from nothing', () => {
  const scenarioDiff = { impactSummary: { total: 2, added: 1, removed: 1, moved: 9, modified: 9, changedFields: 4 }, validationImpact: { introduced: [], resolved: [], persistent: [] } };
  const model = buildDashboardModel(fixture({ scenario: { scenarioId: 'draft' }, scenarioDiff }));
  equal({ total: model.scenarioImpact.total, added: model.scenarioImpact.added, removed: model.scenarioImpact.removed, moved: model.scenarioImpact.moved, modified: model.scenarioImpact.modified, changedFields: model.scenarioImpact.changedFields }, { total: 2, added: 1, removed: 1, moved: 9, modified: 9, changedFields: 4 }, 'impact fields are copied from the diff summary');
  assert(model.scenarioImpact.available, 'provided diff enables scenario impact');
});

test('empty inputs receive stable empty labels and zero metrics', () => {
  const model = buildDashboardModel({});
  equal(model.maps, [], 'missing maps stay empty');
  equal(model.problems.total, 0, 'missing validation is safe');
  equal(model.emptyStates, {
    maps: 'No hay planos con analítica disponible.',
    availability: 'No hay planos para clasificar por disponibilidad.',
    problems: 'No hay problemas de validación.',
    scenario: 'Selecciona un escenario para ver su impacto.',
    attention: 'No hay acciones pendientes.'
  }, 'empty labels are explicit');
});

test('the same input always returns the same serializable model', () => {
  const input = fixture();
  equal(buildDashboardModel(input), buildDashboardModel(input), 'model is deterministic');
});

test('the builder does not mutate inputs and returns an immutable model', () => {
  const input = fixture();
  const before = JSON.stringify(input);
  const model = buildDashboardModel(input);
  equal(JSON.stringify(input), before, 'input remains unchanged');
  assert(Object.isFrozen(model) && Object.isFrozen(model.maps) && Object.isFrozen(model.maps[0]), 'model and nested values are frozen');
  assert(Object.isFrozen(model.kpiCards[0].target) || model.kpiCards[0].target === null, 'navigation descriptors are immutable');
});

test('formatting and invalid numbers never expose NaN or Infinity', () => {
  equal(formatNumber(1234.5), '1.234,5', 'number formatting is locale-independent');
  equal(formatPercent(33.3), '33,3 %', 'percent formatting is explicit');
  const model = buildDashboardModel({
    analytics: { totals: { total: Infinity, occupied: 'bad', free: Number.NaN, reserved: -4, occupancyRate: Infinity, availabilityRate: -1 }, maps: [{ mapId: 'x', seats: { occupancyRate: NaN, availabilityRate: Infinity } }] },
    validation: { summary: { total: Infinity, critical: NaN, warning: -2, info: 'bad' } }
  });
  const serialized = JSON.stringify(model);
  assert(!serialized.includes('NaN') && !serialized.includes('Infinity'), 'serialized model contains only finite display data');
  equal([card(model, 'total').value, card(model, 'occupancy').value, card(model, 'availability').value, model.problems.total], [0, 0, 0, 0], 'invalid values normalize safely');
});
