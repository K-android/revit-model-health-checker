using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMHealthCheck.Engine;
using BIMHealthCheck.UI;
using System;

namespace BIMHealthCheck.Commands
{
    /// <summary>
    /// Command to execute the health check on the current model.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class HealthCheckCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the external command.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc?.Document;

                if (doc == null || !doc.IsValidObject)
                {
                    TaskDialog.Show("Error", "No valid document is currently active.");
                    return Result.Failed;
                }

                HealthCheckEngine engine = new HealthCheckEngine();
                HealthScore score = engine.RunAllChecks(doc);

                HealthCheckDialog dialog = new HealthCheckDialog(score);
                dialog.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"An unexpected error occurred: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}
