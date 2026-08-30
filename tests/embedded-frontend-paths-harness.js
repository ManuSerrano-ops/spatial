'use strict';
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const project = fs.readFileSync(path.join(root, 'PlanoOpenSpaceIT.Windows.csproj'), 'utf8');
const html = fs.readFileSync(path.join(root, 'Resources', 'index.html'), 'utf8');
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };

test('embedded JavaScript keeps the js path expected by index.html', () => {
  assert(project.includes('<LogicalName>$(RootNamespace).Resources.%(RecursiveDir)%(Filename)%(Extension)</LogicalName>'), 'JavaScript resource path preserves its existing js directory once');
  assert(!project.includes('$(RootNamespace).Resources.js/%(RecursiveDir)'), 'JavaScript resource path duplicates js during extraction');
  assert(html.includes('<script src="js/core/app.js"></script>'), 'index must load the coordinator from js/core');
});

test('the contextual layers disclosure has valid summary markup', () => {
  assert(html.includes('<summary>Capas</summary>'), 'layers summary is missing');
  assert(!html.includes('<summary>Capas</summary></summary>'), 'layers summary has an extra closing tag');
});

let passed = 0;
for (const item of tests) {
  try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); }
}
console.log(`Embedded frontend paths harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
