'use strict';
const fs=require('fs');const path=require('path');const density=require('../Resources/js/features/map/map-density-helpers.js');const app=fs.readFileSync(path.join(__dirname,'..','Resources','js','core','app.js'),'utf8');const seats=Array.from({length:10},(_,i)=>({id:`W${i}`,x:.4+i*.0001,y:.4+i*.0001}));const test=require('node:test');const assert=require('node:assert/strict');
function result(zoom){return density.buildMapDensityPresentation({mapId:'m',workspaces:seats,grid:{columns:24,rows:18},viewport:{width:200,height:100},zoom});}
test('nearby, colliding and same-cell workspaces remain pins',()=>{for(const zoom of [.1,1,8]){const value=result(zoom);assert(value.clusters.length===0,'automatic card');assert(value.individuals.length===10,'missing individual')}});
test('renderer only draws persistent areas',()=>assert(app.includes('renderManagedAreaCards(pins, map);')&&!app.includes('density.clusters.forEach'),'renderer aggregate path remains'));
