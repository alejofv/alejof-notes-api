using System.Linq;
using System.Text.RegularExpressions;

namespace Alejof.Notes.Functions.Mapping
{
    public static class MapperExtensions
    {
        private static readonly Regex LinkParser = new Regex(@"\b(?:https?:\/\/|www\.)([^ \f\n\r\t\v\]]+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string UrlDomain(this string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                var match = LinkParser.Matches(value).FirstOrDefault();
                if (match != null)
                {
                    var url = match.Groups[1].Value;

                    return url.Contains("/") ?
                        url.Substring(0, url.IndexOf("/"))
                        : url;
                }
            }

            return "...";
        }
    }
}
