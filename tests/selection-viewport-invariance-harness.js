'use strict';
const fs=require('fs');const path=require('path');const app=fs.readFileSync(path.join(__dirname,'..','Resources','js','core','app.js'),'utf8');const test=require('node:test');const assert=require('node:assert/strict');
test('ordinary pin selection has no navigation call',()=>{const match=app.match(/function selectSeat\([\s\S]*?\n  function renderList/);assert(match&&!match[0].includes('centerSelectedSeat()')&&!match[0].includes('resetViewport()'),'selection changes viewport');});
test('background and bulk clear have no viewport restoration',()=>{const match=app.match(/function clearWorkspaceSelection[\s\S]*?\n  function clearBulkSelection/);assert(match&&!/resetViewport|centerSelectedSeat|fitInitialMap/.test(match[0]),'clear changes viewport');});
test('explicit navigation remains allowed',()=>{const match=app.match(/function focusSeat[\s\S]*?\n  function centerSelectedSeat/);assert(match&&match[0].includes('resetViewport()')&&match[0].includes('centerSelectedSeat()'),'explicit navigation missing');});
test('cluster member inspector does not center',()=>{const match=app.match(/function openAreaMemberInspector[\s\S]*?\n  function updateCellMetadata/);assert(match&&!match[0].includes('centerSelectedSeat()'),'cluster inspector recentered');});
test('background clearing retains pan distinction',()=>assert(app.includes('pan && !pan.moved'),'pan threshold missing'));
