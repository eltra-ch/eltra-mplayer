using System;
using System.Linq;

namespace MPlayerMaster.Helpers
{
    class StringHelpers
    {
        public static string RemoveWhitespace(string input)
        {
            string result = string.Empty;

            if (!string.IsNullOrWhiteSpace(input))
            {
                result = new string(input.ToCharArray()
                .Where(c => !Char.IsWhiteSpace(c))
                .ToArray());
            }

            return result;
        }
    }
}
