'use strict';
const fs = require('fs');
const path = require('path');
const helpers = require('../Resources/js/features/map/map-density-helpers.js');
const test = require('node:test'); const assert = require('node:assert/strict'); const equal = (actual, expected, message) => { if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`); };
const grid = { columns: 24, rows: 18 };
const nearby = [{ id: 'A', x: .4, y: .4 }, { id: 'B', x: .401, y: .401 }, { id: 'C', x: .402, y: .402 }];
const build = options => helpers.buildMapDensityPresentation({ mapId: 'sur', workspaces: nearby, grid, viewport: { width: 800, height: 500 }, pinDiameter: 20, pinMargin: 7, ...options });

test('no managed areas means no visual cluster cards', () => equal(build({ semanticZoom: 'GLOBAL' }).clusters, [], 'global clusters'));
test('nearby workspaces remain individual', () => equal(build({ semanticZoom: 'GLOBAL' }).individuals.map(item => item.id), ['A', 'B', 'C'], 'nearby pins'));
test('zoom out does not create clusters', () => equal(build({ semanticZoom: 'GLOBAL', zoom: .1 }).clusters.length, 0, 'zoom out'));
test('zoom in does not create clusters', () => equal(build({ semanticZoom: 'DETAIL', zoom: 8 }).clusters.length, 0, 'zoom in'));
test('grid cells do not create clusters', () => equal(build({ semanticZoom: 'OPERATIVE' }).clusters.length, 0, 'grid clusters'));
test('collisions do not create clusters', () => { assert(helpers.collisionPairs(nearby, { width: 800, height: 500 }, 1, 20, 7) > 0, 'fixture needs collision'); equal(build({ semanticZoom: 'GLOBAL' }).clusters.length, 0, 'collision cluster'); });
test('presentation remains deterministic and immutable', () => { const before = JSON.stringify(nearby); equal(build({}), build({}), 'deterministic'); equal(JSON.stringify(nearby), before, 'mutation'); });
test('grid cells remain available as non-cluster metadata', () => equal(helpers.buildGridCells({ mapId: 'sur', workspaces: nearby, grid })[0].composition.total, 3, 'cell composition'));
test('focus presentation remains available for manual cluster focus', () => equal([helpers.deriveMapFocusPresentation({ workspace: {}, hasAreaFocus: true, areaFocused: true }), helpers.deriveMapFocusPresentation({ workspace: {}, hasAreaFocus: true })], ['highlighted', 'dimmed'], 'focus'));
test('frontend does not invoke automatic density presentation', () => { const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8'); assert(!app.includes('buildMapDensityPresentation({'), 'automatic density renderer'); assert(app.includes('renderManagedAreaCards'), 'manual card renderer'); });
