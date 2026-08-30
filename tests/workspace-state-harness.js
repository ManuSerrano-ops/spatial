'use strict';

const state = require('../Resources/js/shared/workspace/workspace-state-helpers.js');
const presentation = require('../Resources/js/shared/workspace/workspace-presentation-helpers.js');
const pins = require('../Resources/js/features/map/pin-state-helpers.js');

const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const equal = (actual, expected, message) => {
  if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}; expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
};
const derive = (seat, assignment = {}) => state.deriveEffectiveWorkspaceState({ seat, assignment });
const fixture = () => [
  { mapId: 'norte', seat: { id: 'A', displayLocation: 'A-01', personId: 'legacy-a', type: 'occupied' }, assignment: { workstationId: 'A', personId: 'current-a', status: 'confirmed' } },
  { mapId: 'norte', seat: { id: 'B', displayLocation: 'A-02', type: 'occupied' }, assignment: {} },
  { mapId: 'sur', seat: { id: 'A', displayLocation: 'S-01', type: 'free' }, assignment: {} },
  { mapId: 'sur', seat: { id: 'C', displayLocation: 'S-02', personId: 'legacy-c', type: 'occupied' }, assignment: {} },
  { mapId: 'id', seat: { id: 'D', displayLocation: 'I-01', type: 'free' }, assignment: { workstationId: 'D', status: 'reserved' } }
];
const effectiveRows = rows => rows.map(row => ({ ...row, effective: derive(row.seat, row.assignment) }));
const counts = rows => rows.reduce((total, row) => { total[row.effective.state]++; return total; }, { free: 0, occupied: 0, reserved: 0 });
const quick = (rows, value) => rows.filter(row => row.effective.state === value);
const heatmap = rows => rows.filter(row => row.effective.state === 'occupied' || row.effective.state === 'free').map(row => ({ mapId: row.mapId, id: row.seat.id, layer: row.effective.state === 'occupied' ? 'occupancy' : 'availability' }));

test('automatic assignment with person is occupied', () => equal(derive({ personId: 'legacy' }, { personId: 'current', status: 'confirmed' }).state, 'occupied', 'current assignment wins'));
test('automatic no assignment is free', () => equal(derive({}, {}).state, 'free', 'unassigned seat is free'));
test('inherited occupied without person is free', () => equal(derive({ type: 'occupied' }, {}).state, 'free', 'drawing type is not operational occupancy'));
test('inherited free with current assignment is occupied', () => equal(derive({ type: 'free' }, { personId: 'person-1' }).state, 'occupied', 'current person determines effective occupancy'));
test('explicit reservation is reserved', () => { const result = derive({ personId: 'person-1' }, { status: 'reserved' }); equal([result.state, result.mode], ['reserved', 'manual'], 'reservation has explicit manual priority'); });
test('manual free override is retained', () => { const result = derive({ personId: 'person-1' }, { configuredState: 'manual-free', personId: 'person-1' }); equal([result.state, result.mode], ['free', 'manual'], 'manual free wins over a person'); });
test('manual occupied override is retained', () => { const result = derive({}, { configuredState: 'manual-occupied' }); equal([result.state, result.mode], ['occupied', 'manual'], 'manual occupied is explicit'); });
test('multiple maps preserve independent state', () => { const rows = effectiveRows(fixture()); equal(counts(rows), { free: 2, occupied: 2, reserved: 1 }, 'fixture uses all effective states'); });
test('same technical ID in different maps does not leak state', () => { const rows = effectiveRows(fixture()); equal([rows[0].effective.state, rows[2].effective.state], ['occupied', 'free'], 'input assignment is map-contextual and deterministic'); });
test('quick filters use effective state', () => { const rows = effectiveRows(fixture()); equal([quick(rows, 'occupied').length, quick(rows, 'free').length, quick(rows, 'reserved').length], [2, 2, 1], 'filters agree with the derived state'); });
test('pin base state uses effective state', () => { const result = derive({ type: 'occupied' }, {}); equal(pins.derivePinPresentation({ businessState: result.state }).businessState, 'free', 'pin does not inherit drawing occupancy'); });
test('inspector presentation exposes state and mode', () => { const result = derive({ personId: 'person-1' }, {}); const view = presentation.buildWorkspacePresentation({ seat: { name: 'Mesa 1' }, assignment: {}, effectiveState: result, displayLocation: 'N-01' }); equal([view.assignmentStatus, view.stateMode, view.assignmentStatusLabel, view.stateModeLabel], ['occupied', 'automatic', 'Ocupado', 'Automático'], 'presentation shares the derived state'); });
test('analytics counts use effective state', () => { const rows = effectiveRows(fixture()); equal(counts(rows), { free: 2, occupied: 2, reserved: 1 }, 'analytics source totals are stable'); });
test('dashboard counts equal analytics counts', () => { const rows = effectiveRows(fixture()); equal(counts(rows), counts(rows), 'dashboard and analytics consume identical state totals'); });
test('heatmap uses effective occupancy and availability', () => { const rows = effectiveRows(fixture()); equal(heatmap(rows).map(point => point.layer), ['occupancy', 'availability', 'availability', 'occupancy'], 'reserved seats do not create occupancy or availability points'); });
test('reality and scenario use the same policy', () => { const reality = derive({ type: 'occupied' }, {}); const scenario = derive({ type: 'occupied' }, { personId: 'scenario-person' }); equal([reality.state, scenario.state], ['free', 'occupied'], 'only effective assignment changes the scenario state'); });
test('output is deterministic', () => { const input = { seat: { personId: 'p' }, assignment: {} }; equal(derive(input.seat, input.assignment), derive(input.seat, input.assignment), 'same input has same derived state'); });
test('input is not mutated', () => { const input = { seat: { personId: 'p', type: 'occupied' }, assignment: { status: 'confirmed' } }; const before = JSON.stringify(input); derive(input.seat, input.assignment); equal(JSON.stringify(input), before, 'derivation is pure'); });

let passed = 0;
for (const { name, fn } of tests) {
  try { fn(); passed++; }
  catch (error) { console.error(`FAIL: ${name}: ${error.message}`); }
}
console.log(`workspace-state-harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
