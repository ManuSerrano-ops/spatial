'use strict';

const assert = require('assert');
const { createUiThemeFeature, supportedThemes } = require('../Resources/js/shared/ui/ui-theme-feature.js');

function createDocument() {
  return { documentElement: { dataset: {} } };
}

const tests = [];
const test = (name, run) => tests.push({ name, run });

test('declares the four historical themes in stable order', () => {
  assert.deepStrictEqual(supportedThemes, ['professional-light', 'penpot-dark', 'high-contrast', 'projector']);
});

test('applies an accepted theme to the document and select control', () => {
  const document = createDocument();
  const themeSelect = { value: '' };
  const feature = createUiThemeFeature({ document, themeSelect });
  assert.strictEqual(feature.apply('penpot-dark'), 'penpot-dark');
  assert.strictEqual(document.documentElement.dataset.theme, 'penpot-dark');
  assert.strictEqual(themeSelect.value, 'penpot-dark');
});

test('falls back to the historical professional light theme', () => {
  const document = createDocument();
  const themeSelect = { value: '' };
  const feature = createUiThemeFeature({ document, themeSelect });
  assert.strictEqual(feature.apply('unsupported'), 'professional-light');
  assert.strictEqual(document.documentElement.dataset.theme, 'professional-light');
  assert.strictEqual(themeSelect.value, 'professional-light');
});

test('exposes only visual theme behavior', () => {
  const feature = createUiThemeFeature({ document: createDocument(), themeSelect: { value: '' } });
  assert.deepStrictEqual(Object.keys(feature).sort(), ['apply', 'supportedThemes']);
});

let passed = 0;
for (const item of tests) {
  try { item.run(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); }
}
console.log(`UI theme feature harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
