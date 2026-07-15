using Autodesk.Revit.UI;
using Dynamo.Applications;

namespace Relay.Utilities
{
    internal static class DynamoUtils
    {
        public static void InitializeDynamoRevit(ExternalCommandData commandData)
        {
            var dynamoRevit = new DynamoRevit();
            IDictionary<string, string> journalData = new Dictionary<string, string>
            {
                {JournalKeys.ShowUiKey, false.ToString()},
                {JournalKeys.AutomationModeKey, true.ToString()},
                {JournalKeys.DynPathExecuteKey, true.ToString()},
                {JournalKeys.ForceManualRunKey, false.ToString()},
                {JournalKeys.ModelShutDownKey, true.ToString()},
                {JournalKeys.ModelNodesInfo, false.ToString()},
            };
            var dynamoRevitCommandData = new DynamoRevitCommandData
            {
                Application = commandData.Application,
                JournalData = journalData
            };
            dynamoRevit.ExecuteCommand(dynamoRevitCommandData);
        }
    }
}
