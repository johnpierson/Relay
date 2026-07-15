# Verification

## Automated

- `dotnet test tests/Relay.Tests/Relay.Tests.csproj --no-restore`: 31 passed, 0 failed.
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

Normal asynchronous `dynPathExecute` calls did create a `Dynamo Script` transaction, but host logs never recorded evaluation completion before the UI-less model was replaced. A subsequent attempt loaded the original graph unchanged and paused, temporarily switched the public Dynamo scheduler to synchronous processing, and invoked `ForceRun()` inside Relay's active Revit API context. That avoided test mode but exposed the zero-evaluation behavior described below. Supplied-binding sessions retain paused load and explicit evaluation; their temporary copies preserve the source run mode.

The first synchronous-scheduler host run logged `Beginning engine reset` and `Reset complete` but still produced no observable output, while the same AppData graph ran successfully in full UI. Its diagnostic reported `EvaluationCount=0` with `RunEnabled=True`, proving that Dynamo loaded the graph but `ForceRun()` did not create an executable update task. Inspection of the installed Dynamo 3.0, 3.6, and 4.0 binaries confirmed that `ForceRun()` resets the engine and marks nodes without forced execution. A follow-up that force-marked nodes without resetting the UI-less engine also produced `EvaluationCount=0` with every run gate open. Relay now resets and attaches the engine with `markNodesAsDirty=false`, marks every node with `forceExecute=true`, invokes workspace `Run()` exactly once under synchronous scheduler processing, requires `EvaluationCount` to advance before reporting success, and logs engine attachment, run gates, pending evaluation, and aggregate node states. A skipped evaluation is returned as a concrete invocation failure rather than a success-shaped no-op.
