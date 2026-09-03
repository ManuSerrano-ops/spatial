(() => {
  'use strict';

  const clamp = (value, minimum, maximum) => Math.min(maximum, Math.max(minimum, value));
  const normalizeGrid = definition => ({
    columns: Math.max(1, Math.floor(Number(definition?.columns) || 24)),
    rows: Math.max(1, Math.floor(Number(definition?.rows) || 18))
  });
  const columnName = index => {
    let name = '';
    for (index++; index > 0; index = Math.floor((index - 1) / 26)) name = String.fromCharCode(65 + (index - 1) % 26) + name;
    return name;
  };
  const cellAt = (point, definition) => {
    const grid = normalizeGrid(definition);
    return {
      column: clamp(Math.floor(Number(point?.x) * grid.columns), 0, grid.columns - 1),
      row: clamp(Math.floor(Number(point?.y) * grid.rows), 0, grid.rows - 1)
    };
  };
  const centerOf = (cell, definition) => {
    const grid = normalizeGrid(definition);
    const column = clamp(Math.floor(Number(cell?.column)), 0, grid.columns - 1);
    const row = clamp(Math.floor(Number(cell?.row)), 0, grid.rows - 1);
    return { x: (column + .5) / grid.columns, y: (row + .5) / grid.rows, column, row };
  };
  const labelFor = cell => `${columnName(cell.column)}-${String(cell.row + 1).padStart(2, '0')}`;
  const initialAddCursor = definition => centerOf({
    column: Math.floor(normalizeGrid(definition).columns / 2),
    row: Math.floor(normalizeGrid(definition).rows / 2)
  }, definition);
  const move = (point, direction, definition) => {
    const grid = normalizeGrid(definition);
    const current = cellAt(point, grid);
    const delta = {
      ArrowLeft: { column: -1, row: 0 },
      ArrowRight: { column: 1, row: 0 },
      ArrowUp: { column: 0, row: -1 },
      ArrowDown: { column: 0, row: 1 }
    }[direction];
    if (!delta) return { ...point, ...current, changed: false };
    const next = {
      column: clamp(current.column + delta.column, 0, grid.columns - 1),
      row: clamp(current.row + delta.row, 0, grid.rows - 1)
    };
    if (next.column === current.column && next.row === current.row) return { ...point, ...current, changed: false };
    return { ...centerOf(next, grid), changed: true };
  };

  const api = { normalizeGrid, columnName, cellAt, centerOf, labelFor, initialAddCursor, move };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.GridCursorHelpers = api;
})();
