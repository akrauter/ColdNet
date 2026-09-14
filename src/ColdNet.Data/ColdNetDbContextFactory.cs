using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ColdNet.Data;

/// <summary>Enables `dotnet ef migrations add` from this project without a running host.</summary>
public class ColdNetDbContextFactory : IDesignTimeDbContextFactory<ColdNetDbContext>
{
    public ColdNetDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ColdNetDbContext>();
        builder.UseSqlite("Data Source=coldnet.design.db");
        return new ColdNetDbContext(builder.Options);
    }
}
