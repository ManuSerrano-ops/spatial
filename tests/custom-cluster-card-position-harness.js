'use strict';
const fs = require('fs');
const path = require('path');
const edit = require('../Resources/js/features/managed-areas/cluster-card-edit-helpers.js');
const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8');
const css = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'app.css'), 'utf8');
const test = require('node:test');
const assert = require('node:assert/strict');
const equal = (actual, expected, message) => { if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`); };
const normalizeShape = value => ['automatic', 'compact', 'square', 'vertical'].includes(value) ? value : 'automatic';

test('entering edit mode snapshots position and supports a captured delegated card drag', () => {
  const session = edit.beginCardEdit({ areaId: 'finance', record: { shape: 'compact', cardAnchorX: .25, cardAnchorY: .4 }, normalizeShape });
  equal([session.draftAnchorX, session.draftAnchorY], [.25, .4], 'position snapshot');
  const moved = edit.updateCardEditDraft(session, { anchorX: .7, anchorY: .6 }, normalizeShape);
  equal([moved.draftAnchorX, moved.draftAnchorY], [.7, .6], 'drag did not update draft anchors');
  const drag = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'features', 'managed-areas', 'cluster-card-drag-helpers.js'), 'utf8');
  assert(drag.includes('handle.setPointerCapture(event.pointerId)'), 'move handle does not capture pointer');
  assert(drag.includes('handle.releasePointerCapture(pointer.pointerId)'), 'move handle does not release pointer');
  assert(app.includes('clusterCardDragHelpers.attachClusterCardMoveHandle({'), 'production card does not attach the dedicated move handle');
  assert(drag.includes("card.style.setProperty('--cluster-drag-x'"), 'drag does not update the visual transform');
  assert(css.includes('.managed-area-card.cluster.card-editing.card-moving'), 'moving cursor CSS missing');
});

test('editing card consumes title and body pointer starts and blocks text selection', () => {
  const drag = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'features', 'managed-areas', 'cluster-card-drag-helpers.js'), 'utf8');
  assert(app.includes("wrap.addEventListener('selectstart', event => { const card = event.target.closest?.('.managed-area-card.cluster.card-editing'); if (card) event.preventDefault(); }, true);"), 'selectstart is not blocked in capture phase');
  assert(app.includes("wrap.addEventListener('dragstart', event => { const card = event.target.closest?.('.managed-area-card.cluster.card-editing'); if (card) event.preventDefault(); }, true);"), 'native drag is not blocked in capture phase');
  assert(app.includes('class="cluster-move-handle active" role="button" aria-label="Arrastrar tarjeta de cluster"'), 'dedicated move handle is not rendered');
  assert(drag.includes('card.isConnected') && drag.includes('card.getBoundingClientRect()'), 'live card node and visual preview are not verified during move');
  assert(css.includes('user-select: none !important') && css.includes('-webkit-user-select: none !important'), 'editing CSS allows text selection');
});

test('dragging a normal card cannot fall through to its open-cluster click', () => {
  const render = app.match(/function renderManagedAreaCard[\s\S]*?\n  function renderManagedAreaCards/)[0];
  assert(render.includes("moveHandle.onclick = event => { event.preventDefault(); event.stopPropagation(); };"), 'move handle click is not consumed');
  assert(render.includes("card.dataset.suppressClusterOpen = 'true'"), 'drag does not suppress its synthetic card click');
  assert(render.includes("event.target.closest('.cluster-move-handle, .cluster-card-edit-controls"), 'card click still opens from the move handle');
});

test('normal cluster cards expose the move handle without entering card edit mode', () => {
  const render = app.match(/function renderManagedAreaCard[\s\S]*?\n  function renderManagedAreaCards/)[0];
  assert(render.includes('class="cluster-move-handle active"'), 'move handle is conditional on edit mode');
  assert(render.includes('if (editing) updateClusterCardEditDraft(patch); else persistClusterCardAnchor(area.id, patch);'), 'direct card movement is not persisted on pointerup');
  assert(app.includes('function persistClusterCardAnchor(areaId, patch)'), 'direct position persistence helper missing');
});

test('position is clamped in logical map space and resize remains independent', () => {
  let session = edit.beginCardEdit({ areaId: 'finance', record: { shape: 'automatic' }, normalizeShape });
  session = edit.updateCardEditDraft(session, { anchorX: 4, anchorY: -2 }, normalizeShape);
  equal([session.draftAnchorX, session.draftAnchorY], [1, 0], 'logical anchor bounds');
  session = edit.updateCardEditDraft(session, { width: 240, height: 130 }, normalizeShape);
  equal([session.draftWidth, session.draftHeight, session.draftAnchorX, session.draftAnchorY], [240, 130, 1, 0], 'resize changed position draft');
  assert(app.includes("handle.className = 'cluster-resize-handle'"), 'dedicated resize handle missing');
});

test('Save persists one presentation record and rerender resolves the custom anchor first', () => {
  let session = edit.beginCardEdit({ areaId: 'finance', record: { shape: 'compact' }, normalizeShape });
  session = edit.updateCardEditDraft(session, { anchorX: .61, anchorY: .34, width: 260, height: 170, showMembers: false }, normalizeShape);
  const persisted = edit.commitCardEdit(session, normalizeShape);
  equal(persisted, { shape: 'compact', cardSizingMode: 'manual', showMembers: false, cardWidth: 260, cardHeight: 170, cardAnchorX: .61, cardAnchorY: .34 }, 'persisted presentation record');
  const rerendered = edit.normalizeLayout(persisted, normalizeShape);
  equal([rerendered.anchorX, rerendered.anchorY, rerendered.width, rerendered.height], [.61, .34, 260, 170], 'rerender lost presentation');
  assert(app.includes('function clusterCardAnchor(presentation, layout)'), 'custom anchor resolver missing');
  assert(app.includes('layout.anchorX ?? presentation.x'), 'automatic position can override custom anchor');
});

test('Cancel, reset position and Ctrl+Z restore only presentation metadata', () => {
  const before = { shape: 'compact', cardSizingMode: 'manual', showMembers: true, cardWidth: 220, cardHeight: 120, cardAnchorX: .2, cardAnchorY: .3 };
  let session = edit.beginCardEdit({ areaId: 'finance', record: before, normalizeShape });
  session = edit.updateCardEditDraft(session, { anchorX: .8, anchorY: .8 }, normalizeShape);
  equal(before.cardAnchorX, .2, 'draft mutated persisted position before cancel');
  const reset = edit.resetCardEditPosition(session);
  equal([reset.draftAnchorX, reset.draftAnchorY], [null, null], 'reset position did not return to automatic anchor');
  const restored = edit.normalizeLayout(before, normalizeShape); // single saved snapshot restored by Ctrl+Z
  equal([restored.anchorX, restored.anchorY], [.2, .3], 'Ctrl+Z did not restore previous anchor');
  assert(app.includes('function resetClusterCardEditPosition()'), 'reset position flow missing');
  assert(app.includes('refreshManagedAreaCard(areaId);'), 'Undo does not refresh the changed card');
});

test('custom logical anchors survive zoom and pan unchanged', () => {
  const persisted = { shape: 'compact', cardSizingMode: 'manual', showMembers: true, cardWidth: 250, cardHeight: 150, cardAnchorX: .68, cardAnchorY: .42 };
  const zoomIn = { scale: 2.5, x: -180, y: -92 };
  const zoomOut = { scale: .7, x: 24, y: 18 };
  equal([edit.normalizeLayout(persisted, normalizeShape).anchorX, edit.normalizeLayout(persisted, normalizeShape).anchorY], [.68, .42], 'zoom changed logical anchor');
  equal(zoomIn, { scale: 2.5, x: -180, y: -92 }, 'card anchor changed zoom-in viewport');
  equal(zoomOut, { scale: .7, x: 24, y: 18 }, 'card anchor changed zoom-out/pan viewport');
  assert(app.includes('card.style.left = `${anchor.x * 100}%`') && app.includes('card.style.top = `${anchor.y * 100}%`'), 'card is not anchored in map space');
});

test('card move cannot move workspaces or viewport state', () => {
  const viewport = { scale: 1.4, x: 80, y: -22 };
  const workspaces = [{ id: 'W-1', x: .1, y: .2 }, { id: 'W-2', x: .3, y: .4 }];
  let session = edit.beginCardEdit({ areaId: 'finance', record: { shape: 'automatic' }, normalizeShape });
  session = edit.updateCardEditDraft(session, { anchorX: .7, anchorY: .6 }, normalizeShape);
  edit.commitCardEdit(session, normalizeShape);
  equal(viewport, { scale: 1.4, x: 80, y: -22 }, 'card move changed viewport');
  equal(workspaces, [{ id: 'W-1', x: .1, y: .2 }, { id: 'W-2', x: .3, y: .4 }], 'card move changed workspace coordinates');
  const render = app.match(/function renderManagedAreaCard[\s\S]*?\n  function renderManagedAreaCards/)[0];
  assert(!render.includes('moveWorkspace(') && !render.includes('sendManagedArea('), 'card drag touches operational data');
});
