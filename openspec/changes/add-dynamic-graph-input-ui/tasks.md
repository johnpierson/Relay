## 1. Input Domain and Discovery

- [ ] 1.1 Add immutable graph-input descriptors, typed values, binding results, dialog outcomes, and an explicit supported-node registry keyed by version adapter
- [ ] 1.2 Implement pure `.dyn` JSON discovery for `IsInput` nodes, node GUIDs, custom labels, current values, slider bounds, path metadata, and concrete node identities
- [ ] 1.3 Implement temporary-graph binding for supported string, number, boolean, slider, file path, and directory path nodes with node identity revalidation
- [ ] 1.4 Add focused tests for supported, unsupported, malformed, duplicate-label, missing-GUID, range, path, source-immutability, and semantic-preservation cases

## 2. Generated Input UI

- [ ] 2.1 Add WPF input view models and a modal generated dialog for the initial supported input kinds
- [ ] 2.2 Add per-control validation, blocking unsupported-input feedback, accepted typed bindings, and explicit cancellation behavior
- [ ] 2.3 Wire `Relay.Run` to bypass the dialog for no-input graphs and request values for graphs with declared inputs without introducing global pending-input state
- [ ] 2.4 Test view-model generation, validation, acceptance, cancellation, mixed supported/unsupported inputs, and duplicate display labels independently of Revit

## 3. Revit Categories Integration

- [ ] 3.1 Define a stable Revit category choice model and adapter-owned enumeration/binding boundary that never uses localized names or indices as durable identity
- [ ] 3.2 Implement Categories enumeration and runtime selection for Revit 2025/Dynamo 3.0
- [ ] 3.3 Implement Categories enumeration and runtime selection for Revit 2026/Dynamo 3.6
- [ ] 3.4 Confirm and implement Categories enumeration and runtime selection for Revit 2027/Dynamo 4.0
- [ ] 3.5 Add adapter tests for choice mapping, stale identities, missing nodes, mismatched concrete types, unavailable members, and binding failure before evaluation

## 4. Staged Execution and Lifecycle

- [ ] 4.1 Integrate input discovery and accepted bindings with the staged execution session introduced by `harden-dynamo-graph-execution`
- [ ] 4.2 Guarantee graph load remains paused until all JSON and runtime bindings succeed, then evaluate exactly once
- [ ] 4.3 Dispose Dynamo session and temporary graph resources on dialog cancellation, validation failure, binding failure, invocation failure, and success
- [ ] 4.4 Verify failed or cancelled input collection does not advance the last-successful-document lifecycle marker

## 5. Host and Build Validation

- [ ] 5.1 Host-test no-input, primitive-only, Categories, mixed unsupported, cancellation, stale-category, and binding-failure graphs in Revit 2025, 2026, and 2027
- [ ] 5.2 Document the supported concrete node identities, serialized fields, and runtime members for each Dynamo adapter
- [ ] 5.3 Run focused input discovery, UI view-model, graph binding, adapter, lifecycle, and cleanup tests
- [ ] 5.4 Build `src/Relay.sln` with `Debug R25`, `Debug R26`, and `Debug R27`
- [ ] 5.5 Run strict OpenSpec validation and record the host compatibility results
