## Context

PR #35 established a safer baseline: root enumeration failure aborts synchronization, `TrackButton` appends successful controls to cumulative panel membership, and `HideUnused` enumerates a key snapshot before removing inactive mappings. Autodesk controls can still be hidden but not reliably removed from the ribbon.

The remaining flow is not a complete reconciliation. `SyncGraphs` enumerates nested directories while creating host controls, `HideUnused` independently rechecks `File.Exists`, graph identity is recovered from tooltip text, and `Globals.RelayPanels` is keyed only by panel name. Once a graph is removed, its active mapping is discarded; if the same path returns, Relay creates another host control instead of deliberately reactivating the historical one.

## Goals / Non-Goals

**Goals:**

- Make repeated synchronization idempotent for an unchanged filesystem.
- Reconcile discovered paths, button mappings, historical controls, and panel visibility as one state transition.
- Keep filesystem comparison logic testable without Revit.

**Non-Goals:**

- Remove Autodesk controls from the ribbon or monitor directories continuously.
- Change graph execution or folder layout.

## Decisions

1. Build an immutable discovery snapshot before touching the ribbon. The snapshot contains normalized graph paths grouped by a typed tab-and-panel identity. All applicable directories must enumerate successfully before the snapshot is eligible for reconciliation. The alternative, updating globals during directory traversal, preserves partial-state risk.
2. Compare the completed snapshot with a typed registry to produce a pure reconciliation plan: unchanged, add, deactivate, and reactivate. Apply that plan to Revit only after comparison succeeds. The alternative, combining comparison with host mutation, cannot be tested reliably and can leave partial UI state.
3. Keep active graph lookup separate from historical host controls. A removed graph leaves a hidden historical record; the same normalized path reuses and reveals that control when host behavior permits. A new path creates a new control. This reflects Revit's removal limitation instead of treating hidden controls as deleted.
4. Identify panel membership by both tab and panel name. Panel visibility is derived from every active mapping in that identity, avoiding collisions when different tabs use the same panel name.
5. Store normalized graph paths and control identity in Relay-owned registry state. Ribbon tooltip and description remain presentation metadata and are not used by synchronization as the source of graph identity. Deterministic command activation remains scoped to the separate `route-ribbon-commands-deterministically` change.

## Risks / Trade-offs

- [Ribbon controls accumulate after many unique renames] -> Reactivate stable-path controls where possible and retain only the historical host reference required by Revit.
- [Directory enumeration fails midway] -> Do not apply a new snapshot unless discovery for the relevant root completes successfully.
- [A Revit version cannot reveal a hidden control safely] -> Centralize visibility operations, fall back to creating one replacement mapping, and verify behavior in Revit 2025-2027.
- [Existing panel names collide across tabs] -> Use a composite tab-and-panel identity in registry and reconciliation models.

## Migration Plan

Introduce the pure snapshot and reconciliation models behind the existing explicit/startup Sync entry points, then replace direct global dictionary cleanup with the typed registry. No persisted data migration is needed; state is rebuilt on every Revit session. Rollback restores the PR #35 in-memory implementation.

## Open Questions

- Whether every supported Revit version permits safely revealing a hidden control after a graph is deleted and recreated at the same path must be confirmed in host testing.
