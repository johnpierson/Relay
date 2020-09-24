// (C) Copyright 2020 by  
//
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Windows;
using Relay.Utilities;

// This line is not mandatory, but improves loading performances
[assembly: ExtensionApplication(typeof(Relay.AutoCAD.MyPlugin))]

namespace Relay.AutoCAD
{

    // This class is instantiated by AutoCAD once and kept alive for the 
    // duration of the session. If you don't do any one time initialization 
    // then you should remove this class.
    public class MyPlugin : IExtensionApplication
    {

        void IExtensionApplication.Initialize()
        {
            // Add one time initialization here
            // One common scenario is to setup a callback function here that 
            // unmanaged code can call. 
            // To do this:
            // 1. Export a function from unmanaged code that takes a function
            //    pointer and stores the passed in value in a global variable.
            // 2. Call this exported function in this function passing delegate.
            // 3. When unmanaged code needs the services of this managed module
            //    you simply call acrxLoadApp() and by the time acrxLoadApp 
            //    returns  global function pointer is initialized to point to
            //    the C# delegate.
            // For more info see: 
            // http://msdn2.microsoft.com/en-US/library/5zwkzwf4(VS.80).aspx
            // http://msdn2.microsoft.com/en-us/library/44ey4b32(VS.80).aspx
            // http://msdn2.microsoft.com/en-US/library/7esfatk4.aspx
            // as well as some of the existing AutoCAD managed apps.

            // Initialize your plug-in application here

            //create the about button
            Autodesk.Windows.RibbonButton aboutButton = RibbonUtils.CreateButton("AboutRelay", "About\nRelay",
                new AboutRelayCommand(), "",
                @"C:\Users\johnpierson\Documents\Repos\Relay\RelayGraphs\About_32.png");
            Autodesk.Windows.RibbonButton syncButton = RibbonUtils.CreateButton("SyncGraphs", "Sync\nGraphs",
                new AboutRelayCommand(), "",
                @"C:\Users\johnpierson\Documents\Repos\Relay\RelayGraphs\Sync_32.png");


            //build ribbon panel source, add the buttons and create the ribbon panel
            RibbonPanelSource ribbonPanelSource = new RibbonPanelSource {Title = "Setup"};
            ribbonPanelSource.Items.Add(aboutButton);
            ribbonPanelSource.Items.Add(syncButton);
            RibbonPanel ribbonPanel = new RibbonPanel {Source = ribbonPanelSource};
            
            //create the tab and add the panels
            RibbonTab tab = new RibbonTab {Title = "Relay", Id = "RelayAutoCAD"};
            tab.Panels.Add(ribbonPanel);

            //add the tab to the ribbon
            Autodesk.Windows.RibbonControl ribbon = Autodesk.Windows.ComponentManager.Ribbon;
            ribbon.Tabs.Add(tab);
        }
        
        void IExtensionApplication.Terminate()
        {
            // Do plug-in application clean up here
        }

    }

}
