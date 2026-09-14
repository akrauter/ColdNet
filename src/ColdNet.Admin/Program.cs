using ColdNet.Admin.Components;
using ColdNet.Data;
using ColdNet.EdmVault;
using ColdNet.Engine;
using ColdNet.Engine.Scheduling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddColdNetDataAccess(builder.Configuration);
builder.Services.AddEdmVault(builder.Configuration);
builder.Services.AddColdNetEngine(
    typeof(ColdNet.Modules.Import.ColdImportModule).Assembly,
    typeof(EdmVaultExportModule).Assembly);
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ColdNetDbContext>();
    await DbInitializer.MigrateAndSeedAsync(db);
}

app.Run();
