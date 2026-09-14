using ColdNet.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ColdNet.Data;

/// <summary>
/// The job/configuration store. In d.cold terms this is the combined role of the d.cold database
/// (job queue + status) and the Config directory (process chains, module configuration files) -
/// here unified behind EF Core so both SQLite (default, zero-setup) and SQL Server are supported,
/// matching the database systems d.cold itself requires.
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
