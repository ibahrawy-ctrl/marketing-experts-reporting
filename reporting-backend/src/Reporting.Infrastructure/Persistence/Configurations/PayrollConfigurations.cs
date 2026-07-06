using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Entities.Payroll;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class PayrollImpactReviewConfiguration : IEntityTypeConfiguration<PayrollImpactReview>
{
    public void Configure(EntityTypeBuilder<PayrollImpactReview> b)
    {
        b.ToTable("payroll_impact_reviews");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FinanceNote).HasMaxLength(2000);
        // مراجعة واحدة لكل طلب — يضمن فرادة Lazy create على مستوى القاعدة.
        b.HasIndex(x => x.LeaveRequestId).IsUnique();
        // FK لطلب الإجازة بلا Cascade (Restrict): لا يُحذف الطلب الأصلي عبر هذا الكيان، وعلاقة قراءة فقط.
        b.HasOne<LeaveRequest>()
            .WithMany()
            .HasForeignKey(x => x.LeaveRequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
