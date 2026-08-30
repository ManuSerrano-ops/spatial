(() => {
  'use strict';

  const severityRank = { Critical: 3, Warning: 2, Info: 1, None: 0 };
  const value = (result, name) => result?.[name] ?? result?.[`${name[0].toUpperCase()}${name.slice(1)}`];
  const related = result => value(result, 'relatedEntities') ?? value(result, 'relatedEntityIds') ?? [];
  const normalize = text => String(text ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim().replace(/\s+/g, ' ');

  function isOperational(result) {
    const explicit = value(result, 'operational');
    if (typeof explicit === 'boolean') return explicit;
    return normalize(value(result, 'classification')) !== 'historical';
  }

  function operationalResults(results = []) {
    return (Array.isArray(results) ? results : []).filter(isOperational);
  }

  function getValidationSummary(results = []) {
    return operationalResults(results).reduce((summary, result) => {
      summary.total += 1;
      const key = normalize(value(result, 'severity'));
      if (key === 'critical') summary.critical += 1;
      else if (key === 'warning') summary.warning += 1;
      else if (key === 'info') summary.info += 1;
      return summary;
    }, { total: 0, critical: 0, warning: 0, info: 0 });
  }

  function buildProblemsByWorkspace(results = []) {
    const index = new Map();
    operationalResults(results).forEach(result => {
      const ids = new Set([value(result, 'workspaceId'), value(result, 'entityType') === 'workspace' ? value(result, 'entityId') : null, ...related(result)].filter(Boolean));
      ids.forEach(id => {
        const values = index.get(id) || [];
        values.push(result);
        index.set(id, values);
      });
    });
    return index;
  }

  function getWorkspaceMaxSeverity(workspaceId, index = new Map()) {
    return (index.get(workspaceId) || []).reduce((maximum, result) => {
      const severity = value(result, 'severity') || 'None';
      return severityRank[severity] > severityRank[maximum] ? severity : maximum;
    }, 'None');
  }

  function getProblemsForMap(results = [], mapId) {
    return operationalResults(results).filter(result => value(result, 'mapId') === mapId);
  }

  function groupProblemsByRule(results = []) {
    return operationalResults(results).reduce((groups, result) => {
      const ruleId = value(result, 'ruleId') || 'unknown';
      (groups.get(ruleId) || groups.set(ruleId, []).get(ruleId)).push(result);
      return groups;
    }, new Map());
  }

  function problemMatches(result, filters = {}) {
    if (!isOperational(result)) return false;
    const severity = value(result, 'severity');
    const ruleId = value(result, 'ruleId');
    const mapId = value(result, 'mapId');
    const entityType = value(result, 'entityType');
    if (filters.severity && severity !== filters.severity) return false;
    if (filters.ruleId && ruleId !== filters.ruleId) return false;
    if (filters.mapId && mapId !== filters.mapId) return false;
    if (filters.entityType && entityType !== filters.entityType) return false;
    const query = normalize(filters.text);
    if (!query) return true;
    return normalize([value(result, 'title'), value(result, 'message'), ruleId, value(result, 'entityId'), mapId, value(result, 'field'), value(result, 'details'), ...related(result)].filter(Boolean).join(' ')).includes(query);
  }

  const api = { severityRank, value, related, normalize, isOperational, operationalResults, getValidationSummary, buildProblemsByWorkspace, getWorkspaceMaxSeverity, getProblemsForMap, groupProblemsByRule, problemMatches };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else window.ValidationHelpers = api;
})();
