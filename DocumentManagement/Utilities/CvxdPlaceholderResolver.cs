using System;
using System.Reflection;
using System.Text.RegularExpressions;
using DocumentManagement.Models;

namespace DocumentManagement.Utilities
{
    public static partial class CvxdPlaceholderResolver
    {
        [GeneratedRegex(@"\[(.*?)\]", RegexOptions.Compiled)]
        private static partial Regex TokenRegex();

        public static string Resolve(string pattern, Document doc, CvxdInfo info, CvxdItem? item = null)
        {
            if (string.IsNullOrEmpty(pattern)) return string.Empty;

            string result = PlaceholderRegistry.ResolvePattern(pattern, doc, info, item);

            if (result.EndsWith(".ToUpper()"))
            {
                result = result[..^10].ToUpper();
            }

            return result;
        }

        private static string? GetPropertyValue(string name, Document doc, CvxdInfo info, CvxdItem? item)
        {
            // 1. Try item object (High priority)
            if (item != null)
            {
                var itemProp = item.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (itemProp != null)
                {
                    return itemProp.GetValue(item)?.ToString() ?? string.Empty;
                }
            }

            // 2. Try info object
            var infoProp = info.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (infoProp != null)
            {
                return infoProp.GetValue(info)?.ToString() ?? string.Empty;
            }

            // 3. Try doc object
            var docProp = doc.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (docProp != null)
            {
                return docProp.GetValue(doc)?.ToString() ?? string.Empty;
            }

            return null;
        }
    }
}
