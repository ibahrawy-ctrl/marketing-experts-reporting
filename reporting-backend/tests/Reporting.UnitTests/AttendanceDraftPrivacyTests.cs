using Reporting.Application.Attendance;
using Reporting.Application.Common;
using Reporting.Application.Security;
using Reporting.Domain.Enums;

namespace Reporting.UnitTests;

/// <summary>
/// DEF-P123-003 (P2) — خصوصيّة المسودّة قبل الإرسال.
///
/// <para>
/// **القاعدة الحاكمة المُثبَتة هنا:** المسودّة ملكُ المُبلِّغ وحده حتّى يقرّر إرسالها.
/// كشفها على الموظّف موضوعها قبل الإرسال يُبطِل معنى <c>Draft</c> ويُبطِل ضمانة السحب/الإلغاء،
/// ويُطلِع الموظّف على اتّهام قد لا يُرسَل أصلًا.
/// </para>
/// <para>
/// الحالتان السابقتان للإرسال هما <c>Draft</c> و<c>Cancelled</c> (والأخيرة لا تُبلَغ إلّا من <c>Draft</c>
/// حسب جدول <see cref="AttendanceTransitions"/>) ⇒ كلتاهما محجوبتان عن الموضوع.
/// من <c>Reported</c> فصاعدًا (وهي هدف <c>Submit</c>) يبدأ حقّ الموظّف في الرؤية.
/// </para>
/// <para>الحجب يُترجَم في نقطة النهاية إلى 404 لا 403 — كي لا يُسرَّب وجود الواقعة.</para>
/// </summary>
public class AttendanceDraftPrivacyTests
{
    private static readonly Guid Subject = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Reporter = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Reviewer = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Stranger = Guid.Parse("44444444-4444-4444-4444-444444444444");

    /// <summary>سياق الموظّف موضوع الواقعة (علاقته بنفسه = Self).</summary>
    private static FieldVisibilityContext SubjectCtx() =>
        new(Subject, Subject, new[] { Roles.Employee }, SubjectRelation.Self,
            Array.Empty<string>(), "attendance.read");

    /// <summary>سياق المُبلِّغ: قائد فريق يرى الموضوع كمرؤوس مباشر.</summary>
    private static FieldVisibilityContext ReporterCtx() =>
        new(Reporter, Subject, new[] { Roles.TeamLeader }, SubjectRelation.DirectTeam,
            Array.Empty<string>(), "attendance.read");

    /// <summary>سياق حامل مفتاح المراجعة الصريح (لا يمنحه دور Hr ولا Admin ضمنًا).</summary>
    private static FieldVisibilityContext ReviewerCtx() =>
        new(Reviewer, Subject, new[] { Roles.Hr }, SubjectRelation.Company,
            new[] { AppPermissions.AttendanceReview }, "attendance.read");

    /// <summary>سياق فاعل خارج النطاق تمامًا وبلا أيّ مفتاح.</summary>
    private static FieldVisibilityContext UnrelatedCtx() =>
        new(Stranger, Subject, new[] { Roles.Employee }, SubjectRelation.None,
            Array.Empty<string>(), "attendance.read");

    // ── 1) الموظّف لا يرى مسودّته ────────────────────────────────────────────

    [Theory]
    [InlineData(AttendanceIncidentStatus.Draft)]
    [InlineData(AttendanceIncidentStatus.Cancelled)]
    public void Subject_CannotView_DraftIncident(AttendanceIncidentStatus preSubmission)
    {
        Assert.False(AttendanceAccess.CanViewIncident(SubjectCtx(), Reporter, preSubmission));
    }

    // ── 2) الموظّف يرى الواقعة فور إرسالها وفي كلّ ما بعدها ──────────────────

    [Fact]
    public void Subject_CanView_SubmittedIncident()
    {
        // Draft --Submit--> Reported : أوّل حالة رسميّة بعد الإرسال.
        Assert.True(AttendanceAccess.CanViewIncident(
            SubjectCtx(), Reporter, AttendanceIncidentStatus.Reported));
    }

    [Fact]
    public void Subject_CanView_EveryStatusAfterSubmission()
    {
        var preSubmission = new[]
        {
            AttendanceIncidentStatus.Draft,
            AttendanceIncidentStatus.Cancelled
        };

        foreach (var status in Enum.GetValues<AttendanceIncidentStatus>().Except(preSubmission))
        {
            Assert.True(
                AttendanceAccess.CanViewIncident(SubjectCtx(), Reporter, status),
                $"الموظّف يجب أن يرى واقعته في الحالة {status}.");
        }
    }

    // ── 3) المُبلِّغ يرى مسودّته دائمًا (وإلّا تعذّر عليه تعديلها/إلغاؤها) ────

    [Fact]
    public void Reporter_CanView_OwnDraftIncident()
    {
        Assert.True(AttendanceAccess.CanViewIncident(
            ReporterCtx(), Reporter, AttendanceIncidentStatus.Draft));
    }

    // ── 4) حامل مفتاح المراجعة الصريح يرى المسودّة عند الحاجة التشغيليّة ────

    [Fact]
    public void ReviewerWithPermission_CanView_DraftIncident()
    {
        Assert.True(AttendanceAccess.CanViewIncident(
            ReviewerCtx(), Reporter, AttendanceIncidentStatus.Draft));
    }

    // ── 5) خارج النطاق ⇒ لا اكتشاف إطلاقًا في أيّ حالة ──────────────────────

    [Fact]
    public void UnrelatedActor_CannotDiscover_Incident()
    {
        foreach (var status in Enum.GetValues<AttendanceIncidentStatus>())
        {
            Assert.False(
                AttendanceAccess.CanViewIncident(UnrelatedCtx(), Reporter, status),
                $"فاعل خارج النطاق يجب ألّا يكتشف الواقعة في الحالة {status}.");
        }
    }

    // ── 6) الحجب لا يتوسّع: مشرف داخل نطاقه يبقى قادرًا على الرؤية ──────────

    [Fact]
    public void SupervisorInScope_StillSees_SubmittedIncident_NoRegression()
    {
        var supervisor = new FieldVisibilityContext(
            Reviewer, Subject, new[] { Roles.Manager }, SubjectRelation.Department,
            Array.Empty<string>(), "attendance.read");

        Assert.True(AttendanceAccess.CanViewIncident(
            supervisor, Reporter, AttendanceIncidentStatus.AwaitingEmployee));
    }
}
