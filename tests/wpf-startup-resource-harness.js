'use strict';
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const appXaml = fs.readFileSync(path.join(root, 'src', 'Desktop', 'Host', 'App.xaml'), 'utf8');
const project = fs.readFileSync(path.join(root, 'PlanoOpenSpaceIT.Windows.csproj'), 'utf8');
const test = require('node:test');
const assert = require('node:assert/strict');

test('startup URI resolves the compiled MainWindow resource path', () => {
  assert(project.includes('<Page Include="src\\Desktop\\Host\\MainWindow.xaml" />'), 'MainWindow page is not compiled from its source path');
  assert(appXaml.includes('StartupUri="src/Desktop/Host/MainWindow.xaml"'), 'StartupUri does not match the compiled MainWindow resource path');
});
