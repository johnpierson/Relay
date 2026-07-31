using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Relay.Classes;

namespace Relay.Utilities
{
    /// <summary>
    /// Parses Dynamo .dyn graph files to extract input node definitions,
    /// and applies user-provided values back to a modified copy of the graph.
    /// This class is pure JSON processing — no Revit or Dynamo API dependencies —
    /// so it can be unit tested outside a Revit host.
    /// </summary>
    internal static class DynamoInputParser
    {
        /// <summary>
        /// Delimiter used to join / split multiple UniqueIds for multi-element picker nodes.
        /// UniqueIds themselves never contain a comma, so this is safe.
        /// </summary>
        internal const string SelectionIdSeparator = ",";

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
                    view.ValueKind == JsonValueKind.Object &&
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

                // ----------------------------------------------------------------
                // Step 5: Assign group names from View.Annotations
                // Dynamo groups (annotations) contain a Nodes[] array with node IDs.
                // We assign the annotation's Title to each matching input definition
                // so that InputDialog can render them inside a titled card.
                // ----------------------------------------------------------------
                if (view.ValueKind == JsonValueKind.Object &&
                    view.TryGetProperty("Annotations", out var annotations) &&
                    annotations.ValueKind == JsonValueKind.Array)
                {
                    // Build a lookup: node ID → annotation title
                    // Preserve annotation order so the first annotation that contains a
                    // given node "wins" (in case of nested/overlapping groups).
                    var nodeGroupMap = new Dictionary<string, string>(StringComparer.Ordinal);

                    foreach (var annotation in annotations.EnumerateArray())
                    {
                        var title = annotation.TryGetProperty("Title", out var titleProp)
                            ? titleProp.GetString() ?? string.Empty
                            : string.Empty;

                        if (string.IsNullOrWhiteSpace(title)) continue;

                        if (!annotation.TryGetProperty("Nodes", out var groupNodes) ||
                            groupNodes.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var nodeIdEl in groupNodes.EnumerateArray())
                        {
                            var nodeId = nodeIdEl.GetString();
                            if (string.IsNullOrEmpty(nodeId)) continue;

                            // First annotation wins; don't overwrite an already-assigned group
                            if (!nodeGroupMap.ContainsKey(nodeId))
                                nodeGroupMap[nodeId] = title;
                        }
                    }

                    // Apply group names to input definitions
                    foreach (var inputDef in result.Inputs)
                    {
                        if (nodeGroupMap.TryGetValue(inputDef.Id, out var groupName))
                            inputDef.GroupName = groupName;
                    }
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
        /// The source graph file is never modified.
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

                        // For dropdown nodes (e.g. Categories), also keep SelectedIndex
                        // in Inputs[] in sync.  Dynamo reads this on restore.
                        if (value is DropdownValue dv)
                            input["SelectedIndex"] = JsonValue.Create(dv.SelectedIndex);
                    }
                }

                return WriteTemporaryGraph(graphPath, root.ToJsonString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Failed to apply user values to graph: {ex.Message}");
                return graphPath;
            }
        }

        /// <summary>
        /// Writes the modified graph JSON to a temporary <c>.relay_*.dyn</c> file beside the
        /// source graph — Dynamo's file trust is folder-based, so a copy in %TEMP% can be
        /// silently blocked from running (<c>RunSettings.ForceBlockRun</c>).  Falls back to
        /// the system temp folder when the source folder is not writable, and to the source
        /// path itself (values dropped) as a last resort.
        /// </summary>
        private static string WriteTemporaryGraph(string graphPath, string json)
        {
            var fileName = $".relay_{Guid.NewGuid():N}.dyn";
            var candidates = new List<string>();

            try
            {
                var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(graphPath));
                if (!string.IsNullOrEmpty(sourceDirectory))
                    candidates.Add(Path.Combine(sourceDirectory, fileName));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Relay] Could not resolve the graph directory for '{graphPath}': {ex.Message}");
            }

            candidates.Add(Path.Combine(Path.GetTempPath(), fileName));

            foreach (var candidate in candidates)
            {
                try
                {
                    File.WriteAllText(candidate, json, System.Text.Encoding.UTF8);
                    return candidate;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[Relay] Could not write temporary graph '{candidate}': {ex.Message}");
                }
            }

            return graphPath;
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

            // --- Type: use top-level Inputs[].Type + Type2 for precise detection, then ConcreteType ---
            var inputType = DynamoInputType.Unknown;

            string metaType  = string.Empty;
            string metaType2 = string.Empty;
            if (meta.ValueKind == JsonValueKind.Object)
            {
                if (meta.TryGetProperty("Type", out var typeProp))
                    metaType = typeProp.GetString() ?? string.Empty;
                if (meta.TryGetProperty("Type2", out var type2Prop))
                    metaType2 = type2Prop.GetString() ?? string.Empty;
            }

            // When both Type and Type2 are present, they give the most reliable signal.
            // "selection" + "hostSelection"   → element picker (Selection)
            // "selection" + "dropdownSelection" → dropdown (Dropdown)
            if (string.Equals(metaType, "selection", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(metaType2, "hostSelection",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(metaType2, "hostSelections",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(metaType2, "linkedHostSelection",
                        StringComparison.OrdinalIgnoreCase))
                    inputType = DynamoInputType.Selection;
                else if (string.Equals(metaType2, "dropdownSelection",
                             StringComparison.OrdinalIgnoreCase))
                    inputType = DynamoInputType.Dropdown;
                // "selection" with unknown Type2: fall through to ConcreteType detection
            }
            else if (!string.IsNullOrEmpty(metaType))
            {
                inputType = DetermineInputTypeFromString(metaType);
            }

            if (inputType == DynamoInputType.Unknown && !string.IsNullOrEmpty(concreteType))
                inputType = DetermineInputTypeFromConcreteType(concreteType);

            // File Path / Directory Path nodes derive from Dynamo's string node, so the
            // top-level Inputs[].Type reports "string".  The ConcreteType is the reliable
            // signal, so let it upgrade a String classification to a path kind.
            if (inputType == DynamoInputType.String && !string.IsNullOrEmpty(concreteType))
            {
                var fromConcrete = DetermineInputTypeFromConcreteType(concreteType);
                if (fromConcrete == DynamoInputType.FilePath ||
                    fromConcrete == DynamoInputType.DirectoryPath)
                    inputType = fromConcrete;
            }

            // --- JSON property heuristic: detect Dropdown / Selection when ConcreteType is unrecognised ---
            // This is a reliable fallback: Dynamo's DSDropDownBase nodes always serialise SelectedIndex
            // (never InputValue), and selection/picker nodes always serialise SelectionIdentifier.
            if (inputType == DynamoInputType.Unknown && node.ValueKind == JsonValueKind.Object)
            {
                bool hasSelectedIndex = node.TryGetProperty("SelectedIndex", out _);
                bool hasInputValue    = node.TryGetProperty("InputValue", out _);

                // A node that has SelectedIndex but no InputValue is a dropdown.
                // The exclusion of InputValue avoids misclassifying any hypothetical future node
                // that might have both properties.
                if (hasSelectedIndex && !hasInputValue)
                    inputType = DynamoInputType.Dropdown;
                else if (node.TryGetProperty("SelectionIdentifier", out _))
                    inputType = DynamoInputType.Selection;
            }

            // --- Default value from Nodes[].InputValue ---
            var defaultValue = ExtractDefaultValue(node, inputType);

            // Path nodes keep the absolute path in HintPath while InputValue may be
            // relative to the .dyn location.  Relay executes a temp copy of the graph,
            // so prefer the absolute HintPath as the default.
            if ((inputType == DynamoInputType.FilePath || inputType == DynamoInputType.DirectoryPath) &&
                node.ValueKind == JsonValueKind.Object &&
                node.TryGetProperty("HintPath", out var hintPath) &&
                hintPath.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(hintPath.GetString()))
            {
                defaultValue = hintPath.GetString();
            }

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

            // --- Dropdown-specific data ---
            if (inputType == DynamoInputType.Dropdown && node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("SelectedIndex", out var selIdx) &&
                    selIdx.ValueKind == JsonValueKind.Number)
                    definition.SelectedIndex = selIdx.GetInt32();

                if (node.TryGetProperty("SelectedString", out var selStr))
                    definition.SelectedString = selStr.GetString() ?? string.Empty;

                // Use meta Value as fallback for SelectedString (some nodes write it there)
                if (string.IsNullOrEmpty(definition.SelectedString) &&
                    meta.ValueKind == JsonValueKind.Object &&
                    meta.TryGetProperty("Value", out var metaVal))
                    definition.SelectedString = metaVal.GetString() ?? string.Empty;

                // Some nodes serialise their Items list directly in the .dyn
                if (node.TryGetProperty("Items", out var storedItems) &&
                    storedItems.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in storedItems.EnumerateArray())
                    {
                        string displayName = item.ValueKind == JsonValueKind.String
                            ? item.GetString()
                            : item.ValueKind == JsonValueKind.Object &&
                              item.TryGetProperty("Name", out var n) ? n.GetString() : null;
                        if (!string.IsNullOrEmpty(displayName))
                            definition.Items.Add(displayName);
                    }

                    // Match SelectedString → index within the stored list
                    if (definition.SelectedIndex < 0 && !string.IsNullOrEmpty(definition.SelectedString))
                        definition.SelectedIndex = definition.Items.IndexOf(definition.SelectedString);
                }
            }

            // --- Selection-specific data ---
            if (inputType == DynamoInputType.Selection)
            {
                // DSRevitNodesUI selection nodes (e.g. DSModelElementSelection) store the
                // selected element's UniqueId in the Nodes[].InstanceId array.
                // Older / custom selection nodes use Nodes[].SelectionIdentifier instead.
                // The top-level Inputs[].Value is also a reliable source (present for both).
                if (node.ValueKind == JsonValueKind.Object)
                {
                    if (node.TryGetProperty("SelectionIdentifier", out var selId))
                        definition.SelectionIdentifier = selId.GetString();

                    if (string.IsNullOrEmpty(definition.SelectionIdentifier) &&
                        node.TryGetProperty("InstanceId", out var instanceIds) &&
                        instanceIds.ValueKind == JsonValueKind.Array &&
                        instanceIds.GetArrayLength() > 0)
                    {
                        // Comma-join all UniqueIds for multi-element nodes
                        var ids = new List<string>();
                        foreach (var el in instanceIds.EnumerateArray())
                        {
                            var s = el.GetString();
                            if (!string.IsNullOrEmpty(s)) ids.Add(s);
                        }
                        if (ids.Count > 0)
                            definition.SelectionIdentifier = string.Join(SelectionIdSeparator, ids);
                    }
                }

                // Fallback: top-level Inputs[].Value (UniqueId)
                if (string.IsNullOrEmpty(definition.SelectionIdentifier) &&
                    meta.ValueKind == JsonValueKind.Object &&
                    meta.TryGetProperty("Value", out var metaSelVal) &&
                    metaSelVal.ValueKind == JsonValueKind.String)
                {
                    definition.SelectionIdentifier = metaSelVal.GetString();
                }

                // Determine multiplicity from the ConcreteType name
                var typeName = NormalizeTypeName(concreteType);
                definition.IsMultipleSelection =
                    // DSModelElementsSelection (plural): "Elements" ends in 's', "Selection"
                    // starts with 'S' → normalized contains "elementss" (double-s junction)
                    typeName.Contains("elementss") ||
                    // Legacy / third-party patterns
                    typeName.Contains("selectmodelobjects") ||
                    typeName.Contains("selectmodelobjectsequence") ||
                    typeName.Contains("selectmodelelements") ||
                    typeName.Contains("selectelements") ||
                    typeName.Contains("selectfaces") ||
                    // Type2 = "hostSelections" (plural) also indicates multi-select
                    string.Equals(metaType2, "hostSelections", StringComparison.OrdinalIgnoreCase);
            }

            return definition;
        }

        private static DynamoInputType DetermineInputTypeFromString(string typeString)
        {
            if (string.IsNullOrEmpty(typeString)) return DynamoInputType.Unknown;

            // typeString comes from the top-level Inputs[].Type field in the Dynamo .dyn JSON.
            // Standard values are "number", "boolean", "string", "integer".
            // Revit-specific node types appear when a Revit dropdown or selection node
            // is marked as IsSetAsInput.
            switch (typeString.ToLowerInvariant())
            {
                case "number": return DynamoInputType.Number;
                case "integer": return DynamoInputType.Integer;
                case "boolean": return DynamoInputType.Boolean;
                case "string": return DynamoInputType.String;
                // Revit-specific type strings that appear in the top-level Inputs[] array
                case "revit.category":
                case "revit.bic":
                    return DynamoInputType.Dropdown;
                case "revit.element":
                case "revit.elements":
                case "revit.faceitem":
                    return DynamoInputType.Selection;
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

            // Path browser nodes must be checked before the generic string check because
            // they derive from Dynamo's string node family.
            if (typeName.Contains("input.filename"))
                return DynamoInputType.FilePath;

            if (typeName.Contains("input.directory"))
                return DynamoInputType.DirectoryPath;

            if (typeName.Contains("doubleslider") || typeName.Contains("doublenuminput"))
                return DynamoInputType.Number;

            if (typeName.Contains("integerslider") || typeName.Contains("integernuminput"))
                return DynamoInputType.Integer;

            if (typeName.Contains("boolselector") || typeName.Contains("boolean"))
                return DynamoInputType.Boolean;

            if (typeName.Contains("stringinput") || typeName.Contains("symbol"))
                return DynamoInputType.String;

            // Dropdown nodes (DSDropDownBase-derived, including built-in Revit dropdowns)
            if (typeName.Contains("categories") ||
                typeName.Contains("elementtypes") ||
                typeName.Contains("viewtypes") ||
                typeName.Contains("familytypes") ||
                typeName.Contains("scheduletypes") ||
                typeName.Contains("structuralsection") ||
                typeName.Contains("dropdownbase") ||
                typeName.Contains("dsdropdown"))
                return DynamoInputType.Dropdown;

            // Selection / element-picker nodes
            if (typeName.Contains(".selection.select") ||
                typeName.Contains("selectmodelobject") ||
                typeName.Contains("selectfaces") ||
                typeName.Contains("selectlinkedelement") ||
                // DSRevitNodesUI: Dynamo.Nodes.DSModelElementSelection,
                //                  Dynamo.Nodes.DSModelElementsSelection,
                //                  Dynamo.Nodes.DSFaceSelection, etc.
                (typeName.Contains("dynamo.nodes.ds") && typeName.Contains("select")))
                return DynamoInputType.Selection;

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

            // --- Dropdown node: update SelectedIndex and SelectedString ---
            if (value is DropdownValue dropdownVal)
            {
                node["SelectedIndex"] = JsonValue.Create(dropdownVal.SelectedIndex);
                if (!string.IsNullOrEmpty(dropdownVal.SelectedString))
                    node["SelectedString"] = JsonValue.Create(dropdownVal.SelectedString);
                return;
            }

            // --- Selection node write-back ---
            // Different DSRevitNodesUI picker node types use different JSON properties:
            //   • Dynamo.Nodes.DSModelElementSelection (and DSFaceSelection etc.) → InstanceId array
            //   • Older / third-party selection nodes                              → SelectionIdentifier string
            if (value is string stringValue)
            {
                bool isDSModelSelection = typeName.Contains("dynamo.nodes.ds") &&
                                         typeName.Contains("select");
                bool isLegacySelection  = typeName.Contains(".selection.select") ||
                                          typeName.Contains("selectmodelobject") ||
                                          typeName.Contains("selectfaces") ||
                                          typeName.Contains("selectlinkedelement") ||
                                          typeName.Contains("selectasfamily");

                if (isDSModelSelection)
                {
                    // Write each UniqueId as a separate element of the InstanceId array.
                    // Multiple IDs are stored comma-separated in stringValue.
                    var ids = stringValue.Split(new[] { SelectionIdSeparator }, StringSplitOptions.RemoveEmptyEntries);
                    var idArray = new JsonArray();
                    foreach (var id in ids)
                    {
                        var trimmed = id.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            idArray.Add(JsonValue.Create(trimmed));
                    }
                    node["InstanceId"] = idArray;
                    return;
                }

                if (isLegacySelection)
                {
                    node["SelectionIdentifier"] = JsonValue.Create(stringValue);
                    return;
                }

                // Path browser nodes keep InputValue (possibly relative) and HintPath
                // (absolute).  Relay executes a temp copy of the graph, so write the
                // user's absolute path to both to keep them resolvable and consistent.
                if (typeName.Contains("input.filename") || typeName.Contains("input.directory"))
                {
                    node["InputValue"] = JsonValue.Create(stringValue);
                    node["HintPath"] = JsonValue.Create(stringValue);
                    return;
                }
            }

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
                case DropdownValue dv:
                    return dv.SelectedString ?? dv.SelectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
