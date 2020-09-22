using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Humanizer;

namespace Relay.Utilities
{

    public static class StringUtilities
    {
        public static string GenerateButtonText(this string fileName)
        {
            return fileName.Replace(".dyn", "").Replace(' ','\n').Truncate(20);
        }

    }

}
