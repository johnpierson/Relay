using Humanizer;

namespace Relay.AutoCAD.Utilities
{

    public static class StringUtilities
    {
        public static string GenerateButtonText(this string fileName)
        {
            return fileName.Replace(".dyn", "").Replace(' ','\n').Truncate(20);
        }

    }

}
