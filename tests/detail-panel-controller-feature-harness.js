'use strict';

const assert = require('assert');
const { createDetailPanelControllerFeature } = require('../Resources/js/shared/ui/detail-panel-controller-feature.js');

function createElement() {
  const classes = new Set();
  return {
    dataset: {},
    textContent: '',
    removedAttributes: [],
    classList: {
      add(...values) { values.forEach(value => classes.add(value)); },
      remove(...values) { values.forEach(value => classes.delete(value)); },
      toggle(value, enabled) { if (enabled) classes.add(value); else classes.delete(value); },
      contains(value) { return classes.has(value); }
    },
    removeAttribute(name) { this.removedAttributes.push(name); delete this.dataset[name]; },
    classes
  };
}

function createHarness() {
  const elements = Object.fromEntries(['detail-panel', 'seat-kicker', 'title', 'detail', 'inspector-detail', 'selection-review', 'cell-detail', 'area-detail'].map(id => [id, createElement()]));
  const state = { selectedWorkspaces: new Set(['W1', 'W2']), cellDetail: { mapId: 'M1' }, areaDetail: { areaId: 'A1' }, activeClusterFocus: { areaId: 'A1' }, activeAreaFocus: { areaId: 'A1' } };
  const ui = { seatId: 'W1', assignmentBaseline: { id: 'baseline' } };
  let renders = 0;
  const feature = createDetailPanelControllerFeature({
    state,
    ui,
    getElement: id => elements[id],
    headerFor: (mode, values) => ({ mode, kicker: values.kicker ?? 'KICKER', title: values.title ?? 'TITLE', summary: values.summary ?? 'SUMMARY' }),
    deriveClosedDetailState: source => ({ selectedWorkspace: null, selectedWorkspaces: source.selectedWorkspaces, cellDetail: null, activeAreaFocus: null, mode: null }),
    render: () => { renders++; }
  });
  return { elements, feature, renders: () => renders, state, ui };
}

const tests = [];
const test = (name, run) => tests.push({ name, run });

test('writes the detail header through the normalized panel contract', () => {
  const { feature, elements } = createHarness();
  const header = feature.setHeader('inspector', { kicker: 'UBICACION', title: 'Ana', summary: 'Ocupado' });
  assert.deepStrictEqual(header, { mode: 'inspector', kicker: 'UBICACION', title: 'Ana', summary: 'Ocupado' });
  assert.strictEqual(elements['seat-kicker'].textContent, 'UBICACION');
  assert.strictEqual(elements.title.textContent, 'Ana');
  assert.strictEqual(elements.detail.textContent, 'Ocupado');
});

test('shows only the requested panel mode and marks the panel', () => {
  const { feature, elements } = createHarness();
  feature.show('selection-review', { title: 'Puestos seleccionados' });
  assert.strictEqual(elements['detail-panel'].dataset.mode, 'selection-review');
  assert.strictEqual(elements['detail-panel'].classList.contains('selection-review-mode'), true);
  assert.strictEqual(elements['detail-panel'].classList.contains('hidden'), false);
  assert.strictEqual(elements['selection-review'].classList.contains('hidden'), false);
  assert.strictEqual(elements['inspector-detail'].classList.contains('hidden'), true);
  assert.strictEqual(elements['cell-detail'].classList.contains('hidden'), true);
});

test('maps inspector mode to the inspector detail target', () => {
  const { feature, elements } = createHarness();
  feature.show('inspector');
  assert.strictEqual(elements['inspector-detail'].classList.contains('hidden'), false);
  assert.strictEqual(elements['selection-review'].classList.contains('hidden'), true);
});

test('closes details, preserves selected workspaces and resets panel state', () => {
  const { feature, elements, state, ui, renders } = createHarness();
  feature.show('area-detail');
  feature.close();
  assert.deepStrictEqual([...state.selectedWorkspaces], ['W1', 'W2']);
  assert.strictEqual(ui.seatId, null);
  assert.strictEqual(ui.assignmentBaseline, null);
  assert.strictEqual(state.cellDetail, null);
  assert.strictEqual(state.areaDetail, null);
  assert.strictEqual(state.activeClusterFocus, null);
  assert.strictEqual(state.activeAreaFocus, null);
  assert.strictEqual(elements['detail-panel'].classList.contains('hidden'), true);
  assert.strictEqual(elements['detail-panel'].removedAttributes.includes('data-mode'), true);
  assert.strictEqual(renders(), 1);
});

test('preserves Area Focus and skips render for a silent close', () => {
  const { feature, state, renders } = createHarness();
  const focus = state.activeAreaFocus;
  feature.close({ preserveAreaFocus: true, render: false });
  assert.strictEqual(state.activeAreaFocus, focus);
  assert.strictEqual(renders(), 0);
});

test('exposes only common panel behavior', () => {
  assert.deepStrictEqual(Object.keys(createHarness().feature).sort(), ['close', 'setHeader', 'show']);
});

let passed = 0;
for (const item of tests) {
  try { item.run(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); }
}
console.log(`Detail panel controller feature harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
