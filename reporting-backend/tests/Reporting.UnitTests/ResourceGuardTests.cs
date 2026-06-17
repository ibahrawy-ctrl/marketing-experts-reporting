using Reporting.Application.Common;
using Xunit;

namespace Reporting.UnitTests;

public class ResourceGuardTests
{
    private sealed class FakeUser : ICurrentUser
    {
        public Guid? UserId { get; init; }
        public bool IsAuthenticated { get; init; } = true;
        public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
        public bool IsInRole(string role) => Roles.Contains(role);
        public bool IsInAnyRole(params string[] roles) => roles.Any(IsInRole);
    }

    [Fact]
    public void Owner_CanAccess_OwnResource()
    {
        var id = Guid.NewGuid();
        var user = new FakeUser { UserId = id };
        Assert.True(ResourceGuard.CanAccess(user, id));
    }

    [Fact]
    public void NonOwner_WithoutElevation_IsForbidden()
    {
        var user = new FakeUser { UserId = Guid.NewGuid(), Roles = new[] { Roles.Employee } };
        var result = ResourceGuard.EnsureOwnerOrElevated(user, Guid.NewGuid());
        Assert.False(result.Succeeded);
        Assert.Equal("auth.forbidden", result.ErrorCode);
    }

    [Fact]
    public void Admin_CanAccess_AnyResource()
    {
        var user = new FakeUser { UserId = Guid.NewGuid(), Roles = new[] { Roles.Admin } };
        Assert.True(ResourceGuard.CanAccess(user, Guid.NewGuid()));
    }

    [Fact]
    public void ElevatedRole_CanAccess_WhenAllowed()
    {
        var user = new FakeUser { UserId = Guid.NewGuid(), Roles = new[] { Roles.Manager } };
        Assert.True(ResourceGuard.CanAccess(user, Guid.NewGuid(), Roles.Manager, Roles.TeamLeader));
    }

    [Fact]
    public void Unauthenticated_IsRejected()
    {
        var user = new FakeUser { UserId = null, IsAuthenticated = false };
        var result = ResourceGuard.EnsureOwnerOrElevated(user, Guid.NewGuid());
        Assert.False(result.Succeeded);
        Assert.Equal("auth.unauthenticated", result.ErrorCode);
    }
}
