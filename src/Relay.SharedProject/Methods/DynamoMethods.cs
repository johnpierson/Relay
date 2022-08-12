using System.Collections.Generic;
using Autodesk.Revit.UI;
using Dynamo.Applications;
using Relay.Utilities;

namespace Relay.Methods
{
    internal static class DynamoMethods
    {
        internal static Result RunGraph(UIApplication app, string dynamoJournal)
        {
            //toggle the graph to automatic. this is required for running Dynamo UI-Les
            DynamoUtils.SetToAutomatic(dynamoJournal);

            DynamoRevit dynamoRevit = new DynamoRevit();

            IDictionary<string, string> journalData = new Dictionary<string, string>
            {
                {JournalKeys.ShowUiKey, false.ToString()},
                {JournalKeys.AutomationModeKey, true.ToString()},
                {JournalKeys.DynPathKey, ""},
                {JournalKeys.DynPathExecuteKey, true.ToString()},
                {JournalKeys.ForceManualRunKey, false.ToString()},
                {JournalKeys.ModelShutDownKey, true.ToString()},
                {JournalKeys.ModelNodesInfo, false.ToString()},
            };
            DynamoRevitCommandData dynamoRevitCommandData = new DynamoRevitCommandData
            {
                Application = app,
                JournalData = journalData
            };

            var result = dynamoRevit.ExecuteCommand(dynamoRevitCommandData);

            DynamoRevit.RevitDynamoModel.OpenFileFromPath(dynamoJournal, true);
            DynamoRevit.RevitDynamoModel.ForceRun();

            return result;
        }
    }
}
