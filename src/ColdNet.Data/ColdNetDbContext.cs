using ColdNet.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ColdNet.Data;

/// <summary>
/// The job/configuration store - the combined role of a job queue/status database and a config
/// directory of process chain/module configuration, unified behind EF Core so both SQLite
/// (default, zero-setup) and SQL Server are supported.
/// </summary>
public class ColdNetDbContext(DbContextOptions<ColdNetDbContext> options) : DbContext(options)
{
    public DbSet<ProcessGroup> ProcessGroups => Set<ProcessGroup>();

    public DbSet<ProcessChain> ProcessChains => Set<ProcessChain>();

    public DbSet<ModuleInstance> ModuleInstances => Set<ModuleInstance>();

    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ColdNetDbContext).Assembly);
    }
}
