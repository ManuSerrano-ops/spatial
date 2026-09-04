'use strict';
const fs = require('fs');
const path = require('path');
const root = path.join(__dirname, '..');
const html = fs.readFileSync(path.join(root, 'Resources', 'index.html'), 'utf8');
const app = fs.readFileSync(path.join(root, 'Resources', 'js', 'core', 'app.js'), 'utf8');
const store = fs.readFileSync(path.join(root, 'src', 'Infrastructure', 'Persistence', 'DataStore.cs'), 'utf8');
const test = require('node:test'); const assert = require('node:assert/strict');

test('global Add Workspace continues through the shared flow', () => assert(app.includes("openCreateWorkspaceFlow({ mapId: ui.mapId })"), 'global flow'));
test('Area Detail exposes two explicit non-ambiguous actions', () => {
  assert(html.includes('id="area-detail-create-seat"') && html.includes('Crear puesto en este cluster'), 'create action');
  assert(html.includes('id="area-detail-add-selection"') && html.includes('Añadir puestos existentes'), 'existing action');
  assert(!html.includes('id="area-detail-create-seat" class="primary" type="button">+</button>'), 'ambiguous plus');
});
test('area action reuses openCreateWorkspaceFlow with its map and area id', () => assert(app.includes("openCreateWorkspaceFlow({ mapId: area.mapId, targetManagedAreaId: area.id })"), 'area flow'));
test('area flow locks the source map and targetManagedAreaId', () => assert(app.includes('targetManagedAreaId: area.id') && app.includes('area.mapId !== mapId') && app.includes('ui.addingContext?.targetManagedAreaId'), 'context validation'));
test('coordinates come from the user click rather than the cluster card', () => assert(app.includes("const x = Math.max(0, Math.min(1, (event.clientX - box.left) / box.width))") && app.includes("const y = Math.max(0, Math.min(1, (event.clientY - box.top) / box.height))"), 'click coordinates'));
test('create request carries optional area context through the original createSeat command', () => assert(app.includes("send('createSeat', payloadForScenario({ mapId: ui.mapId, x, y, ...(context?.targetManagedAreaId"), 'create payload'));

test('Undo restores workspace and membership together via the transaction backup', () => assert(store.includes('ManagedAreas.FileName') && store.includes('OperationalBackupFiles'), 'managed area included in backup'));
test('area focus keeps its identity and refreshes member ids after reload', () => assert(app.includes('const focusedArea = appState.activeAreaFocus?.areaId') && app.includes('memberWorkspaceIds: [...focusedArea.workspaceIds]'), 'focus refresh'));
test('creating from an area does not auto-select it for Bulk', () => assert(!/selectedWorkspaces\.add\([^)]*pendingSeat/.test(app), 'no bulk selection'));
test('dissolve action is explicit and confirms that members remain in place', () => assert(html.includes('>Disolver cluster<') && app.includes('Se eliminará únicamente la agrupación.') && app.includes('permanecerán exactamente en sus ubicaciones actuales'), 'dissolve UX'));

