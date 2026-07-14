using System.IO;

namespace Relay.Configuration
{
    internal enum GraphRootResolutionSource
    {
        ExplicitDefault,
        Custom,
        InvalidFallback
    }

    internal sealed record GraphRootResolution(
        string RootPath,
        GraphRootResolutionSource Source,
        string Warning)
    {
        internal bool HasWarning => !string.IsNullOrWhiteSpace(Warning);
    }

    internal interface IGraphRootFileSystem
    {
        string GetFullPath(string path, string basePath);

        bool DirectoryExists(string path);

        void VerifyDirectoryAccess(string path);
    }

    internal sealed class PhysicalGraphRootFileSystem : IGraphRootFileSystem
    {
        public string GetFullPath(string path, string basePath)
        {
            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(path, basePath);
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public void VerifyDirectoryAccess(string path)
        {
            using var entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            entries.MoveNext();
        }
    }

    internal static class GraphRootResolver
    {
        internal static GraphRootResolution Resolve(
            string rawSetting,
            string executingDirectory,
            IGraphRootFileSystem fileSystem = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executingDirectory);

            fileSystem ??= new PhysicalGraphRootFileSystem();
            string fallbackRoot = Path.GetFullPath(executingDirectory);
            string setting = rawSetting?.Trim();

            if (string.Equals(setting, "default", StringComparison.OrdinalIgnoreCase))
            {
                return new GraphRootResolution(
                    fallbackRoot,
                    GraphRootResolutionSource.ExplicitDefault,
                    null);
            }

            if (string.IsNullOrWhiteSpace(setting))
            {
                return InvalidFallback(fallbackRoot, rawSetting, "the setting is empty");
            }

            string resolvedPath;
            try
            {
                resolvedPath = fileSystem.GetFullPath(setting, fallbackRoot);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return InvalidFallback(fallbackRoot, rawSetting, $"the path is malformed: {ex.Message}");
            }

            if (!fileSystem.DirectoryExists(resolvedPath))
            {
                return InvalidFallback(fallbackRoot, rawSetting, "the directory does not exist");
            }

            try
            {
                fileSystem.VerifyDirectoryAccess(resolvedPath);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                return InvalidFallback(fallbackRoot, rawSetting, $"the directory is inaccessible: {ex.Message}");
            }

            return new GraphRootResolution(
                resolvedPath,
                GraphRootResolutionSource.Custom,
                null);
        }

        private static GraphRootResolution InvalidFallback(
            string fallbackRoot,
            string rawSetting,
            string reason)
        {
            string rejectedSetting = rawSetting is null ? "<missing>" : $"'{rawSetting}'";
            return new GraphRootResolution(
                fallbackRoot,
                GraphRootResolutionSource.InvalidFallback,
                $"Relay rejected graph root {rejectedSetting} because {reason}. Using '{fallbackRoot}'.");
        }
    }
}
