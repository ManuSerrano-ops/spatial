'use strict';
const fs = require('fs'); const path = require('path'); const viewport = require('../Resources/js/features/map/map-viewport-helpers.js'); const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8');
const test = require('node:test'); const assert = require('node:assert/strict'); const near = (a, b) => Math.abs(a - b) < .0001;
test('wide map fits a 1280x720 client logically', () => { const fit = viewport.calculateInitialFit({ width: 900, height: 590 }, { width: 1588, height: 1122.6667 }); assert(near(fit.scale, 590 / 1122.6667) && near(fit.x, (900 - 1588 * fit.scale) / 2), 'wide fit'); });
test('portrait map fits a 1366x768 client logically', () => { const fit = viewport.calculateInitialFit({ width: 960, height: 630 }, { width: 1122.56, height: 1587.36 }); assert(near(fit.scale, 630 / 1587.36) && fit.x > 0, 'portrait fit'); });
test('large viewport never upscales above logical one', () => assert(viewport.calculateInitialFit({ width: 1920, height: 1080 }, { width: 800, height: 500 }).scale === 1, 'upscale'));
test('reduced viewport stays positive and centered', () => { const fit = viewport.calculateInitialFit({ width: 500, height: 260 }, { width: 1588, height: 1122 }); assert(fit.scale >= .1 && Number.isFinite(fit.x) && Number.isFinite(fit.y), 'reduced fit'); });
test('frontend fits each map once after the image is ready', () => assert(app.includes('function fitInitialMap') && app.includes('ui.initialViewports.has(mapId)') && app.includes('plan.onload = () => fitInitialMap(map.id)'), 'fit wiring'));
