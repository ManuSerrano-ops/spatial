'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { createUiThemeFeature, supportedThemes } = require('../Resources/js/shared/ui/ui-theme-feature.js');

function createDocument() {
  return { documentElement: { dataset: {} } };
}



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
