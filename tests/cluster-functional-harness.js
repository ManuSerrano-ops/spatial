'use strict';
const fs = require('fs');
const path = require('path');
const helpers = require('../Resources/js/features/map/map-density-helpers.js');
const test = require('node:test'); const assert = require('node:assert/strict'); const equal = (actual, expected, message) => { if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`); };
const seats = Array.from({ length: 10 }, (_, index) => ({ id: `W${index + 1}`, x: .40 + index * .0002, y: .40 + index * .0002, effectiveState: index < 3 ? 'free' : index === 3 ? 'reserved' : 'occupied' }));
const build = context => helpers.buildMapDensityPresentation({ mapId: 'sur', workspaces: seats, grid: { columns: 24, rows: 18 }, semanticZoom: 'GLOBAL', viewport: { width: 1000, height: 700 }, stateFor: seat => seat.effectiveState, problemsFor: seat => seat.id === 'W5' ? 1 : 0, functionalContext: context });

test('every workspace stays individual regardless of functional context', () => {
  for (const context of [{}, { forcedIndividualIds: ['W1'], searchIds: ['W1'] }, { selectedIds: ['W1', 'W2'] }, { plannerDestinationIds: ['W3'] }, { changedIds: ['W4'] }]) {
    const result = build(context); equal(result.individuals.map(item => item.id), seats.map(item => item.id), 'individual pins'); equal(result.clusters, [], 'automatic clusters');
  }
});
test('search focus highlights the hit and dims other individual pins', () => equal([helpers.deriveMapFocusPresentation({ workspace: seats[0], hasSearch: true, searchMatch: true }), helpers.deriveMapFocusPresentation({ workspace: seats[1], hasSearch: true })], ['highlighted', 'dimmed'], 'search focus'));
test('selection focus highlights selected individual pins', () => equal([helpers.deriveMapFocusPresentation({ workspace: seats[0], hasSelection: true, selected: true }), helpers.deriveMapFocusPresentation({ workspace: seats[1], hasSelection: true })], ['highlighted', 'dimmed'], 'selection focus'));
test('planner, problems and scenario changes retain individual focus behavior', () => {
  equal(helpers.deriveMapFocusPresentation({ workspace: seats[0], plannerActive: true, plannerState: 'destination' }), 'highlighted', 'planner');
  equal(helpers.deriveMapFocusPresentation({ workspace: seats[1], problemsFocused: true, problemMatch: true }), 'highlighted', 'problems');
  equal(helpers.deriveMapFocusPresentation({ workspace: seats[2], changesFocused: true, changed: true }), 'highlighted', 'changes');
});
test('grid cell metadata remains non-visual and immutable', () => {
  const before = JSON.stringify(seats); const cells = helpers.buildGridCells({ mapId: 'sur', workspaces: seats, grid: { columns: 24, rows: 18 } }); assert(cells.length > 0, 'grid metadata'); equal(JSON.stringify(seats), before, 'workspace mutation');
});
test('frontend uses managed area cards only', () => {
  const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8');
  assert(app.includes('renderManagedAreaCards(pins, map);'), 'managed area rendering missing');
  assert(!app.includes('buildMapDensityPresentation({'), 'automatic density renderer remains');
  assert(!app.includes('density.clusters.forEach'), 'automatic cluster card renderer remains');
});
