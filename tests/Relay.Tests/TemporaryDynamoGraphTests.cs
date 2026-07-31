using System.Text;
using System.Text.Json.Nodes;
using Relay.Execution;

namespace Relay.Tests;

public sealed class TemporaryDynamoGraphTests
{
    [Fact]
    public void PreparationPreservesRunTypesAndSource()
    {
        const string source = "{\"RunType\":\"Manual\",\"Name\":\"RunType: Manual\",\"Nested\":{\"RunType\":\"Manual\"}}";
        var files = new FakeFileSystem(source);
        using var graph = TemporaryDynamoGraph.Create("source.dyn", files);
        JsonNode prepared = JsonNode.Parse(files.WrittenText);
        Assert.Equal("Manual", prepared?["RunType"]?.GetValue<string>());
        Assert.Equal("Manual", prepared?["Nested"]?["RunType"]?.GetValue<string>());
        Assert.Equal("RunType: Manual", prepared?["Name"]?.GetValue<string>());
        Assert.Equal(source, files.SourceText);
        Assert.Equal("source.dyn", files.PathSource);
    }

    [Fact]
    public void InvalidJsonReportsSourceAndDoesNotWrite()
    {
        var files = new FakeFileSystem("{invalid");
        var exception = Assert.Throws<DynamoGraphPreparationException>(() => TemporaryDynamoGraph.Create("broken.dyn", files));
        Assert.Contains("broken.dyn", exception.Message);
        Assert.Null(files.WrittenText);
    }

    [Fact]
    public void DisposalDeletesTemporaryGraphOnce()
    {
        var files = new FakeFileSystem("{\"RunType\":\"Manual\"}");
        var graph = TemporaryDynamoGraph.Create("source.dyn", files);
        graph.Dispose();
        graph.Dispose();
        Assert.Equal(1, files.DeleteCount);
        Assert.Null(graph.CleanupFailure);
    }

    [Fact]
    public void CleanupFailureIsReportedSeparately()
    {
        var files = new FakeFileSystem("{\"RunType\":\"Manual\"}") { DeleteException = new IOException("locked") };
        var graph = TemporaryDynamoGraph.Create("source.dyn", files);
        graph.Dispose();
        Assert.Contains("locked", graph.CleanupFailure);
    }

    private sealed class FakeFileSystem : ITemporaryGraphFileSystem
    {
        internal FakeFileSystem(string sourceText) => SourceText = sourceText;
        internal string SourceText { get; }
        internal string WrittenText { get; private set; }
        internal int DeleteCount { get; private set; }
        internal Exception DeleteException { get; init; }
        internal string PathSource { get; private set; }
        public string ReadAllText(string path, Encoding encoding) => SourceText;
        public void WriteAllText(string path, string contents, Encoding encoding) => WrittenText = contents;
        public string CreatePath(string sourcePath)
        {
            PathSource = sourcePath;
            return "temporary.dyn";
        }
        public void Delete(string path)
        {
            DeleteCount++;
            if (DeleteException is not null) throw DeleteException;
        }
    }
}
