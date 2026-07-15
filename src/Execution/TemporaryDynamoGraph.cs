using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Relay.Execution;

internal interface ITemporaryGraphFileSystem
{
    string ReadAllText(string path, Encoding encoding);
    void WriteAllText(string path, string contents, Encoding encoding);
    void Delete(string path);
    string CreatePath();
}

internal sealed class TemporaryDynamoGraph : IDisposable
{
    private readonly ITemporaryGraphFileSystem fileSystem;
    private bool disposed;

    private TemporaryDynamoGraph(string sourcePath, string path, ITemporaryGraphFileSystem fileSystem)
    {
        SourcePath = sourcePath;
        Path = path;
        this.fileSystem = fileSystem;
    }

    internal string SourcePath { get; }
    internal string Path { get; }
    internal string CleanupFailure { get; private set; }

    internal static TemporaryDynamoGraph Create(string sourcePath, ITemporaryGraphFileSystem fileSystem = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        fileSystem ??= PhysicalTemporaryGraphFileSystem.Instance;
        string json = fileSystem.ReadAllText(sourcePath, Encoding.UTF8);
        JsonNode root;
        try
        {
            root = JsonNode.Parse(json) ?? throw new JsonException("The graph document is empty.");
        }
        catch (JsonException exception)
        {
            throw new DynamoGraphPreparationException(sourcePath, exception);
        }

        if (root is not JsonObject graph)
        {
            throw new DynamoGraphPreparationException(sourcePath, new JsonException("The graph root must be a JSON object."));
        }

        graph["RunType"] = "Automatic";
        string temporaryPath = fileSystem.CreatePath();
        try
        {
            fileSystem.WriteAllText(temporaryPath, graph.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            return new TemporaryDynamoGraph(sourcePath, temporaryPath, fileSystem);
        }
        catch
        {
            try { fileSystem.Delete(temporaryPath); } catch { }
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            fileSystem.Delete(Path);
        }
        catch (Exception exception)
        {
            CleanupFailure = $"Failed to delete temporary graph '{Path}': {exception.Message}";
        }
    }

    private sealed class PhysicalTemporaryGraphFileSystem : ITemporaryGraphFileSystem
    {
        internal static readonly PhysicalTemporaryGraphFileSystem Instance = new();
        public string ReadAllText(string path, Encoding encoding) => File.ReadAllText(path, encoding);
        public void WriteAllText(string path, string contents, Encoding encoding) => File.WriteAllText(path, contents, encoding);
        public void Delete(string path) => File.Delete(path);
        public string CreatePath() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"relay_{Guid.NewGuid():N}.dyn");
    }
}

internal sealed class DynamoGraphPreparationException : Exception
{
    internal DynamoGraphPreparationException(string graphPath, Exception innerException)
        : base($"Could not prepare Dynamo graph '{graphPath}': {innerException.Message}", innerException) => GraphPath = graphPath;

    internal string GraphPath { get; }
}
