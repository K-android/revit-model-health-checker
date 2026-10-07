using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace BIMHealthCheck.Utilities
{
    /// <summary>
    /// Static utility class with helper methods for common Revit data retrieval.
    /// </summary>
    public static class RevitHelpers
    {
        /// <summary>
        /// Retrieves all non-type elements in the document.
        /// </summary>
        public static IEnumerable<Element> GetAllElements(Document doc)
        {
            return new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements();
        }

        /// <summary>
        /// Retrieves all type elements in the document.
        /// </summary>
        public static IEnumerable<Element> GetAllElementTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .ToElements();
        }

        /// <summary>
        /// Gets the total count of all elements in the document.
        /// </summary>
        public static int GetElementCount(Document doc)
        {
            return new FilteredElementCollector(doc)
                .GetElementCount();
        }

        /// <summary>
        /// Gets the count of all views in the document, excluding view templates.
        /// </summary>
        public static int GetViewCount(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Count(v => !v.IsTemplate);
        }

        /// <summary>
        /// Gets the count of all view sheets in the document.
        /// </summary>
        public static int GetSheetCount(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .GetElementCount();
        }

        /// <summary>
        /// Gets the count of all families in the document.
        /// </summary>
        public static int GetFamilyCount(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .GetElementCount();
        }

        /// <summary>
        /// Formats a file size in bytes to a human-readable string (KB, MB, GB).
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            int i = 0;
            double size = bytes;
            while (size >= 1024 && i < suffixes.Length - 1)
            {
                size /= 1024;
                i++;
            }
            return $"{size:0.##} {suffixes[i]}";
        }

        /// <summary>
        /// Checks if a given view is a view template.
        /// </summary>
        public static bool IsViewTemplate(View view)
        {
            return view != null && view.IsTemplate;
        }
    }
}
