using System.Reflection;
using System.Collections;

namespace Relay.Execution;

internal static class SynchronousDynamoEvaluator
{
    internal static DynamoExecutionOutcome Evaluate(object model, string adapterName)
    {
        ArgumentNullException.ThrowIfNull(model);
        object scheduler = model.GetType().GetProperty("Scheduler")?.GetValue(model);
        if (scheduler is null)
            return CompatibilityFailure(adapterName, "DynamoModel.Scheduler is unavailable.");

        PropertyInfo processModeProperty = scheduler.GetType().GetProperty("ProcessMode");
        if (processModeProperty?.CanRead != true || processModeProperty.CanWrite != true)
            return CompatibilityFailure(adapterName, "DynamoScheduler.ProcessMode is missing or not writable.");

        object workspace = model.GetType().GetProperty("CurrentWorkspace")?.GetValue(model);
        if (workspace is null)
            return CompatibilityFailure(adapterName, "DynamoModel.CurrentWorkspace is unavailable.");
        MethodInfo run = workspace.GetType().GetMethod("Run", Type.EmptyTypes);
        if (run is null)
            return CompatibilityFailure(adapterName, "HomeWorkspaceModel.Run() is missing.");
        PropertyInfo evaluationCountProperty = workspace.GetType().GetProperty("EvaluationCount");
        if (evaluationCountProperty?.CanRead != true)
            return CompatibilityFailure(adapterName, "HomeWorkspaceModel.EvaluationCount is missing or unreadable.");
        long evaluationCountBefore = Convert.ToInt64(evaluationCountProperty.GetValue(workspace));

        object nodesValue = workspace.GetType().GetProperty("Nodes")?.GetValue(workspace);
        if (nodesValue is not IEnumerable nodes)
            return CompatibilityFailure(adapterName, "WorkspaceModel.Nodes is unavailable.");
        object[] nodeList = nodes.Cast<object>().Where(node => node is not null).ToArray();
        var markMethods = new MethodInfo[nodeList.Length];
        for (int index = 0; index < nodeList.Length; index++)
        {
            markMethods[index] = nodeList[index].GetType().GetMethod("MarkNodeAsModified", new[] { typeof(bool) });
            if (markMethods[index] is null)
                return CompatibilityFailure(adapterName, $"Node '{nodeList[index].GetType().FullName}' has no MarkNodeAsModified(bool) method.");
        }

        object previousMode = processModeProperty.GetValue(scheduler);
        object synchronousMode;
        try
        {
            synchronousMode = Enum.Parse(processModeProperty.PropertyType, "Synchronous");
        }
        catch (Exception exception)
        {
            return CompatibilityFailure(adapterName, $"DynamoScheduler.ProcessMode has no Synchronous value: {ExceptionDiagnostics.Describe(exception)}");
        }

        Exception invocationFailure = null;
        Exception restorationFailure = null;
        try
        {
            processModeProperty.SetValue(scheduler, synchronousMode);
            for (int index = 0; index < nodeList.Length; index++)
                markMethods[index].Invoke(nodeList[index], new object[] { true });
            run.Invoke(workspace, null);
        }
        catch (Exception exception)
        {
            invocationFailure = exception;
        }
        finally
        {
            try
            {
                processModeProperty.SetValue(scheduler, previousMode);
            }
            catch (Exception exception)
            {
                restorationFailure = exception;
            }
        }

        if (invocationFailure is not null)
        {
            string diagnostic = $"Dynamo invocation failed in the {adapterName} adapter: {ExceptionDiagnostics.Describe(invocationFailure)}";
            if (restorationFailure is not null)
                diagnostic += $" Scheduler restoration also failed: {ExceptionDiagnostics.Describe(restorationFailure)}";
            return DynamoExecutionOutcome.Failure(DynamoExecutionStage.Invocation, diagnostic);
        }

        if (restorationFailure is not null)
        {
            return DynamoExecutionOutcome.Failure(
                DynamoExecutionStage.Cleanup,
                $"Dynamo evaluation completed, but scheduler restoration failed in the {adapterName} adapter: {ExceptionDiagnostics.Describe(restorationFailure)}");
        }

        long evaluationCountAfter = Convert.ToInt64(evaluationCountProperty.GetValue(workspace));
        string runState = DescribeRunState(workspace);
        LogToDynamo(model, $"[Relay] EvaluationCount {evaluationCountBefore} -> {evaluationCountAfter}; {runState}; {DescribeNodeStates(workspace)}");
        if (evaluationCountAfter <= evaluationCountBefore)
        {
            return DynamoExecutionOutcome.Failure(
                DynamoExecutionStage.Invocation,
                $"Dynamo loaded the graph but did not evaluate it in the {adapterName} adapter. EvaluationCount remained {evaluationCountAfter}; {runState}.");
        }

        object hasRunWithoutCrash = workspace.GetType().GetProperty("HasRunWithoutCrash")?.GetValue(workspace);
        if (hasRunWithoutCrash is bool completedWithoutCrash && !completedWithoutCrash)
        {
            return DynamoExecutionOutcome.Failure(
                DynamoExecutionStage.Invocation,
                $"Dynamo evaluated the graph in the {adapterName} adapter, but the workspace reported a crash. {DescribeNodeStates(workspace)}");
        }

        return DynamoExecutionOutcome.Success();
    }

    private static string DescribeRunState(object workspace)
    {
        object runSettings = workspace.GetType().GetProperty("RunSettings")?.GetValue(workspace);
        object runEnabled = runSettings?.GetType().GetProperty("RunEnabled")?.GetValue(runSettings);
        object forceBlockRun = runSettings?.GetType().GetProperty(
            "ForceBlockRun",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
        object graphRunInProgress = workspace.GetType().GetProperty(
            "GraphRunInProgress",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(workspace);
        return $"RunEnabled={runEnabled ?? "unavailable"}, ForceBlockRun={forceBlockRun ?? "unavailable"}, GraphRunInProgress={graphRunInProgress ?? "unavailable"}";
    }

    private static string DescribeNodeStates(object workspace)
    {
        object nodesValue = workspace.GetType().GetProperty("Nodes")?.GetValue(workspace);
        if (nodesValue is not IEnumerable nodes) return "Node states unavailable";

        var states = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (object node in nodes)
        {
            string state = node?.GetType().GetProperty("State")?.GetValue(node)?.ToString() ?? "Unknown";
            states[state] = states.TryGetValue(state, out int count) ? count + 1 : 1;
        }
        return states.Count == 0
            ? "Node states empty"
            : "Node states: " + string.Join(", ", states.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static void LogToDynamo(object model, string message)
    {
        try
        {
            object logger = model.GetType().GetProperty("Logger")?.GetValue(model);
            logger?.GetType().GetMethod("Log", new[] { typeof(string) })?.Invoke(logger, new object[] { message });
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine($"[Relay] Could not write Dynamo evaluation diagnostics: {ExceptionDiagnostics.Describe(exception)}");
        }
    }

    private static DynamoExecutionOutcome CompatibilityFailure(string adapterName, string diagnostic) =>
        DynamoExecutionOutcome.Failure(DynamoExecutionStage.Compatibility, $"Dynamo compatibility failure in the {adapterName} scheduler boundary: {diagnostic}");
}
