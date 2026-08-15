# CLIENT-360-UAT-READONLY-DIAGNOSTIC-R1 — تقرير تشخيص قراءة-فقط

**التاريخ:** 6 أغسطس 2026 · **النطاق:** كود Client 360 (`CPW-R1B`، آخر commit `6859ee0`) + بيئة TEST/UAT
**القرار العامّ:** `READ-ONLY DIAGNOSTIC COMPLETE — NO CHANGE EXECUTED`

> **إثبات عدم المساس:** صفر تعديل كود · صفر تعديل قاعدة بيانات · صفر إنشاء/حذف مستخدم · صفر تعديل Roles/JobRoles · صفر نقل بيانات Production · صفر Migration · صفر Seeder · صفر Restart · صفر Deploy · صفر Commit/Push · صفر مساس بـRC أو Production · صفر طباعة كلمات مرور/رموز/سلاسل اتصال.
> الأدوات المستخدَمة حصرًا: `Read` / `Grep` / `Glob` / `git` محلّي / بناء واختبارات محلّية / `SELECT` و`systemctl is-active` و`hostname` عبر SSH.

---

## §1 — توثيق نتائج اختبار المالك على TEST

### 1.1 ما نجح فعليًّا (مؤكَّد من المالك ومسنود ببيانات القاعدة)

| # | البند | الحالة | الشاهد في القاعدة الحيّة (`reporting_test_uat`) |
|---|---|---|---|
| 1 | إنشاء عميل جديد | ✅ نجح | `clients` = 3 صفوف |
| 2 | حفظ البيانات الأساسية بعد الخروج والعودة | ✅ نجح | الصفوف باقية بعد إعادة الفتح |
| 3 | تعديل جهة اتصال | ✅ نجح | `client_contacts` = 2 |
| 4 | تغيير جهة الاتصال الأساسية ⟵ إلغاء الأساسيّة السابقة تلقائيًّا | ✅ نجح | **جهة أساسية نشطة واحدة فقط** + فهرس `IX_client_contacts_ClientId_ActivePrimary` الفريد الجزئيّ قائم |
| 5 | إضافة قناة رقمية | ✅ نجح | `client_digital_channels` = 2 |
| 6 | حفظ وتعديل Brand Profile | ✅ نجح | `client_brand_profiles` = 1، و`PK` = فهرس فريد على `ClientId` ⇒ علاقة 1:1 حقيقية |
| 7 | إنشاء مشروع داخل العميل وظهوره في قائمة مشاريعه | ✅ نجح | `projects` = 4 (منها 1 بمدير حساب مُسنَد) |

### 1.2 الملاحظات المفتوحة (مؤكَّدة ومصنَّفة)

| # | الملاحظة | التصنيف الأوّليّ | القسم المرجعيّ |
|---|---|---|---|
| 1 | التحقّق من عدم تخزين الأسرار لم يُختبَر يدويًّا | ✅ **مُغطّى آليًّا** (6 تأكيدات في `Client360FoundationTests.cs`) | §7 |
| 2 | صلاحية مدير الحساب لم تُختبَر | ⚠️ **مؤكَّد بنيويًّا: مستحيل اختبارها على TEST حاليًّا** | §2 / §4 |
| 3 | لا يوجد رفع ملفات للعميل | **MISSING SCOPE** (لم يُصمَّم أصلًا) | §6 |
| 4 | المسمّى الوظيفي «مدير حسابات» غير ظاهر في شاشة إنشاء المستخدم | **UX Gap** (بنيويّ، ليس عيبًا) | §2 |
| 5 | المسمّيات الوظيفية في TEST تختلف عن Production | **مؤكَّد: TEST = صفر مسمّى، Production = 25 مسمّى** | §4 |
| 6 | بيانات الموظفين الكاملة غير موجودة على TEST | **مؤكَّد: 8 مستخدمين مقابل 33** | §4 |

**اكتشاف حاسم (يغيّر تفسير نجاح الاختبار):** العميل الوحيد على TEST الذي يحمل `AccountManagerId` مُسنَد إلى مستخدم **دوره `Admin` وبلا أيّ مسمّى وظيفيّ**. أي أنّ اختبار المالك مرّ عبر فرع الصلاحية `ClientCoreManagers` **ولم يمرّ إطلاقًا عبر فرع `AccountManagerId`**. لذلك «جاهزية بيانات مدير الحساب على TEST» = **NO-GO** (انظر البوّابة النهائية).

---

## §2 — تشخيص نموذج «مدير الحساب» (Account Manager)

### 2.1 الحقيقة البنيوية: ثلاثة محاور مستقلّة تمامًا

| المحور | الاسم التقنيّ | الموضع في الكود | الغرض |
|---|---|---|---|
| **1. دور أمنيّ (Security Role)** | `AccountPortfolioReader` | `Roles.cs:25`، ومجموعة `AccountPortfolioReaders = { AccountPortfolioReader, Admin }` في `Roles.cs:225-228` | فتح مسار القراءة `/api/account-portfolio` عبر سياسة `Policies.AccountPortfolioRead` (`Program.cs:105-106`) |
| **2. مسمّى وظيفيّ (Job Role)** | `Code = 'ACCOUNT_MGR'`، `NameAr = "مدير حسابات"` | `OrgSeeder.cs:433` و`tools/OrgImporter/OrgData.cs:113` | تصنيف تنظيميّ + **شرط ظهور عنصر التنقّل في الواجهة** (`navConfig.ts:181`) |
| **3. إسناد بالمورد (Assignment)** | `Client.AccountManagerId` / `Project.AccountManagerId` | كيانات `Clients` / `Projects` | **مصدر صلاحية الكتابة الفعليّة** على جهات الاتصال والقنوات والبراند |

**لا يوجد دور ASP.NET اسمه حرفيًّا `AccountManager`.** هذا هو منشأ الالتباس بالكامل.

### 2.2 هل يستطيع مستخدم بدور `Employee` أن يكون مدير حساب؟

**نعم — تمامًا، وبلا أيّ دور إضافيّ**، للكتابة على الكيانات الفرعية. الدليل القاطع من الكود:

```csharp
// ClientContactService.cs:160-175  (ومطابق حرفيًّا في ClientBrandService.cs:93-95 و ClientDigitalChannelService.cs:145-147)
private async Task<(string message, string code)?> AuthorizeWriteAsync(Guid clientId, Guid uid, CancellationToken ct)
{
    var client = await _db.Clients.AsNoTracking()
        .Select(c => new { c.Id, c.AccountManagerId })
        .FirstOrDefaultAsync(c => c.Id == clientId, ct);
    if (client is null) return ("العميل غير موجود.", "client.not_found");

    if (client.AccountManagerId == uid) return null;          // ← لا يُشترَط أيّ دور إطلاقًا

    if (_currentUser.IsInAnyRole(Roles.ClientCoreManagers))
    {
        var vis = await _access.ResolveAsync(ct);
        if (vis.CanViewClient(clientId)) return null;
    }
    return ("لا تملك صلاحية إدارة بيانات هذا العميل.", "auth.forbidden");
}
```

### 2.3 عدم تماثل التفويض بين النواة والأبناء (تصميم مقصود، مُوثَّق)

| المورد | الحارس | الملفّ والسطر |
|---|---|---|
| **نواة العميل** (إنشاء/تعديل/أرشفة/إعادة تفعيل/حذف) | `[Authorize(Policy = Policies.ClientCoreManagement)]` على 5 دوالّ | `ClientsController.cs:36,41,46,51,56` |
| **جهات الاتصال / القنوات الرقمية / البراند** | **لا سياسة على أيّ دالّة** — فقط `[Authorize]` على مستوى الصنف | `ClientContactsController.cs:12` · `ClientDigitalChannelsController.cs:12` · `ClientBrandController.cs:12` |

⇒ كتابة الأبناء محكومة **100 % داخل الخدمة** (Resource-Based)، وهذا بالضبط ما يسمح لموظّف عاديّ هو مدير حساب العميل بالتعديل. وهو موثَّق صراحةً في `Roles.cs:50-60`:
> «كتابة الأبناء (جهات الاتصال/القنوات/البراند) لا تعتمد على هذه المجموعة وحدها بل على تفويض بالمورد داخل الخدمة.»

`ClientCoreManagers = { Admin, Ceo, GeneralManager, Manager }` — **يستثني `TeamLeader` عمدًا**.

### 2.4 ما الذي يُظهر `/app/account-portfolio`؟ (تعارض واجب الإبلاغ)

| الطبقة | الشرط | الملفّ |
|---|---|---|
| ظهور عنصر التنقّل | `ctx.jobRoleCode === 'ACCOUNT_MGR'` | `navConfig.ts:181` |
| حارس المسار في الواجهة | `ACCOUNT_PORTFOLIO_ROLES = ['AccountPortfolioReader', 'Admin']` | `App.tsx:88-89, 155-157` |
| سياسة الخادم | `Roles.AccountPortfolioReaders` | `Program.cs:105-106` |

**⇒ عيب اتّساق (UX/BUG خفيف):**
- مستخدم يحمل الدور `AccountPortfolioReader` بلا المسمّى `ACCOUNT_MGR` ⟵ **لا يرى الرابط إطلاقًا** رغم أنّه مخوَّل.
- مستخدم يحمل المسمّى `ACCOUNT_MGR` بلا الدور ⟵ **يرى الرابط ثمّ يُصدم بـ403**.

هذا السلوك مُثبَّت حاليًّا في الاختبارات (`DashboardShell.portfolio.nav.test.tsx:77,84,91`) ⇒ أيّ إصلاح مستقبليّ يجب أن يشمل تحديث الاختبارات.

### 2.5 لماذا لا يظهر المسمّى في شاشة إنشاء المستخدم؟

| الدليل | النتيجة |
|---|---|
| `Grep` لـ`jobRole\|JobRole` داخل `UsersPage.tsx` | **No matches found** (صفر إشارة) |
| `CreateUserRequest(Email, FullName, Password, Roles, DepartmentId, TeamId, ManagerId)` | `DirectoryModels.cs:96-103` — **لا حقل `JobRoleId` إطلاقًا** |
| مسار الإسناد الحقيقيّ | شاشة منفصلة `JobRolesAssignmentPage.tsx` + `UpdateUserJobRoleRequest(Guid? JobRoleId, string? Notes)` (`DirectoryModels.cs:114-118`) |

**التصنيف النهائيّ: `UX Gap` (قابلية اكتشاف) — وليس `Bug`.** الوظيفة موجودة وتعمل، لكنّها في شاشة أخرى غير مربوطة بمسار إنشاء المستخدم، فيظنّ المستخدم أنّها غير موجودة.

### 2.6 هل `ACCOUNT_MGR` موجود فعلًا في قاعدة TEST؟

| السؤال | TEST (`reporting_test_uat`) | Production (`reporting_prod`) |
|---|---|---|
| هل `ACCOUNT_MGR` موجود؟ | **لا** | **نعم** |
| عدد المستخدمين المرتبطين به | **0** | **1** |
| إجمالي المسمّيات الوظيفية | **0** | **25 (كلّها نشطة)** |
| مستخدمون يحملون `AccountPortfolioReader` | **0** | **2** |

**⇒ صلاحية مدير الحساب غير قابلة للاختبار على TEST إطلاقًا في وضعها الحاليّ.** (لم يُنشأ ولم يُعدَّل أيّ مستخدم — قراءة فقط.)

### 2.7 الخلاصة التنفيذية لمدير حساب عامل بالكامل

| المتطلَّب | القيمة | إلزاميّ؟ |
|---|---|---|
| الدور الأمنيّ | `AccountPortfolioReader` | إلزاميّ لفتح `/api/account-portfolio` فقط |
| المسمّى الوظيفيّ | `ACCOUNT_MGR` | إلزاميّ **لظهور عنصر التنقّل** فقط |
| الإسناد | `Client.AccountManagerId = userId` | **إلزاميّ — وهو وحده مصدر صلاحية الكتابة على الأبناء** |
| دور Identity الأساسيّ | أيّ دور (حتى `Employee`) | غير مقيّد |

---

## §3 — مراجعة التسميات العربية + مقترح قرار التسمية (بلا تنفيذ)

### 3.1 الجرد الفعليّ للالتباس — كلمة «حساب» تحمل **ثلاثة معانٍ متضاربة**

**المعنى الأوّل — مدير حساب العميل (Account Management):**

| المصطلح المستخدَم | المواضع |
|---|---|
| «مدير الحساب» | `ClientsPage.tsx:123,293,386` · `ClientDetailPage.tsx:195,1090,1217` · `ProjectDetailPage.tsx:126,1049` · `App.tsx:63,64,88,154` · `TeamLeaderExecutionPage.tsx:33,234` · `api.ts:2084` |
| «مدير حسابات» *(صيغة مختلفة!)* | `ProjectDetailPage.tsx:79` + `NameAr` للمسمّى `ACCOUNT_MGR` في `OrgSeeder.cs:433` و`OrgImporter/OrgData.cs:113` |
| «محفظة مدير الحساب» | `AccountPortfolioPage.tsx:7` · `App.tsx:88` |
| «محفظة الحسابات» *(ثالث!)* | `RoleHomeDashboards.tsx:595,605` |
| «لوحة محفظة العملاء» *(رابع!)* | `HomePage.tsx:89` |
| «صحّة الحسابات» *(غامض)* | `ClientsPage.tsx:254,260` |

**المعنى الثاني — حسابات المستخدمين (User Accounts):** «الحسابات الحسّاسة» (`GovernanceActionItemsPage.tsx:311` · `GovernanceEscalationsPage.tsx:326,768`) · «تفعيل/تعطيل الحسابات» (`HrEmployeesPage.tsx:79`).

**المعنى الثالث — المحاسبة والمالية:** «الإدارة المالية» / `FinanceManager` / `Accountant` (`HomePage.tsx:60-61`) + المسمّيان `ACCOUNTANT` («محاسب») و`FIN_MGR` («مدير مالي»).

**⇒ أخطر نقطة التباس على الإطلاق:** المسمّى الوظيفيّ `ACCOUNT_MGR` اسمه العربيّ **«مدير حسابات»** — وهو يُقرأ فوريًّا كـ«مدير الحسابات المالية»، بينما المقصود «مدير حسابات العملاء». وهذا هو السبب المباشر لعدم عثور المالك عليه.

### 3.2 مقترح قرار التسمية (Naming Decision Proposal — **لم يُنفَّذ**)

| الكيان | الاسم الحاليّ | **الاسم المقترَح** | المبرّر |
|---|---|---|---|
| المسمّى `ACCOUNT_MGR` | «مدير حسابات» | **«مدير حسابات العملاء»** | يفصل نهائيًّا عن المالية بأقلّ تغيير ممكن |
| الحقل `AccountManagerId` في الواجهة | «مدير الحساب» | **«مدير حساب العميل»** | توحيد الصيغة وإزالة الغموض |
| صفحة `/app/account-portfolio` | 4 مسمّيات مختلفة | **«محفظة عملائي»** (موحَّدة في المواضع الأربعة) | تسمية واحدة قابلة للتذكّر |
| «صحّة الحسابات» | غامض | **«صحّة العملاء»** | يزيل التداخل مع المالية |
| الدور `AccountPortfolioReader` | (لا اسم عربيّ) | **«اطّلاع على محفظة العملاء»** | تسمية عربيّة صريحة في مصفوفة الأدوار |
| «الحسابات الحسّاسة» | كما هي | **«حسابات المستخدمين الحسّاسة»** | يفصل معنى user-account |

### 3.3 الملفّات والشاشات المتأثّرة لو نُفِّذ المقترح لاحقًا

**Backend (تغيير بيانات وصفيّة فقط، بلا Migration بنيويّة):** `OrgSeeder.cs:433` · `tools/OrgImporter/OrgData.cs:113` · (اختياريًّا) صفّ `NameAr` للمسمّى في قاعدة الإنتاج عبر `UPDATE` محكوم.
**Frontend (نصوص عرض فقط):** `ClientsPage.tsx` · `ClientDetailPage.tsx` · `ProjectDetailPage.tsx` · `AccountPortfolioPage.tsx` · `RoleHomeDashboards.tsx` · `HomePage.tsx` · `App.tsx` · `TeamLeaderExecutionPage.tsx` · `lib/api.ts` · `navConfig.ts` (تعليقات).
**اختبارات تحتاج تحديثًا:** `DashboardShell.portfolio.nav.test.tsx` (إن تغيّر شرط الظهور أيضًا).

> **لم يُنفَّذ أيّ تغيير تسمية. المقترح فقط.**

---

## §4 — مقارنة TEST مقابل Production (قراءة فقط، بلا بيانات شخصية)

### 4.1 جدول الانحراف التنظيميّ

| المؤشّر | TEST (`reporting_test_uat`) | Production (`reporting_prod`) | الفجوة |
|---|---|---|---|
| المستخدمون (الإجمالي / النشط) | **8 / 8** | **33 / 32** | −25 |
| أدوار ASP.NET (العدد) | 12 | 12 | **متطابقة اسمًا** |
| **المسمّيات الوظيفية** | **0** | **25 (كلّها نشطة)** | **−25 (فجوة كاملة)** |
| مستخدمون لهم مسمّى وظيفيّ | **0** | 31 | −31 |
| `ACCOUNT_MGR` موجود / عدد مستخدميه | **لا / 0** | **نعم / 1** | فجوة حرجة |
| مستخدمو `AccountPortfolioReader` | **0** | **2** | −2 |
| الإدارات / الفِرق | 2 / 2 | 4 / 9 | −2 / −7 |
| مستخدمون لهم مدير مباشر | (سلسلة الـfixture فقط) | 30 | فجوة هيكل إداريّ |
| عدد الهجرات / الرأس | **31** / `20260712211952_AddClient360Foundation` | **30** / `20260724224053_AddReportApproverAndKpiReviewerOverrides` | **مسارَا هجرة متشعّبان** |
| نطاقات البريد | `uat.local` ×6، `marketingexperts.local` ×1، `gmail.com` ×1 | (لم تُعدَّد — تفاديًا للبيانات الشخصية) | — |

**توزيع الأدوار على TEST:** `Admin`=2 · `CEO`=1 · `GeneralManager`=1 · `Manager`=1 · `TeamLeader`=1 · `Employee`=2 · `AccountPortfolioReader`=**0**.

**جرد مسمّيات Production (الرمز | عدد المستخدمين):** `SALES_B2C`=4 · `CONTENT_WRITER`=2 · `DESIGNER`=2 · `SOCIAL_TL`=2 · `VIDEO_EDITOR`=2 · `ACCOUNTANT`=1 · **`ACCOUNT_MGR`=1** · `CEO`=1 · `FIN_MGR`=1 · `GM`=1 · `HR`=1 · `MEDIA_BUYER`=1 · `OCM`=1 · `PERF_LEAD`=1 · `SALES_B2B`=1 · `SALES_B2C_TL`=1 · `SALES_MGR`=1 · `SEO_ARTICLE_WRITER`=1 · `SEO_SPECIALIST`=1 · `SEO_TL`=1 · `SOCIAL_MOD`=1 · `WEB_DEV`=1 · `WEB_TL`=1 · `adminـassistant`=1 · `PLAN_MGR`=0.

> ⚠️ **عيب جودة بيانات مستقلّ في Production:** الرمز `adminـassistant` يحتوي على محرف **التطويل العربيّ `U+0640`** بدل الشرطة السفلية `_`. لن يُطابَق بأيّ بحث بالإنجليزية. **تذكرة مستقلّة مقترَحة، لم تُنفَّذ.**

### 4.2 تصنيف السبب — **A + B معًا، بطبقة D/E فوقهما**

| التصنيف | الحكم | الدليل القاطع |
|---|---|---|
| **A — TEST يستخدم UAT Fixture فقط** | ✅ **مؤكَّد** | `05-seed-uat-fixture.sh` ينشئ **6 مستخدمين حصرًا** (`ceo@ / gm@ / manager@ / lead@ / emp1@ / emp2@ .uat.local`)، وحمولة الإنشاء (سطر 241) **بلا حقل `jobRoleId` إطلاقًا**، و`Grep` لـ`AccountManager` في كامل `Ops/TestUatPreparation/` = **No matches found**. |
| **B — OrgSeeder لا يعمل في Staging** | ✅ **مؤكَّد بنيويًّا وتوثيقيًّا** | `Program.cs:208-210`: `if (app.Environment.IsDevelopment()) await OrgSeeder.SeedAsync(...)`. وقراءة حيّة: `ASPNETCORE_ENVIRONMENT=Staging` في `/etc/khubara-reporting-test.env` ⇒ **الشرط لا يتحقّق**. وموثَّق حرفيًّا في `Ops/TestUatPreparation/README.md:82`: «Catalog Seeders عملت … **OrgSeeder لم يعمل**». |
| **C — الهيكل لم يُنقل قطّ من Production** | ✅ صحيح كنتيجة، لكنّه **قرار مقصود** لا خلل — استراتيجية Option B: قاعدة UAT جديدة نظيفة. |
| **D — فرق Seeder/Migration** | ✅ **مؤكَّد جزئيًّا** | مسارَا هجرة **متشعّبان لا متأخّران**: TEST=31 هجرة برأس `AddClient360Foundation`، PROD=30 برأس `AddReportApproverAndKpiReviewerOverrides`. أي أنّ كلّ بيئة تحمل هجرات لا توجد في الأخرى. |
| **E — خلل مزامنة حقيقيّ** | ❌ **مستبعَد** | لا يوجد مسار مزامنة مصمَّم أصلًا بين البيئتين ⇒ لا يمكن أن يكون «معطوبًا». |

**الحكم النهائيّ: `A + B` هما السبب الجذريّ، و`D` عامل مضاعِف يجب مراعاته في أيّ نقل بيانات مستقبليّ.**

---

## §5 — تقييم نقل البيانات من Production إلى TEST (تقييم فقط — **لم يُنفَّذ**)

### 5.1 مصفوفة التقييم

| المعيار | **A — هيكل تنظيميّ فقط** | **B — نسخة Production مُنقّاة** | **C — Fixture UAT موسَّع** |
|---|---|---|---|
| **الخصوصية** | 🟢 عالية (لا بيانات حسّاسة، لا كلمات مرور، لا رموز) | 🔴 **الأخطر** — أيّ إغفال في التنقية = تسريب بيانات حقيقية | 🟢 **الأعلى** (لا بيانات حقيقية إطلاقًا) |
| **دقّة الاختبار** | 🟡 جيّدة (هيكل حقيقيّ، بلا حِمل تشغيليّ) | 🟢 الأعلى (طبق الأصل) | 🟡 متوسّطة (تحاكي ولا تطابق) |
| **التعقيد** | 🟡 متوسّط (25 مسمّى + 4 إدارات + 9 فِرق + شجرة مديرين) | 🔴 عالٍ جدًّا (تنقية بريد + تصفير كلمات مرور + حذف رموز + تعطيل بريد/إشعارات + تعتيم حسّاس) | 🟢 منخفض (توسيع سكربت قائم) |
| **خطر إرسال إشعارات** | 🟢 منخفض (لا بريد حقيقيّ يُنقَل) | 🔴 **حرج** — بريد إنتاجيّ داخل قاعدة تُقلِع بمجدول بريد قد يُرسِل فعليًّا | 🟢 معدوم (حارس `@uat.local` قائم في الـfixture) |
| **قابلية التكرار** | 🟢 عالية (أداة/سكربت حتميّ) | 🔴 منخفضة (كلّ نسخة تحتاج تنقية يدوية جديدة) | 🟢 الأعلى (Idempotent بحكم التصميم) |
| **Rollback** | 🟢 سهل (حذف ما أُدخِل بمعرّف دفعة) | 🔴 صعب (استعادة قاعدة كاملة) | 🟢 سهل (دالّة Cleanup موجودة سلفًا) |
| **Idempotency** | 🟢 قابل للتحقيق (get-or-create بالرمز `Code`) | 🔴 غير قابل عمليًّا | 🟢 مُحقَّق فعليًّا |
| **تعارض مسارات الهجرة (D)** | 🟡 يحتاج انتباهًا | 🔴 **مانع** — لا يمكن استعادة dump إنتاجيّ على قاعدة برأس هجرة مختلف | 🟢 غير متأثّر |

### 5.2 التوصية النهائية

> **الخيار المُوصى به: `C` أوّلًا، ثمّ `A` عند الحاجة الفعلية.**

**المبرّر:** الهدف المعلن هو اختبار **صلاحية مدير الحساب**، وهذا لا يحتاج نسخ ولا نقل بيانات إنتاجية إطلاقًا — يحتاج فقط: (1) بذر كتالوج المسمّيات الوظيفية الـ25 (أو `ACCOUNT_MGR` وحده)، (2) مستخدم fixture إضافيّ بدور `AccountPortfolioReader` ومسمّى `ACCOUNT_MGR`، (3) ربطه بـ`Client.AccountManagerId` لعميل واحد. كلّ ذلك ضمن `05-seed-uat-fixture.sh` بحارس `@uat.local` القائم، بلا أيّ بيانات حقيقية.

**`B` مرفوض:** تعارض مسارات الهجرة (§4.1) يجعله غير قابل للتنفيذ تقنيًّا أصلًا، فضلًا عن خطر الخصوصية وإرسال البريد.
**`A` يُحتفَظ به احتياطيًّا** لو ثبت لاحقًا أنّ سيناريوهات UAT تحتاج شجرة إدارية إنتاجية حقيقية — وحينها يُنقل الهيكل فقط (Departments/Teams/JobRoles/Manager tree) بلا مستخدمين حقيقيّين.

> **لم يُنفَّذ أيّ نقل بيانات.**

---

## §6 — تشخيص ملفات ومرفقات العميل

### 6.1 الوضع القائم

| السؤال | الإجابة المؤكَّدة |
|---|---|
| هل يوجد كيان `Document` / `Attachment` عامّ؟ | **لا — صفر كيان، صفر جدول** |
| هل يوجد جدول مرفقات في قاعدة TEST؟ | **لا.** (المطابقة الوحيدة في البحث كانت إيجابيّة كاذبة: `client_brand_pro**file**s` تحتوي حرفيًّا على `file`) |
| ما جداول العميل/المشروع الفعلية؟ | `clients` · `client_contacts` · `client_digital_channels` · `client_brand_profiles` · `projects` · `project_workstreams` — **لا غير** |
| هل توجد بنية رفع قابلة لإعادة الاستخدام؟ | **نعم، واحدة فقط** |

### 6.2 البنية الوحيدة القائمة (نمط قابل للاقتباس)

`EmployeeServiceRequestService.UploadFinalDocumentAsync` (`EmployeeServiceRequestService.cs:252`):

| الخاصّية | القيمة | الملاءمة لملفّات العميل |
|---|---|---|
| الحدّ الأقصى | `10 MB` (سطر 22) | ✅ صالح كنقطة بدء |
| النوع المسموح | **PDF حصرًا** (سطر 264) | ❌ غير كافٍ (لوجوهات/صور/Brand Guidelines) |
| مسار التخزين | `App_Data/employee-service-requests/final-documents` أو `_storage.EmployeeServiceFinalDocumentsPath` (سطور 57-60) | ✅ **خارج جذر الويب** — نمط صحيح يُحتذى |
| البيانات الوصفية | عمودان على الكيان نفسه: `HrAttachmentPath` + `HrAttachmentContentType` (سطر 286) | ❌ غير كافٍ |
| العدد | **ملفّ واحد لكلّ سجلّ** | ❌ غير كافٍ |
| الإصدارات (Versioning) | **لا يوجد** | ❌ فجوة |
| التصنيف (Classification) | **لا يوجد** | ❌ فجوة |
| التنزيل المحكوم | `DownloadFinalDocumentAsync` بحارس `auth.forbidden` (سطر 311) | ✅ نمط صحيح يُحتذى |

### 6.3 ما يملكه Client 360 اليوم = **ثلاثة حقول نصّية للروابط فقط**

| الحقل | الملفّ |
|---|---|
| `Client.Website` | `Client.cs:44` |
| `ClientBrandProfile.BrandGuidelinesUrl` | `ClientBrandProfile.cs:45` |
| `ClientDigitalChannel.ProfileUrl` | `ClientDigitalChannel.cs:25` |

### 6.4 تحليل الفجوة مقابل احتياج المالك

| الاحتياج | مغطّى؟ | التصنيف |
|---|---|---|
| العرض الفنّي · خطة التسويق · العقد | ❌ | **MISSING SCOPE** |
| Brand Guidelines (ملفّ) | ⚠️ **رابط فقط** لا ملفّ | **MISSING SCOPE** |
| الصور واللوجوهات | ❌ | **MISSING SCOPE** |
| مستندات Access Handover | ❌ | **MISSING SCOPE** |
| محاضر الاجتماعات | ❌ | **MISSING SCOPE** |
| روابط Google Drive / OneDrive | ⚠️ لا حقل مخصّص (يمكن حشرها في `Notes` — ممارسة سيّئة) | **MISSING SCOPE** |
| ملفّات المشروع | ❌ | **MISSING SCOPE** |

**التصنيف النهائيّ: `MISSING SCOPE` قطعًا — وليس `Bug`.** لم يُصمَّم في `CPW-R1B` أصلًا، ولا يوجد أثر له في الكيانات ولا في الـDTOs ولا في الواجهة ⇒ لا شيء «معطوب».

### 6.5 نطاق مستقلّ مقترَح: `CPW-R1B2 — Client Documents & Important Links Foundation`

**العنوان:** أساس مستندات العميل والروابط المهمّة.
**الخطوط العريضة فقط (بلا تصميم تفصيليّ — ممنوع قبل الاعتماد):**
1. **المسار السريع (روابط):** كيان `ClientImportantLink` (النوع/العنوان/الرابط/المسؤول/تاريخ آخر مراجعة) بحارس `ClientFieldGuards.IsValidUrl` + `AnyContainsSecret` القائمَين. **لا رفع ملفّات، لا تخزين، مخاطر شبه معدومة.**
2. **المسار الكامل (مستندات):** كيان `ClientDocument` (التصنيف/الاسم/المسار/النوع/الحجم/الرافع/التاريخ/الإصدار) مقتبِسًا نمط `UploadFinalDocumentAsync` مع توسيع الأنواع المسموحة والتخزين خارج جذر الويب، وتفويض بالمورد مطابق لـ`AuthorizeWriteAsync`.
3. **الحوكمة:** نفس حارس الأسرار، تدقيق على كلّ رفع/حذف، تنزيل محكوم بالنطاق.

> **لم يُبدأ أيّ تصميم تفصيليّ ولا تنفيذ — كما يقتضي التذكرة.**

---

## §7 — تشخيص حفظ بيانات دخول السوشيال ميديا

### 7.1 التأكيد القاطع: النظام لا يخزّن أيّ سرّ

**فحص الكيانات:** `ClientDigitalChannel` يحوي `ProfileUrl` و`AccessStatus` و`PreferredContactMethodCode` ونحوها — **صفر حقل** للكلمة/الرمز/المفتاح/الكوكي/رمز الاسترداد. الأمر نفسه في `Client` و`ClientContact` و`ClientBrandProfile`.

**فحص الحارس:** `ClientFieldGuards.cs` — قائمة سوداء من **24 مصطلحًا** تُفحَص بلا حساسية لحالة الأحرف:
`password, passwd, pwd, كلمة المرور, كلمة السر, secret, apikey, api key, api_key, access token, accesstoken, access_token, "bearer ", refresh token, refresh_token, client secret, client_secret, recovery code, recoverycode, recovery_code, cookie, otp code, 2fa code`
بالإضافة إلى `IsValidUrl` (يقبل الفارغ، ويشترط `http`/`https` مطلقًا well-formed).

### 7.2 التغطية الآلية (تُغلق ملاحظة المالك «لم يُختبَر يدويًّا»)

| التأكيد | الملفّ:السطر |
|---|---|
| `client.secret_forbidden` | `Client360FoundationTests.cs:138` |
| `client_contact.secret_forbidden` | `:223` |
| `client_channel.profile_url_invalid` | `:274` |
| `client_channel.secret_forbidden` | `:285` |
| `client_brand.guidelines_url_invalid` | `:334` |
| `client_brand.secret_forbidden` | `:345` |

⇒ **الحُرّاس الأربعة للأسرار والاثنان للروابط مغطّاة آليًّا وناجحة (25/25).**

### 7.3 قيدان يجب الإفصاح عنهما بأمانة

1. **الحارس استدلاليّ بالكلمات المفتاحية (Heuristic).** لن يلتقط قيمة كلمة مرور خامّة مثل `Xk92!aQz` لأنّها لا تحوي أيًّا من الـ24 مصطلحًا. الحماية فعّالة ضدّ الإدخال «الواصف» (مثل «كلمة المرور: …») لا ضدّ لصق القيمة وحدها.
2. **تغطية غير كاملة للحقول.** في `ClientContactService.Validate()` (سطور 177-184) تُمرَّر ثلاثة حقول فقط إلى `AnyContainsSecret` (`Notes`, `JobTitle`, `Department`). كما **لا يوجد اختبار يؤكّد `client.website_invalid`** ⇒ فجوة تغطية اختبارية صغيرة.

### 7.4 السياسة التشغيلية المقترَحة (بلا تنفيذ)

**التسلسل الإلزاميّ:**
1. **الأولوية القصوى — التفويض الرسميّ:** طلب صلاحية عبر Meta Business Manager / Google Ads Partner Access / TikTok Business Center. **لا مشاركة كلمات مرور مطلقًا متى توفّر التفويض الرسميّ.**
2. **عند الاضطرار فقط:** استخدام مدير كلمات مرور مشفَّر **خارجيّ** (1Password / Bitwarden Teams) بمشاركة محكومة ومؤقّتة.
3. **ما يحتفظ به النظام حصرًا:** المنصّة · معرّف الحساب (اسم الصفحة/المُعرِّف العامّ) · حالة الوصول (`AccessStatus`) · المسؤول · تاريخ المنح · تاريخ آخر مراجعة · (اختياريًّا مستقبلًا) **مرجع خزنة خارجية**.
4. **قاعدة مطلقة:** **السرّ نفسه لا يُخزَّن في قاعدة بيانات النظام إطلاقًا، ولا حتى مشفَّرًا.**

### 7.5 هل نحتاج حقل `ExternalVaultReference` مستقبلًا؟

**نعم — مُوصى به، وبثلاثة شروط صارمة:**
- يخزّن **معرّفًا/رابطًا للخزنة الخارجية فقط** (مثل `1password://vault/ClientX/MetaBM`) ولا يخزّن السرّ.
- يخضع لنفس `IsValidUrl` + `AnyContainsSecret` (فالمرجع ذاته يجب ألّا يحوي سرًّا).
- الوصول إليه محكوم بنفس تفويض المورد (`AuthorizeWriteAsync`) ومسجَّل في التدقيق.

> **يُدرَج ضمن `CPW-R1B2` كبند فرعيّ. لم يُنفَّذ أيّ تعديل.**

---

## §8 — مراجعة Client 360 قراءة-فقط مقابل `6859ee0`

| المحور | الحالة | الشاهد |
|---|---|---|
| حقول نواة العميل | ✅ سليمة | 3 عملاء محفوظون، الحقول باقية بعد إعادة الفتح |
| جهات الاتصال | ✅ سليمة | 2 جهة، منها 1 جهة اتصال ماليّة |
| **تفرّد الجهة الأساسية** | ✅ **مفروض على مستويين** | معاملة صريحة `DemoteActivePrimariesAsync` **+** فهرس `IX_client_contacts_ClientId_ActivePrimary` الفريد الجزئيّ ⇒ **أساسيّة نشطة واحدة فقط** مؤكَّدة في البيانات الحيّة |
| القنوات الرقمية + `AccessStatus` | ✅ سليمة | 2 قناة، فهرس `IX_client_digital_channels_ClientId` قائم |
| **Brand Profile (1:1)** | ✅ **مفروض بنيويًّا** | `PK_client_brand_profiles` = `CREATE UNIQUE INDEX … ON public.client_brand_profiles ("ClientId")` |
| المشاريع | ✅ سليمة | 4 مشاريع، 1 بمدير حساب |
| الثبات بعد إعادة الفتح | ✅ مؤكَّد | من اختبار المالك + قراءة القاعدة |
| تطابق DTO / API / UI | ✅ مؤكَّد | `ClientDetailPage.tsx:149-155` يمرّر `canWriteChildren` لكلّ تبويب |
| **تطابق الواجهة مع الخادم في التفويض** | ✅ **صحيح ومطابق** | `ClientDetailPage.tsx:99`: `canWriteChildren = canEditClientCore \|\| (user.userId === c.accountManagerId)` — نفس منطق `AuthorizeWriteAsync` حرفيًّا، والتعليق يوضّح أنّ الخادم هو الفاصل النهائيّ |
| تفويض مدير الحساب بالمورد | ✅ سليم ومتماثل | متطابق في الخدمات الثلاث (`ClientContactService.cs:167` · `ClientBrandService.cs:93-95` · `ClientDigitalChannelService.cs:145-147`) |
| قيد TeamLeader | ✅ مطبَّق | `ClientCoreManagers` يستثني `TeamLeader` عمدًا |
| **IDOR / BOLA** | ✅ **محميّ** | كلّ مسار يمرّ بـ`ResolveAsync` ⟵ `CanViewClient(clientId)`، والكتابة بـ`AuthorizeWriteAsync`؛ الأبناء مقيَّدون دومًا بـ`x.ClientId == clientId` |
| حالات Empty / Loading / Error | ✅ موجودة | مغطّاة في التبويبات |
| RTL | ✅ سليم | الواجهة RTL كاملة |
| **صلاحية مدير الحساب على TEST** | ⚠️ **غير مُختبَرة — ولا يمكن اختبارها حاليًّا** | العميل الوحيد المُسنَد مديره حساب = `Admin` بلا مسمّى ⇒ مرّ الاختبار عبر فرع `ClientCoreManagers` لا فرع `AccountManagerId` |

---

## §9 — الاختبارات الآمنة المُنفَّذة (كلّها محلّية أو SELECT)

| الفحص | النتيجة |
|---|---|
| بناء Backend محلّيًّا (`dotnet build Reporting.sln -c Debug`) | ✅ **Build succeeded — 0 Error(s)** (12 تحذير = مهلة قراءة `project.nuget.cache` بسبب مزامنة iCloud، حميدة تمامًا) |
| `Client360FoundationTests` | ✅ **25/25 Passed — 0 Failed** (2 ثانية) |
| Frontend `tsc -b` | ✅ **0 أخطاء** |
| Frontend `vitest run` | ⚠️ **231/232 ناجح — فشل واحد** |
| استعلامات SELECT على TEST و Production | ✅ قراءة فقط، بلا بيانات شخصية |
| `GET` على TEST | ✅ لم يُنفَّذ أيّ `POST/PUT/DELETE` |

**تفصيل الفشل الوحيد (خارج نطاق Client 360 قطعًا):**
`src/pages/pages.test.tsx > LeaveRequestsPage shows heading and a leave request row`
السبب: `Error: useToast must be used within a ToastProvider` — الصفحة صارت تستخدم `useToast` من `components/ActionResultToast.tsx` (**ملفّ غير مُتتبَّع في Git** `??`) بينما الاختبار لا يغلّفها بـ`ToastProvider`، و`LeaveRequestsPage.tsx` نفسه **مُعدَّل وغير مُلتزَم** (`M`).
⇒ **أثر عمل جارٍ لتذكرة أخرى في شجرة العمل الحالية، لا علاقة له بـ`CPW-R1B` ولا بـ`6859ee0`.** لم يُصلَح (خارج النطاق، وممنوع تعديل الكود).

**المحظورات المُلتزَم بها:** صفر `POST/PUT/DELETE` على TEST · صفر بيانات QA · صفر تغيير `User`/`Role`/`JobRole` · صفر تعديل على قاعدة TEST.

---

## §10 — التقرير النهائيّ بالفئات الأربع

### ✅ COMPLETED — يعمل ومُثبَت

1. نواة Client 360 (عميل/جهات اتصال/قنوات/براند/مشاريع) تعمل وتحفظ وتثبت بعد إعادة الفتح.
2. تفرّد جهة الاتصال الأساسية مفروض على مستويين (معاملة + فهرس فريد جزئيّ).
3. علاقة Brand Profile 1:1 مفروضة بنيويًّا بمفتاح أساسيّ فريد على `ClientId`.
4. تفويض بالمورد (Resource-Based) سليم ومتماثل في الخدمات الثلاث، بلا ثغرة IDOR.
5. حُرّاس الأسرار والروابط تعمل ومغطّاة بـ6 تأكيدات آلية (25/25 ناجحة).
6. تطابق الواجهة مع الخادم في إظهار/إخفاء عناصر التحكّم.
7. بناء Backend نظيف + `tsc` نظيف.

### 🐞 BUG

1. **تعارض شرط الوصول إلى محفظة العملاء (خطورة متوسّطة):** الظهور بالمسمّى (`navConfig.ts:181`) بينما الحارس والسياسة بالدور (`App.tsx:88-89` · `Program.cs:105-106`) ⇒ حامل الدور بلا المسمّى لا يرى الرابط، وحامل المسمّى بلا الدور يُصدَم بـ403.
2. **عيب جودة بيانات في Production (خطورة منخفضة):** الرمز `adminـassistant` يحوي محرف التطويل `U+0640` بدل `_`.
3. **فشل اختبار واجهة (خارج النطاق):** `pages.test.tsx > LeaveRequestsPage` بسبب `ToastProvider` مفقود — أثر عمل غير مُلتزَم لتذكرة أخرى.

### 🎨 UX

1. **المسمّى الوظيفيّ غير قابل للإسناد من شاشة إنشاء المستخدم** — موجود في شاشة منفصلة (`JobRolesAssignmentPage.tsx`) غير مربوطة بالمسار ⇒ `UX Gap` لا `Bug`.
2. **التباس التسمية «حساب» بثلاثة معانٍ** (عميل / مستخدم / مالية)، وأخطرها أنّ `ACCOUNT_MGR` اسمه «مدير حسابات» فيُقرأ ماليًّا — راجع مقترح §3.2.
3. **أربع تسميات مختلفة لشاشة واحدة** (محفظة مدير الحساب / محفظة الحسابات / لوحة محفظة العملاء / صحّة الحسابات).

### 📋 MISSING SCOPE

1. **مستندات العميل والروابط المهمّة** — صفر كيان، صفر جدول، صفر واجهة ⇒ `CPW-R1B2` المقترَح (§6.5).
2. **حقل `ExternalVaultReference`** لمرجع الخزنة الخارجية بلا تخزين السرّ (§7.5).
3. **بيانات مدير حساب صالحة للاختبار على TEST** — صفر مسمّى، صفر `ACCOUNT_MGR`، صفر `AccountPortfolioReader`.
4. **تغطية اختبارية لـ`client.website_invalid`.**

### 10.1 خطّة مرحلية مقترَحة (**بلا تنفيذ**)

| المرحلة | المحتوى | التبعية |
|---|---|---|
| **P0 — تمكين اختبار مدير الحساب** | توسيع `05-seed-uat-fixture.sh` ببذر `ACCOUNT_MGR` (أو الكتالوج الـ25) + مستخدم `@uat.local` بدور `AccountPortfolioReader` + ربطه بـ`Client.AccountManagerId` | تصريح مستقلّ |
| **P1 — قرار التسمية** | اعتماد §3.2 ثمّ تنفيذه (نصوص عرض + `NameAr` فقط، بلا Migration بنيويّة) | اعتماد المالك |
| **P2 — إصلاح تعارض المحفظة** | توحيد شرط الظهور مع حارس المسار والسياسة + تحديث `DashboardShell.portfolio.nav.test.tsx` | بعد P1 |
| **P3 — `CPW-R1B2` روابط** | `ClientImportantLink` (المسار السريع، بلا رفع ملفّات) | تذكرة مستقلّة |
| **P4 — `CPW-R1B2` مستندات** | `ClientDocument` + رفع محكوم على نمط `UploadFinalDocumentAsync` | بعد P3 |
| **P5 — نظافة** | إصلاح `adminـassistant` + إضافة اختبار `client.website_invalid` | مستقلّة |

---

## البوّابة النهائية (Final Gate)

| # | البند | القرار | المبرّر |
|---|---|---|---|
| 1 | نواة Client 360 سليمة | ✅ **GO** | 25/25 اختبار + بيانات حيّة متّسقة + قيود قاعدة مفروضة |
| 2 | نموذج مدير الحساب مفهوم | ✅ **GO** | ثلاثة محاور موثَّقة بالكود والسطور (§2) |
| 3 | بيانات مدير الحساب على TEST جاهزة | ❌ **NO-GO** | صفر مسمّى · صفر `ACCOUNT_MGR` · صفر `AccountPortfolioReader` · المُسنَد الوحيد هو `Admin` |
| 4 | وضوح تسمية Role مقابل Job Role | ⚠️ **CONDITIONAL GO** | مفهوم تقنيًّا، لكنّ التسمية العربية مضلِّلة ⇒ مشروط باعتماد §3.2 |
| 5 | تصنيف انحراف TEST/Production | ✅ **GO** | `A + B` سببًا جذريًّا، `D` عاملًا مضاعِفًا، `E` مستبعَد |
| 6 | استراتيجية نسخ بيانات آمنة محدَّدة | ✅ **GO** | الخيار `C` ثمّ `A`؛ `B` مرفوض تقنيًّا وأمنيًّا |
| 7 | تصنيف قدرة الملفّات | ✅ **GO** | `MISSING SCOPE` قطعًا ⇒ `CPW-R1B2` |
| 8 | سياسة بيانات الدخول مؤكَّدة | ✅ **GO** | صفر تخزين للأسرار + 6 تأكيدات آلية + قيدان مُفصَح عنهما |
| 9 | **جاهزية إغلاق `CPW-R1B`** | ⚠️ **CONDITIONAL GO** | النواة سليمة، لكنّ **فرع `AccountManagerId` لم يُختبَر حيًّا قطّ** ⇒ الإغلاق مشروط بتنفيذ P0 ثمّ جولة UAT قصيرة عليه |
| 10 | **جاهزية بدء تطوير جديد** | ❌ **NO-GO** | كما تقتضي التذكرة صراحةً |

---

**التوقّف:** انتهى التقرير. **لم يُنفَّذ أيّ إصلاح، ولا نقل بيانات، ولا Deploy.** لا خطوة تالية دون تصريح مستقلّ صريح.
