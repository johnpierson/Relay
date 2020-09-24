using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Windows;


namespace Relay.AutoCAD
{
    public class RunDynamoGraphCommand : System.Windows.Input.ICommand
    {
        public bool CanExecute(object parameter)
        {
            return true;
        }
        public void Execute(object parameter)
        {

            RibbonCommandItem btn = parameter as RibbonCommandItem;

            if (btn != null)

            {


            }

        }

        public event EventHandler CanExecuteChanged;
    }
    public class AboutRelayCommand : System.Windows.Input.ICommand
    {
        public bool CanExecute(object parameter)
        {
            return true;
        }
        public void Execute(object parameter)
        {

            RibbonCommandItem btn = parameter as RibbonCommandItem;

            if (btn != null)

            {


            }

        }

        public event EventHandler CanExecuteChanged;
    }


}
