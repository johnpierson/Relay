using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Autodesk.DesignScript.Geometry;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using Dynamo.Applications;
using Dynamo.Applications.Models;
using Dynamo.Core;
using Relay.Utilities;
using RevitServices;
using Application = Autodesk.Revit.ApplicationServices.Application;
using Line = Autodesk.DesignScript.Geometry.Line;
using Point = Autodesk.DesignScript.Geometry.Point;

namespace Relay
{
    [Transaction(TransactionMode.Manual)]
    public class RefreshGraphs : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;


            var relayTab = RibbonUtils.GetTab("Relay");

            //create the panels for the sub directories
            foreach (var directory in Directory.GetDirectories(Globals.RelayGraphs))
            {
                DirectoryInfo dInfo = new DirectoryInfo(directory);

                Autodesk.Revit.UI.RibbonPanel panelToUse = null;

                try
                {
                    panelToUse = uiapp.CreateRibbonPanel("Relay", dInfo.Name);
                }
                catch (Exception)
                {
                    panelToUse = uiapp.GetRibbonPanels("Relay").First(p => p.Name.Equals(dInfo.Name));
                }

                //create the buttons
                foreach (var file in Directory.GetFiles(directory, "*.dyn"))
                {
                    FileInfo fInfo = new FileInfo(file);

                    string buttonName = $"relay{fInfo.Name.Replace(" ", "")}";

                    PushButtonData newButtonData = new PushButtonData(buttonName,
                        fInfo.Name,
                        Path.Combine(Globals.ExecutingPath, "Relay.dll"), "Relay.Run")
                    {
                        ToolTip = fInfo.FullName
                    };

                    string icoPath = fInfo.FullName.Replace(".dyn", ".png");
                    newButtonData.LargeImage = File.Exists(icoPath)
                        ? new BitmapImage(new Uri(icoPath))
                        : new BitmapImage(new Uri(Path.Combine(Globals.RelayGraphs, "Dynamo.png")));

                    try
                    {
                        panelToUse.AddItem(newButtonData);
                    }
                    catch (Exception)
                    {
                        var item = panelToUse.GetItems().First(b => b.Name.Equals(buttonName));
                    }
                }
            }

            Autodesk.Windows.ComponentManager.UIElementActivated -= ComponentManagerOnUIElementActivated;
            Autodesk.Windows.ComponentManager.UIElementActivated += ComponentManagerOnUIElementActivated;

            return Result.Succeeded;
        }

        private void ComponentManagerOnUIElementActivated(object sender, UIElementActivatedEventArgs e)
        {
            try
            {
                if (!e.Item.Id.Contains("relay")) return;

                //set our current graph based on the click on the ribbon item
                Globals.CurrentGraphToRun = e.Item.Description;
            }
            catch (Exception)
            {
                // suppress the error if it happens
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Run : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string dynamoJournal = Globals.CurrentGraphToRun;

            if (string.IsNullOrWhiteSpace(dynamoJournal))
            {
                return Result.Failed;
            }

            //toggle the graph to automatic. this is required for running Dynamo UI-Les
            DynamoUtils.SetToAutomatic(dynamoJournal);

            DynamoRevit dynamoRevit = new DynamoRevit();
            
            IDictionary<string, string> journalData = new Dictionary<string, string>
            {
                {JournalKeys.ShowUiKey, false.ToString()},
                {JournalKeys.AutomationModeKey, true.ToString()},
                {JournalKeys.DynPathKey, dynamoJournal},
                {JournalKeys.DynPathExecuteKey, true.ToString()},
                {JournalKeys.ForceManualRunKey, false.ToString()},
                {JournalKeys.ModelShutDownKey, true.ToString()},
                {JournalKeys.ModelNodesInfo, false.ToString()},
            };
            DynamoRevitCommandData dynamoRevitCommandData = new DynamoRevitCommandData
            {
                Application = commandData.Application, JournalData = journalData
            };

            return dynamoRevit.ExecuteCommand(dynamoRevitCommandData);

        }
    }
}
