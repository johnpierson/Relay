using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using Relay.Utilities;
using Application = Autodesk.Revit.ApplicationServices.Application;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using TaskDialogCommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons;
using TaskDialogIcon = Autodesk.Revit.UI.TaskDialogIcon;

namespace Relay
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class RefreshGraphs : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;

            var relayTab = RibbonUtils.GetTab("Relay");

            RibbonUtils.SyncGraphs(uiapp);

            return Result.Succeeded;
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
            
            return Methods.DynamoMethods.RunGraph(commandData.Application, dynamoJournal);
        }
    }
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class About : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog aboutDialog = new TaskDialog($"Relay v.{Globals.RevitVersion}")
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
