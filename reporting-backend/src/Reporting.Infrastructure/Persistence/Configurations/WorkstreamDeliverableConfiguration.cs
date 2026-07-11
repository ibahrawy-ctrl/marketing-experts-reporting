using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Clients;

namespace Reporting.Infrastructure.Persistence.Configurations;

/// <summary>
/// إعداد كيان مخرَج خطّة الإنتاج (P2). جدول جديد بحتٌ إضافيّ — لا يمسّ أيّ جدول قائم.
/// FK على WorkstreamId فقط (Cascade). لا FK على المسؤول (مرجع Guid ليَبقى مرنًا).
/// لا قيد تفرّد — عدد المخرَجات غير محدود لكل تيار عمل. النوع/الاستخدام نصّ من الكتالوج بلا FK.
/// </summary>
public class WorkstreamDeliverableConfiguration : IEntityTypeConfiguration<WorkstreamDeliverable>
{
    public void Configure(EntityTypeBuilder<WorkstreamDeliverable> b)
    {
        b.ToTable("workstream_deliverables");
        b.HasKey(x => x.Id);

        b.Property(x => x.DeliverableTypeCode).IsRequired().HasMaxLength(100);
        b.Property(x => x.UsageContextCode).HasMaxLength(100);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.EstimatedHours).HasColumnType("numeric(10,2)");
        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);

        b.HasOne(x => x.Workstream)
            .WithMany()
            .HasForeignKey(x => x.WorkstreamId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.WorkstreamId);
    }
}
