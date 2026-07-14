## Context

Every graph button invokes the same `Relay.Run` command. `ComponentManager.UIElementActivated` tries to infer the graph by matching `Id.Contains("relay")` and parsing a bracketed path from `Description`, then writes it to `Globals.CurrentGraphToRun`. The command cannot prove that global value belongs to the activation that launched it.

## Goals / Non-Goals

**Goals:**

- Bind each Relay control identity to exactly one normalized graph path.
- Reject missing, stale, or non-Relay activations before execution.
- Keep human-facing tooltip/description independent from routing metadata.

**Non-Goals:**

- Create one external command class per graph or change Dynamo execution.
- Route controls owned by other add-ins.

## Decisions

1. Generate a deterministic internal control name from a collision-resistant hash of the normalized full path plus a Relay prefix. This supports duplicate filenames and stable lookup across syncs. Random GUID names avoid collisions but prevent deterministic reconciliation.
2. Maintain a typed registry from every Autodesk/Revit identity Relay can observe to a graph record. Ribbon text and tooltips are not identifiers. The registry is populated only after control creation succeeds.
3. Treat activation as a short-lived command context. The handler clears prior context first, then sets a validated graph only for a known Relay identity. `Run.Execute` atomically consumes that context so it cannot be reused by a later unrelated command.
4. Return `Result.Failed` with an actionable message when routing fails. Silent catches are replaced with scoped diagnostics around host metadata access.

## Risks / Trade-offs

- [Revit changes the activated item identifier format] -> Capture and normalize both wrapper identifiers behind a version-tested adapter.
- [Hashed names are opaque during debugging] -> Include graph display name and path in registry diagnostics.
- [Activation and command ordering differs by host] -> Verify ordering in Revit 2025-2027 and retain a narrowly scoped fallback only if evidence requires it.

## Migration Plan

The registry is rebuilt at startup; there is no persisted migration. Existing controls from an older loaded assembly require a Revit restart, which is already required to load a new add-in build.

## Open Questions

- Confirm which activated-item property is stable across all three supported Revit versions.
