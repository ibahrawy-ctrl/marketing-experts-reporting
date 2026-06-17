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
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.AccountManagerId);
        b.HasMany(x => x.Projects).WithOne(x => x.Client!)
            .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
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
