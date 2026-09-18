# Encryption at rest

Passwords and other secrets in module settings (currently `SftpImport`/`SftpExport`'s `Password`
and `PrivateKeyPassphrase`) are never stored in the database in plain text. This document covers
how that works, how to manage the encryption key, and how to use the emergency decryption tool.

## How it works

Module settings are marked sensitive with a `[SensitiveValue]` attribute on the settings POCO's
string property, e.g.:

```csharp
public class SftpExportSettings
{
    [SensitiveValue]
    public string Password { get; set; } = string.Empty;
    ...
}
```

`ColdNet.Core.Security.SettingsEncryption` reflects over a settings type's `[SensitiveValue]`
properties whenever `ModuleInstance.SettingsJson` is read or written, transforming just those
fields:

- **Admin UI** - a marked field renders as a masked `<input type="password">` with a Show/Hide
  toggle (`ModuleSettingsEditor.razor`) instead of plain text. Saving a module encrypts its
  sensitive fields before the JSON is persisted; loading a module for editing decrypts them first
  so the form shows real values. The raw JSON view (toggle "Show JSON") is the one place that still
  shows plaintext while editing - it's a bidirectional, hand-editable view, so masking it there
  would risk the user overwriting a real value with a redacted placeholder. A warning is shown
  next to it when a module has sensitive fields.
- **Worker** - `ModuleExecutionContext.GetSettings<T>()` decrypts before deserializing, so modules
  themselves only ever see plaintext at the point they use it (e.g. handing a password to
  `SSH.NET`).
- **Chain/group export** (JSON download from the Admin UI) - sensitive fields are **redacted**
  (blanked to `""`), not exported encrypted or in plaintext. Ciphertext wouldn't be portable to an
  instance with a different encryption key, and exporting plaintext would defeat the point.
  Re-enter passwords manually after importing an exported chain.
- **Chain/group import** - sensitive fields in the imported JSON (plaintext, since exports are
  always redacted) are encrypted before the chain is saved to the database.

Encryption is **AES-256-GCM** (`ColdNet.Core.Security.AesSecretProtector`), an authenticated cipher
- decryption fails loudly (well, falls back to treating the value as unencrypted, see below) if the
ciphertext has been tampered with or the wrong key is used. Each encrypted value is stored as
`enc:v1:<base64(nonce + tag + ciphertext)>`, with a fresh random nonce per value, so encrypting the
same password twice produces different ciphertext.

Values that don't start with the `enc:v1:` prefix are treated as legacy plaintext and passed
through unchanged by `Unprotect` - this means existing unencrypted `SettingsJson` rows keep working
without a migration; they get encrypted the next time that module is saved from the Admin UI.

## The encryption key

The key is a random 256-bit value, base64-encoded, read from configuration key
`ColdNet:Encryption:Key`. **`ColdNet.Admin` and `ColdNet.Worker` must use the same key** - they
read and write the same database.

A key ships in both `appsettings.json` files as a public, dev-only default so the app works out of
the box, the same pattern already used for the EdmVault REST API password. A warning is logged at
startup whenever this exact default key is still in use. Before storing anything real, override it
per instance with an environment variable (`ColdNet__Encryption__Key`) or `dotnet user-secrets`
(dev) - never commit a real key to source control.

Generate a new key with:

```bash
ColdNet.SecretTool.exe generate-key
```

**Rotating the key** requires re-encrypting existing data: decrypt every sensitive field with the
old key and re-save it (e.g. re-save each affected module from the Admin UI) after switching to the
new key, since a value encrypted with the old key cannot be decrypted with the new one - the module
falls back to treating it as an opaque (wrong) plaintext value rather than failing loudly, so check
`ColdNet.SecretTool.exe list-modules` / `decrypt-module` after rotating to confirm nothing was
missed.

## Emergency decryption: ColdNet.SecretTool

A command-line tool for admins who need a value in plain text without going through the Admin UI -
e.g. no network access to the web UI, or inspecting a raw database file directly. It's published
self-contained alongside Admin and Worker in the release package (`SecretTool\` folder).

It reads the same `ColdNet:Encryption:Key` and `ConnectionStrings:ColdNet` /
`ColdNet:Database:Provider` configuration as Admin/Worker from its own `appsettings.json` - point
it at the same key and the same database to use it against a real instance.

The module-based commands (`list-modules`, `decrypt-module`) only run if the tool's module DLLs are
signed by the trusted publisher, like Admin and Worker - see [MODULE-SIGNING.md](MODULE-SIGNING.md#secrettool).

```bash
# Generate a new random key (for initial setup or key rotation)
ColdNet.SecretTool.exe generate-key

# Encrypt/decrypt a single value by hand (e.g. to fix a row directly in the database)
ColdNet.SecretTool.exe encrypt "my-password"
ColdNet.SecretTool.exe decrypt "enc:v1:...."

# Find every module instance that has encrypted fields
ColdNet.SecretTool.exe list-modules

# Show the decrypted values of one module instance's sensitive fields
ColdNet.SecretTool.exe decrypt-module <module-instance-id>
```

`list-modules` and `decrypt-module` connect to the configured database directly (no Admin/Worker
process needs to be running) and use the same `ModuleRegistry` module discovery as the rest of the
app to know which fields on a given module type are sensitive.

Treat this tool like the database itself: anyone who can run it against a production database and
knows (or can read) the encryption key can recover every stored password. Restrict access to the
release package and the key accordingly.
