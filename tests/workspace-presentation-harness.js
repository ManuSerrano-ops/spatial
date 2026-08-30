'use strict';

const fs = require('fs');
const path = require('path');
const { buildWorkspacePresentation } = require('../Resources/js/shared/workspace/workspace-presentation-helpers.js');

const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const present = input => buildWorkspacePresentation({ displayLocation: 'G-05', ...input });

test('assigned user and reference are separate', () => { const value = present({ seat: { name: 'Referencia histórica' }, assignment: { personId: 'ana' }, personName: 'Ana Pérez' }); assert(value.currentPerson === 'Ana Pérez' && value.workstationReference === 'Referencia histórica', 'user and reference conflated'); });
test('human-looking reference without user remains reference', () => { const value = present({ seat: { name: 'Nombre heredado' }, assignment: {} }); assert(value.currentPerson === null && value.workstationReference === 'Nombre heredado', 'reference inferred as person'); });
test('missing user and reference are explicit', () => { const value = present({ seat: {}, assignment: {} }); assert(value.currentPerson === null && value.workstationReference === '', 'missing values not explicit'); });
test('historical marker without assignment is free and does not become current user', () => { const value = present({ seat: { name: 'Etiqueta histórica', state: 'occupied', type: 'occupied' }, assignment: {} }); assert(value.currentPerson === null && value.assignmentStatus === 'free', 'historical drawing state became operational occupancy'); });
test('current assignment overrides historical label for person display', () => { const value = present({ seat: { name: 'Etiqueta histórica', personId: 'old' }, assignment: { personId: 'current' }, personName: 'Usuario actual' }); assert(value.currentPersonId === 'current' && value.currentPerson === 'Usuario actual', 'assignment did not override historical person'); });
test('tooltip semantic source includes location, user and reference', () => { const value = present({ seat: { name: 'Ref A' }, assignment: { personId: 'ana' }, personName: 'Ana' }); assert(value.title === 'G-05 · Ref. Ref A · Ana', 'tooltip title contract invalid'); });
test('inspector semantic source has labelled reference and user', () => { const value = present({ seat: { name: 'Ref A' }, assignment: {} }); assert(value.workstationReference === 'Ref A' && value.currentPerson === null, 'inspector source invalid'); });
test('search semantic source keeps reference separate', () => { const value = present({ seat: { name: 'Ref A' }, assignment: {} }); assert(value.displayLocation === 'G-05' && value.workstationReference === 'Ref A', 'search source invalid'); });
test('list semantic source has distinct person/reference values', () => { const value = present({ seat: { name: 'Ref A' }, assignment: { personId: 'ana' }, personName: 'Ana' }); assert(value.workstationReference !== value.currentPerson, 'list values conflated'); });
test('planner semantic source uses only current user', () => { const value = present({ seat: { name: 'Nombre heredado' }, assignment: {} }); assert(value.currentPerson === null, 'planner would infer historical user'); });
test('scenario semantic source retains field distinction', () => { const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8'); assert(app.includes("personId: 'Usuario'") && app.includes("seatName: 'Referencia del puesto'"), 'scenario labels do not distinguish person/reference'); });
test('problem navigation uses map plus technical workspace identity', () => { const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8'); assert(app.includes('workspaceByIdentity(mapId, workspaceId)') && app.includes('seat._mapId === mapId'), 'map-qualified navigation missing'); });
test('problem selection routes to workspace navigation', () => { const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8'); assert(app.includes("navigateToWorkspace({ workspaceId: target.id, mapId: target.seat._mapId, highlight: 'problem' })"), 'problem selection does not navigate target workspace'); });
test('display location is not a primary key', () => { const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8'); assert(app.includes('workspaceByIdentity(mapId, workspaceId)') && !app.includes('find(seat => displayLocationFor(seat) === workspaceId)'), 'display location used as identity'); });
test('aria begins with display location', () => { const value = present({ displayLocation: 'A-01', seat: { name: 'Ref A' }, assignment: { personId: 'ana' }, personName: 'Ana' }); assert(value.ariaLabel.startsWith('Puesto A-01, Ocupado, Ana'), 'aria primary identity invalid'); });
test('output is deterministic', () => { const input = { seat: { name: 'Ref A' }, assignment: { personId: 'ana' }, personName: 'Ana', displayLocation: 'B-04' }; assert(JSON.stringify(buildWorkspacePresentation(input)) === JSON.stringify(buildWorkspacePresentation(input)), 'output is nondeterministic'); });
test('input is not mutated', () => { const input = { seat: { name: 'Ref A', nested: { keep: true } }, assignment: { personId: 'ana' } }; const before = JSON.stringify(input); buildWorkspacePresentation(input); assert(JSON.stringify(input) === before, 'input was mutated'); });

let passed = 0;
for (const { name, fn } of tests) {
  try { fn(); passed++; }
  catch (error) { console.error(`FAIL: ${name}: ${error.message}`); }
}
console.log(`workspace-presentation-harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
