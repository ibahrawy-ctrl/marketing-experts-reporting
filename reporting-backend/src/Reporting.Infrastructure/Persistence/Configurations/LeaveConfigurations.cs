using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Leave;

namespace Reporting.Infrastructure.Persistence.Configurations;

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> b)
    {
        b.ToTable("leave_requests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.CurrentStep).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Reason).IsRequired().HasMaxLength(1000);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.RejectionReason).HasMaxLength(1000);
        b.Property(x => x.ReturnReason).HasMaxLength(1000);
        b.HasIndex(x => x.RequesterUserId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.RequesterUserId, x.StartDate, x.EndDate });
    }
}

public class LeaveRequestEventConfiguration : IEntityTypeConfiguration<LeaveRequestEvent>
{
    public void Configure(EntityTypeBuilder<LeaveRequestEvent> b)
    {
        b.ToTable("leave_request_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired().HasMaxLength(50);
        b.Property(x => x.Step).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.HasIndex(x => x.LeaveRequestId);
    }
}
