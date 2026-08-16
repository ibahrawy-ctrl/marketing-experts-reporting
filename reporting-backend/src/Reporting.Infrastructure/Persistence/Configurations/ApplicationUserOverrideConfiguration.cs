using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Infrastructure.Identity;

namespace Reporting.Infrastructure.Persistence.Configurations;

/// <summary>
/// تكوين حقلَي التجاوز الصريح على ApplicationUser (ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1):
/// - ReportApproverOverrideUserId: معتمِد التقارير الصريح.
/// - KpiReviewerOverrideUserId: مراجِع KPI الصريح.
/// كلاهما اختياريّ (NULL = المسار الحالي دون تغيير)، FK ذاتيّ إلى AspNetUsers بسلوك Restrict (لا حذف
/// تسلسليّ)، وفهرس مستقلّ لكلّ حقل. لا يمسّ هذا التكوين ManagerId/TeamId/DepartmentId ولا أيّ علاقة قائمة.
/// </summary>
public class ApplicationUserOverrideConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(u => u.ReportApproverOverrideUserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(u => u.ReportApproverOverrideUserId);

        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(u => u.KpiReviewerOverrideUserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(u => u.KpiReviewerOverrideUserId);
    }
}
