using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Org;
using Reporting.Infrastructure.Identity;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("departments");
        b.HasKey(x => x.Id);
        b.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(50);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("\"Code\" IS NOT NULL");
        b.HasMany(x => x.Teams).WithOne(x => x.Department!)
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.ToTable("teams");
        b.HasKey(x => x.Id);
        b.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.HasIndex(x => x.DepartmentId);
    }
}

public class JobRoleConfiguration : IEntityTypeConfiguration<JobRole>
{
    public void Configure(EntityTypeBuilder<JobRole> b)
    {
        b.ToTable("job_roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(50);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("\"Code\" IS NOT NULL");
    }
}

// عضوية الفريق الإضافية (MULTI-TEAM-MEMBERSHIP-MVP-R1) — جدول جديد منفصل تمامًا عن ApplicationUser.TeamId.
public class UserTeamMembershipConfiguration : IEntityTypeConfiguration<UserTeamMembership>
{
    public void Configure(EntityTypeBuilder<UserTeamMembership> b)
    {
        b.ToTable("user_team_memberships");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.TeamId).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.MembershipType).IsRequired().HasMaxLength(40);
        b.Property(x => x.Notes).HasMaxLength(1000);
        // مفتاح خارجي على الفريق (حذف الفريق محروس بطبقة الخدمة؛ Restrict منعًا لحذف عرضيّ).
        b.HasOne(x => x.Team).WithMany()
            .HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        // مفتاح خارجي على المستخدم (AspNetUsers) — حذف المستخدم يحذف عضوياته الإضافية.
        b.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // تفرّد العضوية (مستخدم، فريق) — صفّ واحد لكل ثنائية (نشط أو غير نشط؛ يُعاد تفعيله بدل التكرار).
        b.HasIndex(x => new { x.UserId, x.TeamId }).IsUnique();
        b.HasIndex(x => x.TeamId);
        b.HasIndex(x => x.UserId);
    }
}
