using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Clients;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("clients");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.MainContactName).HasMaxLength(200);
        b.Property(x => x.MainContactInfo).HasMaxLength(300);
        b.Property(x => x.Notes).HasMaxLength(2000);

        // Client 360 Foundation (CPW-R1B) — حقول موسَّعة اختيارية
        b.Property(x => x.TradeNameEn).HasMaxLength(200);
        b.Property(x => x.LegalName).HasMaxLength(300);
        b.Property(x => x.ClientTypeCode).HasMaxLength(100);
        b.Property(x => x.SectorCode).HasMaxLength(100);
        b.Property(x => x.Country).HasMaxLength(100);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Website).HasMaxLength(500);
        b.Property(x => x.SourceCode).HasMaxLength(100);

        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.AccountManagerId);
        b.HasMany(x => x.Projects).WithOne(x => x.Client!)
            .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Contacts).WithOne(x => x.Client!)
            .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.DigitalChannels).WithOne(x => x.Client!)
            .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.BrandProfile).WithOne(x => x.Client!)
            .HasForeignKey<ClientBrandProfile>(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// إعداد جهة اتصال العميل (Client 360 — CPW-R1B). جدول إضافيّ بحتٌ. FK على ClientId (Cascade).
/// فهرس فريد جزئيّ يفرض جهة اتصال أساسية واحدة نشطة كحدّ أقصى لكلّ عميل.
/// </summary>
public class ClientContactConfiguration : IEntityTypeConfiguration<ClientContact>
{
    public void Configure(EntityTypeBuilder<ClientContact> b)
    {
        b.ToTable("client_contacts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.JobTitle).HasMaxLength(200);
        b.Property(x => x.Department).HasMaxLength(200);
        b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.WhatsApp).HasMaxLength(50);
        b.Property(x => x.PreferredContactMethodCode).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(2000);

        b.HasIndex(x => x.ClientId);

        // جهة اتصال أساسية واحدة نشطة كحدّ أقصى لكلّ عميل (فهرس فريد جزئيّ).
        b.HasIndex(x => x.ClientId)
            .HasFilter("\"IsPrimary\" = true AND \"IsActive\" = true")
            .IsUnique()
            .HasDatabaseName("IX_client_contacts_ClientId_ActivePrimary");
    }
}

/// <summary>
/// إعداد قناة العميل الرقمية (Client 360 — CPW-R1B). جدول إضافيّ بحتٌ. FK على ClientId (Cascade).
/// معرّفات مرجعية فقط — لا أسرار.
/// </summary>
public class ClientDigitalChannelConfiguration : IEntityTypeConfiguration<ClientDigitalChannel>
{
    public void Configure(EntityTypeBuilder<ClientDigitalChannel> b)
    {
        b.ToTable("client_digital_channels");
        b.HasKey(x => x.Id);
        b.Property(x => x.PlatformCode).IsRequired().HasMaxLength(100);
        b.Property(x => x.DisplayName).HasMaxLength(200);
        b.Property(x => x.UsernameOrHandle).HasMaxLength(200);
        b.Property(x => x.ProfileUrl).HasMaxLength(500);
        b.Property(x => x.AccessStatusCode).HasMaxLength(100);
        b.Property(x => x.BusinessManagerId).HasMaxLength(100);
        b.Property(x => x.AdAccountId).HasMaxLength(100);
        b.Property(x => x.PixelId).HasMaxLength(100);
        b.Property(x => x.Ga4PropertyId).HasMaxLength(100);
        b.Property(x => x.GtmContainerId).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(2000);

        b.HasIndex(x => x.ClientId);
    }
}

/// <summary>
/// إعداد ملفّ براند العميل (Client 360 — CPW-R1B). علاقة 1:0..1 — المفتاح الأساسيّ = ClientId.
/// جدول إضافيّ بحتٌ. FK على ClientId (Cascade).
/// </summary>
public class ClientBrandProfileConfiguration : IEntityTypeConfiguration<ClientBrandProfile>
{
    public void Configure(EntityTypeBuilder<ClientBrandProfile> b)
    {
        b.ToTable("client_brand_profiles");
        b.HasKey(x => x.ClientId);
        b.Property(x => x.BusinessOverview).HasMaxLength(4000);
        b.Property(x => x.ProductsAndServices).HasMaxLength(4000);
        b.Property(x => x.TargetAudience).HasMaxLength(4000);
        b.Property(x => x.TargetLocations).HasMaxLength(2000);
        b.Property(x => x.UniqueSellingProposition).HasMaxLength(2000);
        b.Property(x => x.Strengths).HasMaxLength(2000);
        b.Property(x => x.Competitors).HasMaxLength(2000);
        b.Property(x => x.ToneOfVoice).HasMaxLength(1000);
        b.Property(x => x.PreferredMessages).HasMaxLength(4000);
        b.Property(x => x.ProhibitedMessages).HasMaxLength(4000);
        b.Property(x => x.BrandGuidelinesUrl).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(2000);
    }
}

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.ServiceType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasIndex(x => x.ClientId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.OwnerTeamId);
        b.HasIndex(x => x.AccountManagerId);
    }
}
