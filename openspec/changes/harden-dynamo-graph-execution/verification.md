# Verification

## Automated

- `dotnet test tests/Relay.Tests/Relay.Tests.csproj`: 63 passed, 0 failed (31 at the time this change was first written; the remainder cover the dynamic input UI and the explicit graph open).
- `Debug R25`: built against DynamoVisualProgramming.Revit 3.0.0.3416.
- `Debug R26`: built against DynamoVisualProgramming.Revit 3.6.1.2665.
- `Debug R27`: built against DynamoVisualProgramming.Revit 27.0.0.3904 (configured Dynamo 4.0 target).
- `Release R25`, `Release R26`, and `Release R27`: built successfully after the direct-execution correction.
- `openspec validate harden-dynamo-graph-execution --strict --json`: passed with no issues.

## Revit Host Matrix

Revit 2027 / Dynamo 4.0 is confirmed working (see "Revit 2027 confirmation" below). Revit 2025 and 2026 have not been exercised, so tasks 2.4, 3.3, and 4.3 remain open for those hosts until the following behavior is confirmed in each:

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

The combined engine-attachment and forced-node path successfully evaluated a simple `Hello There` graph in Revit 2027, proving the adapter and synchronous scheduler path. The upgraded `Place All WallTypes` graph remained graph-specific: Dynamo returned from `UpdateGraphAsyncTask.Initialize` without scheduling evaluation. That graph contains 14 nodes and one saved wall-node trace binding. Because Dynamo 4.0 catches all exceptions inside update-task initialization without logging them, Relay now captures Dynamo/ProtoCore first-chance exceptions only during the synchronous evaluation boundary and reports node modified, forced, error, frozen, and transient counts when no task is created.

### Revit 2027 empty-workspace finding

A later host run reported `EvaluationCount` 0 with `Nodes=0` while every run gate was open (`RunEnabled=True`, `ForceBlockRun=False`, `EngineController=available`) and no first-chance exception was observed. The zero node count identified this as a load failure rather than an evaluation failure: the session's Dynamo log recorded model startup, extension loading, and shutdown without any workspace-open entry.

Relay had supplied the graph in the `dynPath` journal key and relied on `DynamoRevit.ExecuteCommand` to open it. That key is only honored on some branches of that method, and the shipped `DynamoRevitDS.dll` 27.0.0.3904 does not contain the `StartedUIless` state name that current upstream sources branch on, so the branch taken is not something Relay can depend on. The evaluator now calls `DynamoModel.OpenFileFromPath` itself, inside the synchronous scheduler window — under the asynchronous scheduler the open is queued for Revit's Idling loop, which does not run while the external command holds the UI thread. The overload accepting the force-manual-execution flag is preferred so the graph loads paused and Relay retains control of the single `Run()`. `dynPath` is still supplied because DynamoRevit indexes it directly when it does take its open branch.

Opening explicitly also corrects model reuse: with the process-owned model retained across commands, running graph B after graph A would otherwise have re-evaluated the workspace already loaded. An empty workspace is now reported as a `Load`-stage failure naming the graph and Dynamo trusted locations.

### Revit 2027 confirmation

With the explicit open in place, Revit 2027 / Dynamo 4.0 was confirmed end to end: graphs without declared inputs execute and modify the model, graphs with declared inputs collect values through the generated dialog and execute with them applied, and every supported input kind bound correctly. Opening Dynamo manually after a Relay run shows a normal splash screen — the `EBWebView`-under-Program-Files failure and the `DynamoModel.ShutDown called twice` exception are both resolved.
