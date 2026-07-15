# Verification

## Automated

- `dotnet test tests/Relay.Tests/Relay.Tests.csproj --no-restore`: 23 passed, 0 failed.
- `Debug R25`: built against DynamoVisualProgramming.Revit 3.0.0.3416.
- `Debug R26`: built against DynamoVisualProgramming.Revit 3.6.1.2665.
- `Debug R27`: built against DynamoVisualProgramming.Revit 27.0.0.3904 (configured Dynamo 4.0 target).
- `openspec validate harden-dynamo-graph-execution --strict --json`: passed with no issues.

## Revit Host Matrix

Host verification was not run in this implementation session because no Revit 2025, 2026, or 2027 process was available. Tasks 2.4, 3.3, and 4.3 remain open until the following behavior is exercised in each supported host:

- Successful direct execution loads paused and evaluates exactly once.
- Invalid JSON and unavailable or incompatible Dynamo fail with actionable diagnostics.
- Missing nodes, runtime identity mismatches, and binding failures prevent evaluation.
- Cancellation disposes the staged session and temporary graph without advancing document lifecycle state.
- Same-document runs reuse the model policy and cross-document runs request a safe transition.

### Revit 2027 finding

Initial host testing showed that disposing `RevitDynamoModel` as if it were session-owned left Dynamo's shared process model shut down; subsequent Dynamo launches reported `DynamoModel.ShutDown called twice`. Relay now disposes only its execution-session state and leaves the process-owned model lifecycle to Dynamo/Revit.

The same host test also exposed that `dynAutomation=true` is not a paused-load mode in Dynamo 4.0: it starts Dynamo in test mode with synchronous processing and executes the workspace during load. The staged adapter now uses normal UI-less mode, explicitly sets `dynPathExecute=false`, forces manual workspace load, and invokes `ForceRun()` exactly once after binding acceptance.
