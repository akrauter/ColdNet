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
}
