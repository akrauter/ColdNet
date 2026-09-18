using System.Reflection;
using ColdNet.Core.Security;
using ColdNet.Data;
using ColdNet.Engine;
using ColdNet.Engine.Modules;
using ColdNet.Engine.Scheduling;
using ColdNet.EdmVault;
using ColdNet.Worker;

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.AddColdNetDataAccess(builder.Configuration);
builder.Services.AddColdNetSecretProtection(builder.Configuration);
builder.Services.AddEdmVault(builder.Configuration);
builder.Services.AddColdNetEngine(
    [typeof(ColdNet.Modules.Import.ColdImportModule).Assembly, typeof(EdmVaultExportModule).Assembly, .. pluginAssemblies]);

builder.Services.Configure<ColdNetWorkerOptions>(builder.Configuration.GetSection("ColdNet:Worker"));
builder.Services.Configure<ChainSchedulerOptions>(builder.Configuration.GetSection("ColdNet:Worker"));

builder.Services.AddHostedService<ChainWorker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ColdNetDbContext>();
    await DbInitializer.MigrateAndSeedAsync(db);
}

host.Run();
return 0;
