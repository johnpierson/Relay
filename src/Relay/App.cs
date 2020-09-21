using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using Relay.Utilities;


namespace Relay
{
    class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
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
            PushButtonData setupButtonData = new PushButtonData("SyncGraphs", "Sync\nGraphs",
                Path.Combine(Globals.ExecutingPath, "Relay.dll"), "Relay.RefreshGraphs")
            {
                LargeImage = new BitmapImage(new Uri(Path.Combine(Globals.RelayGraphs, "sync.png")))
            };

            setupRibbonPanel.AddItem(setupButtonData);
            

        }
    }
}
