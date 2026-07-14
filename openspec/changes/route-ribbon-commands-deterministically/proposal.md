## Why

Graph execution currently depends on a global path populated by an Autodesk UI activation event that parses brackets from an item's description and silently ignores failures. A missed or unrelated event can run a stale graph or fail without telling the user which mapping was invalid.

## Problem

The selected graph is implicit shared state rather than a deterministic mapping from a Relay ribbon control to a graph path.

## Scope

- Assign each graph button a stable Relay identity and explicit graph-path mapping.
- Resolve the clicked Relay control without parsing presentation text.
- Clear or reject stale selections and surface actionable execution errors.
- Support duplicate graph filenames in different folders without identity collisions.

## Non-goals

- Changing Dynamo graph content or execution semantics.
- Adding dynamic graph input UI.
- Routing non-Relay Autodesk ribbon controls.

## What Changes

- Add a typed button-to-graph registry keyed by stable control identity.
- Make activation handling validate Relay ownership and mapping existence.
- Ensure the external command consumes only a path associated with the current activation.
- Add tests for identity generation, duplicate filenames, missing mappings, and stale state.

## Capabilities

### New Capabilities

- `graph-command-routing`: Defines deterministic routing from a Relay ribbon action to one graph.

### Modified Capabilities

None.

## Risks

- Autodesk and Revit ribbon wrappers expose different identifiers and metadata.
- Existing controls created earlier in a Revit session cannot be recreated in place.

## Compatibility

The mapping must work in Revit 2025, 2026, and 2027. It has no Dynamo version dependency.

## Impact

Affects `src/App.cs`, `src/Command.cs`, `src/Utilities/RibbonUtils.cs`, `src/Utilities/Globals.cs`, and command-routing tests.
