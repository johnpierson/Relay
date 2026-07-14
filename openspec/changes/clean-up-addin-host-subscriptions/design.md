## Context

`App.OnStartup` subscribes `ComponentManager.UIElementActivated`, `AppDomain.AssemblyResolve`, and `ControlledApplication.ApplicationInitialized`. `OnShutdown` returns immediately, leaving subscriptions and static registries intact. Startup can also exit after only some subscriptions have been established.

## Goals / Non-Goals

**Goals:**

- Pair every Relay-owned subscription with deterministic cleanup.
- Make cleanup safe after full, partial, or repeated initialization.
- Release static references to ribbon items, documents, and pending graph context.

**Non-Goals:**

- Unload assemblies or remove ribbon controls from Autodesk's host.
- Change discovery, command routing, or Dynamo behavior beyond clearing owned state.

## Decisions

1. Give the `App` instance explicit boolean ownership flags for each subscription. Subscribe through small methods and set ownership only after success. This is clearer than relying on repeated `-=` calls without knowing partial startup state.
2. Unsubscribe in reverse startup order during `OnShutdown`, with each cleanup independently attempted and diagnosed. One failed host detachment must not skip the remaining cleanup.
3. Add one `Globals.ResetSessionState` operation that clears collections and pending graph/document references but retains immutable assembly metadata. Scattered clears risk omissions as state grows.
4. Prevent duplicate initialization by rejecting or first cleaning an already-owned subscription set. This supports testability even if Revit normally creates one application instance.

## Risks / Trade-offs

- [Static Autodesk event outlives the app instance] -> Keep the delegate target stable and always detach the same instance method.
- [Shutdown occurs after host objects are partly disposed] -> Detach process/AppDomain events independently and treat host-detach failures as diagnostics.
- [Clearing state conflicts with Dynamo cleanup] -> Define reverse ownership order and let the Dynamo execution component release its own state before global reset.

## Migration Plan

No persisted migration is required. A Revit restart loads the new lifecycle behavior. Rollback restores the previous startup/shutdown implementation.

## Open Questions

- Confirm whether `ApplicationInitialized` automatically releases one-shot handlers; Relay will still detach it explicitly for ownership clarity.
