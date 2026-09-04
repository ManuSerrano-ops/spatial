'use strict';
const fs = require('fs');
const path = require('path');
const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8');
const html = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'index.html'), 'utf8');
const test = require('node:test'); const assert = require('node:assert/strict');

test('right-click menu is scoped to the map and only offers creation for two same-map workspaces', () => {
  assert(app.includes("wrap.addEventListener('contextmenu', event => { if (event.target.closest('.map-layers-control')) return; hidePreview(); event.preventDefault(); showContextMenu(event); });"), 'map intercept missing');
  assert(app.includes('const sameMap = selected.length === appState.selectedWorkspaces.size'), 'same-map guard missing');
  assert(app.includes("$('context-create-cluster').classList.toggle('hidden', count < 2)"), 'creation guard missing');
});
test('right-click label reports exact selected workspace count', () => assert(app.includes('Crear cluster con ${count} puestos'), 'dynamic count missing'));
test('dialog requires an explicit trimmed name', () => {
  assert(html.includes('id="create-cluster-name" required'), 'required name input missing');
  assert(app.includes("$('create-cluster-name').value.trim()"), 'name is not trimmed');
  assert(!app.includes('window.prompt(\'Nombre del área gestionada\')'), 'legacy prompt remains');
});
test('duplicate names are rejected within the map', () => assert(app.includes('Ya existe un cluster con ese nombre en este plano.'), 'duplicate-name guard missing'));
test('conflict dialog provides available-only and move choices', () => {
  assert(html.includes('id="create-cluster-available"') && html.includes('id="create-cluster-move"'), 'conflict choices missing');
  assert(app.includes("policy === 'move' ? { moveWorkspaceIds: conflictIds }"), 'move payload missing');
});
test('creation sends workspace IDs and opens the created cluster', () => {
  assert(app.includes("sendManagedArea('create', { mapId: draft.mapId, name, workspaceIds"), 'create command missing');
  assert(app.includes('if (pendingClusterId && managedArea(pendingClusterId)) openAreaDetail(pendingClusterId);'), 'post-create area focus missing');
  assert(!app.includes('personId: draft'), 'person identity must not drive membership');
});
test('cluster detail contains only cluster actions and member actions', () => {
  for (const label of ['Renombrar cluster', 'Crear puesto en este cluster', 'Añadir puestos existentes', 'Fusionar con otro cluster', 'Disolver cluster', 'Quitar del cluster']) assert(html.includes(label) || app.includes(label), `missing ${label}`);
  const detail = html.match(/<section id="area-detail"[\s\S]*?<\/section><\/div><\/aside>/)?.[0] || '';
  for (const forbidden of ['Renombrar zona', 'Eliminar nombre', 'Seleccionar zona', 'Editar apariencia', 'Restablecer']) assert(!detail.includes(forbidden), `obsolete cluster-detail control: ${forbidden}`);
});
test('cluster rename uses the existing managed-area rename transaction', () => {
  assert(html.includes('id="area-detail-rename"'), 'rename button missing');
  assert(html.includes('id="area-detail-rename-form"'), 'rename form missing');
  assert(html.includes('id="area-detail-name" required'), 'required rename input missing');
  assert(app.includes("sendManagedArea('rename', { areaId: area.id, name })"), 'rename command missing');
  assert(app.includes("managedAreas(area.mapId).some(item => item.id !== area.id"), 'rename duplicate-name guard missing');
});
test('manual cards use persistent managed areas and ignore legacy offsets', () => {
  assert(app.includes('managedAreas(map.id).forEach(area =>'), 'persistent managed-area renderer missing');
  assert(app.includes('presentation: { offsetX: 0, offsetY: 0 }'), 'legacy offsets are not ignored');
  assert(app.includes('clusterCardDragHelpers.attachClusterCardMoveHandle({'), 'manual card move handle is not attached only in edit mode');
});
