'use strict';
const fs = require('fs');
const path = require('path');
const resources = path.join(__dirname, '..', 'Resources');
const html = fs.readFileSync(path.join(resources, 'index.html'), 'utf8');
const css = fs.readFileSync(path.join(resources, 'app.css'), 'utf8');
const app = fs.readFileSync(path.join(resources, 'js', 'core', 'app.js'), 'utf8');
const test = require('node:test');
const assert = require('node:assert/strict');

test('zero and one selection hide bulk toolbar', () => assert(app.includes("$('bulk-bar').classList.toggle('hidden', !workspaceSurface || count < 2)"), 'visibility threshold'));
test('two plus selection keeps business actions in toolbar and cluster action in panel', () => assert(!html.includes('id="bulk-create-area"') && html.includes('id="selection-review-create-cluster"'), 'cluster action ownership'));
test('area and movement actions have explicit labels', () => assert(html.includes('Planificar movimiento') && !html.includes('<button id="bulk-create-area" class="primary hidden" type="button">+</button>'), 'labels'));
test('toolbar and contextual row share a normal-flow header', () => assert(/<div class="workspace-header">\s*<div class="view-toolbar">[\s\S]*?<div id="bulk-bar"/.test(html), 'workspace header structure'));
test('toolbar cannot be capped or scrolled', () => {
  assert(!css.includes('max-height: min(34%, 180px)'), 'legacy maximum height');
  assert(!css.includes('.workspace-region .view-toolbar { max-height: 132px'), '780px maximum height');
  assert(!css.includes('.workspace-region .view-toolbar { max-height: 104px'), '620px maximum height');
  assert(css.includes('.workspace-region .view-toolbar { max-height: none; overflow: visible;'), 'normal toolbar overflow');
});
test('bulk bar participates in layout rather than overlaying the map', () => {
  const bulkRule = css.match(/\.bulk-bar \{([^}]+)\}/);
  assert(bulkRule && bulkRule[1].includes('position: relative'), 'relative bulk bar');
  assert(bulkRule[1].includes('width: 100%'), 'full row');
  assert(!bulkRule[1].includes('translateX'), 'no overlay transform');
});
test('client chain uses min-height zero', () => ['main.app-shell', 'main.app-shell > .workspace-region', '.workspace-region > .workspace-header', '.workspace-region > #mapwrap'].forEach(selector => assert(css.includes(selector), selector)));
test('workspace grid reserves an auto header and remaining map content', () => assert(css.includes('grid-template-rows: auto minmax(0, 1fr)') && css.includes('.workspace-region #mapwrap { grid-row: 2'), 'grid contract'));
