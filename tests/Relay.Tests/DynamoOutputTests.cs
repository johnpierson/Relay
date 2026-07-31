using Relay.Classes;
using Relay.Execution;
using Relay.Utilities;

namespace Relay.Tests;

public sealed class DynamoOutputTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
        foreach (var directory in _tempDirectories)
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private string WriteGraph(string json)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"relay_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        var path = Path.Combine(directory, "graph.dyn");
        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
        return path;
    }

    /// <summary>
    /// Mirrors the real shape Dynamo writes: the flag lives in View.NodeViews and the
    /// name/description come from the top-level Outputs array.
    /// </summary>
    private const string SampleGraph = """
    {
      "Name": "Reporting",
      "Outputs": [
        {
          "Id": "3354bcfa6966425e83f00de69f4c450e",
          "Name": "Wall Count",
          "Type": "integer",
          "InitialValue": "1241",
          "Description": "Visualizes a node's output"
        },
        {
          "Id": "aaaa1111bbbb2222cccc3333dddd4444",
          "Name": "Affected Walls",
          "Type": "object"
        }
      ],
      "View": {
        "NodeViews": [
          { "Id": "3354bcfa6966425e83f00de69f4c450e", "Name": "Watch", "IsSetAsOutput": true },
          { "Id": "aaaa1111bbbb2222cccc3333dddd4444", "Name": "Watch", "IsSetAsOutput": true },
          { "Id": "ffff0000ffff0000ffff0000ffff0000", "Name": "Not An Output", "IsSetAsOutput": false }
        ],
        "Annotations": [
          { "Title": "Summary", "Nodes": [ "3354bcfa6966425e83f00de69f4c450e" ] }
        ]
      }
    }
    """;

    // -------------------------------------------------------------------
    // Discovery
    // -------------------------------------------------------------------

    [Fact]
    public void ParsesDeclaredOutputsInAuthoringOrder()
    {
        var result = DynamoOutputParser.ParseGraphOutputs(WriteGraph(SampleGraph));

        Assert.True(result.HasOutputs);
        Assert.Equal("Reporting", result.GraphName);
        Assert.Equal(
            new[] { "3354bcfa6966425e83f00de69f4c450e", "aaaa1111bbbb2222cccc3333dddd4444" },
            result.Outputs.Select(o => o.Id).ToArray());
    }

    [Fact]
    public void PrefersTheDeclaredOutputNameOverTheNodeName()
    {
        var result = DynamoOutputParser.ParseGraphOutputs(WriteGraph(SampleGraph));

        // Both node views are literally named "Watch"; the useful label is the one the
        // author set on the output itself.
        Assert.Equal("Wall Count", result.Outputs[0].Name);
        Assert.Equal("Affected Walls", result.Outputs[1].Name);
        Assert.Equal("Visualizes a node's output", result.Outputs[0].Description);
    }

    [Fact]
    public void AssignsGroupNamesFromAnnotations()
    {
        var result = DynamoOutputParser.ParseGraphOutputs(WriteGraph(SampleGraph));

        Assert.Equal("Summary", result.Outputs[0].GroupName);
        Assert.Null(result.Outputs[1].GroupName);
    }

    [Fact]
    public void GraphWithoutDeclaredOutputsHasNone()
    {
        var json = """
        {
          "Name": "No Outputs",
          "Outputs": [],
          "View": { "NodeViews": [ { "Id": "a", "Name": "Watch", "IsSetAsOutput": false } ] }
        }
        """;

        Assert.False(DynamoOutputParser.ParseGraphOutputs(WriteGraph(json)).HasOutputs);
    }

    [Fact]
    public void MalformedGraphReturnsNoOutputsWithoutThrowing()
    {
        Assert.False(DynamoOutputParser.ParseGraphOutputs(WriteGraph("{ not json")).HasOutputs);
    }

    // -------------------------------------------------------------------
    // Value reading
    // -------------------------------------------------------------------

    [Fact]
    public void ReadsScalarValuesForDeclaredOutputs()
    {
        var id = Guid.NewGuid();
        var nodes = new object[] { new FakeNode(id, FakeMirror.Scalar(1241, "int")) };

        var values = DynamoOutputReader.Read(nodes, Declare(id, "Wall Count"));

        var value = Assert.Single(values);
        Assert.Equal("Wall Count", value.Name);
        Assert.Null(value.Unavailable);
        Assert.Equal("1241", value.Value.Display);
        Assert.False(value.Value.IsCollection);
    }

    [Fact]
    public void NodeIdsMatchAcrossHyphenatedAndCompactGuidForms()
    {
        // The .dyn stores ids without hyphens; the runtime exposes a Guid.
        var id = Guid.NewGuid();
        var nodes = new object[] { new FakeNode(id, FakeMirror.Scalar("ok", "string")) };

        var declared = new[] { new DynamoOutputDefinition { Id = id.ToString("D"), Name = "Result" } };
        var values = DynamoOutputReader.Read(nodes, declared);

        Assert.Null(values[0].Unavailable);
        Assert.Equal("ok", values[0].Value.Display);
    }

    [Fact]
    public void NestedListsArePreservedRatherThanFlattened()
    {
        var id = Guid.NewGuid();
        var mirror = FakeMirror.Collection(
            FakeMirror.Scalar("a", "string"),
            FakeMirror.Collection(FakeMirror.Scalar("b", "string"), FakeMirror.Scalar("c", "string")));
        var nodes = new object[] { new FakeNode(id, mirror) };

        var values = DynamoOutputReader.Read(nodes, Declare(id, "Nested"));

        OutputValueNode root = values[0].Value;
        Assert.True(root.IsCollection);
        Assert.Equal("2 items", root.Display);
        Assert.Equal("a", root.Children[0].Display);
        Assert.True(root.Children[1].IsCollection);
        Assert.Equal("2 items", root.Children[1].Display);
        Assert.Equal(new[] { "b", "c" }, root.Children[1].Children.Select(c => c.Display).ToArray());
    }

    [Fact]
    public void LongListsAreTruncatedButStillReportTheirRealCount()
    {
        var id = Guid.NewGuid();
        var items = Enumerable.Range(0, 250).Select(i => FakeMirror.Scalar(i, "int")).ToArray();
        var nodes = new object[] { new FakeNode(id, FakeMirror.Collection(items)) };

        var values = DynamoOutputReader.Read(nodes, Declare(id, "Many"));

        OutputValueNode root = values[0].Value;
        Assert.Equal("250 items", root.Display);
        // 200 rendered plus one summary row for the remainder.
        Assert.Equal(201, root.Children.Count);
        Assert.Contains("50 more not shown", root.Children[^1].Display);
    }

    [Fact]
    public void NullValuesAreReportedAsNull()
    {
        var id = Guid.NewGuid();
        var nodes = new object[] { new FakeNode(id, FakeMirror.Null()) };

        var values = DynamoOutputReader.Read(nodes, Declare(id, "Nothing"));

        Assert.Equal("null", values[0].Value.Display);
    }

    [Fact]
    public void RevitElementsExposeTheirIdSoTheyCanBeSelected()
    {
        var id = Guid.NewGuid();
        var nodes = new object[] { new FakeNode(id, FakeMirror.Scalar(new FakeRevitElement(987654), "Revit.Elements.Wall")) };

        var values = DynamoOutputReader.Read(nodes, Declare(id, "Wall"));

        Assert.Equal(987654, values[0].Value.ElementId);
        Assert.Equal(new long[] { 987654 }, values[0].Value.AllElementIds().ToArray());
    }

    [Fact]
    public void ElementIdsAreCollectedFromNestedLists()
    {
        var id = Guid.NewGuid();
        var mirror = FakeMirror.Collection(
            FakeMirror.Scalar(new FakeRevitElement(1), "Revit.Elements.Wall"),
            FakeMirror.Collection(FakeMirror.Scalar(new FakeRevitElement(2), "Revit.Elements.Wall")));
        var nodes = new object[] { new FakeNode(id, mirror) };

        var values = DynamoOutputReader.Read(nodes, Declare(id, "Walls"));

        Assert.Equal(new long[] { 1, 2 }, values[0].Value.AllElementIds().ToArray());
    }

    [Fact]
    public void MissingNodeIsReportedAsUnavailableRatherThanThrowing()
    {
        var values = DynamoOutputReader.Read(
            Array.Empty<object>(),
            Declare(Guid.NewGuid(), "Gone"));

        Assert.NotNull(values[0].Unavailable);
        Assert.Contains("not present", values[0].Unavailable);
    }

    [Fact]
    public void NodeWithoutAReadableValueIsReportedAsUnavailable()
    {
        var id = Guid.NewGuid();
        var nodes = new object[] { new FakeNode(id, null) };

        var values = DynamoOutputReader.Read(nodes, Declare(id, "Empty"));

        Assert.NotNull(values[0].Unavailable);
    }

    [Fact]
    public void NoDeclaredOutputsReadsNothing()
    {
        Assert.Empty(DynamoOutputReader.Read(new object[] { new FakeNode(Guid.NewGuid(), FakeMirror.Null()) }, null));
        Assert.Empty(DynamoOutputReader.Read(Array.Empty<object>(), Array.Empty<DynamoOutputDefinition>()));
    }

    private static DynamoOutputDefinition[] Declare(Guid id, string name) =>
        new[] { new DynamoOutputDefinition { Id = id.ToString("N"), Name = name } };

    // -------------------------------------------------------------------
    // Stand-ins for Dynamo's NodeModel and ProtoCore's MirrorData
    // -------------------------------------------------------------------

    private sealed class FakeNode
    {
        internal FakeNode(Guid guid, object cachedValue)
        {
            GUID = guid;
            CachedValue = cachedValue;
        }

        public Guid GUID { get; }
        public object CachedValue { get; }
    }

    private sealed class FakeClassMirror
    {
        public string ClassName { get; init; }
    }

    private sealed class FakeMirror
    {
        public bool IsNull { get; private init; }
        public bool IsCollection { get; private init; }
        public object Data { get; private init; }
        public string StringData => Data?.ToString();
        public FakeClassMirror Class { get; private init; }

        private FakeMirror[] Elements { get; init; } = Array.Empty<FakeMirror>();

        public IEnumerable<FakeMirror> GetElements() => Elements;

        internal static FakeMirror Null() => new() { IsNull = true };

        internal static FakeMirror Scalar(object data, string className) =>
            new() { Data = data, Class = new FakeClassMirror { ClassName = className } };

        internal static FakeMirror Collection(params FakeMirror[] elements) =>
            new() { IsCollection = true, Elements = elements };
    }

    /// <summary>Stands in for Dynamo's Revit element wrapper, which exposes Id.</summary>
    private sealed class FakeRevitElement
    {
        internal FakeRevitElement(long id) => Id = id;

        public long Id { get; }

        public override string ToString() => "Wall";
    }
}
