## Why

PR #35 stopped Sync Graphs from mutating its button dictionary during enumeration and made new controls append to panel membership. The remaining implementation still discovers and mutates ribbon state in one host-bound traversal, derives graph identity from presentation metadata, and cannot reliably reactivate a historical control when a graph returns.

## Problem

Relay has no complete discovery snapshot or typed reconciliation model. It compares individual files against global dictionaries while creating controls, keys panels only by panel name, and removes missing paths from active lookup without retaining enough identity to reuse their hidden controls.

## Scope

- Build a complete, normalized discovery snapshot before changing ribbon state.
- Reconcile that snapshot against typed tab, panel, graph, and historical-control state.
- Hide only buttons whose graph no longer exists and hide a panel only when all tracked buttons are hidden.
- Restore panels and reuse historical controls when valid content remains or returns at the same path.

## Non-goals

- Rebuilding Autodesk ribbon controls that Revit does not allow Relay to remove.
- Changing graph execution or introducing dynamic graph input UI.
- Watching the filesystem continuously; synchronization remains explicit/startup-driven.

## What Changes

- Extract complete graph discovery and snapshot comparison from `RibbonUtils.SyncGraphs`.
- Replace global dictionary cleanup with a typed, two-phase reconciliation plan.
- Identify panels by tab and panel, and keep graph identity independent from tooltip or description text.
- Reactivate reusable historical controls when a graph returns at the same normalized path.
- Extend the existing Relay test project with focused discovery and reconciliation coverage.

## Capabilities

### New Capabilities

- `ribbon-graph-synchronization`: Defines repeatable graph discovery and ribbon-state reconciliation.

### Modified Capabilities

None.

## Risks

- Autodesk ribbon items cannot be physically removed, so hidden controls must remain tracked correctly.
- Reusing a hidden control may expose host-version differences and requires Revit verification.
- Revit ribbon API behavior may differ between supported host versions.

## Compatibility

Applies to Revit 2025, 2026, and 2027. No Dynamo API behavior is changed.

## Impact

Primarily affects `src/Utilities/RibbonUtils.cs`, `src/Utilities/Globals.cs`, new pure synchronization models, and `tests/Relay.Tests`. PR #35 already supplies safe dictionary enumeration, cumulative panel membership, failed-root preservation, and the reusable test project baseline.
