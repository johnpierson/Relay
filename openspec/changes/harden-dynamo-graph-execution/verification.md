# Verification

## Automated

- `dotnet test tests/Relay.Tests/Relay.Tests.csproj --no-restore`: 30 passed, 0 failed.
- `Debug R25`: built against DynamoVisualProgramming.Revit 3.0.0.3416.
- `Debug R26`: built against DynamoVisualProgramming.Revit 3.6.1.2665.
- `Debug R27`: built against DynamoVisualProgramming.Revit 27.0.0.3904 (configured Dynamo 4.0 target).
- `Release R25`, `Release R26`, and `Release R27`: built successfully after the direct-execution correction.
- `openspec validate harden-dynamo-graph-execution --strict --json`: passed with no issues.

## Revit Host Matrix

Host verification remains incomplete. Iterative Revit 2027 testing exposed the lifecycle, scheduling, and test-mode findings below; Revit 2025 and 2026 have not been exercised, and the corrected Revit 2027 direct path still requires confirmation. Tasks 2.4, 3.3, and 4.3 remain open until the following behavior is exercised in each supported host:

- Successful direct execution uses normal UI-less mode and produces exactly one evaluation and undo scope.
- Invalid JSON and unavailable or incompatible Dynamo fail with actionable diagnostics.
- Missing nodes, runtime identity mismatches, and binding failures prevent evaluation.
- Cancellation disposes the staged session and temporary graph without advancing document lifecycle state.
- Same-document runs reuse the model policy and cross-document runs request a safe transition.

### Revit 2027 finding

Initial host testing showed that disposing `RevitDynamoModel` as if it were session-owned left Dynamo's shared process model shut down; subsequent Dynamo launches reported `DynamoModel.ShutDown called twice`. Relay now disposes only its execution-session state and leaves the process-owned model lifecycle to Dynamo/Revit.

The same host test exposed that `dynAutomation=true` starts Dynamo in test mode. A synchronous fallback using that mode produced two undo scopes without graph output and left Dynamo's splash WebView attempting to create `EBWebView` data under the protected Revit installation directory. Relay no longer enables automation mode in either execution path.

Normal asynchronous `dynPathExecute` and `ForceRun()` calls did create a `Dynamo Script` transaction, but host logs never recorded evaluation completion before the UI-less model was replaced. Direct empty-binding commands now load the original graph unchanged and paused, temporarily switch the public Dynamo scheduler to synchronous processing, invoke `ForceRun()` once inside Relay's active Revit API context, and restore asynchronous processing. This avoids test mode while completing the evaluation before Relay returns. Supplied-binding sessions retain paused load and explicit evaluation; their temporary copies preserve the source run mode.

The first synchronous-scheduler host run logged `Beginning engine reset` and `Reset complete` but still produced no observable output, while the same AppData graph ran successfully in full UI. Relay now requires `HomeWorkspaceModel.EvaluationCount` to advance before reporting success and logs the run gates plus aggregate node states to Dynamo's logger. A skipped evaluation is returned as a concrete invocation failure rather than a success-shaped no-op.
