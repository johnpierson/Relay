using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Relay.Utilities;

namespace Relay
{
    class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
            // Attach custom event handler
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
            CreateRibbon(a);
            return Result.Succeeded;
        }


        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }

        public void CreateRibbon(UIControlledApplication a)
        {
            try
            {
                // Create a custom ribbon tab
                a.CreateRibbonTab("Relay");
            }
            catch
            {
                // Might Already Exist
            }


            //add our setup panel and button
            var setupRibbonPanel = a.CreateRibbonPanel("Relay", "Setup");

            PushButtonData aboutButtonData = new PushButtonData("AboutRelay", "About\nRelay",
                Path.Combine(Globals.ExecutingPath, "Relay.dll"), "Relay.About")
            {
                Image = new BitmapImage(new Uri(Path.Combine(Globals.RelayGraphs, "About_16.png")))
            };
            
            PushButtonData syncButtonData = new PushButtonData("SyncGraphs", "Sync\nGraphs",
                Path.Combine(Globals.ExecutingPath, "Relay.dll"), "Relay.RefreshGraphs")
            {
                Image = new BitmapImage(new Uri(Path.Combine(Globals.RelayGraphs, "Sync_16.png"))),
                ToolTip = "This will sync graphs from the default graph directory. Hold down left shift key to force large images"
            };

            setupRibbonPanel.AddStackedItems(aboutButtonData,syncButtonData);
            

        }
        private Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Get assembly name
            var assemblyName = new AssemblyName(args.Name).Name + ".dll";

            // Get resource name
            var resourceName = Globals.EmbeddedLibraries.FirstOrDefault(x => x.EndsWith(assemblyName));
            if (resourceName == null)
            {
                return null;
            }

            // Load assembly from resource
            using (var stream = Globals.ExecutingAssembly.GetManifestResourceStream(resourceName))
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return Assembly.Load(bytes);
            }
        }
    }
}
