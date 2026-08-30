(() => {
  'use strict';

  const clean = value => String(value ?? '').trim();
  const list = value => Array.isArray(value) ? value : [];
  const compare = (left, right) => left.localeCompare(right);
  const uniqueSorted = values => [...new Set(list(values).map(clean).filter(Boolean))].sort(compare);
  const finite = value => Number.isFinite(Number(value)) ? Number(value) : 0;
  const normalizePresentation = value => Object.freeze({ offsetX: finite(value?.offsetX), offsetY: finite(value?.offsetY) });
  const freezeArea = area => Object.freeze({ id: area.id, mapId: area.mapId, name: area.name, workspaceIds: Object.freeze([...area.workspaceIds]), presentation: normalizePresentation(area.presentation) });

  function normalizeState(source = {}) {
    const incoming = Array.isArray(source) ? source : Array.isArray(source.areas) ? source.areas : Array.isArray(source.managedAreas) ? source.managedAreas : source.managedAreas?.areas;
    const areas = list(incoming).map(value => ({
      id: clean(value?.id),
      mapId: clean(value?.mapId),
      name: clean(value?.name),
      workspaceIds: uniqueSorted(value?.workspaceIds),
      presentation: normalizePresentation(value?.presentation || value)
    }));
    const ids = new Set(); const memberships = new Set();
    areas.forEach(area => {
      if (!area.id || !area.mapId || !area.name) throw new Error('Each managed area requires id, mapId, and name.');
      if (ids.has(area.id)) throw new Error(`Managed area id already exists: ${area.id}`);
      ids.add(area.id);
      area.workspaceIds.forEach(workspaceId => {
        const membership = `${area.mapId}\u0000${workspaceId}`;
        if (memberships.has(membership)) throw new Error(`Workspace ${workspaceId} belongs to more than one area on map ${area.mapId}.`);
        memberships.add(membership);
      });
    });
    return Object.freeze({ areas: Object.freeze(areas.sort((left, right) => compare(left.mapId, right.mapId) || compare(left.id, right.id)).map(freezeArea)) });
  }

  const areaById = (state, areaId) => state.areas.find(area => area.id === clean(areaId));
  function requireArea(state, areaId) { const area = areaById(state, areaId); if (!area) throw new Error(`Managed area not found: ${clean(areaId)}`); return area; }
  function requireText(value, field) { const result = clean(value); if (!result) throw new Error(`${field} is required.`); return result; }
  function replaceAreas(state, removeIds, additions) { const removed = new Set(removeIds); return normalizeState({ areas: [...state.areas.filter(area => !removed.has(area.id)), ...additions] }); }
  function membershipOnMap(state, mapId, workspaceId, exceptAreaId = '') { return state.areas.find(area => area.mapId === mapId && area.id !== exceptAreaId && area.workspaceIds.includes(workspaceId)); }
  function assertAvailable(state, mapId, workspaceIds, exceptAreaId = '') { workspaceIds.forEach(workspaceId => { const owner = membershipOnMap(state, mapId, workspaceId, exceptAreaId); if (owner) throw new Error(`Workspace ${workspaceId} already belongs to area ${owner.id} on map ${mapId}.`); }); }

  function snapshot(kind, before, after, details = {}) {
    return Object.freeze({ kind, label: clean(details.label) || kind, before, after, affectedAreaIds: Object.freeze(uniqueSorted(details.areaIds)), affectedWorkspaceIds: Object.freeze(uniqueSorted(details.workspaceIds)) });
  }
  function result(kind, source, mutate, details) { const before = normalizeState(source); const after = mutate(before); return Object.freeze({ state: after, snapshot: snapshot(kind, before, after, details) }); }

  function createArea(source, value = {}) {
    const id = requireText(value.id, 'id'); const mapId = requireText(value.mapId, 'mapId'); const name = requireText(value.name, 'name'); const workspaceIds = uniqueSorted(value.workspaceIds); const presentation = normalizePresentation(value.presentation || value);
    return result('create', source, state => { if (areaById(state, id)) throw new Error(`Managed area id already exists: ${id}`); assertAvailable(state, mapId, workspaceIds); return replaceAreas(state, [], [{ id, mapId, name, workspaceIds, presentation }]); }, { areaIds: [id], workspaceIds, label: `Create managed area ${name}` });
  }
  function renameArea(source, areaId, name) { const nextName = requireText(name, 'name'); return result('rename', source, state => { const area = requireArea(state, areaId); return replaceAreas(state, [area.id], [{ ...area, name: nextName }]); }, { areaIds: [areaId], label: `Rename managed area to ${nextName}` }); }
  function addWorkspaces(source, areaId, workspaceIds) { const additions = uniqueSorted(workspaceIds); return result('add', source, state => { const area = requireArea(state, areaId); assertAvailable(state, area.mapId, additions, area.id); return replaceAreas(state, [area.id], [{ ...area, workspaceIds: uniqueSorted([...area.workspaceIds, ...additions]) }]); }, { areaIds: [areaId], workspaceIds: additions, label: 'Add workspaces to managed area' }); }
  function removeWorkspaces(source, areaId, workspaceIds) { const removals = uniqueSorted(workspaceIds); const removeSet = new Set(removals); return result('remove', source, state => { const area = requireArea(state, areaId); return replaceAreas(state, [area.id], [{ ...area, workspaceIds: area.workspaceIds.filter(id => !removeSet.has(id)) }]); }, { areaIds: [areaId], workspaceIds: removals, label: 'Remove workspaces from managed area' }); }
  function moveWorkspaces(source, fromAreaId, toAreaId, workspaceIds) {
    const moving = uniqueSorted(workspaceIds); const movingSet = new Set(moving);
    return result('move', source, state => { const from = requireArea(state, fromAreaId); const to = requireArea(state, toAreaId); if (from.id === to.id) throw new Error('Source and destination areas must differ.'); if (from.mapId !== to.mapId) throw new Error('Workspaces cannot move between managed areas on different maps.'); moving.forEach(id => { if (!from.workspaceIds.includes(id)) throw new Error(`Workspace ${id} does not belong to area ${from.id}.`); }); return replaceAreas(state, [from.id, to.id], [{ ...from, workspaceIds: from.workspaceIds.filter(id => !movingSet.has(id)) }, { ...to, workspaceIds: uniqueSorted([...to.workspaceIds, ...moving]) }]); }, { areaIds: [fromAreaId, toAreaId], workspaceIds: moving, label: 'Move workspaces between managed areas' });
  }
  function mergeAreas(source, targetAreaId, sourceAreaIds) {
    const sourceIds = uniqueSorted(sourceAreaIds).filter(id => id !== clean(targetAreaId));
    return result('merge', source, state => { const target = requireArea(state, targetAreaId); const sources = sourceIds.map(id => requireArea(state, id)); if (!sources.length) throw new Error('At least one source area is required.'); if (sources.some(area => area.mapId !== target.mapId)) throw new Error('Managed areas on different maps cannot be merged.'); return replaceAreas(state, [target.id, ...sourceIds], [{ ...target, workspaceIds: uniqueSorted([...target.workspaceIds, ...sources.flatMap(area => area.workspaceIds)]) }]); }, { areaIds: [targetAreaId, ...sourceIds], workspaceIds: [], label: 'Merge managed areas' });
  }
  function dissolveArea(source, areaId) { return result('dissolve', source, state => { const area = requireArea(state, areaId); return replaceAreas(state, [area.id], []); }, { areaIds: [areaId], workspaceIds: normalizeState(source).areas.find(area => area.id === clean(areaId))?.workspaceIds || [], label: 'Dissolve managed area' }); }
  function deleteMoveArea(source, fromAreaId, toAreaId) { const state = normalizeState(source); const from = requireArea(state, fromAreaId); return result('deleteMove', state, current => { const moved = moveWorkspaces(current, fromAreaId, toAreaId, from.workspaceIds).state; return replaceAreas(moved, [fromAreaId], []); }, { areaIds: [fromAreaId, toAreaId], workspaceIds: from.workspaceIds, label: 'Delete managed area and move workspaces' }); }
  function restoreSnapshot(operationSnapshot, side = 'before') { if (!operationSnapshot || (side !== 'before' && side !== 'after')) throw new Error('A valid operation snapshot and side are required.'); return normalizeState(operationSnapshot[side]); }

  function deriveAreaPresentation(area, workspaces = [], options = {}) {
    const members = list(workspaces).filter(workspace => area?.workspaceIds?.includes(clean(workspace?.id))); const counts = { total: members.length, free: 0, occupied: 0, reserved: 0, problems: 0 };
    members.forEach(workspace => { const status = clean(options.stateFor?.(workspace) || workspace.effectiveState || workspace.state).toLowerCase(); if (Object.hasOwn(counts, status)) counts[status]++; counts.problems += Math.max(0, Number(options.problemsFor?.(workspace)) || 0); });
    const offsetX = finite(area?.presentation?.offsetX ?? area?.offsetX); const offsetY = finite(area?.presentation?.offsetY ?? area?.offsetY); const x = members.reduce((sum, workspace) => sum + finite(workspace.x), 0) / Math.max(1, members.length) + offsetX; const y = members.reduce((sum, workspace) => sum + finite(workspace.y), 0) / Math.max(1, members.length) + offsetY;
    return Object.freeze({ areaId: clean(area?.id), mapId: clean(area?.mapId), name: clean(area?.name), memberIds: Object.freeze(members.map(workspace => clean(workspace.id))), counts: Object.freeze(counts), x, y, offsetX, offsetY, detail: `${counts.occupied} ocupados · ${counts.free} libres · ${counts.reserved} reservados${counts.problems ? ` · ${counts.problems} problemas` : ''}` });
  }

  const backendActions = Object.freeze({ create: 'createManagedArea', rename: 'renameManagedArea', add: 'addManagedAreaWorkspaces', remove: 'removeManagedAreaWorkspaces', move: 'moveManagedAreaWorkspaces', merge: 'mergeManagedAreas', dissolve: 'dissolveManagedArea', deleteMove: 'deleteMoveManagedArea' });
  function buildBackendCommand(operation, payload = {}) { const action = backendActions[operation]; if (!action) throw new Error(`Unsupported managed area operation: ${operation}`); return Object.freeze({ action, payload: Object.freeze({ ...payload }) }); }

  const api = { normalizeState, createArea, renameArea, addWorkspaces, removeWorkspaces, moveWorkspaces, mergeAreas, dissolveArea, deleteMoveArea, restoreSnapshot, deriveAreaPresentation, buildBackendCommand, backendActions };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.ManagedAreaHelpers = api;
})();
