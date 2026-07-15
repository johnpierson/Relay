## 1. Graph Preparation

- [x] 1.1 Replace text substitution in `DynamoUtils.SetToAutomatic` with JSON-aware temporary graph preparation that preserves run mode
- [x] 1.2 Add or reuse a Relay test project and verify run-mode preservation, semantic field preservation, invalid JSON, source immutability, and cleanup
- [x] 1.3 Add a disposable temporary-graph owner that reports cleanup failures separately

## 2. Dynamo Adapter

- [x] 2.1 Define typed Dynamo runner, disposable execution session, version-neutral node-GUID binding, adapter validation, and execution outcome boundaries
- [x] 2.2 Implement and validate direct empty-binding execution plus paused staged execution in the Revit 2025/Dynamo 3.0 adapter
- [x] 2.3 Implement and validate direct empty-binding execution plus paused staged execution in the Revit 2026/Dynamo 3.6 adapter
- [ ] 2.4 Confirm the Revit 2027 staged execution and runtime binding surface and implement its isolated adapter
- [x] 2.5 Validate expected runtime node identity before applying a supplied binding and prevent evaluation after any binding failure
- [x] 2.6 Map preparation, load, compatibility, binding, invocation, cancellation, and cleanup outcomes to Revit command results and diagnostics

## 3. Lifecycle and Host Verification

- [x] 3.1 Refactor document/model tracking so only successful runs update the weak document reference
- [x] 3.2 Test same-document reuse, document changes, failed or cancelled staged sessions, and missing/incompatible execution or binding members
- [ ] 3.3 Verify successful, invalid-graph, unavailable-Dynamo, paused-load, binding-failure, cancellation, and cross-document runs in Revit 2025, 2026, and 2027

## 4. Validation

- [x] 4.1 Run focused graph-preparation, staged adapter, binding-validation, cancellation, cleanup, and lifecycle tests
- [x] 4.2 Build `src/Relay.sln` with `Debug R25`, `Debug R26`, and `Debug R27`
- [ ] 4.3 Run strict OpenSpec validation and record host compatibility results
