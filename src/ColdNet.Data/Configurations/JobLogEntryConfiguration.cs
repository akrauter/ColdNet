using ColdNet.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColdNet.Data.Configurations;

public class JobLogEntryConfiguration : IEntityTypeConfiguration<JobLogEntry>
{
    public void Configure(EntityTypeBuilder<JobLogEntry> builder)
    {
        builder.ToTable("JobLogEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ModuleTypeName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Message).HasMaxLength(4000);
        builder.HasIndex(e => new { e.JobId, e.TimestampUtc });
    }
}
