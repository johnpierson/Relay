using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Relay.Execution;

namespace Relay.Methods;

internal static class DynamoMethods
{
    private static readonly SuccessfulDocumentTracker<Document> DocumentTracker = new();

    internal static Result RunGraph(UIApplication uiApp, string dynamoJournal, out string diagnostic)
    {
        diagnostic = null;
        TemporaryDynamoGraph temporaryGraph;
        try
        {
            temporaryGraph = TemporaryDynamoGraph.Create(dynamoJournal);
        }
        catch (Exception exception)
        {
            diagnostic = exception.Message;
            return Result.Failed;
        }

        DynamoExecutionOutcome outcome;
        string sessionCleanupFailure;
        try
        {
            Document currentDocument = uiApp.ActiveUIDocument?.Document;
            bool transitionModel = DocumentTracker.RequiresModelTransition(currentDocument);
            var coordinator = new DynamoExecutionCoordinator();
            outcome = coordinator.Execute(
                new ReflectionDynamoRunner(uiApp),
                temporaryGraph.Path,
                transitionModel,
                Array.Empty<DynamoNodeBinding>(),
                CancellationToken.None,
                out sessionCleanupFailure);

            if (outcome.Succeeded) DocumentTracker.RecordSuccess(currentDocument);
        }
        catch (Exception exception)
        {
            outcome = DynamoExecutionOutcome.Failure(DynamoExecutionStage.Invocation, exception.ToString());
            sessionCleanupFailure = null;
        }
        finally
        {
            temporaryGraph.Dispose();
        }

        diagnostic = JoinDiagnostics(outcome.Diagnostic, sessionCleanupFailure, temporaryGraph.CleanupFailure);
        if (!string.IsNullOrWhiteSpace(sessionCleanupFailure)) System.Diagnostics.Trace.WriteLine($"[Relay] {sessionCleanupFailure}");
        if (!string.IsNullOrWhiteSpace(temporaryGraph.CleanupFailure)) System.Diagnostics.Trace.WriteLine($"[Relay] {temporaryGraph.CleanupFailure}");
        if (outcome.Cancelled) return Result.Cancelled;
        return outcome.Succeeded ? Result.Succeeded : Result.Failed;
    }

    private static string JoinDiagnostics(params string[] diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Where(value => !string.IsNullOrWhiteSpace(value)));
}
