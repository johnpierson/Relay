using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Relay.Classes;

namespace Relay.Utilities
{
    /// <summary>
    /// Parses Dynamo .dyn graph files to extract input node definitions,
    /// and applies user-provided values back to a modified copy of the graph.
    /// </summary>
    internal static class DynamoInputParser
    {
        /// <summary>
        /// Reads a .dyn file and returns all nodes marked as IsSetAsInput.
        /// </summary>
        /// <param name="graphPath">Absolute path to the .dyn file.</param>
        /// <returns>A <see cref="DynamoGraphInputs"/> describing the detected inputs.</returns>
        public static DynamoGraphInputs ParseGraphInputs(string graphPath)
        {
            var result = new DynamoGraphInputs { GraphPath = graphPath };

            try
            {
                var json = File.ReadAllText(graphPath, System.Text.Encoding.UTF8);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                result.GraphName = root.TryGetProperty("Name", out var nameProp)
                    ? nameProp.GetString() ?? "Graph"
                    : "Graph";

                if (!root.TryGetProperty("Nodes", out var nodes))
                    return result;

                foreach (var node in nodes.EnumerateArray())
                {
                    if (!node.TryGetProperty("IsSetAsInput", out var isInputProp))
                        continue;
                    if (isInputProp.ValueKind != JsonValueKind.True)
                        continue;

                    var input = ExtractInputDefinition(node);
                    if (input != null)
                        result.Inputs.Add(input);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Failed to parse graph inputs from '{graphPath}': {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Applies user-provided values to a copy of the graph and returns the path of the
        /// new temporary file. The caller is responsible for deleting the temp file.
        /// </summary>
        /// <param name="graphPath">Original .dyn file path.</param>
        /// <param name="userValues">Mapping from node ID to new value.</param>
        /// <returns>Path to the modified temporary .dyn file.</returns>
        public static string ApplyUserValues(string graphPath, Dictionary<string, object> userValues)
        {
            try
            {
                var json = File.ReadAllText(graphPath, System.Text.Encoding.UTF8);
                var root = JsonNode.Parse(json);

                if (root == null)
                    return graphPath;

                var nodes = root["Nodes"]?.AsArray();
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        if (node == null) continue;

                        var id = node["Id"]?.GetValue<string>();
                        if (id == null || !userValues.TryGetValue(id, out var value)) continue;

                        var concreteType = node["ConcreteType"]?.GetValue<string>() ?? string.Empty;
                        ApplyValueToNode(node, value, concreteType);
                    }
                }

                var tempPath = Path.Combine(Path.GetTempPath(), $"relay_modified_{Guid.NewGuid()}.dyn");
                File.WriteAllText(tempPath, root.ToJsonString(), System.Text.Encoding.UTF8);
                return tempPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Failed to apply user values to graph: {ex.Message}");
                return graphPath;
            }
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private static DynamoInputDefinition ExtractInputDefinition(JsonElement node)
        {
            var id = node.TryGetProperty("Id", out var idProp)
                ? idProp.GetString() ?? Guid.NewGuid().ToString()
                : Guid.NewGuid().ToString();

            var name = node.TryGetProperty("Name", out var nameProp)
                ? nameProp.GetString() ?? "Input"
                : "Input";

            var concreteType = node.TryGetProperty("ConcreteType", out var typeProp)
                ? typeProp.GetString() ?? string.Empty
                : string.Empty;

            var inputType = DetermineInputType(concreteType);
            var defaultValue = ExtractDefaultValue(node, inputType);

            var input = new DynamoInputDefinition
            {
                Id = id,
                Name = name,
                NodeType = concreteType,
                DataType = inputType,
                DefaultValue = defaultValue
            };

            // Extract slider range constraints
            if (inputType == DynamoInputType.Number || inputType == DynamoInputType.Integer)
            {
                if (node.TryGetProperty("Minimum", out var min) && min.ValueKind == JsonValueKind.Number)
                    input.MinValue = min.GetDouble();
                if (node.TryGetProperty("Maximum", out var max) && max.ValueKind == JsonValueKind.Number)
                    input.MaxValue = max.GetDouble();
                if (node.TryGetProperty("Step", out var step) && step.ValueKind == JsonValueKind.Number)
                    input.StepValue = step.GetDouble();
            }

            return input;
        }

        private static DynamoInputType DetermineInputType(string concreteType)
        {
            if (string.IsNullOrEmpty(concreteType))
                return DynamoInputType.Unknown;

            // Normalise: take only the type name part (before the assembly comma)
            var typeName = concreteType.Split(',')[0].Trim().ToLowerInvariant();

            if (typeName.Contains("doubleslider") || typeName.Contains("doublenuminput"))
                return DynamoInputType.Number;

            if (typeName.Contains("integerslider") || typeName.Contains("integernuminput"))
                return DynamoInputType.Integer;

            if (typeName.Contains("boolselector") || typeName.Contains("boolean"))
                return DynamoInputType.Boolean;

            if (typeName.Contains("stringinput") || typeName.Contains("symbol"))
                return DynamoInputType.String;

            // Code block nodes can contain any type – treat as unknown (text input)
            return DynamoInputType.Unknown;
        }

        private static object ExtractDefaultValue(JsonElement node, DynamoInputType type)
        {
            // Most built-in input nodes (StringInput, BoolSelector, sliders) store their
            // current value in an "InputValue" property.
            if (node.TryGetProperty("InputValue", out var inputValue))
            {
                switch (inputValue.ValueKind)
                {
                    case JsonValueKind.True:
                        return true;
                    case JsonValueKind.False:
                        return false;
                    case JsonValueKind.Number:
                        return type == DynamoInputType.Integer
                            ? (object)inputValue.GetInt32()
                            : inputValue.GetDouble();
                    case JsonValueKind.String:
                        return inputValue.GetString();
                    default:
                        return inputValue.ToString();
                }
            }

            // Code block nodes store their value in the "Code" property
            if (node.TryGetProperty("Code", out var code))
                return ExtractCodeValue(code.GetString() ?? string.Empty);

            // Fall back to sensible defaults per type
            switch (type)
            {
                case DynamoInputType.Boolean: return false;
                case DynamoInputType.Number:  return 0.0;
                case DynamoInputType.Integer: return 0;
                default:                      return string.Empty;
            }
        }

        /// <summary>
        /// Strips trailing semicolons and surrounding string quotes from a code-block value.
        /// e.g. <c>"hello";</c> → <c>hello</c>, <c>42;</c> → <c>42</c>
        /// </summary>
        private static string ExtractCodeValue(string code)
        {
            var trimmed = code.Trim().TrimEnd(';').Trim();
            if (trimmed.StartsWith("\"", StringComparison.Ordinal) &&
                trimmed.EndsWith("\"", StringComparison.Ordinal) &&
                trimmed.Length >= 2)
            {
                return trimmed.Substring(1, trimmed.Length - 2)
                              .Replace("\\\"", "\"")
                              .Replace("\\\\", "\\");
            }
            return trimmed;
        }

        private static void ApplyValueToNode(JsonNode node, object value, string concreteType)
        {
            var typeName = concreteType.Split(',')[0].Trim().ToLowerInvariant();

            if (typeName.Contains("codeblock"))
            {
                // Code block: update the "Code" property
                node["Code"] = JsonValue.Create(FormatAsCode(value));
            }
            else
            {
                // All other input nodes: update the "InputValue" property
                switch (value)
                {
                    case bool boolVal:
                        node["InputValue"] = JsonValue.Create(boolVal);
                        break;
                    case double doubleVal:
                        node["InputValue"] = JsonValue.Create(doubleVal);
                        break;
                    case int intVal:
                        node["InputValue"] = JsonValue.Create(intVal);
                        break;
                    case string strVal:
                        node["InputValue"] = JsonValue.Create(strVal);
                        break;
                    default:
                        node["InputValue"] = JsonValue.Create(value?.ToString() ?? string.Empty);
                        break;
                }
            }
        }

        /// <summary>
        /// Formats a user-supplied value as a Dynamo code-block expression string
        /// (e.g. <c>42;</c> or <c>"hello";</c>).
        /// </summary>
        private static string FormatAsCode(object value)
        {
            switch (value)
            {
                case bool boolVal:
                    return (boolVal ? "true" : "false") + ";";
                case double doubleVal:
                    return doubleVal.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";";
                case int intVal:
                    return intVal.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";";
                case string strVal:
                    var escaped = strVal.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    return $"\"{escaped}\";";
                default:
                    return (value?.ToString() ?? string.Empty) + ";";
            }
        }
    }
}
