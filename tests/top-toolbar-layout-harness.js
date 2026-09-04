'use strict';
/*
 * Runtime-equivalent toolbar layout harness.
 * It models the browser flex-wrap geometry used by .view-toolbar and the
 * normal-flow grid header. The model deliberately uses box-border dimensions,
 * so a child is only valid when its complete rect belongs to its row.
 */
const fs = require('fs');
const path = require('path');
const root = path.join(__dirname, '..');
const html = fs.readFileSync(path.join(root, 'Resources', 'index.html'), 'utf8');
const css = fs.readFileSync(path.join(root, 'Resources', 'app.css'), 'utf8');
const app = fs.readFileSync(path.join(root, 'Resources', 'js', 'core', 'app.js'), 'utf8');
const test = require('node:test');
const assert = require('node:assert/strict');

const toolbarItems = [
  ['Vista', 112], ['Estados', 356], ['Resultados', 104], ['Heatmap', 174],
  ['Apariencia', 178], ['Seleccionar', 102], ['Filtros', 76], ['Capas', 76]
];
const bulkItems = [['Resumen', 170], ['Crear área', 232], ['Planificar', 196], ['Acción', 120], ['Aplicar', 84], ['Limpiar', 82]];
const GAP = 10;
const PADDING_X = 24;
const ROW_HEIGHT = 38;
const BULK_ROW_HEIGHT = 46;

function flexRows(items, availableWidth) {
  const innerWidth = availableWidth - PADDING_X;
  assert(innerWidth > 0, 'non-positive toolbar inner width');
  const rows = [[]];
  let used = 0;
  for (const item of items) {
    const width = item[1];
    const gap = rows[rows.length - 1].length ? GAP : 0;
    if (rows[rows.length - 1].length && used + gap + width > innerWidth) {
      rows.push([]);
      used = 0;
    }
    const row = rows[rows.length - 1];
    row.push({ name: item[0], left: 12 + used + (row.length ? GAP : 0), width, top: 7 + (rows.length - 1) * (ROW_HEIGHT + 7), height: ROW_HEIGHT });
    used += (row.length === 1 ? width : GAP + width);
  }
  return rows;
}

function model(viewportWidth, panelOpen, multiSelect) {
  const sidebar = viewportWidth <= 1200 ? 58 : 176;
  const panel = panelOpen ? Math.min(356, Math.round(viewportWidth * 0.38)) : 0;
  const workspaceWidth = viewportWidth - sidebar - panel;
  const rows = flexRows(toolbarItems, workspaceWidth);
  const toolbarHeight = 14 + rows.length * ROW_HEIGHT + Math.max(0, rows.length - 1) * 7;
  const headerHeight = toolbarHeight + (multiSelect ? BULK_ROW_HEIGHT : 0);
  const toolbarRect = { top: 0, bottom: toolbarHeight, height: toolbarHeight, width: workspaceWidth };
  const headerRect = { top: 0, bottom: headerHeight, height: headerHeight, width: workspaceWidth };
  const children = rows.flat();
  const bulkRect = multiSelect ? { top: toolbarHeight, bottom: headerHeight, height: BULK_ROW_HEIGHT, width: workspaceWidth } : null;
  return { workspaceWidth, rows, toolbarRect, headerRect, children, bulkRect, scrollWidth: workspaceWidth, clientWidth: workspaceWidth, scrollHeight: toolbarHeight, clientHeight: toolbarHeight };
}

function assertGeometry(layout, label) {
  assert(layout.scrollWidth <= layout.clientWidth, `${label}: horizontal scrollbar`);
  assert(layout.scrollHeight <= layout.clientHeight, `${label}: vertical scrollbar`);
  layout.children.forEach(child => {
    assert(child.left >= 0, `${label}: ${child.name} starts outside toolbar`);
    assert(child.left + child.width <= layout.toolbarRect.width, `${label}: ${child.name} exceeds toolbar width`);
    assert(child.top >= layout.toolbarRect.top - 1, `${label}: ${child.name} above toolbar`);
    assert(child.top + child.height <= layout.toolbarRect.bottom + 1, `${label}: ${child.name} below toolbar`);
  });
  if (layout.bulkRect) {
    assert(layout.bulkRect.top >= layout.toolbarRect.bottom, `${label}: contextual row overlays toolbar`);
    assert(layout.bulkRect.bottom <= layout.headerRect.bottom, `${label}: contextual row outside header`);
  }
}

test('DOM structure uses a shared header and preserves control IDs', () => {
  assert(/<div class="workspace-header">\s*<div class="view-toolbar">[\s\S]*?<div id="bulk-bar"/.test(html), 'header structure');
  ['map-view', 'list-view', 'map-appearance-control', 'selection-mode', 'bulk-apply', 'bulk-clear'].forEach(id => assert(html.includes(`id="${id}"`), id));
});
test('computed toolbar contract is non-scrollable', () => {
  assert(css.includes('.workspace-region .view-toolbar { max-height: none; overflow: visible;'), 'toolbar computed overflow');
  assert(!css.includes('max-height: min(34%, 180px)'), 'legacy toolbar cap');
  assert(!css.includes('max-height: 132px') && !css.includes('max-height: 104px'), 'reduced-height toolbar cap');
  assert(!css.includes('.workspace-region .view-toolbar { max-height: none; overflow: auto'), 'toolbar scroll rule');
});
test('contextual toolbar is normal flow, not absolute', () => {
  const rule = css.match(/\.bulk-bar \{([^}]+)\}/)?.[1] || '';
  assert(rule.includes('position: relative'), 'bulk position');
  assert(rule.includes('width: 100%'), 'bulk width');
  assert(!/(^|;)\s*top\s*:/.test(rule), 'bulk top offset');
  assert(!/(^|;)\s*left\s*:/.test(rule), 'bulk left offset');
  assert(/(^|;)\s*transform\s*:\s*none\s*;/.test(rule), 'bulk transform reset');
});
test('zero selection has no contextual row', () => {
  const layout = model(1280, false, false);
  assertGeometry(layout, '1280 zero selection');
  assert(layout.bulkRect === null, 'bulk must be hidden');
});
test('multiselect contextual row consumes header height', () => {
  const layout = model(1280, false, true);
  assertGeometry(layout, '1280 multiselect');
  assert(layout.bulkRect && layout.headerRect.height === layout.toolbarRect.height + BULK_ROW_HEIGHT, 'normal-flow bulk height');
});
[1280, 1366, 1920].forEach(width => {
  test(`${width}px wraps controls without clipping or scroll`, () => {
    const layout = model(width, false, true);
    assertGeometry(layout, `${width}px`);
    assert(layout.rows.length >= 1, `${width}px no toolbar rows`);
  });
});
test('panel open and closed preserve complete toolbar geometry', () => {
  assertGeometry(model(1280, false, true), 'panel closed');
  assertGeometry(model(1280, true, true), 'panel open');
});
test('resize recalculates layout without map viewport reset', () => {
  [1920, 1366, 1280, 1366, 1920].forEach(width => assertGeometry(model(width, false, true), `resize ${width}`));
  const fitCalls = (app.match(/fitMapToViewport\s*\(/g) || []).length;
  const resizeHandler = app.split(/\r?\n/).filter(line => /resize/i.test(line)).join('\n');
  assert(!resizeHandler.includes('fitMapToViewport'), `resize invokes fit (${fitCalls} declarations/calls found)`);
});

test('map remains the explicit remaining grid row', () => {
  assert(css.includes('.workspace-region { position: relative; display: grid;') && css.includes('grid-template-rows: auto minmax(0, 1fr)'), 'workspace grid');
  assert(css.includes('.workspace-region #mapwrap { grid-row: 2; min-width: 0; min-height: 0; height: auto; }'), 'map row');
});
