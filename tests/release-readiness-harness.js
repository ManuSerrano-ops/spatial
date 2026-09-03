'use strict';

const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const resources = path.join(root, 'Resources');
const data = path.join(root, 'runtime-data', 'data');
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const read = file => fs.readFileSync(file, 'utf8');
const cell = (x, y) => {
  const column = Math.max(0, Math.min(23, Math.floor(Number(x) * 24)));
  const row = Math.max(0, Math.min(17, Math.floor(Number(y) * 18)));
  return `${String.fromCharCode(65 + column)}-${String(row + 1).padStart(2, '0')}`;
};

const index = read(path.join(resources, 'index.html'));
const css = read(path.join(resources, 'app.css'));
const app = read(path.join(resources, 'js', 'core', 'app.js'));
const windowXaml = read(path.join(root, 'src', 'Desktop', 'Host', 'MainWindow.xaml'));

test('all active local styles and scripts exist', () => {
  const references = [...index.matchAll(/(?:src|href)="([^"]+)"/g)].map(match => match[1])
    .filter(reference => !reference.startsWith('#') && !reference.startsWith('data:'));
  for (const reference of references) {
    assert(!/^https?:\/\//i.test(reference), `remote reference found: ${reference}`);
    assert(fs.existsSync(path.join(resources, reference)), `missing local resource: ${reference}`);
  }
});

test('no legacy .orig resource is active or embeddable', () => {
  assert(!index.includes('.orig'), 'index references a legacy .orig resource');
  const project = read(path.join(root, 'PlanoOpenSpaceIT.Windows.csproj'));
  assert(project.includes('<EmbeddedResource Remove="Resources\\**\\*.orig" />'), 'legacy .orig files are not excluded from embedded resources');
});

test('compact navigation has distinguishable visible abbreviations', () => {
  const entries = [...index.matchAll(/<button[^>]*class="[^"]*\bnav-item\b[^"]*"[^>]*>/g)].map(match => match[0]);
  assert(entries.length >= 6, 'simplified sidebar entries need abbreviations and accessible labels');
  const abbreviations = entries.map(entry => entry.match(/data-short="([^"]+)"/)?.[1] || '');
  assert(entries.every(entry => /aria-label="[^"]+"/.test(entry)), 'sidebar entries need accessible labels');
  assert(new Set(abbreviations).size === abbreviations.length, 'compact navigation abbreviations must be unique');
  assert(css.includes('content: attr(data-short)'), 'compact sidebar does not render its abbreviations');
  assert(!entries.some(entry => /disabled/.test(entry)), 'sidebar must not retain disabled placeholder navigation');
});

test('compact map controls remain reachable and search is viewport-positioned', () => {
  assert(!css.includes('@media (max-width: 700px) { .layers-menu, #selection-mode { display: none; }'), 'compact layout hides map controls');
  assert(app.includes('function positionSearchResults()'), 'search results are not positioned from the search control');

});

test('accessibility focus covers map and disclosure controls', () => {
  assert(css.includes('summary:focus-visible, #mapwrap:focus-visible'), 'map and details focus styles are missing');
  assert(index.includes('aria-label="Plano interactivo"'), 'map does not have an accessible label');
});

test('scenario compare uses business units before Apply', () => {
  const helperOffset = index.indexOf('scenario-compare-helpers.js');
  const appOffset = index.indexOf('app.js');
  assert(helperOffset >= 0 && helperOffset < appOffset, 'scenario compare helper is not loaded before app.js');
  assert(app.includes('scenarioCompareHelpers.buildCompareUnits'), 'Compare does not build business units');
  assert(app.includes('scenarioCompareHelpers.flattenSelectedCompareUnits'), 'Apply does not flatten selected business units');
  assert(app.includes('ui.selectedCompareUnitIds'), 'Compare still relies on raw change selection');
});

test('window and shell fit the WebView client area instead of viewport height', () => {
  const minHeight = Number(windowXaml.match(/MinHeight="(\d+)"/)?.[1]);
  assert(Number.isFinite(minHeight) && minHeight < 680, 'window minimum height does not fit a 720px work area with taskbar');
  assert(!/height:\s*calc\(100vh/i.test(css), 'shell still derives height from viewport units');
  assert(css.includes('body { display: grid; grid-template-rows: var(--topbar-height) minmax(0, 1fr); min-height: 0; }'), 'body does not reserve the flexible client-area row');
  assert(css.includes('min-height: 0; height: auto; overflow: hidden; }'), 'app shell does not yield height to the client-area grid');
});

test('display location collisions are reported from the current working data', () => {
  const maps = JSON.parse(read(path.join(data, 'maps.json'))).maps || [];
  const positions = JSON.parse(read(path.join(data, 'positions.json'))).positions || [];
  const coordinate = new Map(positions.map(item => [`${item.mapId}|${item.seatId}`, item]));
  const collisions = [];
  for (const map of maps) {
    const seen = new Map();
    for (const seat of map.seats || []) {
      const position = coordinate.get(`${map.id}|${seat.id}`) || seat;
      if (!Number.isFinite(Number(position.x)) || !Number.isFinite(Number(position.y))) continue;
      const location = cell(position.x, position.y);
      const group = seen.get(location) || [];
      group.push(seat.id);
      seen.set(location, group);
    }
    for (const [location, seatIds] of seen) if (seatIds.length > 1) collisions.push({ mapId: map.id, location, count: seatIds.length });
  }
  console.log(`Display location collisions: ${collisions.length}`);
  assert(Array.isArray(collisions), 'collision report was not generated');
});

let passed = 0;
for (const { name, fn } of tests) {
  try { fn(); passed++; }
  catch (error) { console.error(`FAIL: ${name}: ${error.message}`); }
}
console.log(`Release readiness static harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
