## Context

`DynamoMethods.RunGraph` creates a temporary graph via literal text replacement, locates an assembly whose name contains `DynamoRevitDS`, and invokes unchecked reflected members. It loads and forces evaluation as one opaque operation, so a later input workflow has no validated point at which to inspect a loaded workspace, apply document-backed bindings, or cancel before evaluation. Temporary cleanup is reliable, but preparation and binding failures have little context and the document lifecycle marker is coupled to successful reflection calls.

## Goals / Non-Goals

**Goals:**

- Preserve source graphs while preparing a structurally valid automatic-run copy.
- Validate the Dynamo adapter before invocation and report incompatibility clearly.
- Provide prepare, load, bind, and evaluate stages without evaluating during graph load.
- Define model reuse across documents and guarantee cleanup.

**Non-Goals:**

- Replace DynamoRevit, install packages, or guarantee third-party node compatibility.
- Discover or present dynamic graph inputs; this change defines only typed binding and staged execution boundaries.

## Decisions

1. Parse the `.dyn` document with `JsonNode`/`JsonDocument` semantics and update only the top-level `RunType`, writing a new temporary file. Literal replacement is rejected because formatting or a missing comma changes behavior silently.
2. Define an `IDynamoRunner` that creates an `IDynamoExecutionSession` through one adapter per materially different DynamoRevit surface. The session exposes validated load, bind, and evaluate stages; graph load MUST remain paused until the caller explicitly evaluates it. Direct graph execution passes an empty binding set through the same path.
3. Keep typed binding contracts version-neutral and keyed by Dynamo node GUID. The hardening layer does not discover inputs or interpret UI values; it validates that each supplied binding targets the expected runtime node and delegates version-specific mutation to the adapter.
4. Isolate reflection where public compile-time APIs cannot span versions. Each adapter validates assembly identity, required types, execution members, input-binding members, and compatible signatures before the relevant stage.
5. Return typed preparation, load, binding, invocation, cancellation, and cleanup outcomes that map to a Revit `Result` and detailed diagnostic. Unexpected exceptions retain stack/context and are not converted to success.
6. Track the last successfully attached Revit document via weak reference. A document change requests model shutdown/reinitialization according to the version adapter; failed or cancelled sessions do not advance lifecycle state.
7. Make the execution session and temporary graph disposable owners. Cancellation or failure before evaluation closes any staged model/workspace state and attempts temporary-file deletion; cleanup failure is reported separately from the primary outcome.

## Risks / Trade-offs

- [Reflected Dynamo surface changes] -> Keep adapters small, validate eagerly, and host-test each supported matrix entry.
- [A loaded graph evaluates before bindings are applied] -> Configure and verify paused load in every adapter, and expose evaluation as an explicit one-shot operation.
- [Partially applied bindings leave ambiguous state] -> Prevent evaluation after any binding failure and dispose the entire execution session.
- [JSON serialization changes formatting] -> Operate only on the temporary copy and verify semantic preservation in tests.
- [Model shutdown policy loses performance] -> Prefer correctness across documents; measure before introducing reuse optimization.

## Migration Plan

Introduce the staged runner and session boundary behind the existing `RunGraph` entry point, pass an empty binding set for current callers, then migrate one supported version at a time. The later dynamic-input change can supply typed bindings without replacing the lifecycle contract. Rollback can restore the prior runner without changing graph files or settings.

## Open Questions

- Confirm the final DynamoRevit package/version and callable surface shipped with Revit 2027 before locking that adapter.
- Confirm the paused-load and runtime node-binding members available in Dynamo 3.0, 3.6, and 4.0 before finalizing each session adapter.
