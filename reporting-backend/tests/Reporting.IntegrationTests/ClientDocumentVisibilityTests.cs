using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Reporting.Application.Clients;
using Reporting.Application.Documents;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// صلاحيّات المستندات + رؤية مدير العميل (CPW-R2).
/// <para>
/// يثبت: الترتيب المُلزِم «صلاحيّة العميل أوّلًا ثمّ سياسة المستند»، الفلترة الخادميّة في القائمة،
/// أنّ المنع يعني «غير موجود» (404 <c>client_document.not_found</c>) في كلّ المسارات المباشرة،
/// فصل <c>ManagementAndFinance</c> عن <c>FinanceOnly</c>، السياسات المخصّصة (أدوار/أشخاص)،
/// التوافق الخلفيّ لـ<c>ClientScoped</c>، وقراءة مدير العميل لملفّ عميله الشامل بلا صلاحيّة تعديل أساسيّة.
/// </para>
/// </summary>
[Collection("Integration")]
public class ClientDocumentVisibilityTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ClientDocumentVisibilityTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static readonly byte[] PdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n%%EOF\n");

    // ===== 1) الأدمن يرى كلّ المستندات المسموح بها =====
    [Fact]
    public async Task Admin_Sees_All_Documents_Regardless_Of_Policy()
    {
        var env = await BuildEnvAsync();

        await UploadAsync(env.Admin, env.ClientId, "مستند مالي", "Invoice");                  // ManagementAndFinance
        await UploadAsync(env.Admin, env.ClientId, "اتفاقية سرّية", "NDA");                    // ManagementOnly
        await UploadAsync(env.Admin, env.ClientId, "عرض فنّي", "TechnicalProposal");           // ProjectTeam
        await UploadAsync(env.Admin, env.ClientId, "تقرير عام", "Report");                     // ClientScoped
        await UploadAsync(env.Admin, env.ClientId, "مالية فقط", "Other", DocumentVisibilityType.FinanceOnly);

        var titles = await TitlesAsync(env.Admin, env.ClientId);
        Assert.Equal(5, titles.Count);
        Assert.Contains("مستند مالي", titles);
        Assert.Contains("اتفاقية سرّية", titles);
        Assert.Contains("عرض فنّي", titles);
        Assert.Contains("تقرير عام", titles);
        Assert.Contains("مالية فقط", titles);
    }

    // ===== 2) المالية ترى FinanceOnly =====
    [Fact]
    public async Task Finance_Sees_FinanceOnly_Document()
    {
        var env = await BuildEnvAsync();
        await UploadAsync(env.Admin, env.ClientId, "كشف حساب", "Other", DocumentVisibilityType.FinanceOnly);

        var titles = await TitlesAsync(env.Finance, env.ClientId);
        Assert.Contains("كشف حساب", titles);
    }

    // ===== 3) مدير العميل المُسنَد لا يرى FinanceOnly =====
    [Fact]
    public async Task AssignedAccountManager_DoesNotSee_FinanceOnly_Document()
    {
        var env = await BuildEnvAsync();
        var doc = await UploadAsync(env.Admin, env.ClientId, "كشف حساب سرّي", "Other", DocumentVisibilityType.FinanceOnly);

        var titles = await TitlesAsync(env.AccountManager, env.ClientId);
        Assert.DoesNotContain("كشف حساب سرّي", titles);

        var res = await env.AccountManager.GetAsync($"/api/clients/{env.ClientId}/documents/{doc.Document.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("client_document.not_found", await ProblemTypeAsync(res));
    }

    // ===== 4) مدير العميل يرى العرض الفنّي (ProjectTeam) =====
    [Fact]
    public async Task AccountManager_Sees_TechnicalProposal()
    {
        var env = await BuildEnvAsync();
        var doc = await UploadAsync(env.Admin, env.ClientId, "العرض الفنّي", "TechnicalProposal");
        Assert.Equal(DocumentVisibilityType.ProjectTeam, doc.Document.VisibilityType);

        Assert.Contains("العرض الفنّي", await TitlesAsync(env.AccountManager, env.ClientId));
        var res = await env.AccountManager.GetAsync($"/api/clients/{env.ClientId}/documents/{doc.Document.Id}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 5) مدير العميل يرى الخطّة التسويقيّة (ProjectTeam) =====
    [Fact]
    public async Task AccountManager_Sees_MarketingPlan()
    {
        var env = await BuildEnvAsync();
        var doc = await UploadAsync(env.Admin, env.ClientId, "الخطّة التسويقيّة", "MarketingPlan");
        Assert.Equal(DocumentVisibilityType.ProjectTeam, doc.Document.VisibilityType);

        Assert.Contains("الخطّة التسويقيّة", await TitlesAsync(env.AccountManager, env.ClientId));
    }

    // ===== 6) مدير عميل آخر (غير مُسنَد لهذا العميل) ⇒ 404 =====
    [Fact]
    public async Task UnassignedAccountManager_Gets_404_On_Client_Documents()
    {
        var env = await BuildEnvAsync();
        await UploadAsync(env.Admin, env.ClientId, "عرض فنّي", "TechnicalProposal");

        // مدير حساب حقيقيّ لكنّه لعميل آخر تمامًا.
        var (other, otherId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await CreateClientAsync(env.Admin, "عميل آخر", otherId);

        var res = await other.GetAsync($"/api/clients/{env.ClientId}/documents");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("client.not_found", await ProblemTypeAsync(res));
    }

    // ===== 7) موظّف خارج النطاق ⇒ 404 =====
    [Fact]
    public async Task OutOfScope_Employee_Gets_404_On_Client_Documents()
    {
        var env = await BuildEnvAsync();
        await UploadAsync(env.Admin, env.ClientId, "تقرير عام", "Report");

        var res = await env.Outsider.GetAsync($"/api/clients/{env.ClientId}/documents");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("client.not_found", await ProblemTypeAsync(res));
    }

    // ===== 8) أدوار مخصّصة =====
    [Fact]
    public async Task CustomRoles_Grants_Only_Listed_Roles()
    {
        var env = await BuildEnvAsync();
        var doc = await UploadAsync(env.Admin, env.ClientId, "مستند بأدوار مخصّصة", "Other",
            DocumentVisibilityType.CustomRoles, allowedRoles: new[] { "TeamLeader" });

        // قائد الفريق (له صلاحيّة على العميل عبر مشروعه) داخل قائمة الأدوار ⇒ يرى.
        Assert.Contains("مستند بأدوار مخصّصة", await TitlesAsync(env.TeamLeader, env.ClientId));

        // مدير العميل له صلاحيّة على العميل لكنّ دوره ليس في القائمة ⇒ لا يرى.
        Assert.DoesNotContain("مستند بأدوار مخصّصة", await TitlesAsync(env.AccountManager, env.ClientId));
        var res = await env.AccountManager.GetAsync($"/api/clients/{env.ClientId}/documents/{doc.Document.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== 9) أشخاص محدّدون =====
    [Fact]
    public async Task CustomUsers_Grants_Only_Listed_Users()
    {
        var env = await BuildEnvAsync();
        var doc = await UploadAsync(env.Admin, env.ClientId, "مستند لأشخاص محدّدين", "Other",
            DocumentVisibilityType.CustomUsers, allowedUserIds: new[] { env.AccountManagerId });

        Assert.Contains("مستند لأشخاص محدّدين", await TitlesAsync(env.AccountManager, env.ClientId));

        // قائد الفريق يرى العميل لكنّه ليس ضمن الأشخاص المحدّدين ⇒ لا يرى المستند.
        Assert.DoesNotContain("مستند لأشخاص محدّدين", await TitlesAsync(env.TeamLeader, env.ClientId));
        var res = await env.TeamLeader.GetAsync($"/api/clients/{env.ClientId}/documents/{doc.Document.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== 10) الوصول المباشر للمستند الممنوع ⇒ 404 بلا تسريب بيانات وصفيّة =====
    [Fact]
    public async Task Direct_Get_Of_Denied_Document_Returns_404_Without_Metadata()
    {
        var env = await BuildEnvAsync();
        var doc = await UploadAsync(env.Admin, env.ClientId, "اتفاقية عدم إفشاء", "NDA"); // ManagementOnly

        var res = await env.AccountManager.GetAsync($"/api/clients/{env.ClientId}/documents/{doc.Document.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("client_document.not_found", await ProblemTypeAsync(res));

        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("اتفاقية عدم إفشاء", body);
        Assert.DoesNotContain("NDA", body);
        Assert.DoesNotContain(doc.Document.CurrentFileName ?? "contract.pdf", body);
    }

    // ===== 11) التنزيل المباشر للمستند الممنوع ⇒ 404 (النسخة السارية والنسخة المحدّدة) =====
    [Fact]
    public async Task Direct_Download_Of_Denied_Document_Returns_404()
    {
        var env = await BuildEnvAsync();
        var doc = await UploadAsync(env.Admin, env.ClientId, "فاتورة", "Invoice"); // ManagementAndFinance
        var versionId = doc.Versions[0].Id;

        var current = await env.AccountManager.GetAsync($"/api/clients/{env.ClientId}/documents/{doc.Document.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, current.StatusCode);
        Assert.Equal("client_document.not_found", await ProblemTypeAsync(current));

        var version = await env.AccountManager.GetAsync(
            $"/api/clients/{env.ClientId}/documents/{doc.Document.Id}/versions/{versionId}/download");
        Assert.Equal(HttpStatusCode.NotFound, version.StatusCode);
        Assert.Equal("client_document.not_found", await ProblemTypeAsync(version));

        // المالية تُنزّل نفس المستند بنجاح ⇒ المنع سياسة لا عطل.
        var ok = await env.Finance.GetAsync($"/api/clients/{env.ClientId}/documents/{doc.Document.Id}/download");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    // ===== 12) القائمة تُخفي غير المسموح خادميًّا =====
    [Fact]
    public async Task List_Filters_Hidden_Documents_Server_Side()
    {
        var env = await BuildEnvAsync();
        await UploadAsync(env.Admin, env.ClientId, "عقد", "Contract");            // ManagementAndFinance
        await UploadAsync(env.Admin, env.ClientId, "اتفاقية سرّية", "NDA");        // ManagementOnly
        await UploadAsync(env.Admin, env.ClientId, "عرض فنّي", "TechnicalProposal"); // ProjectTeam
        await UploadAsync(env.Admin, env.ClientId, "تقرير عام", "Report");          // ClientScoped

        var amTitles = await TitlesAsync(env.AccountManager, env.ClientId);
        Assert.Equal(2, amTitles.Count);
        Assert.Contains("عرض فنّي", amTitles);
        Assert.Contains("تقرير عام", amTitles);
        Assert.DoesNotContain("عقد", amTitles);
        Assert.DoesNotContain("اتفاقية سرّية", amTitles);

        var financeTitles = await TitlesAsync(env.Finance, env.ClientId);
        Assert.Single(financeTitles);
        Assert.Contains("عقد", financeTitles);
    }

    // ===== 13) التوافق الخلفيّ: ClientScoped يبقى السلوك السابق =====
    [Fact]
    public async Task ClientScoped_Documents_Remain_Visible_To_Client_Scope()
    {
        var env = await BuildEnvAsync();
        var doc = await UploadAsync(env.Admin, env.ClientId, "تقرير أداء", "Report");
        Assert.Equal(DocumentVisibilityType.ClientScoped, doc.Document.VisibilityType);

        Assert.Contains("تقرير أداء", await TitlesAsync(env.AccountManager, env.ClientId));
        Assert.Contains("تقرير أداء", await TitlesAsync(env.TeamLeader, env.ClientId));

        var download = await env.AccountManager.GetAsync(
            $"/api/clients/{env.ClientId}/documents/{doc.Document.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
    }

    // ===== 14) مدير العميل يقرأ بيانات العميل الأساسيّة =====
    [Fact]
    public async Task AccountManager_Can_Read_Client_Core()
    {
        var env = await BuildEnvAsync();
        var res = await env.AccountManager.GetAsync($"/api/clients/{env.ClientId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<ClientDto>();
        Assert.Equal(env.ClientId, dto!.Id);
    }

    // ===== 15) مدير العميل يقرأ جهات الاتصال =====
    [Fact]
    public async Task AccountManager_Can_Read_Contacts()
    {
        var env = await BuildEnvAsync();
        await env.Admin.PostAsJsonAsync($"/api/clients/{env.ClientId}/contacts",
            new CreateClientContactRequest("جهة اتصال العميل"));

        var res = await env.AccountManager.GetAsync($"/api/clients/{env.ClientId}/contacts");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.ReadAsync<List<ClientContactDto>>();
        Assert.Contains(list!, x => x.Name == "جهة اتصال العميل");
    }

    // ===== 16) مدير العميل يقرأ القنوات الرقميّة =====
    [Fact]
    public async Task AccountManager_Can_Read_DigitalChannels()
    {
        var env = await BuildEnvAsync();
        await env.Admin.PostAsJsonAsync($"/api/clients/{env.ClientId}/digital-channels",
            new CreateClientDigitalChannelRequest("Instagram"));

        var res = await env.AccountManager.GetAsync($"/api/clients/{env.ClientId}/digital-channels");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.ReadAsync<List<ClientDigitalChannelDto>>();
        Assert.Contains(list!, x => x.PlatformCode == "Instagram");
    }

    // ===== 17) مدير العميل يقرأ ملفّ البراند =====
    [Fact]
    public async Task AccountManager_Can_Read_Brand_Profile()
    {
        var env = await BuildEnvAsync();
        await env.Admin.PutAsJsonAsync($"/api/clients/{env.ClientId}/brand",
            new UpsertClientBrandProfileRequest(BusinessOverview: "نظرة عامّة على النشاط"));

        var res = await env.AccountManager.GetAsync($"/api/clients/{env.ClientId}/brand");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 18) مدير العميل لا يملك تعديل بيانات العميل الأساسيّة =====
    [Fact]
    public async Task AccountManager_Cannot_Edit_Client_Core_403()
    {
        var env = await BuildEnvAsync();
        var res = await env.AccountManager.PutAsJsonAsync($"/api/clients/{env.ClientId}",
            new UpdateClientRequest("اسم جديد", ClientStatus.Active));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var archive = await env.AccountManager.PostAsync($"/api/clients/{env.ClientId}/archive", null);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
    }

    // ===== 19) المالية لا تصبح مدير عميل =====
    [Fact]
    public async Task Finance_DoesNotBecome_AccountManager()
    {
        var env = await BuildEnvAsync();
        await UploadAsync(env.Admin, env.ClientId, "تقرير عام", "Report"); // ClientScoped

        // وصول وظيفيّ إلى المستندات لا يعني صلاحيّة على العميل.
        var core = await env.Finance.GetAsync($"/api/clients/{env.ClientId}");
        Assert.Equal(HttpStatusCode.Forbidden, core.StatusCode);

        var edit = await env.Finance.PutAsJsonAsync($"/api/clients/{env.ClientId}",
            new UpdateClientRequest("اسم من المالية", ClientStatus.Active));
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);

        // ولا ترى المستندات ذات النطاق العميليّ.
        Assert.DoesNotContain("تقرير عام", await TitlesAsync(env.Finance, env.ClientId));
    }

    // ===== 20) لا تسريب لمسار التخزين أو الأسرار =====
    [Fact]
    public async Task StorageKey_Never_Leaks_In_Any_Document_Response()
    {
        var env = await BuildEnvAsync();
        var doc = await UploadAsync(env.Admin, env.ClientId, "عقد الخدمات", "Contract");

        var list = await (await env.Admin.GetAsync($"/api/clients/{env.ClientId}/documents")).Content.ReadAsStringAsync();
        var get = await (await env.Admin.GetAsync($"/api/clients/{env.ClientId}/documents/{doc.Document.Id}")).Content.ReadAsStringAsync();
        var usage = await (await env.Admin.GetAsync($"/api/clients/{env.ClientId}/documents/storage-usage")).Content.ReadAsStringAsync();

        foreach (var body in new[] { list, get, usage })
        {
            Assert.DoesNotContain("storageKey", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("StorageKey", body, StringComparison.Ordinal);
        }
    }

    // ===== إضافيّ مُلزَم: Contract = ManagementAndFinance ⇒ المالية ترى ومدير العميل لا يرى =====
    [Fact]
    public async Task Contract_ManagementAndFinance_Finance_Sees_AccountManager_Does_Not()
    {
        var env = await BuildEnvAsync();
        var doc = await UploadAsync(env.Admin, env.ClientId, "عقد الخدمات السنوي", "Contract");
        Assert.Equal(DocumentVisibilityType.ManagementAndFinance, doc.Document.VisibilityType);

        Assert.Contains("عقد الخدمات السنوي", await TitlesAsync(env.Finance, env.ClientId));
        Assert.DoesNotContain("عقد الخدمات السنوي", await TitlesAsync(env.AccountManager, env.ClientId));

        var denied = await env.AccountManager.GetAsync($"/api/clients/{env.ClientId}/documents/{doc.Document.Id}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal("client_document.not_found", await ProblemTypeAsync(denied));

        // فصل ManagementAndFinance عن FinanceOnly: كلاهما تراه المالية، والإدارة ترى الأوّل دون الثاني.
        var financeOnly = await UploadAsync(env.Admin, env.ClientId, "بيان مالي داخليّ", "Other",
            DocumentVisibilityType.FinanceOnly);
        Assert.Contains("بيان مالي داخليّ", await TitlesAsync(env.Finance, env.ClientId));
        Assert.NotEqual(doc.Document.VisibilityType, financeOnly.Document.VisibilityType);
    }

    // ===== البيئة والمساعدون =====

    private sealed record Env(
        HttpClient Admin,
        Guid ClientId,
        HttpClient AccountManager,
        Guid AccountManagerId,
        HttpClient Finance,
        HttpClient TeamLeader,
        HttpClient Outsider);

    /// <summary>
    /// عميل واحد بمدير عميل (موظّف عاديّ)، ومالية على مستوى الشركة، وقائد فريق يكتسب صلاحيّة العميل
    /// عبر كونه مدير حساب مشروع تابع للعميل، وموظّف خارج النطاق تمامًا.
    /// </summary>
    private async Task<Env> BuildEnvAsync()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (am, amId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var (finance, _) = await TestAuth.CreateUserAsync(_factory, "FinanceManager");
        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (outsider, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var client = await CreateClientAsync(admin, $"عميل صلاحيّات {Guid.NewGuid():N}", amId);

        // مشروع تابع للعميل مدير حسابه قائد الفريق ⇒ يكتسب صلاحيّة قراءة العميل (لا صلاحيّة إدارة).
        var project = await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, "مشروع الصلاحيّات", ServiceType.Social, AccountManagerId: tlId));
        Assert.Equal(HttpStatusCode.OK, project.StatusCode);

        return new Env(admin, client.Id, am, amId, finance, tl, outsider);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient c, string name, Guid? accountManagerId = null)
        => (await (await c.PostAsJsonAsync("/api/clients",
                new CreateClientRequest(name, AccountManagerId: accountManagerId)))
            .ReadAsync<ClientDto>())!;

    private static async Task<ClientDocumentDetailDto> UploadAsync(
        HttpClient c,
        Guid clientId,
        string title,
        string categoryCode,
        DocumentVisibilityType? visibility = null,
        IEnumerable<string>? allowedRoles = null,
        IEnumerable<Guid>? allowedUserIds = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(PdfBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "File", "contract.pdf");
        form.Add(new StringContent(title, Encoding.UTF8), "Title");
        form.Add(new StringContent(categoryCode, Encoding.UTF8), "CategoryCode");
        if (visibility is DocumentVisibilityType v)
            form.Add(new StringContent(v.ToString(), Encoding.UTF8), "VisibilityType");
        foreach (var role in allowedRoles ?? Array.Empty<string>())
            form.Add(new StringContent(role, Encoding.UTF8), "AllowedRoles");
        foreach (var uid in allowedUserIds ?? Array.Empty<Guid>())
            form.Add(new StringContent(uid.ToString(), Encoding.UTF8), "AllowedUserIds");

        var res = await c.PostAsync($"/api/clients/{clientId}/documents", form);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<ClientDocumentDetailDto>())!;
    }

    private static async Task<List<string>> TitlesAsync(HttpClient c, Guid clientId)
    {
        var res = await c.GetAsync($"/api/clients/{clientId}/documents");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.ReadAsync<List<ClientDocumentDto>>();
        return list!.Select(x => x.Title).ToList();
    }

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
    }
}
