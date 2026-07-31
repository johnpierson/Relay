using System.Collections;
using System.Globalization;
using System.Reflection;
using Relay.Classes;

namespace Relay.Execution;

/// <summary>
/// Reads the values of declared output nodes out of an evaluated Dynamo workspace.
/// </summary>
/// <remarks>
/// Everything here is reflection over Dynamo types (<c>NodeModel.CachedValue</c> and
/// ProtoCore's <c>MirrorData</c>), so a Dynamo version that renames or reshapes those
/// members degrades to "value unavailable" for that output rather than failing the run —
/// the graph has already executed and its model changes are committed by this point.
/// </remarks>
internal static class DynamoOutputReader
{
    /// <summary>Deepest nesting rendered before the tree is truncated.</summary>
    private const int MaxDepth = 6;

    /// <summary>Items rendered per list before the remainder is summarised.</summary>
    private const int MaxItemsPerList = 200;

    /// <summary>
    /// Collects one <see cref="GraphOutputValue"/> per requested output, in the order the
    /// author declared them.
    /// </summary>
    /// <param name="nodes">Nodes of the evaluated workspace.</param>
    /// <param name="requested">Output nodes declared by the graph.</param>
    internal static IReadOnlyList<GraphOutputValue> Read(
        IEnumerable<object> nodes,
        IReadOnlyCollection<DynamoOutputDefinition> requested)
    {
        if (requested is null || requested.Count == 0) return Array.Empty<GraphOutputValue>();

        var nodesById = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (object node in nodes)
        {
            string id = ReadNodeId(node);
            if (id != null) nodesById[id] = node;
        }

        var values = new List<GraphOutputValue>(requested.Count);
        foreach (DynamoOutputDefinition definition in requested)
        {
            var value = new GraphOutputValue
            {
                Name = definition.Name,
                Description = definition.Description,
                GroupName = definition.GroupName
            };

            if (!nodesById.TryGetValue(Normalize(definition.Id), out object node))
            {
                value.Unavailable = "This output node is not present in the evaluated graph.";
                values.Add(value);
                continue;
            }

            try
            {
                object cached = node.GetType()
                    .GetProperty("CachedValue", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(node);

                if (cached is null)
                    value.Unavailable = "Dynamo reported no value for this node.";
                else
                    value.Value = Convert(cached, 0);
            }
            catch (Exception exception)
            {
                value.Unavailable = $"Relay could not read this value: {ExceptionDiagnostics.Describe(exception)}";
            }

            values.Add(value);
        }

        return values;
    }

    /// <summary>
    /// Node GUIDs are compared without separators or casing because the .dyn stores them
    /// unhyphenated while the runtime exposes a <see cref="Guid"/>.
    /// </summary>
    private static string Normalize(string id) => id?.Replace("-", string.Empty) ?? string.Empty;

    private static string ReadNodeId(object node)
    {
        object id = node?.GetType()
            .GetProperty("GUID", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(node);
        return id is Guid guid ? guid.ToString("N") : null;
    }

    /// <summary>
    /// Converts ProtoCore's <c>MirrorData</c> into a display tree. Dynamo values are
    /// arbitrarily nested lists, so the nesting is preserved rather than flattened.
    /// </summary>
    private static OutputValueNode Convert(object mirrorData, int depth)
    {
        Type type = mirrorData.GetType();

        if (ReadBool(mirrorData, type, "IsNull"))
            return new OutputValueNode { Display = "null" };

        if (ReadBool(mirrorData, type, "IsCollection"))
        {
            if (depth >= MaxDepth)
                return new OutputValueNode { Display = "List (too deeply nested to display)", IsCollection = true };

            var list = new OutputValueNode { IsCollection = true };
            object elements = type.GetMethod("GetElements", Type.EmptyTypes)?.Invoke(mirrorData, null);

            int count = 0;
            int shown = 0;
            if (elements is IEnumerable enumerable)
            {
                foreach (object element in enumerable)
                {
                    count++;
                    if (shown >= MaxItemsPerList || element is null) continue;
                    list.Children.Add(Convert(element, depth + 1));
                    shown++;
                }
            }

            list.Display = count == 1 ? "1 item" : $"{count} items";
            if (count > shown)
                list.Children.Add(new OutputValueNode { Display = $"… {count - shown} more not shown" });

            return list;
        }

        object data = type.GetProperty("Data", BindingFlags.Instance | BindingFlags.Public)?.GetValue(mirrorData);
        var node = new OutputValueNode
        {
            Display = FormatScalar(data) ?? ReadString(mirrorData, type, "StringData") ?? "null",
            TypeName = ReadClassName(mirrorData, type),
            ElementId = TryReadElementId(data)
        };
        return node;
    }

    private static string FormatScalar(object data)
    {
        switch (data)
        {
            case null: return null;
            case string text: return text;
            case bool flag: return flag ? "true" : "false";
            case double number: return number.ToString("0.####", CultureInfo.InvariantCulture);
            case float number: return number.ToString("0.####", CultureInfo.InvariantCulture);
            case IFormattable formattable: return formattable.ToString(null, CultureInfo.InvariantCulture);
            default:
                try { return data.ToString(); }
                catch { return data.GetType().Name; }
        }
    }

    /// <summary>
    /// Pulls the Revit element id out of a Dynamo element wrapper so the results window can
    /// offer to select it in the model. Returns null for anything that is not an element.
    /// </summary>
    private static long? TryReadElementId(object data)
    {
        if (data is null) return null;

        try
        {
            // Revit.Elements.Element exposes Id (the integer value) and InternalElement.
            object id = data.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)?.GetValue(data);
            switch (id)
            {
                case long value: return value;
                case int value: return value;
            }

            object internalElement = data.GetType()
                .GetProperty("InternalElement", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(data);
            object elementId = internalElement?.GetType()
                .GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(internalElement);
            object rawValue = elementId?.GetType()
                .GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(elementId);

            return rawValue switch
            {
                long value => value,
                int value => value,
                _ => null
            };
        }
        catch
        {
            // Not an element, or the wrapper does not expose an id on this Dynamo version
            return null;
        }
    }

    private static bool ReadBool(object target, Type type, string propertyName) =>
        type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target) is true;

    private static string ReadString(object target, Type type, string propertyName)
    {
        try
        {
            return type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(target) as string;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadClassName(object mirrorData, Type type)
    {
        try
        {
            object classMirror = type.GetProperty("Class", BindingFlags.Instance | BindingFlags.Public)?.GetValue(mirrorData);
            return classMirror?.GetType()
                .GetProperty("ClassName", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(classMirror) as string;
        }
        catch
        {
            return null;
        }
    }
}
