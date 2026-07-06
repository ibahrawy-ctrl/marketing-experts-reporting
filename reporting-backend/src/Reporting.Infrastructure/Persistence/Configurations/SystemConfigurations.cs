using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.System;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutbox>
{
    public void Configure(EntityTypeBuilder<EmailOutbox> b)
    {
        b.ToTable("email_outbox");
        b.HasKey(x => x.Id);
        b.Property(x => x.ToEmail).IsRequired().HasMaxLength(320);
        b.Property(x => x.ToName).HasMaxLength(300);
        b.Property(x => x.Subject).IsRequired().HasMaxLength(500);
        b.Property(x => x.Type).IsRequired().HasMaxLength(80);
        b.Property(x => x.LastError).HasMaxLength(2000);
        // فهرس انتقاء الرسائل المستحقّة للإرسال بكفاءة.
        b.HasIndex(x => new { x.Status, x.NextAttemptUtc });
        b.HasIndex(x => x.RecipientId);
    }
}

public class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    public void Configure(EntityTypeBuilder<EmailNotification> b)
    {
        b.ToTable("email_notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.EventType).IsRequired().HasMaxLength(80);
        b.Property(x => x.EntityType).IsRequired().HasMaxLength(100);
        b.Property(x => x.RecipientEmail).HasMaxLength(320);
        b.Property(x => x.RecipientName).HasMaxLength(300);
        b.Property(x => x.Subject).IsRequired().HasMaxLength(500);
        b.Property(x => x.FailureReason).HasMaxLength(2000);
        b.Property(x => x.CorrelationKey).IsRequired().HasMaxLength(300);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
        // مفتاح الترابط الفريد لمنع التكرار.
        b.HasIndex(x => x.CorrelationKey).IsUnique();
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.EventType);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.RecipientUserId);
        b.HasIndex(x => x.CreatedAtUtc);
    }
}

public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> b)
    {
        b.ToTable("email_templates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).IsRequired().HasMaxLength(80);
        b.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        b.Property(x => x.Category).IsRequired().HasMaxLength(40);
        b.Property(x => x.SubjectTemplate).IsRequired().HasMaxLength(500);
        b.Property(x => x.BodyTemplate).IsRequired();
        b.Property(x => x.DefaultMode).IsRequired().HasMaxLength(20);
        b.HasIndex(x => x.Key).IsUnique();
        b.HasIndex(x => x.Category);
    }
}

public class EmailRuleConfiguration : IEntityTypeConfiguration<EmailRule>
{
    public void Configure(EntityTypeBuilder<EmailRule> b)
    {
        b.ToTable("email_rules");
        b.HasKey(x => x.Id);
        b.Property(x => x.TemplateKey).IsRequired().HasMaxLength(80);
        b.Property(x => x.EventType).IsRequired().HasMaxLength(80);
        b.Property(x => x.Mode).IsRequired().HasMaxLength(20);
        b.HasIndex(x => new { x.TemplateKey, x.EventType }).IsUnique();
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).IsRequired().HasMaxLength(80);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Link).HasMaxLength(500);
        b.HasIndex(x => new { x.RecipientId, x.IsRead });
        b.HasIndex(x => new { x.RecipientId, x.CreatedAtUtc });
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired().HasMaxLength(100);
        b.Property(x => x.EntityType).IsRequired().HasMaxLength(100);
        b.Property(x => x.DataJson).HasColumnType("jsonb");
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.ActorId);
    }
}
