namespace Relay.Classes
{
    internal enum DynamoInputType
    {
        Number,
        Integer,
        String,
        Boolean,
        Dropdown,
        Selection,
        Unknown
    }

    internal sealed class DynamoInputDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DynamoInputType DataType { get; set; }
        public object DefaultValue { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public double? StepValue { get; set; }
        public string NodeType { get; set; }
        public string GroupName { get; set; }
        public List<string> Items { get; } = new List<string>();
        public List<string> ItemValues { get; } = new List<string>();
        public int SelectedIndex { get; set; } = -1;
        public string SelectedString { get; set; }
        public string SelectionIdentifier { get; set; }
        public bool IsMultipleSelection { get; set; }
    }

    internal sealed class DropdownValue
    {
        public int SelectedIndex { get; set; } = -1;
        public string SelectedString { get; set; } = string.Empty;
    }

    internal sealed class SelectionValue
    {
        public string Identifier { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
    }

    internal sealed class DynamoGraphInputs
    {
        public string GraphName { get; set; }
        public string GraphPath { get; set; }
        public List<DynamoInputDefinition> Inputs { get; } = new List<DynamoInputDefinition>();
        public bool HasInputs => Inputs.Count > 0;
    }
}
