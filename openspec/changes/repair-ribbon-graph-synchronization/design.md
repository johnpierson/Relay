## Context

`RibbonUtils.SyncGraphs` incrementally creates controls while `Globals.RelayButtons` and `Globals.RelayPanels` act as the in-memory index. `AddItems` replaces panel membership with only the latest stacked items, and `HideUnused` removes dictionary entries during enumeration. Autodesk controls can be hidden but not reliably removed from the ribbon.

## Goals / Non-Goals

**Goals:**

- Make repeated synchronization idempotent for an unchanged filesystem.
- Reconcile discovered paths, button mappings, and panel visibility as one state transition.
- Keep filesystem comparison logic testable without Revit.

**Non-Goals:**

- Remove Autodesk controls from the ribbon or monitor directories continuously.
- Change graph execution or folder layout.

## Decisions

1. Build an immutable discovery snapshot before touching the ribbon. The snapshot contains normalized graph paths grouped by tab and panel. This avoids partially mutating host UI while enumeration is still failing. The alternative, updating globals during directory traversal, preserves the current partial-state risk.
2. Reconcile snapshots against a typed registry. Existing mappings remain when their paths are present; missing paths are hidden and removed from active lookup only after enumeration; new paths are created and appended to complete panel membership. The alternative of replacing each panel list loses existing controls.
3. Keep hidden historical controls in a separate host-item record when required by Revit, while active graph lookup contains only current paths. This reflects the host limitation instead of treating hidden controls as deleted.
4. Store the graph path in both Relay's registry and ribbon metadata needed by Autodesk events. Tooltip remains presentation text, not the source of identity.

## Risks / Trade-offs

- [Ribbon controls accumulate after many renames] -> Reuse a stable path identity where possible and document that Revit cannot remove controls during a session.
- [Directory enumeration fails midway] -> Do not apply a new snapshot unless discovery for the relevant root completes successfully.
- [Host wrappers expose visibility differently] -> Centralize visibility updates and verify them in Revit 2025-2027.

## Migration Plan

Replace the current dictionaries with the typed registry during startup. No persisted data migration is needed; state is rebuilt on every Revit session. Rollback restores the prior in-memory implementation.

## Open Questions

- Whether Revit permits safely reusing a hidden control after a graph is deleted and recreated at the same path must be confirmed in host testing.
