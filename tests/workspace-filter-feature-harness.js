'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { createWorkspaceFilterFeature } = require('../Resources/js/features/filters/workspace-filter-feature.js');

function createFeature(filters = {}) {
  const state = { filters: { quick: 'all', zone: '', person: '', device: '', roseta: '', only: false, ...filters } };
  const people = () => [{ id: 'P1', name: 'Ana Garcia' }, { id: 'P2', name: 'Luis Mora' }];
  const devices = () => [{ id: 'D1', name: 'Portatil', model: 'Pro 14' }];
  const nameFor = (items, id) => items.find(item => item.id === id)?.name || '';
  const stateFor = workspace => workspace.state;
  const completenessFor = workspace => workspace.completeness;
  const valuesFor = workspace => workspace.values;
  return createWorkspaceFilterFeature({ state, stateFor, completenessFor, valuesFor, people, devices, nameFor });
}

const workspace = {
  _mapId: 'M1',
  state: 'occupied',
  completeness: 'complete',
  values: { personId: 'P1', deviceId: 'D1', roseta: 'R-09' }
};



test('accepts a workspace when no filters are active', () => {
  assert.strictEqual(createFeature().matches(workspace), true);
});

test('applies every quick filter using effective state and completeness', () => {
  assert.strictEqual(createFeature({ quick: 'occupied' }).matches(workspace), true);
  assert.strictEqual(createFeature({ quick: 'free' }).matches(workspace), false);
  assert.strictEqual(createFeature({ quick: 'partial' }).matches(workspace), false);
  assert.strictEqual(createFeature({ quick: 'partial' }).matches({ ...workspace, completeness: 'incomplete' }), true);
});

test('filters by map zone', () => {
  assert.strictEqual(createFeature({ zone: 'M1' }).matches(workspace), true);
  assert.strictEqual(createFeature({ zone: 'M2' }).matches(workspace), false);
});

test('finds the current person case-insensitively', () => {
  assert.strictEqual(createFeature({ person: 'ana' }).matches(workspace), true);
  assert.strictEqual(createFeature({ person: 'luis' }).matches(workspace), false);
});

test('finds device name and metadata case-insensitively', () => {
  assert.strictEqual(createFeature({ device: 'portatil' }).matches(workspace), true);
  assert.strictEqual(createFeature({ device: 'pro 14' }).matches(workspace), true);
  assert.strictEqual(createFeature({ device: 'monitor' }).matches(workspace), false);
});

test('filters by network outlet and combines criteria with AND', () => {
  assert.strictEqual(createFeature({ roseta: 'r-09', person: 'ana', zone: 'M1' }).matches(workspace), true);
  assert.strictEqual(createFeature({ roseta: 'r-09', person: 'luis' }).matches(workspace), false);
});

test('exposes only the deterministic matching contract', () => {
  assert.deepStrictEqual(Object.keys(createFeature()), ['matches']);
});
