namespace Relay.Classes
{
    /// <summary>
    /// Represents a single input parameter from a Dynamo graph
    /// </summary>
    internal class DynamoInputDefinition
    {
        /// <summary>
        /// Node ID in the Dynamo graph (used as the unique key)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display name for the input
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optional tooltip/description for the input
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The Dynamo data type of this input
        /// </summary>
        public DynamoInputType DataType { get; set; }

        /// <summary>
        /// Current/default value from the graph
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// For numeric sliders: minimum allowed value
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// For numeric sliders: maximum allowed value
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// For numeric sliders: step increment value
        /// </summary>
        public double? StepValue { get; set; }

        /// <summary>
        /// The full ConcreteType string from the .dyn JSON
        /// </summary>
        public string NodeType { get; set; }
    }

    internal enum DynamoInputType
    {
        Number,
        Integer,
        String,
        Boolean,
        Unknown
    }

    /// <summary>
    /// Represents all inputs extracted from a Dynamo graph
    /// </summary>
    internal class DynamoGraphInputs
    {
        public string GraphName { get; set; }
        public string GraphPath { get; set; }
        public List<DynamoInputDefinition> Inputs { get; set; } = new List<DynamoInputDefinition>();
        public bool HasInputs => Inputs?.Any() == true;
    }
}
