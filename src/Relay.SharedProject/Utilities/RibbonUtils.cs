using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Dynamo.Graph.Workspaces;

using UIFramework;
using AW = Autodesk.Windows;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using Relay.Classes;

namespace Relay.Utilities
{
    class RibbonUtils
    {
        public static List<List<T>> SplitList<T>(List<T> me, int size = 50)
        {
            var list = new List<List<T>>();
            for (int i = 0; i < me.Count; i += size)
                list.Add(me.GetRange(i, Math.Min(size, me.Count - i)));
            return list;
        }

        public static void AddItems(Autodesk.Revit.UI.RibbonPanel panelToUse, string[] dynPaths, bool forceLargeIcon = false)
        {
            var totalFiles = dynPaths.Length;

            List<PushButtonData> pushButtonDatas = new List<PushButtonData>();
            foreach (var file in dynPaths)
            {
                FileInfo fInfo = new FileInfo(file);

                string tooltip = GetDescription(fInfo);

                string buttonName = $"relay{fInfo.Name.Replace(" ", "")}";
                PushButtonData newButtonData = new PushButtonData(buttonName,
                    fInfo.Name.GenerateButtonText(),
                    Path.Combine(Globals.ExecutingPath, "Relay.dll"), "Relay.Run")
                {
                    ToolTip = tooltip,
                };
              
                //set the images, if there are none, use default
                string icon32 = fInfo.FullName.Replace(".dyn", "_32.png");
                newButtonData.LargeImage = File.Exists(icon32)
                    ? new BitmapImage(new Uri(icon32))
                    : new BitmapImage(new Uri(Path.Combine(Globals.RelayGraphs, "Dynamo_32.png")));

                string icon16 = fInfo.FullName.Replace(".dyn", "_16.png");
                newButtonData.Image = File.Exists(icon16)
                    ? new BitmapImage(new Uri(icon16))
                    : new BitmapImage(new Uri(Path.Combine(Globals.RelayGraphs, "Dynamo_16.png")));

                TrySetContextualHelp(newButtonData, fInfo);

                pushButtonDatas.Add(newButtonData);
            }

            if (forceLargeIcon)
            {
                foreach (var pushButton in pushButtonDatas)
                {
                    panelToUse.AddItem(pushButton);
                }
                return;
            }

            var splitButtons = SplitList(pushButtonDatas, 2);

            foreach (var buttonGroup in splitButtons)
            {
                switch (buttonGroup.Count)
                {
                    case 2:
                        panelToUse.AddStackedItems(buttonGroup[0], buttonGroup[1]);
                        break;
                    case 1:
                        panelToUse.AddItem(buttonGroup[0]);
                        break;
                }
            }

        }

        private static string GetDescription(FileInfo fInfo)
        {
            string description;
            try
            {
                string jsonString = File.ReadAllText(fInfo.FullName);
                var relayGraph = JsonSerializer.Deserialize<RelayGraph>(jsonString);

                description = relayGraph.Description != null ? $"{relayGraph.Description}\r\r[{fInfo.FullName}]" : $"[{fInfo.FullName}]";
            }
            catch (Exception e)
            {
                var s = e.Message;
                description = $"[{fInfo.FullName}]";
            }
           
            return description;
        }

        private static void TrySetContextualHelp(PushButtonData pushButtonData, FileInfo fInfo)
        {
            try
            {
                string jsonString = File.ReadAllText(fInfo.FullName);
                var relayGraph = JsonSerializer.Deserialize<RelayGraph>(jsonString);

                if (relayGraph.GraphDocumentationURL is null || string.IsNullOrWhiteSpace(relayGraph.GraphDocumentationURL))
                {
                    return;
                }

                ContextualHelp help = new ContextualHelp(ContextualHelpType.Url, relayGraph.GraphDocumentationURL);
                pushButtonData.SetContextualHelp(help);
            }
            catch (Exception)
            {
                //don't set the help
            }
        }

        public static AW.RibbonItem GetButton(string tabName, string panelName, string itemName)
        {
            AW.RibbonControl ribbon = AW.ComponentManager.Ribbon;

            foreach (AW.RibbonTab tab in ribbon.Tabs)
            {
                if (tab.Name == tabName)
                {
                    foreach (AW.RibbonPanel panel in tab.Panels)
                    {
                        if (panel.Source.Title == panelName)
                        {
                            return panel.FindItem("CustomCtrl_%CustomCtrl_%"
                                                  + tabName + "%" + panelName + "%" + itemName,
                                true) as AW.RibbonItem;
                        }
                    }
                }
            }
            return null;
        }

        public static AW.RibbonPanel GetPanel(string tabName, string panelName)
        {
            AW.RibbonControl ribbon = AW.ComponentManager.Ribbon;

            foreach (AW.RibbonTab tab in ribbon.Tabs)
            {
                if (tab.Name == tabName)
                {
                    foreach (AW.RibbonPanel panel in tab.Panels)
                    {
                        if (panel.Source.Title == panelName)
                        {
                            return panel;
                        }
                    }
                }
            }
            return null;
        }
        public static AW.RibbonTab GetTab(string tabName)
        {
            AW.RibbonControl ribbon = AW.ComponentManager.Ribbon;

            foreach (AW.RibbonTab tab in ribbon.Tabs)
            {
                if (tab.Name == tabName)
                {
                    return tab;
                }
            }
            return null;
        }

        public static void SyncGraphs(UIApplication uiapp)
        {
            if (!Directory.Exists(Globals.RelayGraphs))
            {
                return;
            }
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
        }

    }
}
