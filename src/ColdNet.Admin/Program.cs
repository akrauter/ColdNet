using System.Reflection;
using System.Text;
using ColdNet.Admin.Components;
using ColdNet.Core.ExportImport;
using ColdNet.Core.Security;
using ColdNet.Data;
using ColdNet.EdmVault;
using ColdNet.Engine;
using ColdNet.Engine.Modules;
using ColdNet.Engine.Scheduling;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Must run before anything touches a module assembly (AddEdmVault below already does): verifies the
// built-in modules' signatures on disk first and loads only signed plugin assemblies.
Assembly[] pluginAssemblies;
using (var startupLogFactory = LoggerFactory.Create(b => b.AddSimpleConsole()))
{
    var startupLogger = startupLogFactory.CreateLogger("ColdNet.ModuleSigning");
    try
    {
        pluginAssemblies = ModuleTrust.VerifyAndLoad(
            builder.Configuration.GetSection("ColdNet:ModuleSigning").Get<ModuleSigningOptions>() ?? new ModuleSigningOptions(),
            startupLogger,
            AppContext.BaseDirectory,
            ModuleTrust.BuiltInAssemblyFileNames).ToArray();
    }
    catch (ModuleSignatureException ex)
    {
        startupLogger.LogCritical("{Message}", ex.Message);
        return 1;
    }
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddColdNetDataAccess(builder.Configuration);
builder.Services.AddColdNetSecretProtection(builder.Configuration);
builder.Services.AddEdmVault(builder.Configuration);
builder.Services.AddColdNetEngine(
    [typeof(ColdNet.Modules.Import.ColdImportModule).Assembly, typeof(EdmVaultExportModule).Assembly, .. pluginAssemblies]);
builder.Services.Configure<ChainSchedulerOptions>(builder.Configuration.GetSection("ColdNet:Worker"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/api/export/chains/{id:guid}", async (Guid id, IDbContextFactory<ColdNetDbContext> dbFactory, ModuleRegistry registry) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var chain = await db.ProcessChains
        .Include(c => c.Modules)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (chain is null)
    {
        return Results.NotFound();
    }

    var dto = ProcessExportImportService.ToDto(chain);
    RedactSensitiveSettings(dto, registry);
    var json = System.Text.Json.JsonSerializer.Serialize(dto, ProcessExportImportService.SerializerOptions);
    var safeName = string.Join("_", chain.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    if (string.IsNullOrWhiteSpace(safeName)) safeName = "chain";

    return Results.File(
        Encoding.UTF8.GetBytes(json),
        contentType: "application/json",
        fileDownloadName: $"{safeName}.coldchain.json");
});

app.MapGet("/api/export/groups/{id:guid}", async (Guid id, IDbContextFactory<ColdNetDbContext> dbFactory, ModuleRegistry registry) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var group = await db.ProcessGroups
        .Include(g => g.Chains)
        .ThenInclude(c => c.Modules)
        .FirstOrDefaultAsync(g => g.Id == id);

    if (group is null)
    {
        return Results.NotFound();
    }

    var dto = ProcessExportImportService.ToDto(group);
    foreach (var chainDto in dto.Chains)
    {
        RedactSensitiveSettings(chainDto, registry);
    }
    var json = System.Text.Json.JsonSerializer.Serialize(dto, ProcessExportImportService.SerializerOptions);
    var safeName = string.Join("_", group.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    if (string.IsNullOrWhiteSpace(safeName)) safeName = "group";

    return Results.File(
        Encoding.UTF8.GetBytes(json),
        contentType: "application/json",
        fileDownloadName: $"{safeName}.coldgroup.json");
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ColdNetDbContext>();
    await DbInitializer.MigrateAndSeedAsync(db);
}

app.Run();
return 0;

// Exported chain/group JSON is a file on disk that might get shared, committed, or attached to a
// ticket - blank out passwords/passphrases rather than either leaking plaintext or exporting
// ciphertext that's only decryptable with this instance's own encryption key.
static void RedactSensitiveSettings(ChainExportDto chain, ModuleRegistry registry)
{
    foreach (var module in chain.Modules)
    {
        var settingsType = registry.Find(module.ModuleTypeName)?.SettingsType;
        module.SettingsJson = SettingsEncryption.RedactForExport(module.SettingsJson, settingsType);
    }
}
