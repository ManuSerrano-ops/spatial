'use strict';

const assert = require('assert');
const appearanceHelpers = require('../Resources/js/features/map/map-appearance-helpers.js');
const { createMapAppearanceFeature } = require('../Resources/js/features/map/map-appearance-feature.js');

function createButton(mode) {
  const attributes = new Map();
  return {
    dataset: { mapAppearance: mode },
    setAttribute(name, value) { attributes.set(name, String(value)); },
    attribute(name) { return attributes.get(name); }
  };
}

function createDocument(buttons) {
  const tokens = new Map();
  return {
    documentElement: {
      dataset: {},
      style: { setProperty(name, value) { tokens.set(name, value); } }
    },
    querySelectorAll(selector) {
      assert.strictEqual(selector, '[data-map-appearance]');
      return buttons;
    },
    tokens
  };
}

function createStorage(values = {}, options = {}) {
  const saved = new Map(Object.entries(values));
  return {
    getItem(key) {
      if (options.failRead) throw new Error('read unavailable');
      return saved.get(key) ?? null;
    },
    setItem(key, value) {
      if (options.failWrite) throw new Error('write unavailable');
      saved.set(key, String(value));
    },
    saved
  };
}

const tests = [];
const test = (name, run) => tests.push({ name, run });

test('loads and normalizes the local preference', () => {
  const feature = createMapAppearanceFeature({
    appearanceHelpers,
    state: {},
    storage: createStorage({ 'plano.mapAppearance': 'LIGHT' }),
    document: createDocument([])
  });
  assert.strictEqual(feature.loadPreference(), 'light');
});

test('falls back to dark when local storage cannot be read', () => {
  const feature = createMapAppearanceFeature({
    appearanceHelpers,
    state: {},
    storage: createStorage({}, { failRead: true }),
    document: createDocument([])
  });
  assert.strictEqual(feature.loadPreference(), 'dark');
});

test('applies presentation mode, tokens and accessible button state', () => {
  appearanceHelpers.configureManifest({
    schemaVersion: 2,
    assets: [{ id: 'test', canonical: 'test.svg', dark: 'test.svg', light: 'light/test.svg', lightSha256: 'hash' }]
  });
  const state = {};
  const buttons = [createButton('dark'), createButton('light')];
  const document = createDocument(buttons);
  const feature = createMapAppearanceFeature({ appearanceHelpers, state, storage: createStorage(), document });

  assert.strictEqual(feature.apply('light'), 'light');
  assert.strictEqual(state.mapAppearance, 'light');
  assert.strictEqual(document.documentElement.dataset.mapAppearance, 'light');
  assert.strictEqual(document.tokens.get('--map-pin-halo'), '#ffffff');
  assert.strictEqual(buttons[0].attribute('aria-pressed'), 'false');
  assert.strictEqual(buttons[1].attribute('aria-pressed'), 'true');
});

test('persists only the presentation preference', () => {
  const storage = createStorage();
  const feature = createMapAppearanceFeature({
    appearanceHelpers,
    state: {},
    storage,
    document: createDocument([])
  });
  feature.savePreference('light');
  assert.strictEqual(storage.saved.get('plano.mapAppearance'), 'light');
  assert.strictEqual(storage.saved.size, 1);
});

test('ignores local storage write failures', () => {
  const feature = createMapAppearanceFeature({
    appearanceHelpers,
    state: {},
    storage: createStorage({}, { failWrite: true }),
    document: createDocument([])
  });
  assert.doesNotThrow(() => feature.savePreference('light'));
});

test('uses only injected presentation dependencies', () => {
  const feature = createMapAppearanceFeature({
    appearanceHelpers,
    state: {},
    storage: createStorage(),
    document: createDocument([])
  });
  assert.deepStrictEqual(Object.keys(feature).sort(), ['apply', 'loadPreference', 'savePreference']);
});

let passed = 0;
for (const item of tests) {
  try {
    item.run();
    passed++;
  } catch (error) {
    console.error(`FAIL: ${item.name}: ${error.message}`);
  }
}
console.log(`Map appearance feature harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
