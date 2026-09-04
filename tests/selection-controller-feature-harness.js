'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { createSelectionControllerFeature } = require('../Resources/js/features/selection/selection-controller-feature.js');

function createHarness({ canActivate = true, selected = [] } = {}) {
  const classState = new Map();
  const attributes = new Map();
  const selectionButton = {
    classList: { toggle(name, value) { classState.set(name, Boolean(value)); } },
    setAttribute(name, value) { attributes.set(name, String(value)); },
    textContent: '',
    title: ''
  };
  let dialogClosed = 0;
  const elements = { 'selection-mode': selectionButton, 'bulk-dialog': { close() { dialogClosed++; } } };
  const state = {
    selectedWorkspaces: new Set(selected),
    selectionAnchor: 'anchor',
    bulk: { pendingAction: 'confirmed', inFlight: { active: true }, lastCommitted: { count: 2 }, undoRequested: true }
  };
  const ui = { selectionMode: false, seatId: selected.at(-1) || null, assignmentBaseline: { id: 'baseline' } };
  const events = { close: [], bulkRenders: 0, renders: 0 };
  const feature = createSelectionControllerFeature({
    state,
    ui,
    getElement: id => elements[id] || null,
    canActivateMode: () => canActivate,
    closeDetailPanel: options => events.close.push(options),
    renderBulkBar: () => { events.bulkRenders++; },
    render: () => { events.renders++; },
    deselectWorkspace: (ids, id) => ids.filter(value => value !== id)
  });
  return { attributes, classState, dialogClosed: () => dialogClosed, events, feature, selectionButton, state, ui };
}


test('activates selection mode and exposes its accessible visual state', () => {
  const { feature, ui, classState, attributes, selectionButton } = createHarness();
  assert.strictEqual(feature.setMode(true), true);
  assert.strictEqual(ui.selectionMode, true);
  assert.strictEqual(classState.get('active'), true);
  assert.strictEqual(attributes.get('aria-pressed'), 'true');
  assert.strictEqual(selectionButton.textContent, '✓ Seleccionando');
  assert.strictEqual(selectionButton.title, 'Finalizar selección sin limpiar puestos');
});

test('refuses activation when an incompatible interaction is active', () => {
  const { feature, ui, attributes, selectionButton } = createHarness({ canActivate: false });
  assert.strictEqual(feature.setMode(true), false);
  assert.strictEqual(ui.selectionMode, false);
  assert.strictEqual(attributes.get('aria-pressed'), 'false');
  assert.strictEqual(selectionButton.textContent, 'Seleccionar');
});

test('leaving selection mode does not clear selected workspaces', () => {
  const { feature, state } = createHarness({ selected: ['W1', 'W2'] });
  feature.setMode(true);
  feature.setMode(false);
  assert.deepStrictEqual([...state.selectedWorkspaces], ['W1', 'W2']);
});

test('marks bulk selection as changed without clearing it', () => {
  const { feature, state } = createHarness({ selected: ['W1'] });
  feature.markBulkSelectionChanged();
  assert.strictEqual(state.bulk.lastCommitted, null);
  assert.strictEqual(state.bulk.undoRequested, false);
  assert.deepStrictEqual([...state.selectedWorkspaces], ['W1']);
});

test('clears workspace selection through the existing explicit action contract', () => {
  const { feature, state, ui, events, dialogClosed } = createHarness({ selected: ['W1', 'W2'] });
  feature.clearWorkspaceSelection({ closeAreaFocus: true });
  assert.deepStrictEqual([...state.selectedWorkspaces], []);
  assert.strictEqual(state.selectionAnchor, null);
  assert.deepStrictEqual(state.bulk, { pendingAction: 'reserved', inFlight: null, lastCommitted: null, undoRequested: false });
  assert.strictEqual(ui.seatId, null);
  assert.strictEqual(dialogClosed(), 1);
  assert.deepStrictEqual(events.close, [{ render: false, preserveAreaFocus: false }]);
  assert.strictEqual(events.bulkRenders, 1);
  assert.strictEqual(events.renders, 1);
});

test('updates additive selection and closes the detail only when it becomes empty', () => {
  const { feature, state, ui, events } = createHarness({ selected: ['W1'] });
  feature.updateMultiSelection('W2', true);
  assert.deepStrictEqual([...state.selectedWorkspaces], ['W1', 'W2']);
  assert.strictEqual(ui.seatId, 'W2');
  feature.updateMultiSelection('W2', true);
  assert.deepStrictEqual([...state.selectedWorkspaces], ['W1']);
  feature.updateMultiSelection('W1', true);
  assert.deepStrictEqual([...state.selectedWorkspaces], []);
  assert.strictEqual(ui.assignmentBaseline, null);
  assert.deepStrictEqual(events.close.at(-1), { render: false });
});

test('deselects one workspace and retains the other selected workspaces', () => {
  const { feature, state, ui, events } = createHarness({ selected: ['W1', 'W2'] });
  feature.deselectSelectedWorkspace('W1');
  assert.deepStrictEqual([...state.selectedWorkspaces], ['W2']);
  assert.strictEqual(ui.seatId, 'W2');
  assert.strictEqual(events.bulkRenders, 1);
  assert.strictEqual(events.renders, 1);
});

test('exposes only selection controller responsibilities', () => {
  assert.deepStrictEqual(Object.keys(createHarness().feature).sort(), ['clearBulkSelection', 'clearWorkspaceSelection', 'deselectSelectedWorkspace', 'markBulkSelectionChanged', 'setMode', 'updateMultiSelection']);
});
