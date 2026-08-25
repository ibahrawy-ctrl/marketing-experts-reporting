using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.EmployeeServices;

namespace Reporting.Infrastructure.Persistence.Configurations;

/// <summary>
/// P2-HR-010 — جدول البنود **اليدويّة** وحدها. لا يحتوي بندًا محسوبًا أبدًا،
/// والتفرّد على (الموظّف، المفتاح) يمنع نسختين متنافستين للبند نفسه.
/// </summary>
public class EmployeeChecklistRecordConfiguration : IEntityTypeConfiguration<EmployeeChecklistRecord>
{
    public void Configure(EntityTypeBuilder<EmployeeChecklistRecord> b)
    {
        b.ToTable("employee_checklist_items");
        b.HasKey(x => x.Id);

        b.Property(x => x.ItemKey).IsRequired().HasMaxLength(60);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.EvidenceReference).HasMaxLength(200);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.ConcurrencyStamp).IsRequired().HasMaxLength(40);

        // بند واحد لكلّ موظّف — صفّان بنفس المفتاح كانا سيجعلان «الحالة» سؤالًا بلا جواب.
        b.HasIndex(x => new { x.SubjectUserId, x.ItemKey }).IsUnique();
        b.HasIndex(x => x.OwnerUserId);
    }
}
