using ColdNet.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColdNet.Data.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.FilePrefix).IsRequired().HasMaxLength(200);
        builder.Property(j => j.WorkDirectory).IsRequired().HasMaxLength(1000);
        builder.Property(j => j.ErrorMessage).HasMaxLength(4000);

        // A chain can never have two jobs with the same file prefix at the same time.
        builder.HasIndex(j => new { j.ProcessChainId, j.FilePrefix }).IsUnique();
        builder.HasIndex(j => new { j.ProcessChainId, j.Status, j.CurrentModuleOrder });
    }
}
