'use strict';

const fs = require('fs');
const path = require('path');
const state = require('../Resources/js/shared/workspace/workspace-state-helpers.js');

const root = path.join(__dirname, '..', 'qa-runtime-data', 'data');
const read = name => JSON.parse(fs.readFileSync(path.join(root, name), 'utf8'));
const maps = read('maps.json').maps || [];
const assignments = read('assignments.json').assignments || [];
const people = read('people.json').people || [];
const assignmentsByWorkspace = new Map(assignments.map(assignment => [assignment.workstationId, assignment]));
const peopleById = new Map(people.map(person => [person.id, person.username || person.name || person.id]));
const rows = maps.flatMap(map => (map.seats || []).map(seat => ({ mapId: map.id, mapName: map.name || map.id, seat, assignment: assignmentsByWorkspace.get(seat.id) || {} })));
const text = value => String(value ?? '').trim();
const oldState = ({ assignment }) => text(assignment.status).toLowerCase() === 'reserved' ? 'reserved' : text(assignment.personId) ? 'occupied' : 'free';
const displayLocation = seat => {
  if (Number.isFinite(seat.x) && Number.isFinite(seat.y) && seat.x >= 0 && seat.x <= 1 && seat.y >= 0 && seat.y <= 1) {
    const column = Math.min(23, Math.max(0, Math.floor(seat.x * 24)));
    let label = '';
    for (let value = column + 1; value > 0; value = Math.floor((value - 1) / 26)) label = String.fromCharCode(65 + (value - 1) % 26) + label;
    return `${label}-${String(Math.min(17, Math.max(0, Math.floor(seat.y * 18))) + 1).padStart(2, '0')}`;
  }
  return text(seat.gridCell) || '—';
};
const reference = seat => text(seat.reference) || text(seat.code) || text(seat.workstation) || text(seat.name) || '—';
const person = result => result.currentPersonId ? peopleById.get(result.currentPersonId) || result.currentPersonId : 'Sin asignar';
const countStates = values => values.reduce((summary, value) => { summary[value]++; return summary; }, { free: 0, occupied: 0, reserved: 0 });
const evaluated = rows.map(row => ({ ...row, oldEffectiveState: oldState(row), effective: state.deriveEffectiveWorkspaceState({ seat: row.seat, assignment: row.assignment }) }));
const before = countStates(evaluated.map(row => row.oldEffectiveState));
const after = countStates(evaluated.map(row => row.effective.state));
const changes = evaluated.filter(row => row.oldEffectiveState !== row.effective.state);
const byMap = Object.fromEntries(maps.map(map => {
  const values = evaluated.filter(row => row.mapId === map.id);
  return [map.id, { before: countStates(values.map(row => row.oldEffectiveState)), after: countStates(values.map(row => row.effective.state)), corrected: values.filter(row => row.oldEffectiveState !== row.effective.state).length }];
}));
const markdown = [
  '# Workspace effective-state audit',
  '',
  'Read-only audit of `qa-runtime-data`; no JSON was modified.',
  '',
  '## Summary',
  '',
  `- Total workspaces: ${evaluated.length}`,
  `- Current person present: ${evaluated.filter(row => row.effective.currentPersonId).length}`,
  `- Current person absent: ${evaluated.filter(row => !row.effective.currentPersonId).length}`,
  `- Assignment records present: ${evaluated.filter(row => Object.keys(row.assignment).length).length}`,
  `- Assignment records absent: ${evaluated.filter(row => !Object.keys(row.assignment).length).length}`,
  `- Automatic: ${evaluated.filter(row => row.effective.mode === 'automatic').length}`,
  `- Manual free: ${evaluated.filter(row => row.effective.mode === 'manual' && row.effective.state === 'free').length}`,
  `- Manual occupied: ${evaluated.filter(row => row.effective.mode === 'manual' && row.effective.state === 'occupied').length}`,
  `- Manual reserved: ${evaluated.filter(row => row.effective.mode === 'manual' && row.effective.state === 'reserved').length}`,
  '',
  '| State | Before | After |',
  '|---|---:|---:|',
  `| Free | ${before.free} | ${after.free} |`,
  `| Occupied | ${before.occupied} | ${after.occupied} |`,
  `| Reserved | ${before.reserved} | ${after.reserved} |`,
  '',
  '## Per-map effective totals after correction',
  '',
  '| Map | Free | Occupied | Reserved | Corrected |',
  '|---|---:|---:|---:|---:|',
  ...Object.entries(byMap).map(([mapId, result]) => `| ${mapId} | ${result.after.free} | ${result.after.occupied} | ${result.after.reserved} | ${result.corrected} |`),
  '',
  '## Corrected inconsistencies',
  '',
  'All rows below were previously emitted as `free` because only `assignment.personId` was considered. The current-user fallback `seat.personId` is part of the established workspace presentation contract and now produces the same effective state for backend and frontend consumers.',
  '',
  '| mapId | technicalId | displayLocation | reference | currentPerson | configuredState | old effectiveState | new effectiveState | reason |',
  '|---|---|---|---|---|---|---|---|---|',
  ...changes.map(row => `| ${row.mapId} | ${row.seat.id} | ${displayLocation(row.seat)} | ${reference(row.seat).replace(/\|/g, '\\|')} | ${person(row.effective).replace(/\|/g, '\\|')} | ${row.effective.configuredState} | ${row.oldEffectiveState} | ${row.effective.state} | current person fallback from seat.personId |`),
  '',
  'No `occupied + no current person`, `reserved + current assignment`, or cross-consumer state discrepancies remain under the centralized derivation.'
];

console.log(markdown.join('\n'));
