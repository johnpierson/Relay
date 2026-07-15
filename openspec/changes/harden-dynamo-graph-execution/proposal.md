## Why

Relay locates DynamoRevit through unchecked reflection, rewrites graph JSON with string replacement, and lets initialization or invocation failures escape without useful context. These assumptions are brittle across the supported Revit and Dynamo versions and can leave users with a failed command and no actionable diagnosis.

## Problem

Graph preparation, Dynamo discovery, reflective API binding, model lifecycle policy, invocation, result handling, and cleanup are combined in one host-bound method with few validated boundaries.

## Scope

- Preserve the selected graph byte-for-byte for direct runs and prepare binding copies structurally without changing their run mode.
- Validate DynamoRevit assembly, types, properties, and methods before invocation.
- Provide a staged execution session that can load a graph without evaluation, accept validated typed bindings, and evaluate only after binding succeeds.
- Define Dynamo model reuse and shutdown behavior across active Revit documents.
- Convert preparation and invocation failures into explicit Relay command results and diagnostics.
- Clean up every temporary graph file created for staged binding.

## Non-goals

- Replacing DynamoRevit's supported journal-command execution mechanism.
- Managing third-party Dynamo package installation.
- Implementing dynamic graph input discovery or UI; this change provides only the staged execution and binding seam they require.

## What Changes

- Extract graph preparation and reflective binding behind testable components.
- Use DynamoRevit's supported one-shot UI-less command for empty bindings and validated prepare, load, bind, and evaluate stages when bindings are supplied.
- Fail clearly when DynamoRevit is unavailable or incompatible.
- Track document/model lifecycle only after a successful execution transition.
- Add version-focused tests and host verification for Revit 2025-2027.

## Capabilities

### New Capabilities

- `dynamo-graph-execution`: Defines safe graph preparation, Dynamo invocation, lifecycle handling, cleanup, and failure reporting.

### Modified Capabilities

None.

## Risks

- DynamoRevit's internal/reflected execution and input-binding surfaces can differ by bundled Dynamo release.
- Structural JSON writes must preserve graph fields Relay does not understand.

## Compatibility

Must cover Dynamo 3.0 with Revit 2025, Dynamo 3.6 with Revit 2026, and the configured Dynamo/Revit 2027 integration. Version-specific adapters may be required.

## Impact

Affects `src/Methods/DynamoMethods.cs`, `src/Utilities/DynamoUtils.cs`, `src/Command.cs`, staged execution and binding contracts, JSON handling, diagnostics, and execution tests. The later `add-dynamic-graph-input-ui` change consumes these contracts.
