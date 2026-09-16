namespace ColdNet.Core.Security;

/// <summary>
/// Marks a <c>string</c> property on a module settings class (e.g. <c>SftpImportSettings.Password</c>)
/// as a secret: the admin UI renders it as a masked password field instead of plain text, and it is
/// encrypted at rest in <c>ModuleInstance.SettingsJson</c> - see <see cref="SettingsEncryption"/>,
/// which is what actually reads this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveValueAttribute : Attribute;
