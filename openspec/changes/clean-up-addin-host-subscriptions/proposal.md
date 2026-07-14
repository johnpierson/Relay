## Why

Relay subscribes static Revit ribbon, application-initialized, and assembly-resolution handlers during startup but removes none during shutdown. Repeated load/unload or host teardown can retain Relay state, invoke stale handlers, and complicate assembly resolution for the entire AppDomain.

## Problem

The add-in has no explicit ownership model for host subscriptions or process-scoped mutable state.

## Scope

- Track every host event subscription established during startup.
- Unsubscribe owned handlers during shutdown in a safe order.
- Clear Relay's process-scoped ribbon and graph selection registries.
- Make startup and shutdown idempotent enough to avoid duplicate handlers.

## Non-goals

- Unloading assemblies from Revit's AppDomain.
- Deleting Autodesk ribbon controls during shutdown.
- Changing graph discovery or Dynamo execution behavior.

## What Changes

- Pair startup subscriptions with explicit shutdown cleanup.
- Guard against duplicate subscription and partial-startup states.
- Reset mutable globals whose references should not survive add-in shutdown.
- Add lifecycle tests around the extracted subscription/state coordinator.

## Capabilities

### New Capabilities

- `addin-host-lifecycle`: Defines ownership and cleanup of Relay host subscriptions and process state.

### Modified Capabilities

None.

## Risks

- Shutdown can occur after partial initialization, so cleanup must tolerate missing subscriptions and controls.
- Incorrect unsubscription order could remove data needed by another Relay cleanup step.

## Compatibility

Applies to Revit 2025, 2026, and 2027. No Dynamo API behavior is changed.

## Impact

Primarily affects `src/App.cs`, `src/Utilities/Globals.cs`, and lifecycle-focused tests.
