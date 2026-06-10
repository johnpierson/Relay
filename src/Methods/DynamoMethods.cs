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
            var currentDocument = uiApp.ActiveUIDocument?.Document;
            var sourceGraphPath = dynamoJournal;
            string modifiedGraphPath = null;
            string tempGraphPath = null;

            var graphInputs = DynamoInputParser.ParseGraphInputs(dynamoJournal);
            DynamoInputParser.PopulateRevitItems(graphInputs, currentDocument);

            if (graphInputs.HasInputs)
            {
                var prefilledValues = new Dictionary<string, object>();

                while (true)
                {
                    var dialog = new InputDialog(graphInputs, prefilledValues);

                    try
                    {
                        new System.Windows.Interop.WindowInteropHelper(dialog)
                        {
                            Owner = uiApp.MainWindowHandle
                        };
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[Relay] Could not set input dialog owner: {ex.Message}");
                    }

                    dialog.ShowDialog();

                    if (dialog.WasCancelled)
                        return Result.Cancelled;

                    if (dialog.NeedsPick && !string.IsNullOrEmpty(dialog.PickInputId))
                    {
                        prefilledValues = dialog.PartialValues;
                        HandleSelectionPick(uiApp, graphInputs, prefilledValues, dialog.PickInputId, dialog.PickMultiple);
                        continue;
                    }

                    modifiedGraphPath = DynamoInputParser.ApplyUserValues(dynamoJournal, dialog.UserValues);
                    sourceGraphPath = modifiedGraphPath;
                    break;
                }
            }

            tempGraphPath = DynamoUtils.SetToAutomatic(sourceGraphPath);
            bool shouldShutdownModel = ShouldShutdownDynamoModel(currentDocument);

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

                DynamoRevitCommandData dynamoRevitCommandData = new DynamoRevitCommandData
                {
                    Application = uiApp,
                    JournalData = journalData
                };

                return Result.Succeeded;
            }
            finally
            {
                try
                {
                    if (tempGraphPath != null)
                        System.IO.File.Delete(tempGraphPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Relay] Failed to delete temporary graph file '{tempGraphPath}': {ex.Message}");
                }

                try
                {
                    if (modifiedGraphPath != null && modifiedGraphPath != dynamoJournal)
                        System.IO.File.Delete(modifiedGraphPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Relay] Failed to delete modified graph file '{modifiedGraphPath}': {ex.Message}");
                }
            }
        }

        private static void HandleSelectionPick(
            UIApplication uiApp,
            DynamoGraphInputs graphInputs,
            Dictionary<string, object> prefilledValues,
            string inputId,
            bool pickMultiple)
        {
            var uiDocument = uiApp.ActiveUIDocument;
            if (uiDocument == null)
                return;

            var input = graphInputs.Inputs.FirstOrDefault(item => item.Id == inputId);
            var promptName = input?.Name ?? "element";

            try
            {
                if (pickMultiple)
                {
                    var references = uiDocument.Selection.PickObjects(
                        ObjectType.Element,
                        $"Select elements for '{promptName}'");

                    var uniqueIds = new List<string>();
                    foreach (var reference in references)
                    {
                        var element = uiDocument.Document.GetElement(reference.ElementId);
                        if (!string.IsNullOrEmpty(element?.UniqueId))
                            uniqueIds.Add(element.UniqueId);
                    }

                    if (uniqueIds.Count == 0)
                        return;

                    var identifier = string.Join(",", uniqueIds);
                    prefilledValues[inputId] = new SelectionValue
                    {
                        Identifier = identifier,
                        DisplayText = $"{uniqueIds.Count} element(s) selected"
                    };

                    if (input != null)
                        input.SelectionIdentifier = identifier;

                    return;
                }

                var pickedReference = uiDocument.Selection.PickObject(
                    ObjectType.Element,
                    $"Select element for '{promptName}'");

                var pickedElement = uiDocument.Document.GetElement(pickedReference.ElementId);
                if (string.IsNullOrEmpty(pickedElement?.UniqueId))
                    return;

                var displayText = $"{pickedElement.Category?.Name ?? "Element"} [{GetElementIdString(pickedReference.ElementId)}]";
                prefilledValues[inputId] = new SelectionValue
                {
                    Identifier = pickedElement.UniqueId,
                    DisplayText = displayText
                };

                if (input != null)
                    input.SelectionIdentifier = pickedElement.UniqueId;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Reopen the dialog with the values the user had entered before picking.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Element pick failed: {ex.Message}");
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

        private static string GetElementIdString(Autodesk.Revit.DB.ElementId elementId)
        {
            return elementId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
