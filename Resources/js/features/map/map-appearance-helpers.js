(() => {
  'use strict';

  const modes = new Set(['dark', 'light']);
  const defaultManifestUrl = 'map-themes/light/manifest.json';
  let manifest = null;
  let assetsByCanonicalName = new Map();

  function normalizeMode(value) { return modes.has(String(value || '').toLowerCase()) ? String(value).toLowerCase() : 'dark'; }
  function basename(value) { return String(value || '').replace(/^.*[\\/]/, ''); }
  function validateManifest(value) {
    if (!value || Number(value.schemaVersion) < 2 || !Array.isArray(value.assets)) return false;
    return value.assets.every(asset => asset && asset.id && asset.canonical && asset.dark && asset.light && asset.lightSha256);
  }
  function normalizeManifest(value) {
    if (!value || !Array.isArray(value.assets)) return null;
    const version = Number(value.schemaVersion) || 1;
    const assets = value.assets.map(asset => {
      if (!asset) return null;
      if (version >= 2) return asset;
      return {
        id: asset.id,
        canonical: asset.output || '',
        dark: asset.output || '',
        light: asset.output || '',
        lightSha256: asset.outputSha256 || '',
        transform: asset.transform || [1, 0, 0, 1, 0, 0],
      };
    }).filter(Boolean).map(Object.freeze);
    return Object.freeze({ schemaVersion: 2, assets: Object.freeze(assets) });
  }
  function configureManifest(value) {
    const normalized = normalizeManifest(value);
    if (!normalized || !validateManifest(normalized)) { manifest = null; assetsByCanonicalName = new Map(); return false; }
    const next = new Map();
    normalized.assets.forEach(asset => {
      [asset.canonical, asset.dark].map(basename).filter(Boolean).forEach(name => next.set(name, Object.freeze({ ...asset })));
    });
    manifest = Object.freeze({ ...normalized, assets: Object.freeze(normalized.assets.map(asset => Object.freeze({ ...asset }))) });
    assetsByCanonicalName = next;
    return true;
  }
  function loadManifestSync(url = defaultManifestUrl) {
    if (typeof XMLHttpRequest === 'undefined') return false;
    try {
      const request = new XMLHttpRequest();
      request.open('GET', url, false);
      request.send(null);
      if (request.status !== 0 && (request.status < 200 || request.status >= 300)) return configureManifest(null);
      return configureManifest(JSON.parse(request.responseText));
    } catch { return configureManifest(null); }
  }
  function hasManifest() { return Boolean(manifest); }
  function effectiveMode(value) { const requested = normalizeMode(value); return requested === 'light' && !hasManifest() ? 'dark' : requested; }
  function resolveMapPresentationAsset(resource, mode = 'dark') {
    const requested = normalizeMode(mode);
    if (requested !== 'light' || !hasManifest()) return resource || '';
    const asset = assetsByCanonicalName.get(basename(resource));
    return asset?.light || resource || '';
  }
  function deriveMapContrastTokens(mode = 'dark') {
    return Object.freeze(effectiveMode(mode) === 'light'
      ? { canvas: '#fafafa', grid: 'rgba(31, 41, 55, .16)', gridBlend: 'normal', pinHalo: '#ffffff', clusterSurface: '#ffffff', clusterInk: '#111827', dimmedOpacity: '.34' }
      : { canvas: '#313131', grid: 'rgba(200, 210, 220, .34)', gridBlend: 'screen', pinHalo: '#ffffff', clusterSurface: '#20262f', clusterInk: '#f8fafc', dimmedOpacity: '.24' });
  }
  function deriveMapAppearance(mode = 'dark') { const resolved = effectiveMode(mode); return Object.freeze({ mode: resolved, tokens: deriveMapContrastTokens(resolved) }); }
  function getManifest() { return manifest; }

  const api = { normalizeMode, effectiveMode, resolveMapPresentationAsset, deriveMapContrastTokens, deriveMapAppearance, configureManifest, normalizeManifest, loadManifestSync, hasManifest, getManifest };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') {
    window.MapAppearanceHelpers = api;
    loadManifestSync();
  }
})();
