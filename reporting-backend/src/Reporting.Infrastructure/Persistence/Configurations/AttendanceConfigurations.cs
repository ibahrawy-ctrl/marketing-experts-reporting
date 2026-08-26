using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Attendance;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class AttendanceIncidentTypeConfiguration : IEntityTypeConfiguration<AttendanceIncidentType>
{
    public void Configure(EntityTypeBuilder<AttendanceIncidentType> b)
    {
        b.ToTable("attendance_incident_types");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(40);
        b.Property(x => x.NameAr).IsRequired().HasMaxLength(120);
        // الرمز مفتاح البذر المُتكافئ ⟹ التفرّد شرط لا تحسين.
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class AttendanceIncidentConfiguration : IEntityTypeConfiguration<AttendanceIncident>
{
    public void Configure(EntityTypeBuilder<AttendanceIncident> b)
    {
        b.ToTable("attendance_incidents");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.DetectionSource).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.HrDecision).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Description).IsRequired().HasMaxLength(2000);
        b.Property(x => x.EmployeeResponse).HasMaxLength(2000);
        b.Property(x => x.HrNote).HasMaxLength(2000);
        b.Property(x => x.IdempotencyKey).HasMaxLength(80);

        // فهارس الطوابير والاستعلامات المُصرَّح بها في §6/P2-ATT-005.
        b.HasIndex(x => new { x.SubjectUserId, x.IncidentDate });
        b.HasIndex(x => new { x.Status, x.TeamId });
        b.HasIndex(x => new { x.Status, x.DepartmentId });
        b.HasIndex(x => new { x.IncidentTypeId, x.IncidentDate });
        // فهرس كشف التكرار: نفس الموظّف/اليوم/النوع.
        b.HasIndex(x => new { x.SubjectUserId, x.IncidentDate, x.IncidentTypeId });
        // تكافؤ الإرسال: مفتاح واحد لكلّ مُبلِّغ. الجزئيّة تمنع اصطدام الصفوف بلا مفتاح.
        b.HasIndex(x => new { x.ReportedByUserId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
    }
}

public class AttendanceIncidentEventConfiguration : IEntityTypeConfiguration<AttendanceIncidentEvent>
{
    public void Configure(EntityTypeBuilder<AttendanceIncidentEvent> b)
    {
        b.ToTable("attendance_incident_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired().HasMaxLength(50);
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.Property(x => x.ChangesJson).HasColumnType("jsonb");
        b.HasIndex(x => x.IncidentId);
    }
}

public class AttendanceIncidentAttachmentConfiguration : IEntityTypeConfiguration<AttendanceIncidentAttachment>
{
    public void Configure(EntityTypeBuilder<AttendanceIncidentAttachment> b)
    {
        b.ToTable("attendance_incident_attachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).IsRequired().HasMaxLength(260);
        b.Property(x => x.ContentType).IsRequired().HasMaxLength(120);
        b.Property(x => x.StoredPath).IsRequired().HasMaxLength(500);
        b.Property(x => x.ContentHash).IsRequired().HasMaxLength(64);
        b.HasIndex(x => x.IncidentId);
    }
}
