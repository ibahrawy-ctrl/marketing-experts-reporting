using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.EmployeeServices;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class EmployeeBalanceLedgerConfiguration : IEntityTypeConfiguration<EmployeeBalanceLedger>
{
    public void Configure(EntityTypeBuilder<EmployeeBalanceLedger> b)
    {
        b.ToTable("employee_balance_ledger");
        b.HasKey(x => x.Id);
        b.Property(x => x.BalanceType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.EmployeeId, x.BalanceType, x.Year });
        // فهرس فريد جزئي لمنع الازدواج الآلي على مستوى القاعدة.
        b.HasIndex(x => new { x.RelatedRequestId, x.Source })
            .IsUnique()
            .HasFilter("\"RelatedRequestId\" IS NOT NULL");
    }
}

public class BalancePolicyConfiguration : IEntityTypeConfiguration<BalancePolicy>
{
    public void Configure(EntityTypeBuilder<BalancePolicy> b)
    {
        b.ToTable("balance_policies");
        b.HasKey(x => x.Id);
        b.Property(x => x.PermissionUnit).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.AnnualLeaveDefaultDays).HasPrecision(18, 2);
        b.Property(x => x.PermissionMonthlyLimit).HasPrecision(18, 2);
        b.Property(x => x.PermissionAnnualLimit).HasPrecision(18, 2);
        b.HasIndex(x => new { x.Year, x.JobRoleId }).IsUnique();
    }
}

public class EmployeeServiceRequestConfiguration : IEntityTypeConfiguration<EmployeeServiceRequest>
{
    public void Configure(EntityTypeBuilder<EmployeeServiceRequest> b)
    {
        b.ToTable("employee_service_requests");
        b.HasKey(x => x.Id);
        b.Property(x => x.RequestType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.PreferredLanguage).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.DestinationEntity).HasMaxLength(200);
        b.Property(x => x.AttachmentPath).HasMaxLength(1000);
        b.Property(x => x.HrComment).HasMaxLength(2000);
        b.Property(x => x.HrAttachmentPath).HasMaxLength(1000);
        b.Property(x => x.HrAttachmentOriginalFileName).HasMaxLength(260);
        b.Property(x => x.HrAttachmentContentType).HasMaxLength(100);
        b.Property(x => x.RejectionReason).HasMaxLength(1000);
        b.HasIndex(x => x.RequesterUserId);
        b.HasIndex(x => x.Status);
    }
}

public class EmployeeServiceRequestEventConfiguration : IEntityTypeConfiguration<EmployeeServiceRequestEvent>
{
    public void Configure(EntityTypeBuilder<EmployeeServiceRequestEvent> b)
    {
        b.ToTable("employee_service_request_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired().HasMaxLength(50);
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.HasIndex(x => x.EmployeeServiceRequestId);
    }
}
