using ColdNet.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColdNet.Data.Configurations;

public class ModuleInstanceConfiguration : IEntityTypeConfiguration<ModuleInstance>
{
    public void Configure(EntityTypeBuilder<ModuleInstance> builder)
    {
        builder.ToTable("ModuleInstances");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.ModuleTypeName).IsRequired().HasMaxLength(200);
        builder.Property(m => m.DisplayName).HasMaxLength(200);
        builder.Property(m => m.SettingsJson).IsRequired();

        builder.ComplexProperty(m => m.CommonSettings, cs =>
        {
            cs.Property(p => p.Directory).HasColumnName("Directory").HasMaxLength(1000);
            cs.Property(p => p.OutputDirectory).HasColumnName("OutputDirectory").HasMaxLength(1000);
            cs.Property(p => p.FileExtension).HasColumnName("FileExtension").HasMaxLength(20);
            cs.Property(p => p.OutputFileExtension).HasColumnName("OutputFileExtension").HasMaxLength(20);
            cs.Property(p => p.Save).HasColumnName("Save");
            cs.Property(p => p.DeleteSourceFile).HasColumnName("DeleteSourceFile");
            cs.Property(p => p.MaskForDms).HasColumnName("MaskForDms");
            cs.Property(p => p.Append).HasColumnName("Append");
        });

        builder.ComplexProperty(m => m.DmsSupport, ds =>
        {
            ds.Property(p => p.Enabled).HasColumnName("DmsSupportEnabled");
            ds.Property(p => p.DocumentType).HasColumnName("DmsDocumentType").HasMaxLength(200);
        });

        builder.HasIndex(m => new { m.ProcessChainId, m.Order }).IsUnique();
    }
}
