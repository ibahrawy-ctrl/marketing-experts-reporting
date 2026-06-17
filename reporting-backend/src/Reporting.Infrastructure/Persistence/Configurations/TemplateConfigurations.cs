using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Templates;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class ReportTemplateConfiguration : IEntityTypeConfiguration<ReportTemplate>
{
    public void Configure(EntityTypeBuilder<ReportTemplate> b)
    {
        b.ToTable("report_templates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.DefaultPeriodType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Classification).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.JobRoleId);
        b.HasMany(x => x.Versions).WithOne(x => x.ReportTemplate!)
            .HasForeignKey(x => x.ReportTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReportTemplateVersionConfiguration : IEntityTypeConfiguration<ReportTemplateVersion>
{
    public void Configure(EntityTypeBuilder<ReportTemplateVersion> b)
    {
        b.ToTable("report_template_versions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ReportTemplateId, x.VersionNumber }).IsUnique();
        b.HasMany(x => x.Fields).WithOne(x => x.ReportTemplateVersion!)
            .HasForeignKey(x => x.ReportTemplateVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TemplateFieldConfiguration : IEntityTypeConfiguration<TemplateField>
{
    public void Configure(EntityTypeBuilder<TemplateField> b)
    {
        b.ToTable("template_fields");
        b.HasKey(x => x.Id);
        b.Property(x => x.Label).IsRequired().HasMaxLength(300);
        b.Property(x => x.Key).HasMaxLength(100);
        b.Property(x => x.FieldType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ConfigJson).HasColumnType("jsonb");
        b.HasIndex(x => x.ReportTemplateVersionId);
    }
}
