(() => {
  'use strict';

  function createWorkspaceFilterUiFeature({ state, hasLoaded, allWorkspaces, matches, maps, getElement, document, onFiltersChanged }) {
    function renderChips() {
      const host = getElement('filter-chips');
      if (!host) return;
      const labels = {
        zone: id => maps().find(map => map.id === id)?.name || id,
        person: value => `Persona: ${value}`,
        device: value => `Equipo: ${value}`,
        roseta: value => `Roseta: ${value}`,
        quick: value => value === 'all' ? '' : value
      };
      host.replaceChildren(...Object.entries(state.filters)
        .filter(([key, value]) => value && key !== 'only')
        .map(([key, value]) => {
          const button = document.createElement('button');
          button.textContent = `${labels[key]?.(value) || value} ×`;
          button.onclick = () => {
            state.filters[key] = key === 'quick' ? 'all' : '';
            const input = getElement(`filter-${key}`);
            if (input) input.value = '';
            onFiltersChanged();
          };
          return button;
        }));
    }

    function updateCount() {
      if (!hasLoaded()) return;
      const all = allWorkspaces();
      const matching = all.filter(matches).length;
      const count = getElement('filter-count');
      if (count) count.textContent = `${matching} resultados de ${all.length}`;
      renderChips();
    }

    function bindControls() {
      [['filter-zone', 'zone'], ['filter-person', 'person'], ['filter-device', 'device'], ['filter-roseta', 'roseta']]
        .forEach(([id, key]) => {
          getElement(id).oninput = event => {
            state.filters[key] = event.target.value.trim().toLowerCase();
            onFiltersChanged();
          };
        });
      getElement('filter-only').onchange = event => {
        state.filters.only = event.target.checked;
        onFiltersChanged();
      };
    }

    return Object.freeze({ bindControls, renderChips, updateCount });
  }

  const api = Object.freeze({ createWorkspaceFilterUiFeature });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.WorkspaceFilterUiFeature = api;
})();
