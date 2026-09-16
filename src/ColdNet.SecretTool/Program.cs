using System.Reflection;
using ColdNet.Core.Security;
using ColdNet.Data;
using ColdNet.Engine.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// ColdNet.SecretTool - Notfall-Werkzeug fuer Administratoren.
//
// Passwoerter und andere als [SensitiveValue] markierte Felder werden in der Datenbank nur
// verschluesselt abgelegt (AES-256-GCM, siehe ColdNet.Core.Security). Das Admin-Webinterface kann
// sie deshalb anzeigen/bearbeiten - aber wer keinen Zugriff auf die Weboberflaeche hat (z.B. bei
// einem Notfall direkt auf dem Datenbankserver, oder um einen alten DB-Export zu pruefen), braucht
// dieses Kommandozeilen-Tool. Es liest denselben Verschluesselungsschluessel wie Admin/Worker aus
// appsettings.json (ColdNet:Encryption:Key) - der Schluessel MUSS uebereinstimmen.

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = args[0].ToLowerInvariant();

try
{
    switch (command)
    {
        case "generate-key":
            Console.WriteLine(AesSecretProtector.GenerateBase64Key());
            return 0;

        case "encrypt":
            RequireArgs(args, 2, "encrypt <klartext>");
            Console.WriteLine(CreateProtector(configuration).Protect(args[1]));
            return 0;

        case "decrypt":
            RequireArgs(args, 2, "decrypt <chiffretext>");
            Console.WriteLine(CreateProtector(configuration).Unprotect(args[1]));
            return 0;

        case "list-modules":
            await ListModulesAsync(configuration);
            return 0;

        case "decrypt-module":
            RequireArgs(args, 2, "decrypt-module <module-instance-id>");
            if (!Guid.TryParse(args[1], out var moduleInstanceId))
            {
                Console.Error.WriteLine($"'{args[1]}' ist keine gueltige GUID.");
                return 1;
            }
            return await DecryptModuleAsync(configuration, moduleInstanceId);

        case "-h":
        case "--help":
        case "help":
            PrintUsage();
            return 0;

        default:
            Console.Error.WriteLine($"Unbekanntes Kommando: {command}");
            PrintUsage();
            return 1;
    }
}
catch (UsageException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static void RequireArgs(string[] args, int minLength, string usage)
{
    if (args.Length < minLength)
    {
        throw new UsageException($"Verwendung: ColdNet.SecretTool {usage}");
    }
}

static ISecretProtector CreateProtector(IConfiguration configuration)
{
    var key = configuration["ColdNet:Encryption:Key"];
    if (string.IsNullOrWhiteSpace(key))
    {
        throw new UsageException(
            "Kein Verschluesselungsschluessel gefunden. Erwartet wird 'ColdNet:Encryption:Key' in " +
            "appsettings.json (muss mit dem Schluessel von ColdNet.Admin/ColdNet.Worker uebereinstimmen).");
    }

    try
    {
        return AesSecretProtector.FromBase64Key(key);
    }
    catch (Exception ex) when (ex is FormatException or ArgumentException)
    {
        throw new UsageException($"'ColdNet:Encryption:Key' ist kein gueltiger Base64-256-Bit-Schluessel: {ex.Message}");
    }
}

static ModuleRegistry CreateModuleRegistry()
{
    Assembly[] assemblies =
    [
        typeof(ColdNet.Modules.Import.ColdImportModule).Assembly,
        typeof(ColdNet.EdmVault.EdmVaultExportModule).Assembly,
    ];
    return new ModuleRegistry(assemblies);
}

static ColdNetDbContext CreateDbContext(IConfiguration configuration)
{
    var provider = configuration["ColdNet:Database:Provider"] ?? "Sqlite";
    var connectionString = configuration.GetConnectionString("ColdNet") ?? "Data Source=coldnet.db";

    var optionsBuilder = new DbContextOptionsBuilder<ColdNetDbContext>();
    if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        optionsBuilder.UseSqlServer(connectionString);
    }
    else
    {
        optionsBuilder.UseSqlite(connectionString);
    }

    return new ColdNetDbContext(optionsBuilder.Options);
}

static async Task ListModulesAsync(IConfiguration configuration)
{
    var registry = CreateModuleRegistry();
    await using var db = CreateDbContext(configuration);

    var chainNames = await db.ProcessChains.ToDictionaryAsync(c => c.Id, c => c.Name);
    var modules = await db.ModuleInstances.OrderBy(m => m.ProcessChainId).ThenBy(m => m.Order).ToListAsync();

    var withSecrets = modules
        .Where(m => SettingsEncryption.HasSensitiveProperties(registry.Find(m.ModuleTypeName)?.SettingsType))
        .ToList();

    if (withSecrets.Count == 0)
    {
        Console.WriteLine("Keine Modulinstanzen mit verschluesselten Feldern gefunden.");
        return;
    }

    Console.WriteLine("Modulinstanzen mit verschluesselten Feldern:");
    Console.WriteLine();
    foreach (var module in withSecrets)
    {
        var chainName = chainNames.GetValueOrDefault(module.ProcessChainId, "(unbekannte Kette)");
        Console.WriteLine($"  {module.Id}");
        Console.WriteLine($"    Kette:  {chainName}");
        Console.WriteLine($"    Modul:  {module.ModuleTypeName} ({module.DisplayName})");
        Console.WriteLine();
    }
    Console.WriteLine("Details anzeigen mit: ColdNet.SecretTool decrypt-module <id>");
}

static async Task<int> DecryptModuleAsync(IConfiguration configuration, Guid moduleInstanceId)
{
    var registry = CreateModuleRegistry();
    var protector = CreateProtector(configuration);
    await using var db = CreateDbContext(configuration);

    var module = await db.ModuleInstances.FirstOrDefaultAsync(m => m.Id == moduleInstanceId);
    if (module is null)
    {
        Console.Error.WriteLine($"Keine Modulinstanz mit Id '{moduleInstanceId}' gefunden.");
        return 1;
    }

    var settingsType = registry.Find(module.ModuleTypeName)?.SettingsType;
    if (!SettingsEncryption.HasSensitiveProperties(settingsType))
    {
        Console.WriteLine($"Modul '{module.ModuleTypeName}' ({module.DisplayName}) hat keine verschluesselten Felder.");
        return 0;
    }

    var chainName = await db.ProcessChains
        .Where(c => c.Id == module.ProcessChainId)
        .Select(c => c.Name)
        .FirstOrDefaultAsync() ?? "(unbekannte Kette)";

    var decryptedJson = SettingsEncryption.Decrypt(module.SettingsJson, settingsType, protector);
    var decrypted = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
        decryptedJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

    Console.WriteLine($"Kette:  {chainName}");
    Console.WriteLine($"Modul:  {module.ModuleTypeName} ({module.DisplayName})");
    Console.WriteLine();

    foreach (var prop in SettingsEncryption.GetSensitiveProperties(settingsType!))
    {
        var jsonName = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];
        var value = decrypted.TryGetValue(jsonName, out var element) || decrypted.TryGetValue(prop.Name, out element)
            ? element.ToString()
            : "(nicht gesetzt)";
        Console.WriteLine($"  {prop.Name}: {value}");
    }

    return 0;
}

static void PrintUsage()
{
    Console.WriteLine("""
        ColdNet.SecretTool - Notfall-Werkzeug zum Ver-/Entschluesseln gespeicherter Kennwoerter

        Kommandos:
          generate-key                        Erzeugt einen neuen zufaelligen 256-Bit-Schluessel (Base64)
          encrypt <klartext>                   Verschluesselt einen Wert mit dem konfigurierten Schluessel
          decrypt <chiffretext>                 Entschluesselt einen Wert mit dem konfigurierten Schluessel
          list-modules                        Listet alle Modulinstanzen mit verschluesselten Feldern
          decrypt-module <module-instance-id> Zeigt die entschluesselten Werte einer Modulinstanz an

        Der Schluessel wird aus 'ColdNet:Encryption:Key' in appsettings.json gelesen (muss mit dem
        Schluessel von ColdNet.Admin/ColdNet.Worker uebereinstimmen) und kann per Umgebungsvariable
        ColdNet__Encryption__Key ueberschrieben werden.
        """);
}

sealed class UsageException(string message) : Exception(message);
