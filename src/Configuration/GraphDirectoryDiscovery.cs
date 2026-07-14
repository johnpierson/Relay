using System.IO;

namespace Relay.Configuration
{
    internal sealed record GraphDirectoryDiscoveryResult(
        bool Succeeded,
        string[] Directories,
        string Error);

    internal interface IGraphDirectoryEnumerator
    {
        string[] GetDirectories(string rootPath);
    }

    internal sealed class PhysicalGraphDirectoryEnumerator : IGraphDirectoryEnumerator
    {
        public string[] GetDirectories(string rootPath)
        {
            return Directory.GetDirectories(rootPath);
        }
    }

    internal static class GraphDirectoryDiscovery
    {
        internal static GraphDirectoryDiscoveryResult Discover(
            string rootPath,
            IGraphDirectoryEnumerator enumerator = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            enumerator ??= new PhysicalGraphDirectoryEnumerator();

            try
            {
                return new GraphDirectoryDiscoveryResult(
                    true,
                    enumerator.GetDirectories(rootPath),
                    null);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException
                                       or IOException
                                       or ArgumentException
                                       or System.Security.SecurityException)
            {
                return new GraphDirectoryDiscoveryResult(
                    false,
                    Array.Empty<string>(),
                    $"Relay could not enumerate graph root '{rootPath}': {ex.Message}");
            }
        }
    }
}
