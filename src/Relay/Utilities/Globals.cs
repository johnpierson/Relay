using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace Relay.Utilities
{
    public class Globals
    {
        public static readonly string Version =
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public static string ExecutingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string UserTemp = Environment.GetEnvironmentVariable("TMP", EnvironmentVariableTarget.User);
        public static string RevitVersion { get; set; }
        public static Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();
        public static string[] EmbeddedLibraries = ExecutingAssembly.GetManifestResourceNames().Where(x => x.EndsWith(".dll")).ToArray();
        public static string RelayGraphs = Path.Combine(Globals.ExecutingPath, "RelayGraphs");

        public static string CurrentGraphToRun { get; set; } = "";

    }
}
