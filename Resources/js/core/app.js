(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  // "Equipo Finanzas" tiene 15 caracteres y es el mínimo visible acordado para un nombre de cluster.
  const MIN_CARACTERES_NOMBRE_CLUSTER = 15;
  function reportFrontendError(message) { window.chrome?.webview?.postMessage?.({ action: 'reportPlanResourceDiagnostic', payload: { mapId: 'frontend', resource: 'app.js', result: message } }); }
  window.addEventListener('error', event => reportFrontendError(`JavaScript error: ${event.message}`));
  window.addEventListener('unhandledrejection', event => reportFrontendError(`Unhandled rejection: ${event.reason?.message || String(event.reason)}`));
  const appState = {
    activeMap: null, activeScenario: null, dataContext: 'real', selectedWorkspace: null, selectedPerson: null, selectedDevice: null,
    selectedWorkspaces: new Set(), selectionAnchor: null, selectedProblemId: null, bulk: { pendingAction: 'reserved', inFlight: null, lastCommitted: null, undoRequested: false }, dashboard: { selectedSection: null }, analytics: { status: 'idle', result: null, baseline: null, heatmapMode: 'occupancy', error: null, lastRunAt: null, durationMs: null }, planner: { status: 'idle', step: 'idle', sourceIds: [], destinationIds: [], requestPairs: [], plan: null, selectedProposalId: null, destinationMode: false, overrideSourceId: null, excludedSourceIds: [], sourceIssues: [], error: null }, scenarioComparison: { status: 'idle', changes: [], impactSummary: null, validationImpact: null, selectedChangeId: null, filters: { kind: '', mapId: '', text: '' } }, filters: { quick: 'all', zone: '', person: '', device: '', roseta: '', only: false }, search: { query: '', activeIndex: 0, results: [] }, problemFilters: { severity: '', ruleId: '', mapId: '', entityType: '', text: '', workspaceId: '' }, layers: { seats: true, grid: true, labels: false, people: false, devices: false, network: false, problems: true, heatmap: false }, persistence: { status: 'idle', lastSaved: null, error: null }, validation: { status: 'idle', results: [], summary: { total: 0, critical: 0, warning: 0, info: 0 }, lastRunAt: null, error: null, byWorkspace: new Map() }, viewMode: 'map', zoom: 1, pan: { x: 0, y: 0 }, mapAppearance: 'dark', mapAppearanceManifest: null, gridCellMetadata: {}, gridCellAppearances: {}, clusterCardShapes: {}, managedAreas: Object.freeze({ areas: Object.freeze([]) }), densityModes: {}, cellDetail: null, areaDetail: null, activeClusterFocus: null, activeAreaFocus: null
  };
  const ui = {
      state: null, adding: false, addingContext: null, moving: false, movingSeat: false, placementCursor: null, placementAnnouncement: false, changes: [], compareUnits: [], selectedCompareUnitIds: new Set(), touchedSeats: new Set(), messageTimer: null, busyAction: null, busyTimer: null, disabledControls: new Map(), contextPoint: null, contextMenuRestoreFocus: null, singleKeyShortcutsEnabled: true, undoPayload: null, pendingSeatId: null, pendingMapId: null, pendingAreaFocusId: null, pendingClusterId: null, clusterDraft: null, cardEdit: null, cardMove: null, cardSizeUndo: null, assignmentBaseline: null, pendingWarning: null, bulkConfirmation: null,
      currentScale: 1, targetScale: 1, currentX: 0, targetX: 0, currentY: 0, targetY: 0,
      planResources: { expected: 0, loaded: new Set(), failed: new Map() },
      pan: null, zoomAnchor: null, frame: null, dragVisual: null, busyControl: null, problemHighlightWorkspace: null, problemHighlightMapId: null, problemHighlightTimer: null, searchHitWorkspace: null, searchHitTimer: null, dashboardInitialized: false, initialViewports: new Map()
    };
  Object.defineProperties(ui, {
    mapId: { get: () => appState.activeMap, set: value => { appState.activeMap = value; } },
    seatId: { get: () => appState.selectedWorkspace, set: value => { appState.selectedWorkspace = value; } }
  });
  const wrap = $('mapwrap');
  const list = (source, key) => Array.isArray(source) ? source : Array.isArray(source?.[key]) ? source[key] : [];
  const maps = () => list(ui.state?.maps, 'maps');
  const seats = map => list(map?.seats, 'seats');
  const assignments = () => list(ui.state?.assignments, 'assignments');
  const people = () => list(ui.state?.people, 'people');
  const devices = () => list(ui.state?.devices, 'devices');
  const locations = () => list(ui.state?.locations, 'locations');
  const scenario = () => ui.state?.activeScenario || null;
  const scenarioId = () => scenario()?.id || null;
  const currentMap = () => maps().find(map => map.id === ui.mapId) || maps()[0];
  const currentSeat = () => seats(currentMap()).find(seat => seat.id === ui.seatId);
  const payloadForScenario = payload => scenarioId() ? { ...payload, scenarioId: scenarioId() } : payload;
  const diagnosticMode = new URLSearchParams(location.search).has('diagnostic'); const performanceMeasures = [];
  function measureSync(name, fn) { const start = performance.now(); const value = fn(); const duration = performance.now() - start; if (diagnosticMode) { performanceMeasures.push({ name, duration, at: new Date().toISOString() }); console.debug(`[Plano] ${name}: ${duration.toFixed(1)} ms`); } return value; }
  const validationHelpers = window.ValidationHelpers;
  const movementPlannerHelpers = window.MovementPlannerHelpers;
  const bulkSelectionHelpers = window.BulkSelectionHelpers;
  const selectionReviewHelpers = window.SelectionReviewHelpers;
  const spatialAnalyticsHelpers = window.SpatialAnalyticsHelpers;
  const dashboardHelpers = window.DashboardHelpers;
  const pinStateHelpers = window.PinStateHelpers;
  const workspaceStateHelpers = window.WorkspaceStateHelpers;
  const workspaceQualityHelpers = window.WorkspaceQualityHelpers;
  const rectangleSelectionHelpers = window.RectangleSelectionHelpers;
  const workspacePresentationHelpers = window.WorkspacePresentationHelpers;
  const scenarioCompareHelpers = window.ScenarioCompareHelpers;
  const gridCellMetadataHelpers = window.GridCellMetadataHelpers;
  const mapDensityHelpers = window.MapDensityHelpers;
  const mapAppearanceHelpers = window.MapAppearanceHelpers;
  const managedAreaHelpers = window.ManagedAreaHelpers;
  const detailPanelHelpers = window.DetailPanelHelpers;
  const mapViewportHelpers = window.MapViewportHelpers;
  const clusterCardShapeHelpers = window.ClusterCardShapeHelpers;
  const clusterCardEditHelpers = window.ClusterCardEditHelpers;
  const clusterCardContentHelpers = window.ClusterCardContentHelpers;
  const clusterCardDragHelpers = window.ClusterCardDragHelpers;
  const validationValue = (result, name) => validationHelpers.value(result, name);
  const validationRelated = result => validationHelpers.related(result);
  const icon = (name, size = 16) => `<svg class="icon icon-${size}" aria-hidden="true"><use href="#icon-${name}"></use></svg>`;
  const uiThemeFeature = window.UiThemeFeature.createUiThemeFeature({ document, themeSelect: $('theme') });
  const detailPanelControllerFeature = window.DetailPanelControllerFeature.createDetailPanelControllerFeature({ state: appState, ui, getElement: $, headerFor: detailPanelHelpers.headerFor, deriveClosedDetailState: detailPanelHelpers.deriveClosedDetailState, render: () => render() });
  const mapAppearanceFeature = window.MapAppearanceFeature.createMapAppearanceFeature({ appearanceHelpers: mapAppearanceHelpers, state: appState, storage: localStorage, document });
  const cellAppearanceFeature = window.CellAppearanceFeature.createCellAppearanceFeature({ cellMetadataHelpers: gridCellMetadataHelpers, state: appState, storage: localStorage, onChanged: () => render() });
  const cellDetailFeature = window.CellDetailFeature.createCellDetailFeature({ state: appState, ui, getElement: $, document, mapCells, showDetailMode, workspacePresentation, plannerState, plannerAvailability, getWorkspaceMaxSeverity, severityLabel, escapeHtml });
  const workspaceFilterFeature = window.WorkspaceFilterFeature.createWorkspaceFilterFeature({ state: appState, stateFor: seat => seatType(seat), completenessFor: seat => seatCompleteness(seat), valuesFor: seatValues, people, devices, nameFor });

  // ══ Filtros ══
  const allSeats = () => maps().flatMap(map => seats(map).map(seat => ({ ...seat, _mapId: map.id, _mapName: map.name || map.id })));
  const workspaceFilterUiFeature = window.WorkspaceFilterUiFeature.createWorkspaceFilterUiFeature({ state: appState, hasLoaded: () => Boolean(ui.state), allWorkspaces: allSeats, matches: workspaceFilterFeature.matches, maps, getElement: $, document, onFiltersChanged: () => render() });
  const selectionControllerFeature = window.SelectionControllerFeature.createSelectionControllerFeature({ state: appState, ui, getElement: $, canActivateMode: () => !ui.cardEdit?.active && !ui.adding && plannerState().status === 'idle', closeDetailPanel, renderBulkBar, render, deselectWorkspace: selectionReviewHelpers.deselectWorkspace });
  function getValidationSummary(results = appState.validation.results) { return validationHelpers.getValidationSummary(results); }
  function getProblemsForWorkspace(workspaceId) { return appState.validation.byWorkspace.get(workspaceId) || []; }
  function getWorkspaceMaxSeverity(workspaceId) { return validationHelpers.getWorkspaceMaxSeverity(workspaceId, appState.validation.byWorkspace); }
  function getProblemsForMap(mapId) { return validationHelpers.getProblemsForMap(appState.validation.results, mapId); }
  function groupProblemsByRule(results = appState.validation.results) { return validationHelpers.groupProblemsByRule(results); }
  function severitySymbol(severity) { return severity === 'Critical' ? '×' : severity === 'Warning' ? '!' : severity === 'Info' ? 'i' : '✓'; }
  function severityLabel(severity) { return severity === 'Critical' ? 'Crítico' : severity === 'Warning' ? 'Advertencia' : severity === 'Info' ? 'Información' : 'Sin problemas'; }
  function normalizeValidationResults(results) { return (Array.isArray(results) ? results : []).map(result => ({ id: validationValue(result, 'id'), ruleId: validationValue(result, 'ruleId'), severity: validationValue(result, 'severity'), classification: validationValue(result, 'classification') || 'Operational', operational: validationValue(result, 'operational') !== false, entityType: validationValue(result, 'entityType'), entityId: validationValue(result, 'entityId'), mapId: validationValue(result, 'mapId'), field: validationValue(result, 'field'), title: validationValue(result, 'title'), message: validationValue(result, 'message'), details: validationValue(result, 'details'), relatedEntities: validationRelated(result), suggestedAction: validationValue(result, 'suggestedAction') })); }
  function refreshValidation(options = {}) {
    if (!ui.state || appState.validation.status === 'running') return false;
    appState.validation.status = 'running'; appState.validation.error = null; renderProblems(); renderDashboard();
    return send('runValidation', scenarioId() ? { scenarioId: scenarioId() } : {}, true);
  }
  function applyValidationResponse(data) {
    const results = validationHelpers.operationalResults(normalizeValidationResults(data?.results));
    appState.validation.results = results;
    appState.validation.byWorkspace = validationHelpers.buildProblemsByWorkspace(results);
    appState.validation.summary = getValidationSummary(results);
    appState.validation.lastRunAt = new Date().toISOString();
    appState.validation.status = 'ready'; appState.validation.error = null;
    if (appState.selectedProblemId && !results.some(result => result.id === appState.selectedProblemId)) appState.selectedProblemId = null;
    renderValidationConsumers();
  }
  function failureMessage(error, fallback) { const message = String(error ?? '').trim(); return message || fallback; }
    function failValidation(error) { appState.validation.status = 'error'; appState.validation.error = failureMessage(error, 'La validación no se pudo completar.'); renderProblems(); renderDashboard(); notify('error', appState.validation.error); }
  function renderValidationConsumers() { renderSidebarProblemsBadge(); if (appState.viewMode === 'problems') renderProblems(); if (appState.viewMode === 'dashboard') renderDashboard(); if (ui.state) { render(); if (appState.viewMode === 'list') renderList(); } }
  function renderSidebarProblemsBadge() { const badge = $('problems-badge'); if (!badge) return; const summary = appState.validation.summary; badge.textContent = String(summary.total); badge.classList.toggle('hidden', summary.total === 0); badge.classList.toggle('critical', summary.critical > 0); badge.setAttribute('aria-label', `${summary.total} problemas${summary.critical ? `, ${summary.critical} críticos` : ''}`); }

  function refreshSpatialAnalytics() {
    if (!ui.state || appState.analytics.status === 'running') return false;
    appState.analytics.status = 'running'; appState.analytics.error = null; renderAnalytics(); renderDashboard(); renderHeatmap();
    return send('runSpatialAnalytics', scenarioId() ? { scenarioId: scenarioId() } : {}, true);
  }
  function applySpatialAnalyticsResponse(data) {
    const expected = scenarioId() || null; if ((data?.contextScenarioId || null) !== expected) return;
    const analytics = measureSync('buildSpatialAnalytics', () => ({ result: data?.result || null, baseline: data?.baseline || null, durationMs: Number(data?.durationMs) || 0 }));
    appState.analytics.result = analytics.result; appState.analytics.baseline = analytics.baseline; appState.analytics.durationMs = analytics.durationMs; appState.analytics.lastRunAt = new Date().toISOString(); appState.analytics.status = 'ready'; appState.analytics.error = null;
    renderAnalytics(); renderDashboard(); renderScenarioSpatialComparison(); renderHeatmap();
    reportPlanResourceDiagnostic({ id: 'frontend' }, 'spatial-analytics', 'Spatial analytics initialized. Heatmap module initialized.');
  }
  function failSpatialAnalytics(error) { appState.analytics.status = 'error'; appState.analytics.error = failureMessage(error, 'La analítica espacial no se pudo completar.'); renderAnalytics(); renderDashboard(); renderHeatmap(); notify('error', appState.analytics.error); }
  function analyticsSummary() { return spatialAnalyticsHelpers.normalizeAnalyticsSummary(appState.analytics.result || {}); }
  function currentHeatmapLayer() { return spatialAnalyticsHelpers.layerByMode[spatialAnalyticsHelpers.selectMetricMode(appState.analytics.heatmapMode)]; }
  function renderHeatmap() {
    const overlay = $('spatial-heatmap'); const control = $('heatmap-control'); if (!overlay || !control) return;
    const enabled = appState.viewMode === 'map' && appState.layers.heatmap && plannerState().status === 'idle' && appState.analytics.status === 'ready'; const modeControl = $('heatmap-mode'); const changesOption = modeControl.querySelector('option[value="scenarioChanges"]'); if (changesOption) changesOption.disabled = !scenarioId(); if (!scenarioId() && appState.analytics.heatmapMode === 'scenarioChanges') appState.analytics.heatmapMode = 'occupancy'; control.classList.toggle('hidden', !appState.layers.heatmap); modeControl.value = appState.analytics.heatmapMode; overlay.classList.toggle('hidden', !enabled);
    if (!enabled) { overlay.replaceChildren(); $('heatmap-legend').classList.add('hidden'); return; }
    const mode = spatialAnalyticsHelpers.selectMetricMode(appState.analytics.heatmapMode); const layer = currentHeatmapLayer(); const points = measureSync('buildHeatmap', () => (appState.analytics.result?.heatmapPoints || []).filter(point => point.mapId === ui.mapId && point.layer === layer));
    const max = Math.max(1, ...points.map(point => Number(point.value) || 0)); const palette = { occupancy: ['#1d4ed8', '#60a5fa'], availability: ['#0f766e', '#5eead4'], problems: ['#6d28d9', '#c4b5fd'], scenarioChanges: ['#a16207', '#facc15'] }[mode];
    const namespace = 'http://www.w3.org/2000/svg'; const defs = document.createElementNS(namespace, 'defs'); const gradient = document.createElementNS(namespace, 'radialGradient'); gradient.id = 'heatmap-gradient'; const center = document.createElementNS(namespace, 'stop'); center.setAttribute('offset', '0%'); center.setAttribute('stop-color', palette[0]); center.setAttribute('stop-opacity', '.65'); const edge = document.createElementNS(namespace, 'stop'); edge.setAttribute('offset', '100%'); edge.setAttribute('stop-color', palette[1]); edge.setAttribute('stop-opacity', '0'); gradient.append(center, edge); defs.append(gradient); overlay.replaceChildren(defs);
    points.forEach(point => { const circle = document.createElementNS(namespace, 'circle'); circle.setAttribute('cx', String(Number(point.x) * 1000)); circle.setAttribute('cy', String(Number(point.y) * 1000)); circle.setAttribute('r', String(30 + 46 * (Number(point.value) / max))); circle.setAttribute('fill', 'url(#heatmap-gradient)'); circle.setAttribute('stroke', palette[0]); circle.setAttribute('stroke-width', '2'); circle.setAttribute('stroke-dasharray', mode === 'problems' ? '7 4' : mode === 'scenarioChanges' ? '3 3' : ''); overlay.append(circle); });
    const legend = spatialAnalyticsHelpers.getLegendMetadata(mode, { min: 0, max }); $('heatmap-legend').classList.toggle('hidden', false); $('heatmap-legend').setAttribute('aria-label', legend.ariaLabel); $('heatmap-legend').innerHTML = `<strong>${escapeHtml(legend.label)}</strong><span class="heatmap-scale" aria-hidden="true"></span><span>${escapeHtml(legend.minLabel)} baja</span><span>${escapeHtml(legend.maxLabel)} alta</span>`; $('heatmap-legend').querySelector('.heatmap-scale').style.background = `linear-gradient(90deg, ${palette[1]}33, ${palette[0]}bb)`;
  }
  function renderAnalyticsProblems() { const host = $('analytics-problems'); const list = $('analytics-problems-list'); if (!host || !list) return; const problems = appState.validation.results || []; $('analytics-problems-summary').textContent = problems.length ? `${appState.validation.summary.critical} críticos · ${appState.validation.summary.warning} advertencias · ${appState.validation.summary.info} información` : '✓ No se han detectado problemas operativos.'; list.replaceChildren(...problems.slice(0, 8).map(problem => { const item = document.createElement('li'); const button = document.createElement('button'); button.type = 'button'; button.className = `problem-row severity-${problem.severity.toLowerCase()}`; button.innerHTML = `<span class="problem-row-symbol" aria-hidden="true">${severitySymbol(problem.severity)}</span><span><strong>${escapeHtml(problem.title)}</strong><small>${escapeHtml(problemMapLabel(problem))}</small></span>`; button.onclick = () => selectProblem(problem.id, true); item.append(button); return item; })); host.classList.toggle('hidden', false); }
  function renderAnalytics() {
    const analytics = appState.analytics; const view = $('analyticsview'); if (!view) return; renderAnalyticsProblems(); const summary = analyticsSummary(); $('analytics-context').textContent = analytics.lastRunAt ? `Estado efectivo ${scenarioId() ? 'del escenario activo' : 'de REALIDAD'} · actualización ${new Date(analytics.lastRunAt).toLocaleString()} · ${analytics.durationMs} ms` : 'Calculando estado efectivo…'; const errorText = String(analytics.error ?? '').trim(); $('analytics-running').classList.toggle('hidden', analytics.status !== 'running'); $('analytics-error').classList.toggle('hidden', analytics.status !== 'error' || !errorText); $('analytics-error').textContent = errorText; const cards = [['Total', summary.total], ['Ocupados', summary.occupied], ['Libres', summary.free], ['Reservados', summary.reserved], ['Ocupación', `${summary.occupancyRate}%`], ['Disponibilidad', `${summary.availabilityRate}%`], ['Problemas', summary.problems]]; $('analytics-summary').replaceChildren(...cards.map(([label, value]) => { const item = document.createElement('button'); item.type = 'button'; item.className = 'analytics-card'; item.innerHTML = `<strong>${escapeHtml(String(value))}</strong><span>${escapeHtml(label)}</span>`; if (label === 'Problemas') item.onclick = () => $('analytics-problems')?.scrollIntoView({ block: 'nearest' }); else item.disabled = true; return item; })); renderAnalyticsManagedAreas(); const body = $('analytics-table').querySelector('tbody'); const mapMetrics = analytics.result?.maps || []; body.replaceChildren(...mapMetrics.map(metric => { const row = document.createElement('tr'); const validation = metric.validation || {}; row.innerHTML = `<td><button type="button">${escapeHtml(metric.mapName || metric.mapId)}</button></td><td>${metric.seats?.total || 0}</td><td>${metric.seats?.occupied || 0}</td><td>${metric.seats?.free || 0}</td><td>${metric.seats?.reserved || 0}</td><td>${metric.seats?.total ? `${metric.seats.occupancyRate || 0}%` : '—'}</td><td>${metric.seats?.total ? `${metric.seats.availabilityRate || 0}%` : '—'}</td><td><button type="button">${validation.total || 0}</button></td>`; row.querySelector('td:first-child button').onclick = () => focusSeat(metric.mapId, null); row.querySelector('td:last-child button').onclick = () => { $('analytics-problems')?.scrollIntoView({ block: 'nearest' }); }; return row; }));
  }
  function dashboardModel() {
    if (!dashboardHelpers) return null;
    return measureSync('buildDashboardModel', () => dashboardHelpers.buildDashboardModel({ analytics: appState.analytics, validation: appState.validation, scenario: scenario(), scenarioDiff: appState.scenarioComparison }));
  }
  function activateDashboardTarget(target) {
    if (!target) return;
    if (target.kind === 'list') { appState.filters.quick = target.filters?.quick || 'all'; setViewMode('list'); render(); return; }
    if (target.kind === 'problems') { appState.problemFilters = { ...appState.problemFilters, severity: target.filters?.severity || '', mapId: target.filters?.mapId || '', workspaceId: '' }; appState.selectedProblemId = null; setViewMode('problems'); return; }
    if (target.kind === 'scenarios') { setViewMode('scenarios'); return; }
    if (target.kind === 'map') { setViewMode('map'); focusSeat(target.mapId, null); }
  }
  function dashboardAction(target, className, title, value, description = '') {
    const item = document.createElement(target ? 'button' : 'div');
    if (target) { item.type = 'button'; item.onclick = () => activateDashboardTarget(target); }
    item.className = className;
    if (title) { const heading = document.createElement('span'); heading.className = 'dashboard-card-label'; heading.textContent = title; item.append(heading); }
    const strong = document.createElement('strong'); strong.textContent = value; item.append(strong);
    if (description) { const detail = document.createElement('small'); detail.textContent = description; item.append(detail); }
    return item;
  }
  function renderDashboard() {
    const view = $('dashboardview'); if (!view || !dashboardHelpers) return;
    measureSync('renderDashboard', () => {
      const model = dashboardModel(); if (!model) return;
      $('dashboard-context').textContent = model.context.label;
      $('dashboard-title').textContent = 'Dashboard';
      const updating = appState.analytics.status === 'running' || appState.validation.status === 'running';
      const failed = appState.analytics.status === 'error' ? String(appState.analytics.error ?? '').trim() : appState.validation.status === 'error' ? String(appState.validation.error ?? '').trim() : '';
      const loading = $('dashboard-loading'); loading.textContent = failed || (updating ? 'Actualizando datos operativos…' : ''); loading.classList.toggle('hidden', !updating && !failed); loading.classList.toggle('problem-error', Boolean(failed));
      $('dashboard-subtitle').textContent = model.context.mode === 'scenario' ? 'Estado efectivo y cambios pendientes del escenario activo.' : 'Estado general del Open Space en REALIDAD.';
      const primaryCards = model.kpiCards.filter(card => card.id !== 'problems').slice(0, 6);
      $('dashboard-kpis').replaceChildren(...primaryCards.map(card => dashboardAction(card.target, `dashboard-kpi dashboard-kpi-${card.id}`, card.label, card.displayValue, card.id === 'occupancy' || card.id === 'availability' ? 'Tasa calculada por Analítica espacial' : '')));
      $('dashboard-problems-total').textContent = model.problems.total ? `${dashboardHelpers.formatNumber(model.problems.total)} problemas` : '✓ Sin problemas de validación';
      const problems = $('dashboard-problems');
      if (!model.problems.total) problems.replaceChildren(dashboardAction(null, 'dashboard-empty dashboard-clear', '', '✓ Sin problemas críticos', 'La validación actual no devuelve incidencias.'));
      else problems.replaceChildren(
        ...model.problems.bySeverity.map(item => dashboardAction(item.target, `dashboard-problem-button severity-${item.severity.toLowerCase()}`, `${item.severity === 'Critical' ? '×' : item.severity === 'Warning' ? '!' : 'i'} ${item.label}`, dashboardHelpers.formatNumber(item.count), `Ver ${item.label.toLowerCase()}`)),
        dashboardAction(model.problems.target, 'dashboard-problem-button dashboard-problems-all', 'Total', dashboardHelpers.formatNumber(model.problems.total), 'Ver todos los problemas')
      );
      const mapHost = $('dashboard-maps');
      if (!model.maps.length) mapHost.replaceChildren(dashboardAction(null, 'dashboard-empty', '', model.emptyStates.maps));
      else mapHost.replaceChildren(...model.maps.map(map => {
        const row = document.createElement('button'); row.type = 'button'; row.className = 'dashboard-map-row'; row.onclick = () => activateDashboardTarget(map.target); row.setAttribute('aria-label', `${map.target.label}. Ocupación ${map.occupancyLabel}; ${map.free} libres; ${map.problems.total} problemas.`);
        const heading = document.createElement('span'); heading.className = 'dashboard-map-heading'; const name = document.createElement('strong'); name.textContent = map.mapName; const counts = document.createElement('small'); counts.textContent = `${dashboardHelpers.formatNumber(map.free)} libres · ${dashboardHelpers.formatNumber(map.problems.total)} problemas`; heading.append(name, counts);
        const rate = document.createElement('span'); rate.className = 'dashboard-map-rate'; rate.textContent = map.occupancyLabel;
        const bar = document.createElement('span'); bar.className = 'dashboard-bar'; bar.setAttribute('role', 'progressbar'); bar.setAttribute('aria-label', `Ocupación de ${map.mapName}`); bar.setAttribute('aria-valuemin', '0'); bar.setAttribute('aria-valuemax', '100'); bar.setAttribute('aria-valuenow', String(map.occupancyRate)); bar.setAttribute('aria-valuetext', map.occupancyLabel); const fill = document.createElement('span'); fill.style.width = `${map.occupancyRate}%`; bar.append(fill); row.append(heading, rate, bar); return row;
      }));
      const availability = $('dashboard-availability');
      if (!model.availabilityRanking.length) availability.replaceChildren(dashboardAction(null, 'dashboard-empty', '', model.emptyStates.availability));
      else availability.replaceChildren(...model.availabilityRanking.map((map, index) => { const item = document.createElement('li'); const button = document.createElement('button'); button.type = 'button'; button.textContent = `${index + 1}. ${map.mapName} · ${map.availabilityLabel} libres`; button.onclick = () => activateDashboardTarget(map.target); item.append(button); return item; }));
      const scenarioHost = $('dashboard-scenario'); $('dashboard-scenario-context').textContent = model.context.mode === 'scenario' ? model.context.label : 'Sin escenario activo';
      if (model.context.mode !== 'scenario') scenarioHost.replaceChildren(dashboardAction({ kind: 'scenarios', label: 'Ver escenarios' }, 'dashboard-scenario-action', 'Escenarios', 'Ver escenarios', 'Crea o selecciona una simulación aislada.'));
      else { const impact = model.scenarioImpact; const compare = document.createElement('button'); compare.type = 'button'; compare.textContent = 'Comparar escenario'; compare.onclick = () => setViewMode('scenarios'); if (!impact.available) scenarioHost.replaceChildren(dashboardAction(null, 'dashboard-empty', 'Comparación', 'Actualizando cambios…', 'El detalle se muestra con el Scenario Diff oficial.'), compare); else { const grid = document.createElement('div'); grid.className = 'dashboard-scenario-metrics'; [['Cambios', impact.total], ['Añadidos', impact.added], ['Eliminados', impact.removed], ['Movidos', impact.moved], ['Modificados', impact.modified], ['Introducidos', impact.validation.introduced], ['Resueltos', impact.validation.resolved], ['Persistentes', impact.validation.persistent]].forEach(([label, value]) => grid.append(dashboardAction(null, 'dashboard-scenario-metric', label, dashboardHelpers.formatNumber(value)))); scenarioHost.replaceChildren(grid, compare); } }
      const attention = $('dashboard-attention'); attention.replaceChildren(dashboardAction(model.attention.target, `dashboard-attention-action ${model.attention.state}`, 'Estado operativo', model.attention.state === 'clear' ? '✓ Sin acciones pendientes' : model.attention.label, model.attention.target ? 'Abrir problemas filtrados' : ''));
      if (!ui.dashboardInitialized && appState.analytics.status === 'ready') { ui.dashboardInitialized = true; reportPlanResourceDiagnostic({ id: 'frontend' }, 'dashboard', 'Dashboard initialized.'); }
    });
  }
  function renderAnalyticsManagedAreas() { const host = $('analytics-managed-areas'); const list = $('analytics-managed-areas-list'); if (!host || !list) return; const areas = managedAreas(); host.classList.toggle('hidden', areas.length === 0); if (!areas.length) { list.replaceChildren(); return; } const table = document.createElement('table'); table.className = 'analytics-table'; table.innerHTML = '<thead><tr><th>Cluster</th><th>Plano</th><th>Total</th><th>Ocupados</th><th>Libres</th><th>Reservados</th><th>Ocupación</th><th>Problemas</th></tr></thead>'; const body = document.createElement('tbody'); areas.forEach(area => { const presentation = areaPresentation(area); const row = document.createElement('tr'); row.innerHTML = `<td><button type="button">${escapeHtml(area.name)}</button></td><td>${escapeHtml(maps().find(map => map.id === area.mapId)?.name || area.mapId)}</td><td>${presentation.counts.total}</td><td>${presentation.counts.occupied}</td><td>${presentation.counts.free}</td><td>${presentation.counts.reserved}</td><td>${presentation.counts.total ? `${Math.round(presentation.counts.occupied / presentation.counts.total * 100)}%` : '—'}</td><td>${presentation.counts.problems}</td>`; row.querySelector('button').onclick = () => openAreaDetail(area.id); body.append(row); }); table.append(body); list.replaceChildren(table); }
  function renderScenarioSpatialComparison() {
    const host = $('scenario-spatial-comparison'); const current = appState.analytics.result; const baseline = appState.analytics.baseline; const visible = Boolean(scenarioId() && current && baseline); host.classList.toggle('hidden', !visible); if (!visible) { host.replaceChildren(); return; } const mode = spatialAnalyticsHelpers.selectMetricMode(appState.analytics.heatmapMode); const scale = spatialAnalyticsHelpers.calculateSharedScale(baseline.maps || [], current.maps || [], item => spatialAnalyticsHelpers.getMapMetric({ maps: [item] }, item.mapId, mode)); const title = document.createElement('h2'); title.textContent = `Impacto espacial · ${spatialAnalyticsHelpers.getLegendMetadata(mode, scale).label}`; const note = document.createElement('p'); note.textContent = `REALIDAD y ESCENARIO usan la misma escala 0–${scale.max}${mode === 'occupancy' || mode === 'availability' ? '%' : ''}. Los deltas de porcentaje se expresan en puntos porcentuales.`; const table = document.createElement('div'); table.className = 'scenario-spatial-table'; (current.maps || []).forEach(metric => { const before = spatialAnalyticsHelpers.getMapMetric(baseline, metric.mapId, mode); const after = spatialAnalyticsHelpers.getMapMetric(current, metric.mapId, mode); const delta = mode === 'occupancy' || mode === 'availability' ? spatialAnalyticsHelpers.percentagePointsDelta(after, before) : after - before; const row = document.createElement('button'); row.type = 'button'; row.textContent = `${metric.mapName || metric.mapId}: ${before}${mode === 'occupancy' || mode === 'availability' ? '%' : ''} → ${after}${mode === 'occupancy' || mode === 'availability' ? '%' : ''} (${delta >= 0 ? '+' : ''}${delta}${mode === 'occupancy' || mode === 'availability' ? ' pp' : ''})`; row.onclick = () => focusSeat(metric.mapId, null); table.append(row); }); host.replaceChildren(title, note, table);
  }

  const actionText = {
    loadInitialData: 'Cargando datos…', reloadData: 'Actualizando datos…', getScenarioDiff: 'Cargando cambios…', getUndoPreview: 'Cargando el último cambio…', getEvents: 'Cargando historial…', getBackups: 'Cargando copias de seguridad…', getIntegrityReport: 'Verificando integridad…',
    saveAssignment: 'Guardando asignación…', deleteAssignment: 'Eliminando asignación…', saveSeatPosition: 'Guardando posición…', createSeat: 'Creando puesto…', deleteSeat: 'Eliminando puesto…', createScenario: 'Creando escenario…', createScenarioFromMovementPlan: 'Creando escenario de movimiento…', runMovementPlanner: 'Planificando movimientos…', deleteScenario: 'Eliminando escenario…', restoreBackup: 'Restaurando copia de seguridad…', undoLastChange: 'Deshaciendo último cambio…', applyScenario: 'Aplicando cambios seleccionados…', bulkUpdateAssignments: 'Aplicando edición masiva…', createManagedArea: 'Creando área…', renameManagedArea: 'Renombrando área…', addManagedAreaWorkspaces: 'Añadiendo puestos al área…', removeManagedAreaWorkspaces: 'Quitando puestos del área…', moveManagedAreaWorkspaces: 'Moviendo puestos entre áreas…', mergeManagedAreas: 'Fusionando áreas…', dissolveManagedArea: 'Disolviendo área…', deleteMoveManagedArea: 'Eliminando área y moviendo puestos…', exportExcel: 'Exportando Excel…'
  };
  const confirmationText = {
    saveAssignment: 'Asignación guardada correctamente.', deleteAssignment: 'Asignación eliminada correctamente.', saveSeatPosition: 'Posición guardada correctamente.', createSeat: 'Puesto creado correctamente.', deleteSeat: 'Puesto eliminado correctamente.', createScenario: 'Escenario creado correctamente.', createScenarioFromMovementPlan: 'Escenario de movimiento creado correctamente.', deleteScenario: 'Escenario eliminado correctamente.', restoreBackup: 'Copia de seguridad restaurada correctamente.', undoLastChange: 'Último cambio deshecho correctamente.', applyScenario: 'Cambios aplicados correctamente.', bulkUpdateAssignments: 'Edición masiva aplicada correctamente.', createManagedArea: 'Área creada correctamente.', renameManagedArea: 'Área renombrada correctamente.', addManagedAreaWorkspaces: 'Puestos añadidos al área.', removeManagedAreaWorkspaces: 'Puestos quitados del área.', moveManagedAreaWorkspaces: 'Puestos movidos entre áreas.', mergeManagedAreas: 'Áreas fusionadas correctamente.', dissolveManagedArea: 'Área disuelta correctamente.', deleteMoveManagedArea: 'Área eliminada y puestos movidos.', exportExcel: 'Excel exportado correctamente.'
  };
  function updateActionableControls() {
    if (ui.busyAction) {
      document.querySelectorAll('button, select, input[type="checkbox"]').forEach(control => {
        if (!ui.disabledControls.has(control)) ui.disabledControls.set(control, control.disabled);
        control.disabled = true;
      });
      return;
    }
    ui.disabledControls.forEach((wasDisabled, control) => { control.disabled = wasDisabled; });
    ui.disabledControls.clear();
  }
  function beginRequest(action) {
    ui.busyAction = action;
    const activeControl = document.activeElement;
    ui.busyControl = activeControl instanceof HTMLElement && activeControl.matches('button, select, input[type="checkbox"]') ? activeControl : null;
    ui.busyControl?.classList.add('is-busy');
    updateActionableControls();
    const text = actionText[action] || 'Procesando solicitud…';
    setStatus(text);
    clearTimeout(ui.busyTimer);
    ui.busyTimer = setTimeout(() => setStatus(`${text} Esperando respuesta…`), 2000);
  }
  function finishRequest(success, error) {
    const action = ui.busyAction;
    if (!action) return;
    clearTimeout(ui.busyTimer);
    ui.busyTimer = null;
    ui.busyAction = null;
    ui.busyControl?.classList.remove('is-busy');
    ui.busyControl = null;
    updateActionableControls();
    if (ui.state) render();
    if (!success) {
      setPersistence(/conflict|revisi[oó]n/i.test(error || '') ? 'conflict' : 'error', error); showMessage(error || 'La operación no se pudo completar.', 0);
      setStatus('Error');
      return;
    }
    const confirmation = action === 'bulkUpdateAssignments' && ui.bulkConfirmation ? ui.bulkConfirmation : confirmationText[action];
    const warning = ui.pendingWarning;
    ui.bulkConfirmation = null;
    ui.pendingWarning = null;
    if (confirmation) {
      setPersistence('saved'); const message = warning ? `${confirmation} ${warning}` : confirmation;
      showMessage(message, warning ? 12000 : 5000);
      setStatus(message);
    }
  }
  function cancelRequest() { clearTimeout(ui.busyTimer); ui.busyTimer = null; ui.busyAction = null; ui.busyControl?.classList.remove('is-busy'); ui.busyControl = null; updateActionableControls(); if (ui.state) render(); setStatus('Exportación cancelada.'); }
  function send(action, payload = {}, bypassBusy = false) {
    if (ui.busyAction && !bypassBusy) return false;
    if (!bypassBusy) { beginRequest(action); if (/save|delete|create|apply|undo|bulk/i.test(action)) setPersistence('saving'); }
    const message = { action, requestId: window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random()}`, payload };
    if (window.chrome?.webview?.postMessage) window.chrome.webview.postMessage(message);
    else if (window.webkit?.messageHandlers?.plano?.postMessage) window.webkit.messageHandlers.plano.postMessage(message);
    else finishRequest(false, 'No hay puente nativo disponible.');
    return true;
  }
  function notify(type, message, options = {}) { ui.placementAnnouncement = false; const toast = $('toast'); const duration = options.duration ?? (type === 'error' ? 12000 : type === 'warning' ? 8000 : 4000); toast.className = `toast ${type}`; toast.replaceChildren(); const text = document.createElement('span'); text.textContent = message; const close = document.createElement('button'); close.type = 'button'; close.className = 'icon-only'; close.setAttribute('aria-label', 'Cerrar notificación'); close.textContent = '×'; close.onclick = () => toast.classList.add('hidden'); toast.append(text, close); toast.classList.remove('hidden'); if (duration) setTimeout(() => toast.classList.add('hidden'), duration); }
  function showMessage(text, duration = 5000) { const message = $('message'); message.textContent = text || ''; message.classList.toggle('is-error', Boolean(text) && !duration); if (text) notify(duration ? 'success' : 'error', text, { duration: duration || 12000 }); }
  function setPersistence(status, error = null) { appState.persistence.status = status; appState.persistence.error = error; if (status === 'saved') appState.persistence.lastSavedAt = new Date().toISOString(); const labels = { idle: 'Listo', dirty: 'Cambios pendientes', saving: 'Guardando…', saved: '✓ Guardado', error: '× Error al guardar', conflict: '⚠ Conflicto de datos' }; $('status').textContent = labels[status] || status; }
  function setStatus(text) { $('status').textContent = text; }
  function zoomStatus() { return `${Math.round(ui.currentScale * 100)}%`; }
  function nameFor(items, id) { const item = items.find(value => value.id === id); return item?.username || item?.name || item?.label || id || 'Sin asignar'; }
  function assignmentFor(seatId) { return assignments().find(item => item.workstationId === seatId) || {}; }
  function seatValues(seat) { const a = assignmentFor(seat.id); return { personId: a.personId ?? seat.personId, deviceId: a.deviceId ?? seat.deviceId ?? seat.deviceName, roseta: a.roseta ?? seat.roseta, locationId: a.locationId ?? seat.location }; }
  function resolvedName(items, id) { const item = items.find(value => value.id === id); return item?.username || item?.name || item?.label || ''; }
  function effectiveWorkspaceState(seat) { return workspaceStateHelpers.deriveEffectiveWorkspaceState({ seat, assignment: assignmentFor(seat.id) }); }
  function workspacePresentation(seat) { const values = seatValues(seat); const effectiveState = effectiveWorkspaceState(seat); const personName = resolvedName(people(), values.personId); const equipmentName = resolvedName(devices(), values.deviceId); const problems = getProblemsForWorkspace(seat.id); return workspacePresentationHelpers.buildWorkspacePresentation({ seat, assignment: assignmentFor(seat.id), effectiveState, displayLocation: displayLocationFor(seat), personName, equipmentName, problemSeverity: getWorkspaceMaxSeverity(seat.id), problemCount: problems.length }); }
  function seatType(seat) { return effectiveWorkspaceState(seat).state; }
  function workspaceQuality(seat) { const effective = effectiveWorkspaceState(seat); return workspaceQualityHelpers.deriveWorkspaceQuality({ effectiveState: effective.state, seat, assignment: assignmentFor(seat.id), currentPerson: effective.currentPersonId }); }
  function seatCompleteness(seat) { return workspaceQuality(seat).qualityState === 'incomplete' ? 'incomplete' : 'complete'; }
  function scenarioStateForSeat(seatId) {
    if (!scenarioId()) return 'none';
    const change = ui.changes.find(item => (item.seatId || item.entityId || item.after?.seatId || item.before?.seatId) === seatId);
    return String(change?.kind || change?.type || 'none').toLowerCase();
  }
  function clean(value) { return value === null || value === undefined || value === '' ? null : value; }
  const grid = () => ({ columns: Number(ui.state?.grid?.columns) || 24, rows: Number(ui.state?.grid?.rows) || 18 });
  const gridCursorHelpers = window.GridCursorHelpers;
  function columnName(index) { let name = ''; for (index++; index > 0; index = Math.floor((index - 1) / 26)) name = String.fromCharCode(65 + (index - 1) % 26) + name; return name; }
  function gridCellAt(x, y) { const { columns, rows } = grid(); const column = Math.max(0, Math.min(columns - 1, Math.floor(x * columns))); const row = Math.max(0, Math.min(rows - 1, Math.floor(y * rows))); return `${columnName(column)}-${String(row + 1).padStart(2, '0')}`; }
  function displayLocationFor(seat) { return seat?.displayLocation || seat?.gridCell || gridCellAt(Number(seat?.x), Number(seat?.y)); }
  function renderGridLabels() { const { columns, rows } = grid(); const labels = $('grid-labels'); labels.replaceChildren(); for (let column = 0; column < columns; column++) { const label = document.createElement('span'); label.className = 'grid-column-label'; label.textContent = columnName(column); label.style.left = `${((column + .5) / columns) * 100}%`; labels.append(label); } for (let row = 0; row < rows; row++) { const label = document.createElement('span'); label.className = 'grid-row-label'; label.textContent = String(row + 1).padStart(2, '0'); label.style.top = `${((row + .5) / rows) * 100}%`; labels.append(label); } }
  function renderPlacementCursor() { const cursor = ui.placementCursor; const target = $('grid-cursor'); target.classList.toggle('hidden', !cursor); if (!cursor) return; target.style.left = `${cursor.x * 100}%`; target.style.top = `${cursor.y * 100}%`; }
  function clearPlacementAnnouncement() { if (!ui.placementAnnouncement) return; ui.placementAnnouncement = false; $('toast').classList.add('hidden'); }
  function announcePlacementCursor(cursor) { const cell = gridCursorHelpers.labelFor(gridCursorHelpers.cellAt(cursor, grid())); const action = cursor.kind === 'move' ? 'Mover puesto' : 'Añadir puesto'; const completion = cursor.kind === 'move' ? 'mover' : 'crear'; const toast = $('toast'); ui.placementAnnouncement = true; toast.className = 'toast success placement-announcement'; toast.textContent = `${action}: destino ${cell}. Pulsa Intro para ${completion} o Escape para cancelar.`; toast.classList.remove('hidden'); }
  function clearPlacementCursor(kind = null) { if (!ui.placementCursor || (kind && ui.placementCursor.kind !== kind)) return; ui.placementCursor = null; clearPlacementAnnouncement(); renderPlacementCursor(); }
  function beginMoveMode() { const seat = currentSeat(); if (!seat) return false; setAddMode(false); ui.movingSeat = true; ui.placementCursor = { kind: 'move', seatId: seat.id, x: Number(seat.x), y: Number(seat.y) }; setViewMode('map'); renderPlacementCursor(); setStatus('Mover puesto: usa las flechas para elegir el destino, Intro para confirmar o Escape para cancelar.'); wrap.focus({ preventScroll: true }); return true; }
  function movePlacementCursor(direction) { const cursor = ui.placementCursor; if (!cursor) return false; const next = gridCursorHelpers.move(cursor, direction, grid()); if (!next.changed) return false; ui.placementCursor = { ...cursor, x: next.x, y: next.y }; renderPlacementCursor(); announcePlacementCursor(ui.placementCursor); return true; }
  function cancelPlacementMode() { const cursor = ui.placementCursor; if (!cursor) return false; if (cursor.kind === 'move') { ui.movingSeat = false; clearPlacementCursor('move'); setStatus('Movimiento cancelado.'); } else { setAddMode(false); setStatus('Creación de puesto cancelada.'); } return true; }
  function confirmPlacementCursor() { const cursor = ui.placementCursor; if (!cursor) return false; if (cursor.kind === 'move') { ui.movingSeat = false; clearPlacementCursor('move'); return moveWorkspace(cursor.seatId, cursor.x, cursor.y); } const context = ui.addingContext; if (context?.targetManagedAreaId) { const area = managedArea(context.targetManagedAreaId); if (!area || area.mapId !== ui.mapId) { setAddMode(false); notify('warning', 'La zona gestionada ya no existe o ya no pertenece a este plano. No se creó ningún puesto.'); return false; } } const sent = send('createSeat', payloadForScenario({ mapId: ui.mapId, x: cursor.x, y: cursor.y, ...(context?.targetManagedAreaId ? { targetManagedAreaId: context.targetManagedAreaId } : {}) })); setAddMode(false); return sent; }
  const clamp = (value, minimum, maximum) => Math.min(maximum, Math.max(minimum, value));
  const near = (first, second) => Math.abs(first - second) < 0.0001;
  function getSemanticZoomLevel(zoom, previous = 'GLOBAL') { if (previous === 'DETAIL') return zoom < 1.8 ? 'OPERATIVE' : 'DETAIL'; if (previous === 'OPERATIVE') return zoom >= 2 ? 'DETAIL' : zoom < 1.15 ? 'GLOBAL' : 'OPERATIVE'; return zoom >= 1.25 ? 'OPERATIVE' : 'GLOBAL'; }
  function updateSemanticZoom() { const next = getSemanticZoomLevel(ui.currentScale, appState.semanticZoomLevel || 'GLOBAL'); if (next === appState.semanticZoomLevel) return; appState.semanticZoomLevel = next; $('stage').dataset.semanticZoom = next.toLowerCase(); if (ui.state) requestAnimationFrame(() => render()); }
  function updateLayerPresentation() { const layers = appState.layers; $('pins').classList.toggle('hidden', !layers.seats); $('grid').classList.toggle('hidden', !layers.grid); $('grid-labels').classList.toggle('hidden', !layers.grid); $('stage').classList.toggle('show-labels', Boolean(layers.labels)); $('stage').classList.toggle('show-people', Boolean(layers.people)); $('stage').classList.toggle('show-devices', Boolean(layers.devices)); $('stage').classList.toggle('show-network', Boolean(layers.network)); $('stage').classList.toggle('show-problems', Boolean(layers.problems)); renderHeatmap(); }
  function requestViewportRender() { if (!ui.frame) ui.frame = requestAnimationFrame(renderViewport); }
  function renderViewport() {
    ui.frame = null;
    const follow = 0.15;
    ui.currentScale += (ui.targetScale - ui.currentScale) * follow;
    ui.currentX += (ui.targetX - ui.currentX) * follow;
    ui.currentY += (ui.targetY - ui.currentY) * follow;
    if (ui.zoomAnchor && !near(ui.currentScale, ui.targetScale)) {
      ui.currentX = ui.zoomAnchor.screenX - ui.zoomAnchor.worldX * ui.currentScale;
      ui.currentY = ui.zoomAnchor.screenY - ui.zoomAnchor.worldY * ui.currentScale;
    }
    const stage = $('stage');
    appState.zoom = ui.currentScale; appState.pan = { x: ui.currentX, y: ui.currentY }; updateSemanticZoom();
    stage.style.transform = `translate3d(${ui.currentX}px, ${ui.currentY}px, 0) scale(${ui.currentScale})`;
    if (!ui.busyAction) setStatus(zoomStatus());
    if (ui.dragVisual) { ui.dragVisual.pin.style.left = `${ui.dragVisual.x * 100}%`; ui.dragVisual.pin.style.top = `${ui.dragVisual.y * 100}%`; }
    if (!near(ui.currentScale, ui.targetScale) || !near(ui.currentX, ui.targetX) || !near(ui.currentY, ui.targetY) || ui.dragVisual) requestViewportRender();
  }
  function applyViewport(viewport) { const next = mapViewportHelpers.snapshotViewport(viewport); ui.currentScale = ui.targetScale = next.scale; ui.currentX = ui.targetX = next.x; ui.currentY = ui.targetY = next.y; ui.zoomAnchor = null; requestViewportRender(); }
    function resetViewport(mapId = ui.mapId) { applyViewport(ui.initialViewports.get(mapId) || { scale: 1, x: 0, y: 0 }); }
    function fitInitialMap(mapId) { if (!mapId || ui.initialViewports.has(mapId)) return; requestAnimationFrame(() => { if (ui.initialViewports.has(mapId) || mapId !== ui.mapId) return; const width = $('plan').offsetWidth; const height = $('plan').offsetHeight; if (!width || !height || !wrap.clientWidth || !wrap.clientHeight) return; const fitted = mapViewportHelpers.calculateInitialFit({ width: wrap.clientWidth, height: wrap.clientHeight }, { width, height }, { minimumScale: .1, maximumScale: 1 }); ui.initialViewports.set(mapId, fitted); applyViewport(fitted); }); }
    function loadMapAppearanceManifest() { fetch('map-themes/light/manifest.json', { cache: 'no-store' }).then(response => response.ok ? response.json() : null).then(manifest => { if (!manifest) return; appState.mapAppearanceManifest = mapAppearanceHelpers.normalizeManifest(manifest); if (ui.state) render(); }).catch(() => { /* Canonical fallback assets remain available. */ }); }

  function populateLists() {
    const rosetas = [...new Set([...assignments().map(a => a.roseta), ...maps().flatMap(map => seats(map).map(seat => seat.roseta))].filter(Boolean))];
    $('rosetas-list').replaceChildren(...rosetas.map(value => { const option = document.createElement('option'); option.value = value; return option; }));
    const zone = $('filter-zone'); if (zone) zone.replaceChildren(new Option('Todas', ''), ...maps().map(map => new Option(map.name || map.id, map.id)));
  }
  function renderMode() {
    const select = $('scenario-mode'); const selected = scenarioId() || 'real';
    appState.activeScenario = scenarioId(); appState.dataContext = scenarioId() ? 'scenario' : 'real';
    select.replaceChildren();
    const real = new Option('Realidad confirmada', 'real'); select.add(real);
    list(ui.state?.scenarios, 'scenarios').forEach(item => select.add(new Option(`Escenario · ${item.name || item.id}`, item.id)));
    select.value = selected;
    select.title = select.selectedOptions[0]?.text || '';
    $('diff').disabled = !scenarioId(); $('apply').disabled = !scenarioId() || ui.selectedCompareUnitIds.size === 0; $('delete-scenario').disabled = !scenarioId() || scenario()?.isPrimary === true;
    $('undo').disabled = scenarioId() ? !(scenario()?.undoCount > 0) : false;
    $('scenario-note').textContent = scenarioId()
      ? `Propuesta «${scenario()?.name || scenarioId()}»: edita sin riesgo, revisa Diff y aplica sólo los cambios seleccionados. Los marcadores de escenario son cambios pendientes.`
      : 'Realidad confirmada: los cambios se guardan y se auditan. Para probar una mudanza o reorganización, crea un escenario primero.';
  }
  function resourceFor(map, appearance = appState.mapAppearance) { const resource = map?.image || map?.imageUrl || map?.path || ''; return mapAppearanceHelpers ? mapAppearanceHelpers.resolveMapPresentationAsset(resource, appearance, appState.mapAppearanceManifest) : resource; }
    function canonicalResourceFor(map) { return map?.image || map?.imageUrl || map?.path || ''; }
  function updatePlanDiagnostic() {
    const diagnostics = ui.planResources;
    const target = $('plan-diagnostic');
    if (!target) return;
    const loaded = diagnostics.loaded.size;
    const failed = diagnostics.failed.size;
    target.classList.toggle('has-error', failed > 0);
    target.textContent = `Planos configurados: ${diagnostics.expected} · cargados: ${loaded}${failed ? ` · errores: ${failed}` : ''}`;
    if (diagnostics.expected && loaded + failed >= diagnostics.expected && !diagnostics.summaryReported) { diagnostics.summaryReported = true; reportPlanResourceDiagnostic({ id: 'frontend' }, 'svg-preload', `SVG ${loaded}/${diagnostics.expected}${failed ? `; errores: ${failed}` : ''}`); }
  }
  function reportPlanResourceDiagnostic(map, resource, result) {
    send('reportPlanResourceDiagnostic', { mapId: map?.id || '', resource, result }, true);
  }
  function reportPlanResourceFailure(map, resource, error) {
    const diagnostic = { mapId: map?.id || '', resource, error: error || 'No se pudo cargar el recurso SVG.' };
    ui.planResources.failed.set(diagnostic.mapId, diagnostic);
    updatePlanDiagnostic();
    reportPlanResourceDiagnostic(map, resource, diagnostic.error);
  }
  function checkPlanResources() {
    const configuredMaps = maps();
    const diagnostics = ui.planResources = { expected: configuredMaps.length, loaded: new Set(), failed: new Map(), images: [], summaryReported: false };
    updatePlanDiagnostic();
    configuredMaps.forEach(map => {
      const resource = resourceFor(map);
      if (!resource) {
        reportPlanResourceFailure(map, resource, 'El mapa no define un recurso SVG.');
        return;
      }
      const image = new Image();
      diagnostics.images.push(image);
      image.onload = () => { if (ui.planResources !== diagnostics) return; diagnostics.loaded.add(map.id); updatePlanDiagnostic(); reportPlanResourceDiagnostic(map, resource, 'SVG cargado.'); };
      image.onerror = () => { if (ui.planResources === diagnostics) reportPlanResourceFailure(map, resource, `No se pudo cargar ${resource}.`); };
      image.src = resource;
    });
    // WebView2 can occasionally omit Image.onload for a cached external SVG. Verify only unresolved local assets once so diagnostics describe actual availability rather than that event race.
    setTimeout(() => {
      if (ui.planResources !== diagnostics) return;
      configuredMaps.forEach(map => {
        if (diagnostics.loaded.has(map.id) || diagnostics.failed.has(map.id)) return;
        const resource = resourceFor(map);
        fetch(resource, { cache: 'no-store' }).then(response => {
          if (ui.planResources !== diagnostics) return;
          if (!response.ok) throw new Error(`HTTP ${response.status}`);
          diagnostics.loaded.add(map.id); updatePlanDiagnostic(); reportPlanResourceDiagnostic(map, resource, 'SVG verificado.');
        }).catch(error => { if (ui.planResources === diagnostics) reportPlanResourceFailure(map, resource, `No se pudo verificar ${resource}: ${error.message}`); });
      });
    }, 5000);
    // Ensure the preload summary is emitted even if WebView2 completes all cached Image events without a final layout turn.
    setTimeout(() => {
      if (ui.planResources !== diagnostics || !diagnostics.expected || diagnostics.summaryReported) return;
      diagnostics.summaryReported = true;
      const loaded = diagnostics.loaded.size; const failed = diagnostics.failed.size;
      reportPlanResourceDiagnostic({ id: 'frontend' }, 'svg-preload', `SVG ${loaded}/${diagnostics.expected}${failed ? `; errores: ${failed}` : ''}`);
    }, 5500);
  }
  function renderMapSelector() {
    const select = $('map-select');
    if (!select) return;
    select.replaceChildren(...maps().map(map => new Option(map.name || map.id, map.id)));
    select.value = ui.mapId || '';
  }
  function managedAreas(mapId = null) { return appState.managedAreas.areas.filter(area => !mapId || area.mapId === mapId); }
  function managedArea(areaId) { return appState.managedAreas.areas.find(area => area.id === areaId) || null; }
  function managedMemberIds(mapId) { return new Set(managedAreas(mapId).flatMap(area => area.workspaceIds)); }

  function clusterCardLayout(areaId) { const editing = ui.cardEdit?.active && ui.cardEdit.areaId === areaId ? ui.cardEdit.draft : null; return editing || clusterCardEditHelpers.normalizeLayout(appState.clusterCardShapes?.[areaId], clusterCardShapeHelpers.normalizeClusterCardShape); }
  function clusterCardShape(areaId) { return clusterCardLayout(areaId).shape; }
  function saveClusterCardShapes() { try { localStorage.setItem('plano.clusterCardShapes', JSON.stringify(appState.clusterCardShapes)); } catch { /* Presentation preference is best-effort. */ } }
  function areaPresentation(area) { return managedAreaHelpers.deriveAreaPresentation({ ...area, presentation: { offsetX: 0, offsetY: 0 } }, seats(maps().find(map => map.id === area.mapId)), { stateFor: seat => seatType(seat), problemsFor: seat => getProblemsForWorkspace(seat.id).length }); }
  function sendManagedArea(operation, payload) { const command = managedAreaHelpers.buildBackendCommand(operation, payload); return send(command.action, command.payload); }
  function setDetailHeader(mode, values = {}) { return detailPanelControllerFeature.setHeader(mode, values); }
  function showDetailMode(mode, values = {}) { return detailPanelControllerFeature.show(mode, values); }
  function plannerLocations() { return Object.fromEntries(allSeats().map(seat => [seat.id, { displayLocation: displayLocationFor(seat), mapId: seat._mapId, name: seat.name || 'Puesto' }])); }
  function plannerAssignments() { return Object.fromEntries(assignments().map(item => [item.workstationId, item])); }
  function plannerWorkspaces() { return Object.fromEntries(allSeats().map(seat => { const legacyPersonId = String(seat.personId || '').trim(); const legacyDeviceName = String(seat.deviceName || '').trim(); return [seat.id, { effectiveState: effectiveWorkspaceState(seat).state, assignment: assignmentFor(seat.id), legacyPersonId, legacyPersonResolved: !legacyPersonId || people().filter(person => person.id === legacyPersonId).length === 1, legacyDeviceResolved: !legacyDeviceName || devices().filter(device => device.id && device.name === legacyDeviceName).length === 1 }]; })); }
  function plannerState() { return appState.planner; }
  function plannerPairing() { const planner = plannerState(); return movementPlannerHelpers.buildPairs(planner.sourceIds, planner.destinationIds, planner.excludedSourceIds, plannerLocations()); }
  function plannerWorkspace(id) { return allSeats().find(seat => seat.id === id) || null; }
  function plannerDisplay(id) { const seat = plannerWorkspace(id); return seat ? workspacePresentation(seat).displayLocation : 'Ubicación no indicada'; }
  function plannerPerson(id) { const seat = plannerWorkspace(id); return seat ? workspacePresentation(seat).currentPerson || 'Sin asignar' : 'Sin asignar'; }
  function plannerAvailability(id) { const planner = plannerState(); if (planner.sourceIds.includes(id)) return 'source'; if (planner.destinationIds.includes(id)) return 'destination'; const seat = plannerWorkspace(id); if (!seat || effectiveWorkspaceState(seat).state !== 'free') return 'unavailable'; return assignmentFor(id).workstationId ? 'unavailable' : 'available'; }
  function resetPlanner() { appState.planner = movementPlannerHelpers.createPlannerState(); $('planner-panel').classList.add('hidden'); render(); renderBulkBar(); }
  function startPlanner() {
    if (appState.selectedWorkspaces.size < 2) return;
    if (scenarioId()) { notify('warning', 'El planificador crea un nuevo escenario desde REALIDAD. Vuelve a REALIDAD para iniciar el plan.'); return; }
    const classified = movementPlannerHelpers.classifyEffectiveSources([...appState.selectedWorkspaces], plannerWorkspaces(), plannerLocations());
    const planner = movementPlannerHelpers.createPlannerState();
    planner.status = 'selectingSources'; planner.step = 'sources'; planner.sourceIds = classified.movable; planner.sourceIssues = classified.unavailable;
    appState.planner = planner; setSelectionMode(false); closeDetailPanel({ render: false }); setViewMode('map'); $('planner-panel').classList.remove('hidden'); renderPlanner(); render();
  }
  function beginDestinationSelection(overrideSourceId = null) {
    const planner = plannerState(); if (!planner.sourceIds.length) return;
    if (!overrideSourceId) planner.requestPairs = [];
    planner.status = 'selectingDestinations'; planner.step = 'destinations'; planner.destinationMode = true; planner.overrideSourceId = overrideSourceId; planner.error = null;
    setSelectionMode(false); closeDetailPanel({ render: false }); $('planner-panel').classList.remove('hidden'); setViewMode('map'); renderPlanner(); render();
  }
  function selectPlannerDestination(workspaceId) {
    const planner = plannerState(); if (!planner.destinationMode) return false;
    if (planner.sourceIds.includes(workspaceId)) { notify('warning', 'El origen y el destino son el mismo puesto.'); return true; }
    if (planner.destinationIds.includes(workspaceId)) { notify('warning', 'Destino ya utilizado en este plan.'); return true; }
    if (plannerAvailability(workspaceId) !== 'available') { notify('warning', 'Ese puesto no está disponible como destino.'); return true; }
    if (planner.overrideSourceId) {
      planner.requestPairs = movementPlannerHelpers.overridePair(planner.requestPairs, planner.overrideSourceId, workspaceId);
      planner.destinationIds = planner.requestPairs.map(pair => pair.destinationWorkspaceId);
      planner.overrideSourceId = null; planner.destinationMode = false; runMovementPlan(); return true;
    }
    planner.destinationIds = planner.destinationIds.includes(workspaceId) ? planner.destinationIds.filter(id => id !== workspaceId) : [...planner.destinationIds, workspaceId];
    renderPlanner(); render(); return true;
  }
  function runMovementPlan() {
    const planner = plannerState(); const pairing = planner.requestPairs.length && planner.overrideSourceId === null && planner.status === 'selectingDestinations' ? { pairs: planner.requestPairs, unassigned: [], excluded: planner.excludedSourceIds } : plannerPairing();
    if (!pairing.pairs.length) { notify('warning', 'Selecciona al menos un destino disponible.'); return; }
    planner.requestPairs = pairing.pairs; planner.status = 'planning'; planner.step = 'review'; planner.destinationMode = false; planner.error = null; renderPlanner(); render();
    send('runMovementPlanner', { scenarioId: scenarioId() || undefined, requests: pairing.pairs });
  }
  function applyMovementPlan(data) { const planner = plannerState(); planner.plan = data || { proposals: [], issues: [] }; planner.status = 'review'; planner.step = 'review'; planner.error = null; planner.selectedProposalId = planner.plan.proposals?.some(item => item.id === planner.selectedProposalId) ? planner.selectedProposalId : null; renderPlanner(); render(); }
  function plannerProposal(id) { return plannerState().plan?.proposals?.find(proposal => proposal.id === id) || null; }
  function selectPlannerProposal(id) { plannerState().selectedProposalId = id; renderPlanner(); render(); }
  function excludePlannerSource(id) { const planner = plannerState(); if (!planner.excludedSourceIds.includes(id)) planner.excludedSourceIds.push(id); planner.destinationIds = planner.destinationIds.filter(destination => planner.requestPairs.find(pair => pair.sourceWorkspaceId === id)?.destinationWorkspaceId !== destination); planner.requestPairs = planner.requestPairs.filter(pair => pair.sourceWorkspaceId !== id); planner.plan = null; planner.status = 'selectingDestinations'; planner.step = 'destinations'; renderPlanner(); render(); }
  function createPlannerScenario() { const planner = plannerState(); const proposals = planner.plan?.proposals || []; const name = $('planner-scenario-name')?.value.trim() || 'Plan de movimiento'; if (!proposals.length) return; const payload = movementPlannerHelpers.serializeCreationRequest(name, proposals.map(proposal => ({ sourceWorkspaceId: proposal.source.workspaceId, destinationWorkspaceId: proposal.destination.workspaceId }))); planner.status = 'creatingScenario'; planner.step = 'create'; planner.error = null; renderPlanner(); send('createScenarioFromMovementPlan', payload); }
  function renderPlanner() {
    const panel = $('planner-panel'); if (!panel || plannerState().status === 'idle') return;
    const planner = plannerState(); panel.classList.remove('hidden'); $('planner-context').textContent = scenarioId() ? `Contexto actual: escenario «${scenario()?.name || scenarioId()}». El nuevo plan se guardará en un escenario aislado.` : 'Contexto: REALIDAD. El plan nunca modifica la realidad directamente.';
    const active = planner.step === 'sources' ? 0 : planner.step === 'destinations' ? 1 : planner.step === 'review' ? 2 : 3;
    document.querySelectorAll('[data-planner-step]').forEach((item, index) => { item.classList.toggle('active', index === active); item.classList.toggle('complete', index < active); });
    const content = $('planner-content'); content.replaceChildren();
    if (planner.error) { const error = document.createElement('p'); error.className = 'problems-state problem-error'; error.textContent = planner.error; content.append(error); }
    const section = (title, body) => { const host = document.createElement('section'); host.className = 'planner-section'; const heading = document.createElement('h3'); heading.textContent = title; host.append(heading, body); content.append(host); return host; };
    const text = value => { const node = document.createElement('p'); node.className = 'planner-intro'; node.textContent = value; return node; };
    if (planner.status === 'selectingSources') {
      section('Origen', text(planner.sourceIds.length ? `Seleccionados: ${planner.sourceIds.length + planner.sourceIssues.length} · movibles: ${planner.sourceIds.length} · no utilizables: ${planner.sourceIssues.length}` : 'No hay puestos seleccionados que puedan moverse.'));
      const sourceList = document.createElement('div'); sourceList.className = 'planner-source-list'; planner.sourceIds.forEach(id => { const row = document.createElement('div'); row.className = 'planner-row'; row.innerHTML = `<span><strong>${escapeHtml(plannerPerson(id))}</strong><small>● Origen · ${escapeHtml(plannerDisplay(id))}</small></span><span class="planner-symbol planner-source-symbol" aria-hidden="true">●</span>`; sourceList.append(row); }); planner.sourceIssues.forEach(issue => { const row = document.createElement('div'); row.className = 'planner-row'; row.innerHTML = `<span><strong>${escapeHtml(plannerDisplay(issue.workspaceId))}</strong><small>× ${escapeHtml(issue.message)}</small></span><span class="planner-symbol planner-block-symbol" aria-hidden="true">×</span>`; sourceList.append(row); }); section('Asignaciones seleccionadas', sourceList);
      const actions = document.createElement('div'); actions.className = 'planner-actions'; const next = document.createElement('button'); next.type = 'button'; next.className = 'primary'; next.textContent = 'Seleccionar destinos'; next.disabled = !planner.sourceIds.length; next.onclick = () => beginDestinationSelection(); actions.append(next); content.append(actions);
    } else if (planner.status === 'selectingDestinations') {
      const pairing = plannerPairing(); section('Destino', text(planner.overrideSourceId ? `Elige un nuevo destino para ${plannerPerson(planner.overrideSourceId)} · ${plannerDisplay(planner.overrideSourceId)}.` : `Haz clic en puestos libres del plano. ● Orígenes: ${planner.sourceIds.length} · ◎ destinos: ${planner.destinationIds.length} · × no disponibles.`));
      const summary = document.createElement('div'); summary.className = 'planner-summary'; [['Orígenes', planner.sourceIds.length], ['Destinos', planner.destinationIds.length], ['Planificados', pairing.pairs.length], ['Sin destino', pairing.unassigned.length]].forEach(([label, value]) => { const item = document.createElement('div'); item.innerHTML = `<strong>${value}</strong><span>${label}</span>`; summary.append(item); }); section('Selección', summary);
      if (pairing.unassigned.length) { const list = document.createElement('div'); list.className = 'planner-source-list'; pairing.unassigned.forEach(id => { const row = document.createElement('div'); row.className = 'planner-row'; const exclude = document.createElement('button'); exclude.type = 'button'; exclude.textContent = 'Excluir'; exclude.onclick = () => excludePlannerSource(id); row.innerHTML = `<span><strong>${escapeHtml(plannerPerson(id))}</strong><small>Sin destino · ${escapeHtml(plannerDisplay(id))}</small></span>`; row.append(exclude); list.append(row); }); section('Sin destino', list); }
      const actions = document.createElement('div'); actions.className = 'planner-actions'; const generate = document.createElement('button'); generate.type = 'button'; generate.className = 'primary'; generate.textContent = planner.overrideSourceId ? 'Actualizar propuesta' : 'Generar propuesta'; generate.disabled = !pairing.pairs.length; generate.onclick = runMovementPlan; const back = document.createElement('button'); back.type = 'button'; back.textContent = 'Revisar orígenes'; back.onclick = () => { planner.status = 'selectingSources'; planner.step = 'sources'; planner.destinationMode = false; planner.overrideSourceId = null; renderPlanner(); render(); }; actions.append(back, generate); content.append(actions);
    } else {
      const pairing = plannerPairing(); const review = movementPlannerHelpers.reviewSummary(planner.plan, pairing.unassigned, planner.excludedSourceIds); section(planner.status === 'planning' ? 'Propuesta' : 'Resumen', text(planner.status === 'planning' ? 'Planificando…' : `${review.planned} planificados · ${review.unassigned} sin destino · ${review.blocked} bloqueados.`));
      const summary = document.createElement('div'); summary.className = 'planner-summary'; [['Planificados', review.planned], ['Sin destino', review.unassigned], ['Bloqueados', review.blocked], ['Problemas', review.critical + review.warning + review.info]].forEach(([label, value]) => { const item = document.createElement('div'); item.innerHTML = `<strong>${value}</strong><span>${label}</span>`; summary.append(item); }); section('Impacto', summary);
      if (planner.status !== 'planning') { const proposals = document.createElement('div'); proposals.className = 'planner-proposal-list'; (planner.plan?.proposals || []).forEach(proposal => { const row = document.createElement('div'); row.className = `planner-row${proposal.id === planner.selectedProposalId ? ' selected' : ''}`; const actions = document.createElement('span'); const select = document.createElement('button'); select.type = 'button'; select.textContent = 'Ver'; select.onclick = () => selectPlannerProposal(proposal.id); const origin = document.createElement('button'); origin.type = 'button'; origin.textContent = 'Origen'; origin.onclick = () => navigateToWorkspace({ workspaceId: proposal.source.workspaceId, mapId: proposal.source.mapId, highlight: 'planner' }); const destination = document.createElement('button'); destination.type = 'button'; destination.textContent = 'Destino'; destination.onclick = () => navigateToWorkspace({ workspaceId: proposal.destination.workspaceId, mapId: proposal.destination.mapId, highlight: 'planner' }); const change = document.createElement('button'); change.type = 'button'; change.textContent = 'Cambiar destino'; change.onclick = () => beginDestinationSelection(proposal.source.workspaceId); actions.append(select, origin, destination, change); row.innerHTML = `<span><strong>${escapeHtml(proposal.source.personId ? nameFor(people(), proposal.source.personId) : 'Asignación')}</strong><small>● ${escapeHtml(proposal.source.displayLocation)} → ◎ ${escapeHtml(proposal.destination.displayLocation)} · ✓ Válido</small></span>`; row.append(actions); proposals.append(row); }); section('Propuestas', proposals);
        if (planner.plan?.issues?.length) { const issues = document.createElement('div'); issues.className = 'planner-issue-list'; planner.plan.issues.forEach(issue => { const row = document.createElement('div'); row.className = 'planner-row'; row.innerHTML = `<span><strong>× Bloqueado</strong><small>${escapeHtml(issue.message)}</small></span><span class="planner-symbol planner-block-symbol" aria-hidden="true">×</span>`; issues.append(row); }); section('Bloqueados', issues); }
        const create = document.createElement('section'); create.className = 'planner-section planner-create'; const heading = document.createElement('h3'); heading.textContent = 'Crear escenario'; const label = document.createElement('label'); label.textContent = 'Nombre del escenario'; const input = document.createElement('input'); input.id = 'planner-scenario-name'; input.maxLength = 100; input.value = 'Plan de movimiento'; label.append(input); const confirmation = document.createElement('div'); confirmation.className = 'planner-confirmation'; confirmation.innerHTML = `<p>${review.planned} movimientos · ${review.unassigned + review.excluded} excluidos/sin destino · ${review.critical} críticos · ${review.warning} advertencias.</p>`; const actions = document.createElement('div'); actions.className = 'planner-actions'; const revise = document.createElement('button'); revise.type = 'button'; revise.textContent = 'Ajustar destinos'; revise.onclick = () => beginDestinationSelection(); const createButton = document.createElement('button'); createButton.type = 'button'; createButton.className = 'primary'; createButton.textContent = 'Crear escenario'; createButton.disabled = !review.planned; createButton.onclick = createPlannerScenario; actions.append(revise, createButton); create.append(heading, label, confirmation, actions); content.append(create);
      }
    }
  }
  function renderTabs() {
    const tabs = $('tabs'); tabs.replaceChildren(...maps().map(map => { const button = document.createElement('button'); const name = map.name || map.id; button.textContent = name; button.dataset.short = name.trim().slice(0, 2).toUpperCase(); button.title = name; button.className = map.id === ui.mapId ? 'active' : ''; button.onclick = () => focusSeat(map.id, null); return button; }));
    renderMapSelector();
  }
  function clusterCardAnchor(presentation, layout) { return { x: layout.anchorX ?? presentation.x, y: layout.anchorY ?? presentation.y }; }
  function clusterCardDimensions(layout, shape, name = '') { const defaults = { compact: [132, 54], square: [112, 86], vertical: [96, 104] }; const [width, height] = defaults[shape] || defaults.compact; const visibleCharacters = Math.min(Array.from(name).length, MIN_CARACTERES_NOMBRE_CLUSTER); return { width: layout.width || width, height: layout.height || height, minimumNameWidth: `${visibleCharacters}ch` }; }
  function areaMemberRows(area) { return area.workspaceIds.map(workspaceId => { const seat = workspaceByIdentity(area.mapId, workspaceId); const workspace = seat ? workspacePresentation(seat) : null; return { workspaceId, displayLocation: workspace?.displayLocation || workspaceId, currentPersonId: workspace?.currentPersonId || null, currentPerson: workspace?.currentPerson || null }; }); }
  function persistClusterCardAnchor(areaId, patch) { const before = clusterCardEditHelpers.clone(appState.clusterCardShapes[areaId]); const layout = clusterCardLayout(areaId); const stored = appState.clusterCardShapes[areaId]; const base = stored && typeof stored === 'object' ? stored : {}; appState.clusterCardShapes = { ...appState.clusterCardShapes, [areaId]: { ...base, shape: layout.shape, cardSizingMode: layout.cardSizingMode, showMembers: layout.showMembers, cardAnchorX: patch.anchorX, cardAnchorY: patch.anchorY } }; ui.cardSizeUndo = { areaId, before }; saveClusterCardShapes(); }
    function clusterCardMemberMarkup(model, editing) { if (model.level === 'compact') return ''; if (model.level === 'summary') return `<p class="cluster-member-summary">${model.visibleMembers.map(member => escapeHtml(member.currentPerson)).join(' · ')}${model.overflowLabel ? `${model.visibleMembers.length ? ' · ' : ''}${escapeHtml(model.overflowLabel)} usuarios` : ''}</p>`; const rows = model.visibleMembers.map(member => editing ? `<span class="cluster-member-row"><strong>${escapeHtml(member.currentPerson)}</strong><small>${escapeHtml(member.displayLocation)}</small></span>` : `<button type="button" class="cluster-member-row cluster-member-link" data-workspace-id="${escapeHtml(member.workspaceId)}"><strong>${escapeHtml(member.currentPerson)}</strong><small>${escapeHtml(member.displayLocation)}</small></button>`).join(''); return `<section class="cluster-member-list${model.showLocations ? '' : ' member-narrow'}"><p>PERSONAS</p>${rows}${model.overflowLabel ? `<small class="cluster-member-overflow">${escapeHtml(model.overflowLabel)}</small>` : ''}</section>`; }
  function clusterCardMemberModel(area, layout, dimensions) { return clusterCardContentHelpers.buildClusterCardMemberContent({ level: clusterCardContentHelpers.getClusterCardDetailLevel(dimensions.width, dimensions.height, layout.showMembers), width: dimensions.width, height: dimensions.height, members: areaMemberRows(area) }); }
  function updateClusterCardAdaptiveContent(card, area, content, editing) { const layout = clusterCardLayout(area.id); const dimensions = { width: card.clientWidth || clusterCardDimensions(layout, content.shape, content.name).width, height: card.clientHeight || clusterCardDimensions(layout, content.shape, content.name).height }; const members = clusterCardMemberModel(area, layout, dimensions); const host = card.querySelector('.cluster-card-adaptive-content'); if (host) host.innerHTML = clusterCardMemberMarkup(members, editing); card.classList.toggle('cluster-card-wide', dimensions.width >= 420); card.classList.remove('cluster-detail-compact', 'cluster-detail-summary', 'cluster-detail-members'); card.classList.add(`cluster-detail-${members.level}`); return members; }
  function renderManagedAreaCard(host, map, area) {
    const focusedAreaId = appState.activeAreaFocus?.mapId === map.id ? appState.activeAreaFocus.areaId : null;
    if (area.id === focusedAreaId) return;
      const presentation = areaPresentation(area); if (!presentation.counts.total) return;
      const editing = ui.cardEdit?.active && ui.cardEdit.areaId === area.id;
      const layout = clusterCardLayout(area.id); const content = clusterCardShapeHelpers.buildClusterCardShapePresentation({ name: area.name, counts: presentation.counts, shape: layout.shape }); const dimensions = clusterCardDimensions(layout, content.shape, content.name); const members = clusterCardMemberModel(area, layout, dimensions); const anchor = clusterCardAnchor(presentation, layout); const card = document.createElement('div'); card.className = `managed-area-card cluster cluster-${content.shape}${focusedAreaId ? ' dim' : ''}${editing ? ' card-editing' : ''}${layout.cardSizingMode === 'manual' ? ' card-manual' : ''}${dimensions.width >= 420 ? ' cluster-card-wide' : ''} cluster-detail-${members.level}`; card.tabIndex = 0; card.setAttribute('role', 'button'); card.dataset.areaId = area.id; card.dataset.managedAreaId = area.id; card.dataset.mapId = area.mapId; card.style.left = `${anchor.x * 100}%`; card.style.top = `${anchor.y * 100}%`; card.setAttribute('aria-label', content.tooltip); card.setAttribute('aria-describedby', 'cluster-context-description'); card.title = content.tooltip; card.style.setProperty('--cluster-card-name-min-width', dimensions.minimumNameWidth); if (layout.width) card.style.width = `${layout.width}px`; if (layout.height) card.style.height = `${layout.height}px`; card.innerHTML = `<header class="cluster-card-header"><div class="cluster-move-handle active" role="button" aria-label="Arrastrar tarjeta de cluster" title="Arrastrar tarjeta"><span class="cluster-card-drag-mark" aria-hidden="true">⠿</span><strong class="cluster-card-title">${escapeHtml(content.name)}</strong><span class="cluster-count">${content.badge}</span></div><span class="cluster-context-affordance" aria-hidden="true">⋯</span></header><small class="cluster-counts">${escapeHtml(content.detail)}</small><div class="cluster-card-adaptive-content">${clusterCardMemberMarkup(members, editing)}</div>`;
      if (editing) {
        const handle = document.createElement('button'); handle.type = 'button'; handle.className = 'cluster-resize-handle'; handle.setAttribute('aria-label', 'Redimensionar tarjeta'); handle.title = 'Arrastra para cambiar ancho y alto';
        const controls = document.createElement('div'); controls.className = 'cluster-card-edit-controls'; controls.innerHTML = `<span class="cluster-card-edit-status">${layout.cardSizingMode === 'manual' ? 'Personalizada' : 'Automática'}</span><label class="cluster-card-members-toggle"><input type="checkbox" data-card-edit-members ${layout.showMembers ? 'checked' : ''}>Mostrar usuarios</label><button type="button" data-card-edit-reset-size>Restablecer tamaño</button><button type="button" data-card-edit-reset-position>Restablecer posición</button><button type="button" data-card-edit-save>Guardar</button><button type="button" data-card-edit-cancel>Cancelar</button>`;
        controls.querySelector('[data-card-edit-save]').onclick = event => { event.preventDefault(); event.stopPropagation(); commitClusterCardEdit(); };
        controls.querySelector('[data-card-edit-cancel]').onclick = event => { event.preventDefault(); event.stopPropagation(); cancelClusterCardEdit(); };
        controls.querySelector('[data-card-edit-reset-size]').onclick = event => { event.preventDefault(); event.stopPropagation(); resetClusterCardEditSize(); };
        controls.querySelector('[data-card-edit-reset-position]').onclick = event => { event.preventDefault(); event.stopPropagation(); resetClusterCardEditPosition(); };
        controls.querySelector('[data-card-edit-members]').onchange = event => { event.stopPropagation(); updateClusterCardEditDraft({ showMembers: event.target.checked }); refreshManagedAreaCard(area.id); };
        controls.onpointerdown = event => event.stopPropagation();

        handle.onpointerdown = event => {
          if (event.button !== 0) return;
          event.preventDefault(); event.stopPropagation();
          const start = { x: event.clientX, y: event.clientY, width: card.offsetWidth, height: card.offsetHeight };
          handle.setPointerCapture(event.pointerId);
          const move = pointer => { pointer.preventDefault(); pointer.stopPropagation(); updateClusterCardEditDraft({ width: start.width + pointer.clientX - start.x, height: start.height + pointer.clientY - start.y }); card.style.width = `${ui.cardEdit.draftWidth}px`; card.style.height = `${ui.cardEdit.draftHeight}px`; updateClusterCardAdaptiveContent(card, area, content, true); };
          const finish = pointer => { pointer.preventDefault(); pointer.stopPropagation(); if (handle.hasPointerCapture(pointer.pointerId)) handle.releasePointerCapture(pointer.pointerId); handle.removeEventListener('pointermove', move); handle.removeEventListener('pointerup', finish); handle.removeEventListener('pointercancel', finish); };
          handle.addEventListener('pointermove', move); handle.addEventListener('pointerup', finish); handle.addEventListener('pointercancel', finish);
        };
        const moveHandle = card.querySelector('.cluster-move-handle');
        card.append(handle, controls);
      }
      const moveHandle = card.querySelector('.cluster-move-handle');
      clusterCardDragHelpers.attachClusterCardMoveHandle({
        card, handle: moveHandle, plan: $('plan'),
        getAnchor: () => editing ? { x: ui.cardEdit.draftAnchorX ?? anchor.x, y: ui.cardEdit.draftAnchorY ?? anchor.y } : { x: clusterCardLayout(area.id).anchorX ?? anchor.x, y: clusterCardLayout(area.id).anchorY ?? anchor.y },
        setDraftAnchor: patch => { if (editing) updateClusterCardEditDraft(patch); else persistClusterCardAnchor(area.id, patch); },
        onStateChange: state => { if (state.phase === 'start') ui.cardMove = { areaId: area.id, card, handle: moveHandle, start: state.start, beforeRect: state.beforeRect, moved: false }; else if (state.phase === 'move' && ui.cardMove) { ui.cardMove.afterRect = state.rect; ui.cardMove.moved = true; } else if (state.phase === 'finish') { if (ui.cardMove?.moved) card.dataset.suppressClusterOpen = 'true'; ui.cardMove = null; } }
      });
      moveHandle.onclick = event => { event.preventDefault(); event.stopPropagation(); };

      card.querySelectorAll('.cluster-member-link').forEach(link => link.onclick = event => { event.preventDefault(); event.stopPropagation(); openAreaMemberInspector(area, link.dataset.workspaceId); });
      card.onclick = event => { if (card.dataset.suppressClusterOpen === 'true') { delete card.dataset.suppressClusterOpen; event.preventDefault(); event.stopPropagation(); return; } if (editing || event.target.closest('.cluster-move-handle, .cluster-card-edit-controls, .cluster-resize-handle, .cluster-member-link')) return; openAreaDetail(area.id); };
      card.onkeydown = event => { if (!editing && (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10'))) { event.preventDefault(); event.stopPropagation(); hidePreview(); showClusterContextMenu(area.id, clusterContextAnchor(card), card); return; } if (!editing && (event.key === 'Enter' || event.key === ' ')) { event.preventDefault(); openAreaDetail(area.id); } };
      card.oncontextmenu = event => { event.preventDefault(); event.stopPropagation(); card.focus({ preventScroll: true }); hidePreview(); showClusterContextMenu(area.id, { clientX: event.clientX, clientY: event.clientY }, card); };
      host.append(card);
  }
  function renderManagedAreaCards(host, map) { managedAreas(map.id).forEach(area => renderManagedAreaCard(host, map, area)); }
  function refreshManagedAreaCard(areaId) { const area = managedArea(areaId); const map = currentMap(); const host = $('pins'); if (!area || !map || area.mapId !== map.id || !host) return false; const current = [...host.querySelectorAll('.managed-area-card')].find(card => card.dataset.areaId === area.id); const restoreFocus = document.activeElement === current; current?.remove(); renderManagedAreaCard(host, map, area); if (restoreFocus) host.querySelector(`.managed-area-card[data-area-id="${area.id}"]`)?.focus({ preventScroll: true }); return true; }
  function render() {
    const map = currentMap(); if (!map) return;
    ui.mapId = map.id; const definition = grid(); $('stage').style.setProperty('--grid-columns', definition.columns); $('stage').style.setProperty('--grid-rows', definition.rows); renderGridLabels(); renderPlacementCursor(); requestViewportRender(); const resource = resourceFor(map); const plan = $('plan'); plan.alt = map.name || 'Plano'; plan.onload = () => fitInitialMap(map.id); plan.onerror = () => reportPlanResourceFailure(map, resource, `No se pudo cargar el SVG del plano «${map.name || map.id}». Recurso esperado: ${resource}.`); if (plan.getAttribute('src') !== resource) plan.src = resource; else if (plan.complete) fitInitialMap(map.id);
    renderTabs(); renderMode(); renderHeatmap(); const query = appState.search.query; const pins = $('pins'); pins.replaceChildren();
    const searchIds = query ? seats(map).filter(seat => { const values = seatValues(seat); return [displayLocationFor(seat), seat.id, seat.name, values.personId, values.deviceId, values.roseta].filter(Boolean).join(' ').toLowerCase().includes(query); }).map(seat => seat.id) : [];
    const planner = plannerState(); const plannerDestinationIds = planner.destinationMode ? seats(map).filter(seat => plannerAvailability(seat.id) === 'available').map(seat => seat.id) : planner.destinationIds;
    const changedIds = [...ui.touchedSeats, ...ui.changes.map(item => item.seatId || item.entityId || item.after?.seatId || item.before?.seatId)].filter(Boolean);
    const problemIds = ui.problemHighlightWorkspace ? [ui.problemHighlightWorkspace] : [];
    const activeClusterIds = appState.activeClusterFocus?.mapId === map.id ? appState.activeClusterFocus.memberWorkspaceIds : []; const activeAreaIds = appState.activeAreaFocus?.mapId === map.id ? appState.activeAreaFocus.memberWorkspaceIds : []; const forceIndividualIds = [...appState.selectedWorkspaces, ui.seatId, ...searchIds, ui.searchHitWorkspace, ...problemIds, ...planner.sourceIds, ...plannerDestinationIds, ...changedIds, ...activeClusterIds, ...activeAreaIds].filter(Boolean);
    const functionalContext = { forcedIndividualIds: forceIndividualIds, searchIds, selectedIds: [...appState.selectedWorkspaces], areaFocusIds: activeAreaIds, plannerSourceIds: planner.sourceIds, plannerDestinationIds, problemIds, changedIds, heatmap: appState.layers.heatmap };
    const areaMembers = managedMemberIds(map.id); const individualSeats = seats(map).filter(seat => !areaMembers.has(seat.id) || activeAreaIds.includes(seat.id) || forceIndividualIds.includes(seat.id));
    appState.densityModes = {};
    individualSeats.forEach(seat => {
      const values = seatValues(seat); const displayLocation = displayLocationFor(seat); const text = [displayLocation, seat.id, seat.name, values.personId, values.deviceId, values.roseta].filter(Boolean).join(' ').toLowerCase();
      const severity = getWorkspaceMaxSeverity(seat.id); const plannerMarker = appState.planner.status === 'idle' ? 'none' : plannerAvailability(seat.id); const plannerState = plannerMarker === 'source' ? 'source' : plannerMarker === 'destination' ? 'destination' : plannerMarker === 'unavailable' && appState.planner.destinationMode ? 'blocked' : 'none';
      const workspace = workspacePresentation(seat); const presentation = pinStateHelpers.derivePinPresentation({
        businessState: workspace.assignmentStatus, qualityState: seatCompleteness(seat), problemSeverity: appState.layers.problems ? severity : 'None', problemCount: workspace.problemSummary.count,
        scenarioState: scenarioStateForSeat(seat.id), isScenario: Boolean(scenarioId()), plannerState, displayLocation: workspace.displayLocation, personName: workspace.currentPerson || '', selected: seat.id === ui.seatId,
        multiSelected: appState.selectedWorkspaces.has(seat.id), searchHit: ui.searchHitWorkspace === seat.id,
        dimmed: !workspaceFilterFeature.matches({ ...seat, _mapId: map.id }) ? !appState.filters.only : mapDensityHelpers.deriveMapFocusPresentation({ workspace: seat, filterVisible: true, searchMatch: Boolean(query && text.includes(query)), selected: appState.selectedWorkspaces.has(seat.id) || activeClusterIds.includes(seat.id), areaFocused: activeAreaIds.includes(seat.id), plannerState, problemHighlighted: ui.problemHighlightWorkspace === seat.id && ui.problemHighlightMapId === map.id, problemMatch: Boolean(getProblemsForWorkspace(seat.id).length), changed: changedIds.includes(seat.id), hasSearch: Boolean(query), hasSelection: appState.selectedWorkspaces.size > 0 || Boolean(activeClusterIds.length), hasAreaFocus: Boolean(activeAreaIds.length), plannerActive: appState.planner.status !== 'idle', problemsFocused: Boolean(ui.problemHighlightWorkspace), changesFocused: Boolean(changedIds.length) }) === 'dimmed'
      });
      const pin = document.createElement('button'); pin.className = `pin${presentation.interaction.dimmed ? ' dim' : ''}${ui.problemHighlightWorkspace === seat.id && ui.problemHighlightMapId === map.id ? ' problem-highlight' : ''}${appState.filters.only && !workspaceFilterFeature.matches({ ...seat, _mapId: map.id }) ? ' hidden' : ''}`;
      pin.dataset.state = presentation.businessState; pin.dataset.quality = presentation.qualityState; pin.dataset.problem = presentation.problemSeverity; pin.dataset.scenario = presentation.scenarioState; pin.dataset.planner = presentation.plannerState; pin.dataset.selected = String(presentation.interaction.selected); pin.dataset.multiSelected = String(presentation.interaction.multiSelected); pin.dataset.searchHit = String(presentation.interaction.searchHit); pin.style.setProperty('--pin-z', String(presentation.zIndex));
      pin.innerHTML = `<span class="seat-label">${escapeHtml(displayLocation)}</span><span class="person-label">${escapeHtml(values.personId ? nameFor(people(), values.personId) : '')}</span><span class="device-label">${escapeHtml(values.deviceId ? nameFor(devices(), values.deviceId) : '')}</span><span class="network-label">${escapeHtml(values.roseta || '')}</span><span class="quality-symbol" aria-hidden="true">!</span><span class="problem-symbol" aria-hidden="true">${presentation.problemSymbol}</span><span class="scenario-symbol" aria-hidden="true">${presentation.scenarioSymbol}</span><span class="planner-symbol" aria-hidden="true">${presentation.plannerSymbol}</span>`; pin.style.left = `${Number(seat.x) * 100}%`; pin.style.top = `${Number(seat.y) * 100}%`; pin.dataset.seatLabel = displayLocation; pin.dataset.personLabel = values.personId ? nameFor(people(), values.personId) : ''; pin.dataset.deviceLabel = values.deviceId ? nameFor(devices(), values.deviceId) : ''; pin.dataset.networkLabel = values.roseta || ''; pin.setAttribute('aria-label', presentation.ariaLabel); pin.title = presentation.title;
      pin.onpointerdown = event => { if (appState.planner.destinationMode) return; dragSeat(event, seat, pin); }; pin.onclick = event => { if (appState.planner.destinationMode) { event.preventDefault(); selectPlannerDestination(seat.id); } else if (!ui.moving) selectSeat(seat.id, event, false, false, activeAreaIds.includes(seat.id)); };
      pin.onmouseenter = event => preview(event, seat, presentation); pin.onmousemove = movePreview; pin.onmouseleave = hidePreview; pin.onfocus = event => preview(event, seat, presentation); pin.onblur = hidePreview; pins.append(pin);
    });
    renderManagedAreaCards(pins, map);
    if (!ui.busyAction) setStatus(zoomStatus());
    updateActionableControls();
    workspaceFilterUiFeature.updateCount();
    if (appState.viewMode === 'list') renderList();
    if (appState.areaDetail) renderAreaDetail();
  }
  function mapCells(mapId) {
    const map = maps().find(item => item.id === mapId); if (!map) return [];
    return mapDensityHelpers.buildGridCells({ mapId, workspaces: seats(map), grid: grid(), metadata: appState.gridCellMetadata, stateFor: seat => seatType(seat), problemsFor: seat => getProblemsForWorkspace(seat.id).length });
  }
  function openCellDetail(mapId, cellId) {
    const cell = mapCells(mapId).find(item => item.cellId === cellId); if (!cell) return;
    appState.areaDetail = null; appState.activeAreaFocus = null; appState.cellDetail = { mapId, cellId }; ui.mapId = mapId; ui.seatId = null; setViewMode('map'); renderCellDetail(cell); render();
  }
  function renderCellDetail(cell = null) { cellDetailFeature.render(cell); }
  function openAreaDetail(areaId) { const area = managedArea(areaId); if (!area) return; appState.cellDetail = null; appState.areaDetail = { areaId: area.id }; appState.activeClusterFocus = null; appState.activeAreaFocus = { type: 'Search', areaId: area.id, mapId: area.mapId, memberWorkspaceIds: [...area.workspaceIds] }; ui.mapId = area.mapId; ui.seatId = null; setViewMode('map'); render(); renderAreaDetail(area); }
  function openAreaRename(areaId) {
    const area = managedArea(areaId); if (!area) return;
    openAreaDetail(area.id);
    const input = $('area-detail-name'); input.value = area.name; $('area-detail-rename-form').classList.remove('hidden'); input.focus(); input.select();
  }
  function renderAreaDetail(value = null) {
    const area = value || managedArea(appState.areaDetail?.areaId); if (!area) { appState.areaDetail = null; appState.activeAreaFocus = null; $('area-detail').classList.add('hidden'); return; }
    const presentation = areaPresentation(area); showDetailMode('area-detail', { kicker: 'CLUSTER', title: area.name, summary: `${presentation.counts.total} puestos · ${maps().find(map => map.id === area.mapId)?.name || area.mapId}` });
    $('area-detail-counts').replaceChildren(...[['Total', presentation.counts.total], ['Ocupados', presentation.counts.occupied], ['Libres', presentation.counts.free], ['Reservados', presentation.counts.reserved], ['Problemas', presentation.counts.problems]].map(([label, count]) => { const item = document.createElement('span'); item.innerHTML = `<strong>${count}</strong>${escapeHtml(label)}`; return item; }));
    $('area-detail-list').replaceChildren(...presentation.memberIds.map(workspaceId => { const seat = workspaceByIdentity(area.mapId, workspaceId); if (!seat) { const missing = document.createElement('article'); missing.className = 'area-detail-member missing'; missing.textContent = `${workspaceId} · puesto no disponible`; return missing; } const workspace = workspacePresentation(seat); const row = document.createElement('article'); row.className = 'area-detail-member'; const inspect = document.createElement('button'); inspect.type = 'button'; inspect.dataset.areaAction = 'inspect'; inspect.dataset.workspaceId = workspaceId; inspect.innerHTML = `<span><strong>${escapeHtml(workspace.displayLocation)} · ${escapeHtml(workspace.currentPerson || 'Sin asignar')}</strong><small>${escapeHtml(workspace.assignmentStatusLabel)} · ${escapeHtml(workspace.equipment || 'Sin equipo')}</small></span><span>Abrir inspector completo</span>`; const remove = document.createElement('button'); remove.type = 'button'; remove.dataset.areaAction = 'remove'; remove.dataset.workspaceId = workspaceId; remove.textContent = 'Quitar del cluster'; row.append(inspect, remove); return row; }));
  }
  function selectedWorkspaceIdsForMap(mapId) { return [...appState.selectedWorkspaces].filter(id => Boolean(workspaceByIdentity(mapId, id))); }
  function createAreaFromSelection() { openCreateClusterDialog(); }
  function openAreaMemberInspector(area, workspaceId) { const seat = workspaceByIdentity(area.mapId, workspaceId); if (!seat) return; ui.mapId = area.mapId; ui.seatId = workspaceId; appState.cellDetail = null; appState.areaDetail = null; setViewMode('map'); selectSeat(workspaceId, null, true, true, true); }
  function updateCellMetadata(customName) {
    const detail = appState.cellDetail; if (!detail) return; appState.gridCellMetadata = gridCellMetadataHelpers.renameCell(appState.gridCellMetadata, detail.mapId, detail.cellId, customName); try { localStorage.setItem('plano.gridCellMetadata', JSON.stringify(gridCellMetadataHelpers.serializeMetadata(appState.gridCellMetadata))); } catch { /* Presentation metadata persistence is local and non-operational. */ } render();
  }
  function focusSeat(mapId, seatId) {
    if (ui.addingContext?.targetManagedAreaId && mapId && mapId !== ui.addingContext.mapId) { notify('warning', `El puesto se está creando en «${ui.addingContext.areaName}». Selecciona primero una posición en su plano.`); renderMapSelector(); return; }
    if (mapId && mapId !== ui.mapId && appState.selectedWorkspaces.size) clearBulkSelection();
    if (mapId && maps().some(map => map.id === mapId)) ui.mapId = mapId;
    ui.seatId = seatId;
    resetViewport();
    if (seatId) ui.targetScale = ui.currentScale = 1.35;
    render();
    if (seatId) { centerSelectedSeat(); selectSeat(seatId); }
  }
  function centerSelectedSeat() {
    const seat = currentSeat();
    if (!seat) return;
    const scale = ui.targetScale;
    const width = $('plan').offsetWidth || 850;
    const height = $('plan').offsetHeight || 550;
    ui.targetX = wrap.clientWidth / 2 - Number(seat.x) * width * scale;
    ui.targetY = wrap.clientHeight / 2 - Number(seat.y) * height * scale;
    ui.currentX = ui.targetX; ui.currentY = ui.targetY; requestViewportRender();
  }
  function setSelectionMode(active) { return selectionControllerFeature.setMode(active); }
  function bulkSelectionChanged() { selectionControllerFeature.markBulkSelectionChanged(); }
  function clearWorkspaceSelection(options = {}) { selectionControllerFeature.clearWorkspaceSelection(options); }
  function clearBulkSelection() { selectionControllerFeature.clearBulkSelection(); }
  function selectedBulkWorkspaces() { return [...appState.selectedWorkspaces].map(workspaceId => { const seat = workspaceByIdentity(null, workspaceId); return seat ? { workspaceId, effectiveState: effectiveWorkspaceState(seat).state } : { workspaceId, effectiveState: 'missing' }; }); }
  function currentBulkEligibility() { return bulkSelectionHelpers.deriveBulkActionEligibility(selectedBulkWorkspaces(), appState.bulk.pendingAction || ''); }
  function updateMultiSelection(id, additive = false) { selectionControllerFeature.updateMultiSelection(id, additive); }
  function renderBulkBar() {
    const count = appState.selectedWorkspaces.size; const workspaceSurface = appState.viewMode === 'map' || appState.viewMode === 'list'; const eligibility = currentBulkEligibility(); const summary = bulkSelectionHelpers.buildBulkActionSummary(eligibility); const committed = appState.bulk.lastCommitted;
    $('bulk-bar').classList.toggle('hidden', !workspaceSurface || count < 2); $('bulk-count').textContent = committed ? `${committed.count} puestos ${committed.completed}` : `${count} puestos seleccionados`; $('bulk-detail').textContent = committed ? 'Operación aplicada. Puedes deshacerla.' : summary.detail; $('bulk-plan').classList.toggle('hidden', count < 2 || Boolean(scenarioId()) || Boolean(committed));
    $('bulk-status').value = appState.bulk.pendingAction || ''; $('bulk-apply').textContent = summary.applyLabel; $('bulk-apply').disabled = !summary.canApply || Boolean(appState.bulk.inFlight) || Boolean(committed); $('bulk-apply').setAttribute('aria-label', summary.ariaLabel); $('bulk-apply').title = summary.ariaLabel; $('bulk-undo').classList.toggle('hidden', !committed); if (committed) { const label = `Deshacer ${committed.label.toLowerCase()} de ${committed.count} puestos`; $('bulk-undo').setAttribute('aria-label', label); $('bulk-undo').title = label; }
    renderSelectionReview(eligibility, committed ? { eligibleCount: committed.count, excludedCount: 0, detail: 'Operación aplicada. Puedes deshacerla.' } : summary);
  }
  function selectionReviewWorkspaceData() { return Object.fromEntries([...appState.selectedWorkspaces].map(workspaceId => { const seat = workspaceByIdentity(null, workspaceId); if (!seat) return [workspaceId, {}]; const values = seatValues(seat); const presentation = workspacePresentation(seat); return [workspaceId, { mapId: seat._mapId, displayLocation: presentation.displayLocation, person: presentation.currentPerson, effectiveStateLabel: presentation.assignmentStatusLabel, device: presentation.equipment, roseta: presentation.networkOutlet, reference: presentation.workstationReference, location: resolvedName(locations(), values.locationId) }]; })); }
  function renderSelectionReview(eligibility = currentBulkEligibility(), bulkSummary = bulkSelectionHelpers.buildBulkActionSummary(eligibility)) {
    if (appState.areaDetail) { renderAreaDetail(); return; }
        if (appState.cellDetail) { renderCellDetail(); return; }
    const ids = [...appState.selectedWorkspaces]; const mode = selectionReviewHelpers.selectionReviewMode(ids); const panel = $('detail-panel'); const review = $('selection-review'); const wasReview = panel.classList.contains('selection-review-mode');
    if (mode !== 'selection') { panel.classList.remove('selection-review-mode'); review.classList.add('hidden'); if (wasReview && mode === 'inspector') { const seat = workspaceByIdentity(null, ids[0]); if (seat) { ui.mapId = seat._mapId; ui.seatId = seat.id; selectSeat(seat.id, null, true); } } else if (wasReview && mode === 'empty') panel.classList.add('hidden'); return; }
    const bulkByWorkspace = Object.fromEntries([...eligibility.eligible, ...eligibility.excluded].map(target => [target.workspaceId, target])); const plannerResult = movementPlannerHelpers.classifyEffectiveSources(ids, plannerWorkspaces(), plannerLocations()); const plannerByWorkspace = Object.fromEntries([...plannerResult.movable.map(workspaceId => [workspaceId, { movable: true }]), ...plannerResult.unavailable.map(issue => [issue.workspaceId, { movable: false, reason: issue.message }])]); const items = selectionReviewHelpers.buildSelectionReviewItems(ids, selectionReviewWorkspaceData(), { bulkByWorkspace, plannerByWorkspace }); const summary = selectionReviewHelpers.deriveSelectionReviewSummary(items, bulkSummary);
    showDetailMode('selection-review', { title: 'Puestos seleccionados', summary: `${summary.count} puestos seleccionados · ${summary.bulk?.detail || ''}` }); $('selection-review-create-cluster').textContent = `Crear cluster con ${summary.count} puestos`; $('selection-review-create-cluster').classList.toggle('hidden', summary.count < 2); $('selection-review-add-cluster').classList.toggle('hidden', summary.count < 1); $('selection-review-list').replaceChildren(...items.map(item => { const row = document.createElement('article'); row.className = 'selection-review-item'; row.dataset.workspaceId = item.workspaceId; const focus = document.createElement('button'); focus.type = 'button'; focus.className = 'selection-review-focus'; focus.dataset.reviewAction = 'focus'; focus.dataset.workspaceId = item.workspaceId; focus.setAttribute('aria-label', `Centrar ${item.displayLocation} en el mapa`); const applicability = item.bulk ? item.bulk.eligible ? '✓ Se aplicará' : item.bulk.reason : ''; const planner = item.planner ? item.planner.movable ? 'Movible' : `No movible: ${item.planner.reason}` : ''; focus.innerHTML = `<strong>${escapeHtml(item.displayLocation)} · ${escapeHtml(item.person)}</strong><small>${escapeHtml(item.effectiveState)} · ${escapeHtml(item.device)}</small><small>${escapeHtml(item.roseta)} · ${escapeHtml(item.reference)} · ${escapeHtml(item.location)}</small><span class="selection-review-status">${escapeHtml([applicability, planner].filter(Boolean).join(' · '))}</span>`; const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'selection-review-remove'; remove.dataset.reviewAction = 'remove'; remove.dataset.workspaceId = item.workspaceId; remove.textContent = '×'; remove.setAttribute('aria-label', `Quitar ${item.displayLocation} de la selección`); row.append(focus, remove); return row; }));
  }
  function deselectSelectedWorkspace(workspaceId) { selectionControllerFeature.deselectSelectedWorkspace(workspaceId); }
  function focusSelectionReviewWorkspace(workspaceId) { const seat = workspaceByIdentity(null, workspaceId); if (!seat) return; ui.mapId = seat._mapId; ui.seatId = seat.id; setViewMode('map'); render(); centerSelectedSeat(); clearTimeout(ui.searchHitTimer); ui.searchHitWorkspace = seat.id; render(); ui.searchHitTimer = setTimeout(() => { ui.searchHitWorkspace = null; render(); }, 1600); }
  function selectSeat(id, event = null, preserveSelection = false, forceInspector = false, preserveAreaFocus = false) { appState.cellDetail = null; appState.areaDetail = null; if (!preserveAreaFocus) appState.activeAreaFocus = null; if (!preserveSelection) updateMultiSelection(id, Boolean(event?.ctrlKey || event?.metaKey)); const seat = currentSeat(); if (!seat) return; const values = seatValues(seat); const assignment = assignmentFor(id); const presentation = workspacePresentation(seat); appState.selectedPerson = presentation.currentPersonId; appState.selectedDevice = values.deviceId || null; $('seat-kicker').textContent = `UBICACIÓN · ${presentation.displayLocation}`; $('title').textContent = presentation.currentPerson || presentation.displayLocation; $('detail').textContent = `Estado: ${presentation.assignmentStatusLabel}`; $('detail-effective-state').textContent = presentation.assignmentStatusLabel; $('detail-state-mode').textContent = presentation.stateModeLabel; $('detail-reference').textContent = presentation.workstationReference || 'Sin referencia'; $('detail-user').textContent = presentation.currentPerson || 'Sin asignar'; $('detail-managed-area').textContent = managedAreas(ui.mapId).find(area => area.workspaceIds.includes(id))?.name || 'Sin zona'; $('seat-name').value = presentation.workstationReference || '';  $('person').value = presentation.currentPersonId || ''; $('device').value = values.deviceId || ''; $('location').value = values.locationId || ''; $('roseta').value = presentation.networkOutlet || ''; $('assignment-status').value = assignment.status === 'reserved' ? 'reserved' : 'automatic'; $('notes').value = assignment.notes || ''; ui.assignmentBaseline = { seatName: $('seat-name').value, personId: values.personId || null, deviceId: values.deviceId || null, locationId: values.locationId || null, roseta: values.roseta || null, notes: $('notes').value || '', status: assignment.status || null, hasAssignment: Boolean(assignment.workstationId) }; const problems = getProblemsForWorkspace(id); const workspaceProblems = $('workspace-problems'); workspaceProblems.classList.toggle('hidden', !problems.length); if (problems.length) $('workspace-problems-summary').textContent = problems.map(problem => `${severitySymbol(problem.severity)} ${severityLabel(problem.severity).toLowerCase()}`).join(' · '); if (plannerState().status === 'idle') { if (forceInspector || appState.selectedWorkspaces.size < 2) showDetailMode('inspector', { kicker: `UBICACIÓN · ${presentation.displayLocation}`, title: presentation.currentPerson || presentation.displayLocation, summary: `Estado: ${presentation.assignmentStatusLabel}` }); else renderSelectionReview(); } render(); }
  function renderList() {
    const body = $('list-table')?.querySelector('tbody');
    if (!body) return;
    const query = appState.search.query;
    const rows = allSeats().filter(seat => workspaceFilterFeature.matches(seat)).filter(seat => {
      if (!query) return true;
      const values = seatValues(seat);
      return [seat.id, seat.name, seat._mapName, values.personId, values.deviceId, values.roseta, values.locationId, JSON.stringify(devices().find(device => device.id === values.deviceId) || {})].filter(Boolean).join(' ').toLowerCase().includes(query);
    });
    body.replaceChildren(...rows.map(seat => {
      const values = seatValues(seat); const presentation = workspacePresentation(seat); const row = document.createElement('tr');
      row.className = `${seat.id === ui.seatId && seat._mapId === ui.mapId ? 'selected' : ''}${appState.selectedWorkspaces.has(seat.id) ? ' multi-selected' : ''}`;
      row.tabIndex = 0;
      const quality = seatCompleteness(seat) === 'complete' ? 'Completo' : 'Incompleto'; row.innerHTML = `<td><strong>${escapeHtml(presentation.displayLocation)}</strong></td><td>${escapeHtml(presentation.workstationReference || '—')}</td><td>${escapeHtml(presentation.currentPerson || 'Sin asignar')}</td><td>${escapeHtml(presentation.equipment || '—')}</td><td>${escapeHtml(presentation.assignmentStatusLabel)}</td><td><span class="quality-indicator quality-${quality === 'Completo' ? 'none' : 'warning'}" aria-label="${quality}" title="${quality}">${quality === 'Completo' ? '✓' : '!'} <span class="visually-hidden">${quality}</span></span></td>`;
      row.onclick = event => { const visible = allSeats().filter(workspaceFilterFeature.matches); const anchorIndex = visible.findIndex(item => item.id === appState.selectionAnchor); const index = visible.findIndex(item => item.id === seat.id); if (event.shiftKey && anchorIndex >= 0) { bulkSelectionChanged(); if (!event.ctrlKey && !event.metaKey) appState.selectedWorkspaces.clear(); visible.slice(Math.min(anchorIndex, index), Math.max(anchorIndex, index) + 1).forEach(item => appState.selectedWorkspaces.add(item.id)); ui.seatId = seat.id; ui.mapId = seat._mapId; selectSeat(seat.id, null, true); renderBulkBar(); } else { ui.mapId = seat._mapId; selectSeat(seat.id, event); appState.selectionAnchor = seat.id; } renderList(); };
      row.onkeydown = event => { if (event.key === 'Enter') { ui.mapId = seat._mapId; selectSeat(seat.id, event); renderList(); } };
      return row;
    }));
  }
  function stateLabel(value) { return value === 'occupied' ? 'Ocupado' : value === 'reserved' ? 'Reservado' : 'Libre'; }
  function setViewMode(mode) {
    appState.viewMode = mode;
    const mapSurface = mode === 'map'; const workspaceSurface = mode === 'map' || mode === 'list';
    $('mapwrap').classList.toggle('hidden', !mapSurface);
    $('pin-legend').classList.toggle('hidden', !mapSurface);
    $('heatmap-legend').classList.toggle('hidden', !mapSurface);
    $('listview').classList.toggle('hidden', mode !== 'list');
    $('problemsview').classList.toggle('hidden', mode !== 'problems');
    $('scenariosview').classList.toggle('hidden', mode !== 'scenarios');
    $('analyticsview').classList.toggle('hidden', mode !== 'analytics');
    $('dashboardview').classList.toggle('hidden', mode !== 'dashboard');
    document.querySelector('.view-toolbar')?.classList.toggle('hidden', !workspaceSurface);
        $('bulk-bar').classList.toggle('hidden', !workspaceSurface || appState.selectedWorkspaces.size < 2);
    $('map-view').classList.toggle('active', mode === 'map');
    $('list-view').classList.toggle('active', mode === 'list');
    document.querySelectorAll('[data-app-view]').forEach(button => button.classList.toggle('active', button.dataset.appView === mode));
    if (mode === 'list') renderList();
    if (mode === 'problems') renderProblems();
    if (mode === 'scenarios') renderScenarioComparison();
    if (mode === 'analytics') renderAnalytics();
        renderHeatmap();
    if (mode === 'dashboard') renderDashboard();
  }
  function showActiveMapInList() {
    if (!ui.mapId) return;
    appState.filters = { ...appState.filters, quick: 'all', zone: ui.mapId, person: '', device: '', roseta: '', only: false };
    $('filter-zone').value = ui.mapId;
    $('filter-person').value = '';
    $('filter-device').value = '';
    $('filter-roseta').value = '';
    $('filter-only').checked = false;
    $('filter-bar').querySelectorAll('button').forEach(button => button.classList.toggle('active', button.dataset.filter === 'all'));
    setViewMode('list');
    workspaceFilterUiFeature.updateCount();
    $('listview').focus({ preventScroll: true });
  }
  function selectedProblem() { return appState.validation.results.find(result => result.id === appState.selectedProblemId) || null; }
  function filteredProblems() { return appState.validation.results.filter(result => validationHelpers.problemMatches(result, appState.problemFilters)).filter(result => !appState.problemFilters.workspaceId || getProblemsForWorkspace(appState.problemFilters.workspaceId).includes(result)); }
  function renderProblemFilters() { const results = appState.validation.results; const filters = appState.problemFilters; const bindOptions = (id, values, selected, label) => { const control = $(id); if (!control) return; control.replaceChildren(new Option(label, ''), ...values.map(value => new Option(value.label, value.value))); control.value = selected; }; bindOptions('problem-filter-rule', [...groupProblemsByRule(results).keys()].sort().map(value => ({ value, label: value })), filters.ruleId, 'Todos'); bindOptions('problem-filter-map', maps().map(map => ({ value: map.id, label: map.name || map.id })), filters.mapId, 'Todos'); bindOptions('problem-filter-entity', [...new Set(results.map(result => result.entityType).filter(Boolean))].sort().map(value => ({ value, label: value })), filters.entityType, 'Todas'); $('problem-filter-severity').value = filters.severity; $('problem-filter-text').value = filters.text; }
  function problemMapLabel(problem) { return maps().find(map => map.id === problem.mapId)?.name || problem.mapId || 'Sin plano'; }
  function workspaceByIdentity(mapId, workspaceId) { return allSeats().find(seat => seat.id === workspaceId && (!mapId || seat._mapId === mapId)) || null; }
  function problemTargets(problem) { const ids = [...new Set([problem.entityType === 'workspace' ? problem.entityId : null, ...(problem.relatedEntities || [])].filter(Boolean))]; return ids.map(id => ({ id, seat: workspaceByIdentity(problem.mapId, id) || workspaceByIdentity(null, id) })).filter(target => target.seat); }
  function selectProblem(id, focus = false) { appState.selectedProblemId = id; const problem = selectedProblem(); const target = problem && problemTargets(problem)[0]; if (target) navigateToWorkspace({ workspaceId: target.id, mapId: target.seat._mapId, highlight: 'problem' }); else renderProblems(); if (focus) $('problem-detail')?.focus(); }
  function renderProblemDetail(problem) { const host = $('problem-detail'); host.replaceChildren(); host.tabIndex = -1; if (!problem) { const empty = document.createElement('p'); empty.className = 'problems-state'; empty.textContent = 'Selecciona un problema para ver su detalle.'; host.append(empty); return; } const header = document.createElement('div'); header.className = `problem-severity severity-${problem.severity.toLowerCase()}`; header.textContent = `${severitySymbol(problem.severity)} ${severityLabel(problem.severity).toUpperCase()}`; const title = document.createElement('h2'); title.textContent = problem.title; const details = [['Regla', problem.ruleId], ['Mensaje', problem.message], ['Campo afectado', problem.field || '—'], ['Entidad', `${problem.entityType} · ${problem.entityId}`], ['Plano', problemMapLabel(problem)], ['Detalles', problem.details], ['Acción sugerida', problem.suggestedAction || 'Revisar manualmente.']]; host.append(header, title, ...details.filter(([, value]) => value).map(([label, value]) => { const section = document.createElement('section'); const heading = document.createElement('h3'); heading.textContent = label; const text = document.createElement('p'); text.textContent = value; section.append(heading, text); return section; })); const targets = problemTargets(problem); if (targets.length) { const section = document.createElement('section'); const heading = document.createElement('h3'); heading.textContent = 'Entidades relacionadas'; const list = document.createElement('div'); list.className = 'problem-targets'; targets.forEach(target => { const button = document.createElement('button'); button.type = 'button'; button.textContent = `Ver ${target.id} · ${target.seat._mapName}`; button.onclick = () => navigateToWorkspace({ workspaceId: target.id, mapId: target.seat._mapId, highlight: 'problem' }); list.append(button); }); section.append(heading, list); host.append(section); } const primary = targets[0]; if (primary) { const button = document.createElement('button'); button.type = 'button'; button.className = 'primary'; button.textContent = `Ver en plano · ${primary.id}`; button.onclick = () => navigateToWorkspace({ workspaceId: primary.id, mapId: primary.seat._mapId, highlight: 'problem' }); host.append(button); } }
  function renderProblems() { const view = $('problemsview'); if (!view) return; const validation = appState.validation; const summary = validation.summary; $('problems-last-run').textContent = validation.lastRunAt ? `Última validación: ${new Date(validation.lastRunAt).toLocaleString()}` : 'Aún no se ha validado.'; $('problems-running').classList.toggle('hidden', validation.status !== 'running'); $('problems-error').classList.toggle('hidden', validation.status !== 'error'); $('problems-error-text').textContent = validation.error || ''; $('problems-layout').classList.toggle('hidden', validation.status === 'error'); $('problems-summary').replaceChildren(...[['Críticos', summary.critical, 'Critical'], ['Advertencias', summary.warning, 'Warning'], ['Información', summary.info, 'Info'], ['Total', summary.total, 'None']].map(([label, count, severity]) => { const item = document.createElement('div'); item.className = `summary-card severity-${severity.toLowerCase()}`; item.innerHTML = `<strong>${escapeHtml(String(count))}</strong><span>${escapeHtml(label)}</span>`; return item; })); renderProblemFilters(); const list = $('problems-list'); const entries = filteredProblems(); $('problems-empty').classList.toggle('hidden', validation.status !== 'ready' || entries.length !== 0); list.replaceChildren(...entries.map((problem, index) => { const button = document.createElement('button'); button.type = 'button'; button.className = `problem-row severity-${problem.severity.toLowerCase()}${problem.id === appState.selectedProblemId ? ' selected' : ''}`; button.setAttribute('role', 'option'); button.setAttribute('aria-selected', String(problem.id === appState.selectedProblemId)); button.dataset.problemIndex = String(index); button.innerHTML = `<span class="problem-row-symbol" aria-hidden="true">${severitySymbol(problem.severity)}</span><span><strong>${escapeHtml(problem.title)}</strong><small>${escapeHtml(problem.entityId)} · ${escapeHtml(problemMapLabel(problem))}</small><small>${escapeHtml(problem.message)}</small></span>`; button.onclick = () => selectProblem(problem.id, true); button.onkeydown = event => { if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); const next = entries[(index + (event.key === 'ArrowDown' ? 1 : -1) + entries.length) % entries.length]; selectProblem(next.id); list.querySelector(`[data-problem-index="${(index + (event.key === 'ArrowDown' ? 1 : -1) + entries.length) % entries.length}"]`)?.focus(); } if (event.key === 'Enter') selectProblem(problem.id, true); }; return button; })); renderProblemDetail(selectedProblem()); }
  function openProblemsForWorkspace(workspaceId) { appState.problemFilters.workspaceId = workspaceId; appState.selectedProblemId = getProblemsForWorkspace(workspaceId)[0]?.id || null; setViewMode('problems'); }
  function scenarioKindLabel(kind) { return kind === 'ADDED' ? 'Añadido' : kind === 'REMOVED' ? 'Eliminado' : kind === 'MOVED' ? 'Movido' : 'Modificado'; }
  function scenarioKindSymbol(kind) { return kind === 'ADDED' ? '+' : kind === 'REMOVED' ? '−' : kind === 'MOVED' ? '→' : '~'; }
  function formatScenarioValue(value) { if (value === null || value === undefined) return '—'; return typeof value === 'string' ? value : JSON.stringify(value); }
  function scenarioFieldLabel(field) { return ({ personId: 'Usuario', seatName: 'Referencia del puesto', name: 'Referencia del puesto', deviceId: 'Equipo', roseta: 'Roseta de red', locationId: 'Zona', status: 'Estado' })[field] || field; }
  function selectedScenarioChange() { return appState.scenarioComparison.changes.find(change => change.id === appState.scenarioComparison.selectedChangeId) || null; }
  function filteredScenarioChanges() { const filters = appState.scenarioComparison.filters; const query = normalizeSearchText(filters.text); return appState.scenarioComparison.changes.filter(change => { if (filters.kind && change.kind !== filters.kind) return false; if (filters.mapId && change.mapId !== filters.mapId) return false; if (!query) return true; return normalizeSearchText([change.kind, change.entityType, change.entityId, change.mapId, change.mapName, ...(change.changedFields || []).flatMap(field => [field.field, formatScenarioValue(field.before), formatScenarioValue(field.after)])].join(' ')).includes(query); }); }
  function selectScenarioChange(id, focus = false) { appState.scenarioComparison.selectedChangeId = id; renderScenarioComparison(); if (focus) $('scenario-change-detail')?.focus(); }
  function renderScenarioChangeDetail(change) { const host = $('scenario-change-detail'); host.replaceChildren(); host.tabIndex = -1; if (!change) { const text = document.createElement('p'); text.className = 'problems-state'; text.textContent = 'Selecciona un cambio para revisar sus valores.'; host.append(text); return; } const badge = document.createElement('span'); badge.className = `scenario-kind kind-${change.kind.toLowerCase()}`; badge.textContent = `${scenarioKindSymbol(change.kind)} ${scenarioKindLabel(change.kind).toUpperCase()}`; const targetSeat = change.mapId ? workspaceByIdentity(change.mapId, change.entityId) : null; const targetPresentation = targetSeat ? workspacePresentation(targetSeat) : null; const title = document.createElement('h2'); title.textContent = targetPresentation ? `${targetPresentation.displayLocation}${targetPresentation.workstationReference ? ` · ${targetPresentation.workstationReference}` : ''}` : `${change.entityType === 'workspace' ? 'Puesto' : 'Asignación'} · ${change.entityId}`; const location = document.createElement('p'); location.textContent = [change.mapName || change.mapId, targetPresentation?.currentPerson && `Usuario ${targetPresentation.currentPerson}`, change.fromCell && `Origen ${change.fromCell}`, change.toCell && `Destino ${change.toCell}`].filter(Boolean).join(' · ') || 'Sin plano asociado'; host.append(badge, title, location); const fields = change.changedFields || []; if (fields.length) { const section = document.createElement('section'); const heading = document.createElement('h3'); heading.textContent = 'Campos modificados'; const list = document.createElement('dl'); list.className = 'scenario-field-list'; fields.forEach(field => { const item = document.createElement('div'); const name = document.createElement('dt'); name.textContent = scenarioFieldLabel(field.field); const value = document.createElement('dd'); value.textContent = `${formatScenarioValue(field.before)} → ${formatScenarioValue(field.after)}`; item.append(name, value); list.append(item); }); section.append(heading, list); host.append(section); } if (change.mapId && allSeats().some(seat => seat.id === change.entityId && seat._mapId === change.mapId)) { const button = document.createElement('button'); button.type = 'button'; button.className = 'primary'; button.textContent = `Ver en plano · ${change.entityId}`; button.onclick = () => navigateToWorkspace({ workspaceId: change.entityId, mapId: change.mapId, highlight: 'scenario' }); host.append(button); } }
  function renderScenarioComparison() { const comparison = appState.scenarioComparison; const hasScenario = Boolean(scenarioId()); $('scenario-empty').classList.toggle('hidden', hasScenario); $('scenario-content').classList.toggle('hidden', !hasScenario); $('scenario-refresh').disabled = !hasScenario; $('scenario-view-context').textContent = hasScenario ? `Comparando «${scenario()?.name || scenarioId()}» con su realidad de partida.` : 'Selecciona o crea un escenario para comparar su borrador con la realidad de partida.'; if (!hasScenario) return; const summary = comparison.impactSummary || { total: 0, added: 0, removed: 0, moved: 0, modified: 0, changedFields: 0 }; $('scenario-impact-summary').replaceChildren(...[['Cambios', summary.total], ['Añadidos', summary.added], ['Eliminados', summary.removed], ['Movidos', summary.moved], ['Modificados', summary.modified], ['Campos', summary.changedFields]].map(([label, value]) => { const card = document.createElement('div'); card.className = 'scenario-impact-card'; card.innerHTML = `<strong>${escapeHtml(String(value || 0))}</strong><span>${escapeHtml(label)}</span>`; return card; })); const validation = comparison.validationImpact || { introduced: [], resolved: [], persistent: [] }; $('scenario-validation-impact').replaceChildren(...[['Problemas introducidos', validation.introduced?.length || 0, 'introduced'], ['Problemas resueltos', validation.resolved?.length || 0, 'resolved'], ['Problemas persistentes', validation.persistent?.length || 0, 'persistent']].map(([label, count, type]) => { const button = document.createElement('button'); button.type = 'button'; button.className = `validation-impact ${type}`; button.innerHTML = `<strong>${escapeHtml(String(count))}</strong><span>${escapeHtml(label)}</span>`; button.disabled = !count || type === 'resolved'; button.onclick = () => { if (type === 'introduced') { appState.selectedProblemId = validation.introduced[0]?.id || null; setViewMode('problems'); } }; return button; })); $('scenario-comparison-running').classList.toggle('hidden', comparison.status !== 'running'); const map = $('scenario-filter-map'); map.replaceChildren(new Option('Todos', ''), ...maps().map(item => new Option(item.name || item.id, item.id))); map.value = comparison.filters.mapId; $('scenario-filter-kind').value = comparison.filters.kind; $('scenario-filter-text').value = comparison.filters.text; const list = $('scenario-changes-list'); const changes = filteredScenarioChanges(); $('scenario-changes-empty').classList.toggle('hidden', comparison.status !== 'ready' || changes.length !== 0); list.replaceChildren(...changes.map((change, index) => { const button = document.createElement('button'); button.type = 'button'; button.className = `scenario-change-row kind-${change.kind.toLowerCase()}${change.id === comparison.selectedChangeId ? ' selected' : ''}`; button.setAttribute('role', 'option'); button.setAttribute('aria-selected', String(change.id === comparison.selectedChangeId)); button.dataset.changeIndex = String(index); button.innerHTML = `<span class="scenario-change-symbol" aria-hidden="true">${scenarioKindSymbol(change.kind)}</span><span><strong>${escapeHtml(scenarioKindLabel(change.kind))} · ${escapeHtml(change.entityId)}</strong><small>${escapeHtml(change.mapName || change.mapId || change.entityType)}</small><small>${escapeHtml((change.changedFields || []).map(field => field.field).join(', ') || change.entityType)}</small></span>`; button.onclick = () => selectScenarioChange(change.id, true); button.onkeydown = event => { if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); const nextIndex = (index + (event.key === 'ArrowDown' ? 1 : -1) + changes.length) % changes.length; selectScenarioChange(changes[nextIndex].id); list.querySelector(`[data-change-index="${nextIndex}"]`)?.focus(); } if (event.key === 'Enter') selectScenarioChange(change.id, true); }; return button; })); renderScenarioChangeDetail(selectedScenarioChange()); }
  function normalizeSearchText(value) { return String(value || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim().replace(/\s+/g, ' '); }
  function scoreSearch(query, primary, secondary = '') { const q = normalizeSearchText(query), p = normalizeSearchText(primary), s = normalizeSearchText(secondary); if (!q) return 0; if (p === q) return 1000; if (p.startsWith(q)) return 800; if (p.split(' ').includes(q)) return 600; if (p.includes(q)) return 400; return s.includes(q) ? 200 : 0; }
  function searchEntries(query) {
    const results = []; if (!query) return results;
    allSeats().forEach(seat => { const values = seatValues(seat); const person = people().find(item => item.id === values.personId); const device = devices().find(item => item.id === values.deviceId); const add = (type, id, primaryText, secondaryText, fields) => { const score = Math.max(...fields.map(([primary, secondary = '']) => scoreSearch(query, primary, secondary))); if (score) results.push({ type, id, primaryText, secondaryText, workspaceId: seat.id, mapId: seat._mapId, score }); };
      const presentation = workspacePresentation(seat);
      add('PUESTOS', seat.id, presentation.displayLocation, `${presentation.workstationReference ? `Ref. ${presentation.workstationReference} · ` : ''}${presentation.assignmentStatusLabel} · ${seat._mapName}`, [[presentation.displayLocation], [presentation.workstationReference, seat.id, seat._mapName]]);
      if (person || values.personId) add('PERSONAS', values.personId || seat.id, presentation.currentPerson || values.personId, `${presentation.displayLocation} · ${seat._mapName}`, [[person?.username || values.personId], [person?.name, `${presentation.displayLocation} ${seat._mapName}`]]);
      if (device || values.deviceId) add('EQUIPOS', values.deviceId || seat.id, device?.hostname || device?.name || values.deviceId, `${presentation.displayLocation} · ${presentation.currentPerson || 'Sin asignar'}`, [[device?.hostname || values.deviceId], [device?.name, device?.model, device?.serial, device?.serialNumber]]);
      if (values.roseta) add('RED', values.roseta, `Roseta ${values.roseta}`, presentation.displayLocation, [[values.roseta], [seat._mapName]]);
    });
    managedAreas().forEach(area => { const score = scoreSearch(query, area.name, maps().find(map => map.id === area.mapId)?.name || area.mapId); if (score) results.push({ type: 'AREAS', id: area.id, primaryText: area.name, secondaryText: `${area.workspaceIds.length} puestos · ${maps().find(map => map.id === area.mapId)?.name || area.mapId}`, mapId: area.mapId, areaId: area.id, score }); });
        maps().flatMap(map => mapCells(map.id)).forEach(cell => { if (!cell.customName) return; const score = Math.max(scoreSearch(query, cell.customName, cell.cellId), scoreSearch(query, cell.cellId, cell.customName)); if (score) results.push({ type: 'CELDAS', id: cell.identity, primaryText: cell.customName, secondaryText: `${cell.cellId} · ${cell.members.length} puestos`, mapId: cell.mapId, cellId: cell.cellId, score }); });
    return results.sort((a, b) => b.score - a.score || a.primaryText.localeCompare(b.primaryText)).slice(0, 24);
  }
  function navigateToWorkspace({ workspaceId, mapId, highlight = null }) { const seat = workspaceByIdentity(mapId, workspaceId); const targetMapId = mapId || seat?._mapId; if (!seat || !targetMapId) return false; setViewMode('map'); focusSeat(targetMapId, workspaceId); if (highlight === 'planner') { render(); return true; } if (highlight === 'problem') { clearTimeout(ui.problemHighlightTimer); ui.problemHighlightWorkspace = workspaceId; ui.problemHighlightMapId = targetMapId; render(); ui.problemHighlightTimer = setTimeout(() => { ui.problemHighlightWorkspace = null; ui.problemHighlightMapId = null; render(); }, 2200); } return true; }
  function activateSearchResult(result) { if (!result) return; $('search').value = ''; appState.search.query = ''; $('search-results').classList.add('hidden'); if (result.type === 'AREAS') { ui.mapId = result.mapId; resetViewport(result.mapId); render(); openAreaDetail(result.areaId); return; } if (result.type === 'CELDAS') { ui.mapId = result.mapId; resetViewport(result.mapId); render(); openCellDetail(result.mapId, result.cellId); return; } if (navigateToWorkspace({ workspaceId: result.workspaceId, mapId: result.mapId, highlight: 'search' })) { clearTimeout(ui.searchHitTimer); ui.searchHitWorkspace = result.workspaceId; render(); ui.searchHitTimer = setTimeout(() => { ui.searchHitWorkspace = null; render(); }, 1600); } }
  function positionSearchResults() { const control = document.querySelector('.global-search-control'); const host = $('search-results'); if (!control || !host) return; const rect = control.getBoundingClientRect(); const width = Math.min(440, Math.max(0, window.innerWidth - 16)); const left = Math.max(8, Math.min(rect.left, window.innerWidth - width - 8)); host.style.setProperty('--search-results-left', `${left}px`); host.style.setProperty('--search-results-top', `${Math.max(8, rect.bottom + 4)}px`); host.style.setProperty('--search-results-width', `${width}px`); }
  function renderSearchResults() { const host = $('search-results'); positionSearchResults(); const entries = appState.search.results = measureSync('search', () => searchEntries(appState.search.query)); appState.search.activeIndex = Math.min(appState.search.activeIndex, Math.max(0, entries.length - 1)); host.replaceChildren(); ['AREAS', 'PERSONAS', 'PUESTOS', 'EQUIPOS', 'RED', 'CELDAS'].forEach(group => { const values = entries.filter(item => item.type === group); if (!values.length) return; const title = document.createElement('p'); title.className = 'search-group'; title.textContent = group === 'AREAS' ? 'ÁREAS' : group; host.append(title); values.forEach(entry => { const index = entries.indexOf(entry); const button = document.createElement('button'); button.type = 'button'; button.id = `search-result-${index}`; button.classList.toggle('active', index === appState.search.activeIndex); button.setAttribute('role', 'option'); button.innerHTML = `<strong>${escapeHtml(entry.primaryText)}</strong><small>${escapeHtml(entry.secondaryText)}</small>`; button.onclick = () => activateSearchResult(entry); host.append(button); }); }); $('search').setAttribute('aria-activedescendant', entries.length ? `search-result-${appState.search.activeIndex}` : ''); host.classList.toggle('hidden', !entries.length); }
  function personId(value) { return clean(value); }
  function deviceId(value) { return clean(value); }

  function dragSeat(event, seat, pin) {
    if (event.button !== 0) return; event.preventDefault(); event.stopPropagation(); ui.moving = false; pin.setPointerCapture(event.pointerId); const start = { x: event.clientX, y: event.clientY };
    const move = e => { if (Math.hypot(e.clientX - start.x, e.clientY - start.y) > 3) ui.moving = true; if (!ui.moving) return; const box = $('plan').getBoundingClientRect(); seat.x = Math.max(0, Math.min(1, (e.clientX - box.left) / box.width)); seat.y = Math.max(0, Math.min(1, (e.clientY - box.top) / box.height)); seat.gridCell = gridCellAt(seat.x, seat.y); ui.dragVisual = { pin, x: seat.x, y: seat.y }; requestViewportRender(); };
    const up = e => { move(e); if (pin.hasPointerCapture(e.pointerId)) pin.releasePointerCapture(e.pointerId); window.removeEventListener('pointermove', move); ui.dragVisual = null; if (ui.moving) moveWorkspace(seat.id, seat.x, seat.y); setTimeout(() => ui.moving = false, 0); };
    window.addEventListener('pointermove', move); window.addEventListener('pointerup', up, { once: true });
  }
  function preview(event, seat, presentation) { const workspace = workspacePresentation(seat); const problem = workspace.problemSummary; $('tooltip').innerHTML = `<strong>${escapeHtml(workspace.displayLocation)}</strong><br>Estado: ${escapeHtml(workspace.assignmentStatusLabel)}<br>Usuario: ${escapeHtml(workspace.currentPerson || 'Sin asignar')}${workspace.workstationReference ? `<br>Referencia: ${escapeHtml(workspace.workstationReference)}` : ''}${problem.severity !== 'none' ? `<br>Problemas: ${escapeHtml(String(problem.count || 1))} ${escapeHtml(problem.severity)}` : ''}${workspace.equipment ? `<br>Equipo: ${escapeHtml(workspace.equipment)}` : ''}${workspace.networkOutlet ? `<br>Roseta: ${escapeHtml(workspace.networkOutlet)}` : ''}`; movePreview(event); $('tooltip').classList.add('show'); }
  function movePreview(event) { const rect = event.currentTarget?.getBoundingClientRect?.(); const x = Number.isFinite(event.clientX) ? event.clientX : (rect?.left || 0) + (rect?.width || 0) / 2; const y = Number.isFinite(event.clientY) ? event.clientY : (rect?.top || 0) + (rect?.height || 0) / 2; $('tooltip').style.left = `${Math.min(x + 14, window.innerWidth - 280)}px`; $('tooltip').style.top = `${Math.min(y + 14, window.innerHeight - 120)}px`; }
  function hidePreview() { $('tooltip').classList.remove('show'); }
  function captureFocusRestorer(opener = document.activeElement) { return () => { if (opener?.isConnected && opener !== document.body && opener !== document.documentElement) opener.focus({ preventScroll: true }); else { const active = document.activeElement; if (active && active !== document.body) active.blur(); } }; }
  function openDialog(id) { const dialog = $(id); if (dialog.open) return false; dialog.addEventListener('close', captureFocusRestorer(), { once: true }); dialog.showModal(); return true; }
  function hideContextMenu({ restoreFocus = true } = {}) { const menu = $('context-menu'); const restore = ui.contextMenuRestoreFocus; menu.classList.remove('show'); menu.removeAttribute('data-map-id'); menu.removeAttribute('data-area-id'); ui.contextMenuRestoreFocus = null; if (restoreFocus) restore?.(); }
  function closeMoreMenu() { $('more-menu').classList.remove('open'); $('more').setAttribute('aria-expanded', 'false'); }
  function selectedWorkspacesForCurrentMap() { return selectedWorkspaceIdsForMap(ui.mapId); }
  function positionContextMenu(menu, clientX, clientY) { menu.style.left = '0px'; menu.style.top = '0px'; menu.classList.add('show'); const margin = 8; const rect = menu.getBoundingClientRect(); menu.style.left = `${Math.max(margin, Math.min(clientX, window.innerWidth - rect.width - margin))}px`; menu.style.top = `${Math.max(margin, Math.min(clientY, window.innerHeight - rect.height - margin))}px`; }
  function hideMapContextActions(menu) { ['context-create-here', 'context-enable-selection', 'context-create-cluster', 'context-add-to-cluster', 'context-remove-from-cluster', 'context-select-more', 'context-open-cluster', 'context-rename-cluster', 'context-edit-cluster', 'context-add-selected-to-cluster', 'context-merge-cluster', 'context-clear-selection', 'context-dissolve-cluster', 'context-menu-separator'].forEach(id => $(id).classList.add('hidden')); }
  function showContextMenu(event) { const selected = selectedWorkspacesForCurrentMap(); const sameMap = selected.length === appState.selectedWorkspaces.size; const count = sameMap ? selected.length : 0; const owner = count ? managedAreas(ui.mapId).find(area => selected.every(id => area.workspaceIds.includes(id))) : null; const menu = $('context-menu'); const point = rectangleSelectionHelpers.clientToNormalized(event, $('plan').getBoundingClientRect()); ui.contextPoint = { x: point.x, y: point.y, mapId: ui.mapId }; hideMapContextActions(menu); $('context-create-here').classList.toggle('hidden', count > 0); $('context-enable-selection').classList.toggle('hidden', count > 0); $('context-create-cluster').classList.toggle('hidden', count < 2); $('context-add-to-cluster').classList.toggle('hidden', count < 1); $('context-remove-from-cluster').classList.toggle('hidden', !owner); $('context-select-more').classList.toggle('hidden', count !== 1); $('context-menu-separator').classList.toggle('hidden', count === 0); $('context-clear-selection').classList.toggle('hidden', count === 0); $('context-create-cluster-label').textContent = `Crear cluster con ${count} puestos`; menu.dataset.mapId = ui.mapId; menu.dataset.areaId = owner?.id || ''; positionContextMenu(menu, event.clientX, event.clientY); menu.querySelector('button:not(.hidden)')?.focus(); return true; }
  function clusterContextAnchor(card) { const rect = card.getBoundingClientRect(); return { clientX: rect.left + rect.width / 2, clientY: rect.bottom }; }
  function showClusterContextMenu(areaId, anchor, opener = null) { const area = managedArea(areaId); if (!area || ui.cardEdit?.areaId === area.id) return false; const selected = selectedWorkspaceIdsForMap(area.mapId); const sameMap = selected.length === appState.selectedWorkspaces.size; const count = sameMap ? selected.length : 0; const addable = count ? selected.filter(id => !area.workspaceIds.includes(id)) : []; const menu = $('context-menu'); hideMapContextActions(menu); $('context-open-cluster').classList.remove('hidden'); $('context-rename-cluster').classList.remove('hidden'); $('context-edit-cluster').classList.remove('hidden'); $('context-merge-cluster').classList.remove('hidden'); $('context-dissolve-cluster').classList.remove('hidden'); $('context-add-selected-to-cluster').classList.toggle('hidden', !addable.length); if (addable.length) $('context-add-selected-to-cluster').textContent = `Añadir ${addable.length} puestos a ${area.name}`; $('context-menu-separator').classList.remove('hidden'); menu.dataset.mapId = area.mapId; menu.dataset.areaId = area.id; ui.contextMenuRestoreFocus = captureFocusRestorer(opener ?? document.activeElement); positionContextMenu(menu, anchor.clientX, anchor.clientY); menu.querySelector('button:not(.hidden)')?.focus(); return true; }
  function beginClusterCardEdit(areaId) { const area = managedArea(areaId); if (!area || ui.cardEdit?.active) return false; setSelectionMode(false); const wasFocused = appState.activeAreaFocus?.areaId === area.id; if (wasFocused) closeDetailPanel({ render: false }); ui.cardEdit = clusterCardEditHelpers.beginCardEdit({ areaId: area.id, record: appState.clusterCardShapes[area.id], normalizeShape: clusterCardShapeHelpers.normalizeClusterCardShape }); if (wasFocused) render(); else refreshManagedAreaCard(area.id); return true; }
  function updateClusterCardEditDraft(patch) { if (!ui.cardEdit?.active) return false; ui.cardEdit = clusterCardEditHelpers.updateCardEditDraft(ui.cardEdit, patch, clusterCardShapeHelpers.normalizeClusterCardShape); return true; }
  function commitClusterCardEdit() { const session = ui.cardEdit; if (!session?.active) return false; const record = clusterCardEditHelpers.commitCardEdit(session, clusterCardShapeHelpers.normalizeClusterCardShape); appState.clusterCardShapes = { ...appState.clusterCardShapes, [session.areaId]: record }; ui.cardSizeUndo = { areaId: session.areaId, before: clusterCardEditHelpers.clone(session.before) }; saveClusterCardShapes(); ui.cardEdit = null; refreshManagedAreaCard(session.areaId); return true; }
  function cancelClusterCardEdit() { const session = ui.cardEdit; if (!session?.active) return false; ui.cardEdit = null; refreshManagedAreaCard(session.areaId); return true; }
  function resetClusterCardEditSize() { const session = ui.cardEdit; if (!session?.active) return false; ui.cardEdit = clusterCardEditHelpers.resetCardEditSize(session, clusterCardShapeHelpers.normalizeClusterCardShape); refreshManagedAreaCard(session.areaId); return true; }
  function resetClusterCardEditPosition() { const session = ui.cardEdit; if (!session?.active) return false; ui.cardEdit = clusterCardEditHelpers.resetCardEditPosition(session); refreshManagedAreaCard(session.areaId); return true; }
  function resetClusterCardEditToAutomatic() { const session = ui.cardEdit; if (!session?.active) return false; const shapes = { ...appState.clusterCardShapes }; delete shapes[session.areaId]; appState.clusterCardShapes = shapes; ui.cardSizeUndo = { areaId: session.areaId, before: clusterCardEditHelpers.clone(session.before) }; saveClusterCardShapes(); ui.cardEdit = null; refreshManagedAreaCard(session.areaId); return true; }
  function handleMapBackgroundClick() { if (ui.adding || ui.moving || plannerState().status !== 'idle') return; if (ui.cardEdit?.active) cancelClusterCardEdit(); clearWorkspaceSelection({ closeAreaFocus: true }); }
  function openAddToClusterDialog() { const workspaceIds = selectedWorkspacesForCurrentMap(); if (!workspaceIds.length || workspaceIds.length !== appState.selectedWorkspaces.size) { notify('warning', 'Selecciona puestos de un único plano para añadirlos a un cluster.'); return; } const candidates = managedAreas(ui.mapId); if (!candidates.length) { notify('warning', 'No hay clusters en este plano.'); return; } $('add-to-cluster-select').replaceChildren(...candidates.map(area => new Option(area.name, area.id))); $('add-to-cluster-summary').textContent = `${workspaceIds.length} puestos seleccionados`; openDialog('add-to-cluster-dialog'); }

  function openCreateClusterDialog() { const workspaceIds = selectedWorkspacesForCurrentMap(); if (workspaceIds.length < 2 || workspaceIds.length !== appState.selectedWorkspaces.size) { notify('warning', 'Selecciona al menos dos puestos del mismo plano para crear un cluster.'); return; } const owners = workspaceIds.map(id => ({ workspaceId: id, area: managedAreas(ui.mapId).find(area => area.workspaceIds.includes(id)) || null })).filter(item => item.area); ui.clusterDraft = { mapId: ui.mapId, workspaceIds, conflicts: owners }; $('create-cluster-name').value = ''; $('create-cluster-summary').textContent = `${workspaceIds.length} puestos seleccionados`; const conflictText = owners.length ? `${owners.length} puesto${owners.length === 1 ? '' : 's'} ya pertenece${owners.length === 1 ? '' : 'n'} a ${[...new Set(owners.map(item => `«${item.area.name}»`))].join(', ')}.` : ''; $('create-cluster-conflicts').textContent = conflictText; $('create-cluster-conflicts').classList.toggle('hidden', !conflictText); $('create-cluster-confirm').classList.toggle('hidden', Boolean(owners.length)); $('create-cluster-available').classList.toggle('hidden', !owners.length); $('create-cluster-move').classList.toggle('hidden', !owners.length); openDialog('create-cluster-dialog'); }
  function submitCreateCluster(policy) { const draft = ui.clusterDraft; const name = $('create-cluster-name').value.trim(); if (!draft || !name) { $('create-cluster-name').reportValidity(); return; } const existing = managedAreas(draft.mapId).find(area => normalizeSearchText(area.name) === normalizeSearchText(name)); if (existing) { notify('warning', 'Ya existe un cluster con ese nombre en este plano.'); return; } const conflictIds = draft.conflicts.map(item => item.workspaceId); const workspaceIds = policy === 'available' ? draft.workspaceIds.filter(id => !conflictIds.includes(id)) : draft.workspaceIds; if (!workspaceIds.length) { notify('warning', 'No hay puestos disponibles para crear el cluster.'); return; } ui.pendingClusterId = null; sendManagedArea('create', { mapId: draft.mapId, name, workspaceIds, ...(policy === 'move' ? { moveWorkspaceIds: conflictIds } : {}) }); $('create-cluster-dialog').close(); ui.clusterDraft = null; }
  function escapeHtml(value) { const node = document.createElement('span'); node.textContent = value; return node.innerHTML; }

  function requestLoad(action = 'loadInitialData', id = scenarioId(), bypassBusy = false) { send(action, id ? { scenarioId: id } : {}, bypassBusy); }
  function load(data) { const retainedSeat = ui.pendingSeatId || ui.seatId; const preserveAreaFocus = Boolean(ui.pendingAreaFocusId); const pendingClusterId = ui.pendingClusterId; ui.state = data || {}; try { appState.managedAreas = managedAreaHelpers.normalizeState({ managedAreas: data?.managedAreas || [] }); } catch (error) { appState.managedAreas = managedAreaHelpers.normalizeState(); notify('error', `No se pudieron cargar las áreas gestionadas: ${error.message}`); } const focusedArea = appState.activeAreaFocus?.areaId ? managedArea(appState.activeAreaFocus.areaId) : null; if (focusedArea) appState.activeAreaFocus = { ...appState.activeAreaFocus, mapId: focusedArea.mapId, memberWorkspaceIds: [...focusedArea.workspaceIds] }; if (appState.areaDetail && !managedArea(appState.areaDetail.areaId)) closeDetailPanel({ render: false }); checkPlanResources(); if (ui.pendingMapId) ui.mapId = ui.pendingMapId; ui.mapId = maps().some(map => map.id === ui.mapId) ? ui.mapId : maps()[0]?.id; ui.pendingSeatId = null; ui.pendingMapId = null; ui.pendingAreaFocusId = null; ui.pendingClusterId = null; const available = allSeats(); const retained = retainedSeat && available.find(seat => seat.id === retainedSeat); appState.selectedWorkspaces = new Set([...appState.selectedWorkspaces].filter(id => available.some(seat => seat.id === id))); ui.seatId = retained?.id || null; if (retained) ui.mapId = retained._mapId; ui.touchedSeats.clear(); populateLists(); render(); renderBulkBar(); if (pendingClusterId && managedArea(pendingClusterId)) openAreaDetail(pendingClusterId); if (!ui.dashboardInitialized && dashboardHelpers && $('dashboardview')) { ui.dashboardInitialized = true; reportPlanResourceDiagnostic({ id: 'frontend' }, 'dashboard', 'Dashboard initialized.'); } if (retained) selectSeat(retained.id, null, true, false, preserveAreaFocus); refreshValidation(); refreshSpatialAnalytics(); if (ui.openScenarioCompareAfterPlanner && scenarioId()) { ui.openScenarioCompareAfterPlanner = false; setViewMode('scenarios'); } if (scenarioId()) { getDiff(false, Boolean(ui.busyAction)); return true; } return false; }
  function getDiff(open = false, bypassBusy = false) { if (!scenarioId()) return; appState.scenarioComparison.status = 'running'; if (appState.viewMode === 'scenarios') renderScenarioComparison(); if (open) openDialog('diff-dialog'); $('diff-title').textContent = `Diff · ${scenario()?.name || scenarioId()}`; $('diff-empty').textContent = 'Cargando cambios…'; $('diff-list').replaceChildren(); send('getScenarioDiff', { scenarioId: scenarioId() }, bypassBusy); }
  function compareEndpointLabel(endpoint, fallback) { const seat = endpoint?.workspaceId ? workspaceByIdentity(endpoint.mapId, endpoint.workspaceId) : null; return seat ? workspacePresentation(seat).displayLocation : fallback; }
  function compareUnitPresentation(unit) {
    if (unit.kind !== 'movement') {
      const change = unit.members[0] || {}; const cells = [change.fromCell && `Origen: ${change.fromCell}`, change.toCell && `Destino: ${change.toCell}`].filter(Boolean).join(' → '); const details = cells || (change.changedFields || []).map(field => `${field.field}: ${formatScenarioValue(field.before)} → ${formatScenarioValue(field.after)}`).join(' · ');
      return { title: `${scenarioKindLabel(change.kind || change.type)} · ${change.mapName || 'Plano'}`, details: `${change.seatId || ''} ${details}`.trim(), ariaLabel: `Seleccionar ${scenarioKindLabel(change.kind || change.type).toLowerCase()}` };
    }
    const source = compareEndpointLabel(unit.source, 'Origen no indicado'); const destination = compareEndpointLabel(unit.destination, 'Destino no indicado'); const person = unit.person ? nameFor(people(), unit.person) : 'Usuario no indicado'; const device = unit.device ? nameFor(devices(), unit.device) : '';
    return { title: `${source} · ${person} → ${destination}`, details: [device && `Equipo: ${device}`, unit.members.length > 1 && `${unit.members.length} cambios técnicos`].filter(Boolean).join(' · '), ariaLabel: `Mover ${person} de ${source} a ${destination}` };
  }
  function updateDiffSelection() { const total = ui.compareUnits.length; const selected = ui.selectedCompareUnitIds.size; const selection = $('diff-selection'); selection.hidden = total === 0; $('diff-selection-summary').textContent = `${total} unidad${total === 1 ? '' : 'es'} seleccionable${total === 1 ? '' : 's'} · ${selected} seleccionada${selected === 1 ? '' : 's'}`; $('apply-dialog').disabled = selected === 0; }
  function renderDiff(comparison) {
    const data = Array.isArray(comparison) ? { changes: comparison } : comparison || {}; ui.changes = (data.changes || []).map(change => ({ ...change })); ui.compareUnits = scenarioCompareHelpers.buildCompareUnits(ui.changes); ui.selectedCompareUnitIds = new Set(ui.compareUnits.filter(unit => unit.members.every(change => change.selected !== false)).map(unit => unit.unitId)); ui.touchedSeats = new Set(ui.changes.map(change => change.seatId || change.after?.seatId || change.before?.seatId).filter(Boolean)); appState.scenarioComparison = { ...appState.scenarioComparison, status: 'ready', changes: ui.changes, impactSummary: data.impactSummary || null, validationImpact: data.validationImpact || null, selectedChangeId: ui.changes.some(change => change.id === appState.scenarioComparison.selectedChangeId) ? appState.scenarioComparison.selectedChangeId : null };
    const container = $('diff-list'); $('diff-empty').textContent = ui.compareUnits.length ? '' : 'Este escenario no tiene cambios pendientes.'; container.replaceChildren(...ui.compareUnits.map(unit => { const presentation = compareUnitPresentation(unit); const row = document.createElement('label'); row.className = 'change-row'; const check = document.createElement('input'); check.type = 'checkbox'; check.checked = ui.selectedCompareUnitIds.has(unit.unitId); check.setAttribute('aria-label', presentation.ariaLabel); check.onchange = () => { if (check.checked) ui.selectedCompareUnitIds.add(unit.unitId); else ui.selectedCompareUnitIds.delete(unit.unitId); updateDiffSelection(); renderMode(); if (appState.viewMode === 'dashboard') renderDashboard(); }; const text = document.createElement('span'); text.innerHTML = `<strong>${escapeHtml(presentation.title)}</strong>${presentation.details ? `<small>${escapeHtml(presentation.details)}</small>` : ''}`; row.append(check, text); return row; })); updateDiffSelection(); if (appState.viewMode === 'scenarios') renderScenarioComparison(); if (appState.viewMode === 'dashboard') renderDashboard(); render(); }
  function applySelected() { const ids = scenarioCompareHelpers.flattenSelectedCompareUnits(ui.compareUnits, ui.selectedCompareUnitIds); const selectedUnits = ui.compareUnits.filter(unit => ui.selectedCompareUnitIds.has(unit.unitId)); if (!scenarioId() || !ids.length) { showMessage('Selecciona al menos una unidad.'); return; } if (window.confirm(`¿Aplicar ${selectedUnits.length} unidad(es) a la realidad confirmada?`)) send('applyScenario', { scenarioId: scenarioId(), changeIds: ids }); }
  function renderActivity(target, values, backup) { const host = $(target); const activityIcon = backup ? 'archive-restore' : 'history'; host.replaceChildren(...(values || []).map(item => { const row = document.createElement('div'); row.className = 'activity-row'; const text = document.createElement('span'); text.className = 'activity-text'; text.innerHTML = `${icon(activityIcon)}<span><strong>${escapeHtml(item.title || item.action || item.name || item.id)}</strong><small>${escapeHtml(item.createdAt || item.timestamp || item.date || '')} ${escapeHtml(item.description || item.message || '')}</small></span>`; row.append(text); if (backup) { const button = document.createElement('button'); button.innerHTML = `${icon('archive-restore')}<span>Restaurar</span>`; button.onclick = () => { if (window.confirm('¿Restaurar esta copia de seguridad?')) send('restoreBackup', { backupId: item.id || item.backupId }); }; row.append(button); } return row; })); if (!host.children.length) host.textContent = 'No hay elementos disponibles.'; }
  function renderIntegrity(data) { const counts = data?.counts || {}; const rows = [['Rosetas duplicadas', counts.duplicateRosetas], ['Marcas históricas de ocupación sin asignación', counts.historicalOccupiedMarksWithoutAssignment], ['Asignaciones sin puesto', counts.assignmentsWithMissingWorkstation], ['Posiciones huérfanas', counts.orphanPositions]]; $('integrity-summary').textContent = 'El informe no corrige datos automáticamente.'; const host = $('integrity-list'); host.replaceChildren(...rows.map(([label, count]) => { const row = document.createElement('div'); row.className = 'activity-row'; row.innerHTML = `${icon('shield-check')}<span>${escapeHtml(label)}: ${count || 0}</span>`; return row; })); }
  function moveWorkspace(seatId, x, y) { return send('saveSeatPosition', payloadForScenario({ mapId: ui.mapId, seatId, x, y })); }
  function saveWorkspace(payload) { return send('saveAssignment', payload); }
  function deleteWorkspace(seatId) { return send('deleteSeat', payloadForScenario({ mapId: ui.mapId, seatId })); }
  function validateAssignmentForm() { const errors = {}; const workstationId = ui.seatId; const roseta = $('roseta').value.trim(); if (!workstationId) errors['seat-name'] = 'Selecciona un puesto antes de guardar.'; if (roseta) { const duplicate = assignments().find(item => item.workstationId !== workstationId && String(item.roseta || '').trim().toLowerCase() === roseta.toLowerCase()); if (duplicate) errors.roseta = `La roseta ya está asignada al puesto ${duplicate.workstationId}.`; } return { valid: Object.keys(errors).length === 0, errors }; }
  function renderValidation(errors) { ['seat-name', 'roseta'].forEach(id => { const field = $(id); const errorId = `${id}-error`; let message = $(errorId); if (!message) { message = document.createElement('small'); message.id = errorId; message.className = 'field-error'; field.insertAdjacentElement('afterend', message); } const text = errors[id] || ''; message.textContent = text; message.hidden = !text; field.setAttribute('aria-invalid', String(Boolean(text))); if (text) field.setAttribute('aria-describedby', errorId); else field.removeAttribute('aria-describedby'); }); }
  function assignmentPayload() { const baseline = ui.assignmentBaseline; if (!baseline) return null; const values = { seatName: clean($('seat-name').value.trim()), personId: personId($('person').value), deviceId: deviceId($('device').value), locationId: clean($('location').value), roseta: clean($('roseta').value), notes: $('notes').value.trim() }; const status = $('assignment-status').value === 'reserved' ? 'reserved' : 'confirmed'; const payload = payloadForScenario({ workstationId: ui.seatId }); Object.entries(values).forEach(([key, value]) => { if (value !== baseline[key]) payload[key] = value; }); const hasAssignmentChange = ['personId', 'deviceId', 'locationId', 'roseta', 'notes'].some(key => Object.hasOwn(payload, key)); if (baseline.hasAssignment && status !== (baseline.status || 'confirmed')) payload.status = status; if (!baseline.hasAssignment && (hasAssignmentChange || status === 'reserved')) payload.status = status; return Object.keys(payload).length > (scenarioId() ? 2 : 1) ? payload : null; }

  $('context-create-here').onclick = () => { const point = ui.contextPoint; hideContextMenu(); if (point) { ui.mapId = point.mapId; send('createSeat', payloadForScenario({ mapId: point.mapId, x: point.x, y: point.y })); } };
  $('context-enable-selection').onclick = () => { hideContextMenu(); if (!ui.selectionMode) $('selection-mode').click(); setStatus('Selecciona al menos 2 puestos y haz click derecho para crear un cluster.'); };
  $('context-create-cluster').onclick = () => { hideContextMenu(); openCreateClusterDialog(); };
  $('context-add-to-cluster').onclick = () => { hideContextMenu(); openAddToClusterDialog(); };
  $('context-remove-from-cluster').onclick = () => { const areaId = $('context-menu').dataset.areaId; const workspaceIds = selectedWorkspacesForCurrentMap(); hideContextMenu(); if (areaId && workspaceIds.length) sendManagedArea('remove', { areaId, workspaceIds }); };
  $('context-select-more').onclick = () => { hideContextMenu(); if (!ui.selectionMode) $('selection-mode').click(); setStatus('Selecciona más puestos y haz click derecho para crear un cluster.'); };
  $('context-clear-selection').onclick = () => { hideContextMenu(); clearWorkspaceSelection(); };
  $('context-open-cluster').onclick = () => { const areaId = $('context-menu').dataset.areaId; hideContextMenu(); if (areaId) openAreaDetail(areaId); };
  $('context-rename-cluster').onclick = () => { const areaId = $('context-menu').dataset.areaId; hideContextMenu({ restoreFocus: false }); if (areaId) openAreaRename(areaId); };
  $('context-edit-cluster').onclick = () => { const areaId = $('context-menu').dataset.areaId; hideContextMenu(); if (areaId) beginClusterCardEdit(areaId); };
  $('context-add-selected-to-cluster').onclick = () => { const areaId = $('context-menu').dataset.areaId; const area = managedArea(areaId); const workspaceIds = area ? selectedWorkspacesForCurrentMap().filter(id => !area.workspaceIds.includes(id)) : []; hideContextMenu(); if (area && workspaceIds.length) sendManagedArea('add', { areaId: area.id, workspaceIds }); };
  $('context-merge-cluster').onclick = () => { const area = managedArea($('context-menu').dataset.areaId); hideContextMenu(); if (!area) return; const source = resolveAreaTarget(area, 'Cluster que se fusionará dentro del actual'); if (source && window.confirm(`¿Fusionar «${source.name}» dentro de «${area.name}»?`)) sendManagedArea('merge', { targetAreaId: area.id, sourceAreaIds: [source.id] }); };
  $('context-dissolve-cluster').onclick = () => { const area = managedArea($('context-menu').dataset.areaId); hideContextMenu(); if (area && window.confirm(`¿Disolver el cluster «${area.name}»?\n\nSe eliminará únicamente la agrupación. Los ${area.workspaceIds.length} puestos y sus usuarios permanecerán exactamente en sus ubicaciones actuales.`)) sendManagedArea('dissolve', { areaId: area.id }); };
  $('add-to-cluster-cancel').onclick = () => $('add-to-cluster-dialog').close();
  $('add-to-cluster-form').onsubmit = event => { event.preventDefault(); const areaId = $('add-to-cluster-select').value; const area = managedArea(areaId); const workspaceIds = selectedWorkspacesForCurrentMap().filter(id => !area?.workspaceIds.includes(id)); if (!area || !workspaceIds.length) { notify('warning', 'Selecciona puestos disponibles para el cluster elegido.'); return; } sendManagedArea('add', { areaId, workspaceIds }); $('add-to-cluster-dialog').close(); };
  $('create-cluster-cancel').onclick = () => { $('create-cluster-dialog').close(); ui.clusterDraft = null; };
  $('create-cluster-form').onsubmit = event => { event.preventDefault(); submitCreateCluster('available'); };
  $('create-cluster-available').onclick = () => submitCreateCluster('available');
  $('create-cluster-move').onclick = () => submitCreateCluster('move');
  $('scenario-mode').onchange = event => { event.target.title = event.target.selectedOptions[0]?.text || ''; const id = event.target.value; ui.changes = []; ui.compareUnits = []; ui.selectedCompareUnitIds.clear(); ui.touchedSeats.clear(); requestLoad('reloadData', id === 'real' ? null : id); };
  $('map-select').onchange = event => focusSeat(event.target.value, null);
  if ($('scenario-guide')) $('scenario-guide').onclick = () => openDialog('scenario-guide-dialog');
  $('new-scenario').onclick = () => { $('scenario-name').value = ''; if (openDialog('scenario-dialog')) $('scenario-name').focus(); };
  $('more-new-scenario').onclick = () => {
    closeMoreMenu();
    $('new-scenario').click();
  };
  $('scenario-view-create').onclick = () => $('new-scenario').click();
  $('scenario-refresh').onclick = () => { getDiff(false); refreshSpatialAnalytics(); };
    $('refresh-analytics').onclick = refreshSpatialAnalytics;
    $('analytics-revalidate').onclick = () => refreshValidation({ explicit: true });
    $('dashboard-analytics').onclick = () => setViewMode('analytics');
    $('dashboard-scenarios').onclick = () => setViewMode('scenarios');
    $('heatmap-mode').onchange = event => { appState.analytics.heatmapMode = spatialAnalyticsHelpers.selectMetricMode(event.target.value); renderHeatmap(); renderScenarioSpatialComparison(); };
  [['scenario-filter-kind', 'kind'], ['scenario-filter-map', 'mapId'], ['scenario-filter-text', 'text']].forEach(([id, key]) => $(id).addEventListener('input', event => { appState.scenarioComparison.filters[key] = event.target.value; appState.scenarioComparison.selectedChangeId = null; renderScenarioComparison(); }));
  $('scenario-clear-filters').onclick = () => { appState.scenarioComparison.filters = { kind: '', mapId: '', text: '' }; appState.scenarioComparison.selectedChangeId = null; renderScenarioComparison(); };
  $('cancel-scenario').onclick = () => $('scenario-dialog').close();
  $('scenario-dialog').querySelector('.dialog-close').onclick = () => $('scenario-dialog').close();
  $('create-scenario-confirm').onclick = () => { const name = $('scenario-name').value.trim(); if (!name) { $('scenario-name').reportValidity(); return; } send('createScenario', { name }); };
  $('delete-scenario').onclick = () => { if (scenarioId() && window.confirm(`¿Eliminar el escenario «${scenario()?.name || scenarioId()}»? Esta acción no modifica la realidad.`)) send('deleteScenario', { scenarioId: scenarioId() }); };
  function showUndoPreview(data) { const preview = $('undo-preview'); preview.replaceChildren(); const heading = document.createElement('strong'); heading.textContent = data?.title || 'Último cambio'; const description = document.createElement('p'); description.textContent = data?.description || 'Se restaurará el estado anterior.'; preview.append(heading, description); const changes = data?.changes || []; if (changes.length) { const list = document.createElement('ul'); changes.forEach(change => { const item = document.createElement('li'); const route = [change.fromCell && `de ${change.fromCell}`, change.toCell && `a ${change.toCell}`].filter(Boolean).join(' '); item.textContent = `${change.type || 'Cambio'} · ${change.mapName || 'Plano'} · ${change.seatId || ''} ${route}`.trim(); list.append(item); }); preview.append(list); } if (data?.createdAt) { const metadata = document.createElement('small'); metadata.textContent = `Realizado por ${data.createdBy || 'usuario desconocido'} · ${data.createdAt}`; preview.append(metadata); } openDialog('undo-dialog'); }
  $('undo').onclick = () => { appState.bulk.undoRequested = Boolean(appState.bulk.lastCommitted); ui.undoPayload = scenarioId() ? { scenarioId: scenarioId() } : {}; $('undo-preview').textContent = 'Cargando el cambio pendiente…'; openDialog('undo-dialog'); send('getUndoPreview', ui.undoPayload); };
  $('confirm-undo').onclick = () => { if (ui.undoPayload) send('undoLastChange', ui.undoPayload); $('undo-dialog').close(); }; 
  $('history').onclick = () => { $('more-menu').classList.remove('open'); openDialog('history-dialog'); $('events-list').textContent = 'Cargando…'; send('getEvents'); };
  $('backups').onclick = () => { $('more-menu').classList.remove('open'); openDialog('backups-dialog'); $('backups-list').textContent = 'Cargando…'; send('getBackups'); };
  $('diff').onclick = () => { $('more-menu').classList.remove('open'); getDiff(true); }; $('diff-select-all').onclick = () => { ui.selectedCompareUnitIds = new Set(ui.compareUnits.map(unit => unit.unitId)); updateDiffSelection(); renderMode(); }; $('diff-deselect-all').onclick = () => { ui.selectedCompareUnitIds.clear(); updateDiffSelection(); renderMode(); }; $('apply').onclick = applySelected; $('apply-dialog').onclick = applySelected;
  $('export-excel').onclick = () => { $('more-menu').classList.remove('open'); send('exportExcel'); };
  $('integrity').onclick = () => { $('more-menu').classList.remove('open'); openDialog('integrity-dialog'); $('integrity-summary').textContent = 'Cargando…'; $('integrity-list').replaceChildren(); send('getIntegrityReport'); };
  $('more').onclick = () => { const menu = $('more-menu'); const open = menu.classList.toggle('open'); $('more').setAttribute('aria-expanded', String(open)); };
  $('reload').onclick = () => requestLoad('reloadData');
  $('search').oninput = event => { appState.search.query = event.target.value.trim().toLowerCase(); renderSearchResults(); render(); if (appState.viewMode === 'list') renderList(); };
  $('search').onkeydown = event => { const results = appState.search.results; if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); if (results.length) { appState.search.activeIndex = (appState.search.activeIndex + (event.key === 'ArrowDown' ? 1 : -1) + results.length) % results.length; renderSearchResults(); $('search-results').querySelector('.active')?.scrollIntoView({ block: 'nearest' }); } } if (event.key === 'Enter') { event.preventDefault(); activateSearchResult(results[appState.search.activeIndex]); } if (event.key === 'Escape') $('search-results').classList.add('hidden'); };
  ['seat-name', 'person', 'device', 'location', 'roseta', 'notes', 'assignment-status'].forEach(id => $(id).addEventListener('input', () => { setPersistence('dirty'); renderValidation(validateAssignmentForm().errors); }));
  $('save').onclick = () => { const validation = validateAssignmentForm(); renderValidation(validation.errors); if (!validation.valid) { const first = Object.keys(validation.errors)[0]; $(first)?.focus(); notify('warning', 'Corrige los errores del formulario antes de guardar.'); return; } if (!ui.seatId) return; const payload = assignmentPayload(); if (!payload) { showMessage('No hay cambios que guardar.'); return; } saveWorkspace(payload); };
  $('delete-assignment').onclick = () => { if (ui.seatId) send('deleteAssignment', payloadForScenario({ workstationId: ui.seatId })); };
  $('delete-seat').onclick = () => { if (ui.seatId && window.confirm('¿Eliminar este puesto?')) deleteWorkspace(ui.seatId); };
  $('move-seat').onclick = beginMoveMode;
  $('panel-history').onclick = () => $('history').click();
  $('edit-seat').onclick = () => { if (ui.seatId) $('seat-name').focus(); };

    $('bulk-plan').onclick = startPlanner;
    $('planner-close').onclick = resetPlanner;
  $('bulk-status').onchange = event => { appState.bulk.pendingAction = event.target.value; appState.bulk.lastCommitted = null; renderBulkBar(); };
  $('bulk-clear').onclick = () => clearWorkspaceSelection();
  $('bulk-apply').onclick = () => { const eligibility = currentBulkEligibility(); const summary = bulkSelectionHelpers.buildBulkActionSummary(eligibility); const command = bulkSelectionHelpers.buildBulkSelectionCommand(eligibility); if (!command || appState.bulk.inFlight) return; $('bulk-summary').textContent = `${summary.selectedCount} seleccionados. Se aplicará «${eligibility.action.label}» a ${summary.eligibleCount}. ${summary.excludedCount ? `${summary.excludedCount} no se modificarán.` : ''}`; $('bulk-exclusions').replaceChildren(...eligibility.reasons.map(reason => { const item = document.createElement('li'); item.textContent = `${reason.count}: ${reason.reason}`; return item; })); $('bulk-confirm').textContent = summary.applyLabel; $('bulk-confirm').setAttribute('aria-label', summary.ariaLabel); openDialog('bulk-dialog'); };
  $('bulk-confirm').onclick = () => { if (appState.bulk.inFlight) return; const eligibility = currentBulkEligibility(); const command = bulkSelectionHelpers.buildBulkSelectionCommand(eligibility); if (!command) { $('bulk-dialog').close(); renderBulkBar(); return; } const sent = send('bulkUpdateAssignments', payloadForScenario(command)); if (!sent) return; appState.bulk.inFlight = { action: eligibility.action.id, label: eligibility.action.label, completed: eligibility.action.completed, count: command.workstationIds.length, workstationIds: [...command.workstationIds] }; $('bulk-dialog').close(); renderBulkBar(); };
  $('bulk-undo').onclick = () => $('undo').click();
  $('selection-review-create-cluster').onclick = openCreateClusterDialog;
  $('selection-review-add-cluster').onclick = openAddToClusterDialog;
  $('selection-review-clear').onclick = () => clearWorkspaceSelection();
  $('selection-review-list').onclick = event => { const action = event.target.closest('[data-review-action]'); if (!action) return; const workspaceId = action.dataset.workspaceId; if (action.dataset.reviewAction === 'remove') deselectSelectedWorkspace(workspaceId); else if (action.dataset.reviewAction === 'focus') focusSelectionReviewWorkspace(workspaceId); };
  document.querySelectorAll('[data-app-view]').forEach(button => button.onclick = () => setViewMode(button.dataset.appView));
  document.querySelectorAll('[data-dialog]').forEach(button => button.onclick = () => openDialog(button.dataset.dialog));
  $('map-view').onclick = () => setViewMode('map'); $('list-view').onclick = () => setViewMode('list');
  $('map-to-list').onclick = event => { event.preventDefault(); showActiveMapInList(); };
  workspaceFilterUiFeature.bindControls();
  $('selection-mode').onclick = () => { const enabled = setSelectionMode(!ui.selectionMode); setStatus(enabled ? 'Modo selección rectangular activo.' : 'Modo selección rectangular desactivado. Los puestos ya seleccionados se conservan.'); };
  [['layer-seats', 'seats'], ['layer-grid', 'grid'], ['layer-labels', 'labels'], ['layer-people', 'people'], ['layer-devices', 'devices'], ['layer-rosetas', 'network'], ['layer-problems', 'problems'], ['layer-heatmap', 'heatmap']].forEach(([id, layer]) => $(id).onchange = event => { appState.layers[layer] = event.target.checked; updateLayerPresentation(); render(); });
  function closeDetailPanel(options = {}) { detailPanelControllerFeature.close(options); }
  function closePlannerPanel() { if (plannerState().status !== 'idle') resetPlanner(); }
  $('close-panel').onclick = () => closeDetailPanel();
  $('revalidate').onclick = () => refreshValidation({ explicit: true });
  $('retry-validation').onclick = () => refreshValidation({ explicit: true });
  $('workspace-view-problems').onclick = () => { if (ui.seatId) openProblemsForWorkspace(ui.seatId); };
  [['problem-filter-severity', 'severity'], ['problem-filter-rule', 'ruleId'], ['problem-filter-map', 'mapId'], ['problem-filter-entity', 'entityType'], ['problem-filter-text', 'text']].forEach(([id, key]) => $(id).addEventListener('input', event => { appState.problemFilters[key] = event.target.value; appState.selectedProblemId = null; renderProblems(); }));
  $('clear-problem-filters').onclick = () => { appState.problemFilters = { severity: '', ruleId: '', mapId: '', entityType: '', text: '', workspaceId: '' }; appState.selectedProblemId = null; renderProblems(); };
  function setAddMode(active, context = null) { if (active) { setSelectionMode(false); ui.movingSeat = false; clearPlacementCursor('move'); } ui.adding = active; ui.addingContext = active ? context : null; hideContextMenu(); const button = $('add-seat'); button.classList.toggle('is-active', active); $('add-seat-label').textContent = active ? 'Elegir posición' : 'Añadir puesto'; if (active) { const cursor = gridCursorHelpers.initialAddCursor(grid()); ui.placementCursor = { kind: 'add', x: cursor.x, y: cursor.y }; renderPlacementCursor(); announcePlacementCursor(ui.placementCursor); setStatus(context?.targetManagedAreaId ? `Crear puesto en «${context.areaName}»: haz clic en el plano o usa las flechas para elegir la posición.` : 'Modo añadir puesto: haz clic en el plano o usa las flechas para elegir la posición.'); wrap.focus({ preventScroll: true }); } else { clearPlacementCursor('add'); if (!ui.busyAction) setStatus(zoomStatus()); } }
  function openCreateWorkspaceFlow({ mapId, targetManagedAreaId = null } = {}) { const map = maps().find(item => item.id === mapId); if (!map) { notify('warning', 'El plano de destino ya no existe.'); return; } let context = null; if (targetManagedAreaId) { if (scenarioId()) { notify('warning', 'Crear un puesto dentro de una zona gestionada requiere estar en Realidad, no en un escenario.'); return; } const area = managedArea(targetManagedAreaId); if (!area || area.mapId !== mapId) { notify('warning', 'La zona gestionada ya no está disponible en este plano.'); return; } context = { mapId, targetManagedAreaId: area.id, areaName: area.name }; } ui.mapId = mapId; setViewMode('map'); if (context) closeDetailPanel({ render: false, preserveAreaFocus: true }); render(); setAddMode(true, context); }
  $('add-seat').onclick = () => ui.adding ? setAddMode(false) : openCreateWorkspaceFlow({ mapId: ui.mapId });
  function applyUserPreferences(preferences) { uiThemeFeature.apply(preferences?.theme); ui.singleKeyShortcutsEnabled = preferences?.singleKeyShortcutsEnabled !== false; $('single-key-shortcuts-enabled').checked = ui.singleKeyShortcutsEnabled; }
  function saveUserPreferences() { send('saveUserPreferences', { theme: $('theme').value, singleKeyShortcutsEnabled: ui.singleKeyShortcutsEnabled }, true); }
  $('theme').onchange = event => { uiThemeFeature.apply(event.target.value); saveUserPreferences(); };
  $('single-key-shortcuts-enabled').onchange = event => { ui.singleKeyShortcutsEnabled = event.target.checked; saveUserPreferences(); };
    document.querySelectorAll('[data-map-appearance]').forEach(button => button.onclick = () => { const viewport = mapViewportHelpers.snapshotViewport(ui); const mode = mapAppearanceFeature.apply(button.dataset.mapAppearance); mapAppearanceFeature.savePreference(mode); render(); if (!mapViewportHelpers.sameViewport(viewport, ui)) applyViewport(viewport); });
    $('cell-detail-rename').onclick = () => { $('cell-detail-rename-form').classList.remove('hidden'); $('cell-detail-name').focus(); };
    $('cell-detail-cancel-rename').onclick = () => $('cell-detail-rename-form').classList.add('hidden');
    $('cell-detail-clear-name').onclick = () => updateCellMetadata('');
        $('cell-detail-select-all').onclick = () => { const detail = appState.cellDetail && mapCells(appState.cellDetail.mapId).find(cell => cell.cellId === appState.cellDetail.cellId); if (!detail) return; const allSelected = detail.members.every(seat => appState.selectedWorkspaces.has(seat.id)); bulkSelectionChanged(); detail.members.forEach(seat => { if (allSelected) appState.selectedWorkspaces.delete(seat.id); else appState.selectedWorkspaces.add(seat.id); }); ui.seatId = [...appState.selectedWorkspaces].at(-1) || null; renderBulkBar(); render(); };
        $('cell-detail-edit-appearance').onclick = () => $('cell-detail-appearance-form').classList.toggle('hidden');
            document.querySelectorAll('[data-cell-move]').forEach(button => button.onclick = () => { const current = cellAppearanceFeature.appearanceForCell(appState.cellDetail?.mapId, appState.cellDetail?.cellId); const step = .015; const direction = button.dataset.cellMove; cellAppearanceFeature.update({ offsetX: (Number(current.offsetX) || 0) + (direction === 'left' ? -step : direction === 'right' ? step : 0), offsetY: (Number(current.offsetY) || 0) + (direction === 'up' ? -step : direction === 'down' ? step : 0) }); });
        $('cell-detail-reset-appearance').onclick = () => cellAppearanceFeature.update({ offsetX: 0, offsetY: 0 });
    $('cell-detail-rename-form').onsubmit = event => { event.preventDefault(); updateCellMetadata($('cell-detail-name').value); $('cell-detail-rename-form').classList.add('hidden'); };
    $('cell-detail-list').onclick = event => { const action = event.target.closest('[data-cell-action]'); if (!action) return; const seat = workspaceByIdentity(null, action.dataset.workspaceId); if (!seat) return; if (action.dataset.cellAction === 'select') { updateMultiSelection(seat.id, true); renderBulkBar(); renderCellDetail(); render(); } else if (plannerState().destinationMode) selectPlannerDestination(seat.id); else focusSeat(seat._mapId, seat.id); };
        function resolveAreaTarget(area, message) { const candidates = managedAreas(area.mapId).filter(item => item.id !== area.id); if (!candidates.length) { notify('warning', 'No hay otra área disponible en este plano.'); return null; } const answer = window.prompt(`${message}\n${candidates.map(item => `${item.name} (${item.id})`).join('\n')}`); if (!answer?.trim()) return null; const normalized = normalizeSearchText(answer); const target = candidates.find(item => normalizeSearchText(item.id) === normalized || normalizeSearchText(item.name) === normalized); if (!target) notify('warning', 'No se encontró el área indicada.'); return target || null; }

        $('area-detail-create-seat').onclick = () => { const area = managedArea(appState.areaDetail?.areaId); if (area) openCreateWorkspaceFlow({ mapId: area.mapId, targetManagedAreaId: area.id }); };
        $('area-detail-add-selection').onclick = () => { const area = managedArea(appState.areaDetail?.areaId); if (!area) return; const workspaceIds = selectedWorkspaceIdsForMap(area.mapId).filter(id => !area.workspaceIds.includes(id)); if (!workspaceIds.length) { notify('warning', 'Activa Seleccionar y elige puestos existentes del mismo plano para añadirlos al cluster.'); return; } if (window.confirm(`¿Añadir ${workspaceIds.length} puesto${workspaceIds.length === 1 ? '' : 's'} existente${workspaceIds.length === 1 ? '' : 's'} a «${area.name}»?`)) sendManagedArea('add', { areaId: area.id, workspaceIds }); };
        $('area-detail-rename').onclick = () => openAreaRename(appState.areaDetail?.areaId);
        $('area-detail-cancel-rename').onclick = () => $('area-detail-rename-form').classList.add('hidden');
        $('area-detail-rename-form').onsubmit = event => { event.preventDefault(); const area = managedArea(appState.areaDetail?.areaId); const name = $('area-detail-name').value.trim(); if (!area) return; if (!name) { $('area-detail-name').reportValidity(); return; } if (normalizeSearchText(name) === normalizeSearchText(area.name)) { $('area-detail-rename-form').classList.add('hidden'); return; } if (managedAreas(area.mapId).some(item => item.id !== area.id && normalizeSearchText(item.name) === normalizeSearchText(name))) { notify('warning', 'Ya existe un cluster con ese nombre en este plano.'); return; } $('area-detail-rename-form').classList.add('hidden'); sendManagedArea('rename', { areaId: area.id, name }); };

        $('area-detail-shape').onclick = () => { const area = managedArea(appState.areaDetail?.areaId); if (area) beginClusterCardEdit(area.id); };
        $('area-detail-merge').onclick = () => { const area = managedArea(appState.areaDetail?.areaId); if (!area) return; const source = resolveAreaTarget(area, 'Cluster que se fusionará dentro del actual'); if (source && window.confirm(`¿Fusionar «${source.name}» dentro de «${area.name}»?`)) sendManagedArea('merge', { targetAreaId: area.id, sourceAreaIds: [source.id] }); };
        $('area-detail-dissolve').onclick = () => { const area = managedArea(appState.areaDetail?.areaId); if (area && window.confirm(`¿Disolver el cluster «${area.name}»?\n\nSe eliminará únicamente la agrupación. Los ${area.workspaceIds.length} puestos y sus usuarios permanecerán exactamente en sus ubicaciones actuales.`)) sendManagedArea('dissolve', { areaId: area.id }); };

        $('area-detail-list').onclick = event => { const action = event.target.closest('[data-area-action]'); const area = managedArea(appState.areaDetail?.areaId); if (!action || !area) return; if (action.dataset.areaAction === 'inspect') openAreaMemberInspector(area, action.dataset.workspaceId); else if (action.dataset.areaAction === 'remove') sendManagedArea('remove', { areaId: area.id, workspaceIds: [action.dataset.workspaceId] }); }; 
  document.querySelectorAll('[data-close]').forEach(button => button.onclick = () => $(button.dataset.close).close());
  wrap.addEventListener('dragstart', event => { const card = event.target.closest?.('.managed-area-card.cluster.card-editing'); if (card) event.preventDefault(); }, true);
  wrap.addEventListener('selectstart', event => { const card = event.target.closest?.('.managed-area-card.cluster.card-editing'); if (card) event.preventDefault(); }, true);

  function handlePlanClick(event) {
    const box = $('plan').getBoundingClientRect();
    const x = Math.max(0, Math.min(1, (event.clientX - box.left) / box.width));
    const y = Math.max(0, Math.min(1, (event.clientY - box.top) / box.height));
    if (ui.movingSeat && ui.seatId) {
      const seatId = ui.seatId;
      ui.movingSeat = false;
      clearPlacementCursor('move');
      moveWorkspace(seatId, x, y);
      return;
    }
    if (ui.adding) {
      const context = ui.addingContext;
      if (context?.targetManagedAreaId) {
        const area = managedArea(context.targetManagedAreaId);
        if (!area || area.mapId !== ui.mapId) {
          setAddMode(false);
          notify('warning', 'La zona gestionada ya no existe o ya no pertenece a este plano. No se creó ningún puesto.');
          return;
        }
      }
      send('createSeat', payloadForScenario({ mapId: ui.mapId, x, y, ...(context?.targetManagedAreaId ? { targetManagedAreaId: context.targetManagedAreaId } : {}) }));
      setAddMode(false);
    }
  }
  $('plan').addEventListener('click', handlePlanClick);
  wrap.addEventListener('wheel', event => {
    if (event.target.closest('.map-layers-control')) return;
    event.preventDefault();
    const rect = wrap.getBoundingClientRect();
    const screenX = event.clientX - rect.left; const screenY = event.clientY - rect.top;
    const worldX = (screenX - ui.targetX) / ui.targetScale; const worldY = (screenY - ui.targetY) / ui.targetScale;
    const sensitivity = event.deltaMode === 1 ? .048 : .00125;
    ui.targetScale = clamp(ui.targetScale * Math.exp(-event.deltaY * sensitivity), .1, 10);
    ui.targetX = screenX - worldX * ui.targetScale; ui.targetY = screenY - worldY * ui.targetScale;
    ui.zoomAnchor = { screenX, screenY, worldX, worldY }; requestViewportRender();
  }, { passive: false });
  wrap.addEventListener('pointerdown', event => {
    if (event.target.closest('.map-layers-control')) return;
    if (ui.selectionMode && event.button === 0 && !event.target.closest('.pin, .cluster')) { ui.selectionRect = rectangleSelectionHelpers.clientToNormalized(event, $('plan').getBoundingClientRect()); $('selection-rect').classList.remove('hidden'); return; }
    if (event.button !== 0 || ui.adding || ui.moving || event.target.closest('.pin, .cluster')) return;
    event.preventDefault();
    ui.pan = { x: event.clientX, y: event.clientY, targetX: ui.targetX, targetY: ui.targetY, moved: false };
    ui.zoomAnchor = null; wrap.setPointerCapture(event.pointerId);
  });
  wrap.addEventListener('pointermove', event => {
    if (ui.selectionRect) { const point = rectangleSelectionHelpers.clientToNormalized(event, $('plan').getBoundingClientRect()); const rect = $('selection-rect'); rect.style.left = `${Math.min(point.x, ui.selectionRect.x) * 100}%`; rect.style.top = `${Math.min(point.y, ui.selectionRect.y) * 100}%`; rect.style.width = `${Math.abs(point.x - ui.selectionRect.x) * 100}%`; rect.style.height = `${Math.abs(point.y - ui.selectionRect.y) * 100}%`; return; }
    if (!ui.pan) return;
    if (Math.hypot(event.clientX - ui.pan.x, event.clientY - ui.pan.y) > 4) ui.pan.moved = true;
    ui.targetX = ui.pan.targetX + event.clientX - ui.pan.x; ui.targetY = ui.pan.targetY + event.clientY - ui.pan.y; requestViewportRender();
  });
  wrap.addEventListener('pointerup', event => { if (ui.selectionRect) { bulkSelectionChanged(); const end = rectangleSelectionHelpers.clientToNormalized(event, $('plan').getBoundingClientRect()); rectangleSelectionHelpers.selectByCenter(seats(currentMap()), ui.selectionRect, end, seat => workspaceFilterFeature.matches({ ...seat, _mapId: ui.mapId })).forEach(seat => appState.selectedWorkspaces.add(seat.id)); ui.seatId = [...appState.selectedWorkspaces].at(-1) || null; ui.selectionRect = null; $('selection-rect').classList.add('hidden'); renderBulkBar(); render(); return; } const pan = ui.pan; if (pan && wrap.hasPointerCapture(event.pointerId)) wrap.releasePointerCapture(event.pointerId); ui.pan = null; if (event.button === 0 && pan && !pan.moved) handleMapBackgroundClick(); });
  wrap.addEventListener('contextmenu', event => { if (event.target.closest('.map-layers-control')) return; hidePreview(); event.preventDefault(); showContextMenu(event); });
  document.addEventListener('pointerdown', event => { if (!event.target.closest('#context-menu')) hideContextMenu({ restoreFocus: false }); if (!event.target.closest('#more-menu')) closeMoreMenu(); if (!event.target.closest('.global-search-control, #search-results')) $('search-results').classList.add('hidden'); });
  $('context-menu').addEventListener('keydown', event => { const items = [...$('context-menu').querySelectorAll('button:not(.hidden)')]; const index = items.indexOf(document.activeElement); if ((event.key === 'ArrowDown' || event.key === 'ArrowUp') && items.length) { event.preventDefault(); items[(index + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length].focus(); } else if (event.key === 'Enter' && document.activeElement?.click) { event.preventDefault(); document.activeElement.click(); } });
  window.addEventListener('resize', () => { if (!$('search-results').classList.contains('hidden')) positionSearchResults(); });
  function adjacentSeat(direction) { const origin = currentSeat() || seats(currentMap())[0]; if (!origin) return null; const vector = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[direction]; if (!vector) return null; return seats(currentMap()).filter(seat => seat.id !== origin.id).map(seat => { const dx = seat.x - origin.x, dy = seat.y - origin.y; const forward = dx * vector[0] + dy * vector[1]; const distance = Math.hypot(dx, dy); const sideways = Math.abs(dx * vector[1] - dy * vector[0]); return { seat, score: forward > 0 ? sideways * 2 + distance : Infinity }; }).filter(candidate => Number.isFinite(candidate.score)).sort((a, b) => a.score - b.score)[0]?.seat || null; }
  function isEditableKeyboardEvent(event) { for (const target of event.composedPath?.() || [event.target]) { if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement || target?.isContentEditable || target?.getAttribute?.('role') === 'textbox') return true; if (target === document.body || target === document.documentElement || target?.tabIndex >= 0) break; } return false; }
  function handleEscape(event, editable) {
    const dialog = document.querySelector('dialog[open]');
    if (dialog) {
      event.preventDefault();
      dialog.close();
      return true;
    }
    if (editable) return false;
    if ($('tooltip').classList.contains('show')) {
      event.preventDefault();
      hidePreview();
      return true;
    }
    if ($('context-menu').classList.contains('show')) {
      event.preventDefault();
      hideContextMenu();
      closeMoreMenu();
      return true;
    }
    if (ui.placementCursor) {
      event.preventDefault();
      cancelPlacementMode();
      return true;
    }
    hideContextMenu();
    closeMoreMenu();
    if (appState.viewMode === 'problems' && appState.selectedProblemId) {
      appState.selectedProblemId = null;
      renderProblems();
    } else if (ui.selectionMode) {
      setSelectionMode(false);
      setStatus('Modo selección rectangular desactivado. Los puestos ya seleccionados se conservan.');
    } else if (appState.selectedWorkspaces.size > 1) {
      clearBulkSelection();
    } else if (plannerState().status !== 'idle') {
      closePlannerPanel();
    } else {
      closeDetailPanel();
    }
    $('search-results').classList.add('hidden');
    render();
    return true;
  }
  document.addEventListener('keydown', event => { const editable = isEditableKeyboardEvent(event); const singleKeyShortcut = !editable && ui.singleKeyShortcutsEnabled && !event.ctrlKey && !event.altKey && !event.metaKey; if (singleKeyShortcut && event.key === '/') { event.preventDefault(); $('search').focus(); } if (singleKeyShortcut && event.key.toLowerCase() === 'f') { event.preventDefault(); $('filter-bar').querySelector('button')?.focus(); } if (event.key === 'Escape' && handleEscape(event, editable)) return; if (singleKeyShortcut && event.key.toLowerCase() === 'e' && ui.seatId) $('seat-name').focus(); if (!editable && event.ctrlKey && event.key.toLowerCase() === 'z') { event.preventDefault(); if (ui.cardSizeUndo) { const { areaId, before } = ui.cardSizeUndo; const shapes = { ...appState.clusterCardShapes }; if (before === undefined) delete shapes[areaId]; else shapes[areaId] = before; appState.clusterCardShapes = shapes; ui.cardSizeUndo = null; saveClusterCardShapes(); refreshManagedAreaCard(areaId); } else $('undo').click(); } if (!editable && event.ctrlKey && event.key.toLowerCase() === 'y') showMessage('Rehacer todavía no está disponible.', 0); if (!editable && event.target.closest?.('#mapwrap')) { if (ui.placementCursor && /^Arrow/.test(event.key)) { event.preventDefault(); movePlacementCursor(event.key); return; } if (ui.placementCursor && event.key === 'Enter' && (event.target === $('mapwrap') || event.target === $('grid-cursor'))) { event.preventDefault(); confirmPlacementCursor(); return; } if (/^Arrow/.test(event.key)) { const next = adjacentSeat(event.key); if (next) { event.preventDefault(); selectSeat(next.id); centerSelectedSeat(); } } } });

  window.receiveFromNative = response => {
    if (!response?.success) { if (response?.action === 'bulkUpdateAssignmentsResult') appState.bulk.inFlight = null; if (response?.action === 'runValidationResult') failValidation(response?.error); else if (response?.action === 'runSpatialAnalyticsResult') failSpatialAnalytics(response?.error); else if (response?.action === 'runMovementPlannerResult' || response?.action === 'createScenarioFromMovementPlanResult') { appState.planner.status = 'error'; appState.planner.error = response?.error || 'No se pudo completar el planificador.'; renderPlanner(); finishRequest(false, response?.error); } else if (response?.action === 'saveUserPreferencesResult') showMessage(response?.error || 'No se pudo guardar la apariencia.', 0); else finishRequest(false, response?.error); return; }
    const action = response.action || ''; const data = response.data;
    if (action === 'getUserPreferencesResult' || action === 'saveUserPreferencesResult') applyUserPreferences(data);
    else if (action === 'runValidationResult') applyValidationResponse(data);
    else if (action === 'runSpatialAnalyticsResult') applySpatialAnalyticsResponse(data);
    else if (action === 'runMovementPlannerResult') { applyMovementPlan(data); finishRequest(true); }
    else if (action === 'createScenarioFromMovementPlanResult') { ui.openScenarioCompareAfterPlanner = true; resetPlanner(); notify('success', 'Escenario creado correctamente.'); requestLoad('reloadData', data?.id || data?.scenarioId, true); }
    else if (action === 'loadInitialDataResult' || action === 'reloadDataResult') { if (!load(data)) finishRequest(true); }
    else if (action === 'createScenarioResult') { $('scenario-dialog').close(); requestLoad('reloadData', data?.id || data?.scenarioId, true); }
    else if (action === 'deleteScenarioResult') requestLoad('reloadData', null, true);
    else if (action === 'getScenarioDiffResult') { renderDiff(data || {}); finishRequest(true); }
    else if (action === 'getUndoPreviewResult') { showUndoPreview(data); finishRequest(true); }
    else if (action === 'applyScenarioResult') { $('diff-dialog').close(); requestLoad('reloadData', scenarioId(), true); }
    else if (action === 'createSeatResult') { ui.pendingSeatId = data?.id || null; ui.pendingMapId = data?.mapId || ui.mapId; ui.pendingAreaFocusId = data?.targetManagedAreaId || null; requestLoad('reloadData', scenarioId(), true); }
    else if (/^(createManagedArea|renameManagedArea|addManagedAreaWorkspaces|removeManagedAreaWorkspaces|moveManagedAreaWorkspaces|mergeManagedAreas|dissolveManagedArea|deleteMoveManagedArea)Result$/.test(action)) { if (action === 'createManagedAreaResult') ui.pendingClusterId = data?.areaIds?.[0] || null; requestLoad('reloadData', scenarioId(), true); }
    else if (action === 'getEventsResult') { renderActivity('events-list', data?.events || data, false); finishRequest(true); }
    else if (action === 'getBackupsResult') { renderActivity('backups-list', data?.backups || data, true); finishRequest(true); }
    else if (action === 'getIntegrityReportResult') { renderIntegrity(data); finishRequest(true); }
    else if (action === 'reportPlanResourceDiagnosticResult') { }
    else if (action === 'exportExcelResult') { if (data?.cancelled) cancelRequest(); else { finishRequest(true); const message = `Excel exportado:\n${data?.path || ''}${data?.openFolderError ? `\n\nEl fichero se creó, pero no se pudo abrir el Explorador: ${data.openFolderError}` : ''}`; showMessage(message, data?.openFolderError ? 12000 : 8000); setStatus(message); } }
    else if (action === 'saveAssignmentResult') { const warnings = data?.warnings || []; ui.pendingWarning = warnings.length ? warnings.join(' ') : null; requestLoad('reloadData', scenarioId(), true); }
    else if (action === 'deleteSeatResult') { closeDetailPanel(); requestLoad('reloadData', scenarioId(), true); }
    else if (action === 'bulkUpdateAssignmentsResult') { const committed = appState.bulk.inFlight; appState.bulk.inFlight = null; if (committed && (data?.updated || 0) > 0) { appState.bulk.lastCommitted = committed; appState.bulk.pendingAction = null; ui.bulkConfirmation = `${committed.count} puestos ${committed.completed}.`; } requestLoad('reloadData', scenarioId(), true); }
    else if (action === 'undoLastChangeResult') { if (appState.bulk.undoRequested) { appState.bulk.lastCommitted = null; appState.bulk.undoRequested = false; appState.bulk.pendingAction = null; } requestLoad('reloadData', scenarioId(), true); }
    else if (/deleteAssignmentResult|saveSeatPositionResult|restoreBackupResult/.test(action)) { clearBulkSelection(); requestLoad('reloadData', scenarioId(), true); }
    else finishRequest(true);
  };
  window.chrome?.webview?.addEventListener?.('message', event => {
    window.receiveFromNative(event.data);
  });
  try { appState.gridCellMetadata = gridCellMetadataHelpers.normalizeMetadata(JSON.parse(localStorage.getItem('plano.gridCellMetadata') || '[]')); } catch { appState.gridCellMetadata = {}; }
  cellAppearanceFeature.load();
  try { appState.clusterCardShapes = JSON.parse(localStorage.getItem('plano.clusterCardShapes') || '{}') || {}; } catch { appState.clusterCardShapes = {}; }

  mapAppearanceFeature.apply(mapAppearanceFeature.loadPreference());
  loadMapAppearanceManifest();
  send('getUserPreferences', {}, true);
  send('loadInitialData');

  // ══ Bindings de filtros ══
  if ($('filter-bar')) {
    $('filter-bar').querySelectorAll('button').forEach(btn => {
      btn.onclick = () => {
        $('filter-bar').querySelectorAll('button').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        appState.filters.quick = btn.dataset.filter;
        render();
      };
    });
  }
})();
