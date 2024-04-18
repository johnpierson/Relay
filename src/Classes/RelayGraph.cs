namespace Relay.Classes
{
    internal class RelayGraph
    {
        public string GraphDocumentationURL { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public Dependency[] NodeLibraryDependencies { get; set; }
    }
    internal class Dependency
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }
}