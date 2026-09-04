'use strict';
const fs=require('fs');const path=require('path');const app=fs.readFileSync(path.join(__dirname,'..','Resources','js','core','app.js'),'utf8');const test=require('node:test');const assert=require('node:assert/strict');
test('background closure is centralized and clears area focus',()=>{const fn=app.match(/function handleMapBackgroundClick[\s\S]*?\n  function requestLoad/)[0];assert(fn.includes("clearWorkspaceSelection({ closeAreaFocus: true })"),'does not close focus');assert(!/resetViewport|centerSelectedSeat|fitInitialMap/.test(fn),'moves viewport')});
test('background click only fires after a real non-drag click',()=>assert(app.includes('if (event.button === 0 && pan && !pan.moved) handleMapBackgroundClick()'),'click threshold'));
test('pin members preserve active area focus',()=>assert(app.includes('activeAreaIds.includes(seat.id)'),'member focus preservation'));
test('cluster click switches directly to target area outside edit mode',()=>{const text=app.match(/function renderManagedAreaCard[\s\S]*?\n  function renderManagedAreaCards/)[0];assert(text.includes('openAreaDetail(area.id);')&&text.includes('if (editing || event.target.closest'), 'direct cluster opening')});
test('create and planner modes protect background closing',()=>assert(app.includes("if (ui.adding || ui.moving || plannerState().status !== 'idle') return;"),'mode guard'));
