'use strict';
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const html = fs.readFileSync(path.join(root, 'Resources', 'index.html'), 'utf8');
const css = fs.readFileSync(path.join(root, 'Resources', 'app.css'), 'utf8');
const app = fs.readFileSync(path.join(root, 'Resources', 'js', 'core', 'app.js'), 'utf8');
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };

test('cluster cards expose direct rename and adjustment actions', () => {
  assert(app.includes('data-cluster-card-action="rename"'), 'rename action is not rendered on the card');
  assert(app.includes('data-cluster-card-action="adjust"'), 'adjust action is not rendered on the card');
  assert(app.includes("openAreaRename(area.id)"), 'rename action does not reuse the rename flow');
  assert(app.includes("beginClusterCardEdit(area.id)"), 'adjust action does not reuse free card editing');
  assert(css.includes('.cluster-card-actions {'), 'cluster action styling is missing');
});

test('layers are a contextual map control instead of a toolbar filter', () => {
  assert(/<section id="mapwrap"[\s\S]*?<details id="map-layers-control" class="map-layers-control"/.test(html), 'layers control is not inside the map');
  assert(html.indexOf('id="map-layers-control"') > html.indexOf('<section id="mapwrap"'), 'layers control remains in the toolbar');
  assert(css.includes('.map-layers-control {'), 'floating layer control styling is missing');
  assert(app.includes("event.target.closest('.map-layers-control')"), 'map interaction does not exclude layer controls');
});

test('map appearance switch has only selectable appearance options', () => {
  const control = html.match(/<div id="map-appearance-control"[\s\S]*?<\/div>/)?.[0] || '';
  assert(control.includes('>Oscuro<') && control.includes('>Claro<'), 'appearance options are missing');
  assert(!control.includes('>Plano<'), 'redundant Plano label remains in appearance control');
});

let passed = 0;
for (const item of tests) {
  try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); }
}
console.log(`Cluster map controls harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
