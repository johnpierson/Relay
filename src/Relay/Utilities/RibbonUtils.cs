using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using AW = Autodesk.Windows;

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
        public static void AddItems(Autodesk.Revit.UI.RibbonPanel panelToUse, string[] dynPaths)
        {
            //TODO: Implement duplicate fix
            var totalFiles = dynPaths.Length;

            List<PushButtonData> pushButtonDatas = new List<PushButtonData>();
            foreach (var file in dynPaths)
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
                //TODO: Implement check for 32 and 16 px files
                newButtonData.LargeImage = File.Exists(icoPath)
                    ? new BitmapImage(new Uri(icoPath))
                    : new BitmapImage(new Uri(Path.Combine(Globals.RelayGraphs, "Dynamo_32.png")));

                newButtonData.Image = File.Exists(icoPath)
                    ? new BitmapImage(new Uri(icoPath))
                    : new BitmapImage(new Uri(Path.Combine(Globals.RelayGraphs, "Dynamo_16.png")));

                pushButtonDatas.Add(newButtonData);
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


            return;
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
    }
}
