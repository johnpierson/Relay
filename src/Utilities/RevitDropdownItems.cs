using Relay.Classes;

namespace Relay.Utilities
{
    /// <summary>
    /// Populates dropdown input choices that require live Revit document data
    /// (e.g. Categories).  Kept separate from <see cref="DynamoInputParser"/> so the
    /// parser remains free of Revit API dependencies and unit testable.
    /// </summary>
    internal static class RevitDropdownItems
    {
        /// <summary>
        /// Populates <see cref="DynamoInputDefinition.Items"/> for dropdown inputs that
        /// require live Revit document data.  Call this after
        /// <see cref="DynamoInputParser.ParseGraphInputs"/> when a Revit document is
        /// available.  The method is a no-op when <paramref name="doc"/> is <c>null</c>.
        /// </summary>
        /// <param name="graphInputs">The parsed graph inputs to enrich.</param>
        /// <param name="doc">The active Revit document, or <c>null</c> to skip.</param>
        public static void Populate(DynamoGraphInputs graphInputs, Autodesk.Revit.DB.Document doc)
        {
            if (graphInputs == null || doc == null) return;

            foreach (var input in graphInputs.Inputs)
            {
                if (input.DataType != DynamoInputType.Dropdown || input.Items.Count > 0)
                    continue; // already populated or not a dropdown

                var typeName = input.NodeType?.Split(',')[0].Trim().ToLowerInvariant() ?? string.Empty;

                if (typeName.Contains("categories"))
                    PopulateCategoryItems(input, doc);
                // Additional type-specific population can be added here in future
            }
        }

        private static void PopulateCategoryItems(DynamoInputDefinition input, Autodesk.Revit.DB.Document doc)
        {
            try
            {
                // Build parallel lists: display name (shown in UI) and OST enum name (written
                // back to Nodes[].SelectedString / Inputs[].Value).
                // Dynamo's Categories node serialises SelectedString as the BuiltInCategory
                // enum name (e.g. "OST_Walls"), not the display name ("Walls").
                var pairs = new List<(string DisplayName, string OstName)>();

                foreach (Autodesk.Revit.DB.Category cat in doc.Settings.Categories)
                {
                    if (cat == null || string.IsNullOrEmpty(cat.Name)) continue;

                    // Include only built-in categories (negative integer IDs) to match the set
                    // that Dynamo's DSRevitNodesUI.Categories node enumerates.  User-defined
                    // (family/project) categories have positive IDs and are not in Dynamo's list.
                    var catIdLong = cat.Id.Value;
                    if (catIdLong >= 0) continue;

                    // Get the BuiltInCategory enum name (e.g. "OST_Walls").
                    string ostName;
                    try
                    {
                        var bic = (Autodesk.Revit.DB.BuiltInCategory)(int)catIdLong;
                        ostName = bic.ToString();
                    }
                    catch
                    {
                        // Fallback: use display name if enum cast fails
                        ostName = cat.Name;
                    }

                    pairs.Add((cat.Name, ostName));
                }

                // Sort alphabetically by display name (matches Dynamo's ordering)
                pairs.Sort((a, b) =>
                    string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

                input.Items.AddRange(pairs.Select(p => p.DisplayName));
                input.ItemValues.AddRange(pairs.Select(p => p.OstName));

                // Sync SelectedIndex to OUR sorted list's position.
                // input.SelectedString is the OST name (e.g. "OST_Walls") from the .dyn file.
                // We match against ItemValues (OST names), not display names.
                if (!string.IsNullOrEmpty(input.SelectedString))
                {
                    // Prefer exact OST-name match in ItemValues
                    var idx = input.ItemValues.FindIndex(v =>
                        string.Equals(v, input.SelectedString, StringComparison.OrdinalIgnoreCase));

                    // Fallback: try matching against display names (just in case)
                    if (idx < 0)
                        idx = input.Items.FindIndex(s =>
                            string.Equals(s, input.SelectedString, StringComparison.OrdinalIgnoreCase));

                    if (idx < 0)
                        System.Diagnostics.Trace.WriteLine(
                            $"[Relay] Category '{input.SelectedString}' not found in the document's " +
                            "built-in categories — the ComboBox will show no pre-selection.");

                    input.SelectedIndex = idx; // -1 if not found
                }
                else if (input.SelectedIndex >= 0 && input.SelectedIndex < input.Items.Count)
                {
                    // No SelectedString available — derive the OST name from our sorted list
                    input.SelectedString = input.ItemValues[input.SelectedIndex];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Failed to populate category items: {ex.Message}");
            }
        }
    }
}
