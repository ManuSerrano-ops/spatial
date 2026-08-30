(() => {
  'use strict';

  const collator = new Intl.Collator('es', { numeric: true, sensitivity: 'base' });
  const clean = value => String(value ?? '').trim();
  const freeze = value => Object.freeze(value);

  function getClusterCardDetailLevel(width, height, showMembers = true) {
    if (!showMembers) return 'compact';
    const safeWidth = Number(width) || 0;
    const safeHeight = Number(height) || 0;
    if (safeWidth >= 120 && safeHeight >= 120) return 'members';
    if (safeWidth >= 120 && safeHeight >= 70) return 'summary';
    return 'compact';
  }

  function compareMembers(left, right) {
    return collator.compare(clean(left.displayLocation), clean(right.displayLocation))
      || collator.compare(clean(left.workspaceId), clean(right.workspaceId));
  }

  function memberLimit(level, width, height) {
    const safeWidth = Number(width) || 0;
    const safeHeight = Number(height) || 0;
    if (level === 'summary') {
      const availableContentHeight = Math.max(0, safeHeight - 55);
      const rows = Math.max(1, Math.floor(availableContentHeight / 18));
      return Math.min(rows, Math.max(1, Math.floor(safeWidth / 92)));
    }
    if (level === 'members') {
      const availableContentHeight = Math.max(0, safeHeight - 70);
      const rows = Math.max(1, Math.floor(availableContentHeight / 24));
      const columns = safeWidth >= 420 ? 2 : 1;
      return rows * columns;
    }
    return 0;
  }

  function buildClusterCardMemberContent(input = {}) {
    const level = ['compact', 'summary', 'members'].includes(input.level) ? input.level : 'compact';
    const namedMembers = (Array.isArray(input.members) ? input.members : [])
      .filter(member => clean(member?.currentPerson))
      .map(member => freeze({ workspaceId: clean(member.workspaceId), displayLocation: clean(member.displayLocation), currentPerson: clean(member.currentPerson), currentPersonId: clean(member.currentPersonId) || null }))
      .sort(compareMembers);
    const limit = memberLimit(level, input.width, input.height);
    const visibleMembers = freeze(namedMembers.slice(0, limit));
    const hiddenCount = Math.max(0, namedMembers.length - visibleMembers.length);
    return freeze({ level, showLocations: Number(input.width) >= 180, visibleMembers, hiddenCount, totalNamedMembers: namedMembers.length, overflowLabel: hiddenCount ? `+ ${hiddenCount} más` : null });
  }

  const api = freeze({ getClusterCardDetailLevel, compareMembers, memberLimit, buildClusterCardMemberContent });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.ClusterCardContentHelpers = api;
})();
