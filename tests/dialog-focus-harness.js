'use strict';

const fs = require('fs');
const path = require('path');

const app = fs.readFileSync(
  path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'),
  'utf8'
);
const html = fs.readFileSync(
  path.join(__dirname, '..', 'Resources', 'index.html'), 'utf8'
);
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const equal = (actual, expected, message) => {
  if (actual !== expected) throw new Error(`${message}: ${actual} !== ${expected}`);
};

function createFocusable(document, connected = true) {
  return {
    isConnected: connected,
    focus() { document.activeElement = this; }
  };
}

function createDialog() {
  let closeListener = null;
  return {
    open: false,
    addEventListener(type, listener, options) {
      if (type === 'close' && options?.once) closeListener = listener;
    },
    showModal() { this.open = true; },
    close() {
      this.open = false;
      const listener = closeListener;
      closeListener = null;
      listener?.();
    }
  };
}

function loadFocusHelpers(document, dialogs) {
  const source = app
    .match(/  function captureFocusRestorer[\s\S]*?\n  function hideContextMenu/)[0]
    .replace(/\n  function hideContextMenu$/, '');
  return Function('document', '$', `${source}\nreturn { captureFocusRestorer, openDialog };`)(
    document,
    id => dialogs[id]
  );
}

test('all reachable modal openings use openDialog', () => {
  const reachableDialogs = [
    'add-to-cluster-dialog',
    'create-cluster-dialog',
    'scenario-guide-dialog',
    'scenario-dialog',
    'undo-dialog',
    'history-dialog',
    'backups-dialog',
    'diff-dialog',
    'integrity-dialog',
    'bulk-dialog'
  ];
  reachableDialogs.forEach(id => assert(html.includes(`id="${id}"`), `${id} missing`));
  assert((app.match(/\.showModal\(/g) || []).length === 1, 'direct showModal remains');
  assert(app.includes("function openDialog(id)"), 'openDialog missing');
  assert((app.match(/\bopenDialog\(/g) || []).length === 13, 'not all 12 opening paths use openDialog');
  assert(app.includes("openDialog(button.dataset.dialog)"), 'generic dialog trigger bypasses openDialog');
});

test('close restores the focus captured when the dialog opened', () => {
  const document = { activeElement: null };
  const dialogs = { dialog: createDialog() };
  const { openDialog } = loadFocusHelpers(document, dialogs);
  const opener = createFocusable(document);
  opener.focus();
  equal(openDialog('dialog'), true, 'dialog did not open');
  dialogs.dialog.close();
  assert(document.activeElement === opener, 'focus did not return to the opener');
});

test('a repeated opening preserves the first opener', () => {
  const document = { activeElement: null };
  const dialogs = { dialog: createDialog() };
  const { openDialog } = loadFocusHelpers(document, dialogs);
  const firstOpener = createFocusable(document);
  const secondOpener = createFocusable(document);
  firstOpener.focus();
  equal(openDialog('dialog'), true, 'first opening failed');
  secondOpener.focus();
  equal(openDialog('dialog'), false, 'duplicate opening was accepted');
  dialogs.dialog.close();
  assert(document.activeElement === firstOpener, 'duplicate opening replaced the first opener');
});

test('a disconnected opener is ignored safely', () => {
  const document = { activeElement: null };
  const dialogs = { dialog: createDialog() };
  const { openDialog } = loadFocusHelpers(document, dialogs);
  const opener = createFocusable(document, false);
  document.activeElement = opener;
  openDialog('dialog');
  dialogs.dialog.close();
  assert(document.activeElement === opener, 'closing changed focus without a connected opener');
});

test('the restorer has no dependency on an opener outside another dialog', () => {
  const helper = app.match(/function captureFocusRestorer[\s\S]*?\n  function openDialog/)[0];
  assert(helper.includes('opener = document.activeElement'), 'active element is not captured');
  assert(!helper.includes('closest') && !helper.includes('parentElement'), 'restorer assumes opener location');
});

test('the cluster context menu shares the focus restoration utility', () => {
  const context = app.match(/function hideContextMenu[\s\S]*?\n  function beginClusterCardEdit/)[0];
  assert(context.includes('captureFocusRestorer(opener ?? document.activeElement)'), 'menu does not capture the shared restorer');
  assert(context.includes('if (restoreFocus) restore?.()'), 'menu does not invoke the shared restorer');
});

let passed = 0;
for (const item of tests) {
  try {
    item.fn();
    passed++;
  } catch (error) {
    console.error(`FAIL: ${item.name}: ${error.message}`);
  }
}
console.log(`Dialog focus harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
