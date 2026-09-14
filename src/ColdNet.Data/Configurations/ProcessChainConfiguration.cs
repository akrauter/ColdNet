using ColdNet.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColdNet.Data.Configurations;

public class ProcessChainConfiguration : IEntityTypeConfiguration<ProcessChain>
{
    public void Configure(EntityTypeBuilder<ProcessChain> builder)
    {
        builder.ToTable("ProcessChains");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.WorkerName).IsRequired().HasMaxLength(100);

        builder.HasMany(c => c.Modules)
            .WithOne()
            .HasForeignKey(m => m.ProcessChainId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.ProcessGroupId);
        builder.HasIndex(c => c.WorkerName);
    }
}
