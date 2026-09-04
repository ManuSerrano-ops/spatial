'use strict';
const helpers = require('../Resources/js/shared/validation/validation-helpers.js');
const test = require('node:test');
const assert = require('node:assert/strict');
function equal(actual, expected, message) { assert(JSON.stringify(actual) === JSON.stringify(expected), `${message}; expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`); }

function run(maps, assignments) {
  const seats = maps.flatMap(map => map.seats.map(seat => ({ ...seat, mapId: map.id })));
  const ids = new Set(seats.map(seat => seat.id));
  const out = [];
  const add = (ruleId, severity, entityType, entityId, mapId, field, title, message, relatedEntities = [], suggestedAction = 'Revisar manualmente.') => out.push({ id: `${ruleId}|${entityId}|${relatedEntities.join(',')}`, ruleId, severity, entityType, entityId, mapId, field, title, message, relatedEntities, suggestedAction });
  for (const [field, ruleId, severity, title] of [['roseta', 'duplicate-network-outlet', 'Critical', 'Roseta duplicada'], ['deviceId', 'duplicate-device', 'Critical', 'Equipo duplicado'], ['personId', 'duplicate-person', 'Warning', 'Persona duplicada']]) {
    const groups = assignments.filter(item => item[field]).reduce((all, assignment) => { const key = String(assignment[field]).trim().toLowerCase(); (all[key] ||= []).push(assignment); return all; }, {});
    Object.values(groups).filter(group => new Set(group.map(item => item.workstationId)).size > 1).forEach(group => { const relatedEntities = group.map(item => item.workstationId).sort(); const entityId = String(group[0][field]).trim(); add(ruleId, severity, 'assignment', entityId, seats.find(seat => seat.id === relatedEntities[0])?.mapId ?? null, field, title, `${field} asignado a varios puestos.`, relatedEntities); });
  }
  assignments.filter(item => !ids.has(item.workstationId)).forEach(item => add('assignment-missing-workspace', 'Critical', 'assignment', item.workstationId, null, 'workstationId', 'Asignación sin puesto', 'La asignación referencia un puesto inexistente.'));
  seats.filter(seat => seat.type === 'occupied' && !assignments.some(item => item.workstationId === seat.id)).forEach(seat => add('historical-occupied-without-assignment', 'Info', 'workspace', seat.id, seat.mapId, 'type', 'Marca histórica sin asignación', 'El dibujo heredado indica ocupado sin asignación vigente.'));
  seats.filter(seat => typeof seat.x !== 'number' || typeof seat.y !== 'number' || seat.x < 0 || seat.x > 1 || seat.y < 0 || seat.y > 1).forEach(seat => add('invalid-coordinate', 'Critical', 'workspace', seat.id, seat.mapId, 'x/y', 'Coordenada inválida', 'La coordenada no está normalizada entre 0 y 1.'));
  return out.sort((a, b) => helpers.severityRank[b.severity] - helpers.severityRank[a.severity] || a.ruleId.localeCompare(b.ruleId) || String(a.mapId).localeCompare(String(b.mapId)) || a.entityId.localeCompare(b.entityId));
}

const validMaps = () => [{ id: 'norte', seats: [{ id: 'A', x: 0, y: 1, type: 'free' }, { id: 'B', x: 1, y: 0, type: 'free' }] }, { id: 'sur', seats: [{ id: 'C', x: .5, y: .5, type: 'free' }] }];
const duplicateAssignments = () => [{ workstationId: 'A', roseta: 'R1', deviceId: 'D1', personId: 'P1' }, { workstationId: 'B', roseta: 'R1', deviceId: 'D1', personId: 'P1' }];

test('valid dataset', () => equal(run(validMaps(), []), [], 'valid data must not report results'));
test('duplicate outlet consolidated', () => { const result = run(validMaps(), duplicateAssignments()).find(item => item.ruleId === 'duplicate-network-outlet'); assert(result && result.severity === 'Critical', 'duplicate roseta must be critical'); equal(result.relatedEntities, ['A', 'B'], 'duplicate roseta has both related workspaces'); });
test('duplicate device', () => assert(run(validMaps(), duplicateAssignments()).find(item => item.ruleId === 'duplicate-device')?.severity === 'Critical', 'device must be critical'));
test('duplicate person warning', () => assert(run(validMaps(), duplicateAssignments()).find(item => item.ruleId === 'duplicate-person')?.severity === 'Warning', 'person must be warning'));
test('missing workspace', () => assert(run(validMaps(), [{ workstationId: 'X' }]).some(item => item.ruleId === 'assignment-missing-workspace'), 'broken workspace reference must be reported'));
test('historical marker and coordinates', () => { const results = run([{ id: 'norte', seats: [{ id: 'A', x: -1, y: 2, type: 'occupied' }] }], []); assert(results.some(item => item.ruleId === 'invalid-coordinate') && results.some(item => item.ruleId === 'historical-occupied-without-assignment'), 'both audited rules must be reported'); });
test('coordinate boundaries', () => equal(run([{ id: 'norte', seats: [{ id: 'A', x: 0, y: 1 }, { id: 'B', x: .2, y: .8 }] }], []), [], 'normalized boundaries must be valid'));
test('determinism', () => equal(run(validMaps(), duplicateAssignments()), run(validMaps(), duplicateAssignments()), 'same input must retain IDs and order'));
test('reality has no scenario conflict', () => assert(!run(validMaps(), [{ workstationId: 'A', roseta: 'R17' }, { workstationId: 'B', roseta: 'R22' }]).some(item => item.ruleId === 'duplicate-network-outlet'), 'reality stays valid'));
test('scenario effective state detects conflict', () => assert(run(validMaps(), [{ workstationId: 'A', roseta: 'R17' }, { workstationId: 'B', roseta: 'R17' }]).filter(item => item.ruleId === 'duplicate-network-outlet').length === 1, 'scenario effective state reports one conflict'));
test('scenario correction removes conflict', () => assert(!run(validMaps(), [{ workstationId: 'A', roseta: 'R17' }, { workstationId: 'B', roseta: 'R23' }]).some(item => item.ruleId === 'duplicate-network-outlet'), 'corrected scenario has no conflict'));
test('scenario determinism', () => { const state = [{ workstationId: 'A', deviceId: 'D17' }, { workstationId: 'B', deviceId: 'D17' }]; equal(run(validMaps(), state), run(validMaps(), state), 'scenario preserves deterministic results'); });
test('bridge result contract', () => { const result = run(validMaps(), duplicateAssignments())[0]; ['id', 'ruleId', 'severity', 'entityType', 'entityId', 'mapId', 'field', 'title', 'message', 'relatedEntities', 'suggestedAction'].forEach(key => assert(Object.hasOwn(result, key), `missing ${key}`)); });
const helperResults = [
  { id: 'a', severity: 'Critical', ruleId: 'r1', entityType: 'workspace', entityId: 'A', mapId: 'norte', relatedEntities: [] },
  { id: 'b', severity: 'Critical', ruleId: 'r1', entityType: 'workspace', entityId: 'B', mapId: 'norte', relatedEntities: [] },
  { id: 'c', severity: 'Warning', ruleId: 'r2', entityType: 'assignment', entityId: 'X', mapId: 'sur', relatedEntities: ['A'] },
  { id: 'd', severity: 'Warning', ruleId: 'r2', entityType: 'assignment', entityId: 'Y', mapId: 'sur', relatedEntities: ['B'] },
  { id: 'e', severity: 'Warning', ruleId: 'r3', entityType: 'workspace', entityId: 'C', mapId: 'sur', relatedEntities: [] },
  { id: 'f', severity: 'Info', ruleId: 'r4', entityType: 'workspace', entityId: 'D', mapId: 'id', relatedEntities: [] }
];
test('summary helper', () => equal(helpers.getValidationSummary(helperResults), { total: 6, critical: 2, warning: 3, info: 1 }, 'summary groups severity'));
test('workspace max severity', () => { const index = helpers.buildProblemsByWorkspace(helperResults); assert(helpers.getWorkspaceMaxSeverity('A', index) === 'Critical', 'critical outranks warning'); assert(helpers.getWorkspaceMaxSeverity('missing', index) === 'None', 'missing workspace has no severity'); });
test('map filtering and grouping', () => { assert(helpers.getProblemsForMap(helperResults, 'sur').length === 3, 'map filter uses map ID'); assert(helpers.groupProblemsByRule(helperResults).get('r2').length === 2, 'rule grouping retains members'); });
test('problem text filtering', () => { const result = { ...helperResults[2], title: 'Equipo duplicado', message: 'Riesgo en A', details: 'Duplicado' }; assert(helpers.problemMatches(result, { text: 'equipo' }), 'search checks title'); assert(helpers.problemMatches(result, { text: 'a' }), 'search checks related entities'); assert(!helpers.problemMatches(result, { mapId: 'norte' }), 'map filter rejects another map'); });
test('operational policy excludes historical diagnostics from every helper projection', () => {
  const historical = { id: 'history', severity: 'Info', ruleId: 'historical-occupied-without-assignment', classification: 'Historical', operational: false, entityType: 'workspace', entityId: 'H-01', mapId: 'norte', relatedEntities: [] };
  const active = { id: 'active', severity: 'Warning', ruleId: 'duplicate-person', classification: 'Operational', operational: true, entityType: 'workspace', entityId: 'A-01', mapId: 'norte', relatedEntities: [] };
  equal(helpers.operationalResults([historical, active]).map(item => item.id), ['active'], 'central policy retains only operational results');
  equal(helpers.getValidationSummary([historical, active]), { total: 1, critical: 0, warning: 1, info: 0 }, 'summary excludes historical diagnostics');
  assert(!helpers.buildProblemsByWorkspace([historical, active]).has('H-01'), 'workspace index excludes historical diagnostics');
  equal(helpers.getProblemsForMap([historical, active], 'norte').map(item => item.id), ['active'], 'by-map problems exclude historical diagnostics');
  assert(!helpers.problemMatches(historical, {}), 'Problems Center matching excludes historical diagnostics');
});
