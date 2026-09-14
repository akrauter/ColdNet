using ColdNet.Data;
using ColdNet.Engine;
using ColdNet.Engine.Scheduling;
using ColdNet.EdmVault;
using ColdNet.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddColdNetDataAccess(builder.Configuration);
builder.Services.AddEdmVault(builder.Configuration);
builder.Services.AddColdNetEngine(
    typeof(ColdNet.Modules.Import.ColdImportModule).Assembly,
    typeof(EdmVaultExportModule).Assembly);

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
