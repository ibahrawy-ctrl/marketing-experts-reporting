# DEVELOP_WORKTREE_STABILIZATION_AUDIT

> **تدقيق استقرار شجرة العمل (Working Tree) — للقراءة فقط.**
> الفرع: `develop` · المستودع البعيد: `git@github.com:ibahrawy-ctrl/marketing-experts-reporting.git` (لا فرع `origin/develop` مُتتبَّع محليًّا حاليًّا).
> آخر التزام: `6fd2253 RC-4 Sales Module baseline`.
> تاريخ التدقيق: 2026-07-11.
> **إجمالي مدخلات الحالة:** 91 مدخلًا (25 مُعدَّل M / 3 محذوف D / 63 غير مُتتبَّع ??).
> **قيود مُطبَّقة:** لم يُعدَّل/يُدرَج/يُودَع/يُستعَد/يُحذَف/يُنقَل/يُعاد تسمية/يُنسَّق أي ملف. لم تُشغَّل هجرة/قاعدة بيانات/نشر/تهيئة. فحص ثابت (Static) بالكامل عبر أوامر git للقراءة فقط.
> **تحذير منهجي:** كل ما يلي = واقع فرع `develop` لا الإنتاج. أي إشارة لسلسلة هجرات الإنتاج مُصنَّفة `[UNV]` (غير مُتحقَّق منها من المستودع).

---

## الخلاصة التنفيذية (اقرأ أولًا)

شجرة العمل تحوي **حزمة تنفيذ واحدة متماسكة كبيرة (RC-4 «Project-First Execution»)** موزَّعة على الخلفية والواجهة، **مع ثلاث قضايا تتطلّب قرارًا قبل أي إيداع**:

| # | القضية | الخطورة | القرار المطلوب |
|---|--------|---------|----------------|
| **1** | مجلّد `reporting-backend/publish-p0p1/` = **مخرجات بناء (DLLs/خطوط)** غير مُتتبَّعة، **يفلت من `.gitignore`** لأن النمط `**/publish/` لا يطابق `publish-p0p1` | **حرِج للنظافة** | استبعاد + إضافة نمط gitignore (لا يُودَع أبدًا) |
| **2** | حُذفت اختبارات **ERDS Phase 5/5.5/6** بينما **الكود المصدري لها (PodExecution + ExecutiveDashboard) ما زال حيًّا ومُسجَّلًا في DI** ⇒ فقدان تغطية لكود إنتاجيّ لا يزال يعمل | **يتطلّب قرار مالك** | تأكيد نيّة الإحلال (Project-First يحلّ محلّ Pod) أم استعادة التغطية |
| **3** | 6 ملفّات توثيق تخطيط بالجذر غير مُتتبَّعة (منها منتجات هذه الجلسة) — `Docs/` مُتجاهَل لكنها بالجذر | **قرار مالك** | تتبُّعها أم إبقاؤها خارج المستودع |

**كل ما عدا ذلك** (كل ملفات RC-4 الجديدة + الملفات المُعدَّلة) **حزمة واحدة مترابطة يجب إيداعها ذرّيًّا (Atomic)** — لأن الملفات المُتتبَّعة المُعدَّلة (App.tsx / DashboardShell.tsx / AppDbContext.cs / DI / Program.cs) **تُشير مباشرةً إلى ملفات غير مُتتبَّعة**، فإيداعها منفصلةً **يكسر البناء**.

الهجرات الثلاث **إضافية بحتة (Additive-only)، مرتّبة صحيحًا، ذات رولباك صالح بنيويًّا**. المخطَّط (Snapshot) والكيانات وتسجيل DI متّسقة. لا `[AllowAnonymous]` في المتحكّمات الجديدة.

---

## 1. جرد شجرة العمل الكامل (Complete Worktree Inventory)

### 1.أ — ملفّات مُتتبَّعة مُعدَّلة (Modified · M) — 25

| # | المسار | الحالة | المجال الوظيفي | الميزة/الطور المرجَّح | النوع | مقصود؟ | الدليل | خطر الفقد | خطر الإيداع كما هو |
|---|--------|--------|----------------|------------------------|-------|--------|--------|-----------|--------------------|
| 1 | `.../Reporting.Api/Program.cs` | M | Bootstrap | RC-4 Exec Taxonomy | مصدر | نعم | يضيف `ExecutionTaxonomySeeder.SeedAsync` قبل TemplateSeeder | متوسط | منخفض (يشير لـ Seeder غير مُتتبَّع ⇒ **ذرّي**) |
| 2 | `.../Application/Common/ReportCalendarPolicy.cs` | M | تقويم التقارير | RC-4 (+57 سطر) | مصدر | نعم | diff +57 | متوسط | منخفض |
| 3 | `.../Application/Common/Roles.cs` | M | تفويض | RC-4 P2 | مصدر | نعم | يضيف `ProjectPlanManagers{Admin,CEO,GM,Manager}` | متوسط | منخفض |
| 4 | `.../Domain/Enums/Enums.cs` | M | نطاق المجال | RC-4 Workstreams | مصدر | نعم | يضيف `WorkstreamStatus` + `DeliverablePriority` (إضافيّ) | متوسط | منخفض |
| 5 | `.../Infrastructure/DependencyInjection.cs` | M | DI | RC-4 | مصدر | نعم | يسجّل 4 خدمات جديدة (Workstream/Deliverable/ProjectFirst/Taxonomy) | عالٍ | منخفض (يشير لخدمات غير مُتتبَّعة ⇒ **ذرّي**) |
| 6 | `.../Infrastructure/Persistence/AppDbContext.cs` | M | ORM | RC-4 | مصدر | نعم | يضيف 3 DbSet (ProjectWorkstreams/WorkstreamDeliverables/ExecutionTaxonomyValues) | عالٍ | منخفض (**ذرّي** مع الكيانات) |
| 7 | `.../Persistence/Migrations/AppDbContextModelSnapshot.cs` | M | مخطَّط EF | RC-4 (+190) | مُولَّد/مصدر | نعم | يعكس الجداول الثلاثة الجديدة | عالٍ | منخفض (يجب أن يطابق الهجرات) |
| 8 | `.../Persistence/TemplateSeeder.cs` | M | بذر بيانات | RC-4 قوالب التنفيذ v3/v4 (+510) | مصدر | نعم | diff +510 (أكبر تعديل) | عالٍ | متوسط (يعتمد قيم Taxonomy المبذورة) |
| 9 | `.../Infrastructure/Services/SubmissionService.cs` | M | تسليم/تحقّق | RC-4 Task4 Path A (+78) | مصدر | نعم | `TryReadEntryNumber` + تحقّق أرقام تشغيليّ مقصور على أقسام Project-First (متوافق خلفيًّا) | عالٍ | منخفض (يشير لـ `ProjectFirstExecutionSchema` غير مُتتبَّع ⇒ **ذرّي**) |
| 10 | `.../tests/ErdsPhase3RolloutTests.cs` | M | اختبار | تنظيف ERDS (+11/−49) | اختبار | نعم | تقليص | منخفض | منخفض |
| 11 | `.../tests/ReportsTests.cs` | M | اختبار | تنظيف ERDS (−918) | اختبار | نعم | 0 إشارات Pod/Executive متبقّية | متوسط | منخفض |
| 12 | `frontend/src/App.tsx` | M | توجيه | RC-4 (+15) | مصدر | نعم | يستورد 3 صفحات غير مُتتبَّعة + 3 مسارات جديدة | عالٍ | **يكسر البناء منفصلًا (ذرّي)** |
| 13 | `frontend/src/components/DashboardShell.nav.test.tsx` | M | اختبار واجهة | إعادة هيكلة التنقّل | اختبار | نعم | diff | منخفض | منخفض |
| 14 | `frontend/src/components/DashboardShell.tsx` | M | تنقّل/شل | إعادة هيكلة navConfig (+378/−) | مصدر | نعم | يستورد `../lib/navConfig` + `./HeaderActions` (غير مُتتبَّعَين) | عالٍ | **يكسر البناء منفصلًا (ذرّي)** |
| 15 | `frontend/src/lib/auth.tsx` | M | مصادقة/دور | RC-4 دور AccountPortfolioReader | مصدر | نعم | diff +14 | متوسط | منخفض |
| 16 | `frontend/src/lib/format.ts` | M | تنسيق | RC-4 تسميات | مصدر | نعم | +26 | منخفض | منخفض |
| 17 | `frontend/src/pages/AccountPortfolioPage.tsx` | M | حافظة الحسابات | RC-4 | مصدر | نعم | +4 | منخفض | منخفض |
| 18 | `frontend/src/pages/ProjectDetailPage.tsx` | M | مشروع | RC-4 Workstreams/Deliverables (+802) | مصدر | نعم | أكبر تعديل واجهة | عالٍ | متوسط (يعتمد hooks غير مُتتبَّعة) |
| 19 | `frontend/src/pages/SalesAggregationPage.tsx` | M | تجميع مبيعات | RC-4 (+/−425) | مصدر | نعم | إعادة تشكيل | متوسط | منخفض |
| 20 | `frontend/src/pages/SalesRepDashboardPage.test.tsx` | M | اختبار واجهة | RC-4 | اختبار | نعم | diff | منخفض | منخفض |
| 21 | `frontend/src/pages/SalesRepDashboardPage.tsx` | M | لوحة مندوب | RC-4 (+68) | مصدر | نعم | diff | متوسط | منخفض |
| 22 | `frontend/src/pages/SubmissionsPage.tsx` | M | تسليمات | RC-4 (+52) | مصدر | نعم | diff | متوسط | منخفض |
| 23 | `frontend/src/pages/TeamLeaderSalesDashboardPage.test.tsx` | M | اختبار واجهة | RC-4 | اختبار | نعم | diff | منخفض | منخفض |
| 24 | `frontend/src/pages/TeamLeaderSalesDashboardPage.tsx` | M | لوحة قائد فريق | RC-4 (+7) | مصدر | نعم | diff | منخفض | منخفض |
| 25 | `frontend/src/types/api.ts` | M | عقد API | RC-4 (+185) | مصدر | نعم | أنواع Workstream/Deliverable/Taxonomy/ProjectFirst | عالٍ | منخفض (**ذرّي** مع الواجهة) |

### 1.ب — ملفّات مُتتبَّعة محذوفة (Deleted · D) — 3

| # | المسار | الحالة | المجال | الطور | النوع | مقصود؟ | الدليل | خطر الفقد | خطر الإيداع كما هو |
|---|--------|--------|--------|-------|-------|--------|--------|-----------|--------------------|
| 1 | `.../tests/ErdsPhase55WorkUnitTests.cs` | D | اختبار | ERDS Phase 5.5 (441 سطر) | اختبار | مرجَّح (تنظيف) | كان يختبر تطبيع وحدة العمل | **فقدان تغطية لكود Pod الحيّ** | يقلّل التغطية |
| 2 | `.../tests/ErdsPhase5PodExecutionTests.cs` | D | اختبار | ERDS Phase 5 (384 سطر) | اختبار | مرجَّح (تنظيف) | كان يختبر محرّك تجميع Pod | **فقدان تغطية** | يقلّل التغطية |
| 3 | `.../tests/ErdsPhase6ExecutiveDashboardTests.cs` | D | اختبار | ERDS Phase 6 (372 سطر) | اختبار | مرجَّح (تنظيف) | كان يختبر اللوحة التنفيذية | **فقدان تغطية** | يقلّل التغطية |

> راجع القسم 3 لمراجعة الحذف الكاملة.

### 1.ج — ملفّات مصدر غير مُتتبَّعة (Backend Source · ??)

**Domain (3):** `Entities/Clients/ProjectWorkstream.cs` · `Entities/Clients/WorkstreamDeliverable.cs` · `Entities/ExecutionTaxonomy/ExecutionTaxonomyValue.cs`
**Application (10):** `Clients/DeliverableModels.cs` · `Clients/IProjectWorkstreamService.cs` · `Clients/IWorkstreamDeliverableService.cs` · `Clients/WorkstreamModels.cs` · `Common/ProjectFirstExecutionSchema.cs` · `ExecutionTaxonomy/ExecutionTaxonomyModels.cs` · `ExecutionTaxonomy/IExecutionTaxonomyService.cs` · `Reports/IProjectFirstExecutionAggregationService.cs` · `Reports/ProjectFirstExecutionModels.cs`
**Infrastructure Configurations (3):** `ExecutionTaxonomyConfigurations.cs` · `ProjectWorkstreamConfiguration.cs` · `WorkstreamDeliverableConfiguration.cs`
**Infrastructure Services (4):** `ExecutionTaxonomyService.cs` · `ProjectFirstExecutionAggregationService.cs` · `ProjectWorkstreamService.cs` · `WorkstreamDeliverableService.cs`
**Infrastructure Seeder (1):** `Persistence/ExecutionTaxonomySeeder.cs`
**Api Controllers (5):** `ExecutionTaxonomyController.cs` · `ExecutionTaxonomyOptionsController.cs` · `ProjectFirstExecutionAggregationController.cs` · `ProjectWorkstreamsController.cs` · `WorkstreamDeliverablesController.cs`

جميعها: مصدر · مقصودة (تُشير إليها ملفات مُتتبَّعة مُعدَّلة) · النوع=مصدر · خطر الفقد **عالٍ** (لا نسخة في التاريخ) · خطر الإيداع كما هو **منخفض** بشرط الذرّية.

### 1.د — هجرات غير مُتتبَّعة (Untracked Migrations) — 3 أزواج (6 ملفات)

| المسار | النوع | مقصود؟ | الدليل |
|--------|-------|--------|--------|
| `20260708232456_AddExecutionTaxonomyCatalog.cs` + `.Designer.cs` | هجرة | نعم | CREATE `execution_taxonomy_values` |
| `20260709222126_AddProjectWorkstreams.cs` + `.Designer.cs` | هجرة | نعم | CREATE `project_workstreams` (FK→projects) |
| `20260709231845_AddWorkstreamDeliverables.cs` + `.Designer.cs` | هجرة | نعم | CREATE `workstream_deliverables` (FK→project_workstreams) |

خطر الفقد عالٍ · خطر الإيداع كما هو منخفض (إضافية بحتة، راجع القسم 4).

### 1.هـ — اختبارات غير مُتتبَّعة (Untracked Tests) — 6

`ExecutionTaxonomyAdminTests.cs` · `ExecutionTaxonomyOptionsTests.cs` · `ProjectFirstExecutionAggregationTests.cs` · `ProjectWorkstreamsTests.cs` · `TemplateTaxonomyV4Tests.cs` · `WorkstreamDeliverablesTests.cs` (كلها Integration).
النوع=اختبار · مقصودة · خطر الفقد متوسط · خطر الإيداع كما هو منخفض (لكن **نتائج التشغيل غير مُتحقَّق منها** — القسم 6).

### 1.و — واجهة غير مُتتبَّعة (Untracked Frontend) — 25

**components (12):** `Collapsible.tsx`+`Collapsible.test.tsx` · `DashboardShell.execution.nav.test.tsx` · `DashboardShell.portfolio.nav.test.tsx` · `HeaderActions.tsx` · `ShowMore.tsx`+`ShowMore.test.tsx` · `StickyBar.tsx`+`StickyBar.test.tsx` · `Tabs.tsx`+`Tabs.test.tsx`
**lib (5):** `navConfig.ts` · `useExecutionTaxonomy.ts` · `useProjectExecution.ts` · `useProjectWorkstreams.ts` · `useWorkstreamDeliverables.ts`
**pages (8):** `ExecutionTaxonomyManagementPage.tsx`+`.test.tsx` · `ProjectDeliverables.test.tsx` · `ProjectWorkstreams.test.tsx` · `TaxonomySelect.test.tsx` · `TeamLeaderExecutionPage.tsx` · `TeamLeaderProjectExecutionPage.tsx`+`.test.tsx`
النوع=مصدر/اختبار · مقصودة (`navConfig.ts` و`HeaderActions.tsx` و3 صفحات تُستورَد من ملفات مُتتبَّعة) · خطر الفقد عالٍ · خطر الإيداع كما هو منخفض بشرط الذرّية.

### 1.ز — مخرجات بناء/توليد (Generated/Build Artifacts)

| المسار | النوع | مقصود؟ | الدليل | التوصية |
|--------|-------|--------|--------|---------|
| `reporting-backend/publish-p0p1/` (≈60+ ملف: DLLs، `Reporting.*.dll/pdb`، `LatoFont/*.ttf`، `*.deps.json`، `runtimeconfig.json`، تنفيذيّ `Reporting.Api`) | **مخرجات بناء** | **لا (عرضيّ)** | `git check-ignore` = **NOT IGNORED** (النمط `**/publish/` لا يطابق `publish-p0p1`) | **استبعاد قطعيّ** — لا يُودَع |

### 1.ح — ملفّات توثيق غير مُتتبَّعة (Untracked Documentation) — 6

`ARCHITECTURAL_DECISION_REGISTER.md` · `BRD_CLARIFICATION_REGISTER.md` · `CURRENT_STATE_INVENTORY.md` · `ENTERPRISE_ARCHITECTURE_REVIEW_VALIDATION.md` · `MASTER_EXECUTION_PLAN.md` · `TRACEABILITY_MATRIX.md`
النوع=توثيق · مقصودة (منتجات تخطيط) · ملاحظة: `.gitignore` يتجاهل مجلّد `Docs/` لكن هذه بالجذر ⇒ ليست مُتجاهَلة · **قرار مالك** بشأن التتبُّع.

### 1.ط — ملفّات مؤقّتة / تعذّر التصنيف

- **مؤقّتة:** لا شيء (لا `.tmp`/`.log`/dumps في شجرة العمل).
- **تعذّر التصنيف:** لا شيء — كل المدخلات صُنِّفت بدليل كافٍ.

---

## 2. تجميع مجموعات التغيير (Changeset Clustering)

> **ملاحظة ترابط حاكمة:** المجموعات C1–C5 **ليست قابلة للفصل إلى إيداعات مستقلّة تُبنى وحدها**، لأن الملفّات المشتركة المُعدَّلة (AppDbContext/DI/Program/App.tsx/DashboardShell/api.ts) تربطها. تُوصَف كوحدات منطقية لكنها **تُودَع ذرّيًّا معًا** (راجع القسم 8).

### C1 — Execution Taxonomy Catalog
- **الهدف:** كتالوج تصنيفات تنفيذ (قوائم SingleSelect ثابتة لقوالب v3/v4).
- **الملفات:** `ExecutionTaxonomyValue.cs` · `ExecutionTaxonomyConfigurations.cs` · `ExecutionTaxonomyModels.cs` · `IExecutionTaxonomyService.cs` · `ExecutionTaxonomyService.cs` · `ExecutionTaxonomySeeder.cs` · `ExecutionTaxonomyController.cs` · `ExecutionTaxonomyOptionsController.cs` · هجرة `20260708232456` · Front: `useExecutionTaxonomy.ts` · `ExecutionTaxonomyManagementPage.tsx`(+test) · `TaxonomySelect.test.tsx` · اختبارات `ExecutionTaxonomyAdminTests`/`ExecutionTaxonomyOptionsTests`/`TemplateTaxonomyV4Tests` · تعديلات مشتركة: AppDbContext/DI/Program/TemplateSeeder.
- **الاعتماديات:** projects/templates قائمة. **هجرة:** `20260708232456` (مستقلّة). **تغطية:** 3 ملفات اختبار. **الاكتمال:** كامل (كل الطبقات). **قابلية البناء:** خضراء ضمن الحزمة الكاملة. **إيداع منفصل؟** لا (مشترك مع البقية). **طور نشر منفصل؟** ممكن (الهجرة مستقلّة) لكن يُفضَّل مع الحزمة.

### C2 — Project Workstreams
- **الهدف:** تيّارات عمل داخل المشروع (فريق/مدير مسؤول).
- **الملفات:** `ProjectWorkstream.cs` · `ProjectWorkstreamConfiguration.cs` · `WorkstreamModels.cs` · `IProjectWorkstreamService.cs` · `ProjectWorkstreamService.cs` · `ProjectWorkstreamsController.cs` · هجرة `20260709222126` · Front: `useProjectWorkstreams.ts` · `ProjectWorkstreams.test.tsx` · تعديل `ProjectDetailPage.tsx` · اختبار `ProjectWorkstreamsTests` · مشترك: Enums(WorkstreamStatus)/Roles(ProjectPlanManagers)/AppDbContext/DI.
- **الاعتماديات:** `projects` (FK Cascade). **هجرة:** `20260709222126` (بعد Taxonomy). **تغطية:** اختبار تكامل + واجهة. **الاكتمال:** كامل. **قابلية البناء:** خضراء ضمن الحزمة. **إيداع منفصل؟** لا. **طور نشر؟** يعتمد ترتيب الهجرة قبل C3.

### C3 — Workstream Deliverables
- **الهدف:** مخرَجات مخطَّطة داخل تيار العمل (كميّة/ساعات مقدّرة/أولوية).
- **الملفات:** `WorkstreamDeliverable.cs` · `WorkstreamDeliverableConfiguration.cs` · `DeliverableModels.cs` · `IWorkstreamDeliverableService.cs` · `WorkstreamDeliverableService.cs` · `WorkstreamDeliverablesController.cs` · هجرة `20260709231845` · Front: `useWorkstreamDeliverables.ts` · `ProjectDeliverables.test.tsx` · اختبار `WorkstreamDeliverablesTests` · مشترك: Enums(DeliverablePriority)/AppDbContext/DI/ProjectDetailPage.
- **الاعتماديات:** `project_workstreams` (FK Cascade) ⇒ **يعتمد C2**. **هجرة:** `20260709231845` (الأخيرة). **تغطية:** اختبار تكامل + واجهة. **الاكتمال:** كامل. **قابلية البناء:** خضراء ضمن الحزمة. **إيداع منفصل؟** لا. **طور نشر؟** بعد C2 حتمًا.

### C4 — Project-First Execution Aggregation
- **الهدف:** محرّك تجميع تنفيذ مبنيّ على المشروع + شيما أرقام تشغيليّة + تقارير/لوحات.
- **الملفات:** `ProjectFirstExecutionSchema.cs` · `IProjectFirstExecutionAggregationService.cs` · `ProjectFirstExecutionModels.cs` · `ProjectFirstExecutionAggregationService.cs` · `ProjectFirstExecutionAggregationController.cs` · تعديل `SubmissionService.cs` (تحقّق أرقام مقصور) · Front: `useProjectExecution.ts` · `TeamLeaderExecutionPage.tsx` · `TeamLeaderProjectExecutionPage.tsx`(+test) · اختبار `ProjectFirstExecutionAggregationTests` · مشترك: DI/App.tsx/api.ts/ReportCalendarPolicy.
- **الاعتماديات:** submissions/templates قائمة + Schema. **هجرة:** لا (يقرأ فقط). **تغطية:** اختبار تكامل. **الاكتمال:** كامل. **قابلية البناء:** خضراء ضمن الحزمة. **إيداع منفصل؟** لا. **طور نشر؟** لا هجرة ⇒ code-only.

### C5 — Frontend Navigation Refactor (navConfig)
- **الهدف:** استخراج نموذج التنقّل إلى `navConfig` + مكوّنات UI مشتركة (تنظيم بصريّ بلا تغيير صلاحية/مسار).
- **الملفات:** `navConfig.ts` · `HeaderActions.tsx` · `Collapsible.tsx` · `ShowMore.tsx` · `StickyBar.tsx` · `Tabs.tsx` (+ اختباراتها) · `DashboardShell.execution.nav.test.tsx` · `DashboardShell.portfolio.nav.test.tsx` · تعديل `DashboardShell.tsx`(+378) · `DashboardShell.nav.test.tsx`.
- **الاعتماديات:** لا خلفية. **هجرة:** لا. **تغطية:** اختبارات nav متعددة. **الاكتمال:** كامل. **قابلية البناء:** خضراء ضمن الحزمة (DashboardShell يستورد navConfig/HeaderActions غير المُتتبَّعَين). **إيداع منفصل؟** لا (DashboardShell مشترك). **طور نشر؟** واجهة فقط.

### C6 — ERDS Test Cleanup (حذف)
- **الهدف:** إزالة اختبارات ERDS Phase 5/5.5/6 + تقليص ReportsTests/ErdsPhase3.
- **الملفات:** حذف 3 ملفات اختبار · تعديل `ReportsTests.cs`(−918) · `ErdsPhase3RolloutTests.cs`.
- **⚠ تنبيه:** الكود المصدري لـ Pod/Executive **باقٍ حيًّا** (القسم 3) ⇒ **لا يُخلَط بمجموعات RC-4 دون قرار مالك**. **قابلية البناء:** الاختبارات المتبقّية لا تشير لـ Pod/Executive (0). **إيداع منفصل؟** يُفضَّل التزامًا مستقلًّا موصوفًا. **طور نشر؟** اختبارات فقط.

### C7 — Planning Documentation
- **الهدف:** 6 وثائق تخطيط بالجذر. **الاعتماديات:** لا. **إيداع منفصل؟** نعم (يجب فصلها عن الكود). **قرار مالك** بشأن التتبُّع أصلًا.

### C8 — Build Artifacts (استبعاد)
- `publish-p0p1/`. **ليست مجموعة تطوير** — تُستبعَد.

---

## 3. مراجعة الملفّات المحذوفة (Deleted File Review)

| الملف المحذوف | كان يحوي | الحذف مقصود؟ | بديل موجود؟ | إشارات متبقّية؟ | قد يكسر؟ | التوصية |
|---------------|----------|--------------|-------------|-----------------|----------|---------|
| `ErdsPhase55WorkUnitTests.cs` (441 سطر) | اختبار تطبيع «وحدة العمل» فوق قوالب التنفيذ الستة + قراءة ساعات العمل/المشروع | مرجَّح (تنظيف مع RC-4) | **جزئيًّا** — `ProjectFirstExecutionAggregationTests` يغطّي المحرّك الجديد لا Pod القديم | كود `PodExecutionAggregationService` **حيّ + مُسجَّل في DI** | لا يكسر البناء (اختبار) لكن **يترك كود Pod الحيّ بلا تغطية** | **يتطلّب مراجعة المالك** |
| `ErdsPhase5PodExecutionTests.cs` (384 سطر) | اختبار محرّك تجميع Pod (TableGrid) | مرجَّح | جزئيًّا (المحرّك الجديد Project-First) | `PodExecutionAggregationController` + service **حيّان** | كسر تغطية فقط | **يتطلّب مراجعة المالك** |
| `ErdsPhase6ExecutiveDashboardTests.cs` (372 سطر) | اختبار 7 لوحات تنفيذية فوق Phase 4/5 | مرجَّح | لا بديل مباشر | `ExecutiveDashboardController` + `ExecutiveDashboardService` + models **حيّة** | كسر تغطية فقط | **يتطلّب مراجعة المالك** |

**الحكم:** الحذف **لا يكسر بناءً ولا مسارًا ولا هجرة** (ملفات اختبار فقط، ولا اختبار متبقٍّ يشير لـ Pod/Executive). لكنه **يزيل تغطية كود إنتاجيّ ما زال مُسجَّلًا ويعمل** (`IPodExecutionAggregationService` باقٍ في DI، ومتحكّمات Pod/Executive باقية). النيّة المرجَّحة = Project-First يحلّ محلّ Pod، **لكن الإحلال غير مكتمل** (لم يُزَل كود Pod/Executive). **لا تُستعَد ولا تُحذَف بقية Pod الآن** — قرار مالك: إمّا (أ) تأكيد الإحلال وإزالة كود Pod/Executive لاحقًا كتغيير منفصل، أو (ب) استعادة التغطية إن بقي Pod مدعومًا.

---

## 4. تدقيق سلسلة الهجرات (Migration Chain Audit)

**السلسلة المحلّية (مرتّبة زمنيًّا):**
`… → 20260706092852_AddCourseCatalog → 20260706230935_AddServiceCatalog` **(آخر هجرة مُتتبَّعة)** `→ 20260708232456_AddExecutionTaxonomyCatalog → 20260709222126_AddProjectWorkstreams → 20260709231845_AddWorkstreamDeliverables` **(الثلاث غير مُتتبَّعة)**.

| المعيار | AddExecutionTaxonomyCatalog | AddProjectWorkstreams | AddWorkstreamDeliverables |
|---------|------------------------------|------------------------|----------------------------|
| **الاسم/الطابع** | 20260708232456 | 20260709222126 | 20260709231845 |
| **السلف** | 20260706230935_AddServiceCatalog (مُتتبَّع) | 20260708232456 (Designer يؤكّد) | 20260709222126 (Designer يؤكّد) |
| **اتّساق المخطَّط** | ✔ ModelSnapshot المُعدَّل (+190) يعكس الجداول الثلاثة | ✔ | ✔ |
| **الجداول** | `execution_taxonomy_values` | `project_workstreams` | `workstream_deliverables` |
| **الأعمدة** | 9 (Code/NameAr/NameEn/Domain/IsActive/SortOrder/Ts) | 12 (ProjectId/TypeCode/Name/TeamId/ManagerId/Status/…) | 15 (WorkstreamId/TypeCode/UsageContext/Qty/Hours/Dates/Priority/…) |
| **الفهارس** | فريد (Domain,Code) | (ProjectId) + فريد (ProjectId,TypeCode,TeamId) | (WorkstreamId) |
| **المفاتيح الأجنبية** | لا | → `projects` **Cascade** | → `project_workstreams` **Cascade** |
| **سلوك الحذف** | — | Cascade (حذف مشروع ⇒ حذف تيّاراته) | Cascade (حذف تيّار ⇒ حذف مخرَجاته) |
| **Nullability** | NameEn/UpdatedAt فقط nullable | ManagerId/Notes/UpdatedAt nullable | UsageContext/Hours/Dates/Responsible/Notes/UpdatedAt nullable |
| **ترحيل بيانات** | لا (جدول جديد فارغ؛ يُبذَر عبر Seeder idempotent) | لا | لا |
| **توافق خلفي** | ✔ (لا مساس بجداول قائمة) | ✔ | ✔ |
| **إضافيّ فقط؟** | ✔ CREATE TABLE فقط | ✔ CREATE TABLE فقط | ✔ CREATE TABLE فقط |
| **يعتمد كيانات/إعدادات غير مُودَعة؟** | نعم (Entity/Config غير مُتتبَّعة) ⇒ **ذرّي** | نعم ⇒ ذرّي | نعم ⇒ ذرّي |
| **رولباك صالح بنيويًّا؟** | ✔ Down=DropTable | ✔ Down=DropTable | ✔ Down=DropTable |
| **ترتيب صحيح؟** | ✔ | ✔ (بعد Taxonomy) | ✔ **يجب أن يلي Workstreams** (FK) — الطابع الزمني يضمن ذلك |

**تباعد عن سلسلة الإنتاج:** آخر هجرة مُتتبَّعة = `20260706230935_AddServiceCatalog`. الثلاث الجديدة **غير مُودَعة** ⇒ ليست في أي فرع منشور. **لا يمكن تأكيد سلسلة الإنتاج من المستودع** — أي مطابقة لسلسلة الإنتاج مُصنَّفة `[UNV]` وتتطلّب تحقّقًا مستقلًّا (فحص `__EFMigrationsHistory` على الإنتاج مباشرةً) قبل أي نشر. **لم تُشغَّل أي هجرة في هذا التدقيق.**

**الحكم:** السلسلة الثلاثية **إضافية بحتة، مرتّبة صحيحًا، رولباكها صالح، لا ترحيل بيانات، متوافقة خلفيًّا** — **آمنة بنيويًّا** بشرط إيداعها ذرّيًّا مع كياناتها/إعداداتها والتحقّق من نقطة اتّصال الإنتاج قبل النشر.

---

## 5. تدقيق اكتمال الكود (Code Completeness Audit)

| الطبقة | C1 Taxonomy | C2 Workstreams | C3 Deliverables | C4 Project-First Agg |
|--------|:-----------:|:--------------:|:---------------:|:--------------------:|
| Domain Entity | ✔ كامل | ✔ كامل | ✔ كامل | ▲ N/A (قراءة) |
| Entity Configuration | ✔ | ✔ | ✔ | ▲ N/A |
| DbContext Registration | ✔ (DbSet) | ✔ | ✔ | ▲ N/A |
| Migration | ✔ | ✔ | ✔ | ▲ لا هجرة |
| Application Contract (I…) | ✔ | ✔ | ✔ | ✔ |
| Service | ✔ | ✔ | ✔ | ✔ |
| Authorization | ✔ `[Authorize]` (لا Anonymous) | ✔ | ✔ | ✔ (نطاق Scope∪Portfolio) |
| Controller/API | ✔ (+Options) | ✔ | ✔ | ✔ |
| DTOs/Models | ✔ | ✔ | ✔ | ✔ |
| Validation | ✔ | ✔ | ✔ | ✔ (تحقّق أرقام مقصور بالشيما) |
| Frontend Types | ✔ (api.ts +185) | ✔ | ✔ | ✔ |
| API Client/Hook | ✔ useExecutionTaxonomy | ✔ useProjectWorkstreams | ✔ useWorkstreamDeliverables | ✔ useProjectExecution |
| Page/Components | ✔ Mgmt page | ✔ ProjectDetail | ✔ ProjectDetail | ✔ 2 لوحتان |
| Navigation/Route | ✔ (App.tsx) | ▲ ضمن ProjectDetail | ▲ ضمن ProjectDetail | ✔ (مساران) |
| Tests | ✔ 3 ملفات | ✔ (int+ui) | ✔ (int+ui) | ✔ (int) · **نتائج UNV** |
| Documentation | ✱ Unverified | ✱ Unverified | ✱ Unverified | ✱ Unverified |

**الأسطورة:** ✔ Complete · ▲ Not required/مضمّن · ✱/UNV Unverified.
**الحكم:** الطبقات الأربع **مكتملة عموديًّا (Full-Stack)**. النقص الوحيد = **تأكيد نتائج التشغيل** (لم تُشغَّل الاختبارات لتفادي الكتابة على قاعدة الاختبار المشتركة) + توثيق المستخدم (غير مُتحقَّق منه).

---

## 6. مراجعة مخاطر البناء الثابتة (Static Build Risk Review)

| الفحص | النتيجة | الدليل |
|-------|---------|--------|
| مراجع مفقودة | **خطر عند الإيداع الجزئي فقط** | AppDbContext/DI/Program/SubmissionService (مُتتبَّعة) تشير لكيانات/خدمات/شيما **غير مُتتبَّعة** ⇒ إيداع المُتتبَّع وحده = فشل ترجمة |
| استيرادات محذوفة | لا مشكلة | لا اختبار متبقٍّ يشير لـ Pod/Executive (grep=0) |
| أنواع مكرّرة | لا | الكيانات/الأنواع جديدة بأسماء فريدة |
| تعارض مجالات أسماء | لا | namespaces جديدة (ExecutionTaxonomy/Clients) |
| تعارض مسارات (Routes) | لا | 3 مسارات front جديدة فريدة (`/app/execution-reports`, `/app/execution/team-dashboard`, `/app/execution-taxonomy`) |
| تعارض هجرات | لا | طوابع زمنية فريدة متصاعدة، سلف صحيح |
| ملفات مُولَّدة غير صالحة | لا | ModelSnapshot متّسق مع الهجرات |
| كود ميّت | **محتمل** | كود ERDS Pod/Executive حيّ بلا تغطية اختبار (بعد الحذف) — قد يصبح ميّتًا إن اكتمل الإحلال |
| ميزات ناقصة غير مستخدمة | لا | كل الوحدات موصولة طرفًا لطرف |
| تطابق عقد Front/Back | ✔ ظاهريًّا | api.ts(+185) يعكس DTOs الخلفية؛ **غير مُتحقَّق منه بالبناء** |
| ثغرات تفويض | لا مكتشَفة | كل المتحكّمات الجديدة `[Authorize]`، لا `[AllowAnonymous]` |
| تحقّق مفقود | لا | SubmissionService يضيف تحقّق أرقام مقصورًا بالشيما (متوافق خلفيًّا) |
| IDOR/BOLA محتمل | يتطلّب تحقّق تشغيليّ | C4 يعتمد `IScopeResolver ∪ IClientProjectAccess`؛ سليم تصميميًّا لكن **يجب اختباره** (يدعم توصية A-T5 في خطة التنفيذ) |

**البناء/الاختبارات:**
- **لم يُشغَّل `dotnet build` ولا الاختبارات** حفاظًا على قاعدة الاختبار المشتركة الدائمة `reporting_test` (اختبارات التكامل تكتب عليها) ⇒ **النتيجة الحيّة: UNVERIFIED**.
- **التقييم الثابت:** الحزمة **متّسقة داخليًّا وقابلة للبناء إن أُودعت كاملةً معًا**؛ **غير قابلة للبناء إن أُودعت الملفات المُتتبَّعة المُعدَّلة وحدها**.

---

## 7. مراجعة نظافة الملفّات (File Hygiene Review)

| البند | موجود؟ | مُغطّى بـ .gitignore؟ | ملاحظة |
|-------|:------:|:---------------------:|--------|
| مخرجات بناء (`publish-p0p1/`) | **نعم** | ❌ **لا** (النمط `**/publish/` + `**/publish-rc*/` لا يطابق `publish-p0p1`) | **يجب استبعاده؛ يُوصى بإضافة نمط** `**/publish-*/` مستقبلًا (لا تعديل الآن) |
| مخرجات تغطية (coverage) | لا | (يُغطّى TestResults/) | — |
| تصديرات مؤقّتة | لا | — | — |
| ملفّات بيئة محلّية | لا في الشجرة | ✔ (`*.env`, `appsettings.*.json`) | جيّد |
| ملفّات IDE | لا | ✔ (`.vscode/`, `.idea/`) | جيّد |
| سجلّات (logs) | لا | ✔ (`*.log`) | جيّد |
| كاش مُولَّد | لا | ✔ (`bin/`,`obj/`,`.vite/`) | جيّد |
| Database dumps | لا | — | جيّد |
| تهيئة حسّاسة | لا | ✔ | جيّد |
| وثائق (`Docs/`) | مُتجاهَل | ✔ | لكن الوثائق الست بالجذر **ليست** مُتجاهَلة |

**الفجوة الوحيدة:** `publish-p0p1/` يفلت من التجاهل. **لا تُحذَف ولا يُعدَّل .gitignore ضمن هذا التدقيق** — يُوثَّق فقط.

---

## 8. خطّة الإيداع المُقترَحة (Recommended Commit Plan)

> **لا يُنفَّذ أي إيداع.** خطّة نظرية للمراجعة.

| # | العنوان المقترَح | الملفّات | الغرض | الاعتماديات | فحوص مطلوبة | هجرة؟ | Front/Back ذرّي؟ | الخطر | اعتبار الرولباك |
|---|------------------|---------|-------|-------------|-------------|:-----:|:----------------:|-------|-----------------|
| **0** | (قبل كل شيء) **استبعاد** `publish-p0p1/` | — | منع إيداع مخرجات بناء | — | `git check-ignore` | لا | — | — | لا يُدرَج أصلًا |
| **1** | `RC-4 Project-First Execution: Taxonomy + Workstreams + Deliverables + Aggregation` (**حزمة ذرّية واحدة**) | كل C1+C2+C3+C4+C5 (الملفّات المُتتبَّعة المُعدَّلة + كل الجديدة المصدر/الهجرات/الاختبارات/الواجهة) | تسليم حزمة RC-4 كاملة قابلة للبناء | آخر هجرة مُتتبَّعة `20260706230935` | `dotnet build` + `dotnet test`(بيئة معزولة) + `tsc`+`vite build` + `vitest` | **نعم (3 هجرات إضافية)** | **نعم — إلزاميّ** | متوسط | Backup DB + عكس الهجرات DropTable×3 |
| **2** | `test: remove superseded ERDS Phase 5/5.5/6 tests` (C6) | حذف 3 اختبارات + تعديل ReportsTests/ErdsPhase3 | تنظيف تغطية ERDS المُحلَّة | **قرار مالك (القسم 3)** | `dotnet test` | لا | لا (خلفية اختبار) | منخفض | استعادة الملفات من التاريخ |
| **3** | `docs: execution planning package` (C7) | 6 ملفّات `.md` بالجذر | توثيق تخطيط | قرار مالك بالتتبُّع | مراجعة | لا | لا | منخفض | حذف من التتبُّع |

**قاعدة الذرّية:** الإيداع #1 **لا يُقسَّم** إلى خلفية/واجهة منفصلتين ولا إلى C1..C5 منفصلة، لأن الملفّات المشتركة المُعدَّلة تربطها ويكسر تقسيمها البناء.

**بديل مُحافِظ (إن رُغِب تقليل حجم الإيداع):** يمكن نظريًّا فصل C5 (nav refactor) بإيداع سابق **فقط لو** فُصلت تعديلات DashboardShell/navConfig/HeaderActions معًا وبُنيت مستقلّة؛ لكن App.tsx (يستورد صفحات C4) يظلّ يربط الواجهة بالخلفية ⇒ الأبسط والأأمن = إيداع واحد.

---

## 9. جدول قرار الاستقرار (Stabilization Decision Table)

| المجموعة | التصنيف |
|----------|---------|
| **C1 Execution Taxonomy** | **جاهزة للإيداع** (ضمن الحزمة الذرّية) |
| **C2 Project Workstreams** | **جاهزة للإيداع** (ضمن الحزمة الذرّية) |
| **C3 Workstream Deliverables** | **جاهزة للإيداع** (ضمن الحزمة الذرّية) |
| **C4 Project-First Aggregation** | **جاهزة للإيداع** (ضمن الحزمة الذرّية) |
| **C5 Frontend Nav Refactor** | **جاهزة للإيداع** (ضمن الحزمة الذرّية) |
| **C6 ERDS Test Cleanup** | **يتطلّب قرار مالك** (حذف تغطية لكود Pod/Executive الحيّ) |
| **C7 Planning Docs** | **يتطلّب قرار مالك** (تتبُّع أم لا) |
| **C8 `publish-p0p1/`** | **يُستبعَد كمخرجات بناء** |
| **نتائج تشغيل الاختبارات** | **تعذّر التحديد (UNV)** — لم تُشغَّل حفاظًا على القاعدة المشتركة |

---

## 10. التقييم النهائي GO / NO-GO

| القرار | الحكم | الدليل | الشروط المطلوبة |
|--------|-------|--------|-----------------|
| **آمن لإنشاء إيداعات؟** | **CONDITIONAL GO** | الحزمة متّسقة وذرّية؛ لكن يجب استبعاد `publish-p0p1/`، وحسم C6 (قرار مالك)، وعدم تقسيم الحزمة الذرّية | (1) استبعاد `publish-p0p1/` (2) إيداع RC-4 ذرّيًّا (3) فصل C6/C7 بقرار |
| **آمن لتشغيل الاختبارات؟** | **CONDITIONAL GO** | اختبارات التكامل تكتب على `reporting_test` المشتركة الدائمة (تراكم/هشاشة) | تشغيلها فقط على **قاعدة معزولة/قابلة لإعادة التهيئة** (توصية A-T4)؛ **لا** على قاعدة شبيهة بالإنتاج |
| **آمن للبناء؟** | **CONDITIONAL GO** | البناء لا يكتب قاعدة؛ لكن يفشل إن بُنيت الملفّات المُتتبَّعة وحدها | البناء **بعد** ضمّ كل الملفّات غير المُتتبَّعة للحزمة (`dotnet build` + `tsc/vite build` آمنان — لا DB) |
| **آمن لإنشاء Release Candidate؟** | **NO-GO (الآن)** | عمل غير مُودَع + C6 غير محسوم + سلسلة هجرة الإنتاج `[UNV]` | إتمام الإيداع الذرّي + حسم C6 + التحقّق المستقل من `__EFMigrationsHistory` الإنتاج |
| **آمن للنشر؟** | **NO-GO** | لا يُنشَر من شجرة مختلطة؛ سلسلة الإنتاج غير مُتحقَّقة؛ نمط النشر = نسخة معزولة | إيداع + بناء أخضر + تحقّق هجرة الإنتاج + نسخة معزولة + Backup + خطة رولباك (نمط عمليات النشر السابقة) |
| **آمن لبدء عمل ميزات جديد؟** | **NO-GO (حتى الاستقرار)** | 91 مدخلًا غير مُستقرّ فوقها بناء جديد = مخاطرة فقدان/تعارض | تثبيت الشجرة أولًا (إيداع RC-4 + استبعاد الأرتيفاكت + حسم C6/C7) ثم البدء |

---

## الحكم الختامي

شجرة العمل **مفهومة وقابلة للإيداع بأمان بشرط ثلاثة إجراءات**: (1) **استبعاد** `reporting-backend/publish-p0p1/` (مخرجات بناء تفلت من `.gitignore`)، (2) **إيداع حزمة RC-4 «Project-First Execution» ذرّيًّا** (الخلفية+الواجهة+الهجرات الثلاث الإضافية معًا — التقسيم يكسر البناء)، (3) **قرار مالك** بشأن حذف اختبارات ERDS Phase 5/5.5/6 (كود Pod/Executive المصدريّ ما زال حيًّا ومُسجَّلًا) وبشأن تتبُّع وثائق التخطيط الست. الهجرات الثلاث **إضافية بحتة، مرتّبة، رولباكها صالح، بلا ترحيل بيانات، متوافقة خلفيًّا**. سلسلة هجرة الإنتاج **غير قابلة للتأكيد من المستودع (`[UNV]`)** وتتطلّب تحقّقًا مستقلًّا قبل أي نشر.

> **قيود مُطبَّقة:** فحص للقراءة فقط. لم يُعدَّل/يُدرَج/يُودَع/يُستعَد/يُحذَف/يُنقَل/يُعاد تسمية أي ملف. لم تُشغَّل هجرة/قاعدة/نشر/تهيئة/بناء/اختبار. لم يُبدأ تنفيذ HORIZON A. الهدف المتحقَّق: استعادة شجرة عمل مضبوطة ومفهومة وقابلة للإيداع بأمان.
