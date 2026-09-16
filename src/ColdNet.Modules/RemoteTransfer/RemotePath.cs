namespace ColdNet.Modules.RemoteTransfer;

/// <summary>Remote servers always use POSIX-style paths, regardless of the host OS this runs on - never <see cref="Path.Combine(string, string)"/> for them.</summary>
internal static class RemotePath
{
    public static string Combine(string directory, string fileName)
    {
        var trimmed = directory.TrimEnd('/');
        return string.IsNullOrEmpty(trimmed) ? "/" + fileName : trimmed + "/" + fileName;
    }
}
