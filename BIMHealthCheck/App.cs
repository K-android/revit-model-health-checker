using Autodesk.Revit.UI;
using System;
using System.Reflection;

namespace BIMHealthCheck
{
    /// <summary>
    /// Main application class for the BIM Health Check add-in.
    /// </summary>
    public class App : IExternalApplication
    {
        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                string tabName = "BIM Health";
                application.CreateRibbonTab(tabName);

                string panelName = "Analysis";
                RibbonPanel panel = application.CreateRibbonPanel(tabName, panelName);

                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                PushButtonData buttonData = new PushButtonData(
                    "cmdHealthCheck",
                    "Health\nCheck",
                    assemblyPath,
                    "BIMHealthCheck.Commands.HealthCheckCommand")
                {
                    ToolTip = "Run a comprehensive health check on the current model"
                };

                panel.AddItem(buttonData);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Called when Revit shuts down.
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
