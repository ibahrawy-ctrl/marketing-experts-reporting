using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Reporting.Application.Clients;
using Reporting.Application.Documents;
using Reporting.Domain.Enums;

namespace Reporting.IntegrationTests;

/// <summary>
/// اختبارات خدمة مستندات العميل (CPW-R1B2).
/// تغطّي: دورة الرفع/التنزيل، قائمة السماح والبصمة السحريّة، تعدّد النسخ،
/// الأرشفة، الحذف Tombstone وحارس الإدارة العليا، منع الاستكشاف (404 لا 403)،
/// حارس الأسرار، وعدم تسرّب <c>StorageKey</c> إلى أيّ استجابة.
/// </summary>
[Collection("Integration")]
public class ClientDocumentsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ClientDocumentsTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== أدوات مساعدة =====

    private static readonly byte[] PdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n%%EOF\n");

    private static readonly byte[] PngBytes =
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };

    private static readonly byte[] SvgBytes =
        Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"1\" height=\"1\"/></svg>");

    private static async Task<ClientDto> CreateClientAsync(HttpClient c, string name, Guid? accountManagerId = null)
        => (await (await c.PostAsJsonAsync("/api/clients", new CreateClientRequest(name, AccountManagerId: accountManagerId)))
            .ReadAsync<ClientDto>())!;

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private static MultipartFormDataContent Upload(
        byte[] bytes,
        string fileName,
        string? contentType,
        string title = "عقد الخدمات",
        string categoryCode = "Contract",
        string? description = null,
        string? tags = null,
        string? confidentialityCode = null,
        string? changeNote = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        if (contentType is not null) file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "File", fileName);
        form.Add(new StringContent(title, Encoding.UTF8), "Title");
        form.Add(new StringContent(categoryCode, Encoding.UTF8), "CategoryCode");
        if (description is not null) form.Add(new StringContent(description, Encoding.UTF8), "Description");
        if (tags is not null) form.Add(new StringContent(tags, Encoding.UTF8), "Tags");
        if (confidentialityCode is not null) form.Add(new StringContent(confidentialityCode, Encoding.UTF8), "ConfidentialityCode");
        if (changeNote is not null) form.Add(new StringContent(changeNote, Encoding.UTF8), "ChangeNote");
        return form;
    }

    private static MultipartFormDataContent VersionUpload(byte[] bytes, string fileName, string contentType, string? changeNote = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "File", fileName);
        if (changeNote is not null) form.Add(new StringContent(changeNote, Encoding.UTF8), "ChangeNote");
        return form;
    }

    private static async Task<ClientDocumentDetailDto> UploadPdfAsync(HttpClient c, Guid clientId, string title = "عقد الخدمات")
    {
        var res = await c.PostAsync($"/api/clients/{clientId}/documents", Upload(PdfBytes, "contract.pdf", "application/pdf", title));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<ClientDocumentDetailDto>())!;
    }

    // ===== 1) دورة الرفع الأساسيّة =====

    [Fact]
    public async Task Upload_Pdf_CreatesDocumentWithFirstVersion()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل مستندات {Guid.NewGuid():N}");

        var detail = await UploadPdfAsync(admin, client.Id);

        Assert.Equal(client.Id, detail.Document.ClientId);
        Assert.Equal("عقد الخدمات", detail.Document.Title);
        Assert.Equal("Contract", detail.Document.CategoryCode);
        Assert.Equal(DocumentLifecycleStatus.Current, detail.Document.LifecycleStatus);
        Assert.Equal(1, detail.Document.VersionCount);
        Assert.Equal(1, detail.Document.CurrentVersionNo);
        Assert.Equal("contract.pdf", detail.Document.CurrentFileName);
        Assert.Equal("application/pdf", detail.Document.CurrentContentType);
        Assert.Equal(PdfBytes.Length, detail.Document.CurrentSizeBytes);
        Assert.False(detail.Document.CurrentForceAttachment);

        var version = Assert.Single(detail.Versions);
        Assert.Equal(1, version.VersionNo);
        Assert.True(version.IsCurrent);
        Assert.Equal(64, version.Sha256.Length);
        // C-01: لا محرّك فحص ⇒ الحالة الصادقة NotScanned لا Clean.
        Assert.Equal(DocumentScanStatus.NotScanned, version.ScanStatus);
        Assert.Equal("None", version.ScanEngine);
    }

    [Fact]
    public async Task Documents_Response_DoesNotLeakStorageKey()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل تسريب {Guid.NewGuid():N}");
        var detail = await UploadPdfAsync(admin, client.Id);

        var detailJson = await (await admin.GetAsync($"/api/clients/{client.Id}/documents/{detail.Document.Id}")).Content.ReadAsStringAsync();
        var listJson = await (await admin.GetAsync($"/api/clients/{client.Id}/documents")).Content.ReadAsStringAsync();

        foreach (var payload in new[] { detailJson, listJson })
        {
            Assert.DoesNotContain("storageKey", payload, StringComparison.OrdinalIgnoreCase);
            // مسار التخزين يبدأ دائمًا بنوع المورد ثمّ معرّف العميل.
            Assert.DoesNotContain($"clients/{client.Id:D}", payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ===== 2) سياسة المحتوى (C-04) =====

    [Fact]
    public async Task Upload_DisallowedExtension_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل امتداد {Guid.NewGuid():N}");

        var res = await admin.PostAsync($"/api/clients/{client.Id}/documents",
            Upload(PdfBytes, "payload.exe", "application/octet-stream"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("document.extension_not_allowed", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Upload_MagicNumberMismatch_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل بصمة {Guid.NewGuid():N}");

        // امتداد ونوع محتوى مطابقان، لكنّ البايتات ليست PDF إطلاقًا.
        var res = await admin.PostAsync($"/api/clients/{client.Id}/documents",
            Upload(Encoding.UTF8.GetBytes("this is definitely not a pdf"), "fake.pdf", "application/pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("document.magic_number_mismatch", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Upload_MimeMismatch_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل نوع {Guid.NewGuid():N}");

        var res = await admin.PostAsync($"/api/clients/{client.Id}/documents",
            Upload(PdfBytes, "contract.pdf", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("document.mime_mismatch", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Upload_EmptyFile_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل فارغ {Guid.NewGuid():N}");

        var res = await admin.PostAsync($"/api/clients/{client.Id}/documents",
            Upload(Array.Empty<byte>(), "empty.pdf", "application/pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("document.file_required", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Upload_InvalidCategory_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل تصنيف {Guid.NewGuid():N}");

        var res = await admin.PostAsync($"/api/clients/{client.Id}/documents",
            Upload(PdfBytes, "contract.pdf", "application/pdf", categoryCode: "Alien"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_document.category_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Upload_SecretInMetadata_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل سرّ {Guid.NewGuid():N}");

        var res = await admin.PostAsync($"/api/clients/{client.Id}/documents",
            Upload(PdfBytes, "contract.pdf", "application/pdf", description: "the password is 123456"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_document.secret_forbidden", await ProblemTypeAsync(res));
    }

    // ===== 3) تعدّد النسخ =====

    [Fact]
    public async Task AddVersion_SupersedesPreviousWithoutDeletingIt()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل نسخ {Guid.NewGuid():N}");
        var created = await UploadPdfAsync(admin, client.Id);

        var res = await admin.PostAsync($"/api/clients/{client.Id}/documents/{created.Document.Id}/versions",
            VersionUpload(PngBytes, "contract-v2.png", "image/png", "النسخة المعدّلة"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var detail = (await res.ReadAsync<ClientDocumentDetailDto>())!;
        Assert.Equal(2, detail.Document.VersionCount);
        Assert.Equal(2, detail.Document.CurrentVersionNo);
        Assert.Equal("contract-v2.png", detail.Document.CurrentFileName);
        Assert.Equal(2, detail.Versions.Count);

        var v2 = detail.Versions.Single(v => v.VersionNo == 2);
        var v1 = detail.Versions.Single(v => v.VersionNo == 1);
        Assert.True(v2.IsCurrent);
        Assert.False(v1.IsCurrent);
        Assert.Equal("النسخة المعدّلة", v2.ChangeNote);
        // النسخة القديمة تبقى قابلة للتنزيل — لا حذف للملفّ.
        var old = await admin.GetAsync($"/api/clients/{client.Id}/documents/{created.Document.Id}/versions/{v1.Id}/download");
        Assert.Equal(HttpStatusCode.OK, old.StatusCode);
        Assert.Equal(PdfBytes, await old.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task AddVersion_SecretChangeNote_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل ملاحظة {Guid.NewGuid():N}");
        var created = await UploadPdfAsync(admin, client.Id);

        var res = await admin.PostAsync($"/api/clients/{client.Id}/documents/{created.Document.Id}/versions",
            VersionUpload(PdfBytes, "v2.pdf", "application/pdf", "api_key الجديد بالداخل"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_document.secret_forbidden", await ProblemTypeAsync(res));
    }

    // ===== 4) الأرشفة =====

    [Fact]
    public async Task Archive_HidesFromDefaultListAndBlocksNewVersions()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل أرشفة {Guid.NewGuid():N}");
        var created = await UploadPdfAsync(admin, client.Id);
        var url = $"/api/clients/{client.Id}/documents/{created.Document.Id}";

        var archived = await admin.PatchAsJsonAsync($"{url}/archive", new ArchiveClientDocumentRequest("انتهت صلاحيّة العقد"));
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        var dto = (await archived.ReadAsync<ClientDocumentDto>())!;
        Assert.True(dto.IsArchived);
        Assert.Equal(DocumentLifecycleStatus.Archived, dto.LifecycleStatus);

        var visible = (await (await admin.GetAsync($"/api/clients/{client.Id}/documents")).ReadAsync<List<ClientDocumentDto>>())!;
        Assert.DoesNotContain(visible, d => d.Id == created.Document.Id);

        var all = (await (await admin.GetAsync($"/api/clients/{client.Id}/documents?includeArchived=true")).ReadAsync<List<ClientDocumentDto>>())!;
        Assert.Contains(all, d => d.Id == created.Document.Id);

        var again = await admin.PatchAsJsonAsync($"{url}/archive", new ArchiveClientDocumentRequest("تكرار"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("client_document.state_unchanged.conflict", await ProblemTypeAsync(again));

        var version = await admin.PostAsync($"{url}/versions", VersionUpload(PdfBytes, "v2.pdf", "application/pdf"));
        Assert.Equal(HttpStatusCode.Conflict, version.StatusCode);
        Assert.Equal("client_document.archived.conflict", await ProblemTypeAsync(version));

        var restored = await admin.PatchAsJsonAsync($"{url}/unarchive", new ArchiveClientDocumentRequest(null));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.False((await restored.ReadAsync<ClientDocumentDto>())!.IsArchived);
    }

    // ===== 5) الحذف Tombstone =====

    [Fact]
    public async Task Delete_RequiresReason()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل حذف {Guid.NewGuid():N}");
        var created = await UploadPdfAsync(admin, client.Id);

        var res = await admin.PostAsJsonAsync(
            $"/api/clients/{client.Id}/documents/{created.Document.Id}/delete",
            new DeleteClientDocumentRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("client_document.delete_reason_required", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Delete_ByAccountManagerWithoutTopManagement_IsForbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var client = await CreateClientAsync(admin, $"عميل مدير {Guid.NewGuid():N}", managerId);

        // مدير الحساب يرفع ويعدّل بلا مشكلة…
        var created = await UploadPdfAsync(manager, client.Id);

        // …لكنّ الحذف مقصور على الإدارة العليا.
        var res = await manager.PostAsJsonAsync(
            $"/api/clients/{client.Id}/documents/{created.Document.Id}/delete",
            new DeleteClientDocumentRequest("لم يعد مطلوبًا"));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("auth.forbidden", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Delete_ByAdmin_TombstonesAndHidesDocument()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل شاهدة {Guid.NewGuid():N}");
        var created = await UploadPdfAsync(admin, client.Id);
        var url = $"/api/clients/{client.Id}/documents/{created.Document.Id}";

        var res = await admin.PostAsJsonAsync($"{url}/delete", new DeleteClientDocumentRequest("طلب العميل"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{url}/download")).StatusCode);

        var all = (await (await admin.GetAsync($"/api/clients/{client.Id}/documents?includeArchived=true")).ReadAsync<List<ClientDocumentDto>>())!;
        Assert.DoesNotContain(all, d => d.Id == created.Document.Id);

        // الحذف المنطقيّ لا يُحرّر الحصّة لأنّ الملفّ باقٍ على القرص… لكنّه يخرج من الاحتساب المعروض.
        var usage = (await (await admin.GetAsync($"/api/clients/{client.Id}/documents/storage-usage")).ReadAsync<ClientStorageUsageDto>())!;
        Assert.Equal(0, usage.DocumentCount);
    }

    // ===== 6) التنزيل (C-02) =====

    [Fact]
    public async Task Download_ReturnsExactBytesInline()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل تنزيل {Guid.NewGuid():N}");
        var created = await UploadPdfAsync(admin, client.Id);

        var res = await admin.GetAsync($"/api/clients/{client.Id}/documents/{created.Document.Id}/download");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/pdf", res.Content.Headers.ContentType!.MediaType);
        Assert.Equal("inline", res.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(PdfBytes, await res.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Download_Svg_IsAlwaysAnAttachment()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل SVG {Guid.NewGuid():N}");

        var upload = await admin.PostAsync($"/api/clients/{client.Id}/documents",
            Upload(SvgBytes, "logo.svg", "image/svg+xml", title: "شعار العميل", categoryCode: "Logo"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var detail = (await upload.ReadAsync<ClientDocumentDetailDto>())!;
        Assert.True(detail.Document.CurrentForceAttachment);

        var res = await admin.GetAsync($"/api/clients/{client.Id}/documents/{detail.Document.Id}/download");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("attachment", res.Content.Headers.ContentDisposition!.DispositionType);
    }

    [Fact]
    public async Task Download_Anonymous_IsUnauthorized()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل مجهول {Guid.NewGuid():N}");
        var created = await UploadPdfAsync(admin, client.Id);

        var anonymous = _factory.CreateClient();
        var res = await anonymous.GetAsync($"/api/clients/{client.Id}/documents/{created.Document.Id}/download");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ===== 7) منع الاستكشاف =====

    [Fact]
    public async Task Documents_OutOfScopeClient_LooksLikeNotFound()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل خارج النطاق {Guid.NewGuid():N}");
        await UploadPdfAsync(admin, client.Id);

        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.GetAsync($"/api/clients/{client.Id}/documents");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("client.not_found", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Document_UnderAnotherClient_IsNotFound()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var owner = await CreateClientAsync(admin, $"عميل مالك {Guid.NewGuid():N}");
        var other = await CreateClientAsync(admin, $"عميل آخر {Guid.NewGuid():N}");
        var created = await UploadPdfAsync(admin, owner.Id);

        var res = await admin.GetAsync($"/api/clients/{other.Id}/documents/{created.Document.Id}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("client_document.not_found", await ProblemTypeAsync(res));
    }

    // ===== 8) حدود التخزين المعلَنة =====

    [Fact]
    public async Task StorageUsage_ReportsLimitsAndHonestScannerState()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل حصّة {Guid.NewGuid():N}");
        await UploadPdfAsync(admin, client.Id);

        var usage = (await (await admin.GetAsync($"/api/clients/{client.Id}/documents/storage-usage"))
            .ReadAsync<ClientStorageUsageDto>())!;

        Assert.Equal(client.Id, usage.ClientId);
        Assert.Equal(PdfBytes.Length, usage.UsedBytes);
        Assert.Equal(1, usage.DocumentCount);
        Assert.Equal(1, usage.VersionCount);
        Assert.Equal(25L * 1024 * 1024, usage.MaxUploadSizeBytes);
        Assert.Equal(2L * 1024 * 1024 * 1024, usage.QuotaBytes);
        Assert.Equal(usage.QuotaBytes - usage.UsedBytes, usage.RemainingBytes);
        Assert.Contains(".pdf", usage.AllowedExtensions);
        Assert.DoesNotContain(".exe", usage.AllowedExtensions);
        // C-01: لا محرّك فحص مُهيّأ ولا ادّعاء بالعكس.
        Assert.Equal("None", usage.ScanEngine);
        Assert.False(usage.ScannerConfigured);
    }
}

/// <summary>
/// اختبارات الروابط المهمّة للعميل (CPW-R1B2) — مرجع عنوان فقط بلا أيّ سرّ مضمَّن،
/// ولا حذف نهائيّ (التعطيل هو المسار الوحيد).
/// </summary>
[Collection("Integration")]
public class ClientExternalLinksTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ClientExternalLinksTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<ClientDto> CreateClientAsync(HttpClient c, string name)
        => (await (await c.PostAsJsonAsync("/api/clients", new CreateClientRequest(name))).ReadAsync<ClientDto>())!;

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    [Fact]
    public async Task Link_CreateUpdateDeactivate_RoundTrip()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل روابط {Guid.NewGuid():N}");

        var created = (await (await admin.PostAsJsonAsync($"/api/clients/{client.Id}/links",
            new CreateClientExternalLinkRequest("مجلّد الأصول", "https://drive.example.com/folder/abc", "Drive", "أصول الهويّة", 1)))
            .ReadAsync<ClientExternalLinkDto>())!;

        Assert.Equal("مجلّد الأصول", created.Title);
        Assert.Equal("Drive", created.CategoryCode);
        Assert.True(created.IsActive);

        var updated = (await (await admin.PutAsJsonAsync($"/api/clients/{client.Id}/links/{created.Id}",
            new UpdateClientExternalLinkRequest("مجلّد الأصول (محدّث)", "https://drive.example.com/folder/xyz", "Drive", null, 2)))
            .ReadAsync<ClientExternalLinkDto>())!;
        Assert.Equal("مجلّد الأصول (محدّث)", updated.Title);
        Assert.Equal("https://drive.example.com/folder/xyz", updated.Url);

        var deactivated = await admin.PatchAsync($"/api/clients/{client.Id}/links/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.False((await deactivated.ReadAsync<ClientExternalLinkDto>())!.IsActive);

        var active = (await (await admin.GetAsync($"/api/clients/{client.Id}/links")).ReadAsync<List<ClientExternalLinkDto>>())!;
        Assert.DoesNotContain(active, l => l.Id == created.Id);

        var all = (await (await admin.GetAsync($"/api/clients/{client.Id}/links?includeInactive=true")).ReadAsync<List<ClientExternalLinkDto>>())!;
        Assert.Contains(all, l => l.Id == created.Id);

        var again = await admin.PatchAsync($"/api/clients/{client.Id}/links/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("client_external_link.state_unchanged.conflict", await ProblemTypeAsync(again));
    }

    [Theory]
    [InlineData("ftp://files.example.com/assets", "external_link.scheme_not_allowed")]
    [InlineData("https://user:p4ss@drive.example.com/folder", "external_link.embedded_credentials")]
    [InlineData("https://drive.example.com/folder?access_token=abcdef", "external_link.secret_detected")]
    [InlineData("not-a-url-at-all", "external_link.url_invalid")]
    public async Task Link_UnsafeUrl_IsRejected(string url, string expectedCode)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل رابط غير آمن {Guid.NewGuid():N}");

        var res = await admin.PostAsJsonAsync($"/api/clients/{client.Id}/links",
            new CreateClientExternalLinkRequest("رابط", url, "Drive"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(expectedCode, await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Link_InvalidCategoryOrSecretTitle_IsRejected()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل رابط تصنيف {Guid.NewGuid():N}");

        var category = await admin.PostAsJsonAsync($"/api/clients/{client.Id}/links",
            new CreateClientExternalLinkRequest("رابط", "https://example.com", "Alien"));
        Assert.Equal(HttpStatusCode.BadRequest, category.StatusCode);
        Assert.Equal("client_external_link.category_invalid", await ProblemTypeAsync(category));

        var secret = await admin.PostAsJsonAsync($"/api/clients/{client.Id}/links",
            new CreateClientExternalLinkRequest("لوحة التحكّم — password: 1234", "https://example.com", "Website"));
        Assert.Equal(HttpStatusCode.BadRequest, secret.StatusCode);
        Assert.Equal("client_external_link.secret_forbidden", await ProblemTypeAsync(secret));
    }

    [Fact]
    public async Task Links_OutOfScopeClient_LooksLikeNotFound()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل روابط خارج النطاق {Guid.NewGuid():N}");

        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var res = await employee.GetAsync($"/api/clients/{client.Id}/links");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("client.not_found", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Links_Anonymous_IsUnauthorized()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var client = await CreateClientAsync(admin, $"عميل روابط مجهول {Guid.NewGuid():N}");

        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/clients/{client.Id}/links")).StatusCode);
    }
}
