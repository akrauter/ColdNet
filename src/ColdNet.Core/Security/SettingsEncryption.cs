using System.Reflection;
using System.Text.Json;

namespace ColdNet.Core.Security;

/// <summary>
/// Applies an <see cref="ISecretProtector"/> to just the <see cref="SensitiveValueAttribute"/>-marked
/// string properties of a module settings JSON blob (<c>ModuleInstance.SettingsJson</c>), leaving
/// everything else untouched. Used at every boundary where that JSON crosses into or out of
/// storage: saving/loading a module in the admin UI, running a module, and export/import.
/// </summary>
public static class SettingsEncryption
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The settings type's string properties marked <see cref="SensitiveValueAttribute"/>, if any.</summary>
    public static IReadOnlyList<PropertyInfo> GetSensitiveProperties(Type settingsType) =>
        settingsType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.CanRead && p.CanWrite)
            .Where(p => p.GetCustomAttribute<SensitiveValueAttribute>() is not null)
            .ToList();

    public static bool HasSensitiveProperties(Type? settingsType) =>
        settingsType is not null && GetSensitiveProperties(settingsType).Count > 0;

    /// <summary>Encrypts every sensitive field's current (plaintext) value in <paramref name="json"/>, ready for storage.</summary>
    public static string Encrypt(string json, Type? settingsType, ISecretProtector protector) =>
        Transform(json, settingsType, protector.Protect);

    /// <summary>Decrypts every sensitive field's stored (possibly encrypted) value in <paramref name="json"/>, ready for use/editing.</summary>
    public static string Decrypt(string json, Type? settingsType, ISecretProtector protector) =>
        Transform(json, settingsType, protector.Unprotect);

    /// <summary>Blanks out every sensitive field, for a chain/group export file that might be shared or committed.</summary>
    public static string RedactForExport(string json, Type? settingsType) =>
        Transform(json, settingsType, _ => string.Empty);

    private static string Transform(string json, Type? settingsType, Func<string?, string> transform)
    {
        if (settingsType is null || string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
        {
            return json;
        }

        var sensitiveProps = GetSensitiveProperties(settingsType);
        if (sensitiveProps.Count == 0)
        {
            return json;
        }

        try
        {
            var instance = JsonSerializer.Deserialize(json, settingsType, JsonOptions);
            if (instance is null)
            {
                return json;
            }

            foreach (var prop in sensitiveProps)
            {
                var current = (string?)prop.GetValue(instance);
                prop.SetValue(instance, transform(current));
            }

            return JsonSerializer.Serialize(instance, settingsType, JsonOptions);
        }
        catch (JsonException)
        {
            // Malformed settings JSON isn't this helper's problem to solve - leave it as-is and
            // let whatever validates JSON elsewhere (the admin UI's settings editor) catch it.
            return json;
        }
    }
}
