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
        /// <remarks>
        /// In the Dynamo JSON format, <c>IsSetAsInput</c> is stored inside
        /// <c>View.NodeViews[]</c>, not on the node objects themselves.
        /// The top-level <c>Inputs[]</c> array provides the display name, description,
        /// type string, and range metadata.  The <c>Nodes[]</c> array provides the
        /// current typed <c>InputValue</c> and the concrete node type.
        /// </remarks>
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

                // ----------------------------------------------------------------
                // Step 1: Find which node IDs are marked as input via View.NodeViews
                // ----------------------------------------------------------------
                var inputNodeIds = new List<string>();
                var nodeViewNames = new Dictionary<string, string>(StringComparer.Ordinal);

                if (root.TryGetProperty("View", out var view) &&
                    view.TryGetProperty("NodeViews", out var nodeViews))
                {
                    foreach (var nodeView in nodeViews.EnumerateArray())
                    {
                        if (!nodeView.TryGetProperty("IsSetAsInput", out var isInputProp) ||
                            isInputProp.ValueKind != JsonValueKind.True)
                            continue;

                        var id = nodeView.TryGetProperty("Id", out var idProp)
                            ? idProp.GetString()
                            : null;
                        if (id == null) continue;

                        inputNodeIds.Add(id);

                        if (nodeView.TryGetProperty("Name", out var nvNameProp))
                            nodeViewNames[id] = nvNameProp.GetString() ?? string.Empty;
                    }
                }

                if (inputNodeIds.Count == 0)
                    return result;

                var inputNodeIdSet = new HashSet<string>(inputNodeIds, StringComparer.Ordinal);

                // ----------------------------------------------------------------
                // Step 2: Build metadata lookup from the top-level Inputs array
                // (Name, Description, Type string, range values)
                // ----------------------------------------------------------------
                var inputMetadata = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                if (root.TryGetProperty("Inputs", out var inputs))
                {
                    foreach (var input in inputs.EnumerateArray())
                    {
                        var id = input.TryGetProperty("Id", out var idProp)
                            ? idProp.GetString()
                            : null;
                        if (id != null && inputNodeIdSet.Contains(id))
                            inputMetadata[id] = input;
                    }
                }

                // ----------------------------------------------------------------
                // Step 3: Build node data lookup from the Nodes array
                // (ConcreteType, InputValue, and fallback range values)
                // ----------------------------------------------------------------
                var nodeData = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                if (root.TryGetProperty("Nodes", out var nodes))
                {
                    foreach (var node in nodes.EnumerateArray())
                    {
                        var id = node.TryGetProperty("Id", out var idProp)
                            ? idProp.GetString()
                            : null;
                        if (id != null && inputNodeIdSet.Contains(id))
                            nodeData[id] = node;
                    }
                }

                // ----------------------------------------------------------------
                // Step 4: Build DynamoInputDefinition for each input node,
                // preserving the order from NodeViews
                // ----------------------------------------------------------------
                foreach (var id in inputNodeIds)
                {
                    nodeData.TryGetValue(id, out var node);
                    inputMetadata.TryGetValue(id, out var meta);
                    nodeViewNames.TryGetValue(id, out var nodeViewName);

                    var definition = BuildInputDefinition(id, node, meta, nodeViewName);
                    if (definition != null)
                        result.Inputs.Add(definition);
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

                // Update InputValue in the Nodes array
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

                // Also update Value in the top-level Inputs array (string representation)
                var inputsArray = root["Inputs"]?.AsArray();
                if (inputsArray != null)
                {
                    foreach (var input in inputsArray)
                    {
                        if (input == null) continue;

                        var id = input["Id"]?.GetValue<string>();
                        if (id == null || !userValues.TryGetValue(id, out var value)) continue;

                        input["Value"] = JsonValue.Create(FormatInputsArrayValue(value));
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

        private static DynamoInputDefinition BuildInputDefinition(
            string id,
            JsonElement node,
            JsonElement meta,
            string nodeViewName)
        {
            // --- Name (priority: top-level Inputs > NodeViews > fallback) ---
            string name = "Input";
            if (meta.ValueKind == JsonValueKind.Object &&
                meta.TryGetProperty("Name", out var metaName) &&
                !string.IsNullOrWhiteSpace(metaName.GetString()))
            {
                name = metaName.GetString()!;
            }
            else if (!string.IsNullOrWhiteSpace(nodeViewName))
            {
                name = nodeViewName;
            }

            // --- Description ---
            string description = null;
            if (meta.ValueKind == JsonValueKind.Object &&
                meta.TryGetProperty("Description", out var metaDesc))
                description = metaDesc.GetString();
            if (string.IsNullOrWhiteSpace(description) &&
                node.ValueKind == JsonValueKind.Object &&
                node.TryGetProperty("Description", out var nodeDesc))
                description = nodeDesc.GetString();

            // --- ConcreteType from Nodes array ---
            var concreteType = string.Empty;
            if (node.ValueKind == JsonValueKind.Object &&
                node.TryGetProperty("ConcreteType", out var ctProp))
                concreteType = ctProp.GetString() ?? string.Empty;

            // --- Type: use top-level Inputs[].Type string first, then ConcreteType ---
            var inputType = DynamoInputType.Unknown;
            if (meta.ValueKind == JsonValueKind.Object &&
                meta.TryGetProperty("Type", out var typeProp))
                inputType = DetermineInputTypeFromString(typeProp.GetString());

            if (inputType == DynamoInputType.Unknown && !string.IsNullOrEmpty(concreteType))
                inputType = DetermineInputTypeFromConcreteType(concreteType);

            // --- Default value from Nodes[].InputValue ---
            var defaultValue = ExtractDefaultValue(node, inputType);

            var definition = new DynamoInputDefinition
            {
                Id = id,
                Name = name,
                Description = description,
                NodeType = concreteType,
                DataType = inputType,
                DefaultValue = defaultValue
            };

            // --- Range metadata (MinimumValue / MaximumValue / StepValue) ---
            // Prefer top-level Inputs, fall back to Nodes
            if (inputType == DynamoInputType.Number || inputType == DynamoInputType.Integer)
            {
                definition.MinValue  = ReadNullableDouble(meta, node, "MinimumValue");
                definition.MaxValue  = ReadNullableDouble(meta, node, "MaximumValue");
                definition.StepValue = ReadNullableDouble(meta, node, "StepValue");
            }

            return definition;
        }

        private static DynamoInputType DetermineInputTypeFromString(string typeString)
        {
            if (string.IsNullOrEmpty(typeString)) return DynamoInputType.Unknown;

            switch (typeString.ToLowerInvariant())
            {
                case "number": return DynamoInputType.Number;
                case "integer": return DynamoInputType.Integer;
                case "boolean": return DynamoInputType.Boolean;
                case "string": return DynamoInputType.String;
                default: return DynamoInputType.Unknown;
            }
        }

        /// <summary>
        /// Returns the lowercase, assembly-stripped type name from a Dynamo ConcreteType string
        /// (e.g. <c>"CoreNodeModels.Input.DoubleSlider, CoreNodeModels"</c> → <c>"corenodemodels.input.doubleslider"</c>).
        /// </summary>
        private static string NormalizeTypeName(string concreteType)
        {
            if (string.IsNullOrEmpty(concreteType)) return string.Empty;
            return concreteType.Split(',')[0].Trim().ToLowerInvariant();
        }

        private static DynamoInputType DetermineInputTypeFromConcreteType(string concreteType)
        {
            var typeName = NormalizeTypeName(concreteType);

            if (typeName.Contains("doubleslider") || typeName.Contains("doublenuminput"))
                return DynamoInputType.Number;

            if (typeName.Contains("integerslider") || typeName.Contains("integernuminput"))
                return DynamoInputType.Integer;

            if (typeName.Contains("boolselector") || typeName.Contains("boolean"))
                return DynamoInputType.Boolean;

            if (typeName.Contains("stringinput") || typeName.Contains("symbol"))
                return DynamoInputType.String;

            return DynamoInputType.Unknown;
        }

        private static object ExtractDefaultValue(JsonElement node, DynamoInputType type)
        {
            if (node.ValueKind != JsonValueKind.Object)
                return GetFallbackDefault(type);

            // Most built-in input nodes store the current value in "InputValue"
            if (node.TryGetProperty("InputValue", out var inputValue))
            {
                switch (inputValue.ValueKind)
                {
                    case JsonValueKind.True: return true;
                    case JsonValueKind.False: return false;
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

            return GetFallbackDefault(type);
        }

        private static object GetFallbackDefault(DynamoInputType type)
        {
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

        /// <summary>
        /// Reads a nullable double from the first source that has the property,
        /// preferring <paramref name="primary"/> over <paramref name="fallback"/>.
        /// </summary>
        private static double? ReadNullableDouble(JsonElement primary, JsonElement fallback, string propertyName)
        {
            if (primary.ValueKind == JsonValueKind.Object &&
                primary.TryGetProperty(propertyName, out var pv) &&
                pv.ValueKind == JsonValueKind.Number)
                return pv.GetDouble();

            if (fallback.ValueKind == JsonValueKind.Object &&
                fallback.TryGetProperty(propertyName, out var fv) &&
                fv.ValueKind == JsonValueKind.Number)
                return fv.GetDouble();

            return null;
        }

        private static void ApplyValueToNode(JsonNode node, object value, string concreteType)
        {
            var typeName = NormalizeTypeName(concreteType);

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
        /// Formats a user-supplied value as the string representation used in the
        /// top-level <c>Inputs[].Value</c> field (e.g. <c>"true"</c>, <c>"42"</c>).
        /// </summary>
        private static string FormatInputsArrayValue(object value)
        {
            switch (value)
            {
                case bool boolVal:
                    return boolVal ? "true" : "false";
                case double doubleVal:
                    return doubleVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case int intVal:
                    return intVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return value?.ToString() ?? string.Empty;
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

