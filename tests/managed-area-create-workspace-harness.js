'use strict';
const fs = require('fs');
const path = require('path');
const root = path.join(__dirname, '..');
const html = fs.readFileSync(path.join(root, 'Resources', 'index.html'), 'utf8');
const app = fs.readFileSync(path.join(root, 'Resources', 'js', 'core', 'app.js'), 'utf8');
const store = fs.readFileSync(path.join(root, 'src', 'Infrastructure', 'Persistence', 'DataStore.cs'), 'utf8');
const tests = []; const test = (name, fn) => tests.push({ name, fn }); const assert = (value, message) => { if (!value) throw new Error(message); };

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
test('backend validates the target before adding a workspace', () => assert(store.includes('La zona gestionada ya no existe.') && store.includes('La zona gestionada pertenece a otro plano.'), 'target validation'));
test('backend creates seat and membership in one transaction', () => {
  assert(store.includes('ManagedAreas.AddWorkspaces(managedAreas, state["maps"]!.AsObject(), request.TargetManagedAreaId, [id])'), 'membership mutation');
  assert(store.includes('RealFiles.Append(ManagedAreas.FileName)') && store.includes('ExecuteTransactionUnlocked('), 'single transaction');
});
test('area create has one history event and backup contract', () => assert(store.includes('"Puesto creado en zona"') && store.includes('"Antes de puesto creado en zona"'), 'history and backup'));
test('Undo restores workspace and membership together via the transaction backup', () => assert(store.includes('ManagedAreas.FileName') && store.includes('OperationalBackupFiles'), 'managed area included in backup'));
test('area focus keeps its identity and refreshes member ids after reload', () => assert(app.includes('const focusedArea = appState.activeAreaFocus?.areaId') && app.includes('memberWorkspaceIds: [...focusedArea.workspaceIds]'), 'focus refresh'));
test('creating from an area does not auto-select it for Bulk', () => assert(!/selectedWorkspaces\.add\([^)]*pendingSeat/.test(app), 'no bulk selection'));
test('dissolve action is explicit and confirms that members remain in place', () => assert(html.includes('>Disolver cluster<') && app.includes('Se eliminará únicamente la agrupación.') && app.includes('permanecerán exactamente en sus ubicaciones actuales'), 'dissolve UX'));
test('dissolve writes only managed-area state, leaving workspace documents untouched', () => assert(store.includes('MutateManagedAreasUnlocked(source => ManagedAreas.Dissolve') && store.includes('"Cluster disuelto"'), 'dissolve transaction'));
test('creating inside an area is rejected in scenarios to preserve atomicity', () => assert(store.includes('No se puede crear un puesto dentro de una zona gestionada desde un escenario'), 'scenario guard'));

let passed = 0;
for (const item of tests) { try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); } }
console.log(`Managed-area create workspace harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
