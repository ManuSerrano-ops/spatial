'use strict';

const fs = require('fs');
const path = require('path');
const helpers = require('../Resources/js/features/selection/selection-review-helpers.js');
const bulkHelpers = require('../Resources/js/features/selection/bulk-selection-helpers.js');
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const equal = (actual, expected, message) => { if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`); };
const selected = ['W-1', 'W-2', 'W-3', 'W-4', 'W-5'];
const workspaces = Object.fromEntries(selected.map((id, index) => [id, { mapId: 'norte', displayLocation: `G-${10 + index}`, person: index ? '' : 'jgomez', effectiveStateLabel: index === 2 ? 'Reservado' : index === 3 ? 'Ocupado' : 'Libre', device: index ? '' : 'pcs-lpt-941', roseta: index ? '' : '3-253', reference: index ? '' : 'REF-1', location: index ? '' : 'Norte' }]));
const snapshot = value => JSON.stringify(value);

function build(ids = selected, options = {}) { return helpers.buildSelectionReviewItems(ids, workspaces, options); }

test('zero selected hides review panel', () => equal(helpers.selectionReviewMode([]), 'empty', 'zero mode'));
test('one selected uses inspector mode', () => equal(helpers.selectionReviewMode(['W-1']), 'inspector', 'one mode'));
test('two selected uses selection mode', () => equal(helpers.selectionReviewMode(['W-1', 'W-2']), 'selection', 'two mode'));
test('five selected produce five items', () => equal(build().length, 5, 'item count'));
test('item title uses displayLocation', () => equal(build()[0].displayLocation, 'G-10', 'display location'));
test('current person is displayed', () => equal(build()[0].person, 'jgomez', 'person'));
test('missing person uses clear fallback', () => equal(build()[1].person, 'Sin asignar', 'person fallback'));
test('effective state is shown', () => equal(build()[2].effectiveState, 'Reservado', 'effective state'));
test('device is shown', () => equal(build()[0].device, 'pcs-lpt-941', 'device'));
test('roseta is shown', () => equal(build()[0].roseta, '3-253', 'roseta'));
test('deselect one decreases selection', () => equal(helpers.deselectWorkspace(selected, 'W-3'), ['W-1', 'W-2', 'W-4', 'W-5'], 'deselect'));
test('five to four transition', () => equal(helpers.deselectWorkspace(selected, 'W-5').length, 4, '5 to 4'));
test('two to one switches to inspector', () => equal(helpers.selectionReviewMode(helpers.deselectWorkspace(['W-1', 'W-2'], 'W-2')), 'inspector', '2 to 1'));
test('one to zero clears context', () => equal(helpers.selectionReviewMode(helpers.deselectWorkspace(['W-1'], 'W-1')), 'empty', '1 to 0'));
test('clear all returns empty selection', () => equal(helpers.clearSelection(), [], 'clear'));
test('clear does not mutate workspace data', () => { const before = snapshot(workspaces); helpers.clearSelection(); equal(snapshot(workspaces), before, 'clear data immutability'); });
test('deselect does not mutate Reality data', () => { const before = snapshot(workspaces); helpers.deselectWorkspace(selected, 'W-2'); equal(snapshot(workspaces), before, 'deselect data immutability'); });
test('bulk summary recalculates after removal', () => { const records = selected.map((workspaceId, index) => ({ workspaceId, effectiveState: index === 2 ? 'reserved' : index === 3 ? 'occupied' : 'free' })); const five = bulkHelpers.buildBulkActionSummary(bulkHelpers.deriveBulkActionEligibility(records, 'reserved')); const four = bulkHelpers.buildBulkActionSummary(bulkHelpers.deriveBulkActionEligibility(records.filter(item => item.workspaceId !== 'W-1'), 'reserved')); equal([five.selectedCount, five.eligibleCount, four.selectedCount, four.eligibleCount], [5, 3, 4, 2], 'bulk recalculation'); });
test('Planner eligibility reuses supplied existing result', () => { const items = build(['W-1', 'W-2'], { plannerByWorkspace: { 'W-1': { movable: true }, 'W-2': { movable: false, reason: 'Puesto libre.' } } }); equal(items.map(item => item.planner), [{ movable: true, reason: '' }, { movable: false, reason: 'Puesto libre.' }], 'planner projection'); });
test('selection order remains stable', () => equal(build(['W-4', 'W-1', 'W-3']).map(item => item.workspaceId), ['W-4', 'W-1', 'W-3'], 'stable order'));
test('large selection remains complete', () => { const ids = Array.from({ length: 120 }, (_, index) => `L-${index}`); const values = Object.fromEntries(ids.map(id => [id, { displayLocation: id, effectiveStateLabel: 'Libre' }])); equal(helpers.buildSelectionReviewItems(ids, values).length, 120, 'large count'); });
test('deterministic output', () => equal(build(), build(), 'determinism'));
test('inputs are not mutated and outputs are frozen', () => { const ids = [...selected]; const values = structuredClone(workspaces); const before = snapshot({ ids, values }); const result = helpers.buildSelectionReviewItems(ids, values); assert(Object.isFrozen(result) && Object.isFrozen(result[0]), 'frozen output'); equal(snapshot({ ids, values }), before, 'input immutability'); });
test('frontend uses central selection and delegated panel events', () => { const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8'); assert(app.includes('appState.selectedWorkspaces'), 'central selection missing'); assert(app.includes("$('selection-review-list').onclick"), 'delegated review event missing'); assert(app.includes('deselectSelectedWorkspace'), 'central deselect action missing'); });
test('responsive panel is viewport-contained and scrollable', () => { const css = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'app.css'), 'utf8'); assert(css.includes('.selection-review-list') && css.includes('overflow: auto') && css.includes('min-height: 0'), 'review scrolling contract missing'); assert(!css.includes('height: 100vh'), 'viewport clipping regression introduced'); });

let passed = 0;
for (const { name, fn } of tests) { try { fn(); passed++; } catch (error) { console.error(`FAIL: ${name}: ${error.message}`); } }
console.log(`Selection review harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
