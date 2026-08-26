using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Auth;
using Reporting.Application.Common;
using Reporting.Application.Security;
using Reporting.Infrastructure.Identity;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// P3-SEC-005 (الشقّ الخادميّ) — **انعكاس القدرات** الذي تبني عليه الملاحة شرطَ الظهور.
///
/// <para>الواجهة صارت تُخفي ما لا يملكه المستخدم بدل أن تعرض بابًا يردّ عليه الخادم 403،
/// ومصدر ذلك القرار حقلان جديدان في <c>/auth/me</c> و<c>/auth/login</c>: <c>permissions</c>
/// و<c>scopeType</c>. الخطر المُقابِل لهذا التيسير واضح: لو انعكس على المستخدم مفتاحٌ
/// **لا يملكه** لظهر له سطح مُقفَل؛ ولو انعكست عليه مفاتيح **غيره** لصار الحقل قناة تسريب
/// عن حسابات أخرى؛ ولو أخذ الخادم النطاق من الطلب بدل أن يحسبه لصار الانعكاس تخويلًا.</para>
///
/// <para><b>الثابت المفروض هنا:</b> الانعكاس **مرآة دقيقة لمطالبات المتّصل نفسه**، لا يزيد
/// ولا ينقص ولا يمنح. والقياس على السلوك لا على الوصف: كلّ مفتاح مُعلَن يُقابَل بفتح المسار
/// الذي يحرسه فعلًا، وكلّ مفتاح غائب يُقابَل برفض ذلك المسار.</para>
/// </summary>
[Collection("Phase2")]
public class Phase3CapabilityReflectionTests(Phase2WebApplicationFactory factory)
{
    /// <summary>
    /// إنشاء مستخدم مع **الاحتفاظ ببيانات دخوله**: هذه المجموعة تفحص استجابة تسجيل الدخول
    /// نفسها لا سطحًا محميًّا بها، فلا يكفي عميل جاهز الترويسة كما في <c>Phase2TestAuth</c>.
    /// </summary>
    private async Task<(HttpClient Client, string Email, string Password)> CreateAsync(
        string role, params string[] permissions)
    {
        var email = $"p3-{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        const string password = "Passw0rd#1";

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = $"مستخدم مرحلة 3 {role}",
                IsActive = true
            };
            await users.CreateAsync(user, password);
            var assigned = await users.AddToRoleAsync(user, role);
            if (!assigned.Succeeded)
                throw new InvalidOperationException(
                    $"تعذّر إسناد الدور '{role}': {string.Join("; ", assigned.Errors.Select(e => e.Description))}");
            foreach (var permission in permissions.Distinct())
                await users.AddClaimAsync(user, new Claim(AppPermissions.ClaimType, permission));
        }

        var client = factory.CreateClient();
        var auth = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password)))
            .Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, email, password);
    }

    private static async Task<MeResponse> MeAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<MeResponse>())!;
    }

    // ══════════════════════ ① المرآة تعكس صاحبها وحده ══════════════════════

    /// <summary>
    /// مستخدمان بمفاتيح مختلفة: كلٌّ يرى مفاتيحه بالضبط. المساواة هنا **مجموعيّة تامّة**
    /// لا «يحتوي»: الاحتواء وحده كان سيمرّ لو سرّب الحقل مفاتيح غيره فوق مفاتيحه.
    /// </summary>
    [Fact]
    public async Task Me_Reflects_Exactly_The_Callers_Own_Keys()
    {
        var (a, _, _) = await CreateAsync(Roles.Hr, AppPermissions.HrOperationsView, AppPermissions.HrOperationsExport);
        var (b, _, _) = await CreateAsync(Roles.Hr, AppPermissions.AttendanceReview);

        var meA = await MeAsync(a);
        var meB = await MeAsync(b);

        Assert.Equal(
            new[] { AppPermissions.HrOperationsExport, AppPermissions.HrOperationsView },
            meA.Permissions!.OrderBy(p => p, StringComparer.Ordinal));
        Assert.Equal(
            new[] { AppPermissions.AttendanceReview },
            meB.Permissions!.OrderBy(p => p, StringComparer.Ordinal));
    }

    /// <summary>
    /// بلا مفاتيح ⇒ قائمة **فارغة حاضرة** لا حقل غائب. الفرق ليس شكليًّا: الواجهة تبني
    /// <c>Set</c> من الحقل، والغياب يدفعها إلى احتياطيّ قد يُفسَّر «لا قيد» بدل «لا قدرة».
    /// </summary>
    [Fact]
    public async Task A_User_Without_Keys_Gets_A_Present_Empty_List()
    {
        var (client, _, _) = await CreateAsync(Roles.Employee);

        var raw = await (await client.GetAsync("/api/auth/me")).Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(raw).RootElement;
        Assert.True(root.TryGetProperty("permissions", out var permissions));
        Assert.Equal(JsonValueKind.Array, permissions.ValueKind);
        Assert.Empty(permissions.EnumerateArray());
    }

    /// <summary>
    /// <c>/auth/login</c> و<c>/auth/me</c> مصدران لنفس الواجهة (الأوّل عند الدخول والثاني عند
    /// إعادة التحميل). اختلافهما كان سيُنتج قائمة تتبدّل بلا سبب بين إقلاعٍ وآخر.
    /// </summary>
    [Fact]
    public async Task Login_And_Me_Report_The_Same_Capabilities_And_Scope()
    {
        var (client, email, password) = await CreateAsync(
            Roles.Hr, AppPermissions.HrOperationsView, AppPermissions.HrSensitiveRead);

        var fresh = factory.CreateClient();
        var login = await (await fresh.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password)))
            .Content.ReadFromJsonAsync<AuthResponse>();
        var me = await MeAsync(client);

        Assert.Equal(
            me.Permissions!.OrderBy(p => p, StringComparer.Ordinal),
            login!.Permissions!.OrderBy(p => p, StringComparer.Ordinal));
        Assert.Equal(me.ScopeType, login.ScopeType);
    }

    // ══════════════════════ ② المرآة لا تمنح ══════════════════════

    /// <summary>
    /// المعيار الحاسم: المفتاح المُعلَن يفتح مساره فعلًا، والمفتاح الغائب يُرفَض مساره فعلًا.
    /// أي انحراف في أيّ اتّجاه عطب: الأوّل يعرض سطحًا مُقفَلًا، والثاني يُخفي سطحًا مملوكًا.
    /// </summary>
    [Fact]
    public async Task Every_Reflected_Key_Matches_What_The_Guard_Actually_Admits()
    {
        var (viewer, _, _) = await CreateAsync(Roles.Hr, AppPermissions.HrOperationsView);
        var (bare, _, _) = await CreateAsync(Roles.Hr);

        var meViewer = await MeAsync(viewer);
        Assert.Contains(AppPermissions.HrOperationsView, meViewer.Permissions!);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/hr-operations/dashboard")).StatusCode);

        // التصدير مفتاح مستقلّ: مُعلَن أنّه غائب، ومرفوض فعلًا. الرؤية لا تجرّه.
        Assert.DoesNotContain(AppPermissions.HrOperationsExport, meViewer.Permissions!);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.GetAsync("/api/hr-operations/queues/ReportsMissing/export")).StatusCode);

        var meBare = await MeAsync(bare);
        Assert.DoesNotContain(AppPermissions.HrOperationsView, meBare.Permissions!);
        Assert.Equal(HttpStatusCode.Forbidden, (await bare.GetAsync("/api/hr-operations/dashboard")).StatusCode);
    }

    /// <summary>
    /// أوسع دور في النظام بلا مفاتيح: الانعكاس يُعلن نطاقًا واسعًا (<c>governance</c>) ومع ذلك
    /// **قائمة مفاتيح فارغة** ومسار محروس مرفوض. النطاق موضع رؤية لا رخصة قدرة.
    /// </summary>
    [Fact]
    public async Task A_Wide_Scope_Does_Not_Imply_Any_Capability_Key()
    {
        var (admin, _, _) = await CreateAsync(Roles.Admin);

        var me = await MeAsync(admin);
        Assert.Equal("governance", me.ScopeType);
        Assert.Empty(me.Permissions!);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/hr-operations/dashboard")).StatusCode);
    }

    // ══════════════════════ ③ النطاق يحسبه الخادم ══════════════════════

    /// <summary>
    /// النطاق مشتقّ من الدور خادميًّا بقيم مغلقة معروفة — لا يُرسَل من الواجهة ولا يُخمَّن فيها.
    /// القيم مكتوبة صراحةً لا مستنسخة من <c>RoleAccess</c>، وإلّا لصار الفحص يقيس نفسه.
    /// </summary>
    [Theory]
    [InlineData(Roles.Employee, "own")]
    [InlineData(Roles.TeamLeader, "team")]
    [InlineData(Roles.Manager, "department")]
    [InlineData(Roles.Ceo, "company")]
    [InlineData(Roles.Admin, "governance")]
    public async Task ScopeType_Is_Derived_From_The_Role_On_The_Server(string role, string expected)
    {
        var (client, _, _) = await CreateAsync(role);
        Assert.Equal(expected, (await MeAsync(client)).ScopeType);
    }

    /// <summary>
    /// محاولة إملاء النطاق من العميل (استعلام/ترويسة) لا تغيّر شيئًا: القيمة تبقى المحسوبة.
    /// بغير هذا الفحص كان الحقل يبدو سليمًا وهو قابل للتزوير من الطلب.
    /// </summary>
    [Fact]
    public async Task A_Client_Supplied_Scope_Is_Ignored()
    {
        var (client, _, _) = await CreateAsync(Roles.Employee);

        client.DefaultRequestHeaders.Add("X-Scope-Type", "governance");
        var res = await client.GetAsync("/api/auth/me?scopeType=governance");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("own", (await res.Content.ReadFromJsonAsync<MeResponse>())!.ScopeType);
    }
}
