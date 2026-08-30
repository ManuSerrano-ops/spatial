'use strict';
const { execFileSync } = require('child_process');
const path = require('path');

const root = path.join(__dirname, '..');
const project = path.join(root, 'tests', 'ClusterCardRealDragHarness', 'ClusterCardRealDragHarness.csproj');
try {
  execFileSync('dotnet', ['run', '--project', project, '--no-restore'], { cwd: root, stdio: 'inherit', timeout: 120000 });
  console.log('cluster-card-real-drag-harness: PASS');
} catch (error) {
  console.error('cluster-card-real-drag-harness: FAIL');
  process.exitCode = 1;
}
