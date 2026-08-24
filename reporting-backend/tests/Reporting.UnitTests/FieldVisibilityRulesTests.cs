using Reporting.Application.Common;
using Reporting.Application.Security;

namespace Reporting.UnitTests;

/// <summary>
/// P2-SEC-001 — مصفوفة الرؤية على مستوى الحقل/القسم.
/// اختبارات نقيّة بلا قاعدة بيانات: القرار كلّه في <see cref="FieldVisibilityRules"/>.
/// </summary>
public class FieldVisibilityRulesTests
{
    private static readonly Guid Viewer = Guid.NewGuid();
    private static readonly Guid Subject = Guid.NewGuid();

    private static FieldVisibilityContext Ctx(
        SubjectRelation relation,
        string[] roles,
        string[]? permissions = null,
        bool self = false) =>
        new(
            ViewerUserId: Viewer,
            SubjectUserId: self ? Viewer : Subject,
            Roles: roles,
            Relation: relation,
            Permissions: permissions ?? Array.Empty<string>());

    // ===== خارج النطاق: لا شيء إطلاقًا =====

    [Theory]
    [InlineData(FieldSensitivity.PublicOperational)]
    [InlineData(FieldSensitivity.SharedWithEmployee)]
    [InlineData(FieldSensitivity.Internal)]
    [InlineData(FieldSensitivity.HrOnly)]
    [InlineData(FieldSensitivity.ManagementConfidential)]
    [InlineData(FieldSensitivity.FinancialSensitive)]
    [InlineData(FieldSensitivity.MedicalSensitive)]
    public void OutOfScope_Sees_Nothing_Regardless_Of_Role_Or_Permission(FieldSensitivity sensitivity)
    {
        var ctx = Ctx(
            SubjectRelation.None,
            new[] { Roles.Hr, Roles.Manager, Roles.Admin, Roles.Ceo },
            AppPermissions.All);

        Assert.False(FieldVisibilityRules.CanSee(ctx, sensitivity));
    }

    [Fact]
    public void OutOfScope_Sees_No_Section_Not_Even_Identity()
    {
        var ctx = Ctx(SubjectRelation.None, new[] { Roles.Hr }, AppPermissions.All);

        foreach (var section in Enum.GetValues<Employee360Section>())
            Assert.False(FieldVisibilityRules.CanSeeSection(ctx, section));
    }

    // ===== الموظّف على نفسه =====

    [Fact]
    public void Self_Sees_Public_And_SharedWithEmployee()
    {
        var ctx = Ctx(SubjectRelation.Self, new[] { Roles.Employee }, self: true);

        Assert.True(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.PublicOperational));
        Assert.True(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.SharedWithEmployee));
    }

    [Fact]
    public void Self_Never_Sees_Internal_Or_Higher()
    {
        var ctx = Ctx(SubjectRelation.Self, new[] { Roles.Employee }, self: true);

        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.Internal));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.HrOnly));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.ManagementConfidential));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.FinancialSensitive));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.MedicalSensitive));
    }

    // ===== قائد الفريق / المدير =====

    [Fact]
    public void TeamLeader_InTeam_Sees_Operational_And_Internal_Only()
    {
        var ctx = Ctx(SubjectRelation.DirectTeam, new[] { Roles.TeamLeader });

        Assert.True(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.PublicOperational));
        Assert.True(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.SharedWithEmployee));
        Assert.True(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.Internal));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.HrOnly));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.FinancialSensitive));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.MedicalSensitive));
    }

    [Fact]
    public void Manager_InDepartment_Does_Not_See_Financial_Or_Medical_Or_HrOnly_Automatically()
    {
        var ctx = Ctx(SubjectRelation.Department, new[] { Roles.Manager });

        Assert.True(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.Internal));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.HrOnly));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.FinancialSensitive));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.MedicalSensitive));
    }

    // ===== HR =====

    [Fact]
    public void Hr_Without_Explicit_Permission_Does_Not_See_HrOnly()
    {
        var ctx = Ctx(SubjectRelation.Company, new[] { Roles.Hr });

        Assert.True(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.Internal));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.HrOnly));
    }

    [Fact]
    public void Hr_With_Explicit_Permission_Sees_HrOnly_Only()
    {
        var ctx = Ctx(
            SubjectRelation.Company,
            new[] { Roles.Hr },
            new[] { AppPermissions.HrSensitiveRead });

        Assert.True(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.HrOnly));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.FinancialSensitive));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.MedicalSensitive));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.ManagementConfidential));
    }

    // ===== Admin =====

    [Fact]
    public void Admin_Gets_No_Sensitive_Visibility_Automatically()
    {
        var ctx = Ctx(SubjectRelation.Company, new[] { Roles.Admin });

        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.HrOnly));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.ManagementConfidential));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.FinancialSensitive));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.MedicalSensitive));
    }

    [Fact]
    public void Admin_Only_Viewer_Sees_No_Section_Except_Identity()
    {
        var ctx = Ctx(SubjectRelation.Company, new[] { Roles.Admin });

        Assert.True(FieldVisibilityRules.CanSeeSection(ctx, Employee360Section.Identity));
        foreach (var section in Enum.GetValues<Employee360Section>())
        {
            if (section == Employee360Section.Identity) continue;
            Assert.False(FieldVisibilityRules.CanSeeSection(ctx, section));
        }
    }

    // ===== تعدّد الأدوار = اتّحاد المُمنوح صراحةً =====

    [Fact]
    public void MultiRole_Is_Union_Of_Explicit_Grants_Not_Blanket_Opening()
    {
        var ctx = Ctx(
            SubjectRelation.Department,
            new[] { Roles.Admin, Roles.Manager },
            new[] { AppPermissions.HrSensitiveRead });

        // اتّحاد: يرى ما يمنحه الإشراف + ما مُنِح صراحةً بالإذن…
        Assert.True(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.Internal));
        Assert.True(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.HrOnly));
        // …ولا شيء زائد لمجرّد كونه Admin.
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.FinancialSensitive));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.MedicalSensitive));
        Assert.False(FieldVisibilityRules.CanSee(ctx, FieldSensitivity.ManagementConfidential));
    }

    // ===== الأقسام =====

    [Fact]
    public void Executive_Sees_Operational_Sections_But_Not_Hr_Sections()
    {
        var ctx = Ctx(SubjectRelation.Company, new[] { Roles.Ceo });

        Assert.True(FieldVisibilityRules.CanSeeSection(ctx, Employee360Section.OperationalSummary));
        Assert.True(FieldVisibilityRules.CanSeeSection(ctx, Employee360Section.Kpi));
        Assert.False(FieldVisibilityRules.CanSeeSection(ctx, Employee360Section.LeaveAndPermissions));
        Assert.False(FieldVisibilityRules.CanSeeSection(ctx, Employee360Section.RequestsAndBalances));
        Assert.False(FieldVisibilityRules.CanSeeSection(ctx, Employee360Section.Notes));
    }

    [Fact]
    public void Self_Sees_All_Eleven_Sections()
    {
        var ctx = Ctx(SubjectRelation.Self, new[] { Roles.Employee }, self: true);

        foreach (var section in Enum.GetValues<Employee360Section>())
            Assert.True(FieldVisibilityRules.CanSeeSection(ctx, section), section.ToString());
    }

    // ===== التدقيق =====

    [Theory]
    [InlineData(FieldSensitivity.HrOnly, true)]
    [InlineData(FieldSensitivity.ManagementConfidential, true)]
    [InlineData(FieldSensitivity.FinancialSensitive, true)]
    [InlineData(FieldSensitivity.MedicalSensitive, true)]
    [InlineData(FieldSensitivity.Internal, false)]
    [InlineData(FieldSensitivity.SharedWithEmployee, false)]
    [InlineData(FieldSensitivity.PublicOperational, false)]
    public void Auditable_Tiers_Are_The_Sensitive_Ones(FieldSensitivity sensitivity, bool expected)
        => Assert.Equal(expected, FieldVisibilityRules.IsAuditable(sensitivity));

    // ===== تفسير تصنيف الملاحظة التاريخيّ (بلا Backfill) =====

    [Fact]
    public void Legacy_Note_Without_Classification_Is_Read_As_Internal()
        => Assert.Equal(FieldSensitivity.Internal, NoteSensitivity.Effective(null));

    [Fact]
    public void Unknown_Stored_Classification_Falls_To_Strictest_Not_Weakest()
        => Assert.Equal(FieldSensitivity.ManagementConfidential, NoteSensitivity.Effective(999));

    [Fact]
    public void Known_Stored_Classification_Is_Honoured()
        => Assert.Equal(FieldSensitivity.HrOnly, NoteSensitivity.Effective((int)FieldSensitivity.HrOnly));

    [Fact]
    public void Legacy_Internal_Note_Is_Hidden_From_The_Employee_Himself()
    {
        var ctx = Ctx(SubjectRelation.Self, new[] { Roles.Employee }, self: true);
        var effective = NoteSensitivity.Effective(null);

        Assert.False(FieldVisibilityRules.CanSee(ctx, effective));
    }
}
