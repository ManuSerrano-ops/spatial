'use strict';
const fs=require('fs');const path=require('path');const app=fs.readFileSync(path.join(__dirname,'..','Resources','js','core','app.js'),'utf8');const html=fs.readFileSync(path.join(__dirname,'..','Resources','index.html'),'utf8');const tests=[];const test=(n,f)=>tests.push({n,f});const assert=(v,m)=>{if(!v)throw new Error(m);};
test('selection panel owns cluster actions',()=>['selection-review-create-cluster','selection-review-add-cluster','selection-review-clear'].forEach(id=>assert(html.includes(`id="${id}"`),id)));
test('create label reflects exact selection count',()=>assert(app.includes('Crear cluster con ${summary.count} puestos'),'count label'));
test('create is hidden below two selections',()=>assert(app.includes("classList.toggle('hidden', summary.count < 2)"),'threshold'));
test('panel and menu share creation flow',()=>assert(app.includes("$('selection-review-create-cluster').onclick = openCreateClusterDialog")&&app.includes("$('context-create-cluster').onclick = () => { hideContextMenu(); openCreateClusterDialog(); }"),'shared flow'));
test('panel add-existing shares existing dialog',()=>assert(app.includes("$('selection-review-add-cluster').onclick = openAddToClusterDialog"),'shared add flow'));
test('bulk header no longer duplicates cluster action',()=>assert(!html.includes('bulk-create-area'),'bulk duplication'));
let p=0;for(const t of tests){try{t.f();p++;}catch(e){console.error(`FAIL: ${t.n}: ${e.message}`)}}console.log(`Selection panel cluster harness: ${p}/${tests.length} passed, ${tests.length-p} failed`);process.exitCode=p===tests.length?0:1;
