using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Reporting.Application.Auth;
using Reporting.Application.Security;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// مصنع بالأعلام **مطفأة** — الصورة المرآتيّة لـ<see cref="Phase2WebApplicationFactory"/>.
///
/// <para>
/// لا غنى عنه: كلّ اختبارات المرحلة الثانية تعمل تحت أعلام مرفوعة، فحال «الميزة مغلقة» —
/// وهي حال الإنتاج الفعليّة اليوم — لم تكن مقيسة إطلاقًا. وهذا بالضبط ما كسر تجربة المستخدم:
/// مسار يردّ 404 بنيّة الإخفاء تقرؤه الواجهة «خطأ عامّ». نُثبت هنا أنّ الردّ **404 لا 500**،
/// وأنّ عقد المستخدم يعلن غياب الميزة صراحةً فتستطيع الواجهة التمييز قبل إرسال الطلب أصلًا.
/// </para>
/// </summary>
public sealed class FeaturesDisabledWebApplicationFactory : Phase2WebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // آخر كتابة تفوز ⇒ تُلغى الأعلام التي رفعها الأساس، وتبقى بقيّة الإعدادات (القاعدة، JWT) كما هي.
        builder.UseSetting("Phase2:Employee360Enabled", "false");
        builder.UseSetting("Phase2:AttendanceEnabled", "false");
        builder.UseSetting("Phase2:HrOperationsEnabled", "false");
        builder.UseSetting("Phase2:EmployeeChecklistEnabled", "false");
    }
}

/// <summary>
/// P123-R1 — سطح القدرات: مصدر واحد موثوق لِما يستطيع المستخدم بلوغه فعلًا.
///
/// <para>
/// البُعدان **مستقلّان تمامًا** ولا يُغني أحدهما عن الآخر:
/// <list type="bullet">
/// <item><c>Features</c> — هل السطح مفتوح في هذه البيئة أصلًا؟ (إعداد الخادم)</item>
/// <item><c>Permissions</c> — هل يملكه هذا المستخدم؟ (مطالبات <c>perm</c>)</item>
/// </list>
/// إعلان الميزة **لا يمنح شيئًا**؛ الخادم يبقى المُنفِّذ الوحيد للتخويل. تُقاس هنا الحالتان معًا
/// لأنّ اختبار إحداهما وحدها كان سيسمح بواجهة تعرض ما يردّه الخادم، أو تُخفي ما يسمح به.
/// </para>
/// </summary>
[Collection("Phase2")]
public class PlatformCapabilitiesApiTests : IClassFixture<FeaturesDisabledWebApplicationFactory>
{
    private readonly Phase2WebApplicationFactory _on;
    private readonly FeaturesDisabledWebApplicationFactory _off;

    public PlatformCapabilitiesApiTests(Phase2WebApplicationFactory on, FeaturesDisabledWebApplicationFactory off)
    {
        _on = on;
        _off = off;
    }

    private static async Task<MeResponse> MeAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<MeResponse>())!;
    }

    // ═══════════════ الأعلام مرفوعة ═══════════════

    [Fact]
    public async Task Me_Publishes_Every_Feature_Enabled_By_Server_Configuration()
    {
        var (client, _) = await Phase2TestAuth.CreateUserAsync(_on, "Employee");

        var me = await MeAsync(client);

        // القائمة مشتقّة من الإعداد لا مكتوبة يدويًّا ⇒ إسقاط علم في الخادم يُسقِط المفتاح هنا فورًا.
        Assert.NotNull(me.Features);
        Assert.Equal(AppFeatures.All.OrderBy(k => k), me.Features!.OrderBy(k => k));
    }

    [Fact]
    public async Task Login_And_Me_Publish_The_Same_Feature_Set()
    {
        // مدخلان للتطبيق على **نفس** المستخدم: استجابة تسجيل الدخول، ثمّ الإقلاع بجلسة قائمة.
        // تباعدهما كان سينتج قائمة ملاحة تتغيّر بمجرّد تحديث الصفحة — عطل يصعب تفسيره لأنّ كليهما «يعمل».
        var client = _on.CreateClient();
        var loginRes = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("admin@marketingexperts.local", "Admin#12345"));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var auth = (await loginRes.Content.ReadFromJsonAsync<AuthResponse>())!;

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var me = await MeAsync(client);

        Assert.NotNull(auth.Features);
        Assert.Equal(me.Features!.OrderBy(k => k), auth.Features!.OrderBy(k => k));
    }

    [Fact]
    public async Task Feature_Availability_Is_Not_Authorization()
    {
        // الميزة مفتوحة للجميع في هذه البيئة، والصلاحيّة ليست كذلك. لو خُلط البُعدان لصار
        // رفع العَلَم فتحًا للسطح لكلّ مستخدم — وهو أخطر ما يمكن أن ينتج عن «سطح قدرات».
        var (client, _) = await Phase2TestAuth.CreateUserAsync(_on, "Hr");

        var me = await MeAsync(client);
        Assert.Contains(AppFeatures.HrOperations, me.Features!);
        Assert.DoesNotContain(AppPermissions.HrOperationsView, me.Permissions ?? Array.Empty<string>());

        var res = await client.GetAsync("/api/hr-operations/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Permissions_Are_Per_User_While_Features_Are_Per_Environment()
    {
        var (granted, _) = await Phase2TestAuth.CreateUserAsync(
            _on, "Hr", null, null, null, AppPermissions.HrOperationsView);
        var (plain, _) = await Phase2TestAuth.CreateUserAsync(_on, "Hr");

        var grantedMe = await MeAsync(granted);
        var plainMe = await MeAsync(plain);

        // الصلاحيّة تفترق بين المستخدمَين…
        Assert.Contains(AppPermissions.HrOperationsView, grantedMe.Permissions!);
        Assert.DoesNotContain(AppPermissions.HrOperationsView, plainMe.Permissions ?? Array.Empty<string>());
        // …والميزة لا تفترق: هي خاصّة بالبيئة لا بالشخص.
        Assert.Equal(grantedMe.Features!.OrderBy(k => k), plainMe.Features!.OrderBy(k => k));

        Assert.Equal(HttpStatusCode.OK, (await granted.GetAsync("/api/hr-operations/dashboard")).StatusCode);
    }

    // ═══════════════ الأعلام مطفأة (حال الإنتاج اليوم) ═══════════════

    [Fact]
    public async Task Disabled_Environment_Publishes_No_Features()
    {
        var (client, _) = await Phase2TestAuth.CreateUserAsync(_off, "Admin");

        var me = await MeAsync(client);

        // حتّى Admin: العَلَم إخفاء ميزة لا تفويض، فلا دور يفتح سطحًا مغلقًا بالإعداد.
        Assert.NotNull(me.Features);
        Assert.Empty(me.Features!);
    }

    [Theory]
    [InlineData("/api/employees/me/profile-360")]
    [InlineData("/api/attendance")]
    [InlineData("/api/attendance/types")]
    [InlineData("/api/hr-operations/dashboard")]
    public async Task Disabled_Feature_Yields_NotFound_Never_A_Server_Error(string path)
    {
        // جوهر بوّابة R1: «الميزة مغلقة» ليست عطلًا. أيّ 5xx هنا كان سيصل إلى المستخدم
        // رسالةَ «خطأ مؤقّت، أعد المحاولة» على حالة **دائمة** — وهو ما تمنعه DEC-05 نصًّا.
        var (client, _) = await Phase2TestAuth.CreateUserAsync(
            _off, "Hr", null, null, null, AppPermissions.HrOperationsView);

        var res = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.True((int)res.StatusCode < 500, $"{path} ردّ {(int)res.StatusCode} — الميزة المغلقة لا تُبلَّغ كعطل");
    }

    [Fact]
    public async Task Disabled_Feature_Is_Closed_Even_With_The_Permission_Held()
    {
        // الترتيب مقصود: العَلَم **سابق** على الصلاحيّة. لو كانت الصلاحيّة تتجاوز العَلَم لصار
        // إطفاء الميزة وعدًا كاذبًا، ولانفتح على الإنتاج سطحٌ يُظنّ مغلقًا.
        var (client, _) = await Phase2TestAuth.CreateUserAsync(
            _off, "Hr", null, null, null, AppPermissions.HrOperationsView);

        var me = await MeAsync(client);
        Assert.Contains(AppPermissions.HrOperationsView, me.Permissions!);
        Assert.Empty(me.Features!);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/hr-operations/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Capabilities_Require_An_Authenticated_Session()
    {
        // سطح القدرات يكشف تشكيلة البيئة ⇒ لا يُقرأ بلا جلسة.
        var res = await _on.CreateClient().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
