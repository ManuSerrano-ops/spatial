'use strict';

const fs = require('fs');
const path = require('path');
const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8');
const html = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'index.html'), 'utf8');
const css = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'app.css'), 'utf8');
const gridCursorHelpers = require(path.join(__dirname, '..', 'Resources', 'js', 'features', 'map', 'grid-cursor-helpers.js'));
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const equal = (actual, expected, message) => { if (actual !== expected) throw new Error(`${message}: ${actual} !== ${expected}`); };

class FakeInput {}
class FakeTextarea {}
class FakeSelect {}

function keyboardEvent(target, key) {
  return {
    target, key, ctrlKey: false, altKey: false, metaKey: false,
    composedPath: () => [target, target.document.body],
    preventDefault() { this.defaultPrevented = true; }, defaultPrevented: false
  };
}

function functionSource(name, next) {
  const start = app.indexOf(`  function ${name}`);
  const end = app.indexOf(`  function ${next}`, start);
  return app.slice(start, end);
}

function loadKeyboardHandler(document, ui, options = {}) {
  let handler = null;
  document.addEventListener = (type, listener) => { if (type === 'keydown') handler = listener; };
  const start = app.indexOf('  function isEditableKeyboardEvent');
  const end = app.indexOf('  window.receiveFromNative', start);
  const source = app.slice(start, end);
  const controls = {
    tooltip: { classList: { contains: () => false } },
    'context-menu': { classList: { contains: () => false } },
    'search-results': { classList: { add() {} } },
    search: { focus() {} },
    'filter-bar': { querySelector: () => ({ focus() {} }) },
    'seat-name': { focus() {} }
  };
  Function(
    'document', 'HTMLInputElement', 'HTMLTextAreaElement', 'HTMLSelectElement', '$', 'ui', 'appState',
    'hideContextMenu', 'hidePreview', 'closeMoreMenu', 'renderProblems', 'setSelectionMode', 'setStatus',
    'clearBulkSelection', 'closePlannerPanel', 'plannerState', 'closeDetailPanel', 'render',
    'saveClusterCardShapes', 'refreshManagedAreaCard', 'showMessage', 'adjacentSeat', 'selectSeat',
    'centerSelectedSeat', 'handleEscape', 'movePlacementCursor', 'confirmPlacementCursor',
    `${source}\nreturn null;`
  )(
    document, FakeInput, FakeTextarea, FakeSelect, id => controls[id], ui,
    { viewMode: 'map', selectedProblemId: null, selectedWorkspaces: { size: 0 } },
    () => {}, () => {}, () => {}, () => {}, () => {}, () => {}, () => {}, () => {}, () => ({ status: 'idle' }),
    () => {}, () => {}, () => {}, () => {}, () => {}, options.adjacentSeat ?? (() => null),
    options.selectSeat ?? (() => {}), options.centerSelectedSeat ?? (() => {}), () => false,
    options.movePlacementCursor ?? (() => false), options.confirmPlacementCursor ?? (() => false)
  );
  return handler;
}

function loadBeginMoveMode(dependencies) {
  const source = functionSource('beginMoveMode', 'movePlacementCursor');
  return Function('currentSeat', 'setAddMode', 'ui', 'setViewMode', 'renderPlacementCursor', 'setStatus', 'wrap', `${source}\nreturn beginMoveMode;`)(
    dependencies.currentSeat, dependencies.setAddMode, dependencies.ui, dependencies.setViewMode,
    dependencies.renderPlacementCursor, dependencies.setStatus, dependencies.wrap
  );
}

function loadConfirmPlacement(ui, events) {
  const start = app.indexOf('  function confirmPlacementCursor');
  const end = app.indexOf('  const clamp', start);
  const source = app.slice(start, end);
  return Function('ui', 'clearPlacementCursor', 'moveWorkspace', 'managedArea', 'setAddMode', 'notify', 'send', 'payloadForScenario', `${source}\nreturn confirmPlacementCursor;`)(
    ui,
    () => {},
    (...args) => { events.moves.push(args); return true; },
    id => id === 'A-1' ? { id, mapId: 'sur' } : null,
    active => { events.addMode = active; },
    () => {},
    (...args) => { events.sends.push(args); return true; },
    value => value
  );
}

function loadPlanClick(ui, events) {
  const source = app
    .match(/  function handlePlanClick[\s\S]*?\n  \$\('plan'\)\.addEventListener/)[0]
    .replace(/\n  \$\('plan'\)\.addEventListener$/, '');
  const plan = { getBoundingClientRect: () => ({ left: 12.5, top: 20.5, width: 1000, height: 500 }) };
  return Function('$', 'ui', 'clearPlacementCursor', 'moveWorkspace', 'managedArea', 'setAddMode', 'notify', 'send', 'payloadForScenario', `${source}\nreturn handlePlanClick;`)(
    id => id === 'plan' ? plan : null, ui,
    kind => { events.cleared = kind; },
    (...args) => { events.moves.push(args); },
    () => null, active => { events.addMode = active; },
    () => {}, (...args) => { events.sends.push(args); }, value => value
  );
}

test('mouse Move activation creates a keyboard cursor without changing click precision', () => {
  const ui = { movingSeat: false, placementCursor: null, seatId: 'S-1' };
  const source = { id: 'S-1', x: .317, y: .5 };
  const begin = loadBeginMoveMode({
    currentSeat: () => source,
    setAddMode: () => {}, ui, setViewMode: () => {}, renderPlacementCursor: () => {}, setStatus: () => {},
    wrap: { focus() {} }
  });
  assert(begin(), 'Move mode did not start');
  assert(ui.movingSeat, 'Move mode was not active');
  equal(ui.placementCursor.x, .317, 'cursor did not begin at the source x');
  const events = { moves: [], sends: [] };
  loadPlanClick(ui, events)({ clientX: 412.75, clientY: 270.5 });
  equal(events.moves.length, 1, 'mouse click did not move');
  equal(events.moves[0][0], 'S-1', 'mouse click changed the moving seat');
  equal(events.moves[0][1], .40025, 'mouse x is not the original pixel coordinate');
  equal(events.moves[0][2], .5, 'mouse y is not the original pixel coordinate');
  equal(events.cleared, 'move', 'mouse click did not clear the keyboard cursor');
});

test('mouse Add retains its click coordinate and command path', () => {
  const ui = { mapId: 'sur', adding: true, addingContext: null };
  const events = { moves: [], sends: [] };
  loadPlanClick(ui, events)({ clientX: 412.75, clientY: 270.5 });
  equal(events.moves.length, 0, 'mouse Add moved a seat');
  equal(events.sends.length, 1, 'mouse Add did not send a create command');
  equal(events.sends[0][0], 'createSeat', 'mouse Add command');
  equal(events.sends[0][1].x, .40025, 'mouse Add x is not the original pixel coordinate');
  equal(events.sends[0][1].y, .5, 'mouse Add y is not the original pixel coordinate');
  equal(events.addMode, false, 'mouse Add mode was not closed');
});

test('placement arrows take priority over ordinary seat navigation and Enter confirms', () => {
  const document = { body: {}, documentElement: {}, querySelector: () => null };
  const map = { tabIndex: 0, document, closest: selector => selector === '#mapwrap' ? map : null };
  const ui = { singleKeyShortcutsEnabled: true, placementCursor: { kind: 'move' } };
  let moved = 0;
  let confirmed = 0;
  let adjacent = 0;
  let selected = 0;
  const handler = loadKeyboardHandler(document, ui, {
    movePlacementCursor: direction => { moved++; equal(direction, 'ArrowRight', 'wrong placement direction'); return true; },
    confirmPlacementCursor: () => { confirmed++; return true; },
    adjacentSeat: () => { adjacent++; return { id: 'other' }; },
    selectSeat: () => { selected++; }
  });
  const arrow = keyboardEvent(map, 'ArrowRight');
  handler(arrow);
  equal(moved, 1, 'placement arrow was not handled');
  equal(adjacent, 0, 'placement arrow reached adjacentSeat');
  equal(selected, 0, 'placement arrow changed ordinary selection');
  assert(arrow.defaultPrevented, 'placement arrow did not prevent default scrolling');
  const enter = keyboardEvent(map, 'Enter');
  handler(enter);
  equal(confirmed, 1, 'Enter did not confirm placement');
  ui.placementCursor = null;
  handler(keyboardEvent(map, 'ArrowRight'));
  equal(adjacent, 1, 'ordinary navigation did not return after placement');
  equal(selected, 1, 'ordinary navigation did not select the adjacent seat');
});

test('Enter confirms the actual Move and Add commands for the cursor coordinate', () => {
  const moveEvents = { moves: [], sends: [] };
  const move = loadConfirmPlacement({ placementCursor: { kind: 'move', seatId: 'S-1', x: .5, y: .25 } }, moveEvents);
  assert(move(), 'Move confirmation failed');
  equal(moveEvents.moves.length, 1, 'Move command count');
  equal(moveEvents.moves[0][0], 'S-1', 'Move seat id');
  equal(moveEvents.moves[0][1], .5, 'Move x');
  equal(moveEvents.moves[0][2], .25, 'Move y');

  const addEvents = { moves: [], sends: [] };
  const add = loadConfirmPlacement({
    placementCursor: { kind: 'add', x: 12.5 / 24, y: 9.5 / 18 },
    addingContext: { targetManagedAreaId: 'A-1' }, mapId: 'sur'
  }, addEvents);
  assert(add(), 'Add confirmation failed');
  equal(addEvents.sends.length, 1, 'Add command count');
  equal(addEvents.sends[0][0], 'createSeat', 'Add command');
  equal(addEvents.sends[0][1].x, 12.5 / 24, 'Add x');
  equal(addEvents.sends[0][1].y, 9.5 / 18, 'Add y');
  equal(addEvents.sends[0][1].targetManagedAreaId, 'A-1', 'Add managed-area context');
  equal(addEvents.addMode, false, 'Add mode was not closed after confirmation');
});

test('keyboard destination announcement uses the existing polite toast', () => {
  assert(html.includes('id="toast" class="toast hidden" role="status" aria-live="polite"'), 'existing toast live region missing');
  assert(app.includes('toast.textContent = `${action}: destino ${cell}.'), 'cursor destination is not announced');
  assert(!html.includes('id="grid-cursor" role="status"'), 'cursor introduced a second live region');
});

test('cursor remains visual-only and supports forced colors', () => {
  assert(html.includes('id="grid-cursor" class="grid-cursor hidden" aria-hidden="true"'), 'visual cursor missing');
  assert(css.includes('.grid-cursor') && css.includes('pointer-events: none'), 'cursor can intercept the mouse');
  assert(css.includes('border-color: Highlight') && css.includes('forced-color-adjust: auto'), 'cursor lacks forced-colors support');
});

test('Move has no dependency on adjacentSeat and Add uses the shared grid cursor', () => {
  const move = functionSource('beginMoveMode', 'movePlacementCursor');
  const add = app.match(/function setAddMode[\s\S]*?\n  function openCreateWorkspaceFlow/)[0];
  assert(!move.includes('adjacentSeat'), 'Move still navigates nearby seats');
  assert(add.includes('gridCursorHelpers.initialAddCursor(grid())'), 'Add does not start on the configured grid');
  assert(app.includes("ui.placementCursor && event.key === 'Enter'"), 'keyboard confirmation missing');
  assert(app.includes('cancelPlacementMode();'), 'Escape does not cancel placement');
});

let passed = 0;
for (const item of tests) {
  try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); }
}
console.log(`Keyboard placement harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
