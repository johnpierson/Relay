using System.IO;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Dynamo.Applications;
using Relay.UI;
using Relay.Utilities;

namespace Relay.Methods
{
    internal static class DynamoMethods
    {
        internal static Result RunGraph(UIApplication uiApp, string dynamoJournal)
        {
            // Step 1: Parse the Dynamo graph for input nodes marked as IsSetAsInput
            var graphInputs = DynamoInputParser.ParseGraphInputs(dynamoJournal);

            // Step 2: Populate items for Revit-specific dropdown nodes (e.g. Categories)
            try
            {
                var doc = uiApp.ActiveUIDocument?.Document;
                DynamoInputParser.PopulateRevitItems(graphInputs, doc);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Could not populate Revit dropdown items: {ex.Message}");
            }

            // Step 3: If inputs exist, present a WPF dialog so the user can supply values.
            //         The dialog may close with NeedsPick = true when the user clicks
            //         "Select in Revit", in which case we perform the pick and reopen.
            string sourceGraphPath = dynamoJournal;
            string modifiedGraphPath = null;

            if (graphInputs.HasInputs)
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
                        return Result.Cancelled;

                    // --- User clicked "Select in Revit" ---
                    if (dialog.NeedsPick && !string.IsNullOrEmpty(dialog.PickInputId))
                    {
                        prefilledValues = dialog.PartialValues;

                        var pickInputId  = dialog.PickInputId;
                        var pickMultiple = dialog.PickMultiple;
                        var inputDef     = graphInputs.Inputs.Find(i => i.Id == pickInputId);
                        var promptName   = inputDef?.Name ?? "element";

                        try
                        {
                            var uiDoc = uiApp.ActiveUIDocument;
                            if (uiDoc == null)
                                continue; // no active document, loop back

                            if (pickMultiple)
                            {
                                var refs = uiDoc.Selection.PickObjects(
                                    ObjectType.Element,
                                    $"Select elements for '{promptName}'");

                                if (refs != null && refs.Count > 0)
                                {
                                    // Store as comma-separated element ID integers.
                                    // Element IDs are integers (no embedded commas), so this
                                    // format is safe and matches the single-element convention.
                                    var ids = string.Join(",",
                                        refs.Select(r => GetElementIdString(r.ElementId)));

                                    prefilledValues[pickInputId] = ids;
                                    if (inputDef != null)
                                        inputDef.SelectionIdentifier = ids;
                                }
                            }
                            else
                            {
                                var reference = uiDoc.Selection.PickObject(
                                    ObjectType.Element,
                                    $"Select element for '{promptName}'");

                                var idStr = GetElementIdString(reference.ElementId);

                                prefilledValues[pickInputId] = idStr;
                                if (inputDef != null)
                                    inputDef.SelectionIdentifier = idStr;
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

                        continue; // Reopen the dialog
                    }

                    // --- User clicked "Run Graph" — all values collected ---
                    modifiedGraphPath = DynamoInputParser.ApplyUserValues(dynamoJournal, dialog.UserValues);
                    sourceGraphPath   = modifiedGraphPath;
                    break;
                }
            }

            // Step 4: Create a temporary copy of the (possibly modified) graph set to Automatic run mode.
            // This is required for running Dynamo UI-less.
            string tempGraphPath = DynamoUtils.SetToAutomatic(sourceGraphPath);

            // The intermediate modified file (if any) is no longer needed once SetToAutomatic has
            // written its own temp copy.
            if (modifiedGraphPath != null && modifiedGraphPath != dynamoJournal)
            {
                try { File.Delete(modifiedGraphPath); }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Relay] Failed to delete intermediate graph file '{modifiedGraphPath}': {ex.Message}");
                }
            }

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
                    {JournalKeys.ModelShutDownKey, true.ToString()},
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


                DynamoRevitCommandData dynamoRevitCommandData = new DynamoRevitCommandData
                {
                    Application = uiApp,
                    JournalData = journalData
                };


                //sorry folks, parks closed, the moose out front should have told you
#if Revit2021Pro || Revit2022Pro || Revit2023Pro
                Packages.ResolvePackages(DynamoRevit.RevitDynamoModel.PathManager.DefaultPackagesDirectory, dynamoJournal);
#endif

                return Result.Succeeded;
            }
            finally
            {
                //clean up the temporary graph copy
                try { File.Delete(tempGraphPath); } catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Relay] Failed to delete temporary graph file '{tempGraphPath}': {ex.Message}"); }
            }
        }

        /// <summary>
        /// Returns the integer value of an <see cref="Autodesk.Revit.DB.ElementId"/> as a string.
        /// Uses <c>ElementId.Value</c> on Revit 2025+ and the legacy <c>IntegerValue</c> on
        /// earlier versions.
        /// </summary>
        private static string GetElementIdString(Autodesk.Revit.DB.ElementId elementId)
        {
#if R25_OR_GREATER
            return elementId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
#else
#pragma warning disable CS0618
            return elementId.IntegerValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
#pragma warning restore CS0618
#endif
        }
    }
}
