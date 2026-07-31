using System.IO;
using System.Text.Json;
using Relay.Classes;

namespace Relay.Utilities
{
    /// <summary>
    /// Discovers the nodes a graph author marked as outputs. Pure JSON processing, with no
    /// Revit or Dynamo dependency, so it can be unit tested outside a Revit host.
    /// </summary>
    internal static class DynamoOutputParser
    {
        /// <summary>
        /// Reads a .dyn file and returns the nodes marked <c>IsSetAsOutput</c>.
        /// </summary>
        /// <remarks>
        /// As with inputs, the flag lives in <c>View.NodeViews[]</c> rather than on the node
        /// itself; the top-level <c>Outputs[]</c> array carries the display name and
        /// description.
        /// </remarks>
        public static DynamoGraphOutputs ParseGraphOutputs(string graphPath)
        {
            var result = new DynamoGraphOutputs();

            try
            {
                var json = File.ReadAllText(graphPath, System.Text.Encoding.UTF8);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                result.GraphName = root.TryGetProperty("Name", out var nameProp)
                    ? nameProp.GetString() ?? "Graph"
                    : "Graph";

                if (!root.TryGetProperty("View", out var view) || view.ValueKind != JsonValueKind.Object)
                    return result;

                // Which nodes are declared outputs, in authoring order
                var outputNodeIds = new List<string>();
                var nodeViewNames = new Dictionary<string, string>(StringComparer.Ordinal);

                if (view.TryGetProperty("NodeViews", out var nodeViews) &&
                    nodeViews.ValueKind == JsonValueKind.Array)
                {
                    foreach (var nodeView in nodeViews.EnumerateArray())
                    {
                        if (!nodeView.TryGetProperty("IsSetAsOutput", out var isOutput) ||
                            isOutput.ValueKind != JsonValueKind.True)
                            continue;

                        var id = nodeView.TryGetProperty("Id", out var idProp) ? idProp.GetString() : null;
                        if (id == null) continue;
                        if (outputNodeIds.Contains(id, StringComparer.Ordinal)) continue;

                        outputNodeIds.Add(id);
                        if (nodeView.TryGetProperty("Name", out var nvName))
                            nodeViewNames[id] = nvName.GetString() ?? string.Empty;
                    }
                }

                if (outputNodeIds.Count == 0)
                    return result;

                // Names and descriptions from the top-level Outputs array
                var metadata = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                if (root.TryGetProperty("Outputs", out var outputs) && outputs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var output in outputs.EnumerateArray())
                    {
                        var id = output.TryGetProperty("Id", out var idProp) ? idProp.GetString() : null;
                        if (id != null && outputNodeIds.Contains(id, StringComparer.Ordinal))
                            metadata[id] = output;
                    }
                }

                foreach (var id in outputNodeIds)
                {
                    metadata.TryGetValue(id, out var meta);
                    nodeViewNames.TryGetValue(id, out var nodeViewName);

                    string name = "Output";
                    if (meta.ValueKind == JsonValueKind.Object &&
                        meta.TryGetProperty("Name", out var metaName) &&
                        !string.IsNullOrWhiteSpace(metaName.GetString()))
                        name = metaName.GetString();
                    else if (!string.IsNullOrWhiteSpace(nodeViewName))
                        name = nodeViewName;

                    string description = null;
                    if (meta.ValueKind == JsonValueKind.Object &&
                        meta.TryGetProperty("Description", out var metaDescription))
                        description = metaDescription.GetString();

                    result.Outputs.Add(new DynamoOutputDefinition
                    {
                        Id = id,
                        Name = name,
                        Description = description
                    });
                }

                ApplyGroupNames(view, result.Outputs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Failed to parse graph outputs from '{graphPath}': {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Assigns the containing Dynamo group's title to each output so the results window
        /// can group them the same way the input dialog groups inputs.
        /// </summary>
        private static void ApplyGroupNames(JsonElement view, List<DynamoOutputDefinition> outputs)
        {
            if (!view.TryGetProperty("Annotations", out var annotations) ||
                annotations.ValueKind != JsonValueKind.Array)
                return;

            var nodeGroups = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var annotation in annotations.EnumerateArray())
            {
                var title = annotation.TryGetProperty("Title", out var titleProp)
                    ? titleProp.GetString() ?? string.Empty
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(title)) continue;

                if (!annotation.TryGetProperty("Nodes", out var groupNodes) ||
                    groupNodes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var nodeId in groupNodes.EnumerateArray())
                {
                    var id = nodeId.GetString();
                    // First annotation wins, matching the input parser
                    if (!string.IsNullOrEmpty(id) && !nodeGroups.ContainsKey(id))
                        nodeGroups[id] = title;
                }
            }

            foreach (var output in outputs)
            {
                if (nodeGroups.TryGetValue(output.Id, out var groupName))
                    output.GroupName = groupName;
            }
        }
    }
}
