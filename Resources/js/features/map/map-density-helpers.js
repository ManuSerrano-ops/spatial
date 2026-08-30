(() => {
  'use strict';

  const metadataHelpers = typeof module !== 'undefined' && module.exports ? require('./grid-cell-metadata-helpers.js') : window.GridCellMetadataHelpers;
  const array = value => Array.isArray(value) ? value : [];
  const state = value => ['free', 'occupied', 'reserved'].includes(String(value || '').toLowerCase()) ? String(value).toLowerCase() : 'free';
  const freeze = value => Object.freeze(value);
  const ids = value => new Set(array(value).filter(Boolean));

  function buildGridCells({ mapId, workspaces = [], grid = {}, metadata = {}, stateFor = workspace => workspace.effectiveState || workspace.state, problemsFor = () => 0 } = {}) {
    const cells = new Map();
    array(workspaces).forEach(workspace => {
      const resolved = metadataHelpers.cellAt(workspace.x, workspace.y, grid);
      const cell = cells.get(resolved.cellId) || { mapId, cellId: resolved.cellId, column: resolved.column, row: resolved.row, members: [] };
      cell.members.push(workspace); cells.set(resolved.cellId, cell);
    });
    return freeze([...cells.values()].sort((left, right) => left.row - right.row || left.column - right.column).map(cell => {
      const composition = { total: cell.members.length, free: 0, occupied: 0, reserved: 0, problems: 0 };
      cell.members.forEach(workspace => { composition[state(stateFor(workspace))]++; composition.problems += Math.max(0, Number(problemsFor(workspace)) || 0); });
      const customName = metadataHelpers.labelFor(metadata, cell.mapId, cell.cellId);
      return freeze({ ...cell, identity: metadataHelpers.cellIdentity(cell.mapId, cell.cellId), members: freeze([...cell.members]), customName, composition: freeze(composition) });
    }));
  }

  function collisionPairs(workspaces = [], viewport = {}, zoom = 1, pinDiameter = 20, margin = 7) {
    const width = Math.max(1, Number(viewport.width) || 1000); const height = Math.max(1, Number(viewport.height) || 700); const threshold = (Number(pinDiameter) || 20) + (Number(margin) || 0);
    const values = array(workspaces); let pairs = 0;
    for (let left = 0; left < values.length; left++) for (let right = left + 1; right < values.length; right++) {
      const dx = (Number(values[left].x) - Number(values[right].x)) * width * zoom;
      const dy = (Number(values[left].y) - Number(values[right].y)) * height * zoom;
      if (Math.hypot(dx, dy) < threshold) pairs++;
    }
    return pairs;
  }

  function deriveDensityMode(cell, input = {}) {
    const semanticZoom = String(input.semanticZoom || 'GLOBAL').toUpperCase(); const previous = input.previous || 'individual'; const members = array(cell?.members);
    if (members.length <= 1) return 'individual';
    const collisions = collisionPairs(members, input.viewport, input.zoom, input.pinDiameter, input.pinMargin); const dense = collisions > 0;
    if (semanticZoom === 'GLOBAL') return dense || members.length > 1 ? 'cluster' : 'individual';
    if (semanticZoom === 'DETAIL') return dense && members.length > 10 ? 'cluster' : 'individual';
    return dense || (previous === 'cluster' && members.length > 1) ? 'cluster' : 'individual';
  }

  function deriveClusterFunctionalPresentation(cell, context = {}) {
    const members = array(cell?.members); const forced = ids(context.forcedIndividualIds); const selected = ids(context.selectedIds); const areaFocus = ids(context.areaFocusIds); const search = ids(context.searchIds); const sources = ids(context.plannerSourceIds); const destinations = ids(context.plannerDestinationIds); const problems = ids(context.problemIds); const changed = ids(context.changedIds); const relevant = members.filter(member => forced.has(member.id));
    const visibleSelected = members.filter(member => selected.has(member.id)).length;
    const availableDestinations = members.filter(member => destinations.has(member.id)).length;
    const problemCount = members.filter(member => problems.has(member.id)).length;
    const changedCount = members.filter(member => changed.has(member.id)).length;
    const focus = search.size || areaFocus.size || selected.size || sources.size || destinations.size || problems.size || changed.size;
    return freeze({ forcedIds: freeze(relevant.map(member => member.id)), residualIds: freeze(members.filter(member => !forced.has(member.id)).map(member => member.id)), selectedCount: visibleSelected, availableDestinations, problemCount, changedCount, heatmap: Boolean(context.heatmap), dimmed: Boolean(focus && !relevant.length && !visibleSelected && !availableDestinations && !problemCount && !changedCount), priority: search.size || areaFocus.size ? 'search' : sources.size || destinations.size ? 'planner' : selected.size ? 'selection' : problems.size ? 'problems' : changed.size ? 'changes' : 'normal' });
  }

  function buildClusterPresentation(cell, functional = {}) {
    const counts = cell.composition || { total: 0, free: 0, occupied: 0, reserved: 0, problems: 0 }; const name = cell.customName || cell.cellId; const secondary = cell.customName ? cell.cellId : '';
    const parts = functional.heatmap ? [`${counts.total} puestos`] : [`${counts.occupied} ocupados`, `${counts.free} libres`, `${counts.reserved} reservados`].filter(part => !part.startsWith('0 '));
    if (counts.problems) parts.push(`! ${counts.problems} problemas`); if (functional.selectedCount) parts.push(`${functional.selectedCount} seleccionados`); if (functional.availableDestinations) parts.push(`${functional.availableDestinations} destinos disponibles`); if (functional.changedCount) parts.push(`${functional.changedCount} cambios`);
    return freeze({ mapId: cell.mapId, cellId: cell.cellId, identity: cell.identity, label: name, secondaryLabel: secondary, total: counts.total, composition: freeze({ ...counts }), memberIds: freeze(cell.members.map(item => item.id)), x: cell.members.reduce((sum, item) => sum + Number(item.x || 0), 0) / Math.max(1, cell.members.length), y: cell.members.reduce((sum, item) => sum + Number(item.y || 0), 0) / Math.max(1, cell.members.length), detail: parts.join(' · '), functional, ariaLabel: `${name}${secondary ? `, ${secondary}` : ''}, ${counts.total} puestos, ${counts.occupied} ocupados, ${counts.free} libres, ${counts.reserved} reservados${counts.problems ? `, ${counts.problems} problemas` : ''}${functional.selectedCount ? `, ${functional.selectedCount} seleccionados` : ''}${functional.availableDestinations ? `, ${functional.availableDestinations} destinos disponibles` : ''}` });
  }

  function deriveMapFocusPresentation({ workspace, filterVisible = true, searchMatch = false, selected = false, areaFocused = false, plannerState = 'none', problemHighlighted = false, problemMatch = false, changed = false, hasSearch = false, hasSelection = false, hasAreaFocus = false, plannerActive = false, problemsFocused = false, changesFocused = false } = {}) {
    if (!filterVisible) return 'hidden'; const plannerRelevant = plannerState === 'source' || plannerState === 'destination';
    if (searchMatch || areaFocused || plannerRelevant || selected || problemHighlighted || (problemsFocused && problemMatch) || (changesFocused && changed)) return 'highlighted';
    if ((hasSearch || hasSelection || hasAreaFocus || plannerActive || problemsFocused || changesFocused) && workspace) return 'dimmed'; return 'normal';
  }

  function buildMapDensityPresentation(input = {}) {
    const cells = buildGridCells(input); const individuals = array(input.workspaces); const modes = Object.fromEntries(cells.map(cell => [cell.identity, 'individual']));
    // Cells remain useful for grid metadata and focus, but never create visual clusters.
    return freeze({ cells, individuals: freeze(individuals), clusters: freeze([]), modes: freeze(modes), focus: freeze({}) });
  }

  const api = { buildGridCells, collisionPairs, deriveDensityMode, deriveClusterFunctionalPresentation, buildClusterPresentation, deriveMapFocusPresentation, buildMapDensityPresentation };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.MapDensityHelpers = api;
})();
