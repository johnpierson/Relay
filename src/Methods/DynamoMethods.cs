using System.IO;
using Autodesk.Revit.UI;
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

            // Step 2: If inputs exist, present a WPF dialog so the user can supply values
            string sourceGraphPath = dynamoJournal;
            string modifiedGraphPath = null;

            if (graphInputs.HasInputs)
            {
                var dialog = new InputDialog(graphInputs);

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

                bool? dialogResult = dialog.ShowDialog();

                if (dialog.WasCancelled || dialogResult != true)
                    return Result.Cancelled;

                // Step 3: Write user values back to a modified temp graph
                modifiedGraphPath = DynamoInputParser.ApplyUserValues(dynamoJournal, dialog.UserValues);
                sourceGraphPath = modifiedGraphPath;
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
    }
}
