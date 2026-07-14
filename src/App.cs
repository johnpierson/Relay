using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using Relay.Classes;
using Relay.Configuration;
using Relay.Utilities;

namespace Relay
{
    class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
            CleanupOldImages();

            //set the Revit version for reuse
            Globals.RevitVersion = a.ControlledApplication.VersionNumber;

            string startupWarning = null;
            string settingsPath = Path.Combine(Globals.ExecutingPath, "RelaySettings.ini");
            string configuredRoot = "default";

            if (File.Exists(settingsPath))
            {
                try
                {
                    var relayIni = new RelayIniFile();
                    configuredRoot = relayIni.Read("Path", "Settings");
                }
                catch (Exception ex) when (ex is IOException
                                           or UnauthorizedAccessException
                                           or DllNotFoundException
                                           or EntryPointNotFoundException)
                {
                    configuredRoot = null;
                    startupWarning = $"Relay could not read '{settingsPath}': {ex.Message}";
                }
            }

            GraphRootResolution resolution = GraphRootResolver.Resolve(
                configuredRoot,
                Globals.ExecutingPath);
            Globals.BasePath = resolution.RootPath;

            if (resolution.HasWarning)
            {
                startupWarning = resolution.Warning;
            }

            GraphDirectoryDiscoveryResult discovery = GraphDirectoryDiscovery.Discover(Globals.BasePath);
            if (discovery.Succeeded)
            {
                Globals.PotentialTabDirectories = discovery.Directories;
            }
            else
            {
                Globals.PotentialTabDirectories = Array.Empty<string>();
                startupWarning = startupWarning is null
                    ? discovery.Error
                    : $"{startupWarning} {discovery.Error}";
            }
            
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

            if (startupWarning is not null)
            {
                ReportStartupWarning(startupWarning);
            }

            return Result.Succeeded;
        }

        private static void ReportStartupWarning(string diagnostic)
        {
            System.Diagnostics.Trace.WriteLine($"[Relay] {diagnostic}");
            Autodesk.Revit.UI.TaskDialog.Show(
                "Relay graph configuration",
                "Relay could not use the configured graph location. The add-in remains available; review RelaySettings.ini and use Sync Graphs after correcting the path.");
        }

        private void ControlledApplication_ApplicationInitialized(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            if (sender is UIApplication uiapp)
            {
                RibbonUtils.SyncGraphs(uiapp);
            }
            else
            {
                var application = sender as Application;
                if (application == null) return;
                uiapp = new UIApplication(application);
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

        internal void CleanupOldImages()
        {
            var relayImages = Directory.GetFiles(Globals.UserTemp, "relayImage*.png");

            foreach (var image in relayImages)
            {
                try
                {
                    File.Delete(image);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Relay] Failed to delete temporary image '{image}': {ex.Message}");
                }

            }
        }
    }
}
