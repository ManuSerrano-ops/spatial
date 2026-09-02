'use strict';
const { execFileSync } = require('child_process');
const path = require('path');

const root = path.join(__dirname, '..');
const project = path.join(root, 'tests', 'PlanoOpenSpaceIT.Desktop.Tests', 'PlanoOpenSpaceIT.Desktop.Tests.csproj');
try {
  execFileSync('dotnet', ['test', project, '--no-restore', '--filter', 'FullyQualifiedName~ClusterCardRealDragTests.RealDragPreservesCardAndWorkspaceState'], { cwd: root, stdio: 'inherit', timeout: 120000 });
  console.log('cluster-card-real-drag-harness: PASS');
} catch (error) {
  console.error('cluster-card-real-drag-harness: FAIL');
  process.exitCode = 1;
}
