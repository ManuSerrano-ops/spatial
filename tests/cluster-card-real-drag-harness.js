'use strict';
const { execFileSync } = require('child_process');
const path = require('path');
const test = require('node:test');

const root = path.join(__dirname, '..');
const project = path.join(root, 'tests', 'PlanoOpenSpaceIT.Desktop.Tests', 'PlanoOpenSpaceIT.Desktop.Tests.csproj');

test('real drag preserves card and workspace state', () => {
  execFileSync('dotnet', ['test', project, '--no-restore', '--filter', 'FullyQualifiedName~ClusterCardRealDragTests.RealDragPreservesCardAndWorkspaceState'], { cwd: root, stdio: 'inherit', timeout: 120000 });
});
