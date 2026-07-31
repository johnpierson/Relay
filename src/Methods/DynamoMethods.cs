using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Relay.Classes;
using Relay.Execution;
using Relay.UI;
using Relay.Utilities;

namespace Relay.Methods;

internal static class DynamoMethods
{
    private static readonly SuccessfulDocumentTracker<Document> DocumentTracker = new();

    internal static Result RunGraph(UIApplication uiApp, string dynamoJournal, out string diagnostic)
    {
        diagnostic = null;

        // Discover nodes marked IsSetAsInput.  Graphs with no declared inputs skip the
        // dialog entirely and go straight to execution.
        var graphInputs = DynamoInputParser.ParseGraphInputs(dynamoJournal);

        // Populate items for Revit-backed dropdown nodes (e.g. Categories)
        try
        {
            RevitDropdownItems.Populate(graphInputs, uiApp.ActiveUIDocument?.Document);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Relay] Could not populate Revit dropdown items: {ex.Message}");
        }

        string graphToRun = dynamoJournal;
        string modifiedGraphPath = null;

        if (graphInputs.HasInputs)
        {
            var userValues = CollectUserInputs(uiApp, graphInputs);
            if (userValues == null)
                return Result.Cancelled;

            // Apply the accepted values to a disposable copy beside the source graph
            // (same folder preserves Dynamo's folder-based file trust); the source
            // graph is never touched.
            modifiedGraphPath = DynamoInputParser.ApplyUserValues(dynamoJournal, userValues);
            graphToRun = modifiedGraphPath;
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
                graphToRun,
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
            // Clean up the input-bound temporary copy
            if (modifiedGraphPath != null && modifiedGraphPath != dynamoJournal)
            {
                try { File.Delete(modifiedGraphPath); }
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Relay] Failed to delete temporary graph file '{modifiedGraphPath}': {ex.Message}"); }
            }
        }

        diagnostic = JoinDiagnostics(outcome.Diagnostic, sessionCleanupFailure);
        if (!string.IsNullOrWhiteSpace(sessionCleanupFailure)) System.Diagnostics.Trace.WriteLine($"[Relay] {sessionCleanupFailure}");
        if (outcome.Cancelled) return Result.Cancelled;
        return outcome.Succeeded ? Result.Succeeded : Result.Failed;
    }

    /// <summary>
    /// Shows the generated input dialog (re-showing it around Revit element picks as
    /// needed) and returns the accepted values keyed by node ID, or <c>null</c> when
    /// the user cancels.
    /// </summary>
    private static Dictionary<string, object> CollectUserInputs(UIApplication uiApp, DynamoGraphInputs graphInputs)
    {
        var prefilledValues = new Dictionary<string, object>();

        while (true)
        {
            var dialog = new InputDialog(graphInputs, prefilledValues);

            // Attempt to parent the dialog to the Revit main window
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(dialog);
                helper.Owner = uiApp.MainWindowHandle;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Could not set dialog owner: {ex.Message}");
            }

            dialog.ShowDialog();

            // --- User cancelled the dialog ---
            if (dialog.WasCancelled)
                return null;

            // --- User clicked "Select in Revit" ---
            if (dialog.NeedsPick && !string.IsNullOrEmpty(dialog.PickInputId))
            {
                prefilledValues = dialog.PartialValues;
                PerformElementPick(uiApp, graphInputs, dialog.PickInputId, dialog.PickMultiple, prefilledValues);
                continue; // Reopen the dialog
            }

            // --- User clicked "Run Graph" — all values collected ---
            return dialog.UserValues;
        }
    }

    /// <summary>
    /// Runs <c>PickObject</c>/<c>PickObjects</c> for a selection input and stores the
    /// resulting UniqueId(s) in <paramref name="prefilledValues"/> so the reopened
    /// dialog shows the new selection.  A cancelled or failed pick leaves the previous
    /// selection unchanged.
    /// </summary>
    private static void PerformElementPick(
        UIApplication uiApp,
        DynamoGraphInputs graphInputs,
        string pickInputId,
        bool pickMultiple,
        Dictionary<string, object> prefilledValues)
    {
        var inputDef = graphInputs.Inputs.Find(i => i.Id == pickInputId);
        var promptName = inputDef?.Name ?? "element";

        try
        {
            var uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null)
                return; // no active document — reopen the dialog unchanged

            if (pickMultiple)
            {
                var refs = uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    $"Select elements for '{promptName}'");

                if (refs == null || refs.Count == 0)
                    return;

                // Use UniqueId for each element — this is the format that Dynamo's
                // selection nodes store and deserialise.  UniqueIds never contain
                // commas, so the comma delimiter is safe.
                var idParts = new List<string>(refs.Count);
                foreach (var r in refs)
                {
                    var el = uiDoc.Document.GetElement(r.ElementId);
                    if (el?.UniqueId != null)
                    {
                        idParts.Add(el.UniqueId);
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine(
                            $"[Relay] Could not get UniqueId for element {r.ElementId.Value} — skipping.");
                    }
                }

                if (idParts.Count > 0)
                {
                    var uniqueIds = string.Join(DynamoInputParser.SelectionIdSeparator, idParts);
                    prefilledValues[pickInputId] = new SelectionValue
                    {
                        Identifier = uniqueIds,
                        DisplayText = $"{idParts.Count} element(s) selected"
                    };
                    if (inputDef != null)
                        inputDef.SelectionIdentifier = uniqueIds;
                }
            }
            else
            {
                var reference = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    $"Select element for '{promptName}'");

                var element = uiDoc.Document.GetElement(reference.ElementId);
                if (element?.UniqueId == null)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[Relay] Could not get UniqueId for element {reference.ElementId.Value}.");
                    return; // Reopen without updating — don't store a bad identifier
                }

                var display = $"{element.Category?.Name ?? "Element"} [{element.Id.Value}]";

                prefilledValues[pickInputId] = new SelectionValue
                {
                    Identifier = element.UniqueId,
                    DisplayText = display
                };
                if (inputDef != null)
                    inputDef.SelectionIdentifier = element.UniqueId;
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // User pressed Escape during pick — reopen dialog without updating selection
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[Relay] Pick failed: {ex.Message}");
        }
    }

    private static string JoinDiagnostics(params string[] diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Where(value => !string.IsNullOrWhiteSpace(value)));
}
