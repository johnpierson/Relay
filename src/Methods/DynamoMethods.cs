using System.IO;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Dynamo.Applications;
using Relay.Classes;
using Relay.UI;
using Relay.Utilities;

namespace Relay.Methods
{
    internal static class DynamoMethods
    {
        private static WeakReference<Autodesk.Revit.DB.Document> lastDynamoDocument;

        internal static Result RunGraph(UIApplication uiApp, string dynamoJournal)
        {
            // Discover nodes marked IsSetAsInput.  Graphs with no declared inputs skip the
            // dialog entirely and run exactly as before.
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

            string sourceGraphPath = dynamoJournal;
            string modifiedGraphPath = null;

            if (graphInputs.HasInputs)
            {
                var userValues = CollectUserInputs(uiApp, graphInputs);
                if (userValues == null)
                    return Result.Cancelled;

                // Apply the accepted values to a disposable copy; the source graph is never touched.
                modifiedGraphPath = DynamoInputParser.ApplyUserValues(dynamoJournal, userValues);
                sourceGraphPath = modifiedGraphPath;
            }

            //create a temporary copy of the graph set to automatic. this is required for running Dynamo UI-Less
            string tempGraphPath = DynamoUtils.SetToAutomatic(sourceGraphPath);

            // The intermediate modified copy (if any) is no longer needed once SetToAutomatic
            // has written its own temp copy.
            if (modifiedGraphPath != null && modifiedGraphPath != dynamoJournal)
            {
                try { File.Delete(modifiedGraphPath); } catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Relay] Failed to delete intermediate graph file '{modifiedGraphPath}': {ex.Message}"); }
            }

            var currentDocument = uiApp.ActiveUIDocument?.Document;
            bool shouldShutdownModel = ShouldShutdownDynamoModel(currentDocument);

            //DynamoRevit dynamoRevit = new DynamoRevit();

            try
            {
                IDictionary<string, string> journalData = new Dictionary<string, string>
                {
                    {JournalKeys.ShowUiKey, false.ToString()},
                    {JournalKeys.AutomationModeKey, true.ToString()},
                    {"dynPath", tempGraphPath},
                    //{JournalKeys.DynPathKey, tempGraphPath},
                    //{JournalKeys.DynPathExecuteKey, true.ToString()},
                    {JournalKeys.ForceManualRunKey, true.ToString()},
                    {JournalKeys.ModelShutDownKey, shouldShutdownModel.ToString()},
                    //{JournalKeys.ModelNodesInfo, false.ToString()},
                };

                //get all loaded assemblies
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

                //find the Dynamo Revit one
                var dynamoRevitApplication = loadedAssemblies
                    .FirstOrDefault(a => a.FullName.Contains("DynamoRevitDS"));

                //create our own instances of these things using reflection. Shoutout to BirdTools for helping with this.
                object dInst = dynamoRevitApplication.CreateInstance("Dynamo.Applications.DynamoRevit");
                object dta = dynamoRevitApplication.CreateInstance("Dynamo.Applications.DynamoRevitCommandData");
                dta.GetType().GetProperty("Application").SetValue(dta, uiApp, null);
                dta.GetType().GetProperty("JournalData").SetValue(dta, journalData, null);

                object[] parameters = new object[] { dta };

                dInst.GetType().GetMethod("ExecuteCommand").Invoke(dInst, parameters);

                object rdm = dInst.GetType().GetProperty("RevitDynamoModel").GetValue(dInst, null);

                rdm.GetType().GetMethod("ForceRun").Invoke(rdm, new object[] { });

                UpdateLastDynamoDocument(currentDocument);

                return Result.Succeeded;
            }
            finally
            {
                //clean up the temporary graph copy
                try { File.Delete(tempGraphPath); } catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Relay] Failed to delete temporary graph file '{tempGraphPath}': {ex.Message}"); }
            }
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

        private static bool ShouldShutdownDynamoModel(Autodesk.Revit.DB.Document currentDocument)
        {
            if (currentDocument == null)
            {
                return true;
            }

            if (lastDynamoDocument == null)
            {
                return false;
            }

            return !lastDynamoDocument.TryGetTarget(out var previousDocument)
                   || !ReferenceEquals(previousDocument, currentDocument);
        }

        private static void UpdateLastDynamoDocument(Autodesk.Revit.DB.Document currentDocument)
        {
            if (currentDocument == null)
            {
                lastDynamoDocument = null;
                return;
            }

            lastDynamoDocument = new WeakReference<Autodesk.Revit.DB.Document>(currentDocument);
        }
    }
}
