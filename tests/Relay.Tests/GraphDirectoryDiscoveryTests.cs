using Relay.Configuration;

namespace Relay.Tests;

public sealed class GraphDirectoryDiscoveryTests
{
    [Fact]
    public void SuccessfulDiscoveryReturnsDirectories()
    {
        var expected = new[] { @"C:\Relay\TabOne", @"C:\Relay\TabTwo" };

        var result = GraphDirectoryDiscovery.Discover(
            @"C:\Relay",
            new StubEnumerator(expected));

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.Directories);
        Assert.Null(result.Error);
    }

    [Fact]
    public void UnavailableRootReturnsFailureWithoutAnEmptySuccess()
    {
        var result = GraphDirectoryDiscovery.Discover(
            @"Z:\Graphs",
            new StubEnumerator(new IOException("share unavailable")));

        Assert.False(result.Succeeded);
        Assert.Empty(result.Directories);
        Assert.Contains(@"Z:\Graphs", result.Error);
        Assert.Contains("share unavailable", result.Error);
    }

    private sealed class StubEnumerator : IGraphDirectoryEnumerator
    {
        private readonly string[] directories;
        private readonly Exception exception;

        internal StubEnumerator(string[] directories)
        {
            this.directories = directories;
        }

        internal StubEnumerator(Exception exception)
        {
            this.exception = exception;
        }

        public string[] GetDirectories(string rootPath)
        {
            if (exception is not null)
            {
                throw exception;
            }

            return directories;
        }
    }
}
