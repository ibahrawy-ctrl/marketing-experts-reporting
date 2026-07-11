using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Clients;

namespace Reporting.Infrastructure.Persistence.Configurations;

/// <summary>
/// إعداد كيان تيار العمل (P1). جدول جديد بحتٌ إضافيّ — لا يمسّ جدول المشاريع.
/// FK على ProjectId فقط (Cascade). لا FK على الفريق/المدير (مرجع Guid لِيَبقى مرنًا).
/// تفرّد منطقيّ: لا يتكرّر نفس (المشروع، نوع تيار العمل، الفريق المسؤول).
/// </summary>
public class ProjectWorkstreamConfiguration : IEntityTypeConfiguration<ProjectWorkstream>
{
    public void Configure(EntityTypeBuilder<ProjectWorkstream> b)
    {
        b.ToTable("project_workstreams");
        b.HasKey(x => x.Id);

        b.Property(x => x.WorkstreamTypeCode).IsRequired().HasMaxLength(100);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        b.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => new { x.ProjectId, x.WorkstreamTypeCode, x.ResponsibleTeamId }).IsUnique();
    }
}
