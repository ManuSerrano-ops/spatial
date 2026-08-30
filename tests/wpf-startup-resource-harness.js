'use strict';
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const appXaml = fs.readFileSync(path.join(root, 'src', 'Desktop', 'Host', 'App.xaml'), 'utf8');
const project = fs.readFileSync(path.join(root, 'PlanoOpenSpaceIT.Windows.csproj'), 'utf8');
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };

test('startup URI resolves the compiled MainWindow resource path', () => {
  assert(project.includes('<Page Include="src\\Desktop\\Host\\MainWindow.xaml" />'), 'MainWindow page is not compiled from its source path');
  assert(appXaml.includes('StartupUri="src/Desktop/Host/MainWindow.xaml"'), 'StartupUri does not match the compiled MainWindow resource path');
});

let passed = 0;
for (const item of tests) {
  try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); }
}
console.log(`WPF startup resource harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
