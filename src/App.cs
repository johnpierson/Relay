using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using Relay.Classes;
using Relay.Utilities;

namespace Relay
{
    class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
            //set the Revit version for reuse
            Globals.RevitVersion = a.ControlledApplication.VersionNumber;

            //read the ini file
            if (File.Exists(Path.Combine(Globals.ExecutingPath,"RelaySettings.ini")))
            {
                try
                {
                    var relayIni = new RelayIniFile();
                    var path = relayIni.Read("Path", "Settings");
                    var resetRibbon = relayIni.Read("ResetRibbon", "Settings");
                    //it the path is default, read that
                    Globals.BasePath = path.ToLower().Equals("default") ? Globals.ExecutingPath : path;

                    Globals.ResetRibbonOnSync = bool.Parse(resetRibbon);
                }
                //you screwed up the path mapping, sorry this tool is using the default then
                catch (Exception)
                {
                    Globals.BasePath = Globals.ExecutingPath;
                }
            }
            else
            {
                Globals.BasePath = Globals.ExecutingPath;
            }

            // parse the location for the potential tab name
            Globals.PotentialTabDirectories = Directory.GetDirectories(Globals.BasePath);
            
            if (Globals.PotentialTabDirectories.Any())
            {
                var potentialTabNames =
                    Globals.PotentialTabDirectories.Select(d => new DirectoryInfo(d).Name).ToArray();
                // Use the first folder for this first ribbon
                Globals.RibbonTabName = potentialTabNames.First();
            }
            else
            {
                Globals.RibbonTabName = "Relay";
            }

            // subscribe to ribbon click events
            Autodesk.Windows.ComponentManager.UIElementActivated += ComponentManagerOnUIElementActivated;
            // Attach custom event handler
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
            CreateRibbon(a);
            a.ControlledApplication.ApplicationInitialized += ControlledApplication_ApplicationInitialized;

            return Result.Succeeded;
        }

        private void ControlledApplication_ApplicationInitialized(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            var syncGraphsId = RevitCommandId.LookupCommandId("CustomCtrl_%CustomCtrl_%Relay%Setup%SyncGraphs");

            if (sender is UIApplication uiapp)
            {
                RibbonUtils.SyncGraphs(uiapp);
            }
            else
            {
                uiapp = new UIApplication(sender as Application);
                RibbonUtils.SyncGraphs(uiapp);
            }
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
                a.CreateRibbonTab(Globals.RibbonTabName);
            }
            catch
            {
                // Might Already Exist
            }


            //add our setup panel and button
            var setupRibbonPanel = a.CreateRibbonPanel(Globals.RibbonTabName, "Setup");

            //if the about exists in the relay graphs location, use it, if not use the resource
            string localAboutImage = Path.Combine(Globals.RelayGraphs, "About_16.png");
            BitmapImage aboutImage = aboutImage = File.Exists(localAboutImage) ? new BitmapImage(new Uri(localAboutImage)) : ImageUtils.LoadImage(Globals.ExecutingAssembly, "About_16.png");

            PushButtonData aboutButtonData = new PushButtonData("AboutRelay", "About\nRelay",
                Path.Combine(Globals.ExecutingPath, "Relay.dll"), "Relay.About")
            {
                Image = aboutImage
            };

            //if the sync exists in the relay graphs location, use it, if not use the resource
            string localSyncImage = Path.Combine(Globals.RelayGraphs, "Sync_16.png");
            BitmapImage syncImage = aboutImage = File.Exists(localSyncImage) ? new BitmapImage(new Uri(localSyncImage)) : ImageUtils.LoadImage(Globals.ExecutingAssembly, "Sync_16.png");

            PushButtonData syncButtonData = new PushButtonData("SyncGraphs", "Sync\nGraphs",
                Path.Combine(Globals.ExecutingPath, "Relay.dll"), "Relay.RefreshGraphs")
            {
                Image = syncImage,
                ToolTip = "This will sync graphs from the default graph directory. Hold down left shift key to force large images"
            };

            setupRibbonPanel.AddStackedItems(aboutButtonData, syncButtonData);
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

        private void ComponentManagerOnUIElementActivated(object sender, UIElementActivatedEventArgs e)
        {
            if(e.Item is null ) return;
            try
            {
                if (!e.Item.Id.Contains("relay")) return;
                //set our current graph based on the click on the ribbon item
                Globals.CurrentGraphToRun = e.Item.Description.GetStringBetweenCharacters('[', ']');
            }
            catch (Exception)
            {
                // suppress the error if it happens
            }
        }
    }
}
