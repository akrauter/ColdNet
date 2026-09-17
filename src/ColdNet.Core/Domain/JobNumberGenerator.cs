using System.Security.Cryptography;
using System.Text;

namespace ColdNet.Core.Domain;

/// <summary>
/// Generates the 12-character unique job numbers <c>CNIMPORT</c> produces when
/// "Generate unique job ID" is enabled (e.g. "00106EL123317"), so multiple deliveries that
/// happen to share a source file name never collide.
/// </summary>
public static class JobNumberGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string GenerateUniqueJobId()
    {
        Span<byte> buffer = stackalloc byte[12];
        RandomNumberGenerator.Fill(buffer);

        var sb = new StringBuilder(12);
        foreach (var b in buffer)
        {
            sb.Append(Alphabet[b % Alphabet.Length]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns <paramref name="candidate"/> unchanged if it isn't in <paramref name="existingFilePrefixes"/>,
    /// otherwise appends "-2", "-3", ... until a free value is found. The chosen value is added to
    /// <paramref name="existingFilePrefixes"/> before returning, so a single import pass that discovers
    /// several same-named files in a row never hands out the same prefix twice either - a job's
    /// <c>FilePrefix</c> is unique per chain, and this is the only place that's decided, before any
    /// file is renamed on disk to match it.
    /// </summary>
    public static string MakeUnique(string candidate, ISet<string> existingFilePrefixes)
    {
        if (existingFilePrefixes.Add(candidate))
        {
            return candidate;
        }

        for (var suffix = 2; ; suffix++)
        {
            var next = $"{candidate}-{suffix}";
            if (existingFilePrefixes.Add(next))
            {
                return next;
            }
        }
    }
}
