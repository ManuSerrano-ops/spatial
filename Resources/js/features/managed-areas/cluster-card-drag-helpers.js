(() => {
  'use strict';

  const clamp = (value, minimum, maximum) => Math.max(minimum, Math.min(maximum, value));

  function attachClusterCardMoveHandle(options = {}) {
    const card = options.card;
    const handle = options.handle;
    const plan = options.plan;
    if (!card || !handle || !plan) throw new Error('card, handle, and plan are required.');
    const getAnchor = typeof options.getAnchor === 'function' ? options.getAnchor : () => ({ x: 0.5, y: 0.5 });
    const setDraftAnchor = typeof options.setDraftAnchor === 'function' ? options.setDraftAnchor : () => {};
    const onStateChange = typeof options.onStateChange === 'function' ? options.onStateChange : () => {};

    const down = event => {
      if (event.button !== 0) return;
      event.preventDefault(); event.stopPropagation(); event.stopImmediatePropagation();
      const planRect = plan.getBoundingClientRect();
      if (!planRect.width || !planRect.height) return;
      const startRect = card.getBoundingClientRect();
      const anchor = getAnchor();
      const localScale = planRect.width / Math.max(1, plan.offsetWidth || planRect.width);
      const start = { pointerId: event.pointerId, clientX: event.clientX, clientY: event.clientY, cardLeftPx: startRect.left, cardTopPx: startRect.top, anchorX: Number(anchor.x), anchorY: Number(anchor.y), planRect, localScale };
      handle.setPointerCapture(event.pointerId);
      if (!handle.hasPointerCapture(event.pointerId)) return;
      card.classList.add('card-moving');
      onStateChange({ phase: 'start', card, handle, start, beforeRect: startRect });

      const move = pointer => {
        if (pointer.pointerId !== start.pointerId || !card.isConnected) return;
        pointer.preventDefault(); pointer.stopPropagation(); pointer.stopImmediatePropagation();
        const deltaX = pointer.clientX - start.clientX;
        const deltaY = pointer.clientY - start.clientY;
        card.style.setProperty('--cluster-drag-x', `${deltaX / start.localScale}px`);
        card.style.setProperty('--cluster-drag-y', `${deltaY / start.localScale}px`);
        onStateChange({ phase: 'move', card, handle, start, deltaX, deltaY, rect: card.getBoundingClientRect() });
      };

      const finish = pointer => {
        if (pointer.pointerId !== start.pointerId) return;
        pointer.preventDefault(); pointer.stopPropagation(); pointer.stopImmediatePropagation();
        const finalRect = card.getBoundingClientRect();
        const anchorX = clamp((finalRect.left + finalRect.width / 2 - start.planRect.left) / start.planRect.width, .01, .99);
        const anchorY = clamp((finalRect.top + finalRect.height / 2 - start.planRect.top) / start.planRect.height, .01, .99);
        setDraftAnchor({ anchorX, anchorY });
        card.style.left = `${anchorX * 100}%`;
        card.style.top = `${anchorY * 100}%`;
        card.style.removeProperty('--cluster-drag-x');
        card.style.removeProperty('--cluster-drag-y');
        card.classList.remove('card-moving');
        if (handle.hasPointerCapture(pointer.pointerId)) handle.releasePointerCapture(pointer.pointerId);
        handle.removeEventListener('pointermove', move);
        handle.removeEventListener('pointerup', finish);
        handle.removeEventListener('pointercancel', finish);
        onStateChange({ phase: 'finish', card, handle, start, finalRect, anchorX, anchorY, afterRect: card.getBoundingClientRect() });
      };

      handle.addEventListener('pointermove', move);
      handle.addEventListener('pointerup', finish);
      handle.addEventListener('pointercancel', finish);
    };

    handle.addEventListener('pointerdown', down);
    return Object.freeze({ dispose: () => handle.removeEventListener('pointerdown', down) });
  }

  const api = Object.freeze({ attachClusterCardMoveHandle });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.ClusterCardDragHelpers = api;
})();
