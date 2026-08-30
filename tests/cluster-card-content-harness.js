'use strict';
const fs = require('fs');
const path = require('path');
const content = require('../Resources/js/features/managed-areas/cluster-card-content-helpers.js');
const edit = require('../Resources/js/features/managed-areas/cluster-card-edit-helpers.js');
const presentation = require('../Resources/js/shared/workspace/workspace-presentation-helpers.js');
const app = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'js', 'core', 'app.js'), 'utf8');
const css = fs.readFileSync(path.join(__dirname, '..', 'Resources', 'app.css'), 'utf8');
const tests = [];
const test = (name, fn) => tests.push({ name, fn });
const assert = (value, message) => { if (!value) throw new Error(message); };
const equal = (actual, expected, message) => { if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error(`${message}: expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`); };
const normalizeShape = value => ['automatic', 'compact', 'square', 'vertical'].includes(value) ? value : 'automatic';
const people = count => Array.from({ length: count }, (_, index) => ({ workspaceId: `W-${index + 1}`, displayLocation: `G-${String(index + 1).padStart(2, '0')}`, currentPersonId: `p-${index + 1}`, currentPerson: `Persona ${index + 1}` }));
const members = [
  { workspaceId: 'W-3', displayLocation: 'G-03', currentPersonId: 'marta', currentPerson: 'Marta López' },
  { workspaceId: 'W-1', displayLocation: 'G-01', currentPersonId: 'ana', currentPerson: 'Ana García' },
  { workspaceId: 'W-2', displayLocation: 'G-02', currentPersonId: 'carlos', currentPerson: 'Carlos Martín' },
  { workspaceId: 'W-4', displayLocation: 'G-04', currentPersonId: null, currentPerson: null }
];

test('small card keeps a limited summary while a 210px card shows all three members', () => {
  const small = content.buildClusterCardMemberContent({ level: content.getClusterCardDetailLevel(160, 80, true), width: 160, height: 80, members });
  equal([small.level, small.visibleMembers.length, small.hiddenCount, small.overflowLabel], ['summary', 1, 2, '+ 2 más'], 'small card capacity');
  const large = content.buildClusterCardMemberContent({ level: content.getClusterCardDetailLevel(240, 210, true), width: 240, height: 210, members });
  equal([large.level, large.showLocations, large.visibleMembers.length, large.hiddenCount, large.overflowLabel], ['members', true, 3, 0, null], '3 members in 210px card');
  equal(large.visibleMembers.map(member => member.currentPerson), ['Ana García', 'Carlos Martín', 'Marta López'], 'deterministic order');
});

test('+N appears only for rows that do not fit', () => {
  const capacityFour = content.buildClusterCardMemberContent({ level: 'members', width: 300, height: 170, members: people(10) });
  equal([capacityFour.visibleMembers.length, capacityFour.hiddenCount, capacityFour.overflowLabel], [4, 6, '+ 6 más'], 'four-row capacity');
  const capacityAll = content.buildClusterCardMemberContent({ level: 'members', width: 300, height: 320, members: people(10) });
  equal([capacityAll.visibleMembers.length, capacityAll.hiddenCount, capacityAll.overflowLabel], [10, 0, null], 'all-member capacity');
});

test('draft resize increases visible users before commit', () => {
  let session = edit.beginCardEdit({ areaId: 'test', record: { shape: 'compact' }, normalizeShape });
  session = edit.updateCardEditDraft(session, { width: 240, height: 80 }, normalizeShape);
  const initial = content.buildClusterCardMemberContent({ level: content.getClusterCardDetailLevel(session.draftWidth, session.draftHeight, true), width: session.draftWidth, height: session.draftHeight, members });
  session = edit.updateCardEditDraft(session, { width: 240, height: 210 }, normalizeShape);
  const expanded = content.buildClusterCardMemberContent({ level: content.getClusterCardDetailLevel(session.draftWidth, session.draftHeight, true), width: session.draftWidth, height: session.draftHeight, members });
  equal([initial.visibleMembers.length, initial.hiddenCount], [1, 2], 'initial small draft');
  equal([expanded.visibleMembers.length, expanded.hiddenCount, expanded.overflowLabel], [3, 0, null], 'expanded draft before Save');
  assert(app.includes('updateClusterCardAdaptiveContent(card, area, content, true)'), 'resize does not update card content in real time');
});

test('current person presentation uses assignment before safe legacy fallback and free seats create no fake name', () => {
  const assigned = presentation.buildWorkspacePresentation({ seat: { personId: 'legacy' }, assignment: { personId: 'assigned' }, effectiveState: { state: 'occupied', currentPersonId: 'assigned', mode: 'automatic', modeLabel: 'Automático' }, displayLocation: 'G-01', personName: 'Ana García' });
  equal([assigned.currentPersonId, assigned.currentPerson], ['assigned', 'Ana García'], 'assignment person');
  const free = content.buildClusterCardMemberContent({ level: 'members', width: 300, height: 180, members: [{ workspaceId: 'W-free', displayLocation: 'G-05', currentPerson: null }] });
  equal(free.totalNamedMembers, 0, 'free workspace invented a person');
});

test('showMembers false suppresses content regardless of available space', () => {
  equal(content.getClusterCardDetailLevel(400, 240, false), 'compact', 'disabled members level');
  assert(app.includes('data-card-edit-members'), 'edit control for members missing');
  assert(app.includes('showMembers: event.target.checked'), 'member setting is not drafted');
});

test('member links inspect official workspace outside edit mode and are disabled while editing', () => {
  assert(app.includes("class=\"cluster-member-row cluster-member-link\""), 'member link markup missing');
  assert(app.includes('openAreaMemberInspector(area, link.dataset.workspaceId)'), 'member click does not use inspector flow');
  assert(app.includes('clusterCardMemberMarkup(members, editing)'), 'edit mode does not suppress member links');
  assert(css.includes('.cluster-member-list') && css.includes('overflow: hidden'), 'card member list can scroll or lacks overflow control');
});

test('content rendering is presentation-only and retains count/problem summary', () => {
  const render = app.match(/function renderManagedAreaCard[\s\S]*?\n  function renderManagedAreaCards/)[0];
  assert(app.includes('function areaMemberRows(area)') && app.includes('workspacePresentation(seat)'), 'does not use official workspace presentation');
  assert(render.includes('content.detail'), 'existing count/problem summary removed');
  assert(!render.includes('sendManagedArea(') && !render.includes('moveWorkspace('), 'content rendering mutates membership or coordinates');
});

let passed = 0;
for (const item of tests) { try { item.fn(); passed++; } catch (error) { console.error(`FAIL: ${item.name}: ${error.message}`); } }
console.log(`Cluster card content harness: ${passed}/${tests.length} passed, ${tests.length - passed} failed`);
process.exitCode = passed === tests.length ? 0 : 1;
