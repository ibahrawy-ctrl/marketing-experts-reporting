using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Auth;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// حارس دورية التقارير (UAT Phase 3 — البند 8):
/// • مندوبو المبيعات (SALES_B2C/SALES_B2B) تقاريرهم «يومية» فقط.
/// • بقية الأدوار «أسبوعية». الدورية مفروضة خادميًّا حسب المسمّى الوظيفي للمُرسِل.
/// </summary>
[Collection("Integration")]
public class ReportCadenceTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportCadenceTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب دورية {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    /// <summary>يربط المستخدم بمسمّى وظيفي يحمل الكود المُعطى (عبر الوصول المباشر لقاعدة البيانات).</summary>
    private async Task AssignJobRoleCodeAsync(Guid userId, string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // المسمّى الوظيفي يحمل فهرسًا فريدًا على الكود، وقاعدة بيانات الاختبار مشتركة → ابحث ثم أنشئ.
        var jobRole = await db.JobRoles.FirstOrDefaultAsync(j => j.Code == code);
        if (jobRole is null)
        {
            jobRole = new JobRole { NameAr = $"مسمّى {code}", Code = code };
            db.JobRoles.Add(jobRole);
            await db.SaveChangesAsync();
        }
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRole.Id;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task NonSalesUser_WeeklyAccepted_DailyRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishTemplateAsync(admin);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var weekly = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W50"));
        Assert.Equal(HttpStatusCode.OK, weekly.StatusCode);

        var daily = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Daily, "2026-12-15"));
        Assert.Equal(HttpStatusCode.BadRequest, daily.StatusCode);
    }

    [Fact]
    public async Task SalesUser_DailyAccepted_WeeklyRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishTemplateAsync(admin);
        var (sales, salesId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await AssignJobRoleCodeAsync(salesId, "SALES_B2C");
        // قاعدة الاختبار المشتركة قد تحوي قوالب مرتبطة بمسمّى SALES_B2C من تشغيلات سابقة،
        // فيُسقط حارس الإسناد القالب العام عن المندوب. نُسنِد القالب له صراحةً (Employee Include)
        // كي يبقى اختبار الدورية صالحًا تحت الحارس دون إضعافه.
        await admin.PostAsJsonAsync($"/api/report-templates/{templateId}/assignments",
            new CreateAssignmentRequest(TemplateAssignmentScope.Employee, salesId, TemplateAssignmentKind.Include, null));

        var daily = await sales.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Daily, "2026-12-16"));
        Assert.Equal(HttpStatusCode.OK, daily.StatusCode);

        var weekly = await sales.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, "2026-W51"));
        Assert.Equal(HttpStatusCode.BadRequest, weekly.StatusCode);
    }

    [Fact]
    public async Task Me_ReportsExpectedCadence_DailyForSales_WeeklyForOthers()
    {
        var (sales, salesId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await AssignJobRoleCodeAsync(salesId, "SALES_B2B");
        var (other, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // ملاحظة: الرمز يُصدر عند تسجيل الدخول؛ نعيد تسجيل الدخول بعد ضبط المسمّى ليس مطلوبًا
        // لأن /auth/me يحسب الدورية لحظيًّا من قاعدة البيانات.
        var meSales = await (await sales.GetAsync("/api/auth/me")).ReadAsync<MeResponse>();
        Assert.Equal("Daily", meSales!.ExpectedReportCadence);

        var meOther = await (await other.GetAsync("/api/auth/me")).ReadAsync<MeResponse>();
        Assert.Equal("Weekly", meOther!.ExpectedReportCadence);
    }
}
