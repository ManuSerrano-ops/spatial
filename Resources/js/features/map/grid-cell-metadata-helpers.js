(() => {
  'use strict';

  const clean = value => String(value ?? '').trim();
  const columnName = index => {
    let name = '';
    for (let value = Math.max(0, Number(index) || 0) + 1; value > 0; value = Math.floor((value - 1) / 26)) name = String.fromCharCode(65 + (value - 1) % 26) + name;
    return name;
  };

  function cellAt(x, y, grid = {}) {
    const columns = Math.max(1, Number(grid.columns) || 24);
    const rows = Math.max(1, Number(grid.rows) || 18);
    const column = Math.max(0, Math.min(columns - 1, Math.floor(Math.max(0, Math.min(0.999999, Number(x) || 0)) * columns)));
    const row = Math.max(0, Math.min(rows - 1, Math.floor(Math.max(0, Math.min(0.999999, Number(y) || 0)) * rows)));
    return Object.freeze({ column, row, cellId: `${columnName(column)}-${String(row + 1).padStart(2, '0')}` });
  }

  function cellIdentity(mapId, cellId) { return `${clean(mapId)}|${clean(cellId).toUpperCase()}`; }

  function normalizeMetadata(source = {}) {
    const entries = Array.isArray(source) ? source : Array.isArray(source.cells) ? source.cells : Object.values(source || {});
    const metadata = {};
    entries.forEach(entry => {
      const mapId = clean(entry?.mapId); const cellId = clean(entry?.cellId).toUpperCase(); const customName = clean(entry?.customName);
      if (mapId && cellId && customName) metadata[cellIdentity(mapId, cellId)] = Object.freeze({ mapId, cellId, customName });
    });
    return Object.freeze(metadata);
  }

  function serializeMetadata(source = {}) {
    return Object.freeze(Object.values(normalizeMetadata(source)).sort((left, right) => cellIdentity(left.mapId, left.cellId).localeCompare(cellIdentity(right.mapId, right.cellId))).map(item => Object.freeze({ ...item })));
  }

  function renameCell(source, mapId, cellId, customName) {
    const metadata = { ...normalizeMetadata(source) }; const key = cellIdentity(mapId, cellId); const name = clean(customName);
    if (name) metadata[key] = Object.freeze({ mapId: clean(mapId), cellId: clean(cellId).toUpperCase(), customName: name });
    else delete metadata[key];
    return Object.freeze(metadata);
  }

  function labelFor(source, mapId, cellId) { return normalizeMetadata(source)[cellIdentity(mapId, cellId)]?.customName || ''; }

  function buildCellSearchEntries(cells = [], metadata = {}) {
    return Object.freeze(cells.map(cell => {
      const customName = labelFor(metadata, cell.mapId, cell.cellId);
      return Object.freeze({ type: 'CELDAS', mapId: cell.mapId, cellId: cell.cellId, primaryText: customName || cell.cellId, secondaryText: customName ? `${cell.cellId} · ${cell.members.length} puestos` : `${cell.members.length} puestos`, customName, memberCount: cell.members.length });
    }).filter(entry => entry.customName));
  }

  const api = { columnName, cellAt, cellIdentity, normalizeMetadata, serializeMetadata, renameCell, labelFor, buildCellSearchEntries };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.GridCellMetadataHelpers = api;
})();
