using System.Text.Json;

namespace ColdNet.Core.Properties;

/// <summary>
/// A job's property/variable set, written and consumed by modules such as CNSETVAR, CNPARSE or
/// CNVARFRTXT. Supports multi-valued fields (JPL-style <c>feld[]=</c> repeated-key notation) and
/// is persisted as plain JSON next to the job's files, using the module's configured output file
/// extension as the file name.
/// </summary>
public class PropertyBag
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Dictionary<string, List<string>> _values = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, List<string>> Values => _values;

    /// <summary>Replaces all values of <paramref name="key"/> with a single value.</summary>
    public void Set(string key, string value) => _values[key] = [value];

    /// <summary>Appends a value, turning the field into (or extending) a multi-value field.</summary>
    public void Add(string key, string value)
    {
        if (!_values.TryGetValue(key, out var list))
        {
            list = [];
            _values[key] = list;
        }

        list.Add(value);
    }

    public string? Get(string key) => _values.TryGetValue(key, out var list) && list.Count > 0 ? list[0] : null;

    public IReadOnlyList<string> GetAll(string key) => _values.TryGetValue(key, out var list) ? list : [];

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public bool Remove(string key) => _values.Remove(key);

    public static PropertyBag FromDictionary(IDictionary<string, List<string>> source)
    {
        var bag = new PropertyBag();
        foreach (var (key, value) in source)
        {
            bag._values[key] = [.. value];
        }

        return bag;
    }

    public static async Task<PropertyBag> LoadAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            return new PropertyBag();
        }

        await using var stream = File.OpenRead(path);
        var data = await JsonSerializer.DeserializeAsync<Dictionary<string, List<string>>>(stream, cancellationToken: ct)
                   ?? [];
        return FromDictionary(data);
    }

    public async Task SaveAsync(string path, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, _values, JsonOptions, ct);
    }
}
