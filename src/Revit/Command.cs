using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using Dynamo.Applications;
using Relay.Utilities;
using Application = Autodesk.Revit.ApplicationServices.Application;
using Control = Autodesk.Revit.DB.Control;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using TaskDialogCommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons;
using TaskDialogIcon = Autodesk.Revit.UI.TaskDialogIcon;

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
                //the upper folder name (panel name)
                DirectoryInfo dInfo = new DirectoryInfo(directory);

                Autodesk.Revit.UI.RibbonPanel panelToUse;

                //try to create the panel, if it already exists, just use it
                try
                {
                    panelToUse = uiapp.CreateRibbonPanel("Relay", dInfo.Name);
                }
                catch (Exception)
                {
                    panelToUse = uiapp.GetRibbonPanels("Relay").First(p => p.Name.Equals(dInfo.Name));
                }

                //find the files that do not have a button yet
                var toCreate = Directory.GetFiles(directory, "*.dyn")
                    .Where(f => RibbonUtils.GetButton("Relay", dInfo.Name, $"relay{new FileInfo(f).Name.Replace(" ", "")}") == null).ToArray();

                //if the user is holding down the left shift key, then force the large icons
                bool forceLargeIcons = Keyboard.IsKeyDown(Key.LeftShift);

                RibbonUtils.AddItems(panelToUse, toCreate, forceLargeIcons);
            }

            //subscribe to the events of the button to associate the current DYN
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
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class About : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog aboutDialog = new TaskDialog($"Linked Details v.{Globals.RevitVersion}")
            {
                MainIcon = TaskDialogIcon.TaskDialogIconInformation,
                MainInstruction =
                    @"Hi there! Relay allows you to add DYNs to your ribbon in a pretty okay way.",
                MainContent = @"For a tutorial on getting started, go to <a href=""https://www.notion.so/parallaxteam/Relay-for-Revit-6732550b41d34bce8edc518c0d0e47b9"">Relay docs</a>",
                CommonButtons = TaskDialogCommonButtons.Ok,
                FooterText = @"<a href=""https://icons8.com/"">Icons Courtesy of Icons8</a>"
            };

            var dialogResult = aboutDialog.Show();


            return Result.Succeeded;

        }
    }
}
