'use strict';
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const project = fs.readFileSync(path.join(root, 'PlanoOpenSpaceIT.Windows.csproj'), 'utf8');
const html = fs.readFileSync(path.join(root, 'Resources', 'index.html'), 'utf8');
const test = require('node:test');
const assert = require('node:assert/strict');

test('embedded JavaScript keeps the js path expected by index.html', () => {
  assert(project.includes('<LogicalName>$(RootNamespace).Resources.%(RecursiveDir)%(Filename)%(Extension)</LogicalName>'), 'JavaScript resource path preserves its existing js directory once');
  assert(!project.includes('$(RootNamespace).Resources.js/%(RecursiveDir)'), 'JavaScript resource path duplicates js during extraction');
  assert(html.includes('<script src="js/core/app.js"></script>'), 'index must load the coordinator from js/core');
});

test('the contextual layers disclosure has valid summary markup', () => {
  assert(html.includes('<summary>Capas</summary>'), 'layers summary is missing');
  assert(!html.includes('<summary>Capas</summary></summary>'), 'layers summary has an extra closing tag');
});
