using Autodesk.Revit.UI;
using Dynamo.Applications;
using Relay.Utilities;

namespace Relay.Methods
{
    internal static class DynamoMethods
    {
        private static WeakReference<Autodesk.Revit.DB.Document> lastDynamoDocument;

        internal static Result RunGraph(UIApplication uiApp, string dynamoJournal)
        {
            //create a temporary copy of the graph set to automatic. this is required for running Dynamo UI-Less
            string tempGraphPath = DynamoUtils.SetToAutomatic(dynamoJournal);
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
                try { System.IO.File.Delete(tempGraphPath); } catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[Relay] Failed to delete temporary graph file '{tempGraphPath}': {ex.Message}"); }
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
