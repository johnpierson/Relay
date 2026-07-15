using System.Reflection;
using Autodesk.Revit.UI;
using Dynamo.Applications;

namespace Relay.Execution;

internal sealed class ReflectionDynamoRunner : IDynamoRunner
{
    private const string DynamoAssemblyPrefix = "DynamoRevitDS";
    private readonly UIApplication application;
    private readonly string adapterName;
    private readonly Func<IEnumerable<Assembly>> assemblyProvider;
    private AdapterMembers members;

    internal ReflectionDynamoRunner(UIApplication application)
        : this(application, GetAdapterName(), () => AppDomain.CurrentDomain.GetAssemblies())
    {
    }

    internal ReflectionDynamoRunner(UIApplication application, string adapterName, Func<IEnumerable<Assembly>> assemblyProvider)
    {
        this.application = application ?? throw new ArgumentNullException(nameof(application));
        this.adapterName = adapterName;
        this.assemblyProvider = assemblyProvider ?? throw new ArgumentNullException(nameof(assemblyProvider));
    }

    public DynamoCompatibilityResult Validate()
    {
        Assembly assembly = assemblyProvider().FirstOrDefault(candidate =>
            candidate.GetName().Name?.StartsWith(DynamoAssemblyPrefix, StringComparison.Ordinal) == true);
        if (assembly is null) return Incompatible("assembly", $"No loaded {DynamoAssemblyPrefix} assembly was found.");

        Type applicationType = assembly.GetType("Dynamo.Applications.DynamoRevit", false);
        Type commandDataType = assembly.GetType("Dynamo.Applications.DynamoRevitCommandData", false);
        if (applicationType is null) return Incompatible("type", "Dynamo.Applications.DynamoRevit is missing.");
        if (commandDataType is null) return Incompatible("type", "Dynamo.Applications.DynamoRevitCommandData is missing.");

        PropertyInfo applicationProperty = commandDataType.GetProperty("Application");
        PropertyInfo journalProperty = commandDataType.GetProperty("JournalData");
        MethodInfo executeMethod = applicationType.GetMethod("ExecuteCommand", new[] { commandDataType });
        PropertyInfo modelProperty = applicationType.GetProperty("RevitDynamoModel");
        if (applicationProperty?.CanWrite != true) return Incompatible("command data", "Application property is missing or read-only.");
        if (journalProperty?.CanWrite != true) return Incompatible("command data", "JournalData property is missing or read-only.");
        if (executeMethod is null) return Incompatible("load", "ExecuteCommand(DynamoRevitCommandData) is missing.");
        if (modelProperty?.CanRead != true) return Incompatible("model", "RevitDynamoModel property is missing or unreadable.");

        members = new AdapterMembers(applicationType, commandDataType, applicationProperty, journalProperty, executeMethod, modelProperty);
        return DynamoCompatibilityResult.Compatible();
    }

    public IDynamoExecutionSession LoadPaused(string graphPath, bool shutdownExistingModel)
    {
        if (members is null) throw new InvalidOperationException($"The {adapterName} adapter was not validated.");
        object dynamo = Activator.CreateInstance(members.ApplicationType)
            ?? throw new InvalidOperationException($"The {adapterName} adapter could not create DynamoRevit.");
        object commandData = Activator.CreateInstance(members.CommandDataType)
            ?? throw new InvalidOperationException($"The {adapterName} adapter could not create DynamoRevitCommandData.");
        var journal = new Dictionary<string, string>
        {
            [JournalKeys.ShowUiKey] = false.ToString(),
            [JournalKeys.AutomationModeKey] = true.ToString(),
            ["dynPath"] = graphPath,
            [JournalKeys.ForceManualRunKey] = true.ToString(),
            [JournalKeys.ModelShutDownKey] = shutdownExistingModel.ToString()
        };
        members.ApplicationProperty.SetValue(commandData, application);
        members.JournalProperty.SetValue(commandData, journal);
        members.ExecuteMethod.Invoke(dynamo, new[] { commandData });
        object model = members.ModelProperty.GetValue(dynamo)
            ?? throw new InvalidOperationException($"The {adapterName} adapter returned no RevitDynamoModel after paused load.");
        return new ReflectionDynamoExecutionSession(model, adapterName);
    }

    private DynamoCompatibilityResult Incompatible(string boundary, string detail) =>
        DynamoCompatibilityResult.Incompatible($"Dynamo compatibility failure in the {adapterName} {boundary} boundary: {detail}");

    private static string GetAdapterName()
    {
#if R27
        return "Revit 2027 / Dynamo 4.0";
#elif R26
        return "Revit 2026 / Dynamo 3.6";
#else
        return "Revit 2025 / Dynamo 3.0";
#endif
    }

    private sealed record AdapterMembers(
        Type ApplicationType,
        Type CommandDataType,
        PropertyInfo ApplicationProperty,
        PropertyInfo JournalProperty,
        MethodInfo ExecuteMethod,
        PropertyInfo ModelProperty);
}

internal sealed class ReflectionDynamoExecutionSession : IDynamoExecutionSession
{
    private readonly object model;
    private readonly string adapterName;
    private bool evaluated;
    private bool bindingFailed;
    private bool disposed;

    internal ReflectionDynamoExecutionSession(object model, string adapterName)
    {
        this.model = model;
        this.adapterName = adapterName;
    }

    public string CleanupFailure { get; private set; }

    public DynamoExecutionOutcome ApplyBindings(IReadOnlyCollection<DynamoNodeBinding> bindings)
    {
        if (disposed) return BindingFailure("The execution session is already disposed.");
        if (bindings.Count == 0) return DynamoExecutionOutcome.Success();

        object workspace = model.GetType().GetProperty("CurrentWorkspace")?.GetValue(model);
        object nodesValue = workspace?.GetType().GetProperty("Nodes")?.GetValue(workspace);
        if (nodesValue is not System.Collections.IEnumerable nodes) return BindingFailure("CurrentWorkspace.Nodes is unavailable.");

        var nodesById = new Dictionary<Guid, object>();
        foreach (object node in nodes)
        {
            object idValue = node?.GetType().GetProperty("GUID")?.GetValue(node);
            if (idValue is Guid id) nodesById[id] = node;
        }

        foreach (DynamoNodeBinding binding in bindings)
        {
            if (!nodesById.TryGetValue(binding.NodeId, out object node))
                return BindingFailure($"Node '{binding.NodeId}' was not found by the {adapterName} adapter.");
            string actualType = node.GetType().FullName;
            if (!string.Equals(actualType, binding.ExpectedRuntimeType, StringComparison.Ordinal))
                return BindingFailure($"Node '{binding.NodeId}' expected '{binding.ExpectedRuntimeType}' but was '{actualType}'.");

            PropertyInfo valueProperty = node.GetType().GetProperty("Value");
            if (valueProperty?.CanWrite != true)
                return BindingFailure($"Node '{binding.NodeId}' has no compatible writable Value member in the {adapterName} adapter.");
            try { valueProperty.SetValue(node, binding.Value); }
            catch (Exception exception) { return BindingFailure($"Node '{binding.NodeId}' binding failed: {exception.GetBaseException().Message}"); }
        }

        return DynamoExecutionOutcome.Success();
    }

    public DynamoExecutionOutcome EvaluateOnce()
    {
        if (bindingFailed) return DynamoExecutionOutcome.Failure(DynamoExecutionStage.Invocation, "Evaluation is blocked after a binding failure.");
        if (evaluated) return DynamoExecutionOutcome.Failure(DynamoExecutionStage.Invocation, "This graph session has already been evaluated.");
        if (disposed) return DynamoExecutionOutcome.Failure(DynamoExecutionStage.Invocation, "The execution session is disposed.");
        MethodInfo forceRun = model.GetType().GetMethod("ForceRun", Type.EmptyTypes);
        if (forceRun is null) return DynamoExecutionOutcome.Failure(DynamoExecutionStage.Compatibility, $"ForceRun() is missing in the {adapterName} adapter.");
        try
        {
            forceRun.Invoke(model, null);
            evaluated = true;
            return DynamoExecutionOutcome.Success();
        }
        catch (Exception exception)
        {
            return DynamoExecutionOutcome.Failure(DynamoExecutionStage.Invocation, $"Dynamo invocation failed in the {adapterName} adapter: {exception.GetBaseException().Message}");
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (model is not IDisposable disposable) return;
        try { disposable.Dispose(); }
        catch (Exception exception) { CleanupFailure = $"Dynamo session cleanup failed in the {adapterName} adapter: {exception.Message}"; }
    }

    private DynamoExecutionOutcome BindingFailure(string diagnostic)
    {
        bindingFailed = true;
        return DynamoExecutionOutcome.Failure(DynamoExecutionStage.Binding, diagnostic);
    }
}
