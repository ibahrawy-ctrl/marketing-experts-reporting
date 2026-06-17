using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Infrastructure.Identity;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(x => x.Id);
        b.Ignore(x => x.IsActive);
        b.Property(x => x.Token).IsRequired().HasMaxLength(200);
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => x.UserId);
    }
}
