using System.Reflection;
using System.Collections;

namespace Relay.Execution;

internal static class SynchronousDynamoEvaluator
{
    /// <summary>
    /// Opens <paramref name="graphPath"/> and evaluates it exactly once, with Dynamo's
    /// scheduler held in synchronous mode so the whole sequence completes inside the
    /// calling Revit API context.
    /// </summary>
    /// <remarks>
    /// The graph is opened here rather than through DynamoRevit's <c>dynPath</c> journal
    /// key. That key is only honored on some of <c>ExecuteCommand</c>'s internal branches,
    /// and on Revit 2027 it left the model sitting on an empty workspace — evaluation then
    /// "succeeded" against zero nodes. Opening explicitly also guarantees that a model
    /// reused from an earlier run evaluates the graph the user just clicked rather than
    /// the previous one.
    /// </remarks>
    internal static DynamoExecutionOutcome Evaluate(
        object model,
        string adapterName,
        string graphPath = null,
        IReadOnlyCollection<Relay.Classes.DynamoOutputDefinition> declaredOutputs = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        object scheduler = model.GetType().GetProperty("Scheduler")?.GetValue(model);
        if (scheduler is null)
            return CompatibilityFailure(adapterName, "DynamoModel.Scheduler is unavailable.");

        PropertyInfo processModeProperty = scheduler.GetType().GetProperty("ProcessMode");
        if (processModeProperty?.CanRead != true || processModeProperty.CanWrite != true)
            return CompatibilityFailure(adapterName, "DynamoScheduler.ProcessMode is missing or not writable.");

        MethodInfo resetEngine = model.GetType().GetMethod("ResetEngine", new[] { typeof(bool) });
        if (resetEngine is null)
            return CompatibilityFailure(adapterName, "DynamoModel.ResetEngine(bool) is missing.");

        MethodInfo openFile = null;
        if (graphPath is not null)
        {
            openFile = FindOpenFileMethod(model);
            if (openFile is null)
                return CompatibilityFailure(adapterName, "DynamoModel.OpenFileFromPath(string, ...) is missing.");
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

        // The open has to happen inside the synchronous window too: under the asynchronous
        // scheduler its work is queued for Revit's Idling loop, which never runs while this
        // command holds the UI thread.
        object workspace;
        try
        {
            processModeProperty.SetValue(scheduler, synchronousMode);
            if (openFile is not null)
                openFile.Invoke(model, BuildOpenFileArguments(openFile, graphPath));
            workspace = model.GetType().GetProperty("CurrentWorkspace")?.GetValue(model);
        }
        catch (Exception exception)
        {
            TryRestore(processModeProperty, scheduler, previousMode);
            return DynamoExecutionOutcome.Failure(
                DynamoExecutionStage.Load,
                $"Dynamo could not open '{graphPath}' in the {adapterName} adapter: {ExceptionDiagnostics.Describe(exception)}");
        }

        if (workspace is null)
        {
            TryRestore(processModeProperty, scheduler, previousMode);
            return CompatibilityFailure(adapterName, "DynamoModel.CurrentWorkspace is unavailable.");
        }

        MethodInfo run = workspace.GetType().GetMethod("Run", Type.EmptyTypes);
        PropertyInfo evaluationCountProperty = workspace.GetType().GetProperty("EvaluationCount");
        object nodesValue = workspace.GetType().GetProperty("Nodes")?.GetValue(workspace);
        if (run is null || evaluationCountProperty?.CanRead != true || nodesValue is not IEnumerable nodes)
        {
            TryRestore(processModeProperty, scheduler, previousMode);
            if (run is null)
                return CompatibilityFailure(adapterName, "HomeWorkspaceModel.Run() is missing.");
            if (evaluationCountProperty?.CanRead != true)
                return CompatibilityFailure(adapterName, "HomeWorkspaceModel.EvaluationCount is missing or unreadable.");
            return CompatibilityFailure(adapterName, "WorkspaceModel.Nodes is unavailable.");
        }

        long evaluationCountBefore = Convert.ToInt64(evaluationCountProperty.GetValue(workspace));
        object[] nodeList = nodes.Cast<object>().Where(node => node is not null).ToArray();

        if (nodeList.Length == 0)
        {
            TryRestore(processModeProperty, scheduler, previousMode);
            return DynamoExecutionOutcome.Failure(
                DynamoExecutionStage.Load,
                graphPath is null
                    ? $"The Dynamo workspace is empty in the {adapterName} adapter, so there is nothing to evaluate."
                    : $"Dynamo opened '{graphPath}' in the {adapterName} adapter but the workspace contains no nodes. "
                      + "The graph did not load — check that the file is a valid Dynamo graph and that its folder is a Dynamo trusted location.");
        }

        var markMethods = new MethodInfo[nodeList.Length];
        for (int index = 0; index < nodeList.Length; index++)
        {
            markMethods[index] = nodeList[index].GetType().GetMethod("MarkNodeAsModified", new[] { typeof(bool) });
            if (markMethods[index] is null)
            {
                TryRestore(processModeProperty, scheduler, previousMode);
                return CompatibilityFailure(adapterName, $"Node '{nodeList[index].GetType().FullName}' has no MarkNodeAsModified(bool) method.");
            }
        }

        Exception invocationFailure = null;
        Exception restorationFailure = null;
        var evaluationExceptions = new List<string>();
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> firstChanceHandler = (_, args) =>
        {
            Type declaringType = args.Exception.TargetSite?.DeclaringType;
            string assemblyName = declaringType?.Assembly.GetName().Name ?? string.Empty;
            if (!assemblyName.StartsWith("Dynamo", StringComparison.Ordinal)
                && !assemblyName.StartsWith("ProtoCore", StringComparison.Ordinal)
                && !assemblyName.StartsWith("DSRevit", StringComparison.Ordinal))
            {
                return;
            }

            string detail = $"{args.Exception.GetType().FullName} in {declaringType?.FullName ?? assemblyName}: {args.Exception.Message}";
            lock (evaluationExceptions)
            {
                if (evaluationExceptions.Count < 8 && !evaluationExceptions.Contains(detail, StringComparer.Ordinal))
                    evaluationExceptions.Add(detail);
            }
        };
        try
        {
            AppDomain.CurrentDomain.FirstChanceException += firstChanceHandler;
            resetEngine.Invoke(model, new object[] { false });
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
            AppDomain.CurrentDomain.FirstChanceException -= firstChanceHandler;
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
            string nodeDiagnostics = DescribeNodeExecutionState(workspace);
            string exceptionDiagnostics = evaluationExceptions.Count == 0
                ? "No Dynamo/ProtoCore first-chance exception was observed."
                : "Dynamo evaluation exceptions: " + string.Join(" | ", evaluationExceptions);
            return DynamoExecutionOutcome.Failure(
                DynamoExecutionStage.Invocation,
                $"Dynamo loaded the graph but did not evaluate it in the {adapterName} adapter. EvaluationCount remained {evaluationCountAfter}; {runState}; {nodeDiagnostics}. {exceptionDiagnostics}");
        }

        object hasRunWithoutCrash = workspace.GetType().GetProperty("HasRunWithoutCrash")?.GetValue(workspace);
        if (hasRunWithoutCrash is bool completedWithoutCrash && !completedWithoutCrash)
        {
            return DynamoExecutionOutcome.Failure(
                DynamoExecutionStage.Invocation,
                $"Dynamo evaluated the graph in the {adapterName} adapter, but the workspace reported a crash. {DescribeNodeStates(workspace)}");
        }

        // Read declared outputs from the evaluated nodes. A failure here must not fail the
        // run: the graph has already executed and its model changes are committed.
        IReadOnlyList<Relay.Classes.GraphOutputValue> outputs;
        try
        {
            outputs = DynamoOutputReader.Read(nodeList, declaredOutputs);
        }
        catch (Exception exception)
        {
            outputs = Array.Empty<Relay.Classes.GraphOutputValue>();
            System.Diagnostics.Trace.WriteLine(
                $"[Relay] Could not read graph outputs: {ExceptionDiagnostics.Describe(exception)}");
        }

        return DynamoExecutionOutcome.Success(outputs);
    }

    /// <summary>
    /// Finds <c>DynamoModel.OpenFileFromPath</c>, preferring the overload that also takes
    /// the force-manual-execution flag so the graph loads paused instead of auto-running.
    /// </summary>
    private static MethodInfo FindOpenFileMethod(object model) =>
        model.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(candidate => candidate.Name == "OpenFileFromPath")
            .Where(candidate => candidate.GetParameters() is { Length: > 0 } parameters
                                && parameters[0].ParameterType == typeof(string)
                                && parameters.Skip(1).All(parameter => parameter.ParameterType == typeof(bool)))
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault();

    /// <summary>
    /// The graph must load paused: Relay runs it once itself, after attaching the engine
    /// and marking the nodes.
    /// </summary>
    private static object[] BuildOpenFileArguments(MethodInfo openFile, string graphPath)
    {
        ParameterInfo[] parameters = openFile.GetParameters();
        var arguments = new object[parameters.Length];
        arguments[0] = graphPath;
        for (int index = 1; index < parameters.Length; index++)
        {
            // forceManualExecutionMode is the first flag on every known overload.
            arguments[index] = index == 1
                ? true
                : parameters[index].HasDefaultValue && parameters[index].DefaultValue is bool fallback && fallback;
        }
        return arguments;
    }

    private static void TryRestore(PropertyInfo processModeProperty, object scheduler, object previousMode)
    {
        try
        {
            processModeProperty.SetValue(scheduler, previousMode);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[Relay] Could not restore the Dynamo scheduler process mode: {ExceptionDiagnostics.Describe(exception)}");
        }
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
        object engineController = workspace.GetType().GetProperty("EngineController")?.GetValue(workspace);
        object evaluationPending = workspace.GetType().GetProperty(
            "IsEvaluationPending",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(workspace);
        return $"RunEnabled={runEnabled ?? "unavailable"}, ForceBlockRun={forceBlockRun ?? "unavailable"}, GraphRunInProgress={graphRunInProgress ?? "unavailable"}, EngineController={(engineController is null ? "unavailable" : "available")}, IsEvaluationPending={evaluationPending ?? "unavailable"}";
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

    private static string DescribeNodeExecutionState(object workspace)
    {
        object nodesValue = workspace.GetType().GetProperty("Nodes")?.GetValue(workspace);
        if (nodesValue is not IEnumerable nodes) return "Node execution state unavailable";

        object[] nodeList = nodes.Cast<object>().Where(node => node is not null).ToArray();
        int modified = nodeList.Count(node => ReadBoolean(node, "IsModified"));
        int forceExecution = nodeList.Count(node => ReadBoolean(node, "NeedsForceExecution"));
        int inError = nodeList.Count(node => ReadBoolean(node, "IsInErrorState"));
        int frozen = nodeList.Count(node => ReadBoolean(node, "IsFrozen"));
        int transient = nodeList.Count(node => ReadBoolean(node, "IsTransient"));
        string[] errorTypes = nodeList
            .Where(node => ReadBoolean(node, "IsInErrorState"))
            .Select(node => node.GetType().FullName ?? node.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .Take(4)
            .ToArray();
        string errors = errorTypes.Length == 0 ? "none" : string.Join(", ", errorTypes);
        return $"Nodes={nodeList.Length}, Modified={modified}, ForceExecution={forceExecution}, InError={inError}, Frozen={frozen}, Transient={transient}, ErrorTypes={errors}";
    }

    private static bool ReadBoolean(object target, string propertyName) =>
        target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target) is true;

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
