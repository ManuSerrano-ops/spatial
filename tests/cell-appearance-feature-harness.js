'use strict';

const assert = require('assert');
const { createCellAppearanceFeature } = require('../Resources/js/features/map/cell-appearance-feature.js');

function createStorage(values = {}, options = {}) {
  const saved = new Map(Object.entries(values));
  return {
    getItem(key) { if (options.failRead) throw new Error('read unavailable'); return saved.get(key) ?? null; },
    setItem(key, value) { if (options.failWrite) throw new Error('write unavailable'); saved.set(key, String(value)); },
    saved
  };
}

function createFeature({ state = { gridCellAppearances: {}, cellDetail: null }, storage = createStorage(), changed = () => {} } = {}) {
  return {
    state,
    storage,
    feature: createCellAppearanceFeature({
      cellMetadataHelpers: { cellIdentity: (mapId, cellId) => `${mapId}:${cellId}` },
      state,
      storage,
      onChanged: changed
    })
  };
}

const tests = [];
const test = (name, run) => tests.push({ name, run });

test('uses the historical compact appearance defaults', () => {
  const { feature } = createFeature();
  assert.deepStrictEqual(feature.appearanceForCell('M1', 'C1'), { style: 'compact', offsetX: 0, offsetY: 0 });
});

test('normalizes stored offsets without changing the style contract', () => {
  const { feature } = createFeature({ state: { gridCellAppearances: { 'M1:C1': { style: 'custom', offsetX: '1.5', offsetY: 'invalid' } }, cellDetail: null } });
  assert.deepStrictEqual(feature.appearanceForCell('M1', 'C1'), { style: 'compact', offsetX: 1.5, offsetY: 0 });
});

test('loads the existing local preference key', () => {
  const state = { gridCellAppearances: {}, cellDetail: null };
  const { feature } = createFeature({ state, storage: createStorage({ 'plano.gridCellAppearances': '{"M1:C1":{"offsetX":2}}' }) });
  assert.deepStrictEqual(feature.load(), { 'M1:C1': { offsetX: 2 } });
  assert.deepStrictEqual(state.gridCellAppearances, { 'M1:C1': { offsetX: 2 } });
});

test('falls back to an empty local presentation when storage is invalid', () => {
  const state = { gridCellAppearances: { stale: true }, cellDetail: null };
  const { feature } = createFeature({ state, storage: createStorage({}, { failRead: true }) });
  assert.deepStrictEqual(feature.load(), {});
});

test('updates, persists and re-renders only the requested cell presentation', () => {
  let renders = 0;
  const state = { gridCellAppearances: { 'M1:C2': { offsetX: 7 } }, cellDetail: null };
  const storage = createStorage();
  const { feature } = createFeature({ state, storage, changed: () => { renders++; } });
  feature.updateFor('M1', 'C1', { offsetX: 0.15, offsetY: -0.3 });
  assert.deepStrictEqual(state.gridCellAppearances['M1:C1'], { style: 'compact', offsetX: 0.15, offsetY: -0.3 });
  assert.deepStrictEqual(state.gridCellAppearances['M1:C2'], { offsetX: 7 });
  assert.deepStrictEqual(JSON.parse(storage.saved.get('plano.gridCellAppearances')), state.gridCellAppearances);
  assert.strictEqual(renders, 1);
});

test('updates the active detail cell and is inert without one', () => {
  let renders = 0;
  const state = { gridCellAppearances: {}, cellDetail: { mapId: 'M2', cellId: 'C9' } };
  const { feature } = createFeature({ state, changed: () => { renders++; } });
  feature.update({ offsetX: 1 });
  assert.strictEqual(state.gridCellAppearances['M2:C9'].offsetX, 1);
  state.cellDetail = null;
  feature.update({ offsetX: 2 });
  assert.strictEqual(renders, 1);
});

test('treats local storage writes as best effort', () => {
  const { feature } = createFeature({ storage: createStorage({}, { failWrite: true }) });
  assert.doesNotThrow(() => feature.updateFor('M1', 'C1', { offsetX: 1 }));
});

let passed = 0;
for (const item of tests) {
  try { item.run(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); }
}
console.log(`Cell appearance feature harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
