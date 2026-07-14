## Context

`DynamoMethods.RunGraph` creates a temporary graph via literal text replacement, locates an assembly whose name contains `DynamoRevitDS`, and invokes unchecked reflected members. Temporary cleanup is reliable, but preparation and binding failures have little context and the document lifecycle marker is coupled to successful reflection calls.

## Goals / Non-Goals

**Goals:**

- Preserve source graphs while preparing a structurally valid automatic-run copy.
- Validate the Dynamo adapter before invocation and report incompatibility clearly.
- Define model reuse across documents and guarantee cleanup.

**Non-Goals:**

- Replace DynamoRevit, install packages, or guarantee third-party node compatibility.
- Introduce dynamic graph input UI.

## Decisions

1. Parse the `.dyn` document with `JsonNode`/`JsonDocument` semantics and update only the top-level `RunType`, writing a new temporary file. Literal replacement is rejected because formatting or a missing comma changes behavior silently.
2. Define an `IDynamoRunner` boundary with one adapter per materially different DynamoRevit surface. Reflection remains isolated where public compile-time APIs cannot span versions. Each adapter validates assembly identity, required types, members, and compatible signatures before invocation.
3. Return a typed execution outcome that maps expected failures to a Revit `Result` and detailed diagnostic. Unexpected exceptions retain stack/context and are not converted to success.
4. Track the last successfully attached Revit document via weak reference. A document change requests model shutdown/reinitialization according to the version adapter; failed runs do not advance lifecycle state.
5. Own the temporary graph with `IDisposable`-style cleanup so every exit path attempts deletion and reports cleanup failure separately.

## Risks / Trade-offs

- [Reflected Dynamo surface changes] -> Keep adapters small, validate eagerly, and host-test each supported matrix entry.
- [JSON serialization changes formatting] -> Operate only on the temporary copy and verify semantic preservation in tests.
- [Model shutdown policy loses performance] -> Prefer correctness across documents; measure before introducing reuse optimization.

## Migration Plan

Introduce the runner boundary behind the existing `RunGraph` entry point, then migrate one supported version at a time. Rollback can restore the prior runner without changing graph files or settings.

## Open Questions

- Confirm the final DynamoRevit package/version and callable surface shipped with Revit 2027 before locking that adapter.
