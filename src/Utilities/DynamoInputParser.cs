using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autodesk.Revit.DB;
using Relay.Classes;

namespace Relay.Utilities
{
    internal static class DynamoInputParser
    {
        private const string SelectionIdSeparator = ",";

        public static DynamoGraphInputs ParseGraphInputs(string graphPath)
        {
            var result = new DynamoGraphInputs { GraphPath = graphPath };

            try
            {
                var json = File.ReadAllText(graphPath, System.Text.Encoding.UTF8);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                result.GraphName = ReadString(root, "Name") ?? Path.GetFileNameWithoutExtension(graphPath);

                var inputNodeIds = new List<string>();
                var nodeViewNames = new Dictionary<string, string>(StringComparer.Ordinal);
                JsonElement view = default;

                if (root.TryGetProperty("View", out view) &&
                    view.TryGetProperty("NodeViews", out var nodeViews) &&
                    nodeViews.ValueKind == JsonValueKind.Array)
                {
                    foreach (var nodeView in nodeViews.EnumerateArray())
                    {
                        if (!nodeView.TryGetProperty("IsSetAsInput", out var isInput) ||
                            isInput.ValueKind != JsonValueKind.True)
                            continue;

                        var id = ReadString(nodeView, "Id");
                        if (string.IsNullOrEmpty(id))
                            continue;

                        inputNodeIds.Add(id);
                        nodeViewNames[id] = ReadString(nodeView, "Name") ?? string.Empty;
                    }
                }

                if (inputNodeIds.Count == 0)
                    return result;

                var inputNodeIdSet = new HashSet<string>(inputNodeIds, StringComparer.Ordinal);
                var metadataById = ReadElementLookup(root, "Inputs", inputNodeIdSet);
                var nodesById = ReadElementLookup(root, "Nodes", inputNodeIdSet);

                foreach (var nodeId in inputNodeIds)
                {
                    nodesById.TryGetValue(nodeId, out var node);
                    metadataById.TryGetValue(nodeId, out var metadata);
                    nodeViewNames.TryGetValue(nodeId, out var nodeViewName);

                    result.Inputs.Add(BuildInputDefinition(nodeId, node, metadata, nodeViewName));
                }

                ApplyAnnotationGroups(result, view);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Failed to parse graph inputs from '{graphPath}': {ex.Message}");
            }

            return result;
        }

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
                        var id = node?["Id"]?.GetValue<string>();
                        if (id == null || !userValues.TryGetValue(id, out var value))
                            continue;

                        var concreteType = node["ConcreteType"]?.GetValue<string>() ?? string.Empty;
                        ApplyValueToNode(node, value, concreteType);
                    }
                }

                var inputs = root["Inputs"]?.AsArray();
                if (inputs != null)
                {
                    foreach (var input in inputs)
                    {
                        var id = input?["Id"]?.GetValue<string>();
                        if (id == null || !userValues.TryGetValue(id, out var value))
                            continue;

                        input["Value"] = JsonValue.Create(FormatInputsValue(value));

                        if (value is DropdownValue dropdownValue)
                            input["SelectedIndex"] = JsonValue.Create(dropdownValue.SelectedIndex);
                    }
                }

                var tempPath = Path.Combine(Path.GetTempPath(), $"relay_inputs_{Guid.NewGuid():N}.dyn");
                File.WriteAllText(tempPath, root.ToJsonString(), System.Text.Encoding.UTF8);
                return tempPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Failed to apply graph input values: {ex.Message}");
                return graphPath;
            }
        }

        public static void PopulateRevitItems(DynamoGraphInputs graphInputs, Document document)
        {
            if (graphInputs == null || document == null)
                return;

            foreach (var input in graphInputs.Inputs)
            {
                if (input.DataType != DynamoInputType.Dropdown || input.Items.Count > 0)
                    continue;

                var typeName = NormalizeTypeName(input.NodeType);
                if (typeName.Contains("categories"))
                    PopulateCategoryItems(input, document);
            }
        }

        private static Dictionary<string, JsonElement> ReadElementLookup(
            JsonElement root,
            string propertyName,
            HashSet<string> allowedIds)
        {
            var lookup = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

            if (!root.TryGetProperty(propertyName, out var array) ||
                array.ValueKind != JsonValueKind.Array)
                return lookup;

            foreach (var item in array.EnumerateArray())
            {
                var id = ReadString(item, "Id");
                if (id != null && allowedIds.Contains(id))
                    lookup[id] = item;
            }

            return lookup;
        }

        private static DynamoInputDefinition BuildInputDefinition(
            string nodeId,
            JsonElement node,
            JsonElement metadata,
            string nodeViewName)
        {
            var concreteType = ReadString(node, "ConcreteType") ?? string.Empty;
            var metaType = ReadString(metadata, "Type") ?? string.Empty;
            var metaType2 = ReadString(metadata, "Type2") ?? string.Empty;
            var inputType = DetermineInputType(metaType, metaType2, concreteType, node);

            var definition = new DynamoInputDefinition
            {
                Id = nodeId,
                Name = FirstNonEmpty(ReadString(metadata, "Name"), nodeViewName, "Input"),
                Description = FirstNonEmpty(ReadString(metadata, "Description"), ReadString(node, "Description")),
                NodeType = concreteType,
                DataType = inputType,
                DefaultValue = ReadDefaultValue(node, metadata, inputType)
            };

            if (inputType == DynamoInputType.Number || inputType == DynamoInputType.Integer)
            {
                definition.MinValue = ReadNullableDouble(metadata, node, "MinimumValue");
                definition.MaxValue = ReadNullableDouble(metadata, node, "MaximumValue");
                definition.StepValue = ReadNullableDouble(metadata, node, "StepValue");
            }

            if (inputType == DynamoInputType.Dropdown)
                ReadDropdownState(definition, node, metadata);

            if (inputType == DynamoInputType.Selection)
                ReadSelectionState(definition, node, metadata, metaType2);

            return definition;
        }

        private static DynamoInputType DetermineInputType(
            string metaType,
            string metaType2,
            string concreteType,
            JsonElement node)
        {
            if (string.Equals(metaType, "selection", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(metaType2, "dropdownSelection", StringComparison.OrdinalIgnoreCase))
                    return DynamoInputType.Dropdown;

                if (metaType2.IndexOf("hostSelection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    metaType2.IndexOf("hostSelections", StringComparison.OrdinalIgnoreCase) >= 0)
                    return DynamoInputType.Selection;
            }

            var fromMeta = DetermineInputTypeFromString(metaType);
            if (fromMeta != DynamoInputType.Unknown)
                return fromMeta;

            var fromConcrete = DetermineInputTypeFromConcreteType(concreteType);
            if (fromConcrete != DynamoInputType.Unknown)
                return fromConcrete;

            if (node.ValueKind == JsonValueKind.Object)
            {
                var hasSelectedIndex = node.TryGetProperty("SelectedIndex", out _);
                var hasInputValue = node.TryGetProperty("InputValue", out _);

                if (hasSelectedIndex && !hasInputValue)
                    return DynamoInputType.Dropdown;

                if (node.TryGetProperty("SelectionIdentifier", out _) ||
                    node.TryGetProperty("InstanceId", out _))
                    return DynamoInputType.Selection;
            }

            return DynamoInputType.Unknown;
        }

        private static DynamoInputType DetermineInputTypeFromString(string typeString)
        {
            switch ((typeString ?? string.Empty).ToLowerInvariant())
            {
                case "number":
                    return DynamoInputType.Number;
                case "integer":
                    return DynamoInputType.Integer;
                case "boolean":
                    return DynamoInputType.Boolean;
                case "string":
                    return DynamoInputType.String;
                case "revit.category":
                case "revit.bic":
                    return DynamoInputType.Dropdown;
                case "revit.element":
                case "revit.elements":
                case "revit.faceitem":
                    return DynamoInputType.Selection;
                default:
                    return DynamoInputType.Unknown;
            }
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

            if (typeName.Contains("categories") ||
                typeName.Contains("elementtypes") ||
                typeName.Contains("familytypes") ||
                typeName.Contains("viewtypes") ||
                typeName.Contains("dropdownbase") ||
                typeName.Contains("dsdropdown"))
                return DynamoInputType.Dropdown;

            if (typeName.Contains(".selection.select") ||
                typeName.Contains("selectmodelobject") ||
                typeName.Contains("selectmodel") ||
                typeName.Contains("selectface") ||
                typeName.Contains("selectlinkedelement") ||
                typeName.Contains("dynamo.nodes.ds") && typeName.Contains("select"))
                return DynamoInputType.Selection;

            return DynamoInputType.Unknown;
        }

        private static void ReadDropdownState(DynamoInputDefinition definition, JsonElement node, JsonElement metadata)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("SelectedIndex", out var selectedIndex) &&
                    selectedIndex.ValueKind == JsonValueKind.Number)
                    definition.SelectedIndex = selectedIndex.GetInt32();

                definition.SelectedString = ReadString(node, "SelectedString");

                if (node.TryGetProperty("Items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var display = item.ValueKind == JsonValueKind.String
                            ? item.GetString()
                            : FirstNonEmpty(ReadString(item, "Name"), ReadString(item, "DisplayName"));

                        if (string.IsNullOrWhiteSpace(display))
                            continue;

                        definition.Items.Add(display);

                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            var value = FirstNonEmpty(
                                ReadString(item, "Value"),
                                ReadString(item, "Id"),
                                ReadString(item, "Identifier"),
                                ReadString(item, "UniqueId"));

                            if (!string.IsNullOrEmpty(value))
                                definition.ItemValues.Add(value);
                        }
                    }

                    if (definition.ItemValues.Count != definition.Items.Count)
                        definition.ItemValues.Clear();
                }
            }

            if (string.IsNullOrEmpty(definition.SelectedString))
                definition.SelectedString = ReadString(metadata, "Value") ?? string.Empty;

            if (definition.SelectedIndex < 0 && !string.IsNullOrEmpty(definition.SelectedString))
            {
                if (definition.ItemValues.Count == definition.Items.Count)
                {
                    definition.SelectedIndex = definition.ItemValues.FindIndex(value =>
                        string.Equals(value, definition.SelectedString, StringComparison.OrdinalIgnoreCase));
                }

                if (definition.SelectedIndex < 0)
                {
                    definition.SelectedIndex = definition.Items.FindIndex(item =>
                        string.Equals(item, definition.SelectedString, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        private static void ReadSelectionState(
            DynamoInputDefinition definition,
            JsonElement node,
            JsonElement metadata,
            string metaType2)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                definition.SelectionIdentifier = ReadString(node, "SelectionIdentifier");

                if (string.IsNullOrEmpty(definition.SelectionIdentifier) &&
                    node.TryGetProperty("InstanceId", out var instanceIds) &&
                    instanceIds.ValueKind == JsonValueKind.Array)
                {
                    var ids = new List<string>();
                    foreach (var idValue in instanceIds.EnumerateArray())
                    {
                        var id = idValue.GetString();
                        if (!string.IsNullOrEmpty(id))
                            ids.Add(id);
                    }

                    if (ids.Count > 0)
                        definition.SelectionIdentifier = string.Join(SelectionIdSeparator, ids);
                }
            }

            if (string.IsNullOrEmpty(definition.SelectionIdentifier))
                definition.SelectionIdentifier = ReadString(metadata, "Value");

            var typeName = NormalizeTypeName(definition.NodeType);
            definition.IsMultipleSelection =
                typeName.Contains("elementss") ||
                typeName.Contains("selectmodelelements") ||
                typeName.Contains("selectmodelobjects") ||
                typeName.Contains("selectfaces") ||
                string.Equals(metaType2, "hostSelections", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyAnnotationGroups(DynamoGraphInputs result, JsonElement view)
        {
            if (view.ValueKind != JsonValueKind.Object ||
                !view.TryGetProperty("Annotations", out var annotations) ||
                annotations.ValueKind != JsonValueKind.Array)
                return;

            var groupByNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var annotation in annotations.EnumerateArray())
            {
                var title = ReadString(annotation, "Title");
                if (string.IsNullOrWhiteSpace(title) ||
                    !annotation.TryGetProperty("Nodes", out var nodes) ||
                    nodes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var node in nodes.EnumerateArray())
                {
                    var nodeId = node.GetString();
                    if (!string.IsNullOrEmpty(nodeId) && !groupByNodeId.ContainsKey(nodeId))
                        groupByNodeId[nodeId] = title;
                }
            }

            foreach (var input in result.Inputs)
            {
                if (groupByNodeId.TryGetValue(input.Id, out var groupName))
                    input.GroupName = groupName;
            }
        }

        private static object ReadDefaultValue(JsonElement node, JsonElement metadata, DynamoInputType inputType)
        {
            if (node.ValueKind == JsonValueKind.Object &&
                node.TryGetProperty("InputValue", out var inputValue))
                return ConvertJsonValue(inputValue, inputType);

            if (node.ValueKind == JsonValueKind.Object &&
                node.TryGetProperty("Code", out var code))
                return ExtractCodeValue(code.GetString() ?? string.Empty);

            if (metadata.ValueKind == JsonValueKind.Object &&
                metadata.TryGetProperty("Value", out var metadataValue))
                return ConvertJsonValue(metadataValue, inputType);

            return inputType switch
            {
                DynamoInputType.Boolean => false,
                DynamoInputType.Number => 0.0,
                DynamoInputType.Integer => 0,
                _ => string.Empty
            };
        }

        private static object ConvertJsonValue(JsonElement value, DynamoInputType inputType)
        {
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when inputType == DynamoInputType.Integer =>
                    value.TryGetInt32(out var intValue) ? intValue : (int)Math.Round(value.GetDouble()),
                JsonValueKind.Number => value.GetDouble(),
                JsonValueKind.String => ConvertStringValue(value.GetString(), inputType),
                _ => value.ToString()
            };
        }

        private static object ConvertStringValue(string value, DynamoInputType inputType)
        {
            var text = value ?? string.Empty;

            switch (inputType)
            {
                case DynamoInputType.Boolean:
                    return bool.TryParse(text, out var boolValue) && boolValue;
                case DynamoInputType.Integer:
                    if (int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var intValue))
                        return intValue;

                    return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleInt)
                        ? (int)Math.Round(doubleInt)
                        : 0;
                case DynamoInputType.Number:
                    return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue)
                        ? doubleValue
                        : 0.0;
                default:
                    return text;
            }
        }

        private static void ApplyValueToNode(JsonNode node, object value, string concreteType)
        {
            var typeName = NormalizeTypeName(concreteType);

            if (value is DropdownValue dropdownValue)
            {
                node["SelectedIndex"] = JsonValue.Create(dropdownValue.SelectedIndex);
                node["SelectedString"] = JsonValue.Create(dropdownValue.SelectedString ?? string.Empty);
                return;
            }

            if (value is string selectionId && IsSelectionType(typeName))
            {
                if (typeName.Contains("dynamo.nodes.ds") && typeName.Contains("select"))
                {
                    var array = new JsonArray();
                    foreach (var id in selectionId.Split(new[] { SelectionIdSeparator }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = id.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            array.Add(JsonValue.Create(trimmed));
                    }

                    node["InstanceId"] = array;
                }
                else
                {
                    node["SelectionIdentifier"] = JsonValue.Create(selectionId);
                }

                return;
            }

            if (typeName.Contains("codeblock"))
            {
                node["Code"] = JsonValue.Create(FormatAsCode(value));
                return;
            }

            switch (value)
            {
                case bool boolValue:
                    node["InputValue"] = JsonValue.Create(boolValue);
                    break;
                case int intValue:
                    node["InputValue"] = JsonValue.Create(intValue);
                    break;
                case double doubleValue:
                    node["InputValue"] = JsonValue.Create(doubleValue);
                    break;
                default:
                    node["InputValue"] = JsonValue.Create(value?.ToString() ?? string.Empty);
                    break;
            }
        }

        private static bool IsSelectionType(string typeName)
        {
            return typeName.Contains(".selection.select") ||
                   typeName.Contains("selectmodelobject") ||
                   typeName.Contains("selectmodel") ||
                   typeName.Contains("selectface") ||
                   typeName.Contains("selectlinkedelement") ||
                   typeName.Contains("dynamo.nodes.ds") && typeName.Contains("select");
        }

        private static string FormatInputsValue(object value)
        {
            return value switch
            {
                DropdownValue dropdownValue => dropdownValue.SelectedString ?? string.Empty,
                bool boolValue => boolValue ? "true" : "false",
                int intValue => intValue.ToString(CultureInfo.InvariantCulture),
                double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? string.Empty
            };
        }

        private static string FormatAsCode(object value)
        {
            return value switch
            {
                bool boolValue => (boolValue ? "true" : "false") + ";",
                int intValue => intValue.ToString(CultureInfo.InvariantCulture) + ";",
                double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture) + ";",
                string stringValue => "\"" + stringValue.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\";",
                _ => (value?.ToString() ?? string.Empty) + ";"
            };
        }

        private static void PopulateCategoryItems(DynamoInputDefinition input, Document document)
        {
            try
            {
                var items = new List<(string DisplayName, string BuiltInCategoryName)>();
                foreach (Category category in document.Settings.Categories)
                {
                    if (category == null || string.IsNullOrEmpty(category.Name))
                        continue;

                    var categoryId = category.Id.Value;
                    if (categoryId >= 0)
                        continue;

                    var builtInCategoryName = ((BuiltInCategory)(int)categoryId).ToString();
                    items.Add((category.Name, builtInCategoryName));
                }

                items.Sort((left, right) =>
                    string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));

                input.Items.AddRange(items.Select(item => item.DisplayName));
                input.ItemValues.AddRange(items.Select(item => item.BuiltInCategoryName));

                if (!string.IsNullOrEmpty(input.SelectedString))
                {
                    var selectedIndex = input.ItemValues.FindIndex(value =>
                        string.Equals(value, input.SelectedString, StringComparison.OrdinalIgnoreCase));

                    if (selectedIndex < 0)
                    {
                        selectedIndex = input.Items.FindIndex(value =>
                            string.Equals(value, input.SelectedString, StringComparison.OrdinalIgnoreCase));
                    }

                    input.SelectedIndex = selectedIndex;
                }
                else if (input.SelectedIndex >= 0 && input.SelectedIndex < input.ItemValues.Count)
                {
                    input.SelectedString = input.ItemValues[input.SelectedIndex];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Failed to populate category dropdown: {ex.Message}");
            }
        }

        private static string NormalizeTypeName(string concreteType)
        {
            return (concreteType ?? string.Empty).Split(',')[0].Trim().ToLowerInvariant();
        }

        private static string ExtractCodeValue(string code)
        {
            var trimmed = (code ?? string.Empty).Trim().TrimEnd(';').Trim();
            if (trimmed.Length >= 2 &&
                trimmed.StartsWith("\"", StringComparison.Ordinal) &&
                trimmed.EndsWith("\"", StringComparison.Ordinal))
            {
                return trimmed.Substring(1, trimmed.Length - 2)
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
            }

            return trimmed;
        }

        private static double? ReadNullableDouble(JsonElement primary, JsonElement fallback, string propertyName)
        {
            if (primary.ValueKind == JsonValueKind.Object &&
                primary.TryGetProperty(propertyName, out var primaryValue) &&
                primaryValue.ValueKind == JsonValueKind.Number)
                return primaryValue.GetDouble();

            if (fallback.ValueKind == JsonValueKind.Object &&
                fallback.TryGetProperty(propertyName, out var fallbackValue) &&
                fallbackValue.ValueKind == JsonValueKind.Number)
                return fallbackValue.GetDouble();

            return null;
        }

        private static string ReadString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(propertyName, out var property))
                return null;

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : property.ToString();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }
    }
}
