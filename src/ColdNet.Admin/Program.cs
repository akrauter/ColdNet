using System.Text;
using ColdNet.Admin.Components;
using ColdNet.Core.ExportImport;
using ColdNet.Data;
using ColdNet.EdmVault;
using ColdNet.Engine;
using ColdNet.Engine.Scheduling;
using Microsoft.EntityFrameworkCore;

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

app.MapGet("/api/export/chains/{id:guid}", async (Guid id, IDbContextFactory<ColdNetDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var chain = await db.ProcessChains
        .Include(c => c.Modules)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (chain is null)
    {
        return Results.NotFound();
    }

    var json = ProcessExportImportService.ExportChain(chain);
    var safeName = string.Join("_", chain.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    if (string.IsNullOrWhiteSpace(safeName)) safeName = "chain";

    return Results.File(
        Encoding.UTF8.GetBytes(json),
        contentType: "application/json",
        fileDownloadName: $"{safeName}.coldchain.json");
});

app.MapGet("/api/export/groups/{id:guid}", async (Guid id, IDbContextFactory<ColdNetDbContext> dbFactory) =>
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

    var json = ProcessExportImportService.ExportGroup(group);
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
