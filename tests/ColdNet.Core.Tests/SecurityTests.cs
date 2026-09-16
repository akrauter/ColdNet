using ColdNet.Core.Security;

namespace ColdNet.Core.Tests;

public class AesSecretProtectorTests
{
    private static AesSecretProtector CreateProtector() =>
        AesSecretProtector.FromBase64Key(AesSecretProtector.GenerateBase64Key());

    [Fact]
    public void Protect_then_Unprotect_round_trips_the_original_value()
    {
        var protector = CreateProtector();

        var ciphertext = protector.Protect("hunter2");

        Assert.NotEqual("hunter2", ciphertext);
        Assert.Equal("hunter2", protector.Unprotect(ciphertext));
    }

    [Fact]
    public void Protect_of_the_same_value_twice_produces_different_ciphertext()
    {
        // Random nonce per call - must never be reused, or GCM's confidentiality guarantee breaks.
        var protector = CreateProtector();

        var a = protector.Protect("hunter2");
        var b = protector.Protect("hunter2");

        Assert.NotEqual(a, b);
        Assert.Equal("hunter2", protector.Unprotect(a));
        Assert.Equal("hunter2", protector.Unprotect(b));
    }

    [Fact]
    public void Protect_of_null_or_empty_returns_empty_without_encrypting()
    {
        var protector = CreateProtector();

        Assert.Equal(string.Empty, protector.Protect(null));
        Assert.Equal(string.Empty, protector.Protect(string.Empty));
    }

    [Fact]
    public void Unprotect_of_plain_legacy_text_returns_it_unchanged()
    {
        var protector = CreateProtector();

        Assert.Equal("plaintext-password", protector.Unprotect("plaintext-password"));
    }

    [Fact]
    public void Unprotect_with_the_wrong_key_does_not_throw_and_returns_the_ciphertext_as_is()
    {
        var protectorA = CreateProtector();
        var protectorB = CreateProtector();

        var ciphertext = protectorA.Protect("hunter2");
        var result = protectorB.Unprotect(ciphertext);

        Assert.Equal(ciphertext, result);
    }

    [Fact]
    public void GenerateBase64Key_produces_a_valid_256_bit_key_each_time()
    {
        var key1 = AesSecretProtector.GenerateBase64Key();
        var key2 = AesSecretProtector.GenerateBase64Key();

        Assert.Equal(32, Convert.FromBase64String(key1).Length);
        Assert.NotEqual(key1, key2);
    }
}

public class SettingsEncryptionTests
{
    private class SettingsWithSecret
    {
        public string Host { get; set; } = string.Empty;

        [SensitiveValue]
        public string Password { get; set; } = string.Empty;
    }

    private class SettingsWithoutSecret
    {
        public string Host { get; set; } = string.Empty;
    }

    private static AesSecretProtector CreateProtector() =>
        AesSecretProtector.FromBase64Key(AesSecretProtector.GenerateBase64Key());

    [Fact]
    public void Encrypt_then_Decrypt_round_trips_the_sensitive_field_and_leaves_others_untouched()
    {
        var protector = CreateProtector();
        var json = """{"Host":"sftp.example.com","Password":"hunter2"}""";

        var encrypted = SettingsEncryption.Encrypt(json, typeof(SettingsWithSecret), protector);

        Assert.Contains("sftp.example.com", encrypted);
        Assert.DoesNotContain("hunter2", encrypted);

        var decrypted = SettingsEncryption.Decrypt(encrypted, typeof(SettingsWithSecret), protector);
        Assert.Contains("hunter2", decrypted);
    }

    [Fact]
    public void Encrypt_is_a_no_op_when_the_settings_type_has_no_sensitive_fields()
    {
        var protector = CreateProtector();
        var json = """{"Host":"sftp.example.com"}""";

        var result = SettingsEncryption.Encrypt(json, typeof(SettingsWithoutSecret), protector);

        Assert.Equal(json, result);
    }

    [Fact]
    public void Encrypt_is_a_no_op_when_settings_type_is_null()
    {
        var protector = CreateProtector();
        var json = """{"Password":"hunter2"}""";

        var result = SettingsEncryption.Encrypt(json, null, protector);

        Assert.Equal(json, result);
    }

    [Fact]
    public void RedactForExport_blanks_sensitive_fields_without_needing_a_protector()
    {
        var json = """{"Host":"sftp.example.com","Password":"hunter2"}""";

        var redacted = SettingsEncryption.RedactForExport(json, typeof(SettingsWithSecret));

        Assert.Contains("sftp.example.com", redacted);
        Assert.DoesNotContain("hunter2", redacted);
    }

    [Fact]
    public void Decrypt_of_already_plaintext_legacy_json_returns_it_unchanged_in_effect()
    {
        var protector = CreateProtector();
        var json = """{"Host":"sftp.example.com","Password":"hunter2"}""";

        var decrypted = SettingsEncryption.Decrypt(json, typeof(SettingsWithSecret), protector);

        Assert.Contains("hunter2", decrypted);
    }

    [Fact]
    public void GetSensitiveProperties_finds_only_attributed_string_properties()
    {
        var props = SettingsEncryption.GetSensitiveProperties(typeof(SettingsWithSecret));

        Assert.Single(props);
        Assert.Equal("Password", props[0].Name);
    }
}
