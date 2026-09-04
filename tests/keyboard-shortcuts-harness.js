'use strict';

const fs = require('fs');
const path = require('path');

const app = fs.readFileSync(
  path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'),
  'utf8'
);
const html = fs.readFileSync(
  path.join(__dirname, '..', 'Resources', 'index.html'), 'utf8'
);
const preferences = fs.readFileSync(
  path.join(
    __dirname,
    '..',
    'src',
    'Desktop',
    'Preferences',
    'ExportFolderPreferences.cs'
  ), 'utf8'
);
const test = require('node:test');
const assert = require('node:assert/strict');
const equal = (actual, expected, message) => {
  if (actual !== expected) throw new Error(`${message}: ${actual} !== ${expected}`);
};

class FakeInput {}
class FakeTextarea {}
class FakeSelect {}

function focusable(document, tabIndex = 0) {
  return {
    tabIndex,
    focus() { document.activeElement = this; }
  };
}

function loadKeyboardHandler(document, ui, options = {}) {
  let handler = null;
  document.querySelector ??= () => null;
  document.addEventListener = (type, listener) => {
    if (type === 'keydown') handler = listener;
  };
  const start = app.indexOf('  function isEditableKeyboardEvent');
  const end = app.indexOf('  window.receiveFromNative', start);
  const source = app.slice(start, end);
  const controls = {
    search: focusable(document),
    'filter-bar': { querySelector: () => focusable(document) },
    'seat-name': focusable(document),
    tooltip: { classList: { contains: () => false } },
    'context-menu': { classList: { contains: () => false } },
    'search-results': { classList: { add() {} } },
    ...options.controls
  };
  Function(
    'document',
    'HTMLInputElement',
    'HTMLTextAreaElement',
    'HTMLSelectElement',
    '$',
    'ui',
    'appState',
    'hideContextMenu',
    'hidePreview',
    'closeMoreMenu',
    'renderProblems',
    'setSelectionMode',
    'setStatus',
    'clearBulkSelection',
    'closePlannerPanel',
    'plannerState',
    'closeDetailPanel',
    'render',
    'saveClusterCardShapes',
    'refreshManagedAreaCard',
    'showMessage',
    'adjacentSeat',
    'selectSeat',
    'centerSelectedSeat',
    `${source}\nreturn null;`
  )(
    document,
    FakeInput,
    FakeTextarea,
    FakeSelect,
    id => controls[id],
    ui,
    options.appState ?? { viewMode: 'map', selectedProblemId: null, selectedWorkspaces: { size: 0 } },
    options.hideContextMenu ?? (() => {}),
    options.hidePreview ?? (() => {}),
    options.closeMoreMenu ?? (() => {}),
    options.renderProblems ?? (() => {}),
    options.setSelectionMode ?? (() => {}),
    options.setStatus ?? (() => {}),
    options.clearBulkSelection ?? (() => {}),
    options.closePlannerPanel ?? (() => {}),
    options.plannerState ?? (() => ({ status: 'idle' })),
    options.closeDetailPanel ?? (() => {}),
    options.render ?? (() => {}),
    options.saveClusterCardShapes ?? (() => {}),
    options.refreshManagedAreaCard ?? (() => {}),
    options.showMessage ?? (() => {}),
    options.adjacentSeat ?? (() => null),
    options.selectSeat ?? (() => {}),
    options.centerSelectedSeat ?? (() => {})
  );
  return { handler, controls };
}

function loadHideContextMenu(ui, controls) {
  const source = app
    .match(/  function hideContextMenu[\s\S]*?\n  function closeMoreMenu/)[0]
    .replace(/\n  function closeMoreMenu$/, '');
  return Function('ui', '$', `${source}\nreturn hideContextMenu;`)(ui, id => controls[id]);
}

function keyboardEvent(target, key, path = [target]) {
  return {
    target,
    key,
    ctrlKey: false,
    altKey: false,
    metaKey: false,
    composedPath: () => path,
    preventDefault() { this.defaultPrevented = true; },
    defaultPrevented: false
  };
}

const printableKeys = [
  ...'abcdefghijklmnopqrstuvwxyz',
  ...'0123456789',
  ...'`-=[]\\;\',./',
  ...'~_+{}|:"<>?!@#$%^&*()'
];

test('the preference defaults to enabled and preserves user preferences', () => {
  assert(preferences.includes('bool SingleKeyShortcutsEnabled = true'), 'safe default missing');
  assert(preferences.includes('SaveUserPreferences'), 'combined persistence missing');
  assert(preferences.includes('singleKeyShortcutsEnabled ?? preferences.SingleKeyShortcutsEnabled'), 'existing preference is not preserved');
});

test('the switch is keyboard reachable and has a visible label', () => {
  assert(html.includes('id="single-key-shortcuts-enabled" type="checkbox" checked'), 'switch missing');
  assert(/<span>Atajos de una tecla<\/span>/.test(html), 'switch label missing');
  assert(app.includes("$('single-key-shortcuts-enabled').onchange"), 'switch is not bound');
});

test('disabled preference leaves every printable key inert on the map', () => {
  const document = { activeElement: null, body: {}, documentElement: {} };
  const map = focusable(document);
  const ui = { singleKeyShortcutsEnabled: false, seatId: 'W-1' };
  const { handler } = loadKeyboardHandler(document, ui);
  printableKeys.forEach(key => {
    document.activeElement = map;
    const event = keyboardEvent(map, key, [map, document.body]);
    handler(event);
    assert(document.activeElement === map, `${key} moved focus while disabled`);
    assert(!event.defaultPrevented, `${key} was intercepted while disabled`);
    equal(ui.seatId, 'W-1', `${key} changed application state while disabled`);
  });
});

test('enabled preference makes exactly slash, f and e effective on the map', () => {
  const document = { activeElement: null, body: {}, documentElement: {} };
  const map = focusable(document);
  const ui = { singleKeyShortcutsEnabled: true, seatId: 'W-1' };
  const { handler, controls } = loadKeyboardHandler(document, ui);
  const effects = [];
  printableKeys.forEach(key => {
    document.activeElement = map;
    const event = keyboardEvent(map, key, [map, document.body]);
    handler(event);
    if (event.defaultPrevented || document.activeElement !== map) effects.push(key);
  });
  const expected = ['/', 'f', 'e'];
  equal(effects.sort().join(','), expected.sort().join(','), 'unexpected printable shortcut');
  assert(controls.search !== map, 'search control was not configured');
});

test('editable targets do not intercept single-key shortcuts', () => {
  const document = { activeElement: null, body: {}, documentElement: {} };
  const ui = { singleKeyShortcutsEnabled: true, seatId: 'W-1' };
  const { handler } = loadKeyboardHandler(document, ui);
  const nativeInput = new FakeInput();
  const nativeTextarea = new FakeTextarea();
  const nativeSelect = new FakeSelect();
  const contentEditable = { isContentEditable: true, tabIndex: 0 };
  const roleTextbox = { tabIndex: 0, getAttribute: name => name === 'role' ? 'textbox' : null };
  const componentHost = { tabIndex: 0, getAttribute: name => name === 'role' ? 'textbox' : null };
  [
    [nativeInput, [nativeInput, document.body]],
    [nativeTextarea, [nativeTextarea, document.body]],
    [nativeSelect, [nativeSelect, document.body]],
    [contentEditable, [contentEditable, document.body]],
    [roleTextbox, [roleTextbox, document.body]],
    [{}, [{}, componentHost, document.body]]
  ].forEach(([target, path]) => {
    const event = keyboardEvent(target, 'f', path);
    handler(event);
    assert(!event.defaultPrevented, 'editor shortcut was intercepted');
  });
});

test('editable detection stops at the first focusable path element', () => {
  const start = app.indexOf('function isEditableKeyboardEvent');
  const end = app.indexOf("document.addEventListener('keydown'", start);
  const source = app.slice(start, end);
  assert(source.includes('event.composedPath?.()'), 'composed path missing');
  assert(source.includes("getAttribute?.('role') === 'textbox'"), 'textbox role missing');
  assert(source.includes('target?.tabIndex >= 0'), 'focus boundary missing');
  assert(source.includes('target === document.body'), 'document boundary missing');
  assert(source.includes('HTMLInputElement') && source.includes('HTMLTextAreaElement') && source.includes('HTMLSelectElement'), 'native editors missing');
});

test('global Ctrl+Z reuses central undo button', () => {
  assert(app.includes("event.ctrlKey && event.key.toLowerCase() === 'z'") && app.includes("$('undo').click()"), 'central undo missing');
});

test('Escape closes an open dialog before lower-priority transient UI', () => {
  const document = { activeElement: null, body: {}, documentElement: {} };
  let closed = 0;
  document.querySelector = selector => selector === 'dialog[open]' ? { close() { closed++; } } : null;
  let previewHidden = 0;
  let menuHidden = 0;
  const ui = { singleKeyShortcutsEnabled: true, seatId: 'W-1' };
  const { handler } = loadKeyboardHandler(document, ui, {
    controls: {
      tooltip: { classList: { contains: value => value === 'show' } },
      'context-menu': { classList: { contains: value => value === 'show' } }
    },
    hidePreview: () => { previewHidden++; },
    hideContextMenu: () => { menuHidden++; }
  });
  const event = keyboardEvent(new FakeInput(), 'Escape', [new FakeInput(), document.body]);
  handler(event);
  equal(closed, 1, 'Escape did not close the dialog');
  equal(previewHidden, 0, 'dialog Escape reached the preview');
  equal(menuHidden, 0, 'dialog Escape reached the context menu');
  assert(event.defaultPrevented, 'dialog Escape did not prevent the native duplicate close');
});

test('Escape restores focus to the cluster card through the global cascade', () => {
  const document = { activeElement: null, body: {}, documentElement: {} };
  const opener = focusable(document);
  const menu = {
    classList: {
      contains: value => value === 'show',
      remove() { menu.hidden = true; }
    },
    removeAttribute() {}
  };
  const ui = { singleKeyShortcutsEnabled: true, seatId: 'W-1', contextMenuRestoreFocus: () => opener.focus() };
  let hideContextMenu;
  let moreMenuClosed = 0;
  const { handler, controls } = loadKeyboardHandler(document, ui, {
    controls: { 'context-menu': menu },
    hideContextMenu: () => hideContextMenu(),
    closeMoreMenu: () => { moreMenuClosed++; }
  });
  hideContextMenu = loadHideContextMenu(ui, controls);
  document.activeElement = menu;
  const event = keyboardEvent(menu, 'Escape', [menu, document.body]);
  handler(event);
  assert(menu.hidden, 'Escape did not hide the context menu');
  assert(document.activeElement === opener, 'Escape did not restore focus to the cluster card');
  equal(moreMenuClosed, 1, 'Escape did not close the secondary transient menu');
  assert(event.defaultPrevented, 'context-menu Escape was not consumed by the global cascade');
});

test('Escape reaches the detail panel only after higher-priority UI is absent', () => {
  const document = { activeElement: null, body: {}, documentElement: {} };
  const map = focusable(document);
  let detailPanelsClosed = 0;
  const ui = { singleKeyShortcutsEnabled: true, seatId: 'W-1', selectionMode: false };
  const { handler } = loadKeyboardHandler(document, ui, {
    closeDetailPanel: () => { detailPanelsClosed++; }
  });
  const event = keyboardEvent(map, 'Escape', [map, document.body]);
  handler(event);
  equal(detailPanelsClosed, 1, 'Escape did not reach the detail panel at the end of the cascade');
  assert(event.defaultPrevented === false, 'detail-panel Escape unexpectedly prevented its native behavior');
});

test('Escape exits selection mode before clearing a multi-selection', () => {
  const document = { activeElement: null, body: {}, documentElement: {} };
  const map = focusable(document);
  const appState = { viewMode: 'map', selectedProblemId: null, selectedWorkspaces: { size: 2 } };
  const ui = { singleKeyShortcutsEnabled: true, seatId: 'W-1', selectionMode: true };
  let selectionMode = null;
  let bulkCleared = 0;
  let status = '';
  const { handler } = loadKeyboardHandler(document, ui, {
    appState,
    setSelectionMode: active => { selectionMode = active; },
    setStatus: value => { status = value; },
    clearBulkSelection: () => { bulkCleared++; }
  });
  handler(keyboardEvent(map, 'Escape', [map, document.body]));
  equal(selectionMode, false, 'Escape did not leave selection mode');
  equal(bulkCleared, 0, 'Escape cleared the multi-selection too early');
  assert(status.includes('Los puestos ya seleccionados se conservan.'), 'Escape did not preserve selection feedback');
});

test('non-character keyboard navigation remains outside the preference', () => {
  const start = app.indexOf("document.addEventListener('keydown'");
  const end = app.indexOf('  window.receiveFromNative', start);
  const global = app.slice(start, end);
  assert(global.includes("event.key === 'Escape'"), 'Escape handling missing');
  assert(global.includes("/^Arrow/.test(event.key)"), 'arrow navigation missing');
  assert(global.includes("event.ctrlKey && event.key.toLowerCase() === 'y'"), 'modified redo missing');
});
