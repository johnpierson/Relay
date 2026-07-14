## 1. Graph Preparation

- [ ] 1.1 Replace text substitution in `DynamoUtils.SetToAutomatic` with JSON-aware temporary graph preparation
- [ ] 1.2 Add or reuse a Relay test project and verify run-mode updates, semantic field preservation, invalid JSON, source immutability, and cleanup
- [ ] 1.3 Add a disposable temporary-graph owner that reports cleanup failures separately

## 2. Dynamo Adapter

- [ ] 2.1 Define typed Dynamo runner, binding validation, and execution outcome boundaries
- [ ] 2.2 Implement and validate the Revit 2025/Dynamo 3.0 adapter
- [ ] 2.3 Implement and validate the Revit 2026/Dynamo 3.6 adapter
- [ ] 2.4 Confirm the Revit 2027 Dynamo surface and implement its isolated adapter
- [ ] 2.5 Map preparation, compatibility, invocation, and cancellation outcomes to Revit command results and diagnostics

## 3. Lifecycle and Host Verification

- [ ] 3.1 Refactor document/model tracking so only successful runs update the weak document reference
- [ ] 3.2 Test same-document reuse, document changes, failed runs, and missing/incompatible reflection members
- [ ] 3.3 Verify successful, invalid-graph, unavailable-Dynamo, and cross-document runs in Revit 2025, 2026, and 2027

## 4. Validation

- [ ] 4.1 Run focused graph-preparation, adapter, and lifecycle tests
- [ ] 4.2 Build `src/Relay.sln` with `Debug R25`, `Debug R26`, and `Debug R27`
- [ ] 4.3 Run strict OpenSpec validation and record host compatibility results
