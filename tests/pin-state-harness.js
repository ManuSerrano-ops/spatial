'use strict';

const fs = require('fs');
const path = require('path');
const { derivePinPresentation } = require('../Resources/js/features/map/pin-state-helpers.js');

const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const present = input => derivePinPresentation({ displayLocation: 'F-08', ...input });

test('free business state', () => assert(present({ businessState: 'free' }).businessState === 'free', 'free state missing'));
test('occupied business state', () => assert(present({ businessState: 'occupied' }).businessState === 'occupied', 'occupied state missing'));
test('reserved business state', () => assert(present({ businessState: 'reserved' }).businessState === 'reserved', 'reserved state missing'));
test('occupied with incomplete quality remains occupied', () => { const value = present({ businessState: 'occupied', qualityState: 'partial' }); assert(value.businessState === 'occupied' && value.qualityState === 'incomplete', 'quality replaced business state'); });
test('critical problem has a semantic symbol', () => { const value = present({ problemSeverity: 'Critical', problemCount: 2 }); assert(value.problemSeverity === 'critical' && value.problemSymbol === '×', 'critical presentation invalid'); });
test('warning problem has a semantic symbol', () => assert(present({ problemSeverity: 'Warning' }).problemSymbol === '!', 'warning symbol invalid'));
test('info problem has a semantic symbol', () => assert(present({ problemSeverity: 'Info' }).problemSymbol === 'i', 'info symbol invalid'));
test('selected interaction is independent from business state', () => { const value = present({ businessState: 'occupied', selected: true }); assert(value.interaction.selected && value.businessState === 'occupied', 'selected changed business state'); });
test('search hit is independent and temporary-capable', () => { const value = present({ searchHit: true }); assert(value.interaction.searchHit && value.zIndex === 35, 'search hit priority invalid'); });
test('selected plus critical preserves both layers', () => { const value = present({ selected: true, problemSeverity: 'critical' }); assert(value.interaction.selected && value.problemSymbol === '×' && value.zIndex === 40, 'selected/critical layering invalid'); });
test('scenario moved is visible only in scenario context', () => { assert(present({ isScenario: true, scenarioState: 'MOVED' }).scenarioState === 'moved', 'scenario moved missing'); assert(present({ isScenario: false, scenarioState: 'MOVED' }).scenarioState === 'none', 'reality leaked scenario state'); });
test('planner source has semantic symbol', () => { const value = present({ plannerState: 'source' }); assert(value.plannerSymbol === '●' && value.zIndex === 50, 'planner source invalid'); });
test('planner destination has semantic symbol', () => assert(present({ plannerState: 'destination' }).plannerSymbol === '◎', 'planner destination invalid'));
test('planner blocked has semantic symbol', () => { const value = present({ plannerState: 'blocked' }); assert(value.plannerSymbol === '×' && value.zIndex === 60, 'planner blocked invalid'); });
test('planner source outranks problem', () => assert(present({ plannerState: 'source', problemSeverity: 'critical' }).zIndex === 50, 'planner source did not outrank problem'));
test('planner blocked outranks selection', () => assert(present({ plannerState: 'blocked', selected: true }).zIndex === 60, 'blocked did not outrank selection'));
test('reality does not show scenario state', () => assert(present({ scenarioState: 'ADDED', isScenario: false }).scenarioState === 'none', 'reality scenario marker present'));
test('display location is the primary aria label', () => { const value = present({ displayLocation: 'A-01', businessState: 'occupied', personName: 'Ana' }); assert(value.ariaLabel.startsWith('Puesto A-01, ocupado, Ana'), 'aria label does not prioritize display location'); });
test('aria label includes maximum problem severity', () => assert(present({ problemSeverity: 'critical', problemCount: 1 }).ariaLabel.includes('1 problema crítico'), 'aria problem summary missing'));
test('output is deterministic', () => { const input = { businessState: 'occupied', selected: true, problemSeverity: 'warning', displayLocation: 'D-04' }; assert(JSON.stringify(derivePinPresentation(input)) === JSON.stringify(derivePinPresentation(input)), 'presentation is not deterministic'); });
test('input is not mutated', () => { const input = { businessState: 'reserved', qualityState: 'partial', nested: { keep: true } }; const before = JSON.stringify(input); derivePinPresentation(input); assert(JSON.stringify(input) === before, 'input was mutated'); });
test('CSS keeps fills, overlays and focus separate', () => { const css = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'app.css'), 'utf8'); assert(css.includes('.pin[data-state="occupied"]'), 'business state CSS missing'); assert(css.includes('.pin[data-problem="critical"]'), 'problem overlay CSS missing'); assert(css.includes('.pin[data-planner="blocked"]'), 'planner CSS missing'); assert(css.includes('.pin:focus-visible'), 'focus-visible CSS missing'); assert(css.includes('.problem-symbol') && css.includes('.planner-symbol'), 'contextual states need symbols in addition to color'); assert(!/data-selected="true"\]\s*\{[^}]*background/i.test(css), 'selected must not overwrite business fill'); assert(!/data-problem="critical"[^}]*\{[^}]*background/i.test(css), 'problem must not overwrite business fill'); assert(!css.includes('.pin.occupied.complete'), 'legacy combined pin selector remains'); });

let passed = 0;
for (const { name, fn } of tests) {
  try { fn(); passed++; }
  catch (error) { console.error(`FAIL: ${name}: ${error.message}`); }
}
console.log(`pin-state-harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
