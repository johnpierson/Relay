using System.IO;
using System.Reflection;

namespace Relay.Utilities
{
    public partial class Globals
    {
        public static Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();
        public static readonly string Version = ExecutingAssembly.GetName().Version.ToString();

        public static string ExecutingPath = Path.GetDirectoryName(ExecutingAssembly.Location);
        public static string BasePath { get; set; } = ExecutingPath;

        public static string UserTemp = Environment.GetEnvironmentVariable("TMP", EnvironmentVariableTarget.User);
        public static string RevitVersion { get; set; }
      
        public static string[] EmbeddedLibraries = ExecutingAssembly.GetManifestResourceNames().Where(x => x.EndsWith(".dll")).ToArray();
        public static string[] PotentialTabDirectories { get; set; }

        public static string RibbonTabName { get; set; } = "Relay";
        public static string RelayGraphs = Path.Combine(ExecutingPath, RibbonTabName);

        public static string CurrentGraphToRun { get; set; } = "";

        public static bool ResetRibbonOnSync { get; set; } = false;

    }
}
