(() => {
  'use strict';

  const SHAPES = Object.freeze(['automatic', 'compact', 'square', 'vertical']);
  const FALLBACK_NAME = 'Cluster';
  const METRICS = Object.freeze([
    Object.freeze({ key: 'occupied', label: 'ocupados' }),
    Object.freeze({ key: 'free', label: 'libres' }),
    Object.freeze({ key: 'reserved', label: 'reservados' })
  ]);

  const freeze = value => Object.freeze(value);
  const nonNegativeInteger = value => Math.max(0, Math.floor(Number(value) || 0));
  const own = (value, key) => Object.prototype.hasOwnProperty.call(value, key);

  function normalizeClusterCardShape(value) {
    const shape = String(value || '').trim().toLowerCase();
    return SHAPES.includes(shape) ? shape : 'automatic';
  }

  function normalizeClusterCardName(value) {
    const name = String(value == null ? '' : value).replace(/\s+/g, ' ').trim();
    return name || FALLBACK_NAME;
  }

  function normalizeClusterCardCounts(value = {}) {
    const source = value && typeof value === 'object' ? value : {};
    const occupied = nonNegativeInteger(source.occupied);
    const free = nonNegativeInteger(source.free);
    const reserved = nonNegativeInteger(source.reserved);
    const memberTotal = occupied + free + reserved;
    const total = own(source, 'total') ? nonNegativeInteger(source.total) : memberTotal;
    return freeze({ total: Math.max(total, memberTotal), occupied, free, reserved, problems: nonNegativeInteger(source.problems) });
  }

  function metricContent(counts) {
    const metrics = METRICS.filter(metric => counts[metric.key] > 0).map(metric => freeze({
      key: metric.key,
      label: metric.label,
      value: counts[metric.key],
      text: `${counts[metric.key]} ${metric.label}`
    }));
    if (counts.problems > 0) metrics.push(freeze({ key: 'problems', label: 'problemas', value: counts.problems, text: `! ${counts.problems} problemas` }));
    return freeze(metrics);
  }

  // The rules depend only on normalized input, so a card never changes shape due to render order.
  function chooseAutomaticClusterCardShape(input = {}) {
    const source = input && typeof input === 'object' ? input : {};
    const name = normalizeClusterCardName(source.name);
    const counts = normalizeClusterCardCounts(source.counts || source);
    const visibleMetricCount = metricContent(counts).length;
    const longName = name.length > 18;

    if (longName || visibleMetricCount >= 3) return 'vertical';
    if (counts.total >= 10 || visibleMetricCount === 2 || counts.problems > 0) return 'square';
    return 'compact';
  }

  function deriveClusterCardContent(input = {}) {
    const source = input && typeof input === 'object' ? input : {};
    const name = normalizeClusterCardName(source.name);
    const counts = normalizeClusterCardCounts(source.counts || source);
    const requestedShape = normalizeClusterCardShape(source.shape);
    const shape = requestedShape === 'automatic' ? chooseAutomaticClusterCardShape({ name, counts }) : requestedShape;
    const metrics = metricContent(counts);
    return freeze({
      shape,
      requestedShape,
      name,
      badge: counts.total,
      counts,
      metrics,
      detail: metrics.map(metric => metric.text).join(' · ')
    });
  }

  function deriveClusterCardTooltip(input = {}) {
    const source = input && typeof input === 'object' ? input : {};
    const name = normalizeClusterCardName(source.name);
    const counts = normalizeClusterCardCounts(source.counts || source);
    return `${name}: ${counts.total} puestos · ${counts.occupied} ocupados · ${counts.free} libres · ${counts.reserved} reservados · ${counts.problems} problemas`;
  }

  function buildClusterCardShapePresentation(input = {}) {
    const content = deriveClusterCardContent(input);
    return freeze({ ...content, tooltip: deriveClusterCardTooltip({ name: content.name, counts: content.counts }) });
  }

  const api = freeze({
    SHAPES,
    normalizeClusterCardShape,
    normalizeClusterCardName,
    normalizeClusterCardCounts,
    chooseAutomaticClusterCardShape,
    deriveClusterCardContent,
    deriveClusterCardTooltip,
    buildClusterCardShapePresentation
  });

  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.ClusterCardShapeHelpers = api;
})();
