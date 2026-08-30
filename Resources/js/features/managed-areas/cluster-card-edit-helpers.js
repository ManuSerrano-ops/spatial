(() => {
  'use strict';

  const MIN_WIDTH = 88;
  const MAX_WIDTH = 460;
  const MIN_HEIGHT = 48;
  const MAX_HEIGHT = 340;
  const clone = value => value === undefined ? undefined : JSON.parse(JSON.stringify(value));
  const finiteSize = value => {
    const size = Number(value);
    return Number.isFinite(size) && size > 0 ? size : null;
  };
  const finiteAnchor = value => {
    if (value === null || value === undefined || value === '') return null;
    const anchor = Number(value);
    return Number.isFinite(anchor) && anchor >= 0 && anchor <= 1 ? anchor : null;
  };
  const clamp = (value, minimum, maximum) => Math.max(minimum, Math.min(maximum, value));

  function normalizeLayout(record, normalizeShape) {
    const source = record && typeof record === 'object' ? record : {};
    const width = finiteSize(source.cardWidth ?? source.width);
    const height = finiteSize(source.cardHeight ?? source.height);
    const cardSizingMode = source.cardSizingMode === 'manual' || (width && height) ? 'manual' : 'preset';
    return Object.freeze({
      shape: normalizeShape(source.shape ?? record),
      cardSizingMode,
      width: cardSizingMode === 'manual' ? width : null,
      height: cardSizingMode === 'manual' ? height : null,
      anchorX: finiteAnchor(source.cardAnchorX),
      anchorY: finiteAnchor(source.cardAnchorY),
      showMembers: source.showMembers !== false
    });
  }

  function beginCardEdit({ areaId, record, normalizeShape }) {
    const draft = normalizeLayout(record, normalizeShape);
    return Object.freeze({ active: true, areaId, before: clone(record), draft, draftWidth: draft.width, draftHeight: draft.height, draftAnchorX: draft.anchorX, draftAnchorY: draft.anchorY, draftShowMembers: draft.showMembers });
  }

  function updateCardEditDraft(session, patch, normalizeShape) {
    if (!session?.active) return session;
    const changesSize = patch.width !== undefined || patch.height !== undefined;
    const width = patch.width === undefined ? session.draftWidth : clamp(Number(patch.width), MIN_WIDTH, MAX_WIDTH);
    const height = patch.height === undefined ? session.draftHeight : clamp(Number(patch.height), MIN_HEIGHT, MAX_HEIGHT);
    const anchorX = patch.anchorX === undefined ? session.draftAnchorX : clamp(Number(patch.anchorX), 0, 1);
    const anchorY = patch.anchorY === undefined ? session.draftAnchorY : clamp(Number(patch.anchorY), 0, 1);
    const showMembers = patch.showMembers === undefined ? session.draftShowMembers : Boolean(patch.showMembers);
    const draft = Object.freeze({
      ...session.draft,
      shape: normalizeShape(patch.shape ?? session.draft.shape),
      cardSizingMode: changesSize ? 'manual' : session.draft.cardSizingMode,
      width,
      height,
      anchorX,
      anchorY,
      showMembers
    });
    return Object.freeze({ ...session, draft, draftWidth: width, draftHeight: height, draftAnchorX: anchorX, draftAnchorY: anchorY, draftShowMembers: showMembers });
  }

  function resetCardEditSize(session, normalizeShape) {
    if (!session?.active) return session;
    const draft = Object.freeze({ ...session.draft, shape: normalizeShape(session.draft.shape), cardSizingMode: 'preset', width: null, height: null });
    return Object.freeze({ ...session, draft, draftWidth: null, draftHeight: null });
  }

  function resetCardEditPosition(session) {
    if (!session?.active) return session;
    const draft = Object.freeze({ ...session.draft, anchorX: null, anchorY: null });
    return Object.freeze({ ...session, draft, draftAnchorX: null, draftAnchorY: null });
  }

  function commitCardEdit(session, normalizeShape) {
    if (!session?.active) return null;
    const width = finiteSize(session.draftWidth);
    const height = finiteSize(session.draftHeight);
    const record = { shape: normalizeShape(session.draft.shape), cardSizingMode: session.draft.cardSizingMode, showMembers: session.draftShowMembers !== false };
    if (session.draft.cardSizingMode === 'manual' && width && height) {
      record.cardWidth = clamp(width, MIN_WIDTH, MAX_WIDTH);
      record.cardHeight = clamp(height, MIN_HEIGHT, MAX_HEIGHT);
    }
    const anchorX = finiteAnchor(session.draftAnchorX);
    const anchorY = finiteAnchor(session.draftAnchorY);
    if (anchorX !== null && anchorY !== null) { record.cardAnchorX = anchorX; record.cardAnchorY = anchorY; }
    return Object.freeze(record);
  }

  const api = Object.freeze({ MIN_WIDTH, MAX_WIDTH, MIN_HEIGHT, MAX_HEIGHT, normalizeLayout, beginCardEdit, updateCardEditDraft, resetCardEditSize, resetCardEditPosition, commitCardEdit, clone });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.ClusterCardEditHelpers = api;
})();
