using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Submissions;

namespace Reporting.Infrastructure.Persistence.Configurations;

/// <summary>
/// تكوين منح رؤية التقارير المخفيّ (REPORT-VIEW-GRANTS-R1) — جدول جديد منفصل تمامًا،
/// لا مفاتيح خارجية صلبة على المستخدمين/الفرق (المعرّفات مرجعية فقط) كي لا يربط دورة حياة المنح
/// بحذف مستخدم/فريق، ويبقى الكيان معزولًا عن أيّ سلوك تنظيمي قائم.
/// </summary>
public class ReportViewGrantConfiguration : IEntityTypeConfiguration<ReportViewGrant>
{
    public void Configure(EntityTypeBuilder<ReportViewGrant> b)
    {
        b.ToTable("report_view_grants");
        b.HasKey(x => x.Id);
        b.Property(x => x.GranteeUserId).IsRequired();
        b.Property(x => x.ScopeKind).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasIndex(x => x.GranteeUserId);
        b.HasIndex(x => x.TargetUserId);
        b.HasIndex(x => x.TargetTeamId);
        b.HasIndex(x => x.IsActive);

        // فهرسان فريدان مُصفّيان لمنع تكرار منح نشط لنفس المستفيد/الهدف (الحارس النهائي في طبقة الخدمة أيضًا).
        // ScopeKind=User (0): فريد على (GranteeUserId, TargetUserId) للنشط فقط.
        b.HasIndex(x => new { x.GranteeUserId, x.TargetUserId })
            .IsUnique()
            .HasFilter("\"IsActive\" AND \"ScopeKind\" = 0");
        // ScopeKind=Team (1): فريد على (GranteeUserId, TargetTeamId) للنشط فقط.
        b.HasIndex(x => new { x.GranteeUserId, x.TargetTeamId })
            .IsUnique()
            .HasFilter("\"IsActive\" AND \"ScopeKind\" = 1");
    }
}
