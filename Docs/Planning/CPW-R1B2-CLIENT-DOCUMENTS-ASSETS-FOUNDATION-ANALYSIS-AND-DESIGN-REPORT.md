# CPW-R1B2 — CLIENT DOCUMENTS & ASSETS FOUNDATION
## تقرير التحليل والتصميم (Analysis & Design Only — بلا كود وبلا تنفيذ)

- **التاريخ**: 2026-08-06
- **النطاق**: ملفات **العميل** فقط (Client-level) داخل Client 360 — **ليست ملفات المشروع**.
- **الحالة**: تحليل وتصميم قراءة-فقط. **صفر كود، صفر Migration، صفر Deploy/Restart/Commit/Push، صفر مساس بـProduction أو RC أو TEST.**
- **المراجع الحاكمة**: BRD الفصول 04 و06 و07 و13 (المقاطع المقتبَسة موثَّقة أدناه بأرقامها).

---

## §0 — الملخّص التنفيذيّ

النظام اليوم **لا يملك منظومة ملفات**؛ يملك **مرفَقًا واحدًا** (مستند HR النهائيّ) مبنيًّا بشكل نقطيّ: مسار تخزين واحد في الإعدادات، نقطة رفع واحدة، نقطة تنزيل واحدة، تحقّق PDF ضعيف، وبلا أيّ تجريد تخزين (`IFileStorage`) وبلا نسخ (Versions) وبلا فحص برمجيّات خبيثة وبلا Checksum.

في المقابل، **Client 360 (CPW-R1B) يوفّر أساسًا ممتازًا وجاهزًا للامتداد**: نمط تفويض بالمورد (Resource-Based) مُثبَت، وحُرّاس منع الأسرار `ClientFieldGuards` جاهزون حرفيًّا لمتطلّب «ممنوع تخزين Credentials»، وكتالوج رموز قابل للتوسعة `ClientCodeConstants` جاهز حرفيًّا لمتطلّب «تصنيفات قابلة للتوسعة»، ونمط الحذف الناعم + Global Query Filter مُثبَت في `ADMIN-GOVERNANCE-R1` جاهز حرفيًّا لمتطلّب Tombstone الذي يفرضه BRD §4.40.

**الحكم النهائيّ: `CONDITIONAL GO` للتنفيذ** — التصميم مكتمل ومتوافق رجعيًّا وإضافيّ بالكامل، والشرطان الوحيدان هما قرار مالك المنتج بشأن (أ) غياب محرّك فحص الفيروسات فعليًّا، و(ب) الاكتفاء بالتنزيل عبر نقطة مُصادَقة بدل الروابط الموقَّعة قصيرة العمر. التفصيل في §15.

---

## §1 — الحالة الحاليّة (Current State) — مُثبَتة من الكود

### 1.1 التخزين والإعدادات

| العنصر | الواقع في الكود | الملفّ |
|---|---|---|
| تجريد تخزين (`IFileStorage`/Blob) | **غير موجود إطلاقًا** | — |
| إعدادات التخزين | مفتاح **واحد** فقط: `EmployeeServiceFinalDocumentsPath` | `Reporting.Application/Common/FileStorageOptions.cs` |
| التسجيل | `services.Configure<FileStorageOptions>(...)` | `Reporting.Infrastructure/DependencyInjection.cs:56` |
| المجلّد الفعليّ | fallback إلى `ContentRoot/App_Data/employee-service-requests/final-documents` | `EmployeeServiceRequestService.cs:56-60` |
| خارج جذر الويب | **نعم** — لا `UseStaticFiles`، ولا `wwwroot` في المشروع | `Reporting.Api/Program.cs` |
| حدّ حجم عامّ (`MultipartBodyLengthLimit` / Kestrel `MaxRequestBodySize`) | **غير مضبوط إطلاقًا** (صفر تطابق في كامل الـbackend) | — |
| حدّ حجم موضعيّ | `[RequestSizeLimit(12 * 1024 * 1024)]` على نقطة واحدة فقط | `EmployeeServiceRequestsController.cs:71` |

### 1.2 الرفع والتنزيل الموجودان (الوحيدان في النظام)

- **الرفع**: `POST /api/employee-service-requests/{id}/final-document` — محروس بـ`Policies.HrRequestManagement`، يستقبل `IFormFile`، ويمرّر `FinalDocumentUpload(FileName, ContentType, Length, Stream)`.
- **التنزيل**: `GET /api/employee-service-requests/{id}/final-document` — يُرجِع `PhysicalFile(...)` بعد تفويض داخل الخدمة. **لا static serving** ⇒ متوافق سلفًا مع قاعدة BRD الفصل 13 رقم 18: «كل File Access Server-side».
- **التحقّق الكامل من النوع** = دالّة واحدة (`EmployeeServiceRequestService.cs:325-332`) تجمع بين الامتداد ونوع المحتوى بـ**OR** لا AND، **بلا فحص magic-number**.

### 1.3 بيانات وصف الملفّ الموجودة اليوم

على `EmployeeServiceRequest` فقط: `HrAttachmentPath`، `HrAttachmentOriginalFileName(260)`، `HrAttachmentContentType(100)`، `HrAttachmentSizeBytes(bigint)`، `HrAttachmentUploadedAtUtc`، `HrAttachmentUploadedByUserId` (هجرة `20260624075349_EmployeeServiceFinalDocumentMetadata`).
**لا Checksum، لا StorageKey، لا VersionNo، لا VisibilityLevel، لا MalwareScanStatus، لا QuarantineStatus.**
كما يوجد حقل قديم ضعيف `AttachmentPath` يُملأ **من نصّ حرّ يرسله العميل** (`EmployeeServiceRequestService.cs:127`) — لا يُعاد استعماله في التصميم الجديد.

### 1.4 نقاط الامتداد في Client 360 (CPW-R1B)

- الكيانات: `Client`, `ClientContact`, `ClientDigitalChannel`, `ClientBrandProfile`, `Project` تحت `Reporting.Domain/Entities/Clients/`.
- الإعداد كلّه في ملفّ واحد `ClientConfigurations.cs`: `Cascade` من Client إلى الأبناء، و**`Restrict` من Client إلى Projects**، وفهرس فريد جزئيّ `IX_client_contacts_ClientId_ActivePrimary` بفلتر `"IsPrimary" = true AND "IsActive" = true`، وتحويل الـenums بـ`HasConversion<string>()`.
- التفويض: `ClientProjectAccess.ResolveAsync` يُرجِع `ClientProjectVisibility(SeesAll, ProjectIds, ClientIds)` وفيه `CanViewClient(Guid)`؛ والحارس القانونيّ المتكرّر:
  `if (!vis.CanViewClient(id)) return Result.Failure("هذا العميل خارج نطاق صلاحيتك.", "auth.forbidden");`
- **سابقة معماريّة حاسمة**: `ClientContactsController` و`ClientBrandController` و`ClientDigitalChannelsController` **بلا أيّ `[Authorize(Policy=...)]`** — التفويض بالمورد داخل الخدمة حصرًا. هذا هو النمط الذي يسمح لمدير الحساب بالكتابة **بلا توسيع أيّ دور أمنيّ**.
- الواجهة: `ClientDetailPage.tsx:62-70` تعرّف `TabKey` وستّة تبويبات؛ والسطر 98 يعرّف صلاحية كتابة الأبناء:
  `const canWriteChildren = canEditClientCore || (!!user && user.userId === c.accountManagerId);`

### 1.5 الأصول القابلة لإعادة الاستعمال **حرفيًّا**

| الأصل | الملفّ | كيف يُعاد استعماله |
|---|---|---|
| `ClientFieldGuards.ContainsSecret/AnyContainsSecret` (26 مصطلحًا محظورًا) + `IsValidUrl` (http/https فقط) | `Application/Clients/ClientFieldGuards.cs` | يفي **حرفيًّا** بمتطلّب «ممنوع تخزين Passwords/Tokens/API Keys/Cookies/Secrets» على حقول الوصف/الملاحظات/الوسوم/الروابط |
| `ClientCodeConstants` (كتالوج رموز نصّيّة قابل للتوسعة بلا جدول) | `Application/Clients/ClientCodeConstants.cs` | النمط ذاته لتصنيفات المستندات ودرجات الرؤية وأنواع الروابط ⇒ «تصنيفات قابلة للتوسعة» بإضافة سطر واحد |
| `AccessStatuses = {FullAccess, PartialAccess, Requested, Pending, NoAccess, Revoked}` | نفس الملفّ | يفي حرفيًّا بمتطلّب **Access Status** لتبويب الروابط |
| نمط الحذف الناعم + `HasQueryFilter(x => !x.IsDeleted)` + فهرس فريد جزئيّ `HasFilter("\"IsDeleted\" = false")` | `SubmissionConfigurations.cs:18-31`، `KpiConfigurations.cs:68` | يفي حرفيًّا بـ**Tombstone** الذي يفرضه BRD §4.40 |
| `IAuditService.LogAsync(actorId, action, entityType, entityId, dataJson, ip, ct)` + `audit_logs.DataJson` من نوع `jsonb` | `Application/Audit/IAuditService.cs`، `SystemConfigurations.cs:101` | تدقيق الرفع/التعديل/الحذف/**التنزيل** بلا جدول جديد |
| `ApiControllerBase.FromResult/ToProblem` (لواحق `.not_found`→404، `.conflict`→409، `auth.forbidden`→403) | `Api/Controllers/ApiControllerBase.cs` | كامل خريطة أكواد الخطأ الجديدة بلا كود إضافيّ |
| `downloadFile(path, filename)` (blob + Authorization header) | `reporting-frontend/src/lib/api.ts:65-76` | تنزيل مُصادَق من الواجهة بلا رابط عامّ |
| نموذج الرفع بـ`FormData` + `multipart/form-data` + حُرّاس نوع/حجم في العميل | `pages/HrRequestsPage.tsx:394-415, 545-549` | مرجع بصريّ ووظيفيّ لرافع مستندات العميل |
| `PhysicalFile(...)` بعد تفويض في الخدمة | `EmployeeServiceRequestsController.cs:83-91` | نمط التنزيل الآمن المعتمَد |
| أعمدة `jsonb` عبر `HasColumnType("jsonb")` | `SubmissionConfigurations.cs:41` وغيره | تخزين الوسوم/الكلمات المفتاحيّة مع فهرس GIN |

### 1.6 رأس الهجرات

```
20260709222126_AddProjectWorkstreams
20260709231845_AddWorkstreamDeliverables
20260712211952_AddClient360Foundation
20260713171040_AdminGovernanceReportKpiCorrection   ← رأس المستودع
```
**ملاحظة بيئيّة مُثبَتة**: بيئة TEST/UAT تقف عند 31 هجرة رأسها `20260712211952_AddClient360Foundation` ⇒ **TEST متأخّرة بهجرة واحدة عن المستودع، وليست في حالة تعارض**. هذا يجب تسويته قبل أيّ نشر لاحق (§11).

---

## §2 — ما يفرضه BRD (المرجع الحاكم)

### 2.1 الفصل 04 — §4.38 معمارية الملفات والتخزين
> «يعامل النظام الملف كأصل مُدار، وليس مجرد مسار نصي. ويجب الفصل بين Metadata في قاعدة البيانات وBlob فعلي في Storage مستقل لكل بيئة.»

- **§4.38.1 — بيانات الوصف الإلزاميّة**: `FileId، StorageKey، OriginalName، MimeType، SizeBytes، Checksum، UploadedBy، UploadedAt` + `ParentEntityType/ParentEntityId` + `VisibilityLevel` + تصنيف الوثيقة + حالة الاعتماد + رقم الإصدار + **`MalwareScanStatus` و`QuarantineStatus` قبل الإتاحة** + **«عدم إرجاع المسار الفيزيائي أو سر التخزين إلى العميل»**.
- **§4.38.2 — قواعد البيئات**: Storage مستقلّ لكلّ من Production/RC/TEST؛ النسخ من Production إلى RC يكون Clone مستقلًّا؛ الروابط الموقَّعة قصيرة العمر؛ **«النسخ الاحتياطي للـDB لا يكفي دون Storage Manifest ونسخة ملفات»**.
- **§4.38 جدول المستويات**: مستوى **Client** = «هوية عامة، مستندات تعريفية» — الرؤية **AM/Management**، والإصدارات **نعم**. ومستوى **Commercial** = «العقد والعرض المالي» — الرؤية **Finance/Management فقط**.

### 2.2 الفصل 04 — §4.15 كيان Project Document (النموذج المرجعيّ)
حساسية **مرتفعة**. الحالات: `Draft, Current, Superseded, Archived`. القواعد: **«كل رفع جديد لنفس الوثيقة ينشئ Version»**؛ «العقود والتكلفة لا تظهر لفريق التنفيذ»؛ «الحذف الفيزيائي فقط وفق Retention Policy وبعد الأرشفة».

### 2.3 الفصل 04 — §4.14 كيان Link
حالات Active/Inactive؛ الحقول: الاسم، النوع، الرابط، الوصف، درجة الرؤية، تاريخ الإضافة. القواعد: **«ممنوع تخزين كلمات المرور في الرابط أو الوصف»**؛ يمكن تقييد الرابط على فريق أو دور؛ **«الرابط المكسور يُعلَّم دون حذف التاريخ»**؛ **التعطيل بدل الحذف**.

### 2.4 الفصل 04 — §4.39 إلى §4.43
- Versioning يحفظ `VersionNo, ParentVersion, Snapshot, PublishedAt`.
- «لا يُسجل PasswordHash أو Token أو Connection String أو محتوى مالي حساس كاملًا داخل Audit».
- **سياسة حذف الملفّات = Retention + Tombstone + Delayed Physical Delete** (السبب المُعلَن: منع كسر النسخ والتقارير).
- **`Restrict` هو الافتراضي بين Domains المختلفة**؛ Cascade للمسودات غير المستخدمة فقط؛ Soft Delete يتطلّب `includeInactive` صريحًا في كلّ استعلام.
- Text Search: Index/**GIN** حسب PostgreSQL لحقول البحث مع حدود.
- **§4.42 Document Search**: البحث على الاسم والتصنيف وMetadata؛ الفلاتر على المشروع والرؤية والإصدار؛ والقاعدة: **«لا يجوز إرجاع اسم كيان غير مصرح به ثم منعه عند الفتح فقط»**.
- §4.43: حدث `FileApproved` ⇒ ترقية النسخة إلى Final.

### 2.5 الفصل 06 و07
- §6.15 مساحة العميل: التبويبات = Overview, Contacts, Opportunities, Projects, Contracts, Activities, **Files (ملفات عميل عامة — مصنفة)**, Timeline.
- §6.10: «رفع العقد بواسطة المبيعات لا يعني ظهوره تلقائيًا داخل ملفات المشروع».
- §7.1: «File = أصل له نوع وصلاحية وإصدار» لا «Attachment بلا سياق».
- §7.5 فئات الملفّات ورؤيتها: Project Reference / **Brand Assets** / **Credentials (مقيّد جدًّا)** / **Commercial Summary (AM/Management)** / Deliverable Drafts / Final Approved / **Contract Files (Finance/Management فقط)**.
- §7.5 دورة إصدار الملفّ: `Draft → Submitted → Revision Requested → Approved Internal → Client Review → Final Approved → Superseded`.
- §7.17 قاعدة 11: «الملفات النهائية لا تستبدل دون Version». §7.19 معيار 11: «تظهر الملفات بإصدارات وحالات».

### 2.6 الفصل 13
- معمارية التبويبات: **Client → Overview, Contacts, Projects, Files, Timeline** ⇒ **تبويب الملفّات مُقرّ في BRD**.
- §13.24: Files = **Signed Access**؛ Secrets = **Secret Store**؛ Scope = Server-side Filtering.
- §13.26: Files = **Streaming**. §13.27: **Object Storage** للملفّات (هدف التوسّع).
- §13.31: Backup للـDB والـBackend والـdist وConfig snapshot دون كشف Secrets + Rollback مُختبَر + Smoke بعد النشر.
- §13.34: تسمية الملفّات ⇒ **Safe IDs**. القاعدة 18: **«كل File Access Server-side»**.

---

## §3 — تحليل الفجوات (Gap Analysis)

| # | المتطلّب (التذكرة/BRD) | الموجود | الفجوة | الشدّة |
|---|---|---|---|---|
| G-01 | فصل Metadata عن Blob وتجريد تخزين قابل للتحوّل إلى Object Storage (§4.38، §13.27) | لا شيء — استدعاءات نظام ملفّات مباشرة | **`IFileStorage` غير موجود** | **حرجة** |
| G-02 | Version History بلا حذف القديم (§4.15.2، §7.17) | لا شيء — الرفع الجديد يستبدل | **لا نموذج نسخ إطلاقًا** | **حرجة** |
| G-03 | `Checksum` إلزاميّ + كشف التكرار (§4.38.1) | لا شيء | **لا Checksum ولا Duplicate Detection** | **حرجة** |
| G-04 | `MalwareScanStatus` + `QuarantineStatus` قبل الإتاحة (§4.38.1) | صفر تطابق لـ virus/antivirus/clamav/malware/scan | **لا فحص ولا حقول ولا Hook** | **حرجة** |
| G-05 | تحقّق Mime + Extension + Size متعدّد الأنواع | دالّة PDF واحدة بـOR بلا magic-number | **تحقّق ضعيف وغير قابل للتوسعة** | عالية |
| G-06 | حدّ حجم **قابل للضبط بالإعدادات** | `[RequestSizeLimit(12MB)]` مُصلَّب على نقطة واحدة؛ لا حدّ عامّ | **لا إعداد حجم** | عالية |
| G-07 | تصنيفات قابلة للتوسعة (14 فئة) | لا شيء لملفّات العميل | جديد — لكن النمط جاهز (`ClientCodeConstants`) | متوسّطة |
| G-08 | `VisibilityLevel` وفصل Commercial عن فريق التنفيذ (§4.38، §7.5، §4.15.2) | لا شيء | **لا درجات رؤية للملفّات** | عالية |
| G-09 | Tombstone + Retention + Delayed Physical Delete (§4.40) | نمط الحذف الناعم موجود لكن **ليس للملفّات** | يُنقَل النمط كما هو | متوسّطة |
| G-10 | تبويب External Links مع Access Status/Vault Reference/Owner/Last Verified (§4.14) | `ClientDigitalChannel` يغطّي المنصّات الإعلانيّة فقط لا الروابط العامّة | **لا كيان روابط عامّ** | عالية |
| G-11 | منع تخزين أيّ Credential | **`ClientFieldGuards` موجود وجاهز** | لا فجوة — يُعاد استعماله | — |
| G-12 | بحث وفلترة (اسم/تصنيف/Metadata/وسوم) مع GIN (§4.41، §4.42) | لا فهارس نصّيّة ولا GIN في المشروع | جديد بالكامل | متوسّطة |
| G-13 | Signed Access قصير العمر (§13.24، §4.38.2) | تنزيل عبر نقطة مُصادَقة (متوافق مع القاعدة 18) لكن **بلا روابط موقَّعة** | فجوة مُعلَنة — تُؤجَّل بقرار | متوسّطة |
| G-14 | Storage مستقلّ لكلّ بيئة + Storage Manifest في النسخ الاحتياطيّ (§4.38.2) | مسار واحد بلا Manifest؛ إجراءات النسخ الحاليّة تغطّي DB/publish/dist فقط | **النسخ الاحتياطيّ لا يشمل الملفّات** | عالية |
| G-15 | دور Account Manager مستقلّ في مصفوفة الصلاحيات | **غير موجود** (مُثبَت في UAT-R1)؛ `AccountPortfolioReader` رؤية-فقط بالتصميم؛ `ClientCoreManagers` بلا TeamLeader | يُعالَج بالتفويض بالمورد لا بدور جديد | متوسّطة |
| G-16 | `FieldType.FileUpload=15` و`Image=16` معرَّفان ولم يُنفَّذا (`Enums.cs:45-72`) | تناقض قائم | خارج النطاق — يُسجَّل فقط | منخفضة |

---

## §4 — التصميم الموصى به (Recommended Design)

### 4.1 المبادئ الحاكمة
1. **Additive Only**: ثلاثة جداول جديدة، وتوسعة `FileStorageOptions` بمفاتيح اختياريّة لها قيم افتراضيّة. **صفر ALTER/DROP على جدول قائم، صفر تغيير في سلوك قائم.**
2. **فصل Metadata عن Blob**: القاعدة تحمل الوصف، والقرص يحمل المحتوى، والوصل بينهما `StorageKey` فقط.
3. **`StorageKey` لا يُسرَّب أبدًا** إلى الواجهة (تنفيذًا لـ§4.38.1) — لا في DTO ولا في رسالة خطأ ولا في التدقيق.
4. **Safe IDs** (§13.34): اسم الملفّ على القرص = GUID + امتداد مُطهَّر؛ الاسم الأصليّ يبقى في القاعدة فقط.
5. **الرفع لا يستبدل**: كلّ رفع لنفس المستند = نسخة جديدة، والقديمة تصير `Superseded` ولا تُحذف.
6. **الحذف = Tombstone** (§4.40): علم ناعم + فلتر عالميّ؛ الحذف الفيزيائيّ مؤجَّل لسياسة احتفاظ خارج نطاق V1.
7. **التفويض بالمورد** لا بتوسيع الأدوار: صفر دور جديد، صفر تعديل على `Roles.All`.
8. **`IFileStorage` من اليوم الأوّل** حتى مع تنفيذ محلّيّ واحد، كي يكون التحوّل إلى Object Storage (§13.27) تبديل تنفيذ لا إعادة كتابة.

### 4.2 تجريد التخزين

- `IFileStorage` (في `Reporting.Application/Common/`) بعمليّات: حفظ من Stream ⇒ `StorageKey`، فتح Stream للقراءة، حذف، فحص وجود. **بلا أيّ معرفة بالمسار الفيزيائيّ خارج التنفيذ.**
- `LocalFileStorage` (في `Reporting.Infrastructure/Storage/`) — التنفيذ الوحيد في V1، يكتب تحت الجذر المُعرَّف بالإعدادات وخارج جذر الويب.
- **تخطيط المفتاح**: `clients/{ClientId}/{DocumentId}/{VersionId}{SafeExtension}` — كلّه GUIDs، بلا أيّ اسم مستخدَم أو اسم ملفّ أصليّ على القرص (منع Path Traversal بنيويًّا لأن المكوّنات لا تأتي من المستخدِم إطلاقًا).
- **حاجز مسار إلزاميّ**: قبل أيّ فتح Stream يُتحقَّق أن المسار المُطلَق الناتج يقع تحت الجذر المُهيَّأ، وإلا يُرفَض.

### 4.3 توسعة `FileStorageOptions` (إضافيّة بحتة، بقيم افتراضيّة آمنة)

| المفتاح | الافتراضيّ | الغرض |
|---|---|---|
| `ClientDocumentsPath` | `ContentRoot/App_Data/client-documents` | جذر تخزين مستندات العميل (مستقلّ لكلّ بيئة — §4.38.2) |
| `MaxUploadSizeBytes` | `26214400` (25MB) | حدّ الحجم **القابل للضبط** (يعالج G-06) |
| `MaxVideoUploadSizeBytes` | `209715200` (200MB) | حدّ منفصل لـMP4 لأن 25MB غير واقعيّ للفيديو |
| `AllowedExtensions` | القائمة في §4.5 | قائمة سماح للامتدادات |
| `AllowedMimeTypes` | القائمة في §4.5 | قائمة سماح لأنواع المحتوى |
| `RequireCleanScanBeforeDownload` | `false` | مفتاح تفعيل بوّابة الفحص عند توفّر محرّك (يعالج G-04 هيكليًّا) |
| `ScanEngine` | `None` | `None` في V1؛ نقطة الامتداد للمحرّك |

> المفاتيح القائمة (`EmployeeServiceFinalDocumentsPath`) **لا تُمَسّ إطلاقًا** ⇒ توافق رجعيّ تامّ مع ملفّات البيئة الثلاثة الحاليّة.

### 4.4 التصنيفات (قابلة للتوسعة — G-07)

كتالوج نصّيّ على نمط `ClientCodeConstants` (لا enum، لا جدول) في `ClientDocumentCodeConstants`:

`Proposal, Contract, MarketingPlan, BrandGuideline, BrandAssets, Logo, CreativeAssets, MediaKit, Research, MeetingMinutes, Presentation, InvoiceReference, PurchaseOrder, Other`

**التوسعة = إضافة سطر نصّيّ واحد، بلا Migration وبلا نشر قاعدة.** التسميات العربيّة تُضاف في `reporting-frontend/src/lib/format.ts` على نمط `clientStatusLabel`.

**مواءمة مع فئات BRD الفصل 07 §7.5** (الفئات هناك على مستوى المشروع؛ هنا تُسقَط على مستوى العميل):

| فئة BRD §7.5 | المقابل في هذا التصميم | الرؤية الافتراضيّة |
|---|---|---|
| Brand Assets | `BrandGuideline`, `BrandAssets`, `Logo`, `CreativeAssets`, `MediaKit` | `Internal` |
| Commercial Summary | `Proposal`, `MarketingPlan` | `Management` |
| Contract Files | `Contract`, `InvoiceReference`, `PurchaseOrder` | **`Financial`** |
| Project Reference | `Research`, `MeetingMinutes`, `Presentation` | `Internal` |
| **Credentials** | **لا مقابل — ممنوع بنيويًّا** (يُمنَع بـ`ClientFieldGuards` ويُوجَّه إلى تبويب الروابط بمرجع خزنة) | — |

### 4.5 الأنواع المسموحة

| الامتداد | نوع المحتوى | الحدّ |
|---|---|---|
| `.pdf` | `application/pdf` | `MaxUploadSizeBytes` |
| `.docx` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | `MaxUploadSizeBytes` |
| `.xlsx` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | `MaxUploadSizeBytes` |
| `.pptx` | `application/vnd.openxmlformats-officedocument.presentationml.presentation` | `MaxUploadSizeBytes` |
| `.zip` | `application/zip`, `application/x-zip-compressed` | `MaxUploadSizeBytes` |
| `.png` | `image/png` | `MaxUploadSizeBytes` |
| `.jpg` / `.jpeg` | `image/jpeg` | `MaxUploadSizeBytes` |
| `.svg` | `image/svg+xml` | `MaxUploadSizeBytes` |
| `.csv` | `text/csv` | `MaxUploadSizeBytes` |
| `.mp4` | `video/mp4` | `MaxVideoUploadSizeBytes` |

**قواعد التحقّق (تعالج G-05)** — الثلاثة معًا بـ**AND** لا OR:
1. الامتداد ضمن قائمة السماح **و**
2. نوع المحتوى ضمن قائمة السماح **و** متّسق مع الامتداد **و**
3. **بصمة البايتات الأولى (magic number)** مطابقة للنوع المُعلَن: `%PDF` لـpdf، `PK\x03\x04` لـdocx/xlsx/pptx/zip، `\x89PNG` لـpng، `\xFF\xD8\xFF` لـjpg، `ftyp` عند الإزاحة 4 لـmp4.

**معالجة خاصّة لـ`.svg`**: SVG نصّ XML قابل لتضمين `<script>` ⇒ **يُخزَّن ويُنزَّل فقط** بـ`Content-Disposition: attachment` و`Content-Type: application/octet-stream`، **ولا يُعرَض inline إطلاقًا** (رأس `X-Content-Type-Options: nosniff` مضبوط سلفًا في `Program.cs:220-226`).

### 4.6 دورة حياة المستند والنسخة

- **حالة المستند** (§4.15.1): `Draft` ⟶ `Current` ⟶ `Superseded` ⟶ `Archived`.
- **حالة النسخة** في V1: نسخة واحدة `IsCurrent=true` لكلّ مستند حيّ؛ الباقي `Superseded`. **`VersionNo` تسلسليّ متزايد لكلّ مستند**.
- **دورة الاعتماد السباعيّة** الواردة في §7.5 (`Submitted → Revision Requested → Approved Internal → Client Review → Final Approved`) هي **دورة مستوى المشروع** ⇒ **خارج نطاق V1 صراحةً** وتُؤجَّل إلى Project Workspace، مع إبقاء عمود `ApprovalStatusCode` في النموذج (قيمته الافتراضيّة `NotApplicable`) كي لا يلزم تعديل سكيمة لاحقًا.
- **الاستعادة**: «استعادة نسخة قديمة» = **إنشاء نسخة جديدة برقم أعلى تحمل نفس `StorageKey` وChecksum**، لا استبدال ولا حذف — تنفيذًا لـ«الملفات النهائية لا تستبدل دون Version».

### 4.7 كشف التكرار (G-03)
- `Checksum` = SHA-256 يُحسَب على تيّار الرفع.
- **تكرار داخل نفس المستند**: نفس Checksum = آخر نسخة ⇒ **يُرفَض** بـ`client_document.duplicate_version.conflict` (409) لمنع نسخ عديمة الجدوى.
- **تكرار داخل نفس العميل عبر مستندات مختلفة**: **لا يُرفَض** بل يُرجَع تحذير في الاستجابة مع معرّف المستند المطابق (قد يكون مقصودًا — نفس الشعار في فئتين).

### 4.8 الروابط الخارجيّة (G-10)
كيان مستقلّ تمامًا عن `ClientDigitalChannel` (ذاك خاصّ بالمنصّات الإعلانيّة ومعرّفاتها). أنواع الروابط قابلة للتوسعة:
`GoogleDrive, Notion, Figma, Miro, Dropbox, SharePoint, Website, Folder, Other`.

**بدلاً من أيّ Credential** (تنفيذًا لـ§4.14.2 و§13.24 «Secrets = Secret Store»):
- `AccessStatusCode` — يُعاد استعمال `ClientCodeConstants.AccessStatuses` حرفيًّا.
- `VaultReference` — **مؤشِّر نصّيّ إلى خزنة خارجيّة فقط** (مثل اسم إدخال في مدير كلمات المرور)، **ويمرّ إجباريًّا عبر `ClientFieldGuards.ContainsSecret`** فيُرفَض إن بدا سرًّا.
- `OwnerUserId` — المسؤول الداخليّ عن الوصول.
- `LastVerifiedAtUtc` + `LastVerifiedByUserId` + `IsBroken` — «الرابط المكسور يُعلَّم دون حذف التاريخ» (§4.14.2).
- `IsActive` — **التعطيل بدل الحذف** (§4.14.2).

---

## §5 — نموذج قاعدة البيانات (Database Model)

### 5.1 جدول `client_documents` — رأس المستند (Metadata)

| العمود | النوع | قيود | ملاحظات |
|---|---|---|---|
| `Id` | uuid | PK | `BaseEntity` |
| `ClientId` | uuid | **FK → `clients.Id`، `Restrict`** | §4.40.1: Restrict بين Domains |
| `Title` | varchar(300) | NOT NULL | Document Name |
| `CategoryCode` | varchar(100) | NOT NULL، مُتحقَّق بالكتالوج | قابل للتوسعة |
| `Description` | varchar(2000) | NULL، **يمرّ بحارس الأسرار** | |
| `Notes` | varchar(2000) | NULL، **يمرّ بحارس الأسرار** | |
| `TagsJson` | **jsonb** | NULL | مصفوفة وسوم/كلمات مفتاحيّة |
| `StatusCode` | varchar(20) | NOT NULL، افتراضيّ `Current` | Draft/Current/Superseded/Archived |
| `VisibilityCode` | varchar(20) | NOT NULL، افتراضيّ `Internal` | Internal/Management/Financial/Restricted |
| `ApprovalStatusCode` | varchar(30) | NOT NULL، افتراضيّ `NotApplicable` | حجز لدورة §7.5 المؤجَّلة |
| `CurrentVersionId` | uuid | NULL، **FK → `client_document_versions.Id`، `Restrict`** | مؤشّر النسخة الحاليّة |
| `CurrentVersionNo` | int | NOT NULL، افتراضيّ 0 | مُزال-التطبيع للقوائم السريعة |
| `ExpirationDate` | date | NULL | Metadata مطلوب في التذكرة |
| `ReviewDate` | date | NULL | Metadata مطلوب في التذكرة |
| `CreatedByUserId` | uuid | NOT NULL | Created By |
| `UpdatedByUserId` | uuid | NULL | Last Updated By |
| `IsDeleted` | bool | NOT NULL، افتراضيّ false | **Tombstone** |
| `DeletedAtUtc` | timestamptz | NULL | |
| `DeletedByUserId` | uuid | NULL | |
| `DeleteReason` | varchar(1000) | NULL | سبب إلزاميّ عند الحذف (نمط ADMIN-GOVERNANCE-R1) |
| `CreatedAtUtc` / `UpdatedAtUtc` | timestamptz | من `BaseEntity` | |

**الفهارس**:
- `IX_client_documents_ClientId` على `(ClientId)`
- `IX_client_documents_ClientId_CategoryCode` على `(ClientId, CategoryCode)`
- `IX_client_documents_ClientId_StatusCode` على `(ClientId, StatusCode)`
- `IX_client_documents_CreatedByUserId` على `(CreatedByUserId)` — فلتر «الرافع»
- `IX_client_documents_TagsJson` **GIN** على `TagsJson` (§4.41)
- **فلتر عالميّ**: `HasQueryFilter(x => !x.IsDeleted)` — بالنمط ذاته في `SubmissionConfigurations.cs:31`

### 5.2 جدول `client_document_versions` — النسخ (Blob Metadata)

| العمود | النوع | قيود | ملاحظات |
|---|---|---|---|
| `Id` | uuid | PK | = `FileId` في §4.38.1 |
| `ClientDocumentId` | uuid | **FK → `client_documents.Id`، `Cascade`** | ابن مباشر داخل نفس الـDomain |
| `VersionNo` | int | NOT NULL | تسلسليّ من 1 |
| `ParentVersionId` | uuid | NULL، FK ذاتيّ، `Restrict` | §4.39 `ParentVersion` |
| `StorageKey` | varchar(500) | NOT NULL | **لا يُرجَع للعميل إطلاقًا** |
| `OriginalName` | varchar(300) | NOT NULL | |
| `Extension` | varchar(20) | NOT NULL | مُطهَّر |
| `MimeType` | varchar(150) | NOT NULL | |
| `SizeBytes` | bigint | NOT NULL | |
| `Checksum` | varchar(64) | NOT NULL | SHA-256 hex |
| `IsCurrent` | bool | NOT NULL | نسخة حاليّة واحدة لكلّ مستند |
| `ScanStatusCode` | varchar(20) | NOT NULL، افتراضيّ `NotScanned` | `MalwareScanStatus` (§4.38.1) |
| `QuarantineStatusCode` | varchar(20) | NOT NULL، افتراضيّ `None` | `QuarantineStatus` (§4.38.1) |
| `ScannedAtUtc` | timestamptz | NULL | |
| `ChangeNote` | varchar(1000) | NULL، **يمرّ بحارس الأسرار** | «ما الذي تغيّر في هذه النسخة» |
| `UploadedByUserId` | uuid | NOT NULL | `UploadedBy` |
| `UploadedAtUtc` | timestamptz | NOT NULL | `UploadedAt` / §4.39 `PublishedAt` |
| `IsDeleted` / `DeletedAtUtc` / `DeletedByUserId` | — | | Tombstone موروث من حذف المستند |

**الفهارس والقيود**:
- **فريد**: `IX_client_document_versions_DocId_VersionNo` على `(ClientDocumentId, VersionNo)` — يمنع تصادم الترقيم.
- **فريد جزئيّ**: `IX_client_document_versions_DocId_Current` على `(ClientDocumentId)` بفلتر `"IsCurrent" = true AND "IsDeleted" = false` — يفرض نسخة حاليّة واحدة كحدّ أقصى. **هذه بالضبط تقنية `IX_client_contacts_ClientId_ActivePrimary` القائمة.**
- `IX_client_document_versions_Checksum` على `(Checksum)` — كشف التكرار.
- `IX_client_document_versions_ClientDocumentId` على `(ClientDocumentId)`.
- **فلتر عالميّ**: `HasQueryFilter(x => !x.IsDeleted)`.

> **ملاحظة FK دائريّة**: `client_documents.CurrentVersionId → client_document_versions.Id` مع `client_document_versions.ClientDocumentId → client_documents.Id` تُشكّلان دورة. تُعالَج بجعل `CurrentVersionId` **nullable** و`Restrict`، مع إدراج المستند أوّلًا ثمّ النسخة ثمّ تحديث المؤشّر داخل **معاملة واحدة** — وهذا مدعوم في EF Core بلا أيّ حيلة. البديل الأبسط (إن رُفض الدوران) هو حذف `CurrentVersionId` والاكتفاء بـ`IsCurrent` + الفهرس الفريد الجزئيّ؛ **هذا القرار متروك لمرحلة التنفيذ ولا يغيّر أيّ سلوك خارجيّ**.

### 5.3 جدول `client_external_links` — الروابط الخارجيّة

| العمود | النوع | قيود |
|---|---|---|
| `Id` | uuid | PK |
| `ClientId` | uuid | **FK → `clients.Id`، `Cascade`** (نمط الأبناء القائم للاتّساق مع Contacts/Channels) |
| `Title` | varchar(200) | NOT NULL |
| `LinkTypeCode` | varchar(100) | NOT NULL، مُتحقَّق بالكتالوج |
| `Url` | varchar(1000) | NOT NULL، **`IsValidUrl` (http/https فقط)** |
| `Description` | varchar(2000) | NULL، **حارس الأسرار** |
| `VisibilityCode` | varchar(20) | NOT NULL، افتراضيّ `Internal` |
| `AccessStatusCode` | varchar(100) | NULL، من `ClientCodeConstants.AccessStatuses` |
| `VaultReference` | varchar(300) | NULL، **حارس الأسرار** — مؤشِّر خزنة فقط |
| `OwnerUserId` | uuid | NULL |
| `LastVerifiedAtUtc` | timestamptz | NULL |
| `LastVerifiedByUserId` | uuid | NULL |
| `IsBroken` | bool | NOT NULL، افتراضيّ false |
| `IsActive` | bool | NOT NULL، افتراضيّ true — **تعطيل بدل حذف** |
| `Notes` | varchar(2000) | NULL، **حارس الأسرار** |
| `SortOrder` | int | NOT NULL، افتراضيّ 0 |
| `CreatedByUserId` / `UpdatedByUserId` | uuid | |
| `CreatedAtUtc` / `UpdatedAtUtc` | timestamptz | `BaseEntity` |

**الفهارس**: `IX_client_external_links_ClientId`، و`IX_client_external_links_ClientId_LinkTypeCode`.
**لا حذف ناعم هنا** — التعطيل هو آليّة الإخفاء (مطابقة لـ§4.14.2 ولنمط `ClientDigitalChannel` القائم).

### 5.4 لماذا لا جدول رابع للتدقيق؟
`audit_logs` القائم (`DataJson` من نوع `jsonb`) يستوعب كلّ الأحداث. **صفر جدول تدقيق جديد.** أسماء الأحداث في §9.4.

### 5.5 استراتيجيّة التخزين (Storage Strategy)
- **V1**: قرص محلّيّ خارج جذر الويب، جذر مستقلّ لكلّ بيئة (§4.38.2).
- **V2**: تبديل تنفيذ `IFileStorage` إلى Object Storage (§13.27) — **صفر تغيير في القاعدة أو الـAPI أو الواجهة** لأن `StorageKey` غير شفّاف أصلًا.
- **النسخ الاحتياطيّ (G-14)**: نسخة الـDB **لا تكفي**. يجب لكلّ نشر: `tar` لجذر التخزين + **Storage Manifest** (جدول `StorageKey` ⟷ `Checksum` ⟷ `SizeBytes` مُصدَّر من القاعدة) + تحقّق تطابق بعد الاستعادة.

---

## §6 — الكيانات (Entities) وتوزيعها على الطبقات

| الطبقة | الملفّات الجديدة |
|---|---|
| `Reporting.Domain` | `Entities/Clients/ClientDocument.cs`، `Entities/Clients/ClientDocumentVersion.cs`، `Entities/Clients/ClientExternalLink.cs` |
| `Reporting.Application` | `Clients/IClientDocumentService.cs`، `Clients/IClientExternalLinkService.cs`، `Clients/ClientDocumentModels.cs`، `Clients/ClientDocumentCodeConstants.cs`، `Common/IFileStorage.cs` |
| `Reporting.Infrastructure` | `Services/ClientDocumentService.cs`، `Services/ClientExternalLinkService.cs`، `Storage/LocalFileStorage.cs`، `Persistence/Configurations/ClientDocumentConfigurations.cs`، هجرة واحدة |
| `Reporting.Api` | `Controllers/ClientDocumentsController.cs`، `Controllers/ClientExternalLinksController.cs` |
| Frontend | `pages/ClientDocumentsTab.tsx`، `pages/ClientLinksTab.tsx`، `lib/useClientDocuments.ts`، توسعة `types/api.ts` و`lib/format.ts` و`pages/ClientDetailPage.tsx` |

**التعديلات على ملفّات قائمة (إضافيّة فقط)**: `AppDbContext` (+3 `DbSet`)، `DependencyInjection` (+3 تسجيلات)، `FileStorageOptions` (+7 مفاتيح اختياريّة)، `AppDbContextModelSnapshot` (مُولَّد)، `ClientDetailPage.tsx` (+تبويبان)، `types/api.ts` (+DTOs)، `format.ts` (+خرائط تسميات). **صفر تعديل على `Roles.cs` أو `Program.cs`.**

---

## §7 — الصلاحيات (Permissions)

### 7.1 القاعدة الحاكمة: صفر دور جديد وصفر سياسة جديدة

التذكرة تطلب مصفوفة لثمانية أدوار، لكن **«Account Manager» ليس دورًا أمنيًّا في النظام** (مُثبَت في `UAT-ORGANIZATION-AND-ROLE-WORKFLOW-VALIDATION-R1`: `FinanceEmployee` و`SalesEmployee` و`AccountManager` المستقلّ غير موجودة). الموجود فعليًّا:
- `Roles.ClientCoreManagers = { Admin, Ceo, GeneralManager, Manager }` — **بلا TeamLeader**.
- `Roles.AccountPortfolioReaders = { AccountPortfolioReader, Admin }` — **رؤية فقط بالتصميم**.
- عمود `Client.AccountManagerId` — **هذا هو المصدر الحقيقيّ لهويّة مدير الحساب**.

لذلك تُبنى المصفوفة على **التفويض بالمورد داخل الخدمة**، بالنمط المُثبَت في `ClientContactsController`/`ClientBrandController`/`ClientDigitalChannelsController` (بلا `[Authorize(Policy=...)]` إطلاقًا) وبالمنطق ذاته الذي تعرضه الواجهة اليوم:

```
canWriteChildren = canEditClientCore || (user.userId === client.accountManagerId)
```

**النتيجة: صفر تعديل على `Roles.cs`، صفر سياسة جديدة في `Program.cs`، صفر توسيع صلاحيات لأيّ دور قائم.**

### 7.2 المصفوفة الكاملة (قراءة / رفع / تعديل / حذف / رؤية النسخ)

الرمز: ✅ مسموح — ⛔ ممنوع — 🔶 مشروط بالنطاق أو بدرجة الرؤية.

| الدور الفعليّ | قراءة القائمة | تنزيل الملفّ | رفع نسخة | تعديل Metadata | حذف (Tombstone) | رؤية سجلّ النسخ | رؤية `Financial` |
|---|---|---|---|---|---|---|---|
| `Admin` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `Ceo` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `GeneralManager` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `Manager` | 🔶 ضمن النطاق | 🔶 | ✅ ضمن النطاق | ✅ ضمن النطاق | ⛔ | ✅ | ⛔ |
| **مدير الحساب** (`Client.AccountManagerId == uid`) | ✅ لعميله | ✅ لعميله | ✅ لعميله | ✅ لعميله | ⛔ | ✅ | ✅ لعميله |
| `TeamLeader` | 🔶 ضمن النطاق | 🔶 | ⛔ | ⛔ | ⛔ | ✅ (بلا تنزيل النسخ المقيّدة) | ⛔ |
| `Employee` | 🔶 `Internal` فقط | 🔶 `Internal` فقط | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ |
| `Viewer` | 🔶 Metadata فقط | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ |
| `AccountPortfolioReader` | ✅ لمحفظته | ✅ لمحفظته | ⛔ | ⛔ | ⛔ | ✅ | 🔶 |
| `CeoSupport` | ✅ | ✅ | ⛔ | ⛔ | ⛔ | ✅ | ✅ |

**ملاحظات مُلزِمة**:
1. **الحذف حصريّ للثلاثة العلويّة** (`Admin`/`Ceo`/`GeneralManager`) وبـ**سبب إلزاميّ** (`DeleteReason`) — بالنمط ذاته الذي أقرّه `ADMIN-GOVERNANCE-R1` لحذف التسليمات.
2. **`Viewer` يرى بيانات الوصف ولا يُنزِّل** — التنزيل قرار مستقلّ عن القراءة، وله تدقيقه الخاصّ (§9.4).
3. **درجة الرؤية تُطبَّق قبل النطاق وبعده** — أي أنّ المستند `Financial` لا يظهر لـ`Manager` ولا `TeamLeader` ولا `Employee` **حتّى داخل نطاقهم**، تنفيذًا لـ§7.5 («Contract Files = Finance/Management فقط») و§4.15.2 («العقود والتكلفة لا تظهر لفريق التنفيذ»).
4. **`Restricted`** لا يراه إلّا `Admin`/`Ceo`/`GeneralManager` ومدير حساب العميل نفسه.
5. **لا يوجد دور ماليّ مستقلّ اليوم** ⇒ فئة `Financial` تُقيَّد حاليًّا على الإدارة العليا ومدير الحساب؛ وعند إنشاء دور ماليّ مستقبلًا يُضاف إلى **قائمة قراءة واحدة** بلا تغيير سكيمة.

### 7.3 قاعدة §4.42 (حرجة): لا تسريب بالعنوان

> «لا يجوز إرجاع اسم كيان غير مصرح به ثم منعه عند الفتح فقط.»

**التطبيق**: الفلترة بدرجة الرؤية والنطاق تجري **داخل استعلام القائمة نفسه** (Server-side Filtering، §13.24)، لا في طبقة العرض. المستند غير المصرَّح به **لا يظهر في العدّاد ولا في نتائج البحث ولا في تجميعات التصنيفات** — كأنّه غير موجود.

### 7.4 سلسلة الحُرّاس عند كلّ نقطة (النمط القانونيّ القائم)

بالترتيب الحرفيّ المُعتمَد في `EmployeeServiceRequestService.cs:98-110` و`ClientContactService`:

1. `auth.unauthenticated` (401) — لا هويّة.
2. `client.not_found` (404) — العميل غير موجود أو محذوف.
3. `auth.forbidden` (403) — «هذا العميل خارج نطاق صلاحيتك.» عبر `IClientProjectAccess.CanViewClient`.
4. `client_document.not_found` (404) — المستند غير موجود **أو لا يخصّ هذا العميل** (حاجز IDOR).
5. `auth.forbidden` (403) — درجة الرؤية أعلى من مستوى المستخدِم.
6. `auth.forbidden` (403) — الفعل (رفع/تعديل/حذف) غير مسموح لهذا المستخدِم.

**حاجز IDOR البنيويّ**: كلّ المسارات متداخلة تحت `/api/clients/{clientId}/...`، ويجري التحقّق دائمًا من أنّ `document.ClientId == clientId` **قبل** أيّ قراءة أو تنزيل — فلا يكفي معرفة `documentId` وحده.

---

## §8 — التنقّل وتجربة الاستخدام (Navigation & UX)

### 8.1 موضع التبويبين

`ClientDetailPage.tsx:62-70` يعرّف اليوم ستّة تبويبات. يُضاف **تبويبان في نهاية المصفوفة** (إضافة بحتة، بلا إعادة ترتيب، بلا كسر روابط قائمة):

| المفتاح | التسمية العربيّة | المحتوى |
|---|---|---|
| `documents` | **المستندات** | جدول/بطاقات مستندات العميل |
| `links` | **الروابط الخارجيّة** | جدول الروابط |

هذا **يحقّق حرفيًّا** ما يفرضه BRD الفصل 13 (`Client → Overview, Contacts, Projects, Files, Timeline`) والفصل 06 §6.15 (`Files — ملفات عميل عامة مصنفة`).

### 8.2 تبويب «المستندات»

**رأس التبويب**: عدّاد المستندات المرئيّة للمستخدِم الحاليّ (بعد الفلترة الأمنيّة، لا قبلها) + زرّ **«رفع مستند»** يظهر فقط عند `canWriteChildren`.

**مبدّل العرض**:
- **بطاقات (Cards)** — الافتراضيّ: أيقونة النوع، العنوان، شارة التصنيف، شارة الحالة، شارة درجة الرؤية، `v{N}`، الحجم، تاريخ آخر تحديث، الرافع.
- **جدول (Table)**: الاسم | التصنيف | الحالة | الرؤية | الإصدار | الحجم | النوع | الرافع | آخر تحديث | إجراءات.

**البحث والفلاتر** (كلّها خادميّة — §13.24):
| الفلتر | القيم |
|---|---|
| بحث نصّيّ | العنوان + الوصف + الوسوم |
| التصنيف | الأربعة عشر رمزًا |
| التاريخ | من/إلى على `UpdatedAtUtc` |
| الرافع | قائمة المستخدِمين ضمن النطاق |
| الوسوم | متعدّد الاختيار |
| الإصدار | «الحاليّ فقط» / «يشمل النسخ السابقة» |
| الحالة | Draft/Current/Superseded/Archived |
| المنتهية/المستحقّة للمراجعة | مبنيّ على `ExpirationDate`/`ReviewDate` |

**تنبيهات بصريّة**: شارة صفراء عند اقتراب `ReviewDate`، وشارة حمراء عند تجاوز `ExpirationDate` — بلا أيّ إجراء آليّ (لا حذف ولا أرشفة تلقائيّة).

**درج سجلّ النسخ (Version History Drawer)**: عند فتح مستند تظهر النسخ تنازليًّا: `v{N}` | الحجم | الرافع | التاريخ | ملاحظة التغيير | Checksum مختصر (أوّل 12 محرفًا) | زرّ تنزيل. مع لافتة ثابتة: **«لا تُحذف النسخ السابقة — كلّ رفع ينشئ نسخة جديدة.»**

**نافذة الرفع**: على نمط `HrRequestsPage.tsx` (FormData + `multipart/form-data`)، مع حُرّاس **في العميل** للحجم والامتداد (تجربة أفضل فقط — **التحقّق الحاكم خادميّ دائمًا**)، وحقول: العنوان، التصنيف، الوصف، الوسوم، درجة الرؤية، تاريخ الانتهاء، تاريخ المراجعة، ملاحظة التغيير.

**التنزيل**: عبر `downloadFile(path, filename)` القائم في `lib/api.ts:65-76` (blob + ترويسة Authorization) — **بلا أيّ رابط عامّ وبلا كشف `StorageKey`**.

### 8.3 تبويب «الروابط الخارجيّة»

جدول: الاسم | النوع | الرابط (يُفتح بـ`rel="noopener noreferrer"` و`target="_blank"`) | المالك | حالة الوصول | آخر تحقّق | شارة «مكسور» | شارة «مُعطَّل».

- زرّ **«تحقّقت من الرابط»** يضبط `LastVerifiedAtUtc` و`LastVerifiedByUserId` بنقرة واحدة.
- **لا زرّ حذف** — زرّ **«تعطيل»** فقط (§4.14.2).
- **تحذير دائم ظاهر في النموذج**: «لا تُدخِل كلمات مرور أو رموز وصول. استخدم حقل مرجع الخزنة.» — ومدعوم خادميًّا بـ`ClientFieldGuards`، فالرفض يظهر تلقائيًّا عبر `apiErrorMessage` بلا خريطة إضافيّة.

### 8.4 الحالات الفارغة والأخطاء

- **فارغ**: «لا توجد مستندات لهذا العميل بعد» + زرّ الرفع إن كان مصرَّحًا.
- **فارغ بسبب الصلاحيّة**: نفس النصّ تمامًا — **لا يُفصح للمستخدِم بوجود مستندات لا يراها** (تطبيق §4.42).
- **RTL كامل + خطّ Tajawal** كبقيّة النظام.

### 8.5 ما لا تفعله الواجهة صراحةً

- لا معاينة inline لأيّ ملفّ (لا PDF viewer ولا صور مضمَّنة) في V1 — **تنزيل فقط**، وهو ما يُغلق سطح هجوم SVG/HTML بالكامل.
- لا سحب-وإفلات متعدّد (رفع ملفّ واحد لكلّ نسخة) في V1.
- لا شاشة إدارة تصنيفات — التوسعة عبر الكود بقرار المالك (نمط `ClientCodeConstants`).

---

## §9 — تصميم الـAPI (تصميم فقط — بلا كود)

### 9.1 مستندات العميل — `/api/clients/{clientId}/documents`

| الفعل | المسار | الوصف | الجسم/المعاملات |
|---|---|---|---|
| `GET` | `/api/clients/{clientId}/documents` | قائمة مفلترة مصفّاة أمنيًّا | `search, categoryCode, statusCode, visibilityCode, uploaderId, tag, from, to, includeSuperseded, page, pageSize` |
| `GET` | `/api/clients/{clientId}/documents/{documentId}` | تفاصيل مستند + النسخة الحاليّة | — |
| `POST` | `/api/clients/{clientId}/documents` | **إنشاء مستند + نسخته الأولى** (`multipart/form-data`) | `file` + `title, categoryCode, description, tags, visibilityCode, expirationDate, reviewDate, notes` |
| `PUT` | `/api/clients/{clientId}/documents/{documentId}` | تعديل **بيانات الوصف فقط** (لا يمسّ الملفّ) | `title, categoryCode, description, tags, visibilityCode, statusCode, expirationDate, reviewDate, notes` |
| `DELETE` | `/api/clients/{clientId}/documents/{documentId}` | **Tombstone** بسبب إلزاميّ | `{ reason }` |
| `POST` | `/api/clients/{clientId}/documents/{documentId}/versions` | **رفع نسخة جديدة** (لا يحذف القديمة) | `file` + `changeNote` |
| `GET` | `/api/clients/{clientId}/documents/{documentId}/versions` | سجلّ النسخ (History) | — |
| `GET` | `/api/clients/{clientId}/documents/{documentId}/download` | تنزيل **النسخة الحاليّة** | — |
| `GET` | `/api/clients/{clientId}/documents/{documentId}/versions/{versionId}/download` | تنزيل نسخة محدّدة | — |
| `POST` | `/api/clients/{clientId}/documents/{documentId}/versions/{versionId}/restore` | **استعادة = نسخة جديدة برقم أعلى** | `changeNote` |
| `GET` | `/api/clients/{clientId}/documents/metadata` | التصنيفات ودرجات الرؤية والحالات والوسوم المستعمَلة | — |

> **`PUT` = استبدال كامل لبيانات الوصف** — تنسيقًا مع الدلالة المُثبَتة في Client 360 (`/clients/{id}/brand`, `/contacts/{id}`, `/digital-channels/{id}`): **كتابة جزئيّة تمحو الحقول غير المُرسَلة**. هذا سلوك مقصود ومُوثَّق، والواجهة تُرسِل الكائن كاملًا دائمًا.

### 9.2 الروابط الخارجيّة — `/api/clients/{clientId}/links`

| الفعل | المسار | الوصف |
|---|---|---|
| `GET` | `/api/clients/{clientId}/links` | قائمة (`includeInactive` صريح — §4.40.1) |
| `POST` | `/api/clients/{clientId}/links` | إنشاء رابط |
| `PUT` | `/api/clients/{clientId}/links/{linkId}` | استبدال كامل |
| `POST` | `/api/clients/{clientId}/links/{linkId}/verify` | تسجيل تحقّق + ضبط `IsBroken` |
| `POST` | `/api/clients/{clientId}/links/{linkId}/deactivate` | **تعطيل بدل الحذف** |
| `POST` | `/api/clients/{clientId}/links/{linkId}/activate` | إعادة تفعيل |

**لا `DELETE` إطلاقًا على الروابط** (§4.14.2).

### 9.3 أكواد الأخطاء (تتبع اللواحق القائمة: `.not_found`→404، `.conflict`→409، `auth.forbidden`→403، غيرها→400)

| الكود | HTTP | المعنى |
|---|---|---|
| `client.not_found` | 404 | العميل غير موجود |
| `auth.forbidden` | 403 | خارج النطاق / درجة الرؤية أعلى / فعل غير مسموح |
| `client_document.not_found` | 404 | المستند غير موجود أو لا يخصّ هذا العميل (حاجز IDOR) |
| `client_document.version_not_found` | 404 | النسخة غير موجودة أو لا تخصّ هذا المستند |
| `client_document.title_required` | 400 | العنوان مطلوب |
| `client_document.category_invalid` | 400 | تصنيف خارج الكتالوج |
| `client_document.visibility_invalid` | 400 | درجة رؤية خارج الكتالوج |
| `client_document.file_required` | 400 | لا ملفّ في الطلب |
| `client_document.file_empty` | 400 | حجم صفر |
| `client_document.extension_not_allowed` | 400 | امتداد خارج قائمة السماح |
| `client_document.mime_not_allowed` | 400 | نوع محتوى خارج قائمة السماح أو غير متّسق مع الامتداد |
| `client_document.signature_mismatch` | 400 | بصمة البايتات لا تطابق النوع المُعلَن |
| `client_document.file_too_large` | 400 | تجاوز `MaxUploadSizeBytes` / `MaxVideoUploadSizeBytes` |
| `client_document.secret_forbidden` | 400 | سرّ محتمَل في الوصف/الملاحظات/الوسوم/ملاحظة التغيير |
| `client_document.duplicate_version.conflict` | 409 | نفس Checksum للنسخة الحاليّة لنفس المستند |
| `client_document.delete_reason_required` | 400 | سبب الحذف إلزاميّ |
| `client_document.scan_pending.conflict` | 409 | الفحص لم يكتمل و`RequireCleanScanBeforeDownload=true` |
| `client_document.quarantined.conflict` | 409 | النسخة في الحجر الصحّيّ |
| `client_document.storage_failure` | 400 | فشل كتابة/قراءة التخزين (**بلا كشف أيّ مسار**) |
| `client_link.not_found` | 404 | الرابط غير موجود أو لا يخصّ هذا العميل |
| `client_link.title_required` | 400 | الاسم مطلوب |
| `client_link.url_invalid` | 400 | ليس http/https أو مشوَّه |
| `client_link.type_invalid` | 400 | نوع خارج الكتالوج |
| `client_link.secret_forbidden` | 400 | سرّ محتمَل في الرابط/الوصف/المرجع/الملاحظات |
| `client_link.state_unchanged.conflict` | 409 | تفعيل مُفعَّل أو تعطيل مُعطَّل (نمط `client_channel.state_unchanged.conflict`) |

### 9.4 أحداث التدقيق (على `audit_logs` القائم — بلا جدول جديد)

| الحدث | `EntityType` | محتوى `DataJson` |
|---|---|---|
| `client_document.created` | `ClientDocument` | `clientId, title, categoryCode, visibilityCode, versionNo=1, sizeBytes, mimeType, checksum` |
| `client_document.version_added` | `ClientDocument` | `clientId, versionId, versionNo, sizeBytes, checksum, changeNote` |
| `client_document.updated` | `ClientDocument` | `clientId, changedFields[]` (قِيَم قبل/بعد للحقول غير الحسّاسة) |
| `client_document.downloaded` | `ClientDocument` | `clientId, versionId, versionNo` |
| `client_document.version_restored` | `ClientDocument` | `clientId, sourceVersionNo, newVersionNo` |
| `client_document.deleted` | `ClientDocument` | `clientId, title, reason` |
| `client_document.access_denied` | `ClientDocument` | `clientId, attemptedAction, reasonCode` |
| `client_link.created` / `.updated` | `ClientExternalLink` | `clientId, title, linkTypeCode, accessStatusCode` |
| `client_link.verified` | `ClientExternalLink` | `clientId, isBroken` |
| `client_link.deactivated` / `.activated` | `ClientExternalLink` | `clientId, title` |

**قيود إلزاميّة على التدقيق (§4.39.2)**: **لا يُسجَّل `StorageKey`، ولا مسار فيزيائيّ، ولا محتوى الملفّ، ولا أيّ نصّ رُفض بسبب حارس الأسرار** — يُسجَّل كود الرفض فقط.

**`client_document.downloaded` حدث إلزاميّ** — التنزيل هو الفعل الأكثر حساسيّة في المنظومة كلّها ويجب أن يترك أثرًا لكلّ مرّة.

---

## §10 — التوافق الرجعيّ (Backward Compatibility)

| البُعد | الأثر | الدليل |
|---|---|---|
| جداول قائمة | **صفر `ALTER`، صفر `DROP`، صفر عمود جديد على أيّ جدول قائم** | الهجرة = `CreateTable` ×3 فقط |
| `clients` | يُذكَر كـ`principalTable` لمفتاح أجنبيّ فقط | لا تعديل بنيته |
| `Roles.cs` / `Program.cs` | **بلا تعديل** — صفر دور، صفر سياسة | §7.1 |
| `FileStorageOptions` | +7 مفاتيح **اختياريّة بقيم افتراضيّة**؛ `EmployeeServiceFinalDocumentsPath` بلا مساس | ملفّات البيئة الثلاثة تبقى صالحة كما هي |
| مسار HR القائم (رفع/تنزيل المستند النهائيّ) | **بلا مساس** — لا يشترك في كود ولا في مسار تخزين | مسار جذر منفصل |
| `[RequestSizeLimit(12MB)]` القائم | يبقى كما هو على نقطة HR؛ نقاط المستندات لها حدّها الخاصّ من الإعدادات | لا حدّ عامّ يُضاف على Kestrel |
| واجهات API قائمة | **صفر تغيير** في مسار أو DTO أو كود خطأ قائم | كلّ المسارات جديدة تحت `/api/clients/{clientId}/documents\|links` |
| `ClientDetailPage.tsx` | +تبويبان في نهاية المصفوفة | لا إعادة ترتيب ⇒ الروابط العميقة القائمة تبقى صالحة |
| التقارير وKPI والإجازات والبريد والمجدول | **صفر مساس** | لا ملفّ مشترك |
| Rollback | استعادة `publish`/`dist` + `DropTable` ×3 | لا بيانات قائمة تعتمد على الجداول الجديدة |

**الخلاصة**: التغيير **Additive Only** بالمعنى الحرفيّ — لو أُلغيت الميزة كليًّا بعد النشر، يعود النظام إلى حالته الحاليّة بإسقاط ثلاثة جداول فارغة أو مملوءة، بلا أيّ أثر على أيّ سلوك قائم.

---

## §11 — خطة الهجرة (Migration Plan)

### 11.1 الهجرة الواحدة

- **الاسم المقترَح**: `AddClientDocumentsAndLinksFoundation`.
- **المحتوى**: `CreateTable` لـ`client_documents`، `client_document_versions`، `client_external_links` + الفهارس المذكورة في §5 + المفاتيح الأجنبيّة (`Restrict` من `clients` إلى `client_documents`، `Cascade` من `client_documents` إلى النسخ، `Cascade` من `clients` إلى الروابط).
- **`Down`**: `DropTable` ×3 فقط.
- **صفر `AddColumn`/`AlterColumn`/`DropColumn` على أيّ جدول قائم.**
- الفهرس **GIN** على `TagsJson` يُصرَّح به بـ`HasMethod("gin")` في إعداد EF.

### 11.2 تسوية رؤوس البيئات (مُلزِمة قبل أيّ نشر)

| البيئة | الرأس الحاليّ المُثبَت | الإجراء المطلوب |
|---|---|---|
| المستودع | `20260713171040_AdminGovernanceReportKpiCorrection` | الهجرة الجديدة تُبنى **فوقه** |
| Production | 30 هجرة | يُعاد التحقّق من الرأس قبل النشر مباشرةً |
| RC | 30 هجرة | كما أعلاه |
| **TEST/UAT** | 31 هجرة، الرأس `20260712211952_AddClient360Foundation` | **متأخّرة بهجرة واحدة عن المستودع — تُسوَّى أوّلًا** |

> **قاعدة**: لا تُولَّد الهجرة الجديدة إلّا بعد تأكيد أنّ رأس شجرة العمل هو `20260713171040`، وإلّا نشأ رأسان متوازيان.

### 11.3 تسلسل النشر المقترَح (عند التصريح لاحقًا)

1. **نسخة احتياطيّة كاملة** = `pg_dump` **+ `tar` لجذر تخزين الملفّات + تصدير Storage Manifest** (§4.38.2، G-14). *النسخة الأولى ستكون لجذر فارغ — لكن الإجراء يُثبَّت من اليوم الأوّل.*
2. إنشاء مجلّد التخزين لكلّ بيئة بصلاحيّات `www-data` وخارج جذر الويب، وضبط `ClientDocumentsPath` في ملفّ البيئة.
3. نشر Backend ⟶ الهجرة تُطبَّق تلقائيًّا عند الإقلاع (`db.Database.MigrateAsync()`) ⟶ التحقّق من سطر `Applying migration` **الواحد** ومن أنّ الرأس تقدّم بهجرة واحدة فقط.
4. نشر Frontend (استبدال `dist` ذرّيًّا + `chown`).
5. **Smoke قراءة-فقط**: `/health`=200، التبويبان يظهران، القائمة الفارغة تُرجِع 200، رفع/تنزيل **لا يُجرَّب على بيانات إنتاجيّة حقيقيّة** إلّا بقرار صريح.
6. **Rollback مُختبَر مسبقًا** (§13.31): استعادة `publish`/`dist` + `DropTable` ×3.

### 11.4 ما لا تفعله الهجرة صراحةً

- **لا ترحيل لأيّ مرفَق قائم**: مستند HR النهائيّ يبقى حيث هو بمنظومته الحاليّة. **صفر Backfill، صفر نسخ ملفّات، صفر تعديل صفّ قائم.**
- لا بذر (Seeding) لأيّ تصنيف أو رابط — الجداول تبدأ **فارغة تمامًا**.

---

## §12 — المراجعة الأمنيّة (Security Review)

| السطح | الضابط في هذا التصميم | الحالة |
|---|---|---|
| **تحقّق النوع** | امتداد **و** نوع محتوى **و** بصمة بايتات (magic number) — الثلاثة بـAND | ✅ يعالج G-05 |
| **تحقّق الحجم** | من الإعدادات، حدّان (عامّ + فيديو)، يُفحَص **قبل** الكتابة على القرص | ✅ يعالج G-06 |
| **Path Traversal** | مفتاح التخزين GUIDs بالكامل، الاسم الأصليّ لا يلمس القرص أبدًا، + حاجز احتواء المسار قبل كلّ فتح | ✅ |
| **تسريب المسار** | `StorageKey` غير موجود في أيّ DTO ولا رسالة خطأ ولا سجلّ تدقيق | ✅ §4.38.1 |
| **IDOR** | كلّ المسارات متداخلة تحت `{clientId}` + تحقّق `document.ClientId == clientId` قبل أيّ قراءة | ✅ |
| **تسريب بالعنوان** | الفلترة الأمنيّة داخل استعلام القائمة نفسه؛ العدّادات مفلترة أيضًا | ✅ §4.42 |
| **تفويض التنزيل** | نقطة مُصادَقة + فحص درجة الرؤية + تدقيق إلزاميّ لكلّ تنزيل | ✅ |
| **XSS عبر SVG/HTML** | `attachment` + `application/octet-stream` + لا معاينة inline + `nosniff` مضبوط سلفًا | ✅ |
| **ZIP Bomb** | حدّ الحجم فقط في V1؛ **لا فكّ ضغط إطلاقًا على الخادم** ⇒ سطح الهجوم منعدم | ✅ (بالتصميم) |
| **منع تخزين Credentials** | `ClientFieldGuards` على كلّ حقل نصّيّ حرّ + على الرابط + على مرجع الخزنة | ✅ يعالج G-11 |
| **Checksum وكشف التكرار** | SHA-256 على التيّار + رفض تكرار النسخة | ✅ يعالج G-03 |
| **التدقيق** | 10 أحداث، بلا أسرار وبلا مسارات (§4.39.2) | ✅ |
| **الحذف** | Tombstone بسبب إلزاميّ، مقصور على الإدارة العليا، بلا حذف فيزيائيّ | ✅ §4.40 |
| **فصل الماليّ عن التنفيذ** | `VisibilityCode=Financial` محجوب عن Manager/TeamLeader/Employee داخل نطاقهم | ✅ §7.5، §4.15.2 |
| **فحص البرمجيّات الخبيثة** | **حقول ومنطق البوّابة موجودان (`ScanStatusCode`/`QuarantineStatusCode`/`RequireCleanScanBeforeDownload`) لكن لا محرّك فعليّ** | ⚠️ **فجوة مُعلَنة — شرط القبول (أ)** |
| **روابط موقَّعة قصيرة العمر** | لا — تنزيل عبر نقطة مُصادَقة (متوافق مع القاعدة 18 «كل File Access Server-side» ومع §13.26 Streaming) | ⚠️ **فجوة مُعلَنة — شرط القبول (ب)** |
| **النسخ الاحتياطيّ للملفّات** | إجراء إلزاميّ جديد (tar + Manifest) | ⚠️ إجرائيّ لا برمجيّ — يُثبَّت في Runbook |
| **حدّ معدّل الرفع** | لا حدّ خاصّ في V1 (الحدّ الوحيد القائم على `/api/auth/login`) | ⚠️ مخاطرة منخفضة مقبولة — تُسجَّل كـR-06 |

### 12.1 لماذا «غياب محرّك الفحص» مقبول مؤقّتًا
- الرفع **مقصور على المصرَّح لهم** (`canWriteChildren`) — لا رفع مجهول ولا عامّ.
- الملفّ **لا يُنفَّذ ولا يُعرَض inline ولا يُفكّ ضغطه** على الخادم — التخزين سلبيّ بحت.
- التنزيل يمرّ بمصادقة وتفويض وتدقيق.
- الحقول والبوّابة موجودة ⇒ **تفعيل المحرّك لاحقًا لا يتطلّب أيّ تغيير سكيمة ولا تغيير API**.

---

## §13 — تقييم المخاطر (Risk Assessment)

| # | المخاطرة | الاحتمال | الأثر | التخفيف |
|---|---|---|---|---|
| R-01 | امتلاء القرص على الـVPS (المشترَك مع الأكاديميّة) | **متوسّط** | **عالٍ** — يعطّل الخدمة كلّها | حدّ حجم من الإعدادات + مراقبة مساحة + قرار مبكّر بسقف تخزين لكلّ عميل + خطّة الانتقال إلى Object Storage (§13.27) |
| R-02 | فقد الملفّات لأن النسخ الاحتياطيّ الحاليّ يغطّي DB فقط | **مرتفع إن أُهمِل** | **عالٍ جدًّا** | **إلزام `tar` + Storage Manifest في كلّ نشر** (G-14) — يُدرَج في Runbook قبل أوّل رفع حقيقيّ |
| R-03 | رفع ملفّ خبيث لغياب محرّك الفحص | منخفض | متوسّط | قائمة سماح + magic number + لا تنفيذ/عرض/فكّ ضغط + رفع مقصور على المصرَّح لهم |
| R-04 | تسرّب مستند ماليّ إلى فريق التنفيذ | منخفض | **عالٍ** | `VisibilityCode` مفروض خادميًّا في الاستعلام + اختبار انحدار مخصّص لكلّ دور |
| R-05 | دورة FK بين المستند ونسخته تُعقّد الهجرة | متوسّط | منخفض | nullable + Restrict + معاملة واحدة؛ **بديل جاهز**: إسقاط `CurrentVersionId` والاكتفاء بـ`IsCurrent` |
| R-06 | إغراق بالرفع (لا حدّ معدّل) | منخفض | متوسّط | حدّ الحجم + المصادقة؛ يُضاف حدّ معدّل لاحقًا إن لزم |
| R-07 | انزياح بيئة TEST (متأخّرة بهجرة) يربك النشر | **مرتفع إن أُهمِل** | متوسّط | تسوية الرأس قبل توليد الهجرة (§11.2) |
| R-08 | نموّ `audit_logs` بسبب تدقيق كلّ تنزيل | متوسّط | منخفض | مقبول — التدقيق أهمّ من الحجم؛ يُراجَع مع سياسة احتفاظ لاحقة |
| R-09 | توسّع النطاق نحو ملفّات المشروع أثناء التنفيذ | **متوسّط** | متوسّط | الحدّ صريح في §0 و§4.6: **ملفّات العميل فقط**، ودورة الاعتماد السباعيّة مؤجَّلة |
| R-10 | اختلاف Storage بين البيئات يسبّب مراجع معلَّقة عند استنساخ القاعدة | متوسّط | متوسّط | §4.38.2: استنساخ مستقلّ للتخزين مع القاعدة، وعدم مشاركة الجذر بين بيئتين إطلاقًا |

---

## §14 — خطة التنفيذ وخطة الاختبار وهيكل تجزئة العمل (Execution Plan / Testing Plan / WBS)

### 14.1 هيكل تجزئة العمل (WBS)

| # | الحزمة | المخرَج | يعتمد على |
|---|---|---|---|
| **W1** | تجريد التخزين | `IFileStorage` + `LocalFileStorage` + توسعة `FileStorageOptions` + تسجيل DI | — |
| **W2** | نموذج المجال | `ClientDocument` + `ClientDocumentVersion` + `ClientExternalLink` + `ClientDocumentCodeConstants` | — |
| **W3** | الثبات | `ClientDocumentConfigurations` + 3 `DbSet` + الفهارس والفلاتر العالميّة + **الهجرة الواحدة** | W2 |
| **W4** | التحقّق والأمان | مُتحقِّق النوع/الحجم/البصمة + دمج `ClientFieldGuards` + حاسب SHA-256 + كشف التكرار | W1، W2 |
| **W5** | خدمة المستندات | `ClientDocumentService` (إنشاء/قائمة/تفاصيل/تعديل/حذف/نسخة جديدة/سجلّ/تنزيل/استعادة) + التفويض بالمورد + التدقيق | W3، W4 |
| **W6** | خدمة الروابط | `ClientExternalLinkService` (إنشاء/قائمة/تعديل/تحقّق/تعطيل/تفعيل) + التدقيق | W3، W4 |
| **W7** | الـAPI | `ClientDocumentsController` + `ClientExternalLinksController` | W5، W6 |
| **W8** | الواجهة — المستندات | `ClientDocumentsTab.tsx` + `useClientDocuments.ts` + بطاقات/جدول/فلاتر/درج النسخ/نافذة الرفع | W7 |
| **W9** | الواجهة — الروابط | `ClientLinksTab.tsx` + جدول + تعطيل/تحقّق | W7 |
| **W10** | الدمج في Client 360 | +تبويبان في `ClientDetailPage.tsx` + `types/api.ts` + `format.ts` | W8، W9 |
| **W11** | الاختبارات | تكامل Backend + وحدة + اختبارات الواجهة | W7، W10 |
| **W12** | جاهزيّة التشغيل | Runbook النسخ الاحتياطيّ (tar + Manifest) + تسوية رأس TEST + خطة Rollback مُختبَرة | W3 |

**ترتيب التنفيذ الموصى به**: W1+W2 ⟶ W3 ⟶ W4 ⟶ W5 ⟶ W6 ⟶ W7 ⟶ W8+W9 ⟶ W10 ⟶ W11 ⟶ W12.
**مبدأ التجزئة**: كلّ حزمة تنتهي بشجرة تُبنى وتُختبَر خضراء — لا حزمة تترك النظام مكسورًا.

### 14.2 خطة الاختبار

**أ. اختبارات التكامل (Backend — على نمط `Client360FoundationTests.cs` و`[Collection("Integration")]`)**

| المجموعة | الحالات |
|---|---|
| الرفع | إنشاء مستند بنسخة أولى؛ رفض بلا ملفّ؛ رفض حجم صفر؛ رفض تجاوز الحدّ؛ رفض امتداد غير مسموح؛ رفض نوع محتوى غير متّسق؛ **رفض بصمة بايتات مزيَّفة (ملفّ `.pdf` محتواه ليس PDF)**؛ رفض عنوان فارغ؛ رفض تصنيف خارج الكتالوج |
| النسخ | رفع نسخة ثانية ⇒ `VersionNo=2` و`IsCurrent` ينتقل والأولى **تبقى موجودة**؛ سجلّ النسخ يُرجِع الاثنتين؛ رفض نسخة مطابقة الـChecksum بـ409؛ الاستعادة تُنشئ `VersionNo=3` ولا تحذف شيئًا |
| الأسرار | رفض وصف/ملاحظة/وسم/ملاحظة تغيير تحوي «password» أو «api key» أو «كلمة المرور» بـ`client_document.secret_forbidden` |
| التفويض | كلّ دور من العشرة في §7.2 × (قائمة/تنزيل/رفع/تعديل/حذف) — **50 مسبارًا** بالنمط المُثبَت في UAT-R1 |
| الرؤية | مستند `Financial` **لا يظهر أصلًا** في قائمة `Manager`/`TeamLeader`/`Employee` (لا في النتائج ولا في العدّاد) |
| IDOR | مستند العميل «أ» بمعرّفه الصحيح تحت مسار العميل «ب» ⇒ **404 لا 403** (عدم إفشاء الوجود) |
| الحذف | Tombstone يخفي من كلّ القوائم؛ رفض بلا سبب؛ 403 لغير الإدارة العليا؛ التنزيل بعد الحذف ⇒ 404 |
| التخزين | `StorageKey` **غير موجود** في أيّ استجابة (فحص نصّيّ على جسم JSON الكامل) |
| الروابط | إنشاء/تعديل/تحقّق/تعطيل/تفعيل؛ رفض رابط `javascript:`/`ftp:`؛ رفض رابط يحوي سرًّا؛ **لا وجود لمسار `DELETE`**؛ 409 عند تعطيل مُعطَّل |
| الانحدار | مسار HR (رفع/تنزيل المستند النهائيّ) يعمل كما هو؛ Client 360 (Contacts/Channels/Brand) بلا تغيير؛ `/health`=200 |

**ب. اختبارات الوحدة**: مُتحقِّق البصمة لكلّ نوع؛ حاسب SHA-256؛ مُتحقِّق الكتالوج؛ منطق ترقيم النسخ؛ حاجز احتواء المسار (مسار خارج الجذر ⇒ رفض).

**ج. اختبارات الواجهة (Vitest + RTL)**: ظهور التبويبين؛ إخفاء زرّ الرفع لغير المصرَّح؛ عرض `v{N}` والحجم؛ درج النسخ يعرض النسخ تنازليًّا؛ الحالة الفارغة؛ رسالة رفض السرّ تظهر للمستخدِم.

**د. بوّابة عدم الانحدار (مُلزِمة)**: مقارنة نتائج مجموعة الاختبارات على **شجرة الأساس** مقابل **شجرة المرشّح** ⇒ يجب أن تكون `Candidate-only = []` و`Baseline-only = []` — بالنمط المُثبَت في `LEAVE-WORKFLOW-DEADLOCK-HOTFIX-R1`.

**هـ. UAT**: على بيئة TEST حصرًا، بعد تسوية رأس الهجرات، على عميل تجريبيّ مُنشأ خصّيصًا — **بلا أيّ مساس بـProduction أو RC**.

### 14.3 معايير القبول (Definition of Done)

1. الهجرة **واحدة** و`CreateTable` ×3 فقط، و`has-pending-model-changes` = لا تغييرات.
2. كلّ اختبارات التكامل والوحدة خضراء + بوّابة عدم الانحدار نظيفة.
3. `StorageKey` صفر ظهور في أيّ استجابة أو سجلّ تدقيق (فحص آليّ).
4. مصفوفة الأدوار في §7.2 مُغطّاة باختبار لكلّ خليّة حرجة.
5. Runbook النسخ الاحتياطيّ (tar + Storage Manifest) مكتوب ومُختبَر استعادته مرّة واحدة على الأقلّ.
6. `tsc` = 0 أخطاء وبناء الواجهة أخضر.
7. صفر تعديل على `Roles.cs` و`Program.cs` (فحص `diff` صريح).

---

## §15 — البوّابة النهائيّة (Final Gate)

### 15.1 حكم كلّ ركيزة

| الركيزة | الحكم | المُبرِّر |
|---|---|---|
| **Architecture Ready** | **GO** | التصميم **إضافيّ بحتٌ** فوق Client 360 القائم: ثلاثة كيانات جديدة، ثلاثة جداول جديدة، تبويبان مُلحَقان بنهاية مصفوفة التبويبات. صفر تعديل على `Roles.cs` أو `Program.cs` أو `ScopeResolver` أو مسار الاعتماد. طبقة تجريد التخزين (`IFileStorage`) تُدخَل بلا كسر مسار مرفقات HR القائم الذي يبقى على `EmployeeServiceFinalDocumentsPath` كما هو (§6، §10). |
| **Database Ready** | **GO** | هجرة **واحدة** `CreateTable` ×3 + فهارس + مفاتيح أجنبيّة، و`Down` = `DropTable` ×3. صفر `ALTER` وصفر `DROP` على أيّ جدول قائم. دورة المفتاح الأجنبيّ بين `client_documents.CurrentVersionId` و`client_document_versions.ClientDocumentId` محسومة (قابل للإفراغ + `Restrict` + معاملة واحدة) مع خطّة بديلة مُعلَنة. الجداول تبدأ **فارغة تمامًا** بلا Backfill ولا ترحيل ملفّات ولا بذر (§11.4). |
| **Security Ready** | **CONDITIONAL GO** | أربع عشرة ضابطة من ثماني عشرة مُستوفاة بالكامل (§12): تحقّق نوع بمنطق **AND** لا `OR`، بصمة ملفّ (Magic Number)، تحقّق حجم **قبل** الكتابة على القرص، حاجز احتواء المسار، صفر تسريب لمسار فيزيائيّ، IDOR بـ404 لا 403، منع تسريب بالاسم، تخويل تنزيل مستقلّ عن القراءة، SVG بـ`attachment` دائمًا ولا معاينة مضمَّنة إطلاقًا، صفر فكّ ضغط على الخادم، منع الاعتمادات عبر `ClientFieldGuards`، Checksum، تدقيق كامل، حذف Tombstone. **الأربع المتبقّية شروط مُعلَنة لا عيوب مخفيّة** (تفصيلها في 15.2). |
| **API Ready** | **GO** | سبع عشرة نقطة نهاية مُصمَّمة بالكامل (§9.1، §9.2) بمسارات متداخلة تحت `/api/clients/{clientId}/…` تُنشئ حاجز IDOR بنيويًّا، وخمسة وعشرون رمز خطأ محدَّدًا يتوافق مع اصطلاح `ApiControllerBase` القائم (`.not_found`⟶404، `.conflict`⟶409، `auth.forbidden`⟶403، والباقي 400)، وعشرة أحداث تدقيق على `audit_logs` القائم بلا جدول تدقيق جديد. دلالة `PUT` = استبدال كامل، متّسقة مع سابقة Client 360. |
| **UX Ready** | **GO** | تبويبان مُلحَقان بالنهاية (الروابط العميقة القائمة تبقى صالحة)، عرض Cards/Table، ثمانية مُرشِّحات، درج سجلّ النسخ، حوار رفع على نمط `HrRequestsPage` القائم، تنزيل عبر `downloadFile` القائم في `lib/api.ts`، حالة فارغة **موحَّدة النصّ** سواء كان العميل بلا مستندات أو كان المستخدِم لا يراها (تطبيق §4.42)، RTL كامل وTajawal. غير الأهداف مُعلَنة صراحةً (§8.5). |

### 15.2 الحكم النهائيّ: `CONDITIONAL GO`

**Ready for Implementation = `CONDITIONAL GO`.**

التصميم مكتمل وقابل للتنفيذ فورًا من الناحية المعماريّة وقاعدة البيانات وواجهة الـAPI وتجربة الاستخدام. الشروط الأربعة التالية **قرارات مالك المنتج** لا عوائق تقنيّة، ولا يبدأ التنفيذ قبل حسمها كتابةً:

| # | الشرط | القرار المطلوب | أثر الرفض |
|---|---|---|---|
| **C-01** | **لا يوجد محرّك فحص فيروسات فعليّ** على الخوادم. التصميم يوفّر خطّافًا جاهزًا (`MalwareScanStatus` + `RequireCleanScanBeforeDownload` + `ScanEngine=None`) لكنّه **لا يفحص شيئًا اليوم**. | قبول الرفع بلا فحص في هذه المرحلة مع بقاء الخطّاف، **أو** تأجيل التنفيذ حتّى توفير محرّك (ClamAV أو ما يعادله). | إن رُفض ⟶ تنفيذ المرحلة يتوقّف حتّى توفير المحرّك. |
| **C-02** | **التنزيل عبر نقطة نهاية مُصادَقة** بدل الروابط الموقَّعة قصيرة العمر. الملفّ يمرّ عبر التطبيق في كلّ تنزيل. | قبول هذا النمط (وهو نمط مرفقات HR القائم بالفعل)، **أو** طلب روابط موقَّعة ⟶ نطاق إضافيّ. | إن طُلبت الروابط الموقَّعة ⟶ حزمة عمل إضافيّة خارج نطاق هذه المرحلة. |
| **C-03** | **النسخ الاحتياطيّ الحاليّ يغطّي قاعدة البيانات فقط** ولا يغطّي الملفّات على القرص (الفجوة G-14). | اعتماد Runbook إلزاميّ: `pg_dump` **+** `tar` لجذر التخزين **+** تصدير Storage Manifest، مع التحقّق من التطابق بعد الاستعادة. | إن لم يُعتمَد ⟶ **NO-GO**: فقدان الملفّات لا يمكن استرداده وقاعدة البيانات تصير مؤشِّرات معلَّقة. |
| **C-04** | **لا حدّ معدّل على الرفع** (المخاطرة R-06) ولا حصّة تخزين لكلّ عميل، والخادم مشترك مع خدمات أخرى. | قبول المخاطرة مع مراقبة المساحة، **أو** إضافة حدّ معدّل وحصّة ⟶ نطاق صغير إضافيّ. | إن رُفضت ⟶ حزمة عمل صغيرة تُضاف إلى W12. |

**C-03 هو الشرط الحاسم**: قبوله شرط لازم لأيّ تنفيذ، لأنّ فقدان الملفّات غير قابل للاسترداد. أمّا C-01 وC-02 وC-04 فقرارات موازنة بين السرعة ومستوى التحصين، وكلّها موثَّقة صراحةً في §12 و§13.

### 15.3 التوقّف المُلزَم

هذا التقرير **تحليل وتصميم قراءة-فقط**. حتّى لحظة كتابته:

- **صفر كود مكتوب** — لا كيان ولا خدمة ولا كنترولر ولا مكوّن واجهة.
- **صفر هجرة مُولَّدة** أو مُطبَّقة.
- **صفر نشر** وصفر إعادة تشغيل وصفر Commit وصفر Push.
- **صفر مساس** بـProduction أو RC أو TEST — لا قراءة كتابيّة ولا تعديل إعداد ولا إنشاء مجلّد تخزين.

**لا يبدأ أيّ عمل تنفيذيّ (W1 فما بعد) إلّا بتصريح صريح جديد يحسم C-01 وC-02 وC-03 وC-04.**
