using System.Text.RegularExpressions;

namespace ColdNet.Modules.RemoteTransfer;

/// <summary>Simple "*"/"?" glob matching for remote directory listings, which don't support .NET's file-mask semantics.</summary>
internal static class WildcardMatcher
{
    public static bool IsMatch(string fileName, string mask)
    {
        if (string.IsNullOrWhiteSpace(mask) || mask == "*.*" || mask == "*")
        {
            return true;
        }

        var pattern = "^" + Regex.Escape(mask).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        return Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase);
    }
}
