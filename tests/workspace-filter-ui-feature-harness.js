'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { createWorkspaceFilterUiFeature } = require('../Resources/js/features/filters/workspace-filter-ui-feature.js');

function createButton() {
  return { textContent: '', onclick: null };
}

function createHarness({ filters = {}, loaded = true, workspaces = [] } = {}) {
  const elements = {
    'filter-chips': { children: [], replaceChildren(...children) { this.children = children; } },
    'filter-count': { textContent: '' },
    'filter-zone': { value: '' },
    'filter-person': { value: '' },
    'filter-device': { value: '' },
    'filter-roseta': { value: '' },
    'filter-only': { checked: false }
  };
  const state = { filters: { quick: '', zone: '', person: '', device: '', roseta: '', only: false, ...filters } };
  let renders = 0;
  const feature = createWorkspaceFilterUiFeature({
    state,
    hasLoaded: () => loaded,
    allWorkspaces: () => workspaces,
    matches: workspace => workspace.visible,
    maps: () => [{ id: 'M1', name: 'Norte' }],
    getElement: id => elements[id] || null,
    document: { createElement: tag => { assert.strictEqual(tag, 'button'); return createButton(); } },
    onFiltersChanged: () => { renders++; }
  });
  return { elements, feature, state, renders: () => renders };
}



test('updates the visible workspace count', () => {
  const { feature, elements } = createHarness({ workspaces: [{ visible: true }, { visible: false }, { visible: true }] });
  feature.updateCount();
  assert.strictEqual(elements['filter-count'].textContent, '2 resultados de 3');
});

test('does not touch the UI before data is loaded', () => {
  const { feature, elements } = createHarness({ loaded: false, workspaces: [{ visible: true }] });
  feature.updateCount();
  assert.strictEqual(elements['filter-count'].textContent, '');
});

test('renders chips with the existing zone label and clears one filter on click', () => {
  const { feature, elements, state, renders } = createHarness({ filters: { zone: 'M1' } });
  elements['filter-zone'].value = 'M1';
  feature.renderChips();
  assert.strictEqual(elements['filter-chips'].children.length, 1);
  const chip = elements['filter-chips'].children[0];
  assert.strictEqual(chip.textContent, 'Norte ×');
  chip.onclick();
  assert.strictEqual(state.filters.zone, '');
  assert.strictEqual(elements['filter-zone'].value, '');
  assert.strictEqual(renders(), 1);
});

test('binds text filters with the historical trim and lowercase normalization', () => {
  const { feature, elements, state, renders } = createHarness();
  feature.bindControls();
  elements['filter-person'].oninput({ target: { value: '  ANA  ' } });
  assert.strictEqual(state.filters.person, 'ana');
  assert.strictEqual(renders(), 1);
});

test('binds the only-matches checkbox without changing its boolean value', () => {
  const { feature, elements, state, renders } = createHarness();
  feature.bindControls();
  elements['filter-only'].onchange({ target: { checked: true } });
  assert.strictEqual(state.filters.only, true);
  assert.strictEqual(renders(), 1);
});

test('exposes only filter UI responsibilities', () => {
  assert.deepStrictEqual(Object.keys(createHarness().feature).sort(), ['bindControls', 'renderChips', 'updateCount']);
});
