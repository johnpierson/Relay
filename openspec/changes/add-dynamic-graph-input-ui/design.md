## Context

Relay currently resolves one graph path and invokes `DynamoMethods.RunGraph`. The runner creates an automatic temporary copy, loads DynamoRevit through reflection, and immediately forces evaluation. Dynamo graph authors already identify player-facing fields with `IsInput`, but Relay neither interprets those declarations nor has a pause between graph load and evaluation where document-backed input state can be applied.

Primitive values can often be read from and written to graph JSON. Revit dropdowns such as Categories are different: their options are supplied by the active Revit/Dynamo environment, display names may be localized, and serialized indices are not stable identities across versions. The input feature therefore needs a version-neutral Relay model plus a version-specific runtime binding boundary.

## Goals / Non-Goals

**Goals:**

- Honor Dynamo's existing `IsInput` authoring convention.
- Support primitive, path, slider, and Revit Categories inputs in the initial compatibility matrix.
- Keep input discovery and validation testable without Revit wherever possible.
- Bind Categories through stable identities and the active version adapter.
- Prevent evaluation until all required accepted bindings are applied.
- Preserve direct execution for graphs with no declared inputs.

**Non-Goals:**

- Clone Dynamo Player, render graph outputs, or support arbitrary custom input UI.
- Treat localized labels, selected indices, or node positions as durable identities.
- Persist values between runs or alter source graphs.
- Expand the initial contract to Levels, Views, Element Types, or element selection without a later compatibility decision.

## Decisions

1. Parse `.dyn` JSON before execution and map nodes marked `IsInput` to version-neutral `GraphInputDescriptor` values keyed by node GUID. Discovery records the concrete Dynamo node identity and serialized binding metadata but exposes only Relay input kinds to the UI.
2. Use an explicit supported-node registry per Dynamo adapter. The initial kinds are string, number, boolean, integer slider, number slider, file path, directory path, and Revit Categories. An unknown or unsupported declared input is represented explicitly; Relay does not guess a control from its current JSON value.
3. Generate one Relay-owned WPF dialog from immutable descriptors and return a typed accepted, cancelled, or invalid result. UI controls bind to view models rather than Dynamo or Revit objects so validation can be unit tested.
4. Treat a supported declared input with no valid value as blocking. Treat an unsupported declared input as blocking by default because silently evaluating with an old or default value would make the dialog misleading. The dialog identifies every blocking node by its custom name and input kind.
5. Bind JSON-safe primitive and path values only to the disposable temporary graph. Never update the source `.dyn`. The binder addresses nodes by GUID and verifies the expected concrete type before changing node-specific fields.
6. Represent a category choice with stable host identity plus localized display text. Display text and dropdown index are never the durable binding key. The active Dynamo adapter resolves the stable category identity against the instantiated Categories node and applies the matching runtime item before evaluation.
7. Split execution into prepare, load, bind, and evaluate stages behind an `IDynamoExecutionSession`. Loading MUST NOT evaluate the graph. The session validates all bindings atomically enough that a failure prevents evaluation; it reports which node and compatibility boundary rejected the value.
8. Obtain Categories choices from the runtime node through the version adapter when that callable surface is available, because it most closely matches the installed Dynamo version. A direct Revit category provider may be used only if host verification proves the resulting stable identities and supported set are equivalent; adapters own this policy.
9. Keep all Revit and Dynamo access on the valid external-command/UI thread. The input window is modal to the Relay command. Cancellation closes the execution session, cleans the temporary graph, and returns `Result.Cancelled` without updating successful-document lifecycle state.
10. Graphs with no `IsInput` declarations bypass the dialog and use the same staged runner with an empty binding set. This preserves current user-visible behavior while keeping one execution path.

## Data Flow

```text
registered graph activation
          |
          v
parse IsInput declarations ---- invalid JSON ----> failed command
          |
          v
prepare temporary graph
          |
          v
load paused Dynamo session
          |
          +---- adapter supplies Categories choices
          |
          v
generated WPF dialog ---- cancel ----> dispose session and temporary graph
          |
          v
validate typed values
          |
          v
apply JSON and runtime bindings ---- binding failure ----> no evaluation
          |
          v
evaluate graph ----> outcome ----> cleanup
```

## Compatibility Matrix

Each supported Revit/Dynamo adapter documents and host-tests:

- Recognized concrete node identities for each Relay input kind.
- Serialized fields used for primitive discovery and temporary-copy binding.
- Runtime members used to enumerate and select Categories items.
- Stable Revit category identity conversion in both directions.
- Behavior when a graph was authored by a different compatible Dynamo version.

Missing or incompatible members fail adapter validation with an actionable diagnostic. Relay does not fall back to a selected index or localized name.

## Risks / Trade-offs

- [Dynamo initializes before confirmation] -> Load only when runtime-backed choices are required; primitive-only graphs can show the dialog before session creation if this does not split observable behavior.
- [Runtime dropdown APIs are internal] -> Isolate reflection in small version adapters and validate members before showing an actionable Categories control.
- [Category set changes with document or version] -> Generate choices for the active execution context and revalidate the stable identity immediately before binding.
- [Mixed unsupported inputs confuse users] -> Show all declared inputs and block execution with node-specific compatibility feedback.
- [Temporary JSON and runtime state diverge] -> Use one typed binding set and verify node GUID, expected kind, and accepted value at each binding stage.

## Migration Plan

Implement after the staged execution boundary from `harden-dynamo-graph-execution` is available. Existing graphs require no migration: graphs without `IsInput` continue to run directly, and graphs already prepared for Dynamo Player become eligible for Relay controls according to the supported-node matrix. Rollback removes the pre-run dialog and passes an empty binding set through the hardened runner; source graphs and persisted settings remain unchanged.

## Open Questions

- Confirm the exact runtime members for enumerating and selecting Categories in Dynamo 3.0, 3.6, and 4.0 during adapter implementation.
- Decide whether a later change should add Levels, Views, Element Types, and Revit element selection to the supported matrix.
- Decide whether primitive-only graphs should avoid Dynamo initialization until after the user accepts the dialog, provided lifecycle and diagnostics remain consistent.
