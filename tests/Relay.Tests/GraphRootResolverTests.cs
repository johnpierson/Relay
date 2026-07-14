using Relay.Configuration;

namespace Relay.Tests;

public sealed class GraphRootResolverTests
{
    private const string ExecutingDirectory = @"C:\Relay";

    [Theory]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData(" Default ")]
    public void ExplicitDefaultUsesExecutingDirectory(string setting)
    {
        var result = GraphRootResolver.Resolve(setting, ExecutingDirectory, new FakeFileSystem());

        Assert.Equal(ExecutingDirectory, result.RootPath);
        Assert.Equal(GraphRootResolutionSource.ExplicitDefault, result.Source);
        Assert.False(result.HasWarning);
    }

    [Fact]
    public void ValidCustomRootIsNormalized()
    {
        var fileSystem = new FakeFileSystem
        {
            FullPath = @"D:\Graphs",
            Exists = true
        };

        var result = GraphRootResolver.Resolve(@"D:\Graphs\..\Graphs", ExecutingDirectory, fileSystem);

        Assert.Equal(@"D:\Graphs", result.RootPath);
        Assert.Equal(GraphRootResolutionSource.Custom, result.Source);
        Assert.False(result.HasWarning);
    }

    [Fact]
    public void RelativeRootResolvesAgainstSettingsDirectory()
    {
        var fileSystem = new FakeFileSystem
        {
            FullPath = @"C:\Relay\CompanyGraphs",
            Exists = true
        };

        var result = GraphRootResolver.Resolve("CompanyGraphs", ExecutingDirectory, fileSystem);

        Assert.Equal(@"C:\Relay\CompanyGraphs", result.RootPath);
        Assert.Equal(("CompanyGraphs", ExecutingDirectory), fileSystem.LastFullPathRequest);
    }

    [Theory]
    [InlineData(null, "<missing>")]
    [InlineData("", "''")]
    [InlineData("   ", "'   '")]
    public void EmptySettingFallsBackWithDiagnostic(string setting, string rejectedValue)
    {
        var result = GraphRootResolver.Resolve(setting, ExecutingDirectory, new FakeFileSystem());

        AssertInvalidFallback(result, rejectedValue, "empty");
    }

    [Fact]
    public void MalformedSettingFallsBackWithDiagnostic()
    {
        var fileSystem = new FakeFileSystem { FullPathException = new ArgumentException("bad path") };

        var result = GraphRootResolver.Resolve("bad-path", ExecutingDirectory, fileSystem);

        AssertInvalidFallback(result, "bad-path", "malformed");
    }

    [Fact]
    public void MissingDirectoryFallsBackWithDiagnostic()
    {
        var fileSystem = new FakeFileSystem { FullPath = @"Z:\Missing", Exists = false };

        var result = GraphRootResolver.Resolve(@"Z:\Missing", ExecutingDirectory, fileSystem);

        AssertInvalidFallback(result, @"Z:\Missing", "does not exist");
    }

    [Fact]
    public void InaccessibleDirectoryFallsBackWithDiagnostic()
    {
        var fileSystem = new FakeFileSystem
        {
            FullPath = @"Z:\Restricted",
            Exists = true,
            AccessException = new UnauthorizedAccessException("access denied")
        };

        var result = GraphRootResolver.Resolve(@"Z:\Restricted", ExecutingDirectory, fileSystem);

        AssertInvalidFallback(result, @"Z:\Restricted", "inaccessible");
    }

    private static void AssertInvalidFallback(
        GraphRootResolution result,
        string expectedSetting,
        string expectedReason)
    {
        Assert.Equal(ExecutingDirectory, result.RootPath);
        Assert.Equal(GraphRootResolutionSource.InvalidFallback, result.Source);
        Assert.Contains(expectedSetting, result.Warning);
        Assert.Contains(expectedReason, result.Warning);
    }

    private sealed class FakeFileSystem : IGraphRootFileSystem
    {
        internal string FullPath { get; init; }
        internal bool Exists { get; init; }
        internal Exception FullPathException { get; init; }
        internal Exception AccessException { get; init; }
        internal (string Path, string BasePath) LastFullPathRequest { get; private set; }

        public string GetFullPath(string path, string basePath)
        {
            LastFullPathRequest = (path, basePath);
            if (FullPathException is not null)
            {
                throw FullPathException;
            }

            return FullPath ?? path;
        }

        public bool DirectoryExists(string path)
        {
            return Exists;
        }

        public void VerifyDirectoryAccess(string path)
        {
            if (AccessException is not null)
            {
                throw AccessException;
            }
        }
    }
}
