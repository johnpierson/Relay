using System.IO;

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
    }
}
