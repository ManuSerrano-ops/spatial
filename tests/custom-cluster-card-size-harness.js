'use strict';
const fs = require('fs');
const path = require('path');
const edit = require('../Resources/js/features/managed-areas/cluster-card-edit-helpers.js');
const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8');
const css = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'app.css'), 'utf8');
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const equal = (actual, expected, message) => { if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`); };
const normalizeShape = value => ['automatic', 'compact', 'square', 'vertical'].includes(value) ? value : 'automatic';

function dispatchResize(session, start, moves) {
  let next = session;
  for (const pointer of moves) {
    next = edit.updateCardEditDraft(next, {
      width: start.width + pointer.clientX - start.clientX,
      height: start.height + pointer.clientY - start.clientY
    }, normalizeShape);
  }
  return next;
}

test('right-click Edit Shape starts an active in-place session for the clicked area', () => {
  const session = edit.beginCardEdit({ areaId: 'test', record: { shape: 'compact' }, normalizeShape });
  assert(session.active, 'card edit was not activated');
  equal(session.areaId, 'test', 'wrong area entered edit mode');
  equal(session.before, { shape: 'compact' }, 'original presentation was not snapshotted');
  const begin = app.match(/function beginClusterCardEdit[\s\S]*?\n  function updateClusterCardEditDraft/)[0];
  assert(!begin.includes('showModal'), 'beginClusterCardEdit still opens a modal');
  assert(begin.includes('closeDetailPanel({ render: false })'), 'Cluster Detail focus can hide the editable card');
  assert(app.includes("$('context-edit-cluster').onclick") && app.includes('beginClusterCardEdit(areaId)'), 'context action is not wired to the edit session');
});

test('resize handle pointer movement changes only draft dimensions in real time', () => {
  const session = edit.beginCardEdit({ areaId: 'test', record: { shape: 'automatic' }, normalizeShape });
  const resized = dispatchResize(session, { clientX: 20, clientY: 30, width: 160, height: 72 }, [{ clientX: 100, clientY: 70 }]);
  equal([resized.draftWidth, resized.draftHeight], [240, 112], 'pointer deltas did not resize the draft');
  equal([resized.draft.width, resized.draft.height], [240, 112], 'render layout does not receive the draft size');
  equal(session.draftWidth, null, 'pointer movement mutated the original session');
  assert(app.includes('handle.setPointerCapture(event.pointerId)'), 'handle does not capture the pointer');
  assert(app.includes('handle.releasePointerCapture(pointer.pointerId)'), 'handle does not release the pointer');
  assert(app.includes('pointer.preventDefault(); pointer.stopPropagation(); updateClusterCardEditDraft'), 'resize does not block map interactions');
});

test('manual resize is bounded to usable custom-card limits', () => {
  const session = edit.beginCardEdit({ areaId: 'test', record: { shape: 'automatic' }, normalizeShape });
  const minimum = dispatchResize(session, { clientX: 0, clientY: 0, width: 160, height: 80 }, [{ clientX: -1000, clientY: -1000 }]);
  equal([minimum.draftWidth, minimum.draftHeight], [edit.MIN_WIDTH, edit.MIN_HEIGHT], 'minimum bounds');
  const maximum = dispatchResize(session, { clientX: 0, clientY: 0, width: 160, height: 80 }, [{ clientX: 1000, clientY: 1000 }]);
  equal([maximum.draftWidth, maximum.draftHeight], [edit.MAX_WIDTH, edit.MAX_HEIGHT], 'maximum bounds');
});

test('Save persists one manual presentation record and survives rerender', () => {
  let session = edit.beginCardEdit({ areaId: 'test', record: { shape: 'compact' }, normalizeShape });
  session = dispatchResize(session, { clientX: 0, clientY: 0, width: 150, height: 70 }, [{ clientX: 80, clientY: 40 }]);
  const persisted = edit.commitCardEdit(session, normalizeShape);
  equal(persisted, { shape: 'compact', cardSizingMode: 'manual', showMembers: true, cardWidth: 230, cardHeight: 110 }, 'manual record');
  const rerendered = edit.normalizeLayout(persisted, normalizeShape);
  equal([rerendered.width, rerendered.height], [230, 110], 'persisted dimensions were lost on rerender');
  assert(app.includes("localStorage.setItem('plano.clusterCardShapes'"), 'manual record is not persisted');
  assert(app.includes('ui.cardSizeUndo = { areaId: session.areaId'), 'save does not create a single undo snapshot');
});

test('Cancel discards the draft without creating a persistence or undo mutation', () => {
  const persisted = { shape: 'vertical', cardSizingMode: 'manual', cardWidth: 260, cardHeight: 140 };
  let session = edit.beginCardEdit({ areaId: 'test', record: persisted, normalizeShape });
  session = dispatchResize(session, { clientX: 0, clientY: 0, width: 260, height: 140 }, [{ clientX: 90, clientY: 60 }]);
  equal(persisted, { shape: 'vertical', cardSizingMode: 'manual', cardWidth: 260, cardHeight: 140 }, 'draft resized persisted data before Save');
  assert(app.includes('function cancelClusterCardEdit()'), 'cancel helper missing');
  assert(!app.match(/function cancelClusterCardEdit[\s\S]*?function resetClusterCardEditToAutomatic/)[0].includes('saveClusterCardShapes'), 'cancel persists a draft');
});

test('Automatic reset removes manual dimensions and Ctrl+Z restores the one prior record', () => {
  const before = { shape: 'compact', cardSizingMode: 'manual', cardWidth: 230, cardHeight: 110 };
  const shapes = { test: before };
  delete shapes.test;
  assert(!Object.hasOwn(shapes, 'test'), 'automatic reset did not remove the manual record');
  shapes.test = before; // Simulates the single Ctrl+Z snapshot restoration.
  equal(edit.normalizeLayout(shapes.test, normalizeShape), { shape: 'compact', cardSizingMode: 'manual', width: 230, height: 110, anchorX: null, anchorY: null, showMembers: true }, 'Ctrl+Z did not restore prior dimensions');
  assert(app.includes('function resetClusterCardEditToAutomatic()'), 'automatic reset helper missing');
  assert(app.includes('if (ui.cardSizeUndo) { const { areaId, before } = ui.cardSizeUndo;'), 'Ctrl+Z is not connected to the size snapshot');
});

test('rendering exposes the handle and controls only for the active card without CSS size overrides', () => {
  assert(app.includes("ui.cardEdit?.active && ui.cardEdit.areaId === area.id"), 'active-area render guard missing');
  assert(app.includes("handle.className = 'cluster-resize-handle'"), 'resize handle is not created');
  assert(app.includes("controls.className = 'cluster-card-edit-controls'"), 'in-place edit controls are not created');
  assert(css.includes('.managed-area-card.cluster.card-manual { min-width: 0; max-width: none; min-height: 0;'), 'manual sizing can still be overridden by preset CSS');
  assert(css.includes('.cluster-resize-handle {'), 'resize handle has no visual CSS');
});

test('resize and commit preserve viewport and operational cluster data', () => {
  const viewport = { scale: 1.25, x: 120, y: -48 };
  const members = ['W-1', 'W-2', 'W-3'];
  let session = edit.beginCardEdit({ areaId: 'test', record: { shape: 'automatic' }, normalizeShape });
  session = dispatchResize(session, { clientX: 0, clientY: 0, width: 160, height: 72 }, [{ clientX: 100, clientY: 50 }]);
  edit.commitCardEdit(session, normalizeShape);
  equal(viewport, { scale: 1.25, x: 120, y: -48 }, 'resize changed viewport state');
  equal(members, ['W-1', 'W-2', 'W-3'], 'resize changed cluster membership');
  const renderBody = app.match(/function renderManagedAreaCards[\s\S]*?\n  function render\(/)[0];
  assert(!renderBody.includes('moveWorkspace(') && !renderBody.includes('sendManagedArea('), 'card rendering mutates operational data');
});

let passed = 0;
for (const item of tests) {
  try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); }
}
console.log(`Custom cluster card size harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
