'use strict';
const helpers = require('../Resources/js/features/movement-planner/movement-planner-helpers.js');
const test = require('node:test');
const equal = (actual, expected, message) => { if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`); };
const locations = { 'N-01': { displayLocation: 'B-12' }, 'N-02': { displayLocation: 'F-08' }, 'N-03': { displayLocation: 'B-13' }, 'N-04': { displayLocation: 'D-08' } };

test('initial state', () => equal(helpers.createPlannerState().status, 'idle', 'Planner starts idle'));
test('source classification', () => equal(helpers.classifySources(['N-02', 'N-01', 'N-04'], { 'N-01': { workstationId: 'N-01' }, 'N-04': { workstationId: 'N-04' } }, locations), { movable: ['N-01', 'N-04'], unavailable: [{ workspaceId: 'N-02', code: 'source-unassigned', message: 'El puesto no tiene una asignación para mover.' }] }, 'Only assigned sources are movable'));
test('effective source eligibility distinguishes free, modern, safe legacy and blocked legacy workspaces', () => equal(helpers.classifyEffectiveSources(['N-01', 'N-02', 'N-03', 'N-04', 'N-05', 'N-06'], {
  'N-01': { effectiveState: 'free', assignment: {} },
  'N-02': { effectiveState: 'occupied', assignment: { workstationId: 'N-02', personId: 'p' } },
  'N-03': { effectiveState: 'occupied', assignment: {}, legacyPersonId: 'legacy-safe', legacyPersonResolved: true, legacyDeviceResolved: true },
  'N-04': { effectiveState: 'reserved', assignment: { workstationId: 'N-04', status: 'reserved' } },
  'N-05': { effectiveState: 'occupied', assignment: {}, legacyPersonId: 'legacy-missing', legacyPersonResolved: false, legacyDeviceResolved: true },
  'N-06': { effectiveState: 'occupied', assignment: {}, legacyPersonId: 'legacy-device', legacyPersonResolved: true, legacyDeviceResolved: false }
}, locations), { movable: ['N-03', 'N-02'], unavailable: [
  { workspaceId: 'N-01', code: 'source-free', message: 'Puesto libre.' },
  { workspaceId: 'N-04', code: 'source-reserved', message: 'Puesto reservado.' },
  { workspaceId: 'N-05', code: 'source-person-unresolved', message: 'No se puede determinar el ocupante.' },
  { workspaceId: 'N-06', code: 'source-device-unresolved', message: 'No se puede determinar el equipo.' }
] }, 'effective eligibility matches exact backend legacy constraints'));
test('deterministic source-destination pairing', () => equal(helpers.buildPairs(['N-04', 'N-01'], ['N-02', 'N-03'], [], locations).pairs, [{ sourceWorkspaceId: 'N-01', destinationWorkspaceId: 'N-03' }, { sourceWorkspaceId: 'N-04', destinationWorkspaceId: 'N-02' }], 'Pairs use display location ordering while retaining technical IDs'));
test('fewer destinations leave sources unassigned', () => equal(helpers.buildPairs(['N-01', 'N-04'], ['N-02'], [], locations).unassigned, ['N-04'], 'Unpaired source is explicit'));
test('exclude is not a data mutation', () => { const result = helpers.buildPairs(['N-01', 'N-04'], ['N-02'], ['N-04'], locations); equal(result.pairs, [{ sourceWorkspaceId: 'N-01', destinationWorkspaceId: 'N-02' }], 'Excluded source is absent from request'); equal(result.excluded, ['N-04'], 'Excluded source remains represented in state'); });
test('manual override changes only one pair', () => equal(helpers.overridePair([{ sourceWorkspaceId: 'N-01', destinationWorkspaceId: 'N-02' }, { sourceWorkspaceId: 'N-04', destinationWorkspaceId: 'N-03' }], 'N-04', 'N-02'), [{ sourceWorkspaceId: 'N-01', destinationWorkspaceId: 'N-02' }, { sourceWorkspaceId: 'N-04', destinationWorkspaceId: 'N-02' }], 'Override preserves source technical IDs'));
test('review summary', () => equal(helpers.reviewSummary({ proposals: [{ relatedProblems: [{ severity: 'Warning' }] }, { relatedProblems: [{ severity: 'Critical' }, { severity: 'Info' }] }], issues: [{}] }, ['N-04'], ['N-05']), { planned: 2, blocked: 1, unassigned: 1, excluded: 1, critical: 1, warning: 1, info: 1 }, 'Review counts derive from bridge result'));
test('creation request serialization', () => equal(helpers.serializeCreationRequest(' Plan de movimiento ', [{ sourceWorkspaceId: 'N-01', destinationWorkspaceId: 'N-02' }]), { name: 'Plan de movimiento', requests: [{ sourceWorkspaceId: 'N-01', destinationWorkspaceId: 'N-02' }] }, 'Creation request keeps technical IDs'));
test('display location is presentation order only', () => { const pair = helpers.buildPairs(['N-01'], ['N-02'], [], locations).pairs[0]; equal(pair, { sourceWorkspaceId: 'N-01', destinationWorkspaceId: 'N-02' }, 'Technical identity is never replaced by display location'); });
test('state vocabulary is bounded', () => { ['idle', 'selectingSources', 'selectingDestinations', 'planning', 'review', 'creatingScenario', 'error'].forEach(state => { if (!helpers.states.has(state)) throw new Error(`Missing ${state}`); }); });
