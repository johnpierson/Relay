## Why

Relay runs Dynamo graphs from ribbon buttons without giving users a way to supply values for nodes that graph authors mark as inputs. Users must open Dynamo or maintain duplicate graphs for values that Dynamo Player can request at run time, including document-backed choices such as Categories.

## Problem

Relay treats execution as one opaque operation: select a graph, prepare an automatic temporary copy, and run it. It does not discover `IsInput` nodes, present compatible controls, validate user values, or bind document-dependent selections before evaluation. Static JSON substitution alone is insufficient for Revit dropdown nodes whose choices and runtime values depend on the active host and Dynamo version.

## Scope

- Discover Dynamo nodes marked `IsInput` and map supported nodes to typed Relay input descriptors.
- Generate a Relay-owned WPF dialog for supported primitive, path, slider, and Revit Categories inputs.
- Populate Categories choices from the active Revit/Dynamo execution context and retain a stable category identity independent of display text and list position.
- Bind accepted values to the temporary graph or staged Dynamo workspace before graph evaluation.
- Report unsupported, invalid, stale, and unbindable inputs without modifying the source graph.
- Define cancellation and lifecycle behavior from graph activation through execution cleanup.

## Non-goals

- Reproducing the complete Dynamo Player interface or every input node it supports.
- Supporting arbitrary third-party nodes merely because they are marked `IsInput`.
- Persisting input presets or writing accepted values back to source `.dyn` files.
- Displaying graph outputs or progress in the first release.
- Changing graph discovery, ribbon synchronization, or graph-command routing semantics.

## What Changes

- Add pure graph-input discovery and a typed, version-neutral input model keyed by Dynamo node GUID.
- Add a generated WPF input dialog with validation and explicit unsupported-input feedback.
- Add a Revit Categories choice provider and version-specific Dynamo binding for the selected stable category identity.
- Extend the hardened Dynamo execution seam to load a prepared graph, apply accepted bindings, and only then evaluate it.
- Add focused tests plus Revit 2025-2027 host verification for input discovery, dialog outcomes, binding, cancellation, and cleanup.

## Capabilities

### New Capabilities

- `dynamic-graph-input-ui`: Defines discovery, presentation, validation, and binding of supported Dynamo graph inputs before Relay executes a graph.

### Modified Capabilities

- `dynamo-graph-execution`: Staged execution must permit validated inputs to be applied after graph load and before evaluation.

## Risks

- Dynamo node serialization and runtime input APIs can differ across Dynamo 3.0, 3.6, and 4.0.
- Revit category display names are localized and dropdown ordering is not a stable binding identity.
- Opening a graph to obtain document-backed choices may initialize Dynamo before the user commits to run it.
- A graph can mix supported, unsupported, disconnected, and invalid input nodes in ways that require an explicit run policy.

## Compatibility

Must cover Revit 2025 with Dynamo 3.0, Revit 2026 with Dynamo 3.6, and Revit 2027 with the configured Dynamo 4.0 integration. Relay uses an explicit supported-node matrix per adapter and MUST NOT infer compatibility from display names or reflected type names alone.

## Impact

Affects `src/Command.cs`, new graph-input domain and discovery components, new WPF views/view models, Revit-backed choice providers, `src/Methods/DynamoMethods.cs`, Dynamo version adapters introduced by `harden-dynamo-graph-execution`, diagnostics, and test projects. Users see an input dialog after activating a graph with supported or declared inputs; graphs without declared inputs retain direct execution behavior.
