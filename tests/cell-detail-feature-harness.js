'use strict';

const assert = require('assert');
const { createCellDetailFeature } = require('../Resources/js/features/map/cell-detail-feature.js');

function createElement() {
  const classes = new Set();
  return {
    dataset: {}, value: '', disabled: false, textContent: '', innerHTML: '', children: [], className: '', attributes: new Map(),
    classList: { add(...items) { items.forEach(item => classes.add(item)); }, remove(...items) { items.forEach(item => classes.delete(item)); }, contains(item) { return classes.has(item); } },
    setAttribute(name, value) { this.attributes.set(name, String(value)); },
    append(...items) { this.children.push(...items); },
    replaceChildren(...items) { this.children = items; },
    classes
  };
}

function createHarness({ cellDetail = { mapId: 'M1', cellId: 'C1' }, selected = ['W1'], destinationMode = false } = {}) {
  const elements = Object.fromEntries(['detail-panel', 'cell-detail', 'cell-detail-name', 'cell-detail-clear-name', 'cell-detail-select-all', 'cell-detail-counts', 'cell-detail-list'].map(id => [id, createElement()]));
  const state = { cellDetail, selectedWorkspaces: new Set(selected) };
  const ui = { touchedSeats: new Set(['W2']), changes: [{ seatId: 'W3' }] };
  const shown = [];
  const cell = {
    mapId: 'M1', cellId: 'C1', customName: 'Equipo Norte',
    composition: { total: 2, occupied: 1, free: 1, reserved: 0, problems: 1 },
    members: [{ id: 'W1' }, { id: 'W2' }]
  };
  const feature = createCellDetailFeature({
    state, ui, getElement: id => elements[id], document: { createElement },
    mapCells: mapId => mapId === 'M1' ? [cell] : [],
    showDetailMode: (mode, values) => shown.push({ mode, values }),
    workspacePresentation: workspace => ({ displayLocation: workspace.id === 'W1' ? 'A-01' : 'A-02', currentPerson: workspace.id === 'W1' ? 'Ana' : '', assignmentStatusLabel: workspace.id === 'W1' ? 'Ocupado' : 'Libre', equipment: workspace.id === 'W1' ? 'Portatil' : '', networkOutlet: 'R-01', workstationReference: workspace.id }),
    plannerState: () => ({ destinationMode }), plannerAvailability: id => id === 'W2' ? 'available' : 'unavailable',
    getWorkspaceMaxSeverity: id => id === 'W2' ? 'Warning' : 'None', severityLabel: severity => severity === 'Warning' ? 'Advertencia' : severity,
    escapeHtml: value => String(value).replace(/</g, '&lt;')
  });
  return { cell, elements, feature, shown, state, ui };
}

const tests = [];
const test = (name, run) => tests.push({ name, run });

test('hides the cell section and clears stale state when the cell no longer exists', () => {
  const { feature, elements, state } = createHarness({ cellDetail: { mapId: 'missing', cellId: 'C1' } });
  feature.render();
  assert.strictEqual(state.cellDetail, null);
  assert.strictEqual(elements['cell-detail'].classList.contains('hidden'), true);
  assert.strictEqual(elements['detail-panel'].classList.contains('cell-detail-mode'), false);
});

test('sets the current cell and opens the common cell detail mode', () => {
  const { feature, state, shown } = createHarness();
  feature.render();
  assert.deepStrictEqual(state.cellDetail, { mapId: 'M1', cellId: 'C1' });
  assert.deepStrictEqual(shown, [{ mode: 'cell-detail', values: { title: 'Equipo Norte', summary: 'C1 · 2 puestos' } }]);
});

test('renders cell metadata, counts and member rows', () => {
  const { feature, elements } = createHarness();
  feature.render();
  assert.strictEqual(elements['cell-detail-name'].value, 'Equipo Norte');
  assert.strictEqual(elements['cell-detail-clear-name'].disabled, false);
  assert.strictEqual(elements['cell-detail-select-all'].textContent, 'Seleccionar zona');
  assert.strictEqual(elements['cell-detail-counts'].children.length, 5);
  assert.strictEqual(elements['cell-detail-list'].children.length, 2);
  assert.strictEqual(elements['cell-detail-list'].children[0].children[1].textContent, 'Quitar de selección');
  assert.strictEqual(elements['cell-detail-list'].children[1].children[1].textContent, 'Añadir a selección');
});

test('marks planner availability, problems and scenario changes in member content', () => {
  const { feature, elements } = createHarness({ destinationMode: true });
  feature.render();
  const first = elements['cell-detail-list'].children[0].children[0].innerHTML;
  const second = elements['cell-detail-list'].children[1].children[0].innerHTML;
  assert(first.includes('× No disponible'));
  assert(second.includes('✓ Destino disponible'));
  assert(second.includes('Advertencia'));
  assert(second.includes('Cambio de escenario'));
});

test('renders a generic cell title when no custom name exists', () => {
  const { feature, cell, shown } = createHarness();
  cell.customName = '';
  feature.render(cell);
  assert.deepStrictEqual(shown[0], { mode: 'cell-detail', values: { title: 'C1', summary: '2 puestos en esta celda' } });
});

test('exposes only cell detail rendering', () => {
  assert.deepStrictEqual(Object.keys(createHarness().feature), ['render']);
});

let passed = 0;
for (const item of tests) {
  try { item.run(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); }
}
console.log(`Cell detail feature harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
