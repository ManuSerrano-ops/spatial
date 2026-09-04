'use strict';

const fs = require('fs');
const path = require('path');
const helpers = require('../Resources/js/features/selection/bulk-selection-helpers.js');
const test = require('node:test');
const assert = require('node:assert/strict');
const equal = (actual, expected, message) => { if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`); };
const workspace = (workspaceId, effectiveState, type = 'legacy-drawing-value') => ({ workspaceId, effectiveState, type });
const mixed = [workspace('F-1', 'free'), workspace('F-2', 'free'), workspace('R-1', 'reserved'), workspace('O-1', 'occupied'), workspace('F-3', 'free')];
const snapshot = value => JSON.stringify(value);

function reserveEligibility(values = mixed) { return helpers.deriveBulkActionEligibility(values, 'reserved'); }

test('selecting does not mutate Reality input', () => { const input = structuredClone(mixed); const before = snapshot(input); reserveEligibility(input); equal(snapshot(input), before, 'selection analysis is read-only'); });
test('changing action does not mutate data', () => { const input = structuredClone(mixed); const before = snapshot(input); helpers.deriveBulkActionEligibility(input, 'confirmed'); equal(snapshot(input), before, 'action changes are pending only'); });
test('clear selection is representable without a command', () => equal(helpers.buildBulkSelectionCommand(helpers.deriveBulkActionEligibility([], 'reserved')), null, 'empty selection has no command'));
test('free is eligible for reserve', () => assert(reserveEligibility([workspace('F', 'free')]).eligible[0]?.workspaceId === 'F', 'free target must be eligible'));
test('occupied is blocked for reserve', () => equal(reserveEligibility([workspace('O', 'occupied')]).excluded[0].reason, 'Puesto ocupado.', 'occupied target reason'));
test('mixed selection summary is explicit', () => equal(helpers.buildBulkActionSummary(reserveEligibility()).detail, 'Reservar: 3 aplicables · 2 no aplicables.', 'mixed summary'));
test('mixed selection never silently includes excluded targets', () => equal(helpers.buildBulkSelectionCommand(reserveEligibility()).workstationIds, ['F-1', 'F-2', 'F-3'], 'only confirmed safe subset is sent'));
test('one command represents the bulk operation', () => equal(Object.keys(helpers.buildBulkSelectionCommand(reserveEligibility())).sort(), ['status', 'workstationIds'], 'single command contract'));
test('five targets remain one command and one undo intent', () => equal(helpers.buildBulkSelectionCommand(reserveEligibility([1,2,3,4,5].map(index => workspace(`F-${index}`, 'free')))).workstationIds.length, 5, 'five targets in one request'));
test('all eligible targets are in Apply command', () => equal(helpers.buildBulkSelectionCommand(reserveEligibility()).workstationIds.length, reserveEligibility().eligibleCount, 'complete eligible set'));
test('remove reservation targets only Reserved', () => equal(helpers.buildBulkSelectionCommand(helpers.deriveBulkActionEligibility(mixed, 'confirmed')).workstationIds, ['R-1'], 'remove reservation eligibility'));
test('mixed previous states remain available to backend snapshot', () => equal(mixed.map(item => item.effectiveState), ['free', 'free', 'reserved', 'occupied', 'free'], 'helper does not normalize previous states'));
test('already reserved is a no-op with exact reason', () => { const target = reserveEligibility([workspace('R', 'reserved')]).excluded[0]; equal([target.outcome, target.reason], ['noop', 'Ya reservado.'], 'reserved no-op'); });
test('double Apply is prevented while one command is in flight', () => { const command = helpers.buildBulkSelectionCommand(reserveEligibility()); const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8'); assert(Object.isFrozen(command), 'command is one immutable in-flight identity'); assert(app.includes("if (appState.bulk.inFlight) return"), 'confirm handler does not reject a second submit'); assert(app.includes("Boolean(appState.bulk.inFlight)"), 'Apply is not disabled while in flight'); });
test('invalid action creates no write command', () => equal(helpers.buildBulkSelectionCommand(helpers.deriveBulkActionEligibility(mixed, 'invalid')), null, 'invalid action cannot write'));
test('invalid action creates no undo intent', () => assert(!helpers.buildBulkActionSummary(helpers.deriveBulkActionEligibility(mixed, 'invalid')).canApply, 'invalid action cannot create undo'));
test('selection changes recalculate targets', () => equal(buildCount(mixed.slice(0, 3)), 2, 'eligible count follows current selection'));
test('effectiveState drives eligibility', () => equal(reserveEligibility([workspace('X', 'free', 'occupied')]).eligibleCount, 1, 'effective free wins'));
test('legacy seat.type is ignored', () => equal(reserveEligibility([workspace('X', 'occupied', 'free')]).eligibleCount, 0, 'legacy type cannot make occupied eligible'));
test('Planner launch has no bulk command side effect', () => { const eligibility = reserveEligibility(); const before = snapshot(eligibility); const plannerSelection = mixed.map(item => item.workspaceId); equal(snapshot(eligibility), before, 'planner selection does not change bulk state'); equal(plannerSelection.length, 5, 'planner consumes selection only'); });
test('Planner cancel is not represented as Reality Undo', () => assert(!Object.hasOwn(helpers.actionDefinitions, 'planner'), 'Planner is not a bulk Reality action'));
test('Ctrl+Z and visible Undo use global hook in app', () => { const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8'); assert(app.includes("event.ctrlKey && event.key.toLowerCase() === 'z'"), 'Ctrl+Z hook missing'); assert(app.includes("$('bulk-undo').onclick = () => $('undo').click()"), 'bulk Undo does not invoke global Undo'); });
test('visible Undo is wired to global Undo button', () => { const index = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'index.html'), 'utf8'); assert(index.includes('id="bulk-undo"'), 'visible bulk Undo button missing'); });
test('deterministic result', () => equal(reserveEligibility(), reserveEligibility(), 'same input produces same eligibility'));
test('input and outputs are immutable', () => { const input = structuredClone(mixed); const before = snapshot(input); const result = reserveEligibility(input); assert(Object.isFrozen(result) && Object.isFrozen(result.eligible) && Object.isFrozen(result.reasons), 'output is frozen'); equal(snapshot(input), before, 'input is untouched'); });

function buildCount(values) { return reserveEligibility(values).eligibleCount; }
