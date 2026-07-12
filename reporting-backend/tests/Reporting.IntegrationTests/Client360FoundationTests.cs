using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Clients;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// Client 360 Foundation (CPW-R1B). يثبت: الحقول الموسَّعة التسعة للعميل (round-trip + رفض الرموز
/// غير المعتمَدة والروابط غير الصالحة والأسرار)، جهات الاتصال (CRUD + جهة أساسية نشطة واحدة +
/// التعطيل بدل الحذف)، القنوات الرقمية (CRUD + تحقّق المنصّة/حالة الوصول/الرابط + لا أسرار)،
/// ملفّ البراند (Upsert 1:0..1 + تحقّق الرابط/الأسرار)، الصلاحية القائمة على المورد (مدير الحساب
/// يكتب الأبناء، وخارج النطاق يُمنع)، وسياسة ClientCoreManagement (تستثني قائد الفريق) على العمليات
/// الأساسية الخمس.
/// </summary>
[Collection("Integration")]
public class Client360FoundationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public Client360FoundationTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== الحقول الموسَّعة للعميل =====
    [Fact]
    public async Task CreateClient_WithFullProfile_RoundTrips()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var req = new CreateClientRequest(
            "عميل ملفّ كامل",
            AccountManagerId: null,
            MainContactName: null, MainContactInfo: null, Notes: null,
            Status: Reporting.Domain.Enums.ClientStatus.Active,
            TradeNameEn: "Full Profile Co",
            LegalName: "Full Profile LLC",
            ClientTypeCode: "Company",
            SectorCode: "Technology",
            Country: "SA",
            City: "الرياض",
            Website: "https://example.com",
            SourceCode: "Referral",
            RelationshipStartDate: new DateOnly(2026, 1, 15));

        var res = await admin.PostAsJsonAsync("/api/clients", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<ClientDto>();
        Assert.NotNull(dto);
        Assert.Equal("Full Profile Co", dto!.TradeNameEn);
        Assert.Equal("Full Profile LLC", dto.LegalName);
        Assert.Equal("Company", dto.ClientTypeCode);
        Assert.Equal("Technology", dto.SectorCode);
        Assert.Equal("SA", dto.Country);
        Assert.Equal("الرياض", dto.City);
        Assert.Equal("https://example.com", dto.Website);
        Assert.Equal("Referral", dto.SourceCode);
        Assert.Equal(new DateOnly(2026, 1, 15), dto.RelationshipStartDate);
    }

    [Fact]
    public async Task UpdateClient_Profile_RoundTrips()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل تحديث الملفّ");

        var upd = new UpdateClientRequest(
            "عميل تحديث الملفّ",
            Reporting.Domain.Enums.ClientStatus.Active,
            AccountManagerId: null,
            MainContactName: null, MainContactInfo: null, Notes: null,
            TradeNameEn: "Updated Co",
            LegalName: null,
            ClientTypeCode: "Agency",
            SectorCode: "Ecommerce",
            Country: "AE",
            City: "دبي",
            Website: "https://updated.example.com",
            SourceCode: "Website",
            RelationshipStartDate: null);

        var res = await admin.PutAsJsonAsync($"/api/clients/{client.Id}", upd);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<ClientDto>();
        Assert.Equal("Updated Co", dto!.TradeNameEn);
        Assert.Equal("Agency", dto.ClientTypeCode);
        Assert.Equal("Ecommerce", dto.SectorCode);
        Assert.Equal("AE", dto.Country);
        Assert.Equal("https://updated.example.com", dto.Website);
        Assert.Equal("Website", dto.SourceCode);
    }

    [Fact]
    public async Task CreateClient_InvalidClientType_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest("نوع غير صالح", ClientTypeCode: "Alien"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client.client_type_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task CreateClient_InvalidSector_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest("قطاع غير صالح", SectorCode: "Nonsense"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client.sector_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task CreateClient_InvalidSource_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest("مصدر غير صالح", SourceCode: "Magic"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client.source_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task CreateClient_InvalidWebsite_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest("رابط غير صالح", Website: "ftp://example.com"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client.website_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task CreateClient_SecretInField_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest("سرّ محظور", Notes: "الحساب password=Admin#123"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client.secret_forbidden", await ProblemTypeAsync(res));
    }

    // ===== جهات الاتصال =====
    [Fact]
    public async Task Contact_Create_And_List_RoundTrip()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل جهات الاتصال");

        var created = await CreateContactAsync(admin, client.Id,
            new CreateClientContactRequest("أحمد", JobTitle: "مدير تسويق",
                Email: "a@example.com", PreferredContactMethodCode: "Email", IsPrimary: true));
        Assert.Equal("أحمد", created.Name);
        Assert.True(created.IsPrimary);
        Assert.True(created.IsActive);

        var list = await (await admin.GetAsync($"/api/clients/{client.Id}/contacts"))
            .ReadAsync<List<ClientContactDto>>();
        Assert.Contains(created.Id, list!.Select(c => c.Id));
    }

    [Fact]
    public async Task Contact_SingleActivePrimary_Enforced()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل جهة أساسية واحدة");

        var first = await CreateContactAsync(admin, client.Id,
            new CreateClientContactRequest("الأول", IsPrimary: true));
        var second = await CreateContactAsync(admin, client.Id,
            new CreateClientContactRequest("الثاني", IsPrimary: true));

        var list = await (await admin.GetAsync($"/api/clients/{client.Id}/contacts"))
            .ReadAsync<List<ClientContactDto>>();
        var primaries = list!.Where(c => c.IsPrimary && c.IsActive).ToList();
        Assert.Single(primaries);
        Assert.Equal(second.Id, primaries[0].Id);
        Assert.False(list!.Single(c => c.Id == first.Id).IsPrimary);
    }

    [Fact]
    public async Task Contact_Deactivate_Instead_Of_Delete()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل تعطيل جهة");
        var contact = await CreateContactAsync(admin, client.Id,
            new CreateClientContactRequest("للتعطيل", IsPrimary: true));

        var res = await admin.PatchAsync($"/api/clients/{client.Id}/contacts/{contact.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<ClientContactDto>();
        Assert.False(dto!.IsActive);
        Assert.False(dto.IsPrimary); // تعطيل الأساسيّ يُلغي كونه أساسيًّا

        // ما زال موجودًا (غير محذوف) ويظهر عند includeInactive.
        var all = await (await admin.GetAsync($"/api/clients/{client.Id}/contacts?includeInactive=true"))
            .ReadAsync<List<ClientContactDto>>();
        Assert.Contains(contact.Id, all!.Select(c => c.Id));

        // لا يظهر افتراضيًّا (النشطون فقط).
        var activeOnly = await (await admin.GetAsync($"/api/clients/{client.Id}/contacts"))
            .ReadAsync<List<ClientContactDto>>();
        Assert.DoesNotContain(contact.Id, activeOnly!.Select(c => c.Id));
    }

    [Fact]
    public async Task Contact_InvalidMethod_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل طريقة تواصل خاطئة");
        var res = await admin.PostAsJsonAsync($"/api/clients/{client.Id}/contacts",
            new CreateClientContactRequest("خطأ", PreferredContactMethodCode: "Telepathy"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_contact.method_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Contact_Secret_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل سرّ جهة اتصال");
        var res = await admin.PostAsJsonAsync($"/api/clients/{client.Id}/contacts",
            new CreateClientContactRequest("خطأ", Notes: "التوكن bearer abc123"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_contact.secret_forbidden", await ProblemTypeAsync(res));
    }

    // ===== القنوات الرقمية =====
    [Fact]
    public async Task Channel_Create_And_List_RoundTrip()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل القنوات");
        var created = await CreateChannelAsync(admin, client.Id,
            new CreateClientDigitalChannelRequest("Instagram", DisplayName: "حساب رسميّ",
                UsernameOrHandle: "@brand", ProfileUrl: "https://instagram.com/brand",
                AccessStatusCode: "FullAccess"));
        Assert.Equal("Instagram", created.PlatformCode);
        Assert.Equal("FullAccess", created.AccessStatusCode);

        var list = await (await admin.GetAsync($"/api/clients/{client.Id}/digital-channels"))
            .ReadAsync<List<ClientDigitalChannelDto>>();
        Assert.Contains(created.Id, list!.Select(c => c.Id));
    }

    [Fact]
    public async Task Channel_InvalidPlatform_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل منصّة خاطئة");
        var res = await admin.PostAsJsonAsync($"/api/clients/{client.Id}/digital-channels",
            new CreateClientDigitalChannelRequest("MySpace"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_channel.platform_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Channel_InvalidAccessStatus_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل حالة وصول خاطئة");
        var res = await admin.PostAsJsonAsync($"/api/clients/{client.Id}/digital-channels",
            new CreateClientDigitalChannelRequest("Facebook", AccessStatusCode: "Maybe"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_channel.access_status_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Channel_InvalidProfileUrl_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل رابط قناة خاطئ");
        var res = await admin.PostAsJsonAsync($"/api/clients/{client.Id}/digital-channels",
            new CreateClientDigitalChannelRequest("Facebook", ProfileUrl: "not a url"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_channel.profile_url_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Channel_Secret_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل سرّ قناة");
        var res = await admin.PostAsJsonAsync($"/api/clients/{client.Id}/digital-channels",
            new CreateClientDigitalChannelRequest("Facebook", Notes: "api_key=xyz"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_channel.secret_forbidden", await ProblemTypeAsync(res));
    }

    // ===== ملفّ البراند (1:0..1) =====
    [Fact]
    public async Task Brand_Get_WhenAbsent_ReturnsNull()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل بلا براند");
        var res = await admin.GetAsync($"/api/clients/{client.Id}/brand");
        // FromResult يُرجِع 204 NoContent عند نجاح بقيمة null (لا يوجد ملفّ براند بعد).
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Brand_Upsert_Creates_Then_Updates()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل براند");

        var create = await admin.PutAsJsonAsync($"/api/clients/{client.Id}/brand",
            new UpsertClientBrandProfileRequest(BusinessOverview: "نظرة عامّة",
                ToneOfVoice: "ودود", BrandGuidelinesUrl: "https://brand.example.com/guide.pdf"));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.ReadAsync<ClientBrandProfileDto>();
        Assert.Equal("نظرة عامّة", created!.BusinessOverview);
        Assert.Equal(client.Id, created.ClientId);

        // Upsert ثانٍ يُحدِّث نفس السجلّ (1:0..1 — لا سجلّ ثانٍ).
        var update = await admin.PutAsJsonAsync($"/api/clients/{client.Id}/brand",
            new UpsertClientBrandProfileRequest(BusinessOverview: "نظرة محدَّثة", ToneOfVoice: "رسميّ"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.ReadAsync<ClientBrandProfileDto>();
        Assert.Equal("نظرة محدَّثة", updated!.BusinessOverview);
        Assert.Equal("رسميّ", updated.ToneOfVoice);

        var got = await (await admin.GetAsync($"/api/clients/{client.Id}/brand"))
            .ReadAsync<ClientBrandProfileDto>();
        Assert.Equal("نظرة محدَّثة", got!.BusinessOverview);
    }

    [Fact]
    public async Task Brand_InvalidUrl_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل براند رابط خاطئ");
        var res = await admin.PutAsJsonAsync($"/api/clients/{client.Id}/brand",
            new UpsertClientBrandProfileRequest(BrandGuidelinesUrl: "javascript:alert(1)"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_brand.guidelines_url_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Brand_Secret_400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل براند سرّ");
        var res = await admin.PutAsJsonAsync($"/api/clients/{client.Id}/brand",
            new UpsertClientBrandProfileRequest(Notes: "secret=topSecret"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_brand.secret_forbidden", await ProblemTypeAsync(res));
    }

    // ===== الصلاحية القائمة على المورد =====
    [Fact]
    public async Task AccountManager_Can_Write_Children()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (am, amId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        // الأدمن يُنشئ العميل ويُسنِد مدير الحساب (ضمن نطاقه لأنه SeesAll).
        var client = await (await admin.PostAsJsonAsync("/api/clients",
                new CreateClientRequest("عميل مدير الحساب", AccountManagerId: amId)))
            .ReadAsync<ClientDto>();

        // مدير الحساب (Employee بلا دور إداريّ) يستطيع كتابة الأبناء لأنّه AccountManagerId.
        var contact = await am.PostAsJsonAsync($"/api/clients/{client!.Id}/contacts",
            new CreateClientContactRequest("جهة من مدير الحساب"));
        Assert.Equal(HttpStatusCode.OK, contact.StatusCode);

        var channel = await am.PostAsJsonAsync($"/api/clients/{client.Id}/digital-channels",
            new CreateClientDigitalChannelRequest("Instagram"));
        Assert.Equal(HttpStatusCode.OK, channel.StatusCode);

        var brand = await am.PutAsJsonAsync($"/api/clients/{client.Id}/brand",
            new UpsertClientBrandProfileRequest(BusinessOverview: "من مدير الحساب"));
        Assert.Equal(HttpStatusCode.OK, brand.StatusCode);
    }

    [Fact]
    public async Task OutOfScope_Employee_Cannot_Write_Children()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل خارج النطاق");
        var (stranger, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var res = await stranger.PostAsJsonAsync($"/api/clients/{client.Id}/contacts",
            new CreateClientContactRequest("محاولة غير مصرّحة"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== سياسة ClientCoreManagement (تستثني قائد الفريق) =====
    [Fact]
    public async Task TeamLeader_Cannot_Create_Client_403()
    {
        var (tl, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var res = await tl.PostAsJsonAsync("/api/clients", new CreateClientRequest("محاولة قائد الفريق"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task TeamLeader_Cannot_Archive_Or_Delete_Client_403()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, "عميل يحاول قائد الفريق أرشفته");
        var (tl, _) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");

        var archive = await tl.PostAsync($"/api/clients/{client.Id}/archive", null);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);

        var delete = await tl.DeleteAsync($"/api/clients/{client.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    // ===== مساعدون =====
    private static async Task<ClientDto> CreateClientAsync(HttpClient c, string name)
        => (await (await c.PostAsJsonAsync("/api/clients", new CreateClientRequest(name)))
            .ReadAsync<ClientDto>())!;

    private static async Task<ClientContactDto> CreateContactAsync(HttpClient c, Guid clientId, CreateClientContactRequest req)
        => (await (await c.PostAsJsonAsync($"/api/clients/{clientId}/contacts", req))
            .ReadAsync<ClientContactDto>())!;

    private static async Task<ClientDigitalChannelDto> CreateChannelAsync(HttpClient c, Guid clientId, CreateClientDigitalChannelRequest req)
        => (await (await c.PostAsJsonAsync($"/api/clients/{clientId}/digital-channels", req))
            .ReadAsync<ClientDigitalChannelDto>())!;

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
    }
}
