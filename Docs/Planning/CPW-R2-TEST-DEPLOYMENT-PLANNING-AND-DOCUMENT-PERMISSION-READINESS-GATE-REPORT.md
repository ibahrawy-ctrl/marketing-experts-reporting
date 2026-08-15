# CPW-R2 — TEST DEPLOYMENT PLANNING + MIGRATION + DOCUMENT PERMISSION READINESS GATE

**نوع العمل:** قراءة / تحليل / تخطيط فقط — **صفر تنفيذ**
**التاريخ:** 10 أغسطس 2026
**المرجع المدفوع:** `origin/feature/cpw-r1b2-document-service-20260807` @ `3344f7800f223a97b2fd4429d92d8c3449f3cfd9`

> **إثبات عدم التنفيذ:** لم يُنفَّذ أيّ Deploy / Restart / Migration / Backup فعليّ / mkdir / chmod / chown / تعديل env / Build / Commit / Push / Merge / PR / كتابة على TEST أو RC أو Production أو قاعدة بيانات أو مساحة تخزين. كلّ أوامر الخادم كانت قراءة محضة (`systemctl show`, `psql -tAc SELECT`, `ls`, `stat`, `sha256sum`, `strings`, `curl /health`).

---

## §0 — الحالة المعتمَدة (نقطة الانطلاق)

| البند | القيمة |
|---|---|
| الفرع البعيد | `feature/cpw-r1b2-document-service-20260807` |
| HEAD البعيد = المحلّي | `3344f7800f223a97b2fd4429d92d8c3449f3cfd9` |
| عدد الـCommits فوق R1B2 | **6** (`0da5153` → `d445836` → `e28affb` → `212162d` → `97434c2` → `3344f78`) |
| أساس R1B2 | `1121e5776c9a7b428763db25dcfa0ec9bd996eef` |
| دلتا الملفّات | **31 ملفًّا** |
| البوّابات السابقة | كلّها GO |
| حالة TEST الحاليّة | CPW-R1B2 منشور |
| `develop` / `main` / الوسوم | لم تُمَسّ |
| CI/CD | لا يوجد (`.github/workflows` غائب على كلّ الفروع) |

---

## §1 — خطّ الأساس الحيّ لبيئة TEST

| البند | القيمة الفعليّة | المتوقَّع | النتيجة |
|---|---|---|---|
| Hostname (الخادم) | `srv1747233` (Ubuntu 6.8.0-136) | — | — |
| اسم الخدمة | `khubara-reporting-test.service` | `khubara-reporting-test` | ✅ |
| ActiveState / SubState | `active` / `running` | active | ✅ |
| MainPID | **684344** | — | مُسجَّل |
| NRestarts | **0** | 0 | ✅ |
| بدء التشغيل | `Fri 2026-08-07 15:54:22 UTC` | — | مُسجَّل |
| User / WorkingDirectory | `www-data` / `/opt/reporting-test/publish` | — | ✅ |
| EnvironmentFile | `/etc/khubara-reporting-test.env` | — | ✅ |
| `ASPNETCORE_ENVIRONMENT` | **`Staging`** | Staging | ✅ |
| `ASPNETCORE_URLS` | `http://127.0.0.1:**5091**` | 5091 | ✅ |
| النطاق | `test.emarketingacademy.net` | نفسه | ✅ |
| قاعدة البيانات | **`reporting_test_uat`** | نفسها | ✅ |
| مالك/مستخدم القاعدة | `reporting_test_uat_app` | — | ✅ |
| Health داخليّ | `200` — `{"status":"ok","service":"reporting-api"}` | 200 | ✅ |
| Health عامّ | `200` | 200 | ✅ |

### إثبات عزل Production و RC

| | Production | RC | TEST |
|---|---|---|---|
| الخدمة | `reporting-api` | `khubara-reporting-rc` | `khubara-reporting-test` |
| البيئة | `Production` | `ReleaseCandidate` | `Staging` |
| المنفذ | 5090 | 5092 | 5091 |
| القاعدة | `reporting_prod` | `reporting_rc` | `reporting_test_uat` |
| مستخدم القاعدة | `reporting_app` | `reporting_rc_app` | `reporting_test_uat_app` |
| مسار النشر | `/opt/reporting/publish` | `/opt/reporting-rc/publish` | `/opt/reporting-test/publish` |
| env | `/etc/reporting-api.env` | `/etc/khubara-reporting-rc.env` | `/etc/khubara-reporting-test.env` |
| MainPID | 654185 | 647747 | 684344 |
| NRestarts | 0 | 0 | 0 |

**النتيجة:** ثلاث بيئات **منفصلة تمامًا** بلا أيّ تقاطع في الخدمة أو المنفذ أو القاعدة أو المستخدم أو المسار أو ملفّ الإعداد. ✅ **GO**

---

## §2 — بصمة التشغيل والإعداد الحاليّة على TEST

### Backend
| DLL | sha256 | SourceLink |
|---|---|---|
| `Reporting.Api.dll` | `e26c5c54…` | `1.0.0+1121e5776c9a7b428763db25dcfa0ec9bd996eef` |
| `Reporting.Application.dll` | `169228fb…` | نفسه |
| `Reporting.Domain.dll` | `dc098dd4…` | نفسه |
| `Reporting.Infrastructure.dll` | `a13008c0…` | نفسه |

**إثبات قاطع:** الأربع DLLs تحمل SourceLink = `1121e577…` ⇒ **TEST هي بالضبط CPW-R1B2**، لا أقلّ ولا أكثر. مجلّد النشر 48 عنصرًا، 755 `www-data:www-data`.

### Frontend
| البند | القيمة |
|---|---|
| الجذر | `/opt/reporting-test/frontend/dist` (من توجيه `root` في nginx) |
| الحزمة | `index-uSFMb9aF.js` — sha256 `dae00cd864c4c16ad110086f6c07d21475bbb567048ac10292a7682137e95d0e` — 1,432,608 B |
| CSS | `index-Cgt-yJT6.css` — sha256 `b2c94eff73c5c7431ff37328b4635a8a26fb51848e944cf31e07a52a2973a14e` — 31,403 B |
| `index.html` | sha256 `598a9466a41fc10bbb814c64fe2a95164bec3fdf2159b6d4a2d8be91f82af4f8` |
| API base داخل الحزمة | `https://test.emarketingacademy.net/api` **فقط** |
| تسرّب `localhost:509*` | **0** ✅ |

### الإعداد
| البند | القيمة |
|---|---|
| `/etc/khubara-reporting-test.env` | sha256 `263a07ff7257049fe893dc2a56465ba09a7eb42f0d2b4c46bfdeb4843c3c7e28` — 600 `root:root` — 1207 B |
| `/etc/nginx/sites-available/reporting-test` | sha256 `2b84642ebf7f5a3041c16e493b1965310369a59562a8573ddd5d7d6c7b5f8808` — 644 — 2842 B |
| حماية nginx | `auth_basic` عبر `/etc/nginx/.htpasswd-rc-test`؛ `/api/`, `/hubs/`, `/health` بـ`auth_basic off` وproxy إلى 5091 |

### إعداد التخزين (7 مفاتيح `FileStorage__*`)
| المفتاح | القيمة |
|---|---|
| `DocumentsRootPath` | `/var/lib/reporting-test/documents` |
| `MaxUploadSizeBytes` | `26214400` (25 MB) |
| `ResourceStorageQuotaBytes` | `2147483648` (2 GB) |
| `UploadRateLimitPermitLimit` | `20` |
| `UploadRateLimitWindowSeconds` | `60` |
| `ScanEngine` | `None` |
| `RequireCleanScanBeforeDownload` | `false` |

> **لم يُطبع أيّ سرّ** (سلاسل الاتّصال/كلمات المرور/المفاتيح لم تُقرأ ولم تُعرَض).

**النتيجة:** ✅ **GO** — TEST في حالة معروفة تمامًا وقابلة للاستعادة بالبصمات.

---

## §3 — بوّابة نَسَب الهجرات (Migration Lineage Gate)

| البند | القيمة |
|---|---|
| عدد الهجرات المطبَّقة على `reporting_test_uat` | **33** |
| رأس TEST | `20260807033602_ClientDocumentsAndExternalLinks` |
| قبل الأخير على TEST | `20260713171040_AdminGovernanceReportKpiCorrection` |
| عدد الهجرات في المستودع @ `3344f78` | **34** |
| الفرق (المعلَّقة) | **`20260809165617_ClientDocumentVisibility`** — واحدة فقط |
| السَّلَف المباشر مطبَّق؟ | ✅ نعم (`20260807033602`) |
| رأس متوازٍ؟ | ❌ لا يوجد |
| هجرة على TEST خارج النَّسَب؟ | ❌ لا شيء — مجموعة TEST **مجموعة جزئيّة صارمة** من مجموعة المستودع |
| سبق تطبيقها؟ | ❌ لا |
| العدد المتوقَّع بعد النشر | **34** |

### التحقّق من غياب الأهداف على TEST (قراءة فقط)
| الهدف | موجود؟ |
|---|---|
| جدول `client_document_allowed_roles` | ❌ غير موجود |
| جدول `client_document_allowed_users` | ❌ غير موجود |
| عمود `client_documents.VisibilityType` | ❌ غير موجود |
| عمود `client_documents.VisibilityUpdatedAtUtc` | ❌ غير موجود |
| عمود `client_documents.VisibilityUpdatedByUserId` | ❌ غير موجود |
| فهرس `IX_client_documents_ClientId_VisibilityType` | ❌ غير موجود |
| جدول `client_documents` | ✅ موجود |
| جدول `client_document_versions` | ✅ موجود |
| جدول `client_external_links` | ✅ موجود |

**النتيجة:** نَسَب سليم بلا انحراف، الهجرة المعلَّقة **واحدة** وقابلة للتطبيق بأمان. ✅ **GO** (لم تُطبَّق)

---

## §4 — أمان الهجرة (Migration Safety)

**الملفّ:** `20260809165617_ClientDocumentVisibility.cs` — قُرئ بالكامل (135 سطرًا).

### `Up` — العمليّات المسموحة حصرًا
| # | العمليّة | التفصيل |
|---|---|---|
| 1 | `AddColumn<string>` | `VisibilityType` على `client_documents`, `character varying(40)`, `nullable: false`, **`defaultValue: "ClientScoped"`** |
| 2 | `AddColumn<DateTime>` | `VisibilityUpdatedAtUtc`, `timestamptz`, nullable |
| 3 | `AddColumn<Guid>` | `VisibilityUpdatedByUserId`, `uuid`, nullable |
| 4 | `CreateTable` | `client_document_allowed_roles` (PK + FK Cascade → `client_documents`) |
| 5 | `CreateTable` | `client_document_allowed_users` (PK + FK Cascade → `client_documents`) |
| 6–11 | `CreateIndex` ×6 | تفصيلها أدناه |

### الفهارس الستّة
| الفهرس | الجدول | الأعمدة | فريد؟ |
|---|---|---|---|
| `IX_client_documents_ClientId_VisibilityType` | `client_documents` (**قائم**) | `{ClientId, VisibilityType}` | ❌ **غير فريد** |
| `IX_client_document_allowed_roles_ClientDocumentId` | جديد | `ClientDocumentId` | ❌ |
| `IX_client_document_allowed_roles_DocumentId_RoleName` | جديد | `{ClientDocumentId, RoleName}` | ✅ فريد |
| `IX_client_document_allowed_users_ClientDocumentId` | جديد | `ClientDocumentId` | ❌ |
| `IX_client_document_allowed_users_DocumentId_UserId` | جديد | `{ClientDocumentId, UserId}` | ✅ فريد |
| `IX_client_document_allowed_users_UserId` | جديد | `UserId` | ❌ |

### الأحكام
- **صفر `DropTable` / `DropColumn` / `RenameColumn` / `AlterColumn` تدميريّ / SQL يدويّ / Backfill.** ✅
- **المستندات القائمة:** `defaultValue: "ClientScoped"` يعني أنّ PostgreSQL يملأ الصفوف القائمة تلقائيًّا بـ`ClientScoped` ⇒ **صفر Backfill يدويّ وصفر قفل طويل الأمد على جدول بصفّ واحد**. ✅
- **خطر تعارض الفهارس الفريدة = صفر مطلق:** كلا الفهرسَين الفريدَين على **جدولين جديدين فارغين**؛ الفهرس الوحيد الذي يمسّ جدولًا قائمًا (`client_documents`) **غير فريد**. ✅
- **`Down`:** `DropTable` ×2 + `DropIndex` ×1 + `DropColumn` ×3 — عكس نظيف كامل (تصحيح لسجلّ سابق أغفل `DropIndex`).

**النتيجة:** الهجرة **إضافيّة بحتة وآمنة**. ✅ **GO**

---

## §5 — بيانات المستندات الحاليّة على TEST واتّساق التخزين

| العدّاد | القيمة |
|---|---|
| `client_documents` (الكلّ / غير المحذوف) | **1 / 1** |
| `client_document_versions` | **1** |
| `client_external_links` | **0** |
| `clients` | **4** |
| `projects` | **5** |

### مساحة التخزين
| البند | القيمة |
|---|---|
| الجذر | `/var/lib/reporting-test/documents` — 750 `www-data:www-data` |
| عدد الملفّات الفعليّة | **1** |
| عدد المجلّدات | 4 |
| الحجم الكلّي | **9,406,237 B** |

### مطابقة الصفوف ↔ الملفّات
| البند | القاعدة | القرص | مطابق؟ |
|---|---|---|---|
| `SizeBytes` | 9,406,237 | 9,406,237 | ✅ |
| `Sha256` | `a9c86965c3097abcb57393cdb15fe8dc98bb3d934a5fc5b3f5fc7ca40b990753` | نفسه | ✅ |
| `IsCurrent` / `VersionNo` | `t` / `1` | — | ✅ |
| `ScanStatus` | `NotScanned` | — | مُسجَّل |

- **صفوف بلا ملفّ (`comm -23`): 0** ✅
- **ملفّات بلا صفّ (`comm -13`): 0** ✅

> لم تُعرَض أسماء الملفّات الحسّاسة؛ الاعتماد على `StorageKey` والبصمة فقط.

### البيانات الوصفيّة للمستند الوحيد
`CategoryCode=Contract`, `ConfidentialityCode=Restricted`, `LifecycleStatus=Current`, `ApprovalStatusCode=NotApplicable`, `IsArchived=f`, `IsDeleted=f`, `VersionCount=1`, العميل = `30eedf6f…` (عميل اختبار البحراوي).

**النتيجة:** اتّساق تامّ، صفر يتيم في الاتّجاهين. **لم يُصلَح شيء.** ✅ **GO**

---

## §6 — جاهزيّة UAT لمدير العميل (Account Manager)

| البند | القيمة |
|---|---|
| الحساب | `account.manager@uat.local` — `f18df329-4e41-489c-9887-aeca572658e7` |
| الدور | `AccountPortfolioReader` (دور واحد) |
| نشط / لديه كلمة مرور | ✅ / ✅ |

### العملاء
| العميل | المعرّف | مدير العميل |
|---|---|---|
| **عميل UAT ألفا** (المختار) | `cc877dc2-dc6d-4fb1-97e0-fb84a1714571` | `account.manager@uat.local` ✅ |
| عميل UAT الإدارة المباشرة | `0181867b-…` | نفس مدير العميل |
| تجربة شركة من ابراهيم البحراوي | `30eedf6f-…` | `bhrawy@gmail.com` |
| عميل UAT بيتا | `91ab4f03-…` | (بلا مدير عميل) |

المشاريع: 5، منها **3 بإسناد `Project.AccountManagerId`**.

### سيناريو AM + Finance + Admin
قابل للاختبار بالكامل: مدير العميل (`AccountPortfolioReader`) + المالية (`FinanceManager`) + مدير النظام (`Admin` ×2) كلّها حسابات نشطة قائمة.

**لم يُنشأ أيّ حساب.** ✅ **GO**

---

## §7 — جاهزيّة دور المالية

أدوار TEST الفعليّة (12): `AccountPortfolioReader`, `Accountant`, `Admin`, `CEO`, `CeoSupport`, `Employee`, `FinanceManager`, `GeneralManager`, `HR`, `Manager`, `TeamLeader`, `Viewer`.

| الدور | عدد المستخدمين النشطين |
|---|---|
| `FinanceManager` | **1** (`finance.manager@uat.local` — نشط، لديه كلمة مرور) |
| `Accountant` | **0** ⚠️ |
| `AccountPortfolioReader` | 1 |
| `Admin` | 2 |
| `CEO` / `GeneralManager` / `HR` | 1 / 1 / 1 |
| `Manager` / `TeamLeader` | 2 / 2 |
| `Employee` / `Viewer` | 6 / 1 |
| `CeoSupport` | 0 |

**متطلّب تحضير UAT (لا إصلاح كود، لا إنشاء الآن):**
- **UAT-PREP-01:** لا يوجد مستخدم بدور `Accountant`. سياسة `Finance = {FinanceManager, Accountant}` ⇒ تغطية `FinanceOnly`/`ManagementAndFinance` ستُختبَر بـ`FinanceManager` فقط. يُوصى بتحضير حساب `Accountant` قبل UAT لتغطية الفرع الثاني.
- **UAT-PREP-02:** لا يوجد مستخدم `CeoSupport` (خارج نطاق سياسات المستندات — إعلاميّ فقط).

**لم يُنشأ أيّ مستخدم.** ✅ **GO مشروط بـ UAT-PREP-01**

---

## §8 — مراجعة الافتراضيّات (Document Permission Defaults)

**المصدر:** `Reporting.Application/Documents/DocumentCodeConstants.cs:43-74` — `DefaultVisibilityByCategory`.

| التصنيف | السياسة الافتراضيّة | مطابق للمطلوب؟ |
|---|---|---|
| `Contract` | `ManagementAndFinance` | ✅ |
| `Invoice` | `ManagementAndFinance` | ✅ |
| `Quotation` | `ManagementAndFinance` | ✅ |
| `FinancialProposal` | `ManagementAndFinance` | ✅ |
| `TechnicalProposal` | `ProjectTeam` | ✅ |
| `MarketingPlan` | `ProjectTeam` | ✅ |
| `MeetingMinutes` | `ProjectTeam` | ✅ |
| `BrandAsset` | `ProjectTeam` | ✅ |
| `Logo` | `ProjectTeam` | ✅ |
| `Creative` | `ProjectTeam` | ✅ |
| `Media` | `ProjectTeam` | ✅ |
| `NDA` | `ManagementOnly` | ✅ |
| `Legal` | `ManagementOnly` | ✅ |
| `Identity` | `ManagementOnly` | ✅ |
| `Proposal` | `ClientScoped` | ✅ |
| `Report` | `ClientScoped` | ✅ |
| `Presentation` | `ClientScoped` | ✅ |
| `Other` | `ClientScoped` | ✅ |
| أيّ تصنيف غير مذكور | `ClientScoped` (احتياط `DefaultVisibilityFor`) | ✅ |

**الخريطة مطابقة 18/18 بلا زيادة ولا نقصان.**

### التأكيدان المطلوبان
1. **الافتراضيّ يُطبَّق عند الإنشاء فقط:**
   `ClientDocumentService.cs:187` → `request.VisibilityType ?? DocumentCodeConstants.DefaultVisibilityFor(request.CategoryCode)` — داخل `CreateAsync` **حصرًا**. ✅
2. **التجاوز اليدويّ محفوظ ولا يُدهَس:**
   `ClientDocumentService.cs:327-328` — تعليق صريح «غياب `VisibilityType` يُبقي السياسة الحاليّة كما هي — لا تُطبَّق سياسة التصنيف الافتراضيّة عند التعديل»، والتطبيق `if (request.VisibilityType is DocumentVisibilityType requested)` ⇒ تغيير التصنيف لاحقًا **لا يُعيد ضبط** سياسة اختارها المستخدم. ✅

### مرآة الواجهة
`reporting-frontend/src/lib/format.ts` → `documentDefaultVisibilityByCategory` **مطابقة حرفيًّا** لخريطة الخادم (18 مدخلًا + احتياط `ClientScoped`)، مع توثيق صريح بأنّ «الخادم هو الفاصل في التطبيق».

**النتيجة:** ✅ **GO**

### ⚠️ مكتشف حاسم لـ UAT — FINDING-01
المستند الوحيد القائم على TEST تصنيفه `Contract` وسرّيته `Restricted`، لكنّه **سيحمل `VisibilityType = ClientScoped` بعد الهجرة** (لأنّ الافتراضيّ عند الإنشاء فقط، والهجرة تضع `defaultValue` للجميع). **لن يُقيَّد تلقائيًّا إلى `ManagementAndFinance`.**
هذا **سلوك توافق خلفيّ صحيح ومقصود** (لا يُغيَّر)، لكنّه **يجب أن يُبلَّغ للمالك** ويُعامَل يدويًّا عبر «تعديل سياسة الرؤية» إن أراد تقييده.

---

## §9 — مراجعة الفرض الخادميّ (Server-Side Authorization)

**المصدر:** `ClientDocumentService.cs` + `DocumentAccessEvaluator.cs` + `ClientDocumentsController.cs`.

| المسار | السطر | البوّابة | النتيجة عند المنع |
|---|---|---|---|
| **List** `GET /` | `58-93` | `AuthorizeReadAsync` ثمّ **`.Where(_evaluator.VisibleFilter(context))` داخل الاستعلام** | الصفّ **لا يُرجَع أصلًا** (فلترة SQL) |
| **Get** `GET /{documentId}` | `95-128` | `BuildContextAsync` + `Evaluate(...).CanViewMetadata` | `client_document.not_found` → **404** |
| **Download current** `GET /{id}/download` | `426-451` (`versionId=null`) | `Evaluate(...).CanDownload` **قبل** تحميل أيّ نسخة | `client_document.not_found` → **404** |
| **Download version** `GET /{id}/versions/{versionId}/download` | `426-451` (`versionId` مُمرَّر) | نفس البوّابة، نفس الموضع | `client_document.not_found` → **404** |
| **Version history** | `117-120` داخل `GetAsync` | لا تُقرأ النسخ **إلّا بعد** اجتياز `Evaluate` | لا يوجد مسار منفصل يتخطّى المقيّم |
| AddVersion / Update / SetArchived / Delete | `246`, `315`, `363`, `407` | `Evaluate(...).CanViewMetadata` | 404 |

### الأدلّة الحاسمة
1. **فلترة خادميّة لا واجهيّة:** `VisibleFilter` هو `Expression<Func<ClientDocument,bool>>` يُركَّب على `IQueryable` **قبل** `ToListAsync` (`ClientDocumentService.cs:73`) ⇒ يُترجَم إلى `WHERE`/`EXISTS` في SQL. الصفوف الممنوعة لا تصل إلى الذاكرة إطلاقًا.
2. **مصدر واحد للحقيقة:** كلّ مسارات المستند تمرّ عبر `IDocumentAccessEvaluator`؛ لا نسخة ثانية من منطق الصلاحيّة داخل List/Get/Download.
3. **الترتيب مُلزَم:** صلاحيّة العميل أوّلًا (`AuthorizeReadAsync`) ثمّ سياسة المستند (`Evaluate`) — موثّق في `IDocumentAccessEvaluator.cs:45`.
4. **مضادّ التعداد:** المنع يُرجِع **`client_document.not_found` (404)** لا 403 — في **كلّ** المسارات بلا استثناء.
5. **منع تسرّب البيانات الوصفيّة:** `AllowedRoles`/`AllowedUsers` لا تُحمَّل في القائمة إلّا لمن يملك صلاحيّة الإدارة (`ClientDocumentService.cs:75-77`).
6. **لا اعتماد على إخفاء الواجهة:** إخفاء الأزرار في الواجهة تجميليّ فقط؛ الفرض كلّه خادميّ.

### مصفوفة القرار (`DocumentAccessEvaluator.cs:54-67` — الذاكرة، و`72-97` — SQL؛ متطابقتان منطقيًّا)
| السياسة | الشرط |
|---|---|
| `Admin` | يتجاوز كلّ شيء (`Granted`) |
| `ClientScoped` | `HasClientScopeAccess` |
| `ManagementOnly` | `IsManagement` |
| `ManagementAndFinance` | `IsManagement \|\| IsFinance` |
| `FinanceOnly` | `IsFinance` |
| `HRManagementOnly` | `IsHrManagement` |
| `ProjectTeam` | `IsProjectTeam` |
| `CustomRoles` | تقاطع أدوار المستخدم مع `AllowedRoles` (غير حسّاس لحالة الأحرف في الذاكرة) |
| `CustomUsers` | `AllowedUsers` يحوي `UserId` |
| غير معروف | `false` (رفض افتراضيّ) |

> **ملاحظة تصميميّة موثّقة:** المالية/الموارد البشريّة تصل إلى مستندات أيّ عميل بحكم الوظيفة، لكنّها **لا تكتسب `HasClientScopeAccess`** ⇒ **لا ترى `ClientScoped`**، بل ما تسمح به سياستها فقط (`DocumentAccessEvaluator.cs:99-114`).

**النتيجة:** ✅ **GO**

---

## §10 — مراجعة مسارات Client 360 والأدوار

**المصدر:** `reporting-frontend/src/App.tsx` (الدلتا الفعليّة).

```
- const EXEC_ROLES: Role[] = ['Admin','CEO','GeneralManager','Manager','TeamLeader','CeoSupport','Viewer'];   ← لم يتغيّر
+ const CLIENT_360_ROLES: Role[] = [...EXEC_ROLES, 'AccountPortfolioReader'];                                 ← جديد
- { path: '/app/clients',            roles: EXEC_ROLES }   →  + roles: CLIENT_360_ROLES
- { path: '/app/clients/:clientId',  roles: EXEC_ROLES }   →  + roles: CLIENT_360_ROLES
  { path: '/app/projects',           roles: EXEC_ROLES }   ← لم يتغيّر
  { path: '/app/projects/:projectId',roles: EXEC_ROLES }   ← لم يتغيّر
```

| البند | النتيجة |
|---|---|
| `AccountPortfolioReader` أُضيف إلى `CLIENT_360_ROLES` | ✅ |
| `AccountPortfolioReader` **لم** يُضَف إلى `EXEC_ROLES` | ✅ (`EXEC_ROLES` byte-identical) |
| `/app/clients` و`/app/clients/:clientId` مفتوحان له | ✅ |
| `/app/projects` و`/app/projects/:projectId` **لم تُوسَّع** | ✅ |
| لوحات تنفيذ الفريق **لم تُوسَّع** | ✅ — اختبار حارس قائم: `DashboardShell.execution.nav.test.tsx:68` «مدير الحساب (AccountPortfolioReader) لا يرى لوحة تنفيذ الفريق» |
| كلّ استخدامات `EXEC_ROLES` الأخرى (teams, workflows, analytics, sales-aggregation, reports) | ✅ بلا تغيير |

> **ملاحظة:** `EXECUTION_REPORTS_ROLES` كان يحوي `AccountPortfolioReader` **قبل CPW-R2** (RC4-Task4) — ليس من دلتا هذه التذكرة.

**النتيجة:** الحارس المعماريّ محفوظ. ✅ **GO**

---

## §11 — دلتا التسمية

| الملفّ | التغيير |
|---|---|
| `lib/format.ts` | `AccountPortfolioReader: 'محفظة مدير الحساب'` → **`'محفظة عملائي'`** (سطر واحد) |
| `lib/navConfig.ts` | كلمات بحث تبويب «مشاريع عملائي»: `'محفظة مدير الحساب …'` → `'محفظة عملائي مدير العميل …'` (سطر واحد) |
| `App.tsx` | تعليق توضيحيّ فقط |

| المقياس | القيمة |
|---|---|
| أسطر محذوفة تحوي «مدير الحساب» في الواجهة | 10 |
| أسطر مضافة تحوي «مدير العميل» | 13 |
| **«مدير الحساب» ما زالت موجودة في `reporting-frontend/src` عند HEAD** | **27 موضعًا في 10 ملفّات** |

**الحكم:** التغيير **محصور في نطاق العميل/المحفظة** (تسمية الدور + كلمات بحث التبويب). **ليس إعادة تسمية شاملة** — الدليل أنّ 27 موضعًا باقية بلا مساس (تعليقات، `types/api.ts`، صفحات المحفظة/التنفيذ، اختبارات). ✅ **GO**

---

## §12 — خطّة النسخ الاحتياطيّ (تخطيط — لم تُنفَّذ)

**الطابع الزمنيّ المقترَح:** `TS = YYYYMMDD-HHMMSS` موحَّد لكلّ العناصر.

| # | العنصر | الطريقة المخطَّطة | وجهة الحفظ |
|---|---|---|---|
| 1 | قاعدة بيانات TEST | `pg_dump -Fc reporting_test_uat` (كـ`postgres`، إعادة توجيه stdout — لا `-f` داخل `/root`) | `/root/db-backups/reporting_test_uat-precpwr2-$TS.dump` |
| 2 | Backend publish | `cp -a /opt/reporting-test/publish` | `/opt/reporting-test/publish-backup-cpwr2-$TS` |
| 3 | Frontend dist | `cp -a /opt/reporting-test/frontend/dist` | `/opt/reporting-test/frontend/dist-backup-cpwr2-$TS` |
| 4 | ملفّ البيئة | `cp -a /etc/khubara-reporting-test.env` (600 root:root) | `/root/env-backups/khubara-reporting-test.env-$TS` |
| 5 | إعداد nginx | `cp -a /etc/nginx/sites-available/reporting-test` | `/root/nginx-backups/reporting-test-$TS` |
| 6 | مرفوعات قائمة (خدمات الموظّف) | `tar -czf` للمسار المُعرَّف في `EmployeeServiceFinalDocumentsPath` | `/root/storage-backups/employee-docs-$TS.tgz` |
| 7 | مساحة مستندات العملاء | `tar -czf` لـ`/var/lib/reporting-test/documents` (يحافظ على الأذونات) | `/root/storage-backups/client-documents-$TS.tgz` |
| 8 | **Storage Manifest** | أدناه — **لم يعد N/A** | `/root/storage-backups/manifest-$TS.tsv` |
| 9 | سجلّ الهجرات | `psql -tAc 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1'` | `/root/db-backups/migrations-test-$TS.txt` |
| 10 | حالة الخدمة + البصمات | `systemctl show` + `sha256sum` للـ4 DLLs + الحزمة + env + nginx | `/root/state-$TS.txt` |

### طريقة مطابقة Storage Manifest (مُثبَتة عمليًّا في §5)
1. **من القاعدة:**
   `SELECT v."Id", v."ClientDocumentId", v."VersionNo", v."StorageKey", v."SizeBytes", v."Sha256" FROM client_document_versions v ORDER BY v."StorageKey";`
2. **من القرص:** `find /var/lib/reporting-test/documents -type f -printf '%P\n' | sort` + لكلّ ملفّ `stat -c %s` و`sha256sum`.
3. **الربط:** `StorageKey` ↔ المسار النسبيّ من جذر التخزين (مفتاح المطابقة).
4. **التحقّق الرباعيّ لكلّ صفّ:** (أ) الملفّ موجود، (ب) `SizeBytes` == حجم القرص، (ج) `Sha256` == `sha256sum`، (د) `IsCurrent` يقابل نسخة واحدة فقط لكلّ مستند.
5. **اليتامى:** `comm -23` (صفّ بلا ملفّ) و`comm -13` (ملفّ بلا صفّ) — كلاهما **يجب أن يكون فارغًا**.
6. يُلتقَط الـManifest **مرّتين**: قبل النشر وبعده، ويُقارَنان.

**الحالة:** مخطَّط بالكامل، **لم يُنفَّذ أيّ نسخ**. ✅ **GO (خطّة)**

---

## §13 — استراتيجيّة مصدر البناء

| البند | القرار |
|---|---|
| المصدر الوحيد | **`3344f7800f223a97b2fd4429d92d8c3449f3cfd9` من `origin`** |
| الطريقة | `git fetch origin` ثمّ `git worktree add --detach /private/tmp/cpw-r2-build-<TS> 3344f78…` |
| **ممنوع** | البناء من شجرة العمل الرئيسيّة `/Users/…/Mrketing Experts syestem` (بها 24 ملفًّا مُعدَّلًا + عشرات الملفّات غير المتعقَّبة) |
| **ممنوع** | البناء من `/private/tmp/cpw-r1b2-20260807` (شجرة تطوير، قد تتلوّث) |
| التحقّق الإلزاميّ قبل البناء | `git rev-parse HEAD` == `3344f78…` ؛ `git status --porcelain` **فارغ** ؛ `git rev-list --count 1121e577..HEAD` == **6** ؛ `git diff --name-only 1121e577..HEAD \| wc -l` == **31** |
| التنظيف | `rm -rf` لكلّ `bin/` و`obj/` قبل البناء (شرط SourceLink) |

**النتيجة:** ✅ **GO (خطّة)**

---

## §14 — خطّة بناء Backend (تخطيط — لم يُنفَّذ)

```
export DOTNET_ROOT=$HOME/.dotnet && export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
cd <worktree>/reporting-backend
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet restore Reporting.sln
dotnet build   Reporting.sln -c Release            # مطلوب: 0 أخطاء
dotnet test    tests/Reporting.UnitTests           # متوقَّع 69/69
dotnet test    tests/Reporting.IntegrationTests --filter ClientDocumentVisibilityTests   # متوقَّع 21/21
dotnet test    tests/Reporting.IntegrationTests --filter "Client360|Document"            # متوقَّع 101/101
dotnet ef migrations has-pending-model-changes -p src/Reporting.Infrastructure -s src/Reporting.Api   # متوقَّع: No changes
dotnet publish src/Reporting.Api -c Release -o ./publish \
  -p:SourceRevisionId=3344f7800f223a97b2fd4429d92d8c3449f3cfd9 \
  -p:ContinuousIntegrationBuild=true
```

**التحقّق بعد النشر المحلّي:** الأربع DLLs تحمل `1.0.0+3344f780…` (بحث UTF-16LE عبر `strings -el`).

> **درس مُلزَم:** SourceLink لا يُضمَّن تلقائيًّا — يجب تمرير `-p:SourceRevisionId` صراحةً **بعد** حذف `bin`/`obj`، وإلّا بقيت البصمة القديمة.

**بوّابة:** أيّ فشل في أيّ سطر ⇒ **إيقاف فوريّ** ولا نشر.

---

## §15 — خطّة بناء Frontend (تخطيط — لم يُنفَّذ)

```
cd <worktree>/reporting-frontend
npm ci
npx tsc -b --noEmit            # متوقَّع 0 أخطاء
npx vitest run                 # متوقَّع 252/252 (30 ملفًّا)
VITE_API_BASE_URL=https://test.emarketingacademy.net/api npm run build
```

**التحقّق الإلزاميّ بعد البناء (قبل النقل):**
| الفحص | الشرط |
|---|---|
| `grep -c "https://test.emarketingacademy.net/api" dist/assets/*.js` | **≥ 1** |
| `grep -c "reports.emarketingacademy.net" dist/assets/*.js` | **0** (لا تسرّب Production) |
| `grep -c "rc.emarketingacademy.net" dist/assets/*.js` | **0** (لا تسرّب RC) |
| `grep -c "localhost:509" dist/assets/*.js` | **0** |

**تحذير مقبول وحيد:** `/*#__PURE__*/` في `@microsoft/signalr` (Rolldown) — حميد وموثَّق.

---

## §16 — تسلسل النشر على TEST (ترتيب فقط — لم يُنفَّذ)

| # | الخطوة | نقطة التراجُع |
|---|---|---|
| 1 | التقاط خطّ الأساس + Storage Manifest «قبل» | لا تغيير بعد |
| 2 | تنفيذ عناصر النسخ الاحتياطيّ العشرة (§12) والتحقّق من أحجامها | لا تغيير بعد |
| 3 | إعادة تأكيد `__EFMigrationsHistory` = 33 ورأس `20260807033602` | لا تغيير بعد |
| 4 | بناء Backend + Frontend محلّيًّا (§14/§15) واجتياز كلّ البوّابات | لا تغيير على الخادم |
| 5 | `rsync` للـ`publish` إلى مسار **staging** جديد (لا استبدال بعد) | حذف مجلّد الـstaging |
| 6 | `chown -R www-data:www-data` على staging + مقارنة بصمات DLLs مع المحلّي | حذف مجلّد الـstaging |
| 7 | إيقاف الخدمة → استبدال `publish` من staging | استعادة `publish-backup-cpwr2-$TS` |
| 8 | نقل `dist` الجديد (استبدال ذرّيّ) | استعادة `dist-backup-cpwr2-$TS` |
| 9 | تشغيل الخدمة — **الهجرة تُطبَّق تلقائيًّا عند الإقلاع** | استعادة publish + dist + (عند الحاجة) `pg_restore` |
| 10 | تحقّق: سجلّ الإقلاع يُظهر `Applying migration '20260809165617_ClientDocumentVisibility'` **مرّة واحدة فقط** | كما 9 |
| 11 | تحقّق: عدد الهجرات = **34**؛ الرأس = `20260809165617`؛ SourceLink الحيّ = `3344f780…` | كما 9 |
| 12 | تحقّق: Health داخليّ + عامّ = 200؛ `NRestarts` = 0؛ لا `fail:`/`crit:` | كما 9 |
| 13 | التقاط Storage Manifest «بعد» ومقارنته بـ«قبل» (**يجب أن يكونا متطابقين**) | كما 9 |
| 14 | تنفيذ الدخان (§18) ثمّ دخان مدير العميل (§19) | كما 9 |

---

## §17 — استراتيجيّة التراجُع

### التحليل الحاسم: هل نظام R1B2 القديم آمن مع `VisibilityType NOT NULL DEFAULT 'ClientScoped'`؟

| السيناريو | التحليل | الحكم |
|---|---|---|
| **INSERT** من كود R1B2 | نموذج EF في R1B2 لا يعرف الخاصّيّة ⇒ عبارة `INSERT` المولَّدة **لا تذكر العمود** ⇒ PostgreSQL يطبّق `DEFAULT 'ClientScoped'` ⇒ قيد `NOT NULL` مستوفًى | ✅ **آمن** |
| **SELECT** من كود R1B2 | EF يُسقِط قائمة أعمدة صريحة ⇒ الأعمدة الثلاثة الجديدة تُتجاهَل ببساطة | ✅ **آمن** |
| **UPDATE** من كود R1B2 | يحدّث الأعمدة المعروفة فقط ⇒ `VisibilityType` يبقى كما هو | ✅ **آمن** |
| الجدولان الجديدان | لا يعرفهما R1B2 إطلاقًا؛ لا FK منهما إلى ما هو مطلوب؛ FK إليهما Cascade من `client_documents` | ✅ **آمن** |
| **⚠️ الأثر الوظيفيّ** | أيّ مستند يُنشأ/يُعدَّل تحت R1B2 بعد التراجُع سيحمل `ClientScoped` — **تُفقَد نيّة التقييد** لتلك المستندات | ⚠️ **يُبلَّغ** |

**الخلاصة:** الهجرة إضافيّة بحتة و**متوافقة للأمام مع نظام R1B2** ⇒ **التراجُع التشغيليّ (Runtime Rollback) هو المُفضَّل**.

### المسار المُفضَّل — تراجُع تشغيليّ (بلا لمس القاعدة)
1. استعادة `publish-backup-cpwr2-$TS` → `/opt/reporting-test/publish` + `chown www-data`.
2. استعادة `dist-backup-cpwr2-$TS` → `/opt/reporting-test/frontend/dist`.
3. `systemctl restart khubara-reporting-test` + تحقّق Health.
4. **تُترَك `VisibilityType` و`client_document_allowed_roles` و`client_document_allowed_users` في مكانها** (خاملة، غير ضارّة، وتُستأنف فورًا عند إعادة النشر).

### المسار الاحتياطيّ — تراجُع القاعدة (عند تلف بيانات فقط)
- `pg_restore` من `reporting_test_uat-precpwr2-$TS.dump` **مع** استعادة **لقطة التخزين المتزامنة** `client-documents-$TS.tgz` — **الاثنان معًا إلزاميًّا** وإلّا انكسر الاتّساق بين الصفوف والملفّات.
- **ممنوع `dotnet ef database update <previous>` أو أيّ تشغيل تلقائيّ لـ`Down`** — العكس اليدويّ يُنفَّذ فقط بتصريح صريح ومنفصل.

**النتيجة:** ✅ **GO (خطّة)**

---

## §18 — خطّة الدخان (Smoke) — سيناريوهات A–E

> **تنبيه FINDING-02:** «عميل UAT ألفا» (`cc877dc2…`) لديه حاليًّا **صفر مستندات**. كلّ السيناريوهات أدناه تتطلّب **رفعًا جديدًا** بعد النشر.

| السيناريو | التصنيف عند الرفع | السياسة المتوقَّعة تلقائيًّا | يجب أن يراه | يجب ألّا يراه |
|---|---|---|---|---|
| **A** | `TechnicalProposal` | `ProjectTeam` | Admin، مدير العميل (AM)، مدير حساب المشروع، عضو الفريق المسؤول | `FinanceManager` (بلا صلاحيّة عميل) |
| **B** | `MarketingPlan` | `ProjectTeam` | نفس A | نفس A |
| **C** | `Contract` | `ManagementAndFinance` | Admin، الإدارة (`ClientCoreManagers`)، `FinanceManager` | مدير العميل إن لم يكن إدارة/مالية |
| **D** | `Invoice` | `ManagementAndFinance` | نفس C | نفس C |
| **E** | `Other` (عامّ) | `ClientScoped` | كلّ من له صلاحيّة على العميل (ومنهم AM) | `FinanceManager` (وظيفيّ لا صلاحيّة عميل) |

### مصفوفة التغطية الإلزاميّة لكلّ سيناريو
| # | الفحص | معيار النجاح |
|---|---|---|
| 1 | فلترة القائمة | المستند **غائب تمامًا** من `GET /clients/{id}/documents` لغير المصرَّح — لا مخفيّ بالواجهة |
| 2 | الوصول المباشر | `GET /clients/{id}/documents/{docId}` لغير المصرَّح ⇒ **404 `client_document.not_found`** (**ليس 403**) |
| 3 | تنزيل النسخة الحاليّة | `GET …/download` لغير المصرَّح ⇒ **404** |
| 4 | تنزيل نسخة محدّدة | `GET …/versions/{versionId}/download` لغير المصرَّح ⇒ **404** |
| 5 | سجلّ النسخ | لا يُعرَض إطلاقًا لغير المصرَّح (داخل `GetAsync` المحميّ) |
| 6 | الأرشفة | `POST …/delete` و«أرشفة» لغير المصرَّح ⇒ 404؛ المؤرشَف لا يظهر إلّا بـ`IncludeArchived` |
| 7 | `CustomRoles` | ضبط السياسة على أدوار محدّدة ⇒ يراه حاملو الدور فقط؛ اسم دور خارج `Roles.All` ⇒ **مرفوض** |
| 8 | `CustomUsers` | ضبط السياسة على مستخدم واحد ⇒ يراه هو فقط؛ الباقون 404 |
| 9 | التوافق الخلفيّ | المستند القائم (`Contract`/`Restricted`) يظهر بـ`ClientScoped` ولا يُفقَد ولا يتعطّل تنزيله |
| 10 | التجاوز اليدويّ | تغيير السياسة يدويًّا ثمّ **تغيير التصنيف** ⇒ السياسة اليدويّة **لا تُدهَس** |
| 11 | Admin | يرى الكلّ في كلّ السيناريوهات |
| 12 | اتّساق التخزين | Manifest بعد الدخان == Manifest قبله + الرفوعات الجديدة فقط |

---

## §19 — دخان Client 360 لمدير العميل (AM)

| # | الخطوة | معيار النجاح |
|---|---|---|
| 1 | تسجيل الدخول `account.manager@uat.local` | 200، الدور `AccountPortfolioReader` |
| 2 | فتح «محفظة عملائي» | تظهر التسمية الجديدة «محفظة عملائي» لا «محفظة مدير الحساب» |
| 3 | `/app/clients` | **يُفتَح** (`CLIENT_360_ROLES`) وتظهر عملاؤه فقط |
| 4 | `/app/clients/cc877dc2…` | يُفتَح ملفّ «عميل UAT ألفا» |
| 5 | البيانات الأساسيّة | تُعرَض **قراءة فقط** |
| 6 | جهات الاتّصال | تُعرَض |
| 7 | القنوات | تُعرَض |
| 8 | الهويّة البصريّة (Brand) | تُعرَض |
| 9 | المشاريع | تُعرَض مشاريع العميل |
| 10 | المستندات | تُعرَض **المرشَّحة خادميًّا** فقط (حسب §18) |
| 11 | الروابط الخارجيّة | تُعرَض |
| 12 | **أزرار التحرير الأساسيّ** | **مخفيّة** (`ClientCoreManagers` حصرًا) |
| 13 | **API التحرير الأساسيّ** | استدعاء مباشر ⇒ **مرفوض خادميًّا** (لا يُعتمَد على الإخفاء) |
| 14 | `/app/projects` | **مغلق** (403/إعادة توجيه) — `EXEC_ROLES` لم تُوسَّع |
| 15 | لوحة تنفيذ الفريق | **لا تظهر** في التنقّل ولا تُفتَح |

---

## §20 — قائمة تحقّق المالك (UAT) — 12 خطوة

1. سجّل الدخول بحساب **مدير العميل** وافتح «محفظة عملائي» — تأكّد من التسمية الجديدة.
2. افتح ملفّ **عميل UAT ألفا** وتصفّح: البيانات الأساسيّة، جهات الاتّصال، القنوات، الهويّة البصريّة، المشاريع.
3. تأكّد أنّك **لا ترى أزرار تعديل البيانات الأساسيّة** (التحرير للإدارة فقط).
4. تأكّد أنّ **«المشاريع» العامّة ولوحة تنفيذ الفريق غير متاحتين** لك.
5. ارفع مستندًا تصنيفه **«عرض فنّي»** — تأكّد أنّ السياسة المقترَحة تلقائيًّا هي **«فريق المشروع»**.
6. ارفع مستندًا تصنيفه **«عقد»** — تأكّد أنّ السياسة المقترَحة هي **«الإدارة والمالية»**.
7. ارفع مستندًا تصنيفه **«أخرى»** — تأكّد أنّ السياسة هي **«كل من لديه صلاحية على العميل»**.
8. سجّل الدخول بحساب **المدير المالي** — تأكّد أنّك ترى **العقد** ولا ترى **العرض الفنّي**.
9. من حساب المالي، جرّب فتح رابط العرض الفنّي مباشرةً — يجب أن تظهر رسالة **«المستند غير موجود»** (لا «ممنوع»).
10. غيّر سياسة أحد المستندات يدويًّا إلى **«أشخاص محددون»** واختر شخصًا — تأكّد أنّ غيره لا يراه إطلاقًا.
11. غيّر **تصنيف** ذلك المستند — تأكّد أنّ **سياستك اليدويّة لم تتغيّر**.
12. افتح المستند القديم (**العقد القائم**) — تأكّد أنّه ما زال يعمل، **وقرّر يدويًّا** إن أردت تقييده إلى «الإدارة والمالية» (لن يُقيَّد تلقائيًّا — راجع FINDING-01).

---

## §21 — خطّ أساس Production و RC (قراءة فقط)

| البند | **Production** | **RC** |
|---|---|---|
| الخدمة | `reporting-api` | `khubara-reporting-rc` |
| ActiveState | `active/running` | `active/running` |
| MainPID | **654185** | **647747** |
| NRestarts | **0** | **0** |
| بدء التشغيل | `Fri 2026-08-07 08:57:45 UTC` | `Fri 2026-08-07 07:07:39 UTC` |
| القاعدة | `reporting_prod` | `reporting_rc` |
| عدد الهجرات | **30** | **30** |
| الرأس | `20260724224053_AddReportApproverAndKpiReviewerOverrides` | نفسه |
| جدول `client_documents` | **غير موجود** | **غير موجود** |
| SourceLink (الأربع DLLs) | `1.0.0+ce166662f46598ed3593beed0105ba67059fc3bc` | نفسه |
| الحزمة | `index-CG2a9RiH.js` | `index-D5et8mMC.js` |
| CSS | `index-COKFKQO9.css` | `index-COKFKQO9.css` |

**لم يُمَسّ أيّ منهما.** ✅

---

## §22 — القيود والمخاطر المعروفة

| # | القيد | الأثر | الحالة |
|---|---|---|---|
| **L-01** | **العرض == التنزيل في v1** — `DocumentAccess.CanViewMetadata == CanDownload` (`IDocumentAccessEvaluator.cs:8`) | لا فصل بين رؤية البيانات الوصفيّة وتنزيل الملفّ | مقصود وموثَّق |
| **L-02** | **`CustomUsers` بلا FK** إلى جدول المستخدمين | حذف مستخدم لا يُنظِّف الصفوف تلقائيًّا ⇒ صفوف خاملة | مقبول في v1 |
| **L-03** | **عقد رؤية المشاريع الحاليّ محفوظ** — `/app/projects` وEXEC_ROLES لم تُوسَّع | مدير العميل يصل إلى Client 360 فقط | مقصود (حارس معماريّ) |
| **L-04** | **لا محرّك فحص فيروسات** — `ScanEngine=None`، `RequireCleanScanBeforeDownload=false` | الملفّات تُنزَّل بلا فحص (C-01 من CPW-R1B2) | قائم، خارج النطاق |
| **L-05** | **`BASELINE-DEFECT-01`** — `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete` | فشل أساسيّ قائم على `c157829` | تذكرة مستقلّة |
| **L-06** | **`BASELINE-DEFECT-02`** — `EmployeeProfileScopeTests.Profile_Summary_Reflects_Submitted_Kpi` | فشل أساسيّ قائم | تذكرة مستقلّة |
| **L-07** | **نَسَب Production مُنحرِف عن نَسَب المستودع** — تفصيله أدناه | **حاجز صلب أمام أيّ نشر إنتاجيّ** | **NO-GO ثابت** |
| **L-08** | **UAT-PREP-01** — صفر مستخدم بدور `Accountant` | فرع `Accountant` في سياسة المالية غير مغطّى | تحضير UAT |
| **L-09** | **FINDING-01** — العقد القائم سيصبح `ClientScoped` لا `ManagementAndFinance` | يحتاج ضبطًا يدويًّا إن أُريد التقييد | يُبلَّغ للمالك |
| **L-10** | **FINDING-02** — عميل UAT ألفا بصفر مستندات | كلّ سيناريوهات الدخان تتطلّب رفعًا جديدًا | إجرائيّ |

### تفصيل L-07 — انحراف نَسَب Production
مقارنة `__EFMigrationsHistory` على `reporting_prod` (30) مع مجلّد الهجرات في المستودع @ `3344f78` (34):

**في المستودع وليست على Production (9):**
`20260620001156_FlexiblePositionsPhase1A`, `20260622140138_KpiTemplateAssignmentsPhaseT1`, `20260626124527_AddReportViewGrants`, `20260708232456_AddExecutionTaxonomyCatalog`, `20260709222126_AddProjectWorkstreams`, `20260709231845_AddWorkstreamDeliverables`, `20260712211952_AddClient360Foundation`, `20260807033602_ClientDocumentsAndExternalLinks`, `20260809165617_ClientDocumentVisibility`

**على Production وليست في المستودع (5):**
`20260622144900_KpiTemplateAssignmentsPhaseT1`, `20260626135944_AddReportViewGrants`, `20260715162851_AddBypassTeamLeaderApproval`, `20260716015239_KpiEvaluationPartialUniqueIndex`, `20260724224053_AddReportApproverAndKpiReviewerOverrides`

> **ملاحظتان حرجتان:** (أ) `KpiTemplateAssignmentsPhaseT1` و`AddReportViewGrants` موجودتان **بمعرّفَين زمنيَّين مختلفَين** على الجانبين — نفس الميزة بهجرتَين متمايزتَين. (ب) المشترك = 25 هجرة فقط.
> **الاستنتاج:** لا يمكن تطبيق نَسَب المستودع على Production بلا خطّة توفيق مستقلّة ومُصرَّح بها. **هذا وحده NO-GO ثابت للإنتاج.**

---

## §23 — البوّابة النهائيّة

| # | البند | الحكم |
|---|---|---|
| 1 | **TEST target** — البيئة الصحيحة ومعزولة ومعروفة الحالة | ✅ **GO** |
| 2 | **Migration lineage** — نَسَب سليم، هجرة معلَّقة واحدة، سَلَفها مطبَّق، لا رأس متوازٍ | ✅ **GO** |
| 3 | **Migration safe** — إضافيّة بحتة، `defaultValue` يمنع أيّ Backfill، لا فهرس فريد على بيانات قائمة | ✅ **GO** |
| 4 | **Storage consistency** — 1 صفّ ↔ 1 ملفّ، بصمة وحجم متطابقان، صفر يتيم في الاتّجاهين | ✅ **GO** |
| 5 | **Backup plan** — 10 عناصر + Storage Manifest بطريقة مطابقة رباعيّة مُثبَتة | ✅ **GO (خطّة)** |
| 6 | **AM UAT readiness** — الحساب والعميل والمشاريع جاهزة | ✅ **GO** |
| 7 | **Finance UAT readiness** — `FinanceManager` جاهز؛ `Accountant` = 0 مستخدم | ⚠️ **GO مشروط (UAT-PREP-01)** |
| 8 | **Document permission behavior** — الخريطة 18/18، افتراضيّ عند الإنشاء فقط، التجاوز اليدويّ محفوظ، الفرض خادميّ، المنع 404 | ✅ **GO** |
| 9 | **Rollback** — تراجُع تشغيليّ مُفضَّل، توافق R1B2 مع العمود الجديد مُثبَت تحليليًّا، تراجُع قاعدة+تخزين متزامن كاحتياط | ✅ **GO** |
| 10 | **Smoke plan** — A–E + 12 فحصًا لكلّ سيناريو + دخان AM 15 خطوة + قائمة مالك 12 خطوة | ✅ **GO** |
| 11 | **Safe to request TEST deployment execution** | ✅ **GO** — آمن **طلب** التصريح بالتنفيذ |
| 12 | **Safe to deploy TEST now** | ⛔ **NO-GO** (لا تصريح تنفيذ) |
| 13 | **Safe for Production** | ⛔ **NO-GO** (L-07 انحراف النَّسَب + BASELINE-DEFECT-01/02 + لا تصريح) |

### سلسلة القرار
```
CPW-R2 TEST DEPLOYMENT PLANNING — READ ONLY / NO EXECUTION /
LINEAGE: GO / MIGRATION SAFE: GO / STORAGE CONSISTENT: GO /
PERMISSIONS VERIFIED: GO / ROLLBACK: GO /
SAFE TO REQUEST TEST DEPLOYMENT: GO /
DEPLOY TEST NOW: NO-GO / PRODUCTION: NO-GO
```

---

## المكتشفات الثلاثة الواجب إبلاغها

1. **FINDING-01 (وظيفيّ):** المستند القائم `Contract`/`Restricted` **سيصبح `ClientScoped`** بعد الهجرة — الافتراضيّ يُطبَّق عند الإنشاء فقط. سلوك توافق خلفيّ صحيح، لكنّه يحتاج ضبطًا يدويًّا إن أُريد تقييده.
2. **FINDING-02 (إجرائيّ):** «عميل UAT ألفا» بصفر مستندات ⇒ كلّ سيناريوهات الدخان A–E تتطلّب **رفعًا جديدًا** بعد النشر.
3. **UAT-PREP-01 (تحضير):** دور `Accountant` **موجود بصفر مستخدمين** ⇒ فرع `Accountant` من سياسة المالية غير مغطّى. متطلّب تحضير UAT فقط — **لم يُنشأ أيّ حساب**.

---

## التوقّف المُلزَم

**ممنوع بلا تصريح جديد وصريح:** أيّ Deploy أو Restart أو Migration أو Backup فعليّ أو Build أو Commit أو Push أو Merge أو PR أو Tag أو كتابة على TEST/RC/Production أو قاعدة بيانات أو مساحة تخزين، أو إصلاح `BASELINE-DEFECT-01/02`، أو إنشاء أيّ حساب UAT، أو بدء Project Workspace.
