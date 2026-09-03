'use strict';
const fs = require('fs');
const path = require('path');
const resources = path.join(__dirname, '..', 'Resources');
const app = fs.readFileSync(path.join(resources, 'js', 'core', 'app.js'), 'utf8');
const controller = fs.readFileSync(path.join(resources, 'js', 'features', 'selection', 'selection-controller-feature.js'), 'utf8');
const html = fs.readFileSync(path.join(resources, 'index.html'), 'utf8');
const css = fs.readFileSync(path.join(resources, 'app.css'), 'utf8');
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };

function toolbarBeforeBulk() { return html.indexOf('id="selection-mode"') < html.indexOf('id="bulk-bar"'); }

test('Select is a persistent map-toolbar control before filters and bulk actions', () => {
  assert(toolbarBeforeBulk(), 'selection control is not ahead of contextual bulk actions');
  assert(/view-switch[\s\S]*?id="selection-mode"[\s\S]*?id="filter-bar"/.test(html), 'selection control is not in the left-priority toolbar region');
  assert(css.includes('#selection-mode { order: -1; flex: 0 0 auto; min-width: 124px; }'), 'selection control lacks layout priority');
});

test('bulk visibility depends only on workspace count and never hides Select', () => {
  const renderBulk = app.match(/function renderBulkBar\(\)[\s\S]*?\n  function selectionReviewWorkspaceData/)[0];
  assert(renderBulk.includes("$('bulk-bar').classList.toggle('hidden', !workspaceSurface || count < 2)"), 'bulk threshold missing');
  assert(!renderBulk.includes("$('selection-mode').classList.toggle('hidden'"), 'bulk render hides selection control');
  assert(!app.includes('selectedWorkspaces.size > 0) $(\'selection-mode\').classList.add(\'hidden\')'), 'selection count hides control');
});

test('Select is an accessible toggle with an active visual state', () => {
  assert(html.includes('id="selection-mode" class="selection-mode-toggle" type="button" aria-pressed="false"'), 'toggle ARIA baseline missing');
  const modeStart = app.indexOf('function setSelectionMode(active)');
  const modeEnd = app.indexOf('function bulkSelectionChanged', modeStart);
  const mode = app.slice(modeStart, modeEnd);
  assert(html.includes('js/features/selection/selection-controller-feature.js'), 'selection controller is not loaded');
  assert(mode.includes('return selectionControllerFeature.setMode(active);'), 'app facade does not delegate mode changes');
  assert(controller.includes("button.setAttribute('aria-pressed', String(enabled))"), 'ARIA pressed is not updated');
  assert(controller.includes("button.textContent = enabled ? '✓ Seleccionando' : 'Seleccionar'"), 'visible active label missing');
  assert(controller.includes("button.title = enabled ? 'Finalizar selección sin limpiar puestos'"), 'toggle tooltip missing');
  assert(css.includes('#selection-mode.active'), 'active visual state missing');
});

test('toggle off preserves selected workspaces and Clear remains distinct', () => {
  const click = "$('selection-mode').onclick = () => { const enabled = setSelectionMode(!ui.selectionMode); setStatus(enabled ? 'Modo selección rectangular activo.' : 'Modo selección rectangular desactivado. Los puestos ya seleccionados se conservan.'); };";
  assert(app.includes(click), 'button does not toggle selection mode');
  assert(!click.includes('clearWorkspaceSelection'), 'toggle clears selected workspaces');
  assert(app.includes("$('bulk-clear').onclick = () => clearWorkspaceSelection()"), 'Clear is not explicit');
});


test('selection and card editing are incompatible without clearing selected workspaces', () => {
  const beginEdit = app.match(/function beginClusterCardEdit[\s\S]*?\n  function updateClusterCardEditDraft/)[0];
  assert(beginEdit.includes('setSelectionMode(false);'), 'card edit does not take interaction priority');
  assert(!beginEdit.includes('clearWorkspaceSelection'), 'card edit clears selection');
});

test('responsive 1280, 1366 and 1920 keep the control in normal-flow wrapping toolbar', () => {
  assert(css.includes('.workspace-region .view-toolbar {') && css.includes('flex-wrap: wrap'), 'toolbar cannot wrap at constrained widths');
  assert(!css.includes('#selection-mode { display: none'), 'responsive CSS hides selection control');
  [1280, 1366, 1920].forEach(width => assert(width >= 1280 && toolbarBeforeBulk(), `selection control not represented at ${width}px`));
});

let passed = 0;
for (const item of tests) { try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); } }
console.log(`Selection mode visibility harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
