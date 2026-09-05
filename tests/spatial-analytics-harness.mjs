import test from 'node:test';
import assert from 'node:assert/strict';
import helpers from '../Resources/js/features/analytics/spatial-analytics-helpers.js';
const equal = (actual, expected, message) => {
  if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}; expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`);
};

const analytics = {
  Maps: [
    {
      MapId: 'north',
      Seats: { OccupancyRate: 62.5, AvailabilityRate: 25 },
      Validation: { Critical: 1, Warning: 2, Info: 1 }
    },
    {
      mapId: 'south',
      seats: { occupancyRate: 20, availabilityRate: 60 },
      validation: { total: 5 }
    }
  ],
  HeatmapPoints: [
    { MapId: 'north', Layer: 'scenario-changes', Value: 1 },
    { mapId: 'north', layer: 'scenario-changes', value: 1 },
    { mapId: 'south', layer: 'scenario-changes', value: 3 }
  ]
};

test('selects only supported metric modes', () => {
  equal(helpers.metricModes, ['occupancy', 'availability', 'problems', 'scenarioChanges'], 'mode vocabulary is bounded');
  equal(helpers.selectMetricMode('availability'), 'availability', 'availability remains selected');
  equal(helpers.selectMetricMode('scenario-changes'), 'scenarioChanges', 'backend scenario layer aliases normalize');
  equal(helpers.selectMetricMode('unknown', 'problems'), 'problems', 'invalid mode falls back deterministically');
});

test('provides accessible legend metadata for every metric mode', () => {
  helpers.metricModes.forEach(mode => {
    const legend = helpers.getLegendMetadata(mode, { max: 8 });
    assert(legend.id && legend.label && legend.description && legend.ariaLabel, `${mode} legend has accessible text`);
    assert(legend.ariaLabel.includes(legend.label), `${mode} aria label includes its name`);
    equal([legend.min, legend.max], [0, 8], `${mode} legend retains its scale`);
  });
  equal(helpers.getLegendMetadata('occupancy', { max: 100 }).unit, '%', 'rate legends expose percent units');
});

test('looks up map metrics across bridge naming conventions', () => {
  assert(helpers.findMapMetrics(analytics, 'north') === analytics.Maps[0], 'map lookup returns the original matching map');
  equal(helpers.getMapMetric(analytics, 'north', 'occupancy'), 62.5, 'occupancy uses per-map rate');
  equal(helpers.getMapMetric(analytics, 'north', 'availability'), 25, 'availability uses per-map rate');
  equal(helpers.getMapMetric(analytics, 'north', 'problems'), 4, 'problems sums validation severities when no total exists');
  equal(helpers.getMapMetric(analytics, 'south', 'problems'), 5, 'explicit validation total is retained');
  equal(helpers.getMapMetric(analytics, 'north', 'scenarioChanges'), 2, 'scenario density sums matching layer values');
  equal(helpers.getMapMetric(analytics, 'missing', 'occupancy'), 0, 'missing map has a safe zero metric');
});

test('calculates a deterministic global comparison scale', () => {
  const scale = helpers.calculateSharedScale([{ value: 3 }, { value: 7 }], [{ value: 5 }, { value: 9 }]);
  equal(scale, { min: 0, max: 9, globalMax: 9, leftMax: 7, rightMax: 9 }, 'both sides use one global maximum');
  equal(helpers.getSharedScale([9, 1], [7]).max, 9, 'alias uses the same global maximum');
  equal(helpers.calculateSharedScale([], []).max, 0, 'empty comparison has a stable zero scale');
});

test('creates immutable heatmap layer visibility state', () => {
  const initial = helpers.createHeatmapLayerVisibility('problems');
  equal(initial, { occupancy: false, availability: false, problems: true, scenarioChanges: false }, 'only active layer starts visible');
  const updated = helpers.setHeatmapLayerVisibility(initial, 'scenarioChanges', true);
  assert(updated !== initial, 'visibility update is immutable');
  assert(helpers.isHeatmapLayerVisible(updated, 'problems'), 'existing layer remains visible');
  assert(helpers.isHeatmapLayerVisible(updated, 'scenarioChanges'), 'requested layer becomes visible');
  assert(!helpers.isHeatmapLayerVisible(updated, 'availability'), 'unselected layer remains hidden');
  equal(helpers.setHeatmapLayerVisibility({}, 'unknown'), { occupancy: false, availability: false, problems: false, scenarioChanges: false }, 'unknown layers do not enable a metric');
});

test('normalizes numeric analytics summaries safely', () => {
  const normalized = helpers.normalizeAnalyticsSummary({
    Totals: { Total: '8', Occupied: '5', Free: 2, Reserved: 1, OccupancyRate: '62.5', AvailabilityRate: 125 },
    Validation: { Critical: '2', Warning: 'bad', Info: 1 },
    Scenario: { TotalChanges: '4', MappedChanges: Number.NaN }
  });
  equal(normalized, {
    total: 8, occupied: 5, free: 2, reserved: 1, occupancyRate: 62.5, availabilityRate: 100,
    critical: 2, warning: 0, info: 1, problems: 3, scenarioChanges: 4, mappedScenarioChanges: 0
  }, 'bridge numeric strings and invalid values normalize to safe summary fields');
  equal(helpers.normalizeAnalyticsSummary({}), {
    total: 0, occupied: 0, free: 0, reserved: 0, occupancyRate: 0, availabilityRate: 0,
    critical: 0, warning: 0, info: 0, problems: 0, scenarioChanges: 0, mappedScenarioChanges: 0
  }, 'missing analytics data normalizes to zeroes');
});

test('calculates percentage-points deltas', () => {
  equal(helpers.percentagePointsDelta(62.5, 50), 12.5, 'increase is expressed in percentage points');
  equal(helpers.percentagePointDelta(20, 37.25), -17.25, 'decrease is expressed in percentage points');
  equal(helpers.percentagePointsDelta(0.3, 0.1), 0.2, 'delta has deterministic decimal rounding');
  equal(helpers.percentagePointsDelta('bad', 10), -10, 'invalid current values safely normalize to zero');
});
