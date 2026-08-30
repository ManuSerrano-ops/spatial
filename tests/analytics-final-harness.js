'use strict';
const fs = require('fs');
const path = require('path');
const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8');
const html = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'index.html'), 'utf8');
const engine = fs.readFileSync(path.join(__dirname, '..', 'src', 'Domain', 'Analytics', 'SpatialAnalyticsEngine.cs'), 'utf8');
const tests = []; const test = (name, fn) => tests.push({ name, fn }); const assert = (value, message) => { if (!value) throw new Error(message); };

test('analytics uses configured maps rather than workspace groups', () => {
  assert(engine.includes('maps["maps"]'), 'configured map registry is not used');
  assert(!engine.includes('seats.GroupBy'), 'analytics still derives map rows from seats');
});
test('zero-seat map rates render as an em dash', () => assert(app.includes("metric.seats?.total ? `${metric.seats.occupancyRate || 0}%` : '—'"), 'zero-seat occupancy missing'));
test('empty analytics error does not render a visible banner', () => {
  assert(app.includes("const errorText = String(analytics.error ?? '').trim()"), 'error is not trimmed');
  assert(app.includes("analytics.status !== 'error' || !errorText"), 'empty error banner guard missing');
});
test('analytics view centrally hides map-only surfaces', () => {
  assert(app.includes("const mapSurface = mode === 'map'; const workspaceSurface = mode === 'map' || mode === 'list';"), 'central view scope missing');
  for (const id of ['mapwrap', 'pin-legend', 'heatmap-legend', 'bulk-bar']) assert(app.includes(`$('${id}')`), `missing view control for ${id}`);
  assert(app.includes("document.querySelector('.view-toolbar')?.classList.toggle('hidden', !workspaceSurface)"), 'map toolbar is not centrally scoped');
});
test('heatmap cannot render outside the map', () => assert(app.includes("appState.viewMode === 'map' && appState.layers.heatmap"), 'heatmap view guard missing'));
test('analytics includes a persistent-cluster table with navigation', () => {
  assert(html.includes('id="analytics-managed-areas"'), 'cluster analytics region missing');
  for (const heading of ['Cluster', 'Plano', 'Total', 'Ocupados', 'Libres', 'Reservados', 'Ocupación', 'Problemas']) assert(app.includes(`<th>${heading}</th>`), `cluster metric ${heading} missing`);
  assert(app.includes('row.querySelector(\'button\').onclick = () => openAreaDetail(area.id);'), 'cluster navigation missing');
});
test('analytics problems reuse semantic problem rows and table formatting travels with the table', () => {
  assert(html.includes('<ul id="analytics-problems-list" class="analytics-problems-list"></ul>'), 'analytics problems are not a list');
  assert(app.includes("button.className = `problem-row severity-${problem.severity.toLowerCase()}`"), 'analytics problems do not reuse problem rows');
  assert(app.includes("const item = document.createElement('li')"), 'analytics problems lack list items');
  assert(html.includes('id="analytics-table" class="analytics-table"'), 'map table lacks shared table class');
  assert(app.includes("table.className = 'analytics-table'"), 'cluster table lacks shared table class');
  const css = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'app.css'), 'utf8');
  assert(css.includes('.analytics-table th, .analytics-table td'), 'cell formatting remains tied to scroll container');
  assert(!css.includes('.analytics-table-scroll th, .analytics-table-scroll td'), 'scroll container still owns table formatting');
});
let passed = 0; for (const item of tests) { try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); } }
console.log(`Analytics final harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`); process.exitCode = passed === tests.length ? 0 : 1;
