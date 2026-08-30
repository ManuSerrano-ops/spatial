'use strict';

const helpers = require('../Resources/js/features/managed-areas/managed-area-helpers.js');
const tests = []; const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const equal = (actual, expected, message) => { if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`); };
const fails = (fn, text) => { try { fn(); } catch (error) { if (!text || error.message.includes(text)) return; throw error; } throw new Error(`Expected error containing: ${text}`); };
const area = (id, mapId, name, workspaceIds = [], presentation = { offsetX: 0, offsetY: 0 }) => ({ id, mapId, name, workspaceIds, presentation });
const initial = { areas: [area('north-a', 'north', 'North A', ['W-2', 'W-1']), area('north-b', 'north', 'North B', ['W-3']), area('south-a', 'south', 'South A', ['W-1'])] };
const names = state => state.areas.map(item => item.id);
const members = (state, id) => state.areas.find(item => item.id === id)?.workspaceIds;

test('normalizes areas and members deterministically', () => equal(helpers.normalizeState(initial), { areas: [area('north-a', 'north', 'North A', ['W-1', 'W-2']), area('north-b', 'north', 'North B', ['W-3']), area('south-a', 'south', 'South A', ['W-1'])] }, 'canonical state'));
test('create adds a canonical managed area', () => { const output = helpers.createArea(initial, area('north-c', 'north', 'North C', ['W-5', 'W-4'])); equal([names(output.state), members(output.state, 'north-c')], [['north-a', 'north-b', 'north-c', 'south-a'], ['W-4', 'W-5']], 'create'); });
test('create rejects duplicate area ids', () => fails(() => helpers.createArea(initial, area('north-a', 'north', 'Duplicate')), 'already exists'));
test('create enforces one area per workspace per map', () => fails(() => helpers.createArea(initial, area('north-c', 'north', 'North C', ['W-1'])), 'already belongs'));
test('the same workspace may belong to one area on each map', () => assert(helpers.createArea(initial, area('east-a', 'east', 'East A', ['W-1'])).state.areas.length === 4, 'cross-map membership rejected'));
test('rename changes only the area name', () => { const output = helpers.renameArea(initial, 'north-a', 'Finance'); equal(output.state.areas.find(item => item.id === 'north-a'), area('north-a', 'north', 'Finance', ['W-1', 'W-2']), 'rename'); });
test('rename rejects blank names', () => fails(() => helpers.renameArea(initial, 'north-a', '  '), 'name is required'));
test('add inserts members in stable order without duplicates', () => equal(members(helpers.addWorkspaces(initial, 'north-a', ['W-5', 'W-4', 'W-4']).state, 'north-a'), ['W-1', 'W-2', 'W-4', 'W-5'], 'add'));
test('add rejects membership owned by another area on that map', () => fails(() => helpers.addWorkspaces(initial, 'north-a', ['W-3']), 'already belongs'));
test('remove releases membership without deleting the area', () => { const state = helpers.removeWorkspaces(initial, 'north-a', ['W-1']).state; equal([members(state, 'north-a'), names(state)], [['W-2'], ['north-a', 'north-b', 'south-a']], 'remove'); });
test('remove of a missing member is a deterministic no-op state', () => equal(helpers.removeWorkspaces(initial, 'north-a', ['missing']).state, helpers.normalizeState(initial), 'remove no-op'));
test('move transfers membership atomically', () => { const state = helpers.moveWorkspaces(initial, 'north-a', 'north-b', ['W-2', 'W-1']).state; equal([members(state, 'north-a'), members(state, 'north-b')], [[], ['W-1', 'W-2', 'W-3']], 'move'); });
test('move requires source membership', () => fails(() => helpers.moveWorkspaces(initial, 'north-a', 'north-b', ['W-9']), 'does not belong'));
test('move cannot cross maps', () => fails(() => helpers.moveWorkspaces(initial, 'north-a', 'south-a', ['W-1']), 'different maps'));
test('merge retains target identity and dissolves sources', () => { const state = helpers.mergeAreas(initial, 'north-a', ['north-b']).state; equal([names(state), members(state, 'north-a')], [['north-a', 'south-a'], ['W-1', 'W-2', 'W-3']], 'merge'); });
test('merge cannot cross maps', () => fails(() => helpers.mergeAreas(initial, 'north-a', ['south-a']), 'different maps'));
test('dissolve removes the area and releases its workspaces', () => equal(names(helpers.dissolveArea(initial, 'north-a').state), ['north-b', 'south-a'], 'dissolve'));
    test('deleteMove transfers all members and removes source area', () => { const state = helpers.deleteMoveArea(initial, 'north-a', 'north-b').state; equal([names(state), members(state, 'north-b')], [['north-b', 'south-a'], ['W-1', 'W-2', 'W-3']], 'deleteMove'); });
    test('presentation offsets survive normalization', () => equal(helpers.normalizeState({ managedAreas: [area('a', 'map', 'A', ['W'], { offsetX: .2, offsetY: -.1 })] }).areas[0].presentation, { offsetX: .2, offsetY: -.1 }, 'presentation'));
    test('all WebView backend commands are explicit', () => equal(Object.keys(helpers.backendActions), ['create', 'rename', 'add', 'remove', 'move', 'merge', 'dissolve', 'deleteMove'], 'actions'));
    test('backend command carries immutable payload', () => { const command = helpers.buildBackendCommand('create', { mapId: 'north', name: 'Area', workspaceIds: ['W'] }); equal(command.action, 'createManagedArea', 'action'); assert(Object.isFrozen(command) && Object.isFrozen(command.payload), 'command immutability'); });
test('invalid incoming state detects duplicate map membership', () => fails(() => helpers.normalizeState({ areas: [area('a', 'map', 'A', ['W']), area('b', 'map', 'B', ['W'])] }), 'more than one area'));
test('operations never mutate caller state', () => { const input = structuredClone(initial); const before = JSON.stringify(input); helpers.moveWorkspaces(input, 'north-a', 'north-b', ['W-1']); equal(JSON.stringify(input), before, 'input mutation'); });
test('state and operation snapshot are deeply immutable at collection boundaries', () => { const output = helpers.createArea(initial, area('north-c', 'north', 'C')); assert(Object.isFrozen(output) && Object.isFrozen(output.state) && Object.isFrozen(output.state.areas) && Object.isFrozen(output.state.areas[0]) && Object.isFrozen(output.state.areas[0].workspaceIds) && Object.isFrozen(output.snapshot), 'result is not frozen'); });
test('same operation produces byte-identical deterministic state and snapshot', () => { const run = () => helpers.addWorkspaces(initial, 'north-a', ['W-5', 'W-4']); equal(run(), run(), 'determinism'); });
test('snapshot is JSON serializable for Undo transport', () => { const output = helpers.moveWorkspaces(initial, 'north-a', 'north-b', ['W-1']); equal(JSON.parse(JSON.stringify(output.snapshot)), output.snapshot, 'snapshot transport'); });
test('snapshot identifies operation and affected entities', () => { const snapshot = helpers.moveWorkspaces(initial, 'north-a', 'north-b', ['W-2']).snapshot; equal([snapshot.kind, snapshot.affectedAreaIds, snapshot.affectedWorkspaceIds], ['move', ['north-a', 'north-b'], ['W-2']], 'snapshot identity'); });
test('Undo restores exact canonical before state', () => { const output = helpers.mergeAreas(initial, 'north-a', ['north-b']); equal(helpers.restoreSnapshot(output.snapshot, 'before'), helpers.normalizeState(initial), 'undo restore'); });
test('Redo restores exact canonical after state', () => { const output = helpers.dissolveArea(initial, 'north-a'); equal(helpers.restoreSnapshot(output.snapshot, 'after'), output.state, 'redo restore'); });

let passed = 0;
for (const item of tests) { try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); } }
console.log(`Managed area harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
