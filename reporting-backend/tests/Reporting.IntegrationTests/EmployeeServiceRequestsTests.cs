using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// اختبارات أمان وتكامل لطلبات الموارد البشرية العامة (V1.1 — خدمات الموظف):
/// إنشاء/عرض الموظّف لطلباته، العنوان الإلزامي، حصر القائمة العامة على الإدارة/HR،
/// مسار المعالجة (بدء المراجعة/تعليق/إكمال/رفض بسبب إلزامي)، الإلغاء قبل الإكمال،
/// ومنع الأدوار غير المخوّلة من المعالجة (403) وغير المصادَق (401).
/// </summary>
[Collection("Integration")]
public class EmployeeServiceRequestsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public EmployeeServiceRequestsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private static Task<HttpResponseMessage> CreateAsync(HttpClient c, string title,
        EmployeeServiceRequestType type = EmployeeServiceRequestType.SalaryCertificate,
        PreferredLanguage lang = PreferredLanguage.Arabic)
        => c.PostAsJsonAsync("/api/employee-service-requests",
            new CreateEmployeeServiceRequest(type, title, "وصف", lang, "جهة", null), TestJson.Options);

    private static async Task<EmployeeServiceRequestDto> CreateOkAsync(HttpClient c, string title)
        => (await (await CreateAsync(c, title)).ReadAsync<EmployeeServiceRequestDto>())!;

    // ===== 1) غير المصادَق ⇒ 401 =====
    [Fact]
    public async Task Anonymous_401()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/employee-service-requests/my")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await CreateAsync(anon, "طلب")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/employee-service-requests")).StatusCode);
    }

    // ===== 2) الموظّف ينشئ ويرى طلبه =====
    [Fact]
    public async Task Employee_Creates_And_Sees_Own_Request()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(emp.Client, "شهادة راتب");
        Assert.Equal(EmployeeServiceRequestStatus.Submitted, created.Status);
        Assert.Equal(emp.UserId, created.RequesterUserId);

        var mine = (await (await emp.Client.GetAsync("/api/employee-service-requests/my"))
            .ReadAsync<List<EmployeeServiceRequestListItemDto>>())!;
        Assert.Contains(mine, r => r.Id == created.Id);

        var byId = (await (await emp.Client.GetAsync($"/api/employee-service-requests/{created.Id}"))
            .ReadAsync<EmployeeServiceRequestDto>())!;
        Assert.Equal("شهادة راتب", byId.Title);
        Assert.True(byId.CanCancel);
    }

    // ===== 3) العنوان مطلوب ⇒ 400 =====
    [Fact]
    public async Task Create_Without_Title_400()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await CreateAsync(emp.Client, "   ");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("employee_service_request.title_required", await ErrorCodeAsync(res));
    }

    // ===== 4) الموظّف لا يرى القائمة العامة ⇒ 403 =====
    [Fact]
    public async Task Employee_Cannot_List_All_403()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var res = await emp.Client.GetAsync("/api/employee-service-requests");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 5) HR/Admin يرى القائمة العامة =====
    [Fact]
    public async Task Manager_Cannot_List_TeamLeader_Cannot_List_403()
    {
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var mgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager);
        Assert.Equal(HttpStatusCode.Forbidden, (await tl.Client.GetAsync("/api/employee-service-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mgr.Client.GetAsync("/api/employee-service-requests")).StatusCode);

        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/employee-service-requests")).StatusCode);
    }

    // ===== 6) بدء المراجعة (Submitted → InReview) =====
    [Fact]
    public async Task StartReview_Moves_To_InReview()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "خطاب بنكي");

        var res = await admin.PostAsync($"/api/employee-service-requests/{created.Id}/start-review", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = (await res.ReadAsync<EmployeeServiceRequestDto>())!;
        Assert.Equal(EmployeeServiceRequestStatus.InReview, dto.Status);
    }

    // ===== 7) تعليق HR — النص مطلوب =====
    [Fact]
    public async Task Comment_Requires_Text_And_Records()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "شهادة خبرة");

        var bad = await admin.PostAsJsonAsync($"/api/employee-service-requests/{created.Id}/comment",
            new EmployeeServiceRequestCommentRequest("  "), TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("employee_service_request.comment_required", await ErrorCodeAsync(bad));

        var ok = await admin.PostAsJsonAsync($"/api/employee-service-requests/{created.Id}/comment",
            new EmployeeServiceRequestCommentRequest("بانتظار تأكيد البيانات"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var dto = (await ok.ReadAsync<EmployeeServiceRequestDto>())!;
        Assert.Equal("بانتظار تأكيد البيانات", dto.HrComment);
    }

    // ===== 8) الإكمال (Submitted/InReview → Completed) =====
    [Fact]
    public async Task Complete_Sets_Completed_And_Timestamp()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "تحديث بيانات");

        var res = await admin.PostAsJsonAsync($"/api/employee-service-requests/{created.Id}/complete",
            new EmployeeServiceRequestCompleteRequest("تم الإصدار"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = (await res.ReadAsync<EmployeeServiceRequestDto>())!;
        Assert.Equal(EmployeeServiceRequestStatus.Completed, dto.Status);
        Assert.NotNull(dto.CompletedAtUtc);
        Assert.False(dto.CanCancel);
    }

    // ===== 9) الرفض — السبب إلزامي =====
    [Fact]
    public async Task Reject_Requires_Reason()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "خطاب سفارة");

        var bad = await admin.PostAsJsonAsync($"/api/employee-service-requests/{created.Id}/reject",
            new EmployeeServiceRequestRejectRequest("  "), TestJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("employee_service_request.rejection_reason_required", await ErrorCodeAsync(bad));

        var ok = await admin.PostAsJsonAsync($"/api/employee-service-requests/{created.Id}/reject",
            new EmployeeServiceRequestRejectRequest("بيانات ناقصة"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var dto = (await ok.ReadAsync<EmployeeServiceRequestDto>())!;
        Assert.Equal(EmployeeServiceRequestStatus.Rejected, dto.Status);
        Assert.Equal("بيانات ناقصة", dto.RejectionReason);
    }

    // ===== 10) الموظّف يلغي طلبه قبل الإكمال، ولا يمكنه بعد الإكمال =====
    [Fact]
    public async Task Owner_Cancels_Before_Completion_And_Not_After()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        var c1 = await CreateOkAsync(emp.Client, "طلب يُلغى");
        var cancel = await emp.Client.PostAsync($"/api/employee-service-requests/{c1.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(EmployeeServiceRequestStatus.Cancelled,
            (await cancel.ReadAsync<EmployeeServiceRequestDto>())!.Status);

        var c2 = await CreateOkAsync(emp.Client, "طلب يكتمل");
        await admin.PostAsJsonAsync($"/api/employee-service-requests/{c2.Id}/complete",
            new EmployeeServiceRequestCompleteRequest(null), TestJson.Options);
        var late = await emp.Client.PostAsync($"/api/employee-service-requests/{c2.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        Assert.Equal("employee_service_request.cannot_cancel", await ErrorCodeAsync(late));
    }

    // ===== 11) غير المالك لا يلغي (403) ولا يرى طلب غيره (403) =====
    [Fact]
    public async Task NonOwner_Cannot_Cancel_Or_View_403()
    {
        var owner = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var other = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(owner.Client, "طلب خاص");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await other.Client.GetAsync($"/api/employee-service-requests/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await other.Client.PostAsync($"/api/employee-service-requests/{created.Id}/cancel", null)).StatusCode);
    }

    // ===== 12) الموظّف لا يعالج (بدء مراجعة/تعليق/إكمال/رفض) ⇒ 403 =====
    [Fact]
    public async Task Employee_Cannot_Process_403()
    {
        var owner = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(owner.Client, "طلب");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await emp.Client.PostAsync($"/api/employee-service-requests/{created.Id}/start-review", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await emp.Client.PostAsJsonAsync($"/api/employee-service-requests/{created.Id}/comment",
                new EmployeeServiceRequestCommentRequest("x"), TestJson.Options)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await emp.Client.PostAsJsonAsync($"/api/employee-service-requests/{created.Id}/complete",
                new EmployeeServiceRequestCompleteRequest(null), TestJson.Options)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await emp.Client.PostAsJsonAsync($"/api/employee-service-requests/{created.Id}/reject",
                new EmployeeServiceRequestRejectRequest("سبب"), TestJson.Options)).StatusCode);
    }

    // ===== 13) HR يرشّح بالنوع/الحالة/المالك =====
    [Fact]
    public async Task Manage_List_Filters_By_User_And_Status()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "طلب فلترة");

        var byUser = (await (await admin.GetAsync(
                $"/api/employee-service-requests?userId={emp.UserId}&status={EmployeeServiceRequestStatus.Submitted}"))
            .ReadAsync<List<EmployeeServiceRequestListItemDto>>())!;
        Assert.NotEmpty(byUser);
        Assert.All(byUser, r => Assert.Equal(emp.UserId, r.RequesterUserId));
        Assert.Contains(byUser, r => r.Id == created.Id);
    }

    // ============================================================
    // HR-S2 — رفع/تنزيل الملف النهائي (الخطاب)
    // ============================================================

    private static byte[] PdfBytes(int extra = 0)
    {
        var head = Encoding.ASCII.GetBytes("%PDF-1.4\n%âãÏÓ\n1 0 obj<<>>endobj\n");
        if (extra <= 0) return head;
        var buf = new byte[head.Length + extra];
        Array.Copy(head, buf, head.Length);
        return buf;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient c, Guid id,
        byte[] bytes, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(part, "file", fileName);
        return c.PostAsync($"/api/employee-service-requests/{id}/final-document", form);
    }

    private static Task<HttpResponseMessage> UploadEmptyAsync(HttpClient c, Guid id)
        => UploadAsync(c, id, Array.Empty<byte>(), "empty.pdf", "application/pdf");

    private static Task<HttpResponseMessage> UploadNoFileAsync(HttpClient c, Guid id)
    {
        var form = new MultipartFormDataContent();
        return c.PostAsync($"/api/employee-service-requests/{id}/final-document", form);
    }

    // ===== 14) HR/Admin يرفع PDF ⇒ 200 + علم الملف + الاسم + وقت الرفع =====
    [Fact]
    public async Task Hr_Uploads_Pdf_200_SetsFinalDocument()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "خطاب راتب");

        var res = await UploadAsync(admin, created.Id, PdfBytes(), "letter.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = (await res.ReadAsync<EmployeeServiceRequestDto>())!;
        Assert.True(dto.HasFinalDocument);
        Assert.Equal("letter.pdf", dto.FinalDocumentFileName);
        Assert.NotNull(dto.FinalDocumentUploadedAt);
    }

    // ===== 15) المدير العام (HrRequestManager) يرفع ⇒ 200 =====
    [Fact]
    public async Task GeneralManager_Uploads_200()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var created = await CreateOkAsync(emp.Client, "خطاب جهة");

        var res = await UploadAsync(gm.Client, created.Id, PdfBytes(), "doc.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((await res.ReadAsync<EmployeeServiceRequestDto>())!.HasFinalDocument);
    }

    // ===== 16) الموظّف (المالك) لا يرفع ⇒ 403 =====
    [Fact]
    public async Task Owner_Employee_Cannot_Upload_403()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(emp.Client, "محاولة رفع");

        var res = await UploadAsync(emp.Client, created.Id, PdfBytes(), "x.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 17) موظّف آخر لا يرفع ⇒ 403 =====
    [Fact]
    public async Task Other_Employee_Cannot_Upload_403()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var other = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(emp.Client, "طلب");

        var res = await UploadAsync(other.Client, created.Id, PdfBytes(), "x.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 18) ملف غير PDF ⇒ 400 =====
    [Fact]
    public async Task Upload_NonPdf_400()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "طلب");

        var res = await UploadAsync(admin, created.Id, Encoding.ASCII.GetBytes("hello"), "note.txt", "text/plain");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("employee_service_request.file_must_be_pdf", await ErrorCodeAsync(res));
    }

    // ===== 19) ملف فارغ ⇒ 400 =====
    [Fact]
    public async Task Upload_Empty_400()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "طلب");

        var res = await UploadEmptyAsync(admin, created.Id);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("employee_service_request.file_required", await ErrorCodeAsync(res));
    }

    // ===== 20) لا ملف إطلاقًا ⇒ 400 =====
    [Fact]
    public async Task Upload_NoFile_400()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "طلب");

        // لا جزء "file" إطلاقًا ⇒ يرفضه التحقّق التلقائي ([ApiController]) بـ 400 قبل الوصول للخدمة.
        var res = await UploadNoFileAsync(admin, created.Id);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 21) ملف يتجاوز 10MB ⇒ 400 =====
    [Fact]
    public async Task Upload_TooLarge_400()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "طلب");

        var big = PdfBytes(10 * 1024 * 1024 + 1);
        var res = await UploadAsync(admin, created.Id, big, "big.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("employee_service_request.file_too_large", await ErrorCodeAsync(res));
    }

    // ===== 22) رفع لطلب غير موجود ⇒ 404 =====
    [Fact]
    public async Task Upload_MissingRequest_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await UploadAsync(admin, Guid.NewGuid(), PdfBytes(), "x.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("employee_service_request.not_found", await ErrorCodeAsync(res));
    }

    // ===== 23) المالك ينزّل بعد الرفع ⇒ 200 PDF =====
    [Fact]
    public async Task Owner_Downloads_After_Upload_200()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "خطاب");
        await UploadAsync(admin, created.Id, PdfBytes(), "letter.pdf", "application/pdf");

        var res = await emp.Client.GetAsync($"/api/employee-service-requests/{created.Id}/final-document");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);
        Assert.True((await res.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    // ===== 24) HR/Admin ينزّل ⇒ 200 =====
    [Fact]
    public async Task Hr_Downloads_200()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "خطاب");
        await UploadAsync(admin, created.Id, PdfBytes(), "letter.pdf", "application/pdf");

        var res = await admin.GetAsync($"/api/employee-service-requests/{created.Id}/final-document");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 25) موظّف آخر لا ينزّل ⇒ 403 =====
    [Fact]
    public async Task Other_Employee_Cannot_Download_403()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var other = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "خطاب");
        await UploadAsync(admin, created.Id, PdfBytes(), "letter.pdf", "application/pdf");

        var res = await other.Client.GetAsync($"/api/employee-service-requests/{created.Id}/final-document");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ===== 26) تنزيل بلا ملف ⇒ 404 =====
    [Fact]
    public async Task Download_NoFile_404()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(emp.Client, "خطاب بلا ملف");

        var res = await emp.Client.GetAsync($"/api/employee-service-requests/{created.Id}/final-document");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("employee_service_request.final_document.not_found", await ErrorCodeAsync(res));
    }

    // ===== 27) تنزيل لطلب غير موجود ⇒ 404 =====
    [Fact]
    public async Task Download_MissingRequest_404()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.GetAsync($"/api/employee-service-requests/{Guid.NewGuid()}/final-document");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("employee_service_request.not_found", await ErrorCodeAsync(res));
    }

    // ===== 28) لا يتسرّب المسار الداخلي في أي استجابة =====
    [Fact]
    public async Task Response_Never_Leaks_Internal_Path()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "خطاب");
        var up = await UploadAsync(admin, created.Id, PdfBytes(), "letter.pdf", "application/pdf");
        var upBody = await up.Content.ReadAsStringAsync();

        var byId = await admin.GetAsync($"/api/employee-service-requests/{created.Id}");
        var idBody = await byId.Content.ReadAsStringAsync();

        foreach (var body in new[] { upBody, idBody })
        {
            Assert.DoesNotContain("final-documents", body);
            Assert.DoesNotContain("App_Data", body);
            Assert.DoesNotContain("HrAttachmentPath", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hrAttachmentPath", body);
        }
    }

    // ===== 29) لا يمكن الاستبدال بعد الإكمال ⇒ 409 =====
    [Fact]
    public async Task Cannot_Replace_After_Completed_409()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "خطاب");
        await UploadAsync(admin, created.Id, PdfBytes(), "first.pdf", "application/pdf");
        await admin.PostAsJsonAsync($"/api/employee-service-requests/{created.Id}/complete",
            new EmployeeServiceRequestCompleteRequest("تم"), TestJson.Options);

        var res = await UploadAsync(admin, created.Id, PdfBytes(), "second.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("employee_service_request.final_document_locked.conflict", await ErrorCodeAsync(res));
    }

    // ===== 30) يمكن الاستبدال قبل الإكمال =====
    [Fact]
    public async Task Can_Replace_Before_Completed()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "خطاب");
        await UploadAsync(admin, created.Id, PdfBytes(), "first.pdf", "application/pdf");

        var res = await UploadAsync(admin, created.Id, PdfBytes(), "second.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("second.pdf", (await res.ReadAsync<EmployeeServiceRequestDto>())!.FinalDocumentFileName);
    }

    // ===== 31) الرفع يكتب حدثًا في الخطّ الزمني =====
    [Fact]
    public async Task Upload_Writes_Timeline_Event()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "خطاب");
        await UploadAsync(admin, created.Id, PdfBytes(), "letter.pdf", "application/pdf");

        var dto = (await (await admin.GetAsync($"/api/employee-service-requests/{created.Id}"))
            .ReadAsync<EmployeeServiceRequestDto>())!;
        Assert.Contains(dto.Timeline, e => e.Action == "final_document_uploaded");
    }

    // ===== 32) مسار الإكمال غير مكسور — الرفع ثم الإكمال ثم التنزيل =====
    [Fact]
    public async Task Complete_Flow_Unbroken_With_Final_Document()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await CreateOkAsync(emp.Client, "خطاب");
        await UploadAsync(admin, created.Id, PdfBytes(), "letter.pdf", "application/pdf");

        var complete = await admin.PostAsJsonAsync($"/api/employee-service-requests/{created.Id}/complete",
            new EmployeeServiceRequestCompleteRequest("تم الإصدار"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var dto = (await complete.ReadAsync<EmployeeServiceRequestDto>())!;
        Assert.Equal(EmployeeServiceRequestStatus.Completed, dto.Status);
        Assert.True(dto.HasFinalDocument);

        var dl = await emp.Client.GetAsync($"/api/employee-service-requests/{created.Id}/final-document");
        Assert.Equal(HttpStatusCode.OK, dl.StatusCode);
    }

    // ===== 33) غير المصادَق لا يرفع ولا ينزّل ⇒ 401 =====
    [Fact]
    public async Task Anonymous_Upload_Download_401()
    {
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var created = await CreateOkAsync(emp.Client, "خطاب");
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await UploadAsync(anon, created.Id, PdfBytes(), "x.pdf", "application/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync($"/api/employee-service-requests/{created.Id}/final-document")).StatusCode);
    }
}
