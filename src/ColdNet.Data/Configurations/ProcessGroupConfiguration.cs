using ColdNet.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColdNet.Data.Configurations;

public class ProcessGroupConfiguration : IEntityTypeConfiguration<ProcessGroup>
{
    public void Configure(EntityTypeBuilder<ProcessGroup> builder)
    {
        builder.ToTable("ProcessGroups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
        builder.Property(g => g.ShortName).HasMaxLength(10);
        builder.Property(g => g.Description).HasMaxLength(1000);

        builder.HasMany(g => g.Chains)
            .WithOne()
            .HasForeignKey(c => c.ProcessGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
