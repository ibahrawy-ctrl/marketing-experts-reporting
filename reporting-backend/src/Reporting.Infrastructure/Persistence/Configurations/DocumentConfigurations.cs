using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Enums;

namespace Reporting.Infrastructure.Persistence.Configurations;

/// <summary>
/// إعداد مستند العميل (CPW-R1B2 — خدمة المستندات، أوّل ارتباط مورد).
/// جدول إضافيّ بحتٌ. الحذف من جهة العميل <c>Restrict</c> لأنّ المستندات تحمل ملفّات فعليّة
/// على القرص، فمنع الحذف النهائيّ للعميل يمنع بقاء ملفّات يتيمة.
/// </summary>
public class ClientDocumentConfiguration : IEntityTypeConfiguration<ClientDocument>
{
    public void Configure(EntityTypeBuilder<ClientDocument> b)
    {
        b.ToTable("client_documents");
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.CategoryCode).IsRequired().HasMaxLength(100);
        b.Property(x => x.Tags).HasMaxLength(500);
        b.Property(x => x.ConfidentialityCode).HasMaxLength(100);
        b.Property(x => x.LifecycleStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ApprovalStatusCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.ArchiveReason).HasMaxLength(1000);
        b.Property(x => x.DeleteReason).HasMaxLength(1000);

        // CPW-R2 — سياسة الرؤية. نصّيّة بقيمة افتراضيّة في القاعدة ⇒ الصفوف القائمة تصبح ClientScoped بلا Backfill.
        b.Property(x => x.VisibilityType)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired()
            .HasDefaultValue(DocumentVisibilityType.ClientScoped);

        b.HasMany(x => x.AllowedRoles).WithOne(x => x.Document!)
            .HasForeignKey(x => x.ClientDocumentId).OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.AllowedUsers).WithOne(x => x.Document!)
            .HasForeignKey(x => x.ClientDocumentId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Client).WithMany(x => x.Documents)
            .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);

        // النسخة السارية — مفتاح أجنبيّ اختياريّ بلا ملاحة عكسيّة لكسر دورة المفاتيح.
        b.HasOne(x => x.CurrentVersion).WithMany()
            .HasForeignKey(x => x.CurrentVersionId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Versions).WithOne(x => x.Document!)
            .HasForeignKey(x => x.ClientDocumentId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ClientId);
        b.HasIndex(x => new { x.ClientId, x.CategoryCode });
        b.HasIndex(x => new { x.ClientId, x.IsArchived, x.IsDeleted })
            .HasDatabaseName("IX_client_documents_ClientId_Visibility");

        b.HasIndex(x => new { x.ClientId, x.VisibilityType })
            .HasDatabaseName("IX_client_documents_ClientId_VisibilityType");
    }
}

/// <summary>
/// إعداد الأدوار المسموح لها برؤية مستند (CPW-R2). جدول إضافيّ بحتٌ، Cascade من المستند.
/// فريد على (المستند، الدور) لمنع التكرار.
/// </summary>
public class ClientDocumentAllowedRoleConfiguration : IEntityTypeConfiguration<ClientDocumentAllowedRole>
{
    public void Configure(EntityTypeBuilder<ClientDocumentAllowedRole> b)
    {
        b.ToTable("client_document_allowed_roles");
        b.HasKey(x => x.Id);

        b.Property(x => x.RoleName).IsRequired().HasMaxLength(64);

        b.HasIndex(x => x.ClientDocumentId);
        b.HasIndex(x => new { x.ClientDocumentId, x.RoleName })
            .IsUnique()
            .HasDatabaseName("IX_client_document_allowed_roles_DocumentId_RoleName");
    }
}

/// <summary>
/// إعداد المستخدمين المسموح لهم برؤية مستند (CPW-R2). جدول إضافيّ بحتٌ، Cascade من المستند،
/// وبلا FK إلى <c>AspNetUsers</c> اتّساقًا مع نمط المعرّفات المجرّدة في النظام.
/// </summary>
public class ClientDocumentAllowedUserConfiguration : IEntityTypeConfiguration<ClientDocumentAllowedUser>
{
    public void Configure(EntityTypeBuilder<ClientDocumentAllowedUser> b)
    {
        b.ToTable("client_document_allowed_users");
        b.HasKey(x => x.Id);

        b.HasIndex(x => x.ClientDocumentId);
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => new { x.ClientDocumentId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_client_document_allowed_users_DocumentId_UserId");
    }
}

/// <summary>
/// إعداد نسخة ملفّ المستند (CPW-R1B2). جدول إضافيّ بحتٌ.
/// <c>StorageKey</c> مفتاح داخليّ لا يظهر في أيّ DTO أو سجلّ؛ يُفهرَس فريدًا لمنع الازدواج.
/// <c>Sha256</c> مفهرس للكشف السريع عن التكرار داخل المستند نفسه.
/// </summary>
public class ClientDocumentVersionConfiguration : IEntityTypeConfiguration<ClientDocumentVersion>
{
    public void Configure(EntityTypeBuilder<ClientDocumentVersion> b)
    {
        b.ToTable("client_document_versions");
        b.HasKey(x => x.Id);

        b.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(300);
        b.Property(x => x.StorageKey).IsRequired().HasMaxLength(500);
        b.Property(x => x.ContentType).IsRequired().HasMaxLength(200);
        b.Property(x => x.Sha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.ScanStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ScanEngine).IsRequired().HasMaxLength(100);
        b.Property(x => x.ScanDetail).HasMaxLength(1000);
        b.Property(x => x.ChangeNote).HasMaxLength(1000);

        b.HasIndex(x => x.ClientDocumentId);
        b.HasIndex(x => x.StorageKey).IsUnique();
        b.HasIndex(x => new { x.ClientDocumentId, x.Sha256 });

        // رقم نسخة فريد داخل المستند الواحد.
        b.HasIndex(x => new { x.ClientDocumentId, x.VersionNo })
            .IsUnique()
            .HasDatabaseName("IX_client_document_versions_DocumentId_VersionNo");

        // نسخة سارية واحدة كحدّ أقصى لكلّ مستند (فهرس فريد جزئيّ).
        b.HasIndex(x => x.ClientDocumentId)
            .HasFilter("\"IsCurrent\" = true")
            .IsUnique()
            .HasDatabaseName("IX_client_document_versions_DocumentId_Current");
    }
}

/// <summary>
/// إعداد الرابط الخارجيّ للعميل (CPW-R1B2 — «الروابط المهمّة»). جدول إضافيّ بحتٌ.
/// مرجع عنوان فقط بلا أسرار؛ الحذف من جهة العميل <c>Cascade</c> لأنّه لا ملفّات مرتبطة به.
/// </summary>
public class ClientExternalLinkConfiguration : IEntityTypeConfiguration<ClientExternalLink>
{
    public void Configure(EntityTypeBuilder<ClientExternalLink> b)
    {
        b.ToTable("client_external_links");
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Url).IsRequired().HasMaxLength(1000);
        b.Property(x => x.CategoryCode).IsRequired().HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(1000);

        b.HasOne(x => x.Client).WithMany(x => x.ExternalLinks)
            .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ClientId);
        b.HasIndex(x => new { x.ClientId, x.CategoryCode });
    }
}
