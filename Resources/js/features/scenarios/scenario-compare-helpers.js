(() => {
  'use strict';

  const array = value => Array.isArray(value) ? value : [];
  const text = value => String(value ?? '').trim();
  const freezeArray = values => Object.freeze([...values]);
  const identity = value => {
    if (value && typeof value === 'object' && text(value.id)) return `id:${text(value.id)}`;
    return `value:${JSON.stringify(value)}`;
  };

  function unique(values = []) {
    const seen = new Set();
    return values.filter(value => {
      const key = identity(value);
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    });
  }

  function aggregateValidationImpact(...sources) {
    const aggregate = { introduced: [], resolved: [], persistent: [] };
    const append = source => {
      if (!source) return;
      if (Array.isArray(source)) { source.forEach(append); return; }
      if (typeof source !== 'object') return;
      ['introduced', 'resolved', 'persistent'].forEach(kind => {
        aggregate[kind].push(...array(source[kind]));
      });
      if (source.validationImpact) append(source.validationImpact);
    };
    sources.forEach(append);
    return Object.freeze({
      introduced: freezeArray(unique(aggregate.introduced)),
      resolved: freezeArray(unique(aggregate.resolved)),
      persistent: freezeArray(unique(aggregate.persistent))
    });
  }

  function valueFrom(change, phase, field) {
    const direct = change?.[phase]?.[field];
    if (direct !== null && direct !== undefined && text(direct)) return direct;
    const fieldChange = array(change?.changedFields).find(item => item?.field === field);
    const value = fieldChange?.[phase];
    return value !== null && value !== undefined && text(value) ? value : null;
  }

  function endpointFrom(change, phase) {
    if (!change) return null;
    return Object.freeze({
      mapId: text(change.mapId) || null,
      workspaceId: text(change.entityId || change.seatId) || null,
      personId: valueFrom(change, phase, 'personId'),
      deviceId: valueFrom(change, phase, 'deviceId')
    });
  }

  function firstEndpoint(members, phase, field) {
    const matching = array(members).find(member => valueFrom(member, phase, field));
    return endpointFrom(matching || array(members)[phase === 'before' ? 0 : array(members).length - 1], phase);
  }

  function describeMovement(members) {
    const source = firstEndpoint(members, 'before', 'personId');
    const destination = firstEndpoint(members, 'after', 'personId');
    const personId = source?.personId || destination?.personId || null;
    const deviceId = source?.deviceId || destination?.deviceId || null;
    return Object.freeze({ source, destination, person: personId, device: deviceId });
  }

  function finalizeUnit(unit) {
    const members = freezeArray(unit.members);
    const memberChangeIds = freezeArray(unique(unit.memberChangeIds.filter(Boolean)));
    const movement = unit.kind === 'movement' ? describeMovement(members) : { source: null, destination: null, person: null, device: null };
    return Object.freeze({
      ...unit,
      ...movement,
      members,
      memberChangeIds,
      validationImpact: aggregateValidationImpact(members)
    });
  }

  function buildCompareUnits(changes = []) {
    const units = [];
    const operations = new Map();
    array(changes).forEach(change => {
      const operationId = text(change?.operationId);
      if (!operationId) {
        units.push({
          unitId: `change|${text(change?.id)}`,
          kind: 'change',
          atomic: false,
          members: [change],
          memberChangeIds: [change?.id]
        });
        return;
      }

      let unit = operations.get(operationId);
      if (!unit) {
        unit = {
          unitId: `movement|${operationId}`,
          kind: 'movement',
          operationId,
          operationType: text(change?.operationType || change?.type) || 'movement',
          atomic: Boolean(change?.atomic),
          members: [],
          memberChangeIds: []
        };
        operations.set(operationId, unit);
        units.push(unit);
      }
      unit.members.push(change);
      unit.memberChangeIds.push(change?.id);
    });
    return freezeArray(units.map(finalizeUnit));
  }

  function flattenSelectedCompareUnits(units = [], selectedUnitIds = []) {
    const selected = new Set(selectedUnitIds);
    const ids = [];
    const seen = new Set();
    array(units).forEach(unit => {
      if (!selected.has(unit?.unitId)) return;
      array(unit.memberChangeIds).forEach(id => {
        const key = text(id);
        if (!key || seen.has(key)) return;
        seen.add(key);
        ids.push(key);
      });
    });
    return freezeArray(ids);
  }

  const api = { buildCompareUnits, flattenSelectedCompareUnits, aggregateValidationImpact };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.ScenarioCompareHelpers = api;
})();
