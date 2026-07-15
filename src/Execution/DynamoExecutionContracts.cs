namespace Relay.Execution;

internal enum DynamoExecutionStage
{
    Preparation,
    Compatibility,
    Load,
    Binding,
    Invocation,
    Cancellation,
    Cleanup
}

internal sealed record DynamoExecutionOutcome(
    bool Succeeded,
    bool Cancelled,
    DynamoExecutionStage Stage,
    string Diagnostic)
{
    internal static DynamoExecutionOutcome Success() => new(true, false, DynamoExecutionStage.Invocation, null);
    internal static DynamoExecutionOutcome Failure(DynamoExecutionStage stage, string diagnostic) => new(false, false, stage, diagnostic);
    internal static DynamoExecutionOutcome Cancel(string diagnostic) => new(false, true, DynamoExecutionStage.Cancellation, diagnostic);
}

internal sealed record DynamoNodeBinding(Guid NodeId, string ExpectedRuntimeType, object Value);

internal interface IDynamoRunner
{
    DynamoCompatibilityResult Validate();
    DynamoExecutionOutcome ExecuteDirect(string graphPath, bool shutdownExistingModel);
    IDynamoExecutionSession LoadPaused(string graphPath, bool shutdownExistingModel);
}

internal sealed record DynamoCompatibilityResult(bool IsCompatible, string Diagnostic)
{
    internal static DynamoCompatibilityResult Compatible() => new(true, null);
    internal static DynamoCompatibilityResult Incompatible(string diagnostic) => new(false, diagnostic);
}

internal interface IDynamoExecutionSession : IDisposable
{
    DynamoExecutionOutcome ApplyBindings(IReadOnlyCollection<DynamoNodeBinding> bindings);
    DynamoExecutionOutcome EvaluateOnce();
    string CleanupFailure { get; }
}

internal sealed class DynamoExecutionCoordinator
{
    internal DynamoExecutionOutcome Execute(
        IDynamoRunner runner,
        string graphPath,
        bool shutdownExistingModel,
        IReadOnlyCollection<DynamoNodeBinding> bindings,
        CancellationToken cancellationToken,
        out string cleanupFailure)
    {
        cleanupFailure = null;
        DynamoCompatibilityResult compatibility = runner.Validate();
        if (!compatibility.IsCompatible)
        {
            return DynamoExecutionOutcome.Failure(DynamoExecutionStage.Compatibility, compatibility.Diagnostic);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return DynamoExecutionOutcome.Cancel("Dynamo execution was cancelled before evaluation.");
        }

        IReadOnlyCollection<DynamoNodeBinding> effectiveBindings = bindings ?? Array.Empty<DynamoNodeBinding>();
        if (effectiveBindings.Count == 0)
        {
            try
            {
                // Empty-binding button runs do not need a rewritten temporary graph or a
                // paused reflection session. Use DynamoRevit's supported UI-less path.
                return runner.ExecuteDirect(graphPath, shutdownExistingModel);
            }
            catch (Exception exception)
            {
                return DynamoExecutionOutcome.Failure(
                    DynamoExecutionStage.Invocation,
                    $"Dynamo UI-less execution failed: {ExceptionDiagnostics.Describe(exception)}");
            }
        }

        IDynamoExecutionSession session;
        try
        {
            session = runner.LoadPaused(graphPath, shutdownExistingModel);
        }
        catch (Exception exception)
        {
            return DynamoExecutionOutcome.Failure(
                DynamoExecutionStage.Load,
                $"Dynamo paused load failed: {ExceptionDiagnostics.Describe(exception)}");
        }

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return DynamoExecutionOutcome.Cancel("Dynamo execution was cancelled before evaluation.");
            }

            DynamoExecutionOutcome bindingOutcome = session.ApplyBindings(effectiveBindings);
            if (!bindingOutcome.Succeeded)
            {
                return bindingOutcome;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return DynamoExecutionOutcome.Cancel("Dynamo execution was cancelled before evaluation.");
            }

            return session.EvaluateOnce();
        }
        finally
        {
            session.Dispose();
            cleanupFailure = session.CleanupFailure;
        }
    }
}

internal static class ExceptionDiagnostics
{
    internal static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception root = exception.GetBaseException();
        return $"{root.GetType().FullName}: {root.Message}";
    }
}

internal sealed class SuccessfulDocumentTracker<TDocument> where TDocument : class
{
    private WeakReference<TDocument> lastSuccessfulDocument;

    internal bool RequiresModelTransition(TDocument currentDocument)
    {
        if (currentDocument is null) return true;
        if (lastSuccessfulDocument is null) return false;
        return !lastSuccessfulDocument.TryGetTarget(out TDocument previous) || !ReferenceEquals(previous, currentDocument);
    }

    internal void RecordSuccess(TDocument currentDocument)
    {
        lastSuccessfulDocument = currentDocument is null ? null : new WeakReference<TDocument>(currentDocument);
    }
}
