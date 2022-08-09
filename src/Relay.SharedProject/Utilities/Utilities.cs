using Humanizer;

namespace Relay.Utilities
{
    public static class StringUtilities
    {
        public static string GenerateButtonText(this string fileName)
        {
            return fileName.Replace(".dyn", "").Replace(' ','\n').Truncate(20);
        }
        public static string GetStringBetweenCharacters(this string input, char charFrom, char charTo)
        {
            int posFrom = input.IndexOf(charFrom);
            if (posFrom != -1) //if found char
            {
                int posTo = input.IndexOf(charTo, posFrom + 1);
                if (posTo != -1) //if found char
                {
                    return input.Substring(posFrom + 1, posTo - posFrom - 1);
                }
            }

            return string.Empty;
        }
    }
}
