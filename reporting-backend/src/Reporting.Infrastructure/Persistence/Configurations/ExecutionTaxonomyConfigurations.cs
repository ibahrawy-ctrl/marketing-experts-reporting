using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.ExecutionTaxonomy;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class ExecutionTaxonomyValueConfiguration : IEntityTypeConfiguration<ExecutionTaxonomyValue>
{
    public void Configure(EntityTypeBuilder<ExecutionTaxonomyValue> b)
    {
        b.ToTable("execution_taxonomy_values");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(100);
        b.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Domain).IsRequired().HasMaxLength(100);
        // فريد ضمن نفس الـ Domain: نفس الرمز لا يتكرّر داخل بُعد واحد (يجوز تكراره عبر أبعاد مختلفة).
        b.HasIndex(x => new { x.Domain, x.Code }).IsUnique();
    }
}
