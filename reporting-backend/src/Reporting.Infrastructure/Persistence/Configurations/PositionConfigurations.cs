using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Positions;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> b)
    {
        b.ToTable("positions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasMany(x => x.Permissions).WithOne(x => x.Position!)
            .HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Scopes).WithOne(x => x.Position!)
            .HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Assignments).WithOne(x => x.Position!)
            .HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PositionPermissionConfiguration : IEntityTypeConfiguration<PositionPermission>
{
    public void Configure(EntityTypeBuilder<PositionPermission> b)
    {
        b.ToTable("position_permissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.PermissionKey).IsRequired().HasMaxLength(50);
        b.HasIndex(x => new { x.PositionId, x.PermissionKey }).IsUnique();
    }
}

public class PositionScopeConfiguration : IEntityTypeConfiguration<PositionScope>
{
    public void Configure(EntityTypeBuilder<PositionScope> b)
    {
        b.ToTable("position_scopes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.PositionId);
    }
}

public class UserPositionConfiguration : IEntityTypeConfiguration<UserPosition>
{
    public void Configure(EntityTypeBuilder<UserPosition> b)
    {
        b.ToTable("user_positions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.PositionId }).IsUnique();
        b.HasIndex(x => x.PositionId);
    }
}
