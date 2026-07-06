using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Courses;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("courses");
        b.HasKey(x => x.Id);
        b.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.HasIndex(x => x.NameAr).IsUnique();
    }
}
