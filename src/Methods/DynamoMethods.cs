using Autodesk.Revit.UI;
using Dynamo.Applications;
using Relay.Utilities;

namespace Relay.Methods
{
    internal static class DynamoMethods
    {
        internal static Result RunGraph(UIApplication uiApp, string dynamoJournal)
        {
            //toggle the graph to automatic. this is required for running Dynamo UI-Less
            DynamoUtils.SetToAutomatic(dynamoJournal);

            //DynamoRevit dynamoRevit = new DynamoRevit();


            IDictionary<string, string> journalData = new Dictionary<string, string>
            {
                {JournalKeys.ShowUiKey, false.ToString()},
                {JournalKeys.AutomationModeKey, true.ToString()},
                {"dynPath", dynamoJournal},
                //{JournalKeys.DynPathKey, dynamoJournal},
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
    }
}
