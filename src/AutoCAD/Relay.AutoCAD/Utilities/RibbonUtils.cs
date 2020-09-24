using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Relay.AutoCAD;
using Relay.AutoCAD.Utilities;
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

        public static AW.RibbonButton CreateButton(string name, string text, ICommand commandHandler, string commandParameter, string largeImagePath, string smallImagePath = "")
        {
            Autodesk.Windows.RibbonButton newButton = new Autodesk.Windows.RibbonButton
            {
                Name = name,
                Text = text,
                CommandHandler = commandHandler,
                CommandParameter = commandParameter
            };
            Bitmap bm = new Bitmap(largeImagePath);
            IntPtr hBitmap = bm.GetHbitmap();
            newButton.LargeImage = GetImage(hBitmap);
            newButton.ShowImage = true;
            newButton.ShowText = true;
            newButton.Size = AW.RibbonItemSize.Large;
            newButton.Orientation = Orientation.Vertical;

            return newButton;
        }
        private static BitmapSource GetImage(IntPtr bm)
        {
            return Imaging.CreateBitmapSourceFromHBitmap(bm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        public static void AddItems(AW.RibbonPanelSource panelToUse, string[] dynPaths)
        {
            var totalFiles = dynPaths.Length;

            //List<AW.RibbonButton> pushButtonDatas = new List<AW.RibbonButton>();
            foreach (var file in dynPaths)
            {
                FileInfo fInfo = new FileInfo(file);

                //set the images, if there are none, use default
                string icon32 = fInfo.FullName.Replace(".dyn", "_32.png");
                string largeImage = File.Exists(icon32)
                    ? icon32
                    : Path.Combine(Globals.RelayGraphs, "Dynamo_32.png");

                string icon16 = fInfo.FullName.Replace(".dyn", "_16.png");
                string smallImage = File.Exists(icon16)
                    ? icon16
                    : Path.Combine(Globals.RelayGraphs, "Dynamo_16.png");

                var newButton = CreateButton($"relay{fInfo.Name.Replace(" ", "")}", fInfo.Name.GenerateButtonText(),
                    new RunDynamoGraphCommand(), fInfo.FullName, largeImage, smallImage);

                panelToUse.Items.Add(newButton);

                //pushButtonDatas.Add(newButton);
            }

            //var splitButtons = SplitList(pushButtonDatas, 2);

            //foreach (var buttonGroup in splitButtons)
            //{
            //    switch (buttonGroup.Count)
            //    {
            //        case 2:
            //            panelToUse.AddStackedItems(buttonGroup[0], buttonGroup[1]);
            //            break;
            //        case 1:
            //            panelToUse.AddItem(buttonGroup[0]);
            //            break;
            //    }
            //}
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
