# Workspace identity and presentation architecture

## Field model

| Field | Meaning | Presentation rule |
|---|---|---|
| `maps[].id` | Technical map key | Navigation key; never primary human label. |
| `maps[].seats[].id` | Technical workspace key | Navigation key with `mapId`; never a person label. |
| `displayLocation` | Computed from persisted `displayLocation`, `gridCell`, or coordinates | Primary human location. Presentation only, never lookup key. |
| `maps[].seats[].name` | Legacy/reference text associated with a workspace | `workstationReference`; never infer a current person from text. |
| `reference`, `code`, `workstation` | Optional future/reference aliases | Precede legacy `name` when present. |
| `assignments[].personId` | Current assignment person when a record exists | Takes precedence over `seat.personId`. |
| `maps[].seats[].personId` | Existing map-level current-person fallback | Used only when assignment has no `personId`; text labels never substitute it. |
| `people[].username` / `name` | Directory display for a current person ID | Human current-user label. |
| `assignments[].deviceId`, `seat.deviceId`, `seat.deviceName` | Equipment association | Equipment presentation only. |
| `assignments[].roseta`, `seat.roseta` | Network outlet | Network presentation only. |
| `seat.type` / assignment status | Business state or inherited drawing marker | Display as state; not evidence of a current person. |

## Global identity hierarchy

1. **Location:** `displayLocation`
2. **Workstation reference:** reference/code/workstation/legacy `seat.name`
3. **Current user:** assignment `personId`, then existing map `personId` fallback
4. **Status, equipment, network and problem summary**

A human-looking `seat.name` with no person ID remains a reference. It is never
shown as the current user.

## QA runtime-data audit

Read-only source: `qa-runtime-data/data`.

| Metric | Count |
|---|---:|
| Total workspaces | 270 |
| With current person ID | 112 |
| Without current person ID | 158 |
| With nonblank reference/name | 268 |
| Historical occupied without assignment | 201 |
| Occupied drawing marks | 212 |

`reference looks human / no assignment` is operationally equivalent to the 201
active historical occupied marks: these records have `type: occupied`, no
assignment record by `workstationId`, and a nonblank legacy `seat.name` except
for individual blank-reference cases.

### Historical occupied without assignment

| Map | Total | Trigger | Current assignment |
|---|---:|---|---|
| Norte | 22 | `seat.type = occupied`, no matching assignment | no record |
| Nivel 3 | 28 | `seat.type = occupied`, no matching assignment | no record |
| Sur | 151 | `seat.type = occupied`, no matching assignment | no record |
| I+D | 0 | — | — |
| QC | 0 | — | — |

The canonical rule is `historical-occupied-without-assignment` in
`ValidationEngine.cs`: an occupied drawing mark with no assignment record is
an **Info** problem. It does not establish a current person.

### Classification

| Class | Rule | Result |
|---|---|---:|
| A | Historical marker + no assignment | 201 active records |
| B | Reference/name only + no current person | contained in A when the marker is occupied; 268 references total |
| C | Valid current person assignment/fallback | 112 |
| D | Inconsistent/missing data | Included in Validation/Integrity output; no data changed by this work |
| E | Other | Remaining free/reserved/non-person workspace states |

## Shared presentation helper

`Resources/js/shared/workspace/workspace-presentation-helpers.js` exports the pure
`buildWorkspacePresentation(input)` helper. It returns:

```js
{
  displayLocation,
  workstationReference,
  currentPerson,
  assignmentStatus,
  equipment,
  networkOutlet,
  problemSummary,
  ariaLabel,
  title
}
```

It is used by map pin context, tooltip, inspector, list, search and Planner
text. It does not persist data or infer people from reference text.

## Selection and problem navigation

A Problem resolves related workspaces through `problemTargets()`, then uses
`workspaceByIdentity(mapId, workspaceId)` and `navigateToWorkspace(...)`.
`mapId + technical seatId` remain the navigation identity. `displayLocation`
is never used as a key.

Selecting a Problem now selects its first resolvable workspace target, navigates
to its map and renders the same workspace inspector used by normal map/list
selection. The selected Problem record remains separate because one validation
record can relate to more than one workspace.
