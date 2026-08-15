# CPW-R1B2 — خدمة مستندات وأصول العميل — عقد التنفيذ (R1)

الحالة: **مُنفَّذ محليًّا على فرع `feature/cpw-r1b2-document-service-20260807` فوق الأساس `c157829`.**
لم يُدفَع ولم يُدمَج ولم يُنشَر على أيّ بيئة.

هذه الوثيقة هي العقد المرجعيّ لما بُني فعلًا: نموذج البيانات، سطح الـAPI، ثوابت الأمان،
مفاتيح الإعداد، وسلوك الواجهة. ما لم يُذكر هنا فهو غير موجود.

---

## 1. النطاق

| داخل النطاق | خارج النطاق |
|---|---|
| رفع مستندات العميل وتعدّد نسخها | فحص فيروسات فعليّ (C-01) |
| تنزيل مصادَق ومحكوم بالنطاق | روابط تنزيل موقَّعة أو Static Files (C-02) |
| أرشفة/إلغاء أرشفة | حذف نهائيّ للصفّ أو للملفّ |
| حذف منطقيّ (Tombstone) بسبب إلزاميّ | مشاركة خارجيّة مع العميل |
| الروابط المهمّة (Drive/Analytics/…) | تخزين سحابيّ (S3 وما شابه) |
| حصّة تخزين + حدّ حجم + حدّ معدّل رفع (C-04) | استخراج نصّ/فهرسة/معاينة داخل المتصفّح |

---

## 2. نموذج البيانات

الهجرة الوحيدة: `20260807033602_ClientDocumentsAndExternalLinks` — **إضافيّة بالكامل**
(`CreateTable` ×3 + `CreateIndex` ×10 + `AddForeignKey` ×1؛ بلا `DropTable`/`DropColumn`/`AlterColumn`).
`Down` = إسقاط الجداول الثلاثة فقط. Model Sync: `No changes`.

### 2.1 `client_documents`
رأس المستند. `ClientId` → `clients` بـ **Restrict**، و`CurrentVersionId` → `client_document_versions` بـ **Restrict**.
حقول الدورة: `LifecycleStatus` (`Current`/`Superseded`/`Archived`)، `IsArchived`+`ArchivedAtUtc`+`ArchivedByUserId`+`ArchiveReason`،
و**Tombstone**: `IsDeleted`+`DeletedAtUtc`+`DeletedByUserId`+`DeleteReason`.
الفهارس: `ClientId`، `(ClientId,CategoryCode)`، `(ClientId,IsArchived,IsDeleted)`، `CurrentVersionId`.

### 2.2 `client_document_versions`
النسخة الفعليّة للملفّ. `ClientDocumentId` → `client_documents` بـ **Cascade**.
`StorageKey` (مفتاح التخزين على القرص)، `Sha256`، `SizeBytes`، `ContentType`، `OriginalFileName`،
`ScanStatus`+`ScanEngine`+`ScannedAtUtc`+`ScanDetail`، `IsCurrent`، `ChangeNote`.
الفهارس: `StorageKey` **فريد**، `(ClientDocumentId,VersionNo)` **فريد**،
`ClientDocumentId` **فريد بمرشّح `"IsCurrent" = true`** (نسخة سارية واحدة لا أكثر)، `(ClientDocumentId,Sha256)`.

> **مرجعيّة دائريّة مقصودة:** المستند يشير إلى نسخته السارية والنسخة تشير إلى مستندها.
> لذلك `CreateAsync` يكتب الصفّين أوّلًا بـ`CurrentVersionId = NULL` ثمّ يضبط المؤشّر
> في حفظ ثانٍ **داخل معاملة واحدة**. أيّ محاولة لكتابتهما بمؤشّر مضبوط في `SaveChanges` واحد
> تُسقِط EF Core بـ«circular dependency».

### 2.3 `client_external_links`
`ClientId` → `clients` بـ **Cascade**. `Title`/`Url`/`CategoryCode`/`Description`/`IsActive`/`SortOrder`.
لا حذف نهائيّ — التعطيل هو المسار الوحيد.

---

## 3. سطح الـAPI

### 3.1 المستندات — `api/clients/{clientId:guid}/documents`

| الفعل | المسار | ملاحظات |
|---|---|---|
| `GET` | `/` | فلاتر: `categoryCode`, `confidentialityCode`, `lifecycleStatus`, `search`, `includeArchived` |
| `GET` | `/storage-usage` | المستخدَم/الحصّة/المتبقّي + حدّ الرفع + الامتدادات المسموحة + حالة المحرّك |
| `GET` | `/{documentId}` | تفاصيل + كلّ النسخ |
| `POST` | `/` | `multipart/form-data` — حدّ معدّل + `RequestSizeLimit(32MB)` |
| `POST` | `/{documentId}/versions` | نسخة أحدث؛ السابقة تصير `Superseded` ولا تُحذف |
| `PUT` | `/{documentId}` | بيانات وصفيّة فقط — لا يمسّ أيّ ملفّ |
| `PATCH` | `/{documentId}/archive` \| `/unarchive` | |
| `POST` | `/{documentId}/delete` | Tombstone بسبب إلزاميّ |
| `GET` | `/{documentId}/download` | النسخة السارية |
| `GET` | `/{documentId}/versions/{versionId}/download` | نسخة محدّدة |

حقول الرفع: `File`, `Title`, `CategoryCode`, `Description`, `Tags`, `ConfidentialityCode`, `ChangeNote`.

### 3.2 الروابط — `api/clients/{clientId:guid}/links`
`GET /?includeInactive=` · `POST /` · `PUT /{id}` · `PATCH /{id}/activate` · `PATCH /{id}/deactivate`.

### 3.3 أكواد الأخطاء (تُحمَل في `type` داخل ProblemDetails)

| الكود | HTTP | المعنى |
|---|---|---|
| `client.not_found` | 404 | العميل غير مرئيّ للمستخدم أو غير موجود (تقنيع مقصود) |
| `client_document.not_found` | 404 | المستند غير موجود، أو تحت عميل آخر، أو محذوف Tombstone |
| `client_document_version.not_found` | 404 | النسخة غير موجودة |
| `document.file_required` | 400 | لا ملفّ أو ملفّ فارغ |
| `document.file_too_large` | 400 | تجاوز `MaxUploadSizeBytes` |
| `document.quota_exceeded` | 400 | تجاوز حصّة العميل |
| `document.extension_not_allowed` | 400 | امتداد خارج قائمة السماح |
| `document.mime_mismatch` | 400 | نوع المحتوى المُعلَن لا يوافق الامتداد |
| `document.magic_number_mismatch` | 400 | البصمة السحريّة لا توافق الامتداد |
| `document.scan_rejected` | 400 | المحرّك رفض المحتوى |
| `client_document.title_required` | 400 | |
| `client_document.category_invalid` | 400 | تصنيف خارج القائمة القانونيّة |
| `client_document.confidentiality_invalid` | 400 | |
| `client_document.secret_forbidden` | 400 | حارس الأسرار في العنوان/الوصف/الوسوم/ملاحظة التغيير |
| `client_document.delete_reason_required` | 400 | |
| `client_document.archived.conflict` | 409 | إضافة نسخة إلى مؤرشف |
| `client_document.state_unchanged.conflict` | 409 | أرشفة مؤرشف أو العكس |
| `client_document.scan_not_clean.conflict` / `.scan_rejected.conflict` | 409 | حين يُشترَط الفحص النظيف |
| `client_document.file_missing` | 400 | الصفّ قائم والملفّ مفقود على القرص |
| `external_link.url_required` / `url_too_long` / `url_invalid` | 400 | |
| `external_link.scheme_not_allowed` | 400 | غير `http`/`https` |
| `external_link.embedded_credentials` | 400 | `user:pass@` داخل العنوان |
| `external_link.secret_detected` | 400 | `access_token=` وما شابه |
| `client_external_link.category_invalid` / `.secret_forbidden` | 400 | |
| `client_external_link.state_unchanged.conflict` | 409 | |
| `auth.forbidden` | 403 | يرى العميل لكن لا يملك الإجراء |
| `auth.unauthenticated` | 401 | |

---

## 4. ثوابت الأمان (ملزِمة — أيّ تغيير فيها يفتح التذكرة من جديد)

1. **الصلاحيّة داخل الخدمة لا على الـController.** لا سياسة دور على أيّ من الـControllerين؛ القرار
   Resource-Based داخل `ClientDocumentService`/`ClientExternalLinkService`.
   - **قراءة**: مدير الحساب للعميل، أو من يرى العميل عبر `IClientProjectAccess`.
   - **كتابة**: مدير الحساب للعميل، أو `Roles.ClientCoreManagers` **بشرط** رؤية العميل.
   - **حذف**: `Roles.TeamManagement` حصرًا — مدير الحساب وحده لا يكفي (⇒ `auth.forbidden`).
2. **لا تسريب وجود.** خارج النطاق ⇒ `client.not_found` (404) لا 403. الوصول العابر بين العملاء
   (`documentId` تحت `clientId` آخر) ⇒ `client_document.not_found`.
3. **`StorageKey` لا يظهر إطلاقًا** — لا في DTO ولا في التدقيق ولا في السجلّ ولا في الواجهة.
   حمولة التدقيق تقتصر على `ClientId, CategoryCode, VersionNo, SizeBytes, Sha256, ScanStatus`.
4. **C-01 — أمانة حالة الفحص.** لا محرّك فعليّ ⇒ `NullDocumentScanner` يُرجِع `NotScanned` بمحرّك `None`
   ولا يُدَّعى «نظيف» أبدًا. `RequireCleanScanBeforeDownload=false` ما دام لا محرّك، وإلّا تعطّلت الخدمة كلّها.
5. **C-02 — التنزيل عبر نقطة نهاية مصادَقة ومحكومة ومُدقَّقة فقط.** لا روابط موقَّعة، لا `UseStaticFiles`،
   لا مسار قرص في أيّ استجابة. كلّ تنزيل يكتب حدث `client_document.downloaded`.
6. **التحقّق ثلاثيّ الطبقات**: الامتداد **و** نوع المحتوى **و** البصمة السحريّة (أوّل 512 بايت) — بـ**AND** لا OR.
7. **مرفق إجباريّ** لكلّ ما يُنفَّذ داخل المتصفّح: `.svg .zip .csv .txt .html .htm` + كلّ امتداد مجهول
   ⇒ `Content-Disposition: attachment`. غير ذلك `inline` بترميز `UTF-8''`.
8. **حارس الأسرار** يفحص كلّ حقل نصّيّ حرّ (عنوان/وصف/وسوم/ملاحظة تغيير/عنوان رابط) ويرفض
   كلمات المرور والرموز ومفاتيح الـAPI بالعربيّة والإنجليزيّة.
9. **لا حذف نهائيّ.** الحذف Tombstone بسبب إلزاميّ؛ الصفّ يبقى والملفّ يبقى على القرص.
   النسخة القديمة تبقى قابلة للتنزيل بعد رفع نسخة أحدث.
10. **C-04** — حدّ حجم + حصّة لكلّ عميل + حدّ معدّل رفع لكلّ مستخدم + سقف نقل صلب 32MB.

---

## 5. الإعداد (`FileStorage__*`)

| المفتاح | الافتراضيّ | ملاحظة |
|---|---|---|
| `FileStorage__DocumentsRootPath` | `ContentRoot/App_Data/documents` | **يجب** ضبطه صراحةً في الإنتاج خارج شجرة النشر (مثال `/var/lib/reporting/documents`) وخارج wwwroot |
| `FileStorage__MaxUploadSizeBytes` | `26214400` (25MB) | الحدّ الملزِم؛ سقف النقل في الـController 32MB بهامش المغلّف |
| `FileStorage__ResourceStorageQuotaBytes` | `2147483648` (2GB) | لكلّ عميل |
| `FileStorage__UploadRateLimitPermitLimit` | `20` | لكلّ مستخدم |
| `FileStorage__UploadRateLimitWindowSeconds` | `60` | |
| `FileStorage__AllowedExtensions` | فارغ ⇒ القائمة الافتراضيّة | التوسيع من الإعداد لا من الكود |
| `FileStorage__AllowedMimeTypes` | فارغ ⇒ القائمة الافتراضيّة | |
| `FileStorage__ScanEngine` | `None` | |
| `FileStorage__RequireCleanScanBeforeDownload` | `false` | يُرفَع إلى `true` فقط بعد وجود محرّك فعليّ |

> **C-03 (نسخ احتياطيّ) لم يُغلَق بالكود ولا يمكن إغلاقه بالكود.** النسخ الاحتياطيّ الحاليّ يغطّي
> قاعدة البيانات فقط. **صفوف المستندات بلا ملفّاتها = بيانات ميّتة.** قبل أيّ نشر إنتاج يلزم
> Runbook يجمع `pg_dump` + أرشفة `DocumentsRootPath` + بيان تخزين للمطابقة. هذا شرط نشر لا شرط كود.

---

## 6. الواجهة الأماميّة

تبويبان جديدان في `ClientDetailPage`: **المستندات** و**الروابط المهمّة**، بنفس حارس الكتابة
المستعمَل لبقيّة أقسام Client 360.

- الرفع بـ`FormData` مع إسقاط ترويسة `Content-Type` ليضبط المتصفّح حدّ `multipart`.
- التنزيل عبر `downloadFile` على نقطة النهاية المصادَقة — **لا مسار تخزين ولا رابط موقَّع في الواجهة**.
- `useClientDocuments.ts` يبطل مفاتيح `client-documents` / `client-document` / `client-storage-usage`
  بعد كلّ عمليّة كتابة.
- أنواع الواجهة (`types/api.ts`) **لا تحوي حقل `storageKey` إطلاقًا**.
- التعدادات تُنقَل نصًّا (`JsonStringEnumConverter`) ⇒ اتّحادات نصّيّة في TypeScript.

---

## 7. الاختبارات

`tests/Reporting.IntegrationTests/ClientDocumentsTests.cs` — صنفان، **28 اختبارًا، كلّها ناجحة**.

- `ClientDocumentsTests` (23 حالة): دورة الرفع، عدم تسرّب `StorageKey`، رفض الامتداد،
  رفض عدم تطابق البصمة السحريّة، رفض عدم تطابق نوع المحتوى، الملفّ الفارغ، التصنيف غير القانونيّ،
  حارس الأسرار في البيانات الوصفيّة وفي ملاحظة التغيير، تعدّد النسخ مع بقاء القديمة قابلة للتنزيل،
  الأرشفة وحجب المؤرشف وحارس النسخة على المؤرشف، سبب الحذف الإلزاميّ، حارس الإدارة العليا على الحذف،
  Tombstone وإخفاء المحذوف من كلّ المسارات، التنزيل ببايتات مطابقة، SVG كمرفق دائمًا،
  التنزيل المجهول 401، تقنيع 404 خارج النطاق، IDOR عابر بين العملاء، تقرير الحصّة وحالة المحرّك الأمينة.
- `ClientExternalLinksTests` (5 حالات منها `[Theory]` بأربع حالات): دورة إنشاء/تعديل/تعطيل،
  رفض `ftp://`، رفض بيانات الاعتماد المضمَّنة، رفض الرمز في سلسلة الاستعلام، رفض العنوان غير الصالح،
  التصنيف غير القانونيّ، حارس الأسرار، تقنيع 404، و401 للمجهول.

`CustomWebApplicationFactory` يوجّه التخزين إلى مجلّد مؤقّت خارج شجرة المشروع ويرفع حدّ معدّل الرفع
حتّى لا تُخنق المجموعة.

---

## 8. قيود معروفة

1. **لا محرّك فحص** (C-01) — واعية ومُعلَنة في الاستجابة (`scanEngine=None`, `scannerConfigured=false`).
2. **C-03 مفتوح** — لا Runbook نسخ احتياطيّ للملفّات بعد؛ شرط نشر إنتاج.
3. **حذف العميل** لا يحذف الملفّات من القرص (FK على المستندات `Restrict`؛ الروابط `Cascade`).
4. **لا إعادة استخدام محتوى مكرّر** — الفهرس `(ClientDocumentId,Sha256)` للكشف لا للمنع؛ نسخة بنفس
   البصمة تُخزَّن كملفّ مستقلّ.
5. **لا معاينة داخل التطبيق** — كلّ استهلاك عبر التنزيل.
