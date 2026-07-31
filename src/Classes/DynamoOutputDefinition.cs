namespace Relay.Classes
{
    /// <summary>
    /// A node the graph author marked as an output (<c>IsSetAsOutput</c>), which Relay
    /// shows the user after the run.
    /// </summary>
    internal sealed class DynamoOutputDefinition
    {
        /// <summary>Node ID in the Dynamo graph.</summary>
        public string Id { get; set; }

        /// <summary>Display name — the node's custom name when the author set one.</summary>
        public string Name { get; set; }

        /// <summary>Optional description shown under the value.</summary>
        public string Description { get; set; }

        /// <summary>
        /// Optional group name from the Dynamo annotation containing this node, used to
        /// group outputs in the results window exactly as inputs are grouped.
        /// </summary>
        public string GroupName { get; set; }
    }

    /// <summary>
    /// All outputs declared by a graph.
    /// </summary>
    internal sealed class DynamoGraphOutputs
    {
        public string GraphName { get; set; }
        public List<DynamoOutputDefinition> Outputs { get; set; } = new();
        public bool HasOutputs => Outputs?.Any() == true;
    }

    /// <summary>
    /// One value produced by a declared output node after evaluation.
    /// </summary>
    internal sealed class GraphOutputValue
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string GroupName { get; set; }

        /// <summary>Root of the value tree; never null.</summary>
        public OutputValueNode Value { get; set; } = OutputValueNode.Empty();

        /// <summary>
        /// Non-null when Relay could not read this node's value, e.g. the node was not
        /// present in the evaluated workspace.
        /// </summary>
        public string Unavailable { get; set; }
    }

    /// <summary>
    /// A value in the output tree. Dynamo values are nested lists, so this mirrors that
    /// shape rather than flattening to a string.
    /// </summary>
    internal sealed class OutputValueNode
    {
        /// <summary>Text shown for this value, or the list summary for a collection.</summary>
        public string Display { get; set; } = string.Empty;

        /// <summary>Runtime type name, shown as a hint beside the value when known.</summary>
        public string TypeName { get; set; }

        public bool IsCollection { get; set; }

        public List<OutputValueNode> Children { get; } = new();

        /// <summary>
        /// Revit element id when the value is a Revit element, which lets the results
        /// window select it in the model.
        /// </summary>
        public long? ElementId { get; set; }

        internal static OutputValueNode Empty() => new() { Display = "null" };

        /// <summary>Every element id in this subtree, in document order.</summary>
        internal IEnumerable<long> AllElementIds()
        {
            if (ElementId.HasValue) yield return ElementId.Value;
            foreach (var child in Children)
                foreach (var id in child.AllElementIds())
                    yield return id;
        }
    }
}
