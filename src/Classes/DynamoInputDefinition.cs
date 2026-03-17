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

        /// <summary>
        /// Optional group name from a Dynamo annotation (group) that contains this node.
        /// When non-null/non-empty, the input dialog renders this input inside a
        /// <see cref="System.Windows.Controls.GroupBox"/> labelled with this name.
        /// </summary>
        public string GroupName { get; set; }

        // ---- Dropdown properties ----

        /// <summary>
        /// For dropdown nodes: ordered list of display items (may be empty until populated
        /// by <c>DynamoInputParser.PopulateRevitItems</c>).
        /// </summary>
        public List<string> Items { get; set; } = new List<string>();

        /// <summary>
        /// For dropdown nodes: parallel serialisation identifiers for each item in
        /// <see cref="Items"/>.  For Revit Categories, this holds the BuiltInCategory enum
        /// name (e.g. <c>"OST_Walls"</c>) rather than the display name.  When non-empty,
        /// write-back uses <c>ItemValues[selectedIndex]</c> as <c>SelectedString</c>.
        /// </summary>
        public List<string> ItemValues { get; set; } = new List<string>();

        /// <summary>
        /// For dropdown nodes: zero-based index of the currently selected item (-1 = none).
        /// </summary>
        public int SelectedIndex { get; set; } = -1;

        /// <summary>
        /// For dropdown nodes: display string of the currently selected item.
        /// </summary>
        public string SelectedString { get; set; }

        // ---- Selection / picker properties ----

        /// <summary>
        /// For selection nodes: the stored selection identifier (element ID string or similar).
        /// </summary>
        public string SelectionIdentifier { get; set; }

        /// <summary>
        /// For selection nodes: whether the node supports picking multiple elements.
        /// </summary>
        public bool IsMultipleSelection { get; set; }
    }

    internal enum DynamoInputType
    {
        Number,
        Integer,
        String,
        Boolean,
        /// <summary>A node that presents a fixed or dynamic dropdown list (rendered as a ComboBox).</summary>
        Dropdown,
        /// <summary>A node that selects one or more Revit model elements via <c>PickObject</c>.</summary>
        Selection,
        Unknown
    }

    /// <summary>
    /// Holds the selected index and display string chosen for a <see cref="DynamoInputType.Dropdown"/> input.
    /// Stored in the <c>UserValues</c> dictionary that is passed to
    /// <see cref="Relay.Utilities.DynamoInputParser.ApplyUserValues"/>.
    /// </summary>
    internal sealed class DropdownValue
    {
        public int SelectedIndex { get; set; } = -1;
        public string SelectedString { get; set; } = string.Empty;
    }

    /// <summary>
    /// Holds the Revit UniqueId (written back to <c>SelectionIdentifier</c> in the .dyn) and a
    /// human-readable description shown in the input dialog.
    /// Stored in <c>prefilledValues</c> between dialog invocations.
    /// </summary>
    internal sealed class SelectionValue
    {
        /// <summary>
        /// The Revit element UniqueId (or comma-separated UniqueIds for multi-select).
        /// This is the value written to <c>Nodes[].SelectionIdentifier</c>.
        /// </summary>
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// A short human-readable description shown in the dialog info box,
        /// e.g. "Walls [12345]".
        /// </summary>
        public string DisplayText { get; set; } = string.Empty;
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
