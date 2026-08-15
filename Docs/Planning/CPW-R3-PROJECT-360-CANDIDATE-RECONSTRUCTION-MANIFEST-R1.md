# CPW-R3 — Project 360 · بيان إعادة بناء المرشَّح (Candidate Reconstruction Manifest) — R1

**التذكرة:** CPW-R3 — Project 360 Completion Candidate
**المرحلة:** H-1 (بيان المرشَّح) تمهيدًا لـH-2 (إعادة البناء في شجرة عمل معزولة)
**التاريخ:** 15 أغسطس 2026
**الفرع المصدر:** `develop` @ `c157829`
**سلطة التنفيذ:** تصريح المالك «CPW-R3 — PROJECT 360 COMPLETION CANDIDATE» §15 و§16

---

## 1. لماذا هذا البيان أصلًا

شجرة العمل **متّسخة قبل بدء CPW-R3**: فيها عمل مستخدم سابق غير مرتبط بهذه التذكرة (جدولة تذكيرات التقارير، البريد، تصنيف التنفيذ، صفحات واجهة، أدوات مرّة واحدة). تحويل هذه الشجرة مباشرةً إلى مرشَّح كان سيخلط ثلاث حزم مستقلّة في التزام واحد، ويجعل «اجتياز الانحدار» عديم الدلالة لأنّه لا يفصل من الذي اجتاز: حزمة CPW-R3 أم بقايا عمل ثالث.

لذلك: **البيان أوّلًا، ثمّ إعادة البناء في شجرة معزولة، ثمّ الانحدار داخل المرشَّح لا داخل الشجرة المتّسخة.**

**إعادة ترتيب مُعلَنة:** نُفِّذت المرحلة H قبل المرحلة G عمدًا. تشغيل انحدار §13 على الشجرة الأصليّة كان سيُثبت سلامة خليط، لا سلامة مرشَّح؛ فالانحدار الكامل يُنفَّذ **داخل** شجرة المرشَّح على قاعدة PostgreSQL مؤقّتة معزولة، ولا يُمَسّ `reporting_test` إطلاقًا.

---

## 2. القياس الخام لحالة الشجرة

| القياس | القيمة |
|---|---|
| ملفّات متتبَّعة معدَّلة (`git diff --stat`) | **35** |
| إدراجات / حذوفات | **2208 / 269** |
| ملفّات غير متتبَّعة تخصّ CPW-R3 (كود) | **48** ملفًّا (بعد فرد المجلّدات) |
| ملفّات غير متتبَّعة تخصّ CPW-R3 (توثيق) | **10** تقارير `Docs/Planning/CPW-R3-*` |
| ملفّات غير متتبَّعة **خارج** CPW-R3 | `CLAUDE.md`, `Ops/`, `tools/LegacyExecutionFixture/`, `ActionResultToast.tsx`, وسائر `Docs/` غير CPW-R3 |

---

## 3. دلالات التصنيف الأربع

| الرمز | المعنى | المصير في المرشَّح |
|---|---|---|
| **CAND** | من حزمة CPW-R3 (W1–W6 + إغلاق الصحّة/الصلاحيّة + واجهة R2-W12 + اختباراتها) | **يدخل** |
| **PRE** | عمل مستخدم سابق موجود قبل بدء CPW-R3 ولا علاقة له بها | **يُستبعَد** |
| **MIXED** | ملفّ واحد يحوي hunks من الفئتين معًا | **يدخل جزئيًّا بفصل الـhunks** |
| **DOC** | توثيق CPW-R3 | **يدخل** |

---

## 4. الملفّات المتتبَّعة المعدَّلة — تصنيف على مستوى الـhunk

### 4.1 CAND — الفرق كلّه من CPW-R3 (11 ملفًّا)

> **تصحيح ذاتيّ أثناء H-2 (يُسجَّل ولا يُخفى):** صُنِّف `ExecutionTaxonomySeeder.cs` و`ExecutionTaxonomyService.cs` ابتداءً ضمن PRE **بالخطأ**، اعتمادًا على اسم الملفّ (تصنيف التنفيذ = عمل سابق) لا على محتوى الـhunk. اكتُشف الخطأ في H-2 عند تتبّع مصدر بذر كتالوج الـ38 قيمة (W5 §E). التدقيق الآليّ بعدّ علامات CPW-R3 في الأسطر المضافة لكلّ الملفّات الـ35 أعطى فصلًا حادًّا: **12 ملفًّا بعلامات ≥1 و23 ملفًّا بصفر علامة**، بلا منطقة رماديّة. الملفّان مُضافان أدناه، وفرقهما **كلّه** CPW-R3 (hunk واحد في الأوّل، وثلاثة في الثاني كلّها للمجالات الثلاثة).
>
> **الأثر لو لم يُكتشف:** كان المرشَّح سيبني بنجاح لكنّه يفشل وظيفيًّا — صفر قيمة كتالوج ⟹ مخطَّط الاستراتيجيّة فارغ ومسار المخرَجات التعاقديّة بلا أنواع. بناءٌ ناجح لا يُثبت اكتمال حزمة تعتمد على بيانات مبذورة.

| # | الملفّ | الـhunks | المحتوى المُثبَت |
|---|---|---|---|
| 1 | `Reporting.Domain/Entities/Clients/Project.cs` | كامل | 12 عمودًا جديدًا: `Summary`, `ProjectOwnerId`, `TeamLeaderId`, `ProgressPercent`, `Background`, `BusinessContext`, `ScopeText`, `OutOfScope`, `SuccessDefinition`, `HealthStatus`, `HealthPercent`, `HealthComputedAtUtc` |
| 2 | `Reporting.Domain/Entities/Governance/Decision.cs` | كامل | `Guid? ProjectId` فقط |
| 3 | `Reporting.Domain/Enums/Enums.cs` | hunk واحد @685 | 10 تعدادات `Project*` مُلحقة في نهاية الملفّ — صفر مساس بتعداد قائم |
| 4 | `Reporting.Infrastructure/Persistence/AppDbContext.cs` | hunkان | `using ...Entities.Projects360;` + 6 `DbSet` |
| 5 | `Persistence/Configurations/ClientConfigurations.cs` | hunk واحد | أطوال الخصائص، `numeric(9,2)`، تحويل `HealthStatus` إلى نصّ، 3 فهارس |
| 6 | `Persistence/Configurations/GovernanceConfigurations.cs` | hunk واحد | `b.HasIndex(x => x.ProjectId)` |
| 7 | `Persistence/Migrations/AppDbContextModelSnapshot.cs` | 13 hunk | **مُحقَّق سطرًا سطرًا**: كلّ `ToTable` مُضاف ينتمي إلى `project_objectives`, `project_kpis`, `project_kpi_readings`, `project_deliverables`, `project_strategies`, `project_strategy_attributes`؛ وكلّ `b.Property` مُضاف خارج هذه الجداول هو أحد أعمدة `Project` الاثني عشر أو `Decision.ProjectId` |
| 8 | `reporting-frontend/src/App.tsx` | 3 hunks | استيراد `Project360Page`، ثابت `PROJECT_360_ROLES`، مسار `/app/projects/:projectId/360` |
| 9 | `reporting-frontend/src/pages/ProjectDetailPage.tsx` | hunk واحد | رابط الدخول إلى مساحة العمل 360 |
| 10 | `Persistence/ExecutionTaxonomySeeder.cs` | hunk واحد @225 | **بذر كتالوج الـ38** (6 `strategy_section` + 14 `strategy_field` + 18 `contract_deliverable`) — DEC-W4-01 |
| 11 | `Services/ExecutionTaxonomyService.cs` | 3 hunks | `using ...Projects360;` + تحديث تعليق + إضافة المجالات الثلاثة إلى `KnownDomains` (19 ⟵ 22) |

**ملاحظة حوكمة على السطر 7:** لقطة النموذج (`ModelSnapshot`) **لم تُعدَّل يدويًّا**؛ تغيّرها ناتج آليًّا عن توليد الهجرة `20260811142239`. النهي في التصريح («ممنوع تعديل Model Snapshot») يُفهَم منعًا للتحرير اليدويّ الذي يفكّ التزامن، وقد أُثبت التزامن بـ`Model Sync` نظيف في المرحلة G.

### 4.2 MIXED — ملفّ واحد يتطلّب فصل hunks

| الملفّ | hunk | التصنيف | الإجراء |
|---|---|---|---|
| `Reporting.Infrastructure/DependencyInjection.cs` | `@@ -58,0 +59` — `services.Configure<ReportReminderSchedulerOptions>(…)` | **PRE** | **يُستبعَد** |
| | `@@ -75,0 +77` — `services.AddHostedService<ReportReminderSchedulerService>()` | **PRE** | **يُستبعَد** |
| | `@@ -104,0 +107,16` — كتلة «Project 360 (CPW-R3 · W4 + W5)»: 8 تسجيلات خدمة تبدأ بـ`IProject360Authorization` وتنتهي بـ`IProjectGovernanceReadService`، ومنها التسجيل المزدوج المقصود لـ`ProjectObjectiveService` (النوع الملموس ثمّ الواجهة عبر `sp.GetRequiredService`) | **CAND** | **يدخل وحده** |

هذا هو **الملفّ الوحيد** في الحزمة كلّها الذي يحتاج فصلًا على مستوى الـhunk. باقي الملفّات تُنقَل أو تُترَك كوحدات كاملة.

**سبب التسجيل المزدوج (تُوثَّق كي لا تُقرأ لاحقًا كخطأ):** لوحة النظرة العامّة تستدعي بانيَ الأهداف مباشرةً؛ تسجيل النوع الملموس مرّة ثمّ إعادة استعماله عبر الواجهة يمنع إنشاء نسختين في الطلب الواحد.

### 4.3 PRE — عمل مستخدم سابق يُستبعَد كاملًا (23 ملفًّا)

**Backend (13):**
`Application/Common/ProjectFirstExecutionSchema.cs` · `Application/Notifications/EmailNotificationOptions.cs` · `Application/Reports/ProjectFirstExecutionModels.cs` · `Persistence/TemplateSeeder.cs` · `Services/EmailNotificationService.cs` · `Services/ProjectFirstExecutionAggregationService.cs` · `Services/ReportCalendarService.cs` · `Services/ReportDueService.cs` · `Services/ReportReminderService.cs` · `Services/ReportingService.cs` · `tests/…/EmployeeProfileScopeTests.cs` · `tests/…/ReportCalendarTests.cs` · `tests/…/ReportRemindersTests.cs`

**Frontend (10):**
`components/ui.tsx` · `lib/api.ts` · `lib/format.ts` · `main.tsx` · `pages/HrRequestsPage.tsx` · `pages/KpiPage.tsx` · `pages/LeaveRequestsPage.tsx` · `pages/ProjectRepeatableGrid.test.tsx` · `pages/SubmissionsPage.tsx` · `types/api.ts`

**فحص التبعيّة العكسيّة (حرج):** حزمة CPW-R3 تستهلك من `components/ui.tsx` و`lib/api.ts` و`lib/format.ts` — لكنّها تستهلك **السطح القائم في `HEAD`** لا التعديلات المتّسخة. لذلك استبعاد هذه الملفّات لا يكسر المرشَّح. يُثبَت هذا في H-2 ببناء نظيف داخل شجرة المرشَّح (`tsc -b` + `npm run build` + `dotnet build`) بعد استبعادها فعليًّا.

---

## 5. الملفّات غير المتتبَّعة — تصنيف

### 5.1 CAND — Backend (38 ملفًّا)

**Domain — كيانات (6):** `Entities/Projects360/{ProjectObjective, ProjectKpi, ProjectKpiReading, ProjectDeliverable, ProjectStrategy, ProjectStrategyAttribute}.cs`

**Domain — نماذج القراءة (5):** `Projects360/{ProjectHealthReason, ProjectHealthReasonCodes, ProjectHealthSnapshot, ProjectKpiAchievement, ProjectObjectiveProgress}.cs`

**Application (5):** `Projects360/{IProject360Authorization, IProject360Services, Project360Codes, Project360Models, ProjectHealthPolicy}.cs`

**Infrastructure — إعداد وهجرة (3):** `Persistence/Configurations/Projects360Configurations.cs` · `Migrations/20260811142239_AddProject360Foundation.cs` · `…Designer.cs`

**Infrastructure — خدمات (9):** `Project360Authorization.cs` · `Project360Guards.cs` · `ProjectHealthService.cs` · `ProjectStrategyService.cs` · `ProjectObjectiveService.cs` · `ProjectKpiService.cs` · `ProjectContractDeliverableService.cs` · `ProjectOverviewService.cs` · `ProjectGovernanceReadService.cs`

**Api — متحكّمات (7):** `ProjectObjectivesController.cs` · `ProjectKpisController.cs` · `ProjectStrategyController.cs` · `ProjectContractDeliverablesController.cs` · `ProjectOverviewController.cs` · `ProjectGovernanceReadController.cs` · `ProjectHealthController.cs`

**اختبارات (4):** `IntegrationTests/{Project360FoundationTests, Project360ApiSurfaceTests, Project360HealthAndAuthorizationTests}.cs` · `UnitTests/ProjectHealthPolicyTests.cs`

### 5.2 CAND — Frontend (14 ملفًّا)

`types/project360.ts` · `lib/useProject360.ts` · `lib/project360Format.ts` · `pages/Project360Page.tsx` · `pages/Project360Page.test.tsx` · `components/project360/`: `shared.tsx`, `ProjectOverviewTab.tsx`, `ProjectBriefTab.tsx`, `ProjectStrategyTab.tsx`, `ProjectObjectivesTab.tsx`, `ProjectKpisTab.tsx`, `ProjectContractDeliverablesTab.tsx`, `ProjectGovernanceTab.tsx`, `ProjectHealthPanel.tsx`, `project360Tabs.test.tsx`

### 5.3 DOC — توثيق CPW-R3 (10 + تقارير هذه الحزمة)

`Docs/Planning/CPW-R3-PROJECT-360-FOUNDATION-*` (R1 التشخيص، R2 التصميم المنقَّح، W0, W1-A, W1, W3, W4, W5, W6) · `CPW-R3-PROJECT-360-MAPPING-ADDENDUM-AND-CLASSIFICATION-ERRATUM-R1.md` · وهذا البيان · وتقارير المراحل B/C/E/F/G/I/K التي تُكتَب في المرحلة K.

### 5.4 PRE — غير متتبَّع ويُستبعَد

`CLAUDE.md` · `Ops/` · `Application/Common/ReportWorkingDaysPolicy.cs` · `Application/Notifications/ReportReminderSchedulerOptions.cs` · `Infrastructure/Services/ReportReminderSchedulerService.cs` · `tests/…/ModerationPerformanceV5Tests.cs` · `tests/…/ReportReminderSchedulerTests.cs` · `tests/…/ReportWorkingDaysPolicyTests.cs` · `tools/LegacyExecutionFixture/` · `frontend/src/components/ActionResultToast.tsx` · كلّ `Docs/` غير CPW-R3 (وهي ~110 ملفًّا من تذاكر سابقة)

---

## 6. الحدود الصلبة المُثبَتة في هذا البيان

| القيد | الحالة | الدليل |
|---|---|---|
| صفر هجرة #34 | ✅ | مجلّد `Migrations` لا يحوي إلّا `20260811142239` جديدة فوق الـ32 المتتبَّعة ⟹ 33 |
| صفر تعديل على الهجرة #33 | ✅ | الملفّ غير متتبَّع أصلًا؛ لم يُعدَّل بعد توليده في W3 |
| صفر مساس بالمستندات/المهامّ/CRM/المالية | ✅ | لا ملفّ من هذه النطاقات في قوائم CAND |
| صفر تغيير في Workstream | ✅ | `ProjectDeliverable` كيان مستقلّ؛ لا ملفّ Workstream في CAND |
| صفر `git add -A` | ✅ | المرحلة I تستعمل تجهيزًا مُسمّى حصرًا وفق هذا البيان |
| صفر إتلاف لعمل المستخدم | ✅ | كلّ PRE يبقى في الشجرة الأصليّة كما هو؛ الاستبعاد يعني «لا يُنقَل»، لا «يُحذف» |

---

## 7. خطّة H-2 (إعادة البناء) — مشتقّة حرفيًّا من الجداول أعلاه

1. `git worktree add <candidate> c157829` — شجرة نظيفة على قاعدة `develop`.
2. نقل ملفّات §4.1 و§5.1 و§5.2 و§5.3 كما هي.
3. تطبيق **hunk `@@ -104,0 +107,16` وحده** من `DependencyInjection.cs` (§4.2) بتحرير موضعيّ لا بنسخ الملفّ.
4. إثبات النظافة: `git status` داخل المرشَّح يجب ألّا يُظهر أيّ ملفّ من قوائم §4.3 أو §5.4.
5. بوّابات البناء: `dotnet build` + `tsc -b` + `npm run build` — أيّ فشل هنا يعني تبعيّة مخفيّة على ملفّ PRE، وتُعالَج بإعادة كتابة الاعتماد داخل نطاق CPW-R3 لا باستيراد ملفّ PRE.
6. ثمّ المرحلة G كاملة داخل هذه الشجرة على قاعدة مؤقّتة معزولة.

---

## 7-أ. تبعيّات مخفيّة على ملفّات PRE — اكتُشفت وأُزيلت في H-2

بناء الواجهة داخل المرشَّح كشف ما لا يمكن أن تكشفه الشجرة المتّسخة: مكوّنات Project 360 كانت تستهلك سطحًا **لا وجود له في `HEAD`**، وإنّما أضافته تذكرة **APPROVAL ACTION UX R1** المتّسخة.

| التبعيّة | مصدرها الحقيقيّ | مواضع الاستهلاك | العلاج داخل نطاق CPW-R3 |
|---|---|---|---|
| `Button` بخاصّيّة `loading` | `components/ui.tsx` — hunk موسوم حرفيًّا `APPROVAL ACTION UX R1` | 6 أزرار في 5 تبويبات | `ActionButton` في `components/project360/shared.tsx`: يغلّف `Button` ويحوّل `busy` إلى `disabled` + `aria-busy` |
| `apiErrorCode` | `lib/api.ts` — نفس التذكرة (بجانب `approvalErrorMessage`) | `shared.tsx` موضع واحد | `httpStatusOf` محلّيّة عبر `axios.isAxiosError`، والرسالة من `apiErrorMessage` **الموجودة في `HEAD`** |

**دلالة الاكتشاف:** بناء الخلفيّة نجح من أوّل محاولة (0 أخطاء) رغم استبعاد 23 ملفّ PRE، أمّا الواجهة ففشلت بـ7 أخطاء TypeScript. لو نُفّذ الانحدار على الشجرة الأصليّة لما ظهر أيّ منها، ولَكانت الحزمة ستُدمَج ثمّ تنكسر لحظة سحب `develop` نظيفًا. هذا وحده يبرّر إعادة الترتيب المُعلَنة في §1.

**مبدأ العلاج:** لا استيراد لملفّ PRE ولا نسخ لـhunk منه. الحزمة تملك سلوكها محلّيًّا، فإن نُشرت APPROVAL ACTION UX R1 لاحقًا فلا تعارض ولا تكرار — `ActionButton` يبقى غلافًا رقيقًا فوق `Button` أيًّا كان سطحه.

**أثر جانبيّ إيجابيّ مُثبَت:** اختبارات الواجهة داخل المرشَّح **271/271 ناجحة في 30 ملفًّا** بلا استثناء واحد، بينما كانت في الشجرة المتّسخة 275/276 بعطب `LeaveRequestsPage/useToast`. غياب العطب في المرشَّح يُثبت تصنيفه **Pre-existing Dirty Worktree** لا Candidate Regression.

---

## 8. الخلاصة

- ملفّات تدخل المرشَّح: **11** متتبَّعة كاملة + **1** متتبَّع جزئيًّا (hunk واحد) + **52** غير متتبَّع (38 backend + 14 frontend) + توثيق CPW-R3.
- ملفّات تُستبعَد: **23** متتبَّعة + **2** hunk من `DependencyInjection.cs` + كلّ ما في §5.4.
- نقاط التماسّ الوحيدة بين الحزمتين: `DependencyInjection.cs` (محلولة بفصل hunk)، و`ui.tsx`/`api.ts`/`format.ts` (محلولة باستهلاك سطح `HEAD` لا سطح الشجرة المتّسخة).

### طريقة التدقيق الآليّة المعتمدة (بديلًا عن الحكم بالاسم)

```
for f in $(git diff --name-only); do
  n=$(git diff -U0 -- "$f" | grep -E "^\+" \
      | grep -icE "project360|projects360|CPW-R3|Project 360|strategy_section|strategy_field|contract_deliverable|HealthStatus|HealthPercent|HealthComputedAtUtc|ProjectOwnerId|TeamLeaderId")
  echo "$n  $f"
done | sort -rn
```

النتيجة فصلٌ حادّ بلا منطقة رماديّة: **12 ملفًّا ≥1 علامة** (وهي بالضبط CAND + MIXED)، و**23 ملفًّا بصفر علامة** (وهي بالضبط PRE). هذا الفصل هو دليل التصنيف، لا حدس التسمية.

**الحالة: بيان مُصحَّح ومُطبَّق فعليًّا في شجرة المرشَّح.**
