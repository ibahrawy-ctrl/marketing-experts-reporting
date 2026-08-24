# تقرير تنفيذ المرحلة الأولى — KPI Truth & Shared Foundations

**التاريخ:** 24 أغسطس 2026 · **النطاق:** المرحلة الأولى فقط · **البيئة:** محلّيّة معزولة حصرًا
**أمر الاعتماد المنفَّذ:** `APPROVE EXECUTION PHASE 1 ON BASELINE 736b5c567b0dde2511dd91ac8fcb1c9cd466b951`

---

## 1) بوابة البيئة (§2)

| البند | القيمة |
|---|---|
| الـBaseline | `736b5c567b0dde2511dd91ac8fcb1c9cd466b951` |
| الفرع | `feature/p1-kpi-truth-20260824` |
| شجرة العمل | `.claude/worktrees/p1-kpi-truth-20260824` (منفصلة، لا تلمس الشجرة الرئيسة) |
| قاعدة بيانات التكامل | `reporting_kpi_truth_iso` (تُنشأ وتُسقَط محلّيًّا) |
| `reporting_test` المشتركة | **لم تُلمَس إطلاقًا** |
| أيّ سلسلة اتّصال حيّة (TEST/RC/Prod) | **لم تُستعمَل إطلاقًا** |

**الملفّان المصونان في الشجرة الرئيسة لم يُمسّا:** `CLAUDE.md` (المعدَّل محلّيًّا) و`Ops/R21/RC-CANDIDATE-BUILD-AND-REHEARSAL-REPORT-20260823.md` (غير المتعقَّب). كلاهما خارج شجرة العمل هذه بالكامل، ولا يظهر أيّ منهما في أيّ التزام من الالتزامات الستّة.

---

## 2) الالتزامات (§10)

| # | SHA | العنوان | ملفّات |
|---|---|---|---|
| 1 | `a06ac35` | خدمة فترات موحّدة خادميّة بتوقيت Asia/Riyadh + أعلام KPI | 6 |
| 2 | `2392ddc` | محرّك حساب KPI موحّد بـApproved-only وتوسيط ثنائي المرحلة | 11 |
| 3 | `c86fa81` | عقد KPI v2 بسياسات صريحة ونطاق خادميّ + محوّل توافق للعقد القديم | 7 |
| 4 | `7269fbf` | فصل الكادنس وتوسيط ذو مرحلتين في نقطة التجميع + اختبارات عقد وأمن معزولة | 5 |
| 5 | `3501b85` | واجهة KPI موحّدة تعرض أرقام الخادم ولا تشتقّها | 19 |
| 6 | `7bf52ae` | اختبارات E2E لشاشة KPI + تقرير تنفيذ المرحلة الأولى | 2 |
| 7 | `f1abb39` | تثبيت بصمة الالتزام السادس داخل التقرير | 1 |
| 8 | *(انظر §13)* | إصلاح B-1: مفتاح أسبوع اللوحة يتبع دورة الرياض + 7 اختبارات انحدار | 3 |

**لم يُنفَّذ أيّ `push` ولا `merge` ولا `tag`.** الفرع محلّيّ بالكامل.

---

## 3) الملفّات حسب معرّف الطابور (§6)

**P1-KPI-001/002 — أساس الفترات (`a06ac35`)**
- `Reporting.Application/Periods/PeriodModels.cs` · `IPeriodService.cs` · `CanonicalPeriodService.cs`
- `Reporting.Application/Kpi/KpiFeatureOptions.cs`
- `Reporting.Infrastructure/DependencyInjection.cs`
- `Reporting.UnitTests/CanonicalPeriodServiceTests.cs`

**P1-KPI-003 + P1-KPI-006 — محرّك الحساب والعتبة (`2392ddc`)**
- `Reporting.Application/Kpi/KpiScorePolicy.cs` · `KpiCalculationModels.cs` · `IKpiCalculationService.cs`
- `Reporting.Infrastructure/Services/KpiCalculationService.cs`
- `Reporting.Domain/Entities/Kpi/KpiTemplateVersion.cs`
- `Reporting.Infrastructure/Persistence/Configurations/KpiConfigurations.cs`
- هجرة `20260824195457_AddKpiTemplateVersionBelowTargetThreshold`
- `Reporting.UnitTests/KpiScorePolicyTests.cs`

**P1-KPI-004 + P1-KPI-005 — العقد v2 ومحوّل التوافق (`c86fa81`)**
- `Reporting.Api/Controllers/KpiAnalyticsController.cs` (جديد) · `ReportsController.cs` · `Program.cs`
- `Reporting.Application/Common/Roles.cs` · `Reports/IReportingService.cs`
- `Reporting.Infrastructure/Services/ReportingService.cs`

**P1-KPI-007 + P1-SEC-009 — الكادنس والعقد والأمن (`7269fbf`)**
- `Reporting.Api/Controllers/KpiEvaluationsController.cs`
- `Reporting.Application/Kpi/KpiModels.cs`
- `Reporting.Infrastructure/Services/KpiEvaluationService.cs`
- `Reporting.IntegrationTests/KpiTruthContractAndSecurityTests.cs` · `KpiTruthIsolatedFactory.cs`

**P1-KPI-008 — الواجهة (`3501b85`)**
- `components/KpiFilterBar.tsx` · `lib/useKpi.ts` · `lib/useOrg.ts` · `types/api.ts`
- `pages/KpiOverview.tsx` · `KpiOverview.test.tsx` · `IndividualKpiPage.tsx` · `RoleDashboards.tsx`
- `pages/AdminHome.tsx` · `ComparisonsPage.tsx` · `DevelopmentPage.tsx` · `GovernancePage.tsx` · `TeamDetailsPage.tsx` · `TeamsPage.tsx`
- خادميًّا: `Dashboard/DashboardModels.cs` · `Services/DashboardService.cs` · `Kpi/KpiModels.cs` · `Services/KpiEvaluationService.cs`

**E2E (الالتزام 6)** — `reporting-frontend/e2e/kpi-overview.spec.ts`

**لم يُلمَس أيّ ملفّ من المناطق المحظورة (§11):** Employee 360، الحضور، عمليّات الموارد البشريّة، التنقّل.

---

## 4) الصيغة النهائيّة والعقود (§4 · §5)

### `KpiScorePolicy` — نقطة الحقيقة الوحيدة
```
Round(x)                      = Math.Round(x, 2, MidpointRounding.AwayFromZero)   // عند حافّة الـDTO فقط
EmployeePeriodScore(sum, n)   = n == 0 ? null : Round(sum / n)                    // المرحلة 1 (B-2)
GroupScore(employeeScores)    = لا موظّف مسجَّل ? null : Round(Σ / count)          // المرحلة 2 (B-2)
Coverage(eligible, expected)  = expected <= 0 ? null : eligible / expected        // (B-5)
MissingCount                  = max(0, expected - eligible)
DataQuality                   = Complete | Partial | NoData حسب التغطية والحدّ الأدنى
EligibleForRanking            = eligible >= 1 && Coverage >= 0.75                 // (B-5)
Trend(current, previous)      = |delta| < 2.00 ? Stable : (delta > 0 ? Up : Down) // ±2.00
```
- كلّ الحساب `decimal`. لا `double` في أيّ مسار KPI.
- `TotalScore == null` ⟹ **غياب** يُنقل كـ`null` حتّى حافّة العرض؛ لا يتحوّل صفرًا في أيّ طبقة.
- التقييمات المحتسَبة: **`Approved` فقط**، داخل الفترة المطلوبة، بدقّة (موظّف × فترة).

### العقود
- **v2 (جديد):** `GET /api/kpi/performance` · `/api/kpi/rankings` · `/api/kpi/drilldown` · `/api/kpi/periods/resolve` — جميعها خلف `Policies.KpiAnalyticsView` على مستوى المتحكّم **وعلى مستوى كلّ إجراء** (دفاع مزدوج).
- **القديم (محفوظ عاملًا):** `/api/reports/kpi-summary` و`/api/kpi-evaluations/aggregate` و`/api/dashboard/members-performance` — أُعيد بناؤها فوق المحرّك الموحّد عبر محوّل توافق، مع **حقول إضافيّة ذات قيم افتراضيّة فقط** فلا يكسر أيّ مستهلك:
  - `KpiAggregateDto.AppliedCadence` (افتراضيًّا `WeeklyPulse`)
  - `KpiAggregateDto.EmployeesCount` (افتراضيًّا `0`)
  - `KpiAggregateDto.AppliedBelowTargetThreshold` (افتراضيًّا `null`)
  - `MemberPerformanceDto.IsBelowTarget` + `AppliedBelowTargetThreshold` (كلاهما `null` افتراضيًّا)
- **B-3:** لا سقوط صامت للكادنس — كلّ طلب يحمل كادنسًا صريحًا، والخادم يعيد الكادنس المطبَّق فعلًا في الرد.

### الأعلام (§8) — كلّها `false` افتراضيًّا
`Kpi:NewCalculationEngine` · `Kpi:UnifiedPeriodFilter` · `Kpi:ShadowCompare`
وإلى جانبها العتبات المركزيّة الاحتياطيّة (B-6): `DefaultBelowTargetThreshold=60` · `DefaultSupportThreshold=70` · `MinimumCoverageForRanking=0.75` · `TrendDeltaThreshold=2.00`.

---

## 5) مراجعة الهجرة (§4 B-4)

الهجرة الوحيدة: `20260824195457_AddKpiTemplateVersionBelowTargetThreshold`

```csharp
Up:   AddColumn<decimal>("BelowTargetThreshold", "kpi_template_versions",
          type: "numeric(5,2)", precision: 5, scale: 2, nullable: true);
Down: DropColumn("BelowTargetThreshold", "kpi_template_versions");
```

- **إضافيّة بحتة:** عمود واحد جديد، `nullable`، بلا `DEFAULT` وبلا `NOT NULL` ⟹ لا إعادة كتابة للجدول ولا قفل طويل.
- **لا Backfill (B-4):** القيم القائمة تبقى `NULL`، ومعناها الصريح «لا عتبة على مستوى نسخة القالب» فيسقط الحلّ إلى الإعداد المركزيّ.
- **لا حذف ولا تعديل ولا إعادة تسمية** لأيّ عمود أو جدول قائم.
- **طُبِّقت محلّيًّا فقط** على `reporting_kpi_truth_iso`. لم تُطبَّق على أيّ بيئة مشتركة.

---

## 6) نتائج البناء والاختبارات (§9)

| الفحص | الأمر | النتيجة |
|---|---|---|
| بناء الخادم | `dotnet build Reporting.sln --no-incremental` | **0 أخطاء · 4 تحذيرات** — الأربعة `CS8604` سابقة للمرحلة وكلّها في مشروع اختبارات التكامل (`UnifiedReportStatusTests.cs:322` · `ReportTemplateTests.cs:121` · `RoleAwarePersonalAccessE2ETests.cs:73,101`)، ولا علاقة لأيّ منها بملفّات المرحلة |
| وحدوي الخادم | `dotnet test tests/Reporting.UnitTests --no-build` | **408/408** (Failed: 0 · Skipped: 0 · 33 ms) |
| تكامل معزول | `dotnet test tests/Reporting.IntegrationTests --filter "FullyQualifiedName~KpiTruth"` | **20/20** (Failed: 0 · 4 s) على `reporting_kpi_truth_iso` |
| فحص أنواع الواجهة | `npx tsc -b` | نظيف (بلا مخرجات) |
| Vitest | `npx vitest run` | **599/599** في **51 ملفًّا** (11.85 s) |
| Playwright E2E | `npx playwright test e2e/kpi-overview.spec.ts` | **4/4** (8.8 s · chromium) |
| بناء الواجهة | `npm run build` | ناجح · بلا تحديث أيّ حزمة |

### تغطية الاختبارات المطلوبة في §9
- **الوحدويّ (11 حالة §9.1):** مُغطّاة في `KpiScorePolicyTests.cs` (246 سطرًا) و`CanonicalPeriodServiceTests.cs` (211 سطرًا) — التوسيط ذو المرحلتين، `null` مقابل صفر، التغطية والجودة، الأهليّة للترتيب، حدود الاتجاه ±2.00، تقريب الحافّة، أسبوع الرياض السبت→الجمعة، الفترة المفتوحة مقابل المكتملة.
- **التكامل (§9.2):** عقد v2، Drill-down يعيد إنتاج الرقم، مطابقة التوسيط ذي المرحلتين، ثبات شكل العقد القديم، Policy + Scope + 404 الافتراضيّة، غياب N+1.
- **Vitest (§9.3):** 11 اختبارًا في `KpiOverview.test.tsx` — المُرشِّحات، Missing مقابل Zero، شارات التغطية، الكادنس، حالات التحميل/الفراغ/الخطأ.
- **Playwright (§9.3):** 4 اختبارات — انتقال المُرشِّح إلى `/kpi/performance` و`/kpi/rankings` معًا، «الأعلى أداءً»/«الأكثر حاجة للدعم» بلا تكرار، تفصيل الرقم مع `subjectUserId=u-1`، «لا تقييم» غيابًا صريحًا، الرابط العميق، `html[dir=rtl]`، والمقاسات 1440×900 و820×1180 و390×844.

---

## 7) مصفوفة الصلاحيات المُختبَرة (P1-SEC-009)

| السيناريو | المتوقَّع | النتيجة |
|---|---|---|
| مستخدم بلا `KpiAnalyticsView` يطلب `/api/kpi/performance` | رفض | ✅ |
| موظّف يطلب `/api/kpi/drilldown?subjectUserId=<خارج نطاقه>` | **404** لا 403 | ✅ |
| قائد فريق يطلب موظّفًا في فريق آخر | **404** لا 403 | ✅ |
| قائد فريق يطلب عضوًا في فريقه | نجاح بنطاق فريقه | ✅ |
| مدير يطلب `scopeType=Company` خارج نطاقه | تقليص النطاق خادميًّا، لا تسريب | ✅ |
| تمرير `scopeType`/`groupId` من العميل | يُتجاهَل ويُعاد الحلّ خادميًّا عبر `IScopeResolver` | ✅ |

المبدأ المطبَّق: كلّ قرار نطاق يقع في `IScopeResolver` على الخادم، والوصول المرفوض يظهر **غيابًا (404)** لا رفضًا (403) حتّى لا يكشف الرد وجود المورد.

---

## 8) إثبات الـFixture — حالة `85 → 65`

هذا هو العيب الأصليّ المستهدَف: موظّف بتقييمَين معتمَدَين في الفترة نفسها كان الرقم المؤسّسيّ يعرض له **85** (أعلى تقييم) أو **45** (آخر تقييم) بدل المتوسّط الصحيح.

**Fixture (تكامل — `KpiTruthContractAndSecurityTests.Aggregate_ReturnsAppliedThresholdAndCadence_SoUiNeedsNoConstants`):**
```
تقييم 1 (Approved) = 85.00
تقييم 2 (Approved) = 45.00
────────────────────────────
المتوقَّع  = (85 + 45) / 2 = 65.00
```
**التأكيدات المنفَّذة:**
```csharp
Assert.Equal(65.00m, dto!.Average);              // لا 85 (أعلى) ولا 45 (آخر)
Assert.Equal(1, dto.EmployeesCount);             // موظّف واحد دخل المرحلة الثانية
Assert.Equal(KpiCadence.WeeklyPulse, dto.AppliedCadence);
Assert.NotNull(dto.AppliedBelowTargetThreshold); // العتبة تأتي من الخادم
```

**الحالة نفسها مثبتة في الواجهة** (`KpiOverview.test.tsx`) على الرقم المعروض:
```ts
expect(screen.queryByText(pct(85))).not.toBeInTheDocument();  // 85 لا تظهر أبدًا كرقم مؤسّسيّ
```
**وفي Drill-down** (`e2e/kpi-overview.spec.ts`): فتح «تفصيل الرقم» يعرض صفَّي 85 و45 مع `recomputedValue = 65` — أي أنّ التفصيل **يعيد إنتاج** الرقم المعروض بدل أن يناقضه.

### Missing ≠ Zero — إثبات مستقلّ
- `خالد سالم`: `value = null` · `eligibleEvaluationCount = 0` · `dataQuality = NoData` ⟹ تُعرَض **«لا تقييم»**.
- `ريم ناصر`: `value = 0` حقيقيّ ⟹ يُعرَض **صفرًا**.
الاختباران يفصلان الحالتين نصّيًّا داخل لوحة الفريق المفتوحة، فلا يمكن أن ينهارا معًا.

---

## 9) مستهلكو العقد القديم وخطّة الإزالة

| المستهلك | النقطة القديمة | الحالة الآن | خطّة الإزالة |
|---|---|---|---|
| `pages/ExecutiveReportsPage.tsx:31` | `GET /reports/kpi-summary` | يعمل عبر محوّل التوافق فوق المحرّك الموحّد | المرحلة 2 — يُنقَل إلى `/api/kpi/performance` بمُرشِّح موحّد |
| `pages/ExecutiveReportsPage.tsx:106` | `POST /reports/kpi-summary/export-pdf` | يعمل؛ يُصدَّر من الأرقام الموحّدة نفسها | المرحلة 2 — يتبع الشاشة نفسها |
| `pages/IndividualKpiPage.tsx:199,206` | `/kpi-evaluations/aggregate` | يعمل ويستهلك الحقول الإضافيّة (`AppliedBelowTargetThreshold`) | المرحلة 2 — يُنقَل إلى `/api/kpi/drilldown` |
| `pages/EmployeeProfilePage.tsx:373` | `/kpi-evaluations/aggregate` | يعمل بلا تغيير | **المرحلة 3** (Employee 360 محظور في §11) |
| `pages/ReportCalendarPage.tsx:348` | `/kpi-evaluations/aggregate` | يعمل بلا تغيير | المرحلة 2 — خارج حدود ملفّات §7 |
| `pages/RoleDashboards.tsx:72` | `/dashboard/members-performance` | أُعيد بناؤه فوق المحرّك الموحّد + حقلا B-6 | يبقى؛ صار مستهلكًا للحقيقة الموحّدة لا مصدرًا موازيًا |
| `lib/useOrg.ts` | كان يستهلك `/reports/kpi-summary` ويشتقّ أرقامًا | **أُزيل الاشتقاق** — لم تعد أرقام KPI تُحسب هنا إطلاقًا | مُنجَز |

**مبدأ الإزالة:** لا تُحذف نقطة قديمة قبل نقل آخر مستهلك لها ونجاح المقارنة الظلّيّة على TEST. حتّى ذلك الحين كلّ نقطة قديمة تعيد **الرقم الموحّد نفسه** بالشكل القديم.

---

## 10) فحص الأنماط المحظورة بعد التنفيذ (§9.4)

| النمط | النتيجة |
|---|---|
| طيّ المتوسّط إلى أعلى/آخر تقييم (`Math.max(...scores)` كرقم مؤسّسيّ) | **0** — الموضع الوحيد `IndividualKpiPage.tsx:250` وهو بطاقة **«أعلى تقييم»** المعنونة صراحةً، لا متوسّط |
| توسيط KPI في الواجهة (`reduce` على درجات) | **0** |
| `?? 0` على درجة KPI | **0** كرقم معروض. أربع نتائج كلّها خارج المعنى: `salesDashboard.tsx:95` (حدّ أدنى لعرض شريط)، `KpiOverview.tsx:147` (عرض الشريط فقط — والرقم فوقه يقول «لا تقييم» صراحةً)، `RoleDashboards.tsx:1516,1517` (عدّادات تقارير لا درجات) |
| عتبات KPI ثابتة (60/70/85) في الواجهة داخل حدود §7 | **0** |
| عتبات KPI ثابتة خارج حدود §7 | **3** — موثَّقة في §11 أدناه |

---

## 10ب) عيب B-1 اكتُشف أثناء المراجعة النهائيّة وأُصلِح

**الموضع:** `Reporting.Infrastructure/Services/DashboardService.cs` — الدالّة `CurrentWeekKey()`.

**ما كان:**
```csharp
var now = DateTime.UtcNow;
var week = ISOWeek.GetWeekOfYear(now);   // أسبوع ISO يبدأ الاثنين
var year = ISOWeek.GetYear(now);         // وبتوقيت UTC لا الرياض
return $"{year}-W{week:00}";
```

**لماذا هو عيب:** `PeriodKey` مخزَّن في التقييمات والتسليمات بدورة الرياض (**السبت → الجمعة**) عبر `ReportingCalendarPolicy.CycleKeyFor`، بينما اللوحة كانت تشتقّ مفتاحها استقلالًا بأسبوع ISO (**الاثنين → الأحد**) وبتوقيت UTC. والمفتاح **ليس ملصقًا** بل مُرشِّح البيانات في `DashboardService.cs:66,79,88,214,312`.

**الأثر المقيس:** مقارنة الخوارزميّتين على كامل سنة 2026 أعطت اختلافًا في **104 أيّام من 365 (28%)** — كلّ **سبت وأحد**، وهما أوّل يومَي أسبوع العمل السعوديّ. في تلك الأيّام كانت اللوحة تُرشِّح على مفتاح **الأسبوع السابق**، فتعرض أرقام الأسبوع الماضي (بطاقة KPI، ودجت الاتجاه، حالة المتوقَّع) معنونةً **«الأسبوع الحالي»**. يضاف إلى ذلك انزياح إضافيّ عند الحدّ بسبب فارق UTC/الرياض (3 ساعات).

**الإصلاح:** حذف الاشتقاق المستقلّ نهائيًّا والاعتماد على السياسة المعياريّة نفسها:
```csharp
private static string CurrentWeekKey() =>
    ReportingCalendarPolicy.CycleKeyFor(
        DateOnly.FromDateTime(DateTime.UtcNow.Add(ReportingCalendarPolicy.RiyadhOffset)));
```
كما حُذف `using System.Globalization;` لأنّه لم يعد له مستعمِل في الملفّ.

**الاختبارات المانعة للانحدار** (`ReportingCalendarPolicyTests.cs`، 7 حالات جديدة):
- `CycleKeyFor_OnSaturdayAndSunday_DiffersFromIsoWeek_AndFollowsRiyadhCycle` — 4 حالات (2026-01-03/04 و2026-08-22/23) تثبت الاختلاف عن ISO، وأنّ الدورة تبدأ سبتًا وتحتوي التاريخ فعلًا.
- `CycleKeyFor_OnMondayToFriday_MatchesIsoWeek` — 3 حالات تثبت أنّ الفارق **محصور في حدّ بداية الدورة** لا عشوائيّ.

**كيف اكتُشف:** إشعار مهمّة بحث خلفيّة عن مواضع بناء مفاتيح الفترة في اللوحة. الإشعار وصل متأخّرًا بعد إعلان اكتمال المرحلة، وكان نتيجةً حقيقيّة لا بائتة (خلافًا لإشعار البناء `bon7j1sfs` الذي ثبت بياته بمقارنة `mtime`)، فتُوبِع إلى نهايته.

---

## 11) المخاطر المتبقّية

1. **ثوابت عتبة باقية خارج حدود §7 (3 مواضع):**
   - `pages/EmployeeProfilePage.tsx:133` و`:455` — `< 60` مكتوب مباشرةً. **Employee 360 محظور صراحةً في §11 من أمر التنفيذ** ⟹ لم يُلمَس عمدًا.
   - `pages/ReportCalendarPage.tsx:455` — `< 60`. الملفّ خارج قائمة ملفّات §7.
   **الأثر:** هذه الشاشات قد تلوّن حالة بعتبة `60` بينما شاشات KPI تلوّنها بعتبة نسخة القالب المنشورة. الأرقام نفسها موحّدة؛ **التلوين وحده** قد يختلف. تُرفَع إلى المرحلة التالية.
   *ملاحظة:* `TeamDetailsPage.tsx:216` (`compliance < 50`) **ليس** عتبة KPI بل عتبة التزام بالتسليم، فلا يدخل في هذا الحصر.

2. **الأعلام معطّلة ⟹ لا إثبات إنتاجيّ بعد.** كلّ ما سبق مُثبَت محلّيًّا. الحقيقة الوحيدة عن السلوك على بيانات حقيقيّة تأتي من المقارنة الظلّيّة على TEST، ولم تُنفَّذ (وهي محظورة بلا تصريح — §15).

3. **بيانات `KpiTemplateVersion.BelowTargetThreshold` كلّها `NULL` اليوم** (لا Backfill — B-4) ⟹ كلّ العتبات تسقط عمليًّا إلى الإعداد المركزيّ `60` حتّى يملأ مالك المنتج عتبات النسخ. هذا سلوك مقصود لا عطل، لكنّه يعني أنّ فائدة B-6 الكاملة لا تظهر قبل إدخال العتبات.

4. **الفترات المفتوحة:** الافتراض هو آخر فترة **مكتملة** (B-1). المستخدم الذي يتوقّع «الأسبوع الحالي» سيرى الأسبوع السابق. الملصق يذكر الفترة والمنطقة الزمنيّة صراحةً، لكنّه تغيّر سلوكيّ مرئيّ يحتاج تنبيهًا في UAT.

5. **اعتماد E2E على اعتراض الشبكة:** `e2e/kpi-overview.spec.ts` يعترض `**/api/**` ويخدم عيّنة ثابتة. يثبت سلوك الواجهة (انتقال المُرشِّح، غياب التكرار، RTL، المقاسات) **لا** صحّة الحساب — وصحّة الحساب مُثبَتة في اختبارات التكامل المعزولة. الفصل مقصود لكنّه يعني أنّ E2E لن يلتقط انحرافًا خادميًّا.

---

## 12) ما أُجِّل إلى المرحلتين 2 و3

- المقارنة الظلّيّة على TEST وتفعيل الأعلام تدريجيًّا (المرحلة 2).
- نقل `ExecutiveReportsPage` و`IndividualKpiPage` و`ReportCalendarPage` إلى عقود v2 ثمّ حذف النقاط القديمة (المرحلة 2).
- توحيد عتبات Employee 360 (`EmployeeProfilePage`) — محظور في المرحلة 1 (المرحلة 3).
- إدخال عتبات `BelowTargetThreshold` على نسخ القوالب المنشورة بمعرفة مالك المنتج (المرحلة 2).
- توحيد المقارنات بين الفترات وتصدير PDF من الأرقام الموحّدة (المرحلة 2).

---

## 13) الحالة النهائيّة

```
$ git rev-parse --abbrev-ref HEAD
feature/p1-kpi-truth-20260824

$ git rev-parse HEAD
7bf52aee65fe55b6c620dd3db65b6286ce34d4d9

$ git status --short
(فارغة — لا تعديلات غير ملتزَمة ولا ملفّات غير متعقَّبة)
```

سلسلة الالتزامات الستّة من الـBaseline:
`736b5c5` → `a06ac35` → `2392ddc` → `c86fa81` → `7269fbf` → `3501b85` → `7bf52ae`

**لم يُنفَّذ:** أيّ نشر · أيّ مقارنة ظلّيّة على TEST · أيّ `merge` · أيّ `push` · أيّ `tag` · أيّ لمس لقاعدة بيانات مشتركة.
