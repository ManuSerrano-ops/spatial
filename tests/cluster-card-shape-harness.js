'use strict';
const fs = require('fs');
const path = require('path');
const vm = require('vm');
const helpers = require('../Resources/js/features/managed-areas/cluster-card-shape-helpers.js');

const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const equal = (actual, expected, message) => { if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`); };

test('normalizes every supported shape and falls back to automatic', () => {
  equal(['automatic', 'compact', 'square', 'vertical'].map(helpers.normalizeClusterCardShape), ['automatic', 'compact', 'square', 'vertical'], 'supported shapes');
  equal(helpers.normalizeClusterCardShape(' SQUARE '), 'square', 'case and whitespace');
  equal(helpers.normalizeClusterCardShape('wide'), 'automatic', 'unknown shape');
});

test('derives concise content with total badge and no zero metrics', () => {
  const content = helpers.deriveClusterCardContent({ name: '  Equipo   Norte ', shape: 'compact', counts: { total: 8, occupied: 5, free: 0, reserved: 3, problems: 0 } });
  equal(content, {
    shape: 'compact', requestedShape: 'compact', name: 'Equipo Norte', badge: 8,
    counts: { total: 8, occupied: 5, free: 0, reserved: 3, problems: 0 },
    metrics: [{ key: 'occupied', label: 'ocupados', value: 5, text: '5 ocupados' }, { key: 'reserved', label: 'reservados', value: 3, text: '3 reservados' }],
    detail: '5 ocupados · 3 reservados'
  }, 'concise content');
});

test('includes the problem marker only when there are problems', () => {
  equal(helpers.deriveClusterCardContent({ counts: { problems: 0 } }).detail.includes('!'), false, 'zero problems');
  equal(helpers.deriveClusterCardContent({ counts: { problems: 2 } }).metrics.at(-1), { key: 'problems', label: 'problemas', value: 2, text: '! 2 problemas' }, 'positive problems');
});

test('tooltip always supplies the complete metric set', () => {
  equal(helpers.deriveClusterCardTooltip({ name: 'Sur', counts: { total: 4, occupied: 4 } }), 'Sur: 4 puestos · 4 ocupados · 0 libres · 0 reservados · 0 problemas', 'full metrics tooltip');
});

test('automatic shape is stable and derives from normalized name and counts', () => {
  equal(helpers.chooseAutomaticClusterCardShape({ name: 'Corto', counts: { total: 2, occupied: 2 } }), 'compact', 'small simple cluster');
  equal(helpers.chooseAutomaticClusterCardShape({ name: 'Corto', counts: { total: 2, occupied: 1, free: 1 } }), 'square', 'mixed states');
  equal(helpers.chooseAutomaticClusterCardShape({ name: 'Nombre de cluster muy largo', counts: { total: 2, occupied: 2 } }), 'vertical', 'long name');
  equal(helpers.chooseAutomaticClusterCardShape({ name: 'Corto', counts: { total: 12, occupied: 12 } }), 'square', 'large total');
  const input = { name: 'Corto', counts: { total: 2, occupied: 1, free: 1 } };
  equal(helpers.chooseAutomaticClusterCardShape(input), helpers.chooseAutomaticClusterCardShape(input), 'repeatable result');
});

test('normalization and presentation are immutable and do not mutate input', () => {
  const input = { name: '  QA ', shape: 'automatic', counts: { total: 1.9, occupied: -3, free: 1.8, reserved: 0, problems: -1 } };
  const before = JSON.stringify(input);
  const presentation = helpers.buildClusterCardShapePresentation(input);
  equal(input, JSON.parse(before), 'input mutation');
  equal(presentation.counts, { total: 1, occupied: 0, free: 1, reserved: 0, problems: 0 }, 'normalized counts');
  assert(Object.isFrozen(presentation) && Object.isFrozen(presentation.counts) && Object.isFrozen(presentation.metrics), 'presentation should be frozen');
});

test('exports the same API to browser window', () => {
  const source = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'features', 'managed-areas', 'cluster-card-shape-helpers.js'), 'utf8');
  const browser = { window: {} };
  vm.runInNewContext(source, browser);
  assert(browser.window.ClusterCardShapeHelpers, 'window API missing');
  equal(browser.window.ClusterCardShapeHelpers.normalizeClusterCardShape('VERTICAL'), 'vertical', 'window API behavior');
});

test('manual edit mode exposes a resize handle only while active', () => { const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8'); const css = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'app.css'), 'utf8'); assert(app.includes("ui.cardEdit?.active && ui.cardEdit.areaId === area.id"), 'active edit guard'); assert(app.includes("handle.className = 'cluster-resize-handle'"), 'resize handle'); assert(app.includes('handle.setPointerCapture(event.pointerId)'), 'pointer capture'); assert(css.includes('.cluster-resize-handle'), 'handle style'); });
test('save, cancel and reset retain presentation-only layout', () => { const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8'); assert(app.includes('function cancelClusterCardEdit()'), 'cancel'); assert(app.includes('function resetClusterCardEditToAutomatic()'), 'reset'); assert(app.includes('function commitClusterCardEdit()'), 'save'); assert(app.includes('saveClusterCardShapes()'), 'persistence'); });
let passed = 0;
for (const item of tests) {
  try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); }
}
console.log(`Cluster card shape harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
