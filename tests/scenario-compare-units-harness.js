'use strict';

const helpers = require('../Resources/js/features/scenarios/scenario-compare-helpers.js');
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const equal = (actual, expected, message) => {
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    throw new Error(`${message}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`);
  }
};

const change = (id, options = {}) => ({
  id,
  kind: options.kind || 'MODIFIED',
  type: options.type,
  operationId: options.operationId,
  atomic: options.atomic,
  entityType: options.entityType || 'assignment',
  entityId: options.entityId || id,
  mapId: options.mapId || 'norte',
  before: options.before,
  after: options.after,
  changedFields: options.changedFields || [],
  validationImpact: options.validationImpact
});
const modernSource = change('assignment|norte|G-10', { operationId: 'move-a', atomic: true, type: 'movement', entityId: 'G-10', before: { workstationId: 'G-10', personId: 'p-jgomez', deviceId: 'd-941' } });
const modernDestination = change('assignment|norte|H-05', { operationId: 'move-a', atomic: true, type: 'movement', entityId: 'H-05', after: { workstationId: 'H-05', personId: 'p-jgomez', deviceId: 'd-941' } });
const legacySource = change('seat|sur|G-10', { operationId: 'move-b', atomic: true, type: 'movement', entityType: 'workspace', entityId: 'G-10', mapId: 'sur', before: { id: 'G-10', personId: 'p-mlopez', deviceName: 'pcs-lpt-410' }, after: { id: 'G-10' }, changedFields: [{ field: 'personId', before: 'p-mlopez', after: null }] });
const legacyDestination = change('assignment|sur|H-05', { operationId: 'move-b', atomic: true, type: 'movement', entityId: 'H-05', mapId: 'sur', after: { workstationId: 'H-05', personId: 'p-mlopez', deviceId: 'd-410' } });
const independent = change('assignment|norte|K-11', { kind: 'MODIFIED', entityId: 'K-11', changedFields: [{ field: 'notes', before: 'A', after: 'B' }] });

const units = changes => helpers.buildCompareUnits(changes);

test('independent raw diff becomes one independent unit', () => {
  const result = units([independent]);
  equal(result.length, 1, 'one raw independent change has one unit');
  equal(result[0].kind, 'change', 'independent kind is change');
});
test('two same-operation members become one unit', () => equal(units([modernSource, modernDestination]).length, 1, 'modern movement groups'));
test('three same-operation members become one unit', () => equal(units([modernSource, modernDestination, change('seat|norte|G-10', { operationId: 'move-a', atomic: true, type: 'movement', entityType: 'workspace', entityId: 'G-10' })]).length, 1, 'three members group'));
test('different operation IDs remain distinct', () => equal(units([modernSource, modernDestination, legacySource, legacyDestination]).length, 2, 'operations do not merge'));
test('modern movement is a movement unit', () => equal(units([modernSource, modernDestination])[0].kind, 'movement', 'modern semantics are preserved'));
test('legacy movement is a movement unit', () => equal(units([legacySource, legacyDestination])[0].kind, 'movement', 'legacy implementation details are hidden'));
test('independent edit remains independent beside movement', () => equal(units([modernSource, modernDestination, independent]).map(unit => unit.kind), ['movement', 'change'], 'independent edit is not grouped'));
test('member change IDs are complete and ordered', () => equal(units([modernSource, modernDestination])[0].memberChangeIds, ['assignment|norte|G-10', 'assignment|norte|H-05'], 'all raw members are retained'));
test('selected movement flattens every member', () => equal(helpers.flattenSelectedCompareUnits(units([modernSource, modernDestination]), ['movement|move-a']), ['assignment|norte|G-10', 'assignment|norte|H-05'], 'whole movement expands'));
test('unselected movement flattens to zero IDs', () => equal(helpers.flattenSelectedCompareUnits(units([modernSource, modernDestination]), []), [], 'unselected group is absent'));
test('select all operates on units', () => { const result = units([modernSource, modernDestination, independent]); const selected = result.map(unit => unit.unitId); equal(selected.length, 2, 'select all sees two business units'); });
test('deselect all clears units', () => equal(helpers.flattenSelectedCompareUnits(units([modernSource, modernDestination, independent]), []), [], 'no unit means no payload'));
test('visible counts are business-unit counts', () => equal(units([modernSource, modernDestination, legacySource, legacyDestination, independent]).length, 3, 'two movements and one edit'));
test('selected count is business-unit count', () => { const result = units([modernSource, modernDestination, legacySource, legacyDestination, independent]); const selected = new Set(['movement|move-a', 'change|assignment|norte|K-11']); equal([...selected].filter(id => result.some(unit => unit.unitId === id)).length, 2, 'two selected units'); });
test('flattening is deterministic', () => { const result = units([modernSource, modernDestination, independent]); const selected = [result[2 - 1].unitId, result[0].unitId]; equal(helpers.flattenSelectedCompareUnits(result, selected), ['assignment|norte|G-10', 'assignment|norte|H-05', 'assignment|norte|K-11'], 'unit order controls payload order'); });
test('flattening deduplicates raw member IDs deterministically', () => { const duplicate = { ...modernDestination, id: 'assignment|norte|H-05' }; equal(helpers.flattenSelectedCompareUnits(units([modernSource, modernDestination, duplicate]), ['movement|move-a']), ['assignment|norte|G-10', 'assignment|norte|H-05'], 'duplicate ID is sent once'); });
test('mixed A+B+D selection excludes C completely', () => { const moveC = change('seat|sur|J-04', { operationId: 'move-c', atomic: true, type: 'movement', entityType: 'workspace', entityId: 'J-04', before: { personId: 'p-c' } }); const moveCDestination = change('assignment|sur|K-08', { operationId: 'move-c', atomic: true, type: 'movement', entityId: 'K-08', after: { personId: 'p-c' } }); const result = units([modernSource, modernDestination, legacySource, legacyDestination, moveC, moveCDestination, independent]); const payload = helpers.flattenSelectedCompareUnits(result, ['movement|move-a', 'movement|move-b', 'change|assignment|norte|K-11']); equal(payload, ['assignment|norte|G-10', 'assignment|norte|H-05', 'seat|sur|G-10', 'assignment|sur|H-05', 'assignment|norte|K-11'], 'A, B and D are complete while C contributes zero members'); });
test('unselected C contributes zero member IDs', () => { const result = units([change('seat|sur|J-04', { operationId: 'move-c', atomic: true, type: 'movement', entityType: 'workspace', entityId: 'J-04', before: { personId: 'p-c' } }), change('assignment|sur|K-08', { operationId: 'move-c', atomic: true, type: 'movement', entityId: 'K-08', after: { personId: 'p-c' } })]); equal(helpers.flattenSelectedCompareUnits(result, []), [], 'unselected C emits no raw ID'); });
test('remaining C reload remains one movement unit', () => { const result = units([change('seat|sur|J-04', { operationId: 'move-c', atomic: true, type: 'movement', entityType: 'workspace', entityId: 'J-04', before: { personId: 'p-c' } }), change('assignment|sur|K-08', { operationId: 'move-c', atomic: true, type: 'movement', entityId: 'K-08', after: { personId: 'p-c' } })]); equal(result.length, 1, 'remaining operation is whole'); });
test('validation impact aggregates, deduplicates and preserves categories', () => { const issue = { id: 'duplicate-person', severity: 'Critical' }; const result = units([change('seat|sur|G-10', { operationId: 'move-validation', atomic: true, type: 'movement', before: { personId: 'p' }, validationImpact: { introduced: [issue], persistent: [{ id: 'old', severity: 'Warning' }] } }), change('assignment|sur|H-05', { operationId: 'move-validation', atomic: true, type: 'movement', after: { personId: 'p' }, validationImpact: { introduced: [issue], resolved: [{ id: 'resolved', severity: 'Info' }] } })])[0].validationImpact; equal(result.introduced.length, 1, 'introduced issue is deduplicated'); equal(result.resolved.length, 1, 'resolved issue retained'); equal(result.persistent.length, 1, 'persistent issue retained'); });
test('ordering follows first operation occurrence', () => equal(units([independent, legacySource, legacyDestination, modernSource, modernDestination]).map(unit => unit.unitId), ['change|assignment|norte|K-11', 'movement|move-b', 'movement|move-a'], 'first raw occurrence determines order'));
test('same input produces same output', () => equal(units([modernSource, modernDestination, independent]), units([modernSource, modernDestination, independent]), 'deterministic result'));
test('input is not mutated', () => { const input = [modernSource, modernDestination]; const snapshot = JSON.stringify(input); const result = units(input); assert(Object.isFrozen(result) && Object.isFrozen(result[0]) && Object.isFrozen(result[0].memberChangeIds), 'output contract is frozen'); equal(JSON.stringify(input), snapshot, 'input snapshot stays unchanged'); });
test('duplicate workspace technical IDs across maps do not misgroup', () => { const north = change('seat|norte|shared-id', { operationId: 'move-norte', atomic: true, type: 'movement', entityType: 'workspace', entityId: 'shared-id', mapId: 'norte', before: { personId: 'north-person' } }); const south = change('seat|sur|shared-id', { operationId: 'move-sur', atomic: true, type: 'movement', entityType: 'workspace', entityId: 'shared-id', mapId: 'sur', before: { personId: 'south-person' } }); const result = units([north, south]); equal(result.map(unit => unit.unitId), ['movement|move-norte', 'movement|move-sur'], 'only operationId controls grouping'); equal(helpers.flattenSelectedCompareUnits(result, result.map(unit => unit.unitId)), ['seat|norte|shared-id', 'seat|sur|shared-id'], 'map-qualified member IDs remain distinct'); });

let passed = 0;
for (const { name, fn } of tests) {
  try { fn(); passed++; }
  catch (error) { console.error(`FAIL: ${name}: ${error.message}`); }
}
console.log(`Scenario compare units harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
