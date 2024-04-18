using System.IO;
using Autodesk.Revit.UI;
using Dynamo.Applications;

namespace Relay.Utilities
{
    class DynamoUtils
    {
        public static void SetToAutomatic(string filePath)
        {
           string text = File.ReadAllText(filePath);
           text = text.Replace(@"""RunType"": ""Manual"",", @"""RunType"": ""Automatic"",");

           File.WriteAllText(filePath,text);
        }

        public static void InitializeDynamoRevit(ExternalCommandData commandData)
        {
            DynamoRevit dynamoRevit = new DynamoRevit();

            IDictionary<string, string> journalData = new Dictionary<string, string>
            {
                {JournalKeys.ShowUiKey, false.ToString()},
                {JournalKeys.AutomationModeKey, true.ToString()},
                {JournalKeys.DynPathExecuteKey, true.ToString()},
                {JournalKeys.ForceManualRunKey, false.ToString()},
                {JournalKeys.ModelShutDownKey, true.ToString()},
                {JournalKeys.ModelNodesInfo, false.ToString()},
            };
            DynamoRevitCommandData dynamoRevitCommandData = new DynamoRevitCommandData
            {
                Application = commandData.Application,
                JournalData = journalData
            };
            dynamoRevit.ExecuteCommand(dynamoRevitCommandData);
        }
    }
}
