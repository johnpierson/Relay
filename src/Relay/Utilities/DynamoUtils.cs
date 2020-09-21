using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynamo.Applications;
using DynamoInstallDetective;

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
