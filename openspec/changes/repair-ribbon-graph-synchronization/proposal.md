## Why

Syncing graphs can throw while removing dictionary entries during enumeration and can lose panel membership when only newly created buttons are recorded. The result is a user-visible ribbon that hides valid graph buttons or entire panels after a sync.

## Problem

Relay treats incremental additions as the complete panel state, mutates the button registry while iterating it, and derives graph identity from tooltip text that is not consistently available through Autodesk ribbon events.

## Scope

- Reconcile discovered graph paths with existing ribbon state without removing live entries during enumeration.
- Preserve complete panel membership across repeated syncs.
- Hide only buttons whose graph no longer exists and hide a panel only when all tracked buttons are hidden.
- Restore panels and buttons when valid content remains or is added.

## Non-goals

- Rebuilding Autodesk ribbon controls that Revit does not allow Relay to remove.
- Changing graph execution or introducing dynamic graph input UI.
- Watching the filesystem continuously; synchronization remains explicit/startup-driven.

## What Changes

- Replace in-loop registry mutation with a reconciliation pass.
- Merge newly created items into panel state rather than replacing prior membership.
- Ensure graph metadata remains available to both Revit and Autodesk ribbon representations.
- Add focused tests for discovery and reconciliation logic outside the Revit host.

## Capabilities

### New Capabilities

- `ribbon-graph-synchronization`: Defines repeatable graph discovery and ribbon-state reconciliation.

### Modified Capabilities

None.

## Risks

- Autodesk ribbon items cannot be physically removed, so hidden controls must remain tracked correctly.
- Revit ribbon API behavior may differ between supported host versions.

## Compatibility

Applies to Revit 2025, 2026, and 2027. No Dynamo API behavior is changed.

## Impact

Primarily affects `src/Utilities/RibbonUtils.cs`, `src/Utilities/Globals.cs`, and tests for pure graph-state reconciliation.
