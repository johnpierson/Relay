using System;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
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
    public static class ImageUtils
    {
        public static BitmapImage LoadImage(Assembly a, string name)
        {
            var img = new BitmapImage();
            try
            {
                var resourceName = a.GetManifestResourceNames().FirstOrDefault(x => x.Contains(name));
                var stream = a.GetManifestResourceStream(resourceName);

                img.BeginInit();
                img.StreamSource = stream;
                img.EndInit();
            }
            catch (Exception e)
            {
                // ignored
            }

            return img;
        }
    }
}
