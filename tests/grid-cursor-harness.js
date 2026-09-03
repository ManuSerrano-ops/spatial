'use strict';

const path = require('path');
const cursor = require(path.join(__dirname, '..', 'Resources', 'js', 'features', 'map', 'grid-cursor-helpers.js'));
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const equal = (actual, expected, message) => { if (actual !== expected) throw new Error(`${message}: ${actual} !== ${expected}`); };

const grid = { columns: 24, rows: 18 };

test('the real grid starts Add at the center cell M-10', () => {
  const initial = cursor.initialAddCursor(grid);
  equal(cursor.labelFor(initial), 'M-10', 'initial Add cell');
  equal(initial.x, 12.5 / 24, 'initial Add x');
  equal(initial.y, 9.5 / 18, 'initial Add y');
});

test('the first Move arrow snaps a raw source to the adjacent cell center', () => {
  let value = { x: .317, y: .5 };
  ['ArrowRight', 'ArrowRight', 'ArrowRight', 'ArrowRight', 'ArrowRight'].forEach(direction => { value = cursor.move(value, direction, grid); });
  equal(cursor.labelFor(value), 'M-10', 'five right arrows from H');
  equal(value.x, 12.5 / 24, 'Move coordinate is the M center');
  equal(value.y, 9.5 / 18, 'Move coordinate is the row center');
});

test('left edge retains the original point and reports no false move', () => {
  const source = { x: .01, y: .5 };
  const value = cursor.move(source, 'ArrowLeft', grid);
  assert(!value.changed, 'left edge changed');
  equal(value.x, source.x, 'left edge x');
  equal(value.y, source.y, 'left edge y');
});

test('right edge retains the original point and reports no false move', () => {
  const source = { x: .99, y: .5 };
  const value = cursor.move(source, 'ArrowRight', grid);
  assert(!value.changed, 'right edge changed');
  equal(value.x, source.x, 'right edge x');
  equal(value.y, source.y, 'right edge y');
});

test('top edge retains the original point and reports no false move', () => {
  const source = { x: .5, y: .01 };
  const value = cursor.move(source, 'ArrowUp', grid);
  assert(!value.changed, 'top edge changed');
  equal(value.x, source.x, 'top edge x');
  equal(value.y, source.y, 'top edge y');
});

test('bottom edge retains the original point and reports no false move', () => {
  const source = { x: .5, y: .99 };
  const value = cursor.move(source, 'ArrowDown', grid);
  assert(!value.changed, 'bottom edge changed');
  equal(value.x, source.x, 'bottom edge x');
  equal(value.y, source.y, 'bottom edge y');
});

test('cursor movement follows the configured grid rather than hard-coded dimensions', () => {
  const small = { columns: 3, rows: 2 };
  const initial = cursor.initialAddCursor(small);
  equal(cursor.labelFor(initial), 'B-02', 'dynamic initial cell');
  const moved = cursor.move({ x: .1, y: .1 }, 'ArrowRight', small);
  equal(cursor.labelFor(moved), 'B-01', 'dynamic next cell');
  equal(moved.x, .5, 'dynamic x center');
  equal(moved.y, .25, 'dynamic y center');
});

let passed = 0;
for (const item of tests) {
  try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); }
}
console.log(`Grid cursor harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
