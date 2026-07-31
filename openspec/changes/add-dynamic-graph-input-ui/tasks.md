## Implementation note

The shipped implementation applies accepted values by rewriting a temporary copy of the
`.dyn` and running it through the staged execution pipeline with an empty runtime-binding
set. The per-adapter supported-node registry and runtime `DynamoNodeBinding` path described
in `design.md` are not built; node kinds are recognised from serialized `ConcreteType` and
the top-level `Inputs[]` `Type`/`Type2` fields, which behave the same across Dynamo 3.0,
3.6, and 4.0. Items below reflect what exists, not what was originally scoped.

## 1. Input Domain and Discovery

- [x] 1.1 Input descriptors, typed dropdown/selection values, and a typed apply result (`AppliedInputGraph`) — *no per-adapter supported-node registry; detection is version-neutral*
- [x] 1.2 Pure `.dyn` JSON discovery of input nodes, ids, labels, descriptions, current values, slider bounds, path metadata, and concrete node identities — *inputs are declared by `IsSetAsInput` in `View.NodeViews`, which is where Dynamo actually stores the flag*
- [x] 1.3 Temporary-graph binding for string, number, boolean, slider, file path, directory path, dropdown, and selection nodes, keyed by node id with the write shape chosen from the concrete type
- [x] 1.4 Tests for supported, unsupported, malformed, duplicate node views, heuristic detection, range, path, source-immutability, apply-failure, and write-fallback cases

## 2. Generated Input UI

- [x] 2.1 Modal generated dialog covering every supported input kind — *controls are built directly rather than through separate view models*
- [x] 2.2 Per-control validation, blocking invalid values, explicit unsupported-input feedback, and cancellation
- [x] 2.3 `Relay.Run` bypasses the dialog for graphs with no declared inputs and collects values otherwise, with no global pending-input state
- [ ] 2.4 Dialog logic is not unit tested — it is a WPF `Window` and cannot be compiled into the test project. Only the number parsing was extracted (`Relay.Utilities.NumericInput`) and covered. Extracting view models would close this.

## 3. Revit Categories Integration

- [x] 3.1 Categories bind by `BuiltInCategory` enum name (e.g. `OST_Walls`), never by localized display text or dropdown index — *one version-neutral implementation rather than an adapter-owned boundary*
- [x] 3.2 Revit 2025 / Dynamo 3.0 — same implementation; builds, not host-tested
- [x] 3.3 Revit 2026 / Dynamo 3.6 — same implementation; builds, not host-tested
- [x] 3.4 Revit 2027 / Dynamo 4.0 — host-confirmed
- [ ] 3.5 `RevitDropdownItems` requires a live `Document` and has no test coverage

## 4. Staged Execution and Lifecycle

- [x] 4.1 Input discovery and accepted values feed the staged execution pipeline from `harden-dynamo-graph-execution`
- [x] 4.2 The graph is opened paused and evaluated exactly once, gated on `EvaluationCount` advancing
- [x] 4.3 Temporary graph copies are deleted on cancellation, apply failure, invocation failure, and success; the scheduler mode is restored on every path
- [x] 4.4 Cancelled or failed runs do not advance the last-successful-document marker

## 5. Host and Build Validation

- [x] 5.1 Revit 2027: no-input, input-driven (including Categories and element selection), and cancellation confirmed on the host — **Revit 2025 and 2026 not exercised**
- [x] 5.2 Supported node identities and serialized fields documented in `DynamoInputParser`; runtime members and host findings recorded in `harden-dynamo-graph-execution/verification.md`
- [x] 5.3 Focused discovery, write-back, number-parsing, execution-coordinator, evaluator, and cleanup tests run (63 passing)
- [x] 5.4 `Debug R25`, `Debug R26`, and `Debug R27` build clean
- [x] 5.5 Strict OpenSpec validation run; host results recorded in `verification.md`
