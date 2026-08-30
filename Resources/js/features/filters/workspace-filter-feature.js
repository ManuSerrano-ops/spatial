(() => {
  'use strict';

  function createWorkspaceFilterFeature({ state, stateFor, completenessFor, valuesFor, people, devices, nameFor }) {
    function matches(workspace) {
      const filters = state.filters;
      const workspaceState = stateFor(workspace);
      const completeness = completenessFor(workspace);
      const quick = filters.quick === 'occupied' ? workspaceState === 'occupied'
        : filters.quick === 'free' ? workspaceState === 'free'
          : filters.quick === 'reserved' ? workspaceState === 'reserved'
            : filters.quick === 'partial' ? completeness === 'incomplete'
              : true;
      const values = valuesFor(workspace);
      const person = nameFor(people(), values.personId).toLowerCase();
      const device = `${nameFor(devices(), values.deviceId)} ${JSON.stringify(devices().find(item => item.id === values.deviceId) || {})}`.toLowerCase();
      return quick
        && (!filters.zone || workspace._mapId === filters.zone)
        && (!filters.person || person.includes(filters.person))
        && (!filters.device || device.includes(filters.device))
        && (!filters.roseta || String(values.roseta || '').toLowerCase().includes(filters.roseta));
    }

    return Object.freeze({ matches });
  }

  const api = Object.freeze({ createWorkspaceFilterFeature });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (typeof window !== 'undefined') window.WorkspaceFilterFeature = api;
})();
