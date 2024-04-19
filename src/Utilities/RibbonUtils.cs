using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using AW = Autodesk.Windows;
using System.Text.Json;
using System.Windows.Input;
using Relay.Classes;
using System.Reflection;
using Autodesk.Revit.DB.Electrical;

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
            var assembly = Assembly.GetExecutingAssembly();
            var totalFiles = dynPaths.Length;

            List<PushButtonData> pushButtonDatas = new List<PushButtonData>();
            foreach (var file in dynPaths)
            {
                //generate file info to get the datas
                FileInfo fInfo = new FileInfo(file);

                string tooltip = GetDescription(fInfo);

                string buttonName = $"relay{fInfo.Name.Replace(" ", "")}?{Guid.NewGuid()}";

                PushButtonData newButtonData = new PushButtonData(buttonName,
                    fInfo.Name.GenerateButtonText(),
                    Path.Combine(Globals.ExecutingPath, "Relay.dll"), "Relay.Run")
                {
                    ToolTip = tooltip
                };
              
                //set the images, if there are none, use default
                string icon32 = fInfo.FullName.Replace(".dyn", "_32.png");

                //trying out using temp icons to enable button hiding and deleting later
                if (File.Exists(icon32))
                {
                    var temp32 = Path.Combine(Globals.UserTemp, $"relayImage{Guid.NewGuid()}.png");
                    File.Copy(icon32, temp32);
                    icon32 = temp32;
                }
               
                newButtonData.LargeImage = File.Exists(icon32)
                    ? new BitmapImage(new Uri(icon32))
                    : ImageUtils.LoadImage(assembly, "Dynamo_32.png");

                string icon16 = fInfo.FullName.Replace(".dyn", "_16.png");

                //trying out using temp icons to enable button hiding and deleting later
                if (File.Exists(icon16))
                {
                    var temp16 = Path.Combine(Globals.UserTemp, $"relayImage{Guid.NewGuid()}.png");
                    File.Copy(icon16, temp16);
                    icon16 = temp16;
                }

                newButtonData.Image = File.Exists(icon16)
                    ? new BitmapImage(new Uri(icon16))
                    : ImageUtils.LoadImage(assembly, "Dynamo_16.png");

                TrySetContextualHelp(newButtonData, fInfo);

                pushButtonDatas.Add(newButtonData);
            }

            if (!panelToUse.Visible)
            {
                panelToUse.Visible = true;
            }

            if (forceLargeIcon)
            {
                foreach (var pushButton in pushButtonDatas)
                {
                    panelToUse.AddItem(pushButton);
                }
                return;
            }

            List<RibbonItem> createdButtons = new List<RibbonItem>();

            var splitButtons = SplitList(pushButtonDatas, 2);

            foreach (var buttonGroup in splitButtons)
            {
                switch (buttonGroup.Count)
                {
                    case 2:
                        var stack = panelToUse.AddStackedItems(buttonGroup[0], buttonGroup[1]);
                        createdButtons.Add(stack[0]);
                        createdButtons.Add(stack[1]);
                        break;
                    case 1:
                        var singleButton = panelToUse.AddItem(buttonGroup[0]);
                        createdButtons.Add(singleButton);
                        break;
                }
            }

            foreach (var ribbonItem in createdButtons)
            {
                Globals.RelayButtons.Add(ribbonItem.ToolTip.GetStringBetweenCharacters('[', ']'), ribbonItem);
            }

            //now add the buttons to our panel dictionary for later
            if (Globals.RelayPanels.ContainsKey(panelToUse.Name))
            {
                Globals.RelayPanels[panelToUse.Name] = createdButtons;
            }
            else
            {
                Globals.RelayPanels.Add(panelToUse.Name, createdButtons);
            }
          
        }

        public static void HideUnused()
        {
            foreach (var key in Globals.RelayButtons.Keys)
            {
                Globals.RelayButtons.TryGetValue(key, out RibbonItem ribbonItem);

                if (ribbonItem != null)
                {
                    var path = ribbonItem.ToolTip.GetStringBetweenCharacters('[', ']');

                    //check if the file exists, if not, hide the button and remove the button from our dictionary
                    if (File.Exists(path)) continue;
                    ribbonItem.Visible = false;

                    Globals.RelayButtons.Remove(key);
                }
            }

            //now hide empty panels if there are any
            foreach (var panel in Globals.RelayPanels.Keys)
            {
                var worked = Globals.RelayPanels.TryGetValue(panel, out List<RibbonItem> ribbonItems);
                if (worked)
                {
                    var allHidden = ribbonItems.All(r => !r.Visible);

                    if (allHidden)
                    {
                        //k, all the buttons are hidden. get the panel to hide now
                        var ribbonPanel = GetPanel(Globals.RibbonTabName, panel);
                        ribbonPanel.IsVisible = false;
                    }
                    
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
            // rescan the potential tab directory
            Globals.PotentialTabDirectories = Directory.GetDirectories(Globals.BasePath);

            // no tabs found, exit
            if (!Globals.PotentialTabDirectories.Any())
            {
                return;
            }

            // iterate through all sub folders
            foreach (var potentialTabDirectory in Globals.PotentialTabDirectories)
            {
                // current tab name
                string potentialTab = new DirectoryInfo(potentialTabDirectory).Name;

                try
                {
                    // Create a custom ribbon tab
                    uiapp.CreateRibbonTab(potentialTab);
                }
                catch
                {
                    // Might Already Exist
                }


                //check if a specific Revit version directory exists, if it does use that.
                string[] graphDirectories = Directory.GetDirectories(potentialTabDirectory);
                var versionDirectories = Directory.GetDirectories(potentialTabDirectory);

                if (versionDirectories.Any(d => new DirectoryInfo(d).Name.Contains(Globals.RevitVersion)))
                {
                    var versionDirectory = versionDirectories.FirstOrDefault(d => new DirectoryInfo(d).Name.Contains(Globals.RevitVersion));

                    if (versionDirectory != null)
                    {
                        graphDirectories = Directory.GetDirectories(versionDirectory);
                    }
                }

                //create the panels for the sub directories
                foreach (var directory in graphDirectories)
                {
                    //the upper folder name (panel name)
                    DirectoryInfo dInfo = new DirectoryInfo(directory);

                    Autodesk.Revit.UI.RibbonPanel panelToUse;

                    //try to create the panel, if it already exists, just use it
                    try
                    {
                        panelToUse = uiapp.CreateRibbonPanel(potentialTab, dInfo.Name);
                        panelToUse.Title = dInfo.Name;
                    }
                    catch (Exception)
                    {
                        panelToUse = uiapp.GetRibbonPanels(potentialTab).First(p => p.Name.Equals(dInfo.Name));

                        panelToUse.Visible = true;
                    }

                    //find the files that do not have a button yet
                   
                    List<string> toCreate = new List<string>();

                    foreach (var file in Directory.GetFiles(directory, "*.dyn"))
                    {
                        FileInfo fInfo = new FileInfo(file);

                        var dynPath = fInfo.FullName;

                        if (!Globals.RelayButtons.ContainsKey(dynPath))
                        {
                            toCreate.Add(file);
                        }
                    }


                    //if the user is holding down the left shift key, then force the large icons
                    bool forceLargeIcons = Keyboard.IsKeyDown(Key.LeftShift);

                    if (toCreate.Any())
                    {
                        RibbonUtils.AddItems(panelToUse, toCreate.ToArray(), forceLargeIcons);
                    }
                }
            }
        }

    }
}
