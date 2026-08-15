# CPW-R1B1 — مواءمة «مدير العميل» وجاهزية UAT — تقرير تحليل وتصميم (قراءة-فقط)

**التاريخ:** 6 أغسطس 2026
**الحالة:** تحليل وتصميم فقط — **لم يُنفَّذ أيّ كود، ولا Seeder، ولا Deploy، ولا كتابة على أيّ قاعدة بيانات.**
**نطاق الأدلّة:** قراءة المستودع + استعلامات `SELECT` فقط على TEST (`reporting_test_uat`) و Production (`reporting_prod`).
**الهدف:** إغلاق فجوة مدير العميل على TEST/UAT وتوحيد نموذج الصلاحيات والتسمية، دون توسيع نطاق Client 360 ودون بدء Project Workspace.

---

## §0 — سجلّ الالتزام بالمحظورات

| المحظور | الحالة |
|---|---|
| نسخ Production كاملة | **لم يحدث** — لا `pg_dump`، لا نسخ بيانات، استعلامات تجميعيّة فقط |
| نقل PasswordHash أو Tokens | **لم يحدث** — لم تُقرأ أعمدة الاعتماد إطلاقًا |
| تعديل Production أو RC | **لم يحدث** — لا `UPDATE/INSERT/DELETE/ALTER` على أيّ بيئة |
| Deploy قبل تقرير واعتماد | **لم يحدث** |
| تغيير صلاحيات أدوار أخرى | **لم يحدث** (ولا يُقترَح في التصميم) |
| بدء Client Documents | **لم يُبدأ** |
| بدء Project Workspace | **لم يُبدأ** |

كذلك: **لم تُعرَض أيّ بيانات شخصية حسّاسة من Production** — معرّفات المستخدمين مقتطعة إلى 8 محارف، بلا أسماء ولا بريد ولا هواتف.

---

## §1 — إثبات النموذج الحالي والـRoot Cause

### 1.1 المحاور الثلاثة لمفهوم «مدير العميل» (مُثبَتة بالكود)

| المحور | القيمة الفعليّة | موضع الإثبات |
|---|---|---|
| **دور الأمان (Security Role)** | `AccountPortfolioReader` | `Roles.cs:225-227` → `AccountPortfolioReaders = { AccountPortfolioReader, Admin }` |
| **المسمّى الوظيفيّ (Job Role)** | `Code = 'ACCOUNT_MGR'`، `NameAr = "مدير حسابات"` | `OrgSeeder.cs:433`، `tools/OrgImporter/OrgData.cs:113` |
| **الإسناد (Assignment)** | `Client.AccountManagerId` و`Project.AccountManagerId` | `Domain/Entities/Clients/Client.cs:16`، `Domain/Entities/Projects/Project.cs:23` |

**حقيقة بنيويّة مؤكَّدة:** **لا يوجد دور ASP.NET Identity اسمه `AccountManager` حرفيًّا.** المفهوم موزَّع على ثلاثة محاور مستقلّة، لا آليّة واحدة تُلزِم بتزامنها.

### 1.2 التناقض بين الطبقات الأربع (الدليل الحرفيّ)

| الطبقة | الملفّ:السطر | المحور المستخدَم فعليًّا |
|---|---|---|
| **Navigation** | `navConfig.ts:181` → `if (t.accountManagerOnly) return ctx.jobRoleCode === 'ACCOUNT_MGR';` | **المسمّى الوظيفيّ** |
| **Route guard** | `App.tsx:88` → `ACCOUNT_PORTFOLIO_ROLES = ['AccountPortfolioReader','Admin']` | **دور الأمان** |
| **API policy** | `Program.cs:105-106` → `AddPolicy(Policies.AccountPortfolioRead, p => p.RequireRole(Roles.AccountPortfolioReaders))` | **دور الأمان** |
| **Service resource auth** | `ClientContactService.AuthorizeWriteAsync:167` → `if (client.AccountManagerId == uid) return null;` | **الإسناد فقط** |

⇒ **ثلاث طبقات، ثلاثة محاور مختلفة.** يمكن لمستخدم أن يحمل الدور بلا المسمّى (فيصل إلى المسار لكن لا يرى عنصر التنقّل)، أو أن يكون مُسنَدًا بلا دور ولا مسمّى (فيكتب على جهات الاتصال عبر الـAPI مباشرةً بينما لا يرى شيئًا في الواجهة).

### 1.3 الحقيقة الأولى — انشقاق المِرساة (Two-Anchor Split)

`AccountPortfolioService` يُقصِر النطاق **حصرًا** على `Project.AccountManagerId`، وينفي صراحةً في توثيقه اعتماد `Client.AccountManagerId`:

```csharp
// AccountPortfolioService.cs:1-6 (XML doc)
/// النطاق مفروض خادمًا حصرًا على مشاريع المستخدم الحالي نفسه (Project.AccountManagerId == uid)
/// — لا IClientProjectAccess، لا منح رؤية، لا عضوية فِرق، لا مسمّى وظيفي، لا Client.AccountManagerId.

// AccountPortfolioService.cs:43-46
var projects = await _db.Projects.AsNoTracking().Where(p => p.AccountManagerId == uid)...

// AccountPortfolioService.cs:85-91 (GetMyClientAsync)
var myProjects = await _db.Projects.AsNoTracking()
    .Where(p => p.AccountManagerId == uid && p.ClientId == id)...
if (myProjects.Count == 0)
    return Result<...>.Failure("هذا العميل خارج نطاق محفظتك.", "auth.forbidden");
```

بينما خدمات أبناء العميل الثلاث تُقصِر الكتابة **حصرًا** على `Client.AccountManagerId`:

```csharp
// ClientContactService.cs:160-175 (ونظيراتها في ClientDigitalChannelService و ClientBrandService)
private async Task<(string message, string code)?> AuthorizeWriteAsync(Guid clientId, Guid uid, CancellationToken ct)
{
    var client = await _db.Clients.AsNoTracking()
        .Select(c => new { c.Id, c.AccountManagerId })
        .FirstOrDefaultAsync(c => c.Id == clientId, ct);
    if (client is null) return ("العميل غير موجود.", "client.not_found");
    if (client.AccountManagerId == uid) return null;
    if (_currentUser.IsInAnyRole(Roles.ClientCoreManagers)) { ... }
    return ("لا تملك صلاحية إدارة بيانات هذا العميل.", "auth.forbidden");
}
```

⇒ **الأثر العمليّ:** مستخدم مُسنَد على مشروع فقط (`Project.AccountManagerId`) **يرى** العميل في محفظته لكنه **لا يستطيع** تعديل جهات اتصاله؛ ومستخدم مُسنَد على العميل فقط (`Client.AccountManagerId`) **يستطيع** تعديل جهات الاتصال لكنه **لا يرى** العميل في محفظته إطلاقًا. الاختبارات المطلوبة في §5 (2/3 مقابل 4/5/6) تسقط على جانبَي هذا الانشقاق.

### 1.4 الحقيقة الثانية — طريق مسدود على مسار تفاصيل العميل

```ts
// App.tsx:59
const EXEC_ROLES: Role[] = ['Admin','CEO','GeneralManager','Manager','TeamLeader','CeoSupport','Viewer'];
// App.tsx:151
{ path: '/app/clients/:clientId', element: <ClientDetailPage />, roles: EXEC_ROLES },
```

`EXEC_ROLES` **لا تحوي** `AccountPortfolioReader`. وشاشة `ClientDetailPage` هي **الواجهة الوحيدة** التي تُحرَّر فيها جهات الاتصال والقنوات الرقميّة وملفّ البراند:

```ts
// ClientDetailPage.tsx:99
const canWriteChildren = canEditClientCore || (!!user && user.userId === c.accountManagerId);
// ClientDetailPage.tsx:150-152
{tab === 'contacts' && <ContactsTab clientId={c.id} canWrite={canWriteChildren} />}
{tab === 'channels' && <ChannelsTab clientId={c.id} canWrite={canWriteChildren} />}
{tab === 'brand'    && <BrandTab    clientId={c.id} canWrite={canWriteChildren} />}
```

⇒ الواجهة **مُهيّأة بالفعل** لمدير العميل (سطر 99 يطابق المِرساة الخادميّة تمامًا)، لكنها **غير قابلة للوصول** لأنّ حارس المسار يمنعه. **اختبارات §5 رقم 4 و5 و6 لا يمكن أن تنجح عبر الواجهة اليوم — فقط عبر استدعاء API مباشر.**

### 1.5 الحقيقة الثالثة — بوّابة البيئة تُلغي محور المسمّى على TEST

```csharp
// Program.cs:208-210
if (app.Environment.IsDevelopment())
    await OrgSeeder.SeedAsync(...);
```

بيئة TEST تعمل بـ`ASPNETCORE_ENVIRONMENT=Staging` ⇒ `OrgSeeder` **لا يعمل إطلاقًا** ⇒ `job_roles` = **0 صفّ** على `reporting_test_uat` (مقيس فعليًّا، §6) ⇒ `ACCOUNT_MGR` غير موجود ⇒ `ctx.jobRoleCode` دائمًا `null` ⇒ **عنصر «مشاريع عملائي» لا يمكن أن يظهر على TEST مهما فعلنا بالأدوار.**

### 1.6 الحقيقة الرابعة — لا مستخدم مدير عميل حقيقيّ على TEST

القياس الفعليّ (§6): حاملو `AccountPortfolioReader` على TEST = **0**؛ والعميل الوحيد الذي له `AccountManagerId` مُسنَد إلى حساب **Admin** المالك (`4b5074eb`) — وهو يتجاوز كلّ الحُرّاس بحكم دوره. ⇒ **فرع «مدير العميل» لم يُختبَر على TEST ولا مرّة واحدة.** وعميلا UAT المُعدّان للاختبار (`ألفا`، `بيتا`) بلا مدير عميل إطلاقًا.

### 1.7 ✦ الـRoot Cause النهائيّ

> **لا يوجد تعريف واحد موثوق لـ«مدير العميل» في النظام.** المفهوم موزَّع على **ثلاثة محاور مستقلّة غير متزامنة** (دور أمان + مسمّى وظيفيّ + إسناد) وعلى **مِرساتَي إسناد متعارضتَين** (`Client.AccountManagerId` مقابل `Project.AccountManagerId`)، وكلّ طبقة من طبقات الفرض الأربع تختار محورًا مختلفًا. يُضاف إلى ذلك أنّ مسار الواجهة الوحيد الذي يُمكّن الكتابة على أبناء العميل محجوب عن دور المحفظة، وأنّ محور المسمّى الوظيفيّ **غائب كلّيًّا على TEST** لأنّ الـSeeder مقصور على بيئة Development.
>
> النتيجة: سلوك غير قابل للتنبّؤ عبر البيئات، وفجوة UAT مطلقة (صفر تغطية لفرع مدير العميل على TEST)، مع مخاطرة IDOR كامنة لأنّ الفرض النهائيّ يعتمد على مِرساة واحدة ضيّقة لا تغطّي كلّ مسارات الوصول.

---

## §2 — قرار التسمية وقائمة التغيير (**دون تنفيذ — بانتظار الموافقة**)

### 2.1 جدول التسمية المعتمَد

| المفهوم (إنجليزيّ) | العربيّة المعتمدة | الحالة اليوم |
|---|---|---|
| Account Manager | **مدير العميل** | ⚠️ يُستخدَم اليوم «مدير الحساب» / «مدير حسابات» |
| Accounting | **الحسابات المالية** | ⚠️ يُستخدَم اليوم «الحسابات» |
| Finance Manager | **المدير المالي** | ✅ مطابق بالفعل — لا تغيير |

### 2.2 مبدأ حاكم للتغيير

**تُغيَّر النصوص العربية المعروضة فقط.** لا تُعاد تسمية أيّ معرّف برمجيّ:
`AccountManagerId` · `AccountPortfolioReader` · `ACCOUNT_MGR` · `ACCOUNTANT` · `AccountPortfolio*` · `Policies.AccountPortfolioRead`
— إعادة تسميتها تستلزم هجرة قاعدة بيانات + كسر توافق الـAPI + كسر بيانات Production القائمة، وهو خارج نطاق هذه التذكرة تمامًا.

### 2.3 قائمة التغيير الكاملة (~70 موضعًا)

#### (أ) نصوص مؤثّرة على البيانات — تحتاج قرارًا خاصًّا

| الملفّ:السطر | النصّ الحاليّ | المقترَح | ملاحظة |
|---|---|---|---|
| `OrgSeeder.cs:433` | `("مدير حسابات", "ACCOUNT_MGR", "GM")` | `("مدير العميل", "ACCOUNT_MGR", "GM")` | Development فقط |
| `tools/OrgImporter/OrgData.cs:113` | `"مدير حسابات"` | `"مدير العميل"` | أداة، غير مُشغَّلة |
| **قاعدة Production** | `job_roles.NameAr = 'مدير حسابات'` لصفّ `ACCOUNT_MGR` | `'مدير العميل'` | **صفّ واحد** — يحتاج تصريحًا مستقلًّا خارج هذه التذكرة |
| **قاعدة TEST** | لا صفوف (0 job_roles) | يُنشَأ بـ«مدير العميل» | ضمن §4 |

#### (ب) نصوص عرض في الـBackend

| الملفّ:السطر | الحاليّ | المقترَح |
|---|---|---|
| `Roles.cs:377` | `DisplayAr_Accountant = "الحسابات"` | `"الحسابات المالية"` |
| `Roles.cs:378` | `DisplayAr_AccountPortfolioReader = "محفظة مدير الحساب"` | `"محفظة مدير العميل"` |
| `Roles.cs:376` | `DisplayAr_FinanceManager = "المدير المالي"` | ✅ بلا تغيير |

#### (ج) نصوص عرض في الـFrontend

| الملفّ:السطر | الحاليّ | المقترَح |
|---|---|---|
| `format.ts:67` | `Accountant: 'الحسابات'` | `'الحسابات المالية'` |
| `format.ts:68` | `AccountPortfolioReader: 'محفظة مدير الحساب'` | `'محفظة مدير العميل'` |
| `format.ts:66` | `FinanceManager: 'المدير المالي'` | ✅ بلا تغيير |
| `ClientsPage.tsx:123, 293, 386` | `مدير الحساب` | `مدير العميل` |
| **`ClientsPage.tsx:254, 260`** | **`صحّة الحسابات`** | **`صحّة ملفّات العملاء`** — أسوأ التباسًا (تُقرأ ماليًّا) |
| `ClientDetailPage.tsx:195, 1090, 1217` | `مدير الحساب` | `مدير العميل` |
| `ProjectDetailPage.tsx:126, 1049` | `مدير الحساب` / `مدير حسابات` | `مدير العميل` |
| `RoleHomeDashboards.tsx:595, 605` | `محفظة الحسابات` | `محفظة عملائي` |

#### (د) تعليقات ووثائق داخليّة (بلا أثر تشغيليّ — للاتّساق)

`navConfig.ts:46,65,107,108,115` · `App.tsx:63,64,67,154` · `types/api.ts:2084,2528` · `useAccountPortfolio.ts:10` · `AccountPortfolioPage.tsx:7` · `TeamLeaderExecutionPage.tsx:33,234` · `ClientDetailPage.tsx:97` · `ProjectDetailPage.tsx:79` · `Program.cs:105` · `Roles.cs:22,42,219,469` · `AccountPortfolioController.cs:8` · `IAccountPortfolioService.cs:6` · `AccountPortfolioModels.cs:5` · `ClientContactsController.cs:9,10` · `ClientBrandController.cs:9` · `ClientDigitalChannelsController.cs:9` · `IClientContactService.cs:7` · `IClientDigitalChannelService.cs:7` · `IClientBrandService.cs:7` · `ProjectWorkstreamsController.cs:11,12,27` · `WorkstreamDeliverablesController.cs:11,12,26` · `Client.cs:16` · `Project.cs:23`

#### (هـ) أسماء اختبارات وتعليقاتها

`DashboardShell.portfolio.nav.test.tsx:8,55,62,66,77` · `DashboardShell.execution.nav.test.tsx:7,68` · `ProjectWorkstreamsTests.cs:211,226,240,250,258,292` · `ProjectRepeatableGridTests.cs:189` · `AccountPortfolioTests.cs:14`

#### (و) نصوص خارج نطاق «مدير العميل» — يُوصى بعدم لمسها الآن

`OrgSeeder.cs:326` و`tools/TemplateBinder/Program.cs:37`: عنوان قالب `"تقرير الحسابات"` مربوط بـ`ACCOUNTANT` — تغييره يمسّ ربط القوالب بالمسمّيات (مطابقة بالعنوان) ⇒ **خارج النطاق، يُؤجَّل**.

**⛔ لم يُنفَّذ أيّ من هذه التغييرات. القائمة معروضة للاعتماد فقط.**

---

## §3 — تصميم شرط الوصول الموحّد

### 3.1 المبدأ الحاكم

> **المسمّى الوظيفيّ بيانات موارد بشريّة، وليس صلاحية.**
> الفرض الخادميّ يقوم على محورَين فقط: **دور الأمان** + **الإسناد**.
> المسمّى الوظيفيّ يُستخدَم للعرض والتصنيف الإداريّ فقط، ولا يُبنى عليه أيّ قرار أمنيّ.
> **الخادم هو الحارس النهائيّ** — الواجهة تُخفي وتُظهر فقط، ولا تمنح ولا تمنع.

### 3.2 الشرط الموحّد

يُعتبَر المستخدم **مدير عميل فعليًّا لعميل ما** إذا وفقط إذا:

```
IsAccountManagerOfClient(uid, clientId) :=
      Client.AccountManagerId == uid
   OR EXISTS(Project p : p.ClientId == clientId AND p.AccountManagerId == uid)
```

ويُشترَط لدخول سطح المحفظة أصلًا: `roles contains AccountPortfolioReader`.
أمّا `JobRoleCode = ACCOUNT_MGR` فهو **شرط عرض وتصنيف** (وشرط اتّساق بيانات في UAT)، **وليس شرط تفويض**.

**توحيد المِرساة** يُنفَّذ عبر مُساعِد مشترك واحد — مقترَح: `IAccountManagerAssignment.IsAccountManagerOfClientAsync(clientId, uid, ct)` في `Reporting.Application/Clients/` — تستهلكه الخدمات الثلاث (`ClientContactService` / `ClientDigitalChannelService` / `ClientBrandService`) و`AccountPortfolioService` معًا ⇒ **مصدر حقيقة واحد**، وينتهي انشقاق §1.3.

### 3.3 الطبقات الأربع بعد التوحيد

| الطبقة | القاعدة بعد التوحيد | التغيير المطلوب |
|---|---|---|
| **Navigation** (`navConfig.ts`) | يظهر «مشاريع عملائي» لحاملي `AccountPortfolioReader` — **بالدور لا بالمسمّى** | استبدال `accountManagerOnly` بـ`roles: ['AccountPortfolioReader','Admin']` (أو إبقاء العَلَم مع تغيير شرطه إلى الدور) |
| **Route guard** (`App.tsx`) | `/app/account-portfolio` و`/app/account-portfolio/clients/:id` ⇐ `ACCOUNT_PORTFOLIO_ROLES` | بلا تغيير + **قرار مطلوب** بشأن مسار تحرير الأبناء (أدناه) |
| **API policy** (`Program.cs`) | `AccountPortfolioRead` ⇐ `AccountPortfolioReaders` — **بلا تغيير**؛ ومسارات الأبناء تبقى `[Authorize]` مجرَّدة | بلا تغيير |
| **Service resource auth** | كلّ كتابة على أبناء العميل تمرّ بـ`AuthorizeWriteAsync` المُعاد بناؤه على المُساعِد الموحَّد | تعديل الدالّة في الخدمات الثلاث (منطق واحد مشترك) |

**ملاحظة اتّساق:** غياب سياسة الدور عن `ClientContactsController` / `ClientDigitalChannelsController` / `ClientBrandController` **مقصود وموثَّق** (تعليق `ClientContactsController.cs:9-10`)، ويطابق السابقة القائمة في `ProjectWorkstreamsController` — **يبقى كما هو**؛ الحارس الحقيقيّ داخل الخدمة.

### 3.4 ✦ قرار مطلوب — طريق تحرير أبناء العميل لمدير العميل

| | **الخيار A** | **الخيار B (المُوصى به)** |
|---|---|---|
| **الوصف** | إضافة `AccountPortfolioReader` إلى حارس `/app/clients/:clientId` | إضافة تبويبات جهات الاتصال/القنوات/البراند داخل `AccountPortfolioClientPage` |
| **الملفّات** | `App.tsx:151` (سطر واحد) | صفحة المحفظة + إعادة استخدام مكوّنات `ContactsTab`/`ChannelsTab`/`BrandTab` القائمة |
| **الأثر الجانبيّ** | يفتح شاشة إداريّة كاملة (تشمل الملفّ الأساسيّ وأزرار الأرشفة) لمدير العميل — تُخفى بـ`canEditClientCore` لكن السطح يتّسع | لا اتّساع للسطح الإداريّ؛ مدير العميل يبقى داخل عائلة المحفظة |
| **مخاطرة IDOR** | يعتمد على `ClientsController` القرائيّ + نطاق الرؤية | يعتمد على `AccountPortfolioService` الذي يفرض النطاق أصلًا |
| **الكلفة** | دقائق | أعلى (عمل واجهة) |
| **التوصية** | — | ✅ **B** — أقلّ سطحًا وأقلّ مخاطرة |

**لن يُنفَّذ أيّ خيار قبل اختيار المالك صراحةً.**

---

## §4 — خطة بيانات UAT (**خطة فقط — لا تُنفَّذ قبل موافقة منفصلة**)

**البيئة الوحيدة المستهدَفة:** TEST — `reporting_test_uat` على `test.emarketingacademy.net`.
**ممنوع منعًا باتًّا:** أيّ مساس بـ`reporting_prod` أو `reporting_rc`، وأيّ استخدام لبيانات موظفين حقيقيّة.

### 4.1 العناصر الأربعة

| # | العنصر | القيم | مفتاح الـidempotency |
|---|---|---|---|
| 1 | **مسمّى وظيفيّ** | `Code='ACCOUNT_MGR'`، `NameAr='مدير العميل'`، `IsActive=true` | البحث بـ`Code` قبل الإنشاء |
| 2 | **مستخدم UAT** | بريد داخل `@uat.local` (مقترَح `am1@uat.local`)، دور `AccountPortfolioReader`، مسمّى `ACCOUNT_MGR`، `IsActive=true` | البحث بالبريد قبل الإنشاء |
| 3 | **عميل مُسنَد** | `عميل UAT ألفا` (قائم) ⇐ `AccountManagerId = am1` + مشروع واحد على نفس العميل بـ`Project.AccountManagerId = am1` (لتغطية المِرساتَين) | فحص `AccountManagerId` الحاليّ قبل الضبط |
| 4 | **عميل غير مُسنَد** | `عميل UAT بيتا` (قائم) — يبقى `AccountManagerId = NULL` | تأكيد فقط، بلا كتابة |

**سبب إضافة المشروع في البند 3:** إثبات أنّ المِرساتَين موحَّدتان بعد §3، ومنع تكرار انشقاق §1.3 صامتًا.

### 4.2 طريقة التنفيذ (عند الاعتماد لاحقًا)

- الإنشاء **عبر الـAPI حصرًا** لا عبر `INSERT` مباشر (سلامة Identity: `PasswordHash`، `SecurityStamp`، `NormalizedEmail`) — تماشيًا مع نمط `Ops/TestUatPreparation/05-seed-uat-fixture.sh` القائم.
- **فجوة معروفة يجب سدّها:** السكربت الحاليّ **لا يحمل حقل `jobRoleId`** ⇒ يلزم توسيعه بخطوة إسناد المسمّى (`PATCH /api/directory/users/{id}/job-role`).
- التقيّد بحُرّاس الحزمة: وضع PLAN افتراضيّ، والكتابة تتطلّب `--apply` + `OPS_ALLOW_WRITE=1` + تأكيد تفاعليّ، مع `FORBIDDEN_NAME_REGEX` الذي يوقف أيّ اسم يشي بـProduction/RC.
- **إعادة التشغيل آمنة:** كلّ خطوة تبحث بمفتاح ثابت قبل الإنشاء ⇒ التشغيل الثاني = صفر كتابة.

### 4.3 مسألة مفتوحة — كيف يصل المسمّى إلى TEST؟

`OrgSeeder` محجوب على Staging (§1.5). ثلاثة مسارات:

| المسار | التقييم |
|---|---|
| توسيع بوّابة البيئة لتشمل Staging | ❌ يجلب هيكلًا تنظيميًّا تجريبيًّا كاملًا إلى TEST — رفض |
| Seeder صغير idempotent للمسمّيات الأساسيّة فقط في غير Production | ⚠️ ممكن، لكنه يوسّع سطح الكود |
| **إنشاء المسمّى عبر الـAPI ضمن fixture الـUAT** (`POST /api/directory/job-roles`) | ✅ **المُوصى به** — بلا كود إنتاج، بلا هجرة، idempotent بطبيعته |

**⛔ لم يُنشَأ أيّ مسمّى ولا مستخدم ولا إسناد. هذه خطة فقط.**

---

## §5 — تصميم اختبارات الصلاحيات (12 اختبارًا)

| # | الاختبار | الطبقة | النتيجة المتوقَّعة | الحالة اليوم |
|---|---|---|---|---|
| 1 | مدير العميل يرى «مشاريع عملائي» | Navigation | ظاهر | ❌ يفشل (شرط المسمّى + 0 مسمّيات على TEST) |
| 2 | يرى العميل المُسنَد فقط | Service | عميل واحد | ⚠️ يعتمد على مِرساة المشروع وحدها |
| 3 | يرى مشاريع ذلك العميل | Service | مشاريعه فقط | ✅ يعمل عند وجود إسناد مشروع |
| 4 | يُحرّر جهات اتصال العميل المُسنَد | Service + Route | نجاح | ❌ عبر الواجهة (طريق مسدود §1.4) — ✅ عبر API |
| 5 | يُحرّر القنوات الرقميّة | Service + Route | نجاح | ❌ / ✅ (نفس السبب) |
| 6 | يُحرّر ملفّ البراند | Service + Route | نجاح | ❌ / ✅ (نفس السبب) |
| 7 | **لا** يُعدّل الملفّ الأساسيّ للعميل | API policy | `403` | ✅ (`Policies.ClientCoreManagement`) |
| 8 | **لا** ينشئ عميلًا | API policy | `403` | ✅ (`ClientsController.cs:36`) |
| 9 | **لا** يرى/يُعدّل عميلًا غير مُسنَد (IDOR) | Service | `403 auth.forbidden` | ✅ منطقيًّا — **غير مُثبَت باختبار** |
| 10 | موظّف عاديّ لا يرى المحفظة | Nav + Route + API | لا ظهور، `403` | ✅ |
| 11 | Admin/CEO/GM يحتفظون بوصولهم الإداريّ | كلّ الطبقات | بلا تغيير | ✅ — **اختبار انحدار إلزاميّ** |
| 12 | TeamLeader **لا** يرث صلاحية مدير العميل | كلّ الطبقات | لا ظهور، `403` على الكتابة | ✅ (`ClientCoreManagers` تستبعد TeamLeader عمدًا) |

### 5.1 اختبارات قائمة تُثبّت السلوك المعيب — يجب تعديلها مع التغيير

- **`DashboardShell.portfolio.nav.test.tsx:77`** — اسمه حرفيًّا:
  `'حامل دور AccountPortfolioReader بلا مسمّى ACCOUNT_MGR لا يرى العنصر (الظهور بالمسمّى لا بالدور)'`
  ⇒ **هذا الاختبار يُثبّت العيب نفسه.** يجب عكسه عند اعتماد §3.
- `DashboardShell.portfolio.nav.test.tsx:55,62,84,91` — تحتاج مراجعة.
- `AccountPortfolioTests.cs` — 7 اختبارات تكامل تستخدم `TestAuth.CreateUserAsync(_factory, Roles.AccountPortfolioReader)`؛ تُوسَّع بحالات المِرساة المزدوجة و**اختبار IDOR صريح للبند 9** (مفقود اليوم).

---

## §6 — المقارنة مع Production (قراءة-فقط، بلا نقل بيانات)

### 6.1 Production — `reporting_prod`

| القياس | القيمة |
|---|---|
| `ACCOUNT_MGR` موجود؟ | ✅ نعم — `NameAr = 'مدير حسابات'`، نشط |
| `ACCOUNTANT` موجود؟ | ✅ نعم — `NameAr = 'محاسب'` |
| عملاء لهم `AccountManagerId` | **10 / 10** |
| مشاريع لها `AccountManagerId` | **33 / 33** |
| مستخدمون مُسنَدون (مميَّزون) | **4** |
| حاملو `AccountPortfolioReader` | **2** |
| حاملو المسمّى `ACCOUNT_MGR` | **1** |

**تفصيل المستخدمين الأربعة** (معرّفات مقتطعة، بلا أيّ بيانات شخصيّة):

| المعرّف | المسمّى الوظيفيّ | دور الأمان | عملاء | مشاريع | الملاحظة |
|---|---|---|---|---|---|
| `7e2cb6ac` | CEO | CEO | 1 | 4 | مُسنَد بحكم منصبه |
| `aee6885e` | **ACCOUNT_MGR** | **AccountPortfolioReader** + Employee | 7 | 22 | ✅ **المستخدم الوحيد المتوائم على المحاور الثلاثة** |
| `d3d6c8a8` | OCM | **AccountPortfolioReader** + Employee | 1 | 4 | ⚠️ **يملك المسار ولا يرى عنصر التنقّل** — تجسيد حيّ لعيب §1.2 على Production |
| `f4e25122` | GM | GeneralManager | 1 | 3 | مُسنَد بحكم منصبه |

**نتيجة مهمّة:** الأربعة **مُسنَدون على المِرساتَين معًا** (العميل والمشروع) ⇒ انشقاق §1.3 **لا يظهر على Production اليوم**، لكنه فخّ كامن: أوّل عميل يُسنَد على مِرساة واحدة فقط يُفعّله.

**فروق التسمية:** `'مدير حسابات'` مقابل `'محاسب'` — الالتباس قائم فعليًّا في بيانات Production (صفّ واحد يحتاج تصحيحًا بتصريح مستقلّ).

### 6.2 TEST — `reporting_test_uat`

| القياس | القيمة |
|---|---|
| `job_roles` | **0** |
| `ACCOUNT_MGR` | **0** |
| مستخدمون | 8 |
| مستخدمون لهم مسمّى وظيفيّ | **0** |
| دور `AccountPortfolioReader` مُعرَّف | ✅ 1 |
| حاملو `AccountPortfolioReader` | **0** |
| عملاء | 3 (منهم 1 له `AccountManagerId`) |
| مشاريع | 4 (منها 1 له `AccountManagerId`) |
| `client_contacts` / `client_digital_channels` / `client_brand_profiles` | 2 / 2 / 1 |

**العملاء:** `تجربة شركة من ابراهيم البحراوي` (مدير العميل = `4b5074eb` = حساب Admin المالك) · `عميل UAT ألفا` (بلا مدير) · `عميل UAT بيتا` (بلا مدير).
**المستخدمون:** `admin@marketingexperts.local` (Admin) · `bhrawy@gmail.com` (Admin) · `ceo@uat.local` · `emp1@uat.local` · `emp2@uat.local` · `gm@uat.local` · `lead@uat.local` · `manager@uat.local`.

### 6.3 الفجوة

| البُعد | Production | TEST | الفجوة |
|---|---|---|---|
| المسمّى `ACCOUNT_MGR` | موجود | **غائب** | 🔴 حاجب |
| حاملو `AccountPortfolioReader` | 2 | **0** | 🔴 حاجب |
| عميل مُسنَد لغير-Admin | نعم | **لا** | 🔴 حاجب |
| عميل غير مُسنَد لاختبار IDOR | — | متاح (`بيتا`) | 🟢 جاهز |

⇒ **TEST غير جاهزة لتنفيذ اختبارات §5 الاثني عشر. الفجوة بيانات وليست كودًا فقط.**

---

## §7 — خطة التنفيذ (مقسَّمة، غير مبدوءة)

### A. مواءمة الكود (Backend + Frontend)
- A1: مُساعِد التفويض الموحَّد `IAccountManagerAssignment` + تنفيذه.
- A2: إعادة بناء `AuthorizeWriteAsync` في الخدمات الثلاث على المُساعِد.
- A3: توحيد `AccountPortfolioService` على المِرساة المزدوجة.
- A4: مواءمة التنقّل على الدور بدل المسمّى (`navConfig.ts`).
- A5: تنفيذ الخيار A أو B (§3.4) — **بعد قرار المالك**.
- A6: تغييرات التسمية (§2) — **بعد اعتماد جدول التسمية**.
- **هجرة قاعدة بيانات: غير مطلوبة** — لا حقول ولا جداول جديدة. (لو لزم تصحيح `NameAr` على Production فهو `UPDATE` بيانات بتصريح مستقلّ، لا هجرة.)

### B. تجهيزة UAT
- B1: توسيع `05-seed-uat-fixture.sh` بحقل `jobRoleId` + خطوة إنشاء المسمّى (idempotent).
- B2: إنشاء المستخدم `am1@uat.local` + إسناد الدور والمسمّى.
- B3: إسناد `عميل UAT ألفا` + مشروع واحد عليه.
- B4: تأكيد بقاء `عميل UAT بيتا` بلا إسناد.
- **كلّها PLAN حتى موافقة مستقلّة.**

### C. الاختبارات
- C1: اختبارات تكامل Backend للبنود 2،3،4،5،6،7،8،**9 (IDOR)**،11،12.
- C2: اختبارات واجهة للبندَين 1 و10 + **عكس** `portfolio.nav.test.tsx:77`.
- C3: انحدار كامل — Admin/CEO/GM/TeamLeader/Employee بلا تغيير.

### D. نشر TEST
- D1: بناء + نشر على `khubara-reporting-test` (5091) — **بعد اجتياز C**.
- D2: تشغيل fixture الـUAT.
- D3: تحقّق دخانيّ (Smoke) للبنود الاثني عشر.

### E. UAT المالك
- E1: تنفيذ الاثني عشر يدويًّا على `test.emarketingacademy.net`.
- E2: تقرير قبول + قرار GO/NO-GO لأيّ مسار لاحق.

---

## §8 — التقرير النهائي

| البند | الخلاصة |
|---|---|
| **Root Cause** | لا تعريف موثوق واحد لـ«مدير العميل»: ثلاثة محاور غير متزامنة + مِرساتا إسناد متعارضتان + أربع طبقات فرض تختار محاور مختلفة + مسار تحرير الأبناء محجوب عن دور المحفظة + محور المسمّى غائب كلّيًّا على TEST (Seeder مقصور على Development). |
| **النموذج الموحّد** | الفرض الخادميّ = **دور الأمان `AccountPortfolioReader`** + **الإسناد** (`Client.AccountManagerId` **أو** مشروع للعميل بـ`Project.AccountManagerId`). المسمّى الوظيفيّ عرض وتصنيف لا تفويض. الخادم هو الحارس النهائيّ. |
| **التسمية النهائية** | مدير العميل · الحسابات المالية · المدير المالي (الأخيرة مطابقة بالفعل). النصوص العربية فقط تتغيّر؛ المعرّفات البرمجيّة لا تُمسّ. |
| **الملفّات المتأثّرة** | Backend: `IAccountManagerAssignment`(جديد) · `ClientContactService` · `ClientDigitalChannelService` · `ClientBrandService` · `AccountPortfolioService` · `Roles.cs`(نصوص). Frontend: `navConfig.ts` · `App.tsx` · `format.ts` · `ClientsPage` · `ClientDetailPage` · `ProjectDetailPage` · `RoleHomeDashboards` · صفحة المحفظة (لو الخيار B). Tests: `AccountPortfolioTests.cs` · `DashboardShell.portfolio.nav.test.tsx` وغيرها. Ops: `05-seed-uat-fixture.sh`. |
| **هل Migration مطلوبة؟** | **لا.** لا حقول ولا جداول جديدة؛ الحقول القائمة كافية. |
| **هل Seeder/UAT fixture مطلوبة؟** | **نعم** — fixture UAT idempotent على TEST فقط (مسمّى + مستخدم + إسناد). لا Seeder إنتاجيّ جديد. |
| **خطة الاختبارات** | 12 اختبارًا موزَّعة على 4 طبقات + انحدار كامل + إضافة اختبار IDOR المفقود + عكس الاختبار الذي يُثبّت العيب. |
| **خطة الـrollback** | كود: `git revert` للـcommit (لا هجرة تُعكَس) + استعادة `publish-backup`/`dist-backup` + إعادة تشغيل الخدمة. بيانات UAT: عكس الإسناد (`AccountManagerId = NULL`)، تعطيل مستخدم UAT، تعطيل المسمّى — كلّها على TEST فقط وبلا أثر خارجها. |
| **تأثير Production/RC** | **صفر.** لم تُعدَّل ولن تُعدَّل ضمن هذه التذكرة. تصحيح `NameAr` على Production **مؤجَّل بتصريح مستقلّ**. النشر مقصور على TEST. |
| **ما لن يُنفَّذ** | إعادة تسمية أيّ معرّف برمجيّ · تغيير صلاحيات أيّ دور آخر · Client Documents · Project Workspace · توسيع Client 360 · أيّ Seeder إنتاجيّ · أيّ نسخ من Production · أيّ كتابة على Production/RC · تغيير عنوان قالب «تقرير الحسابات». |

### البوّابة النهائية

| البند | القرار |
|---|---|
| نموذج مدير العميل موحَّد | **GO** — التصميم مكتمل ومسنود بأدلّة من الكود والبيئتين |
| التسمية معتمَدة | **GO** — الجدول والقائمة جاهزان (بانتظار توقيع المالك على القائمة) |
| خطة تجهيزة UAT جاهزة | **GO** — idempotent، على TEST حصرًا، بلا بيانات حقيقيّة |
| خطة التفويض جاهزة | **CONDITIONAL GO** — مشروطة بقرار المالك بين الخيار A والخيار B (§3.4) |
| آمن لبدء التنفيذ | **CONDITIONAL GO** — بعد اعتماد التسمية واختيار A/B |
| آمن لنشر TEST الآن | **NO-GO** |
| آمن لبدء Project Workspace | **NO-GO** |

---

**توقّف مُلزَم:** انتهى التحليل والتصميم. **لم يُنفَّذ كود ولا Seeder ولا Deploy.** لا يُبدأ أيّ بند من §7 قبل موافقة صريحة تتضمّن: اعتماد جدول التسمية، واختيار الخيار A أو B، وتصريحًا منفصلًا لتشغيل fixture الـUAT.
