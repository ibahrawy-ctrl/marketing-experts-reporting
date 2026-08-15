# CPW-R3 — PROJECT 360 FOUNDATION — W0: تجميد خطّ الأساس (Baseline Freeze)

**الحالة:** `READ ONLY / PASS / STOP`
**التاريخ:** 2026-08-11
**المصدر الوحيد للحقيقة:** `Docs/Planning/CPW-R3-PROJECT-360-FOUNDATION-R2-REVISED-DESIGN-REPORT.md`
**نطاق هذه المرحلة:** تحقّق قرائيّ بحت. **صفر تعديل على أيّ ملفّ متتبَّع في Git.**

---

## 1. هويّة خطّ الأساس

| البُعد | القيمة المُثبَتة |
|---|---|
| الفرع | `develop` |
| HEAD | `c157829f750ce98b7e7aad451a23183b58462cb4` |
| تاريخ HEAD | Fri Aug 7 06:25:20 2026 +0300 |
| رسالة HEAD | `feat(governance): close ADMIN-GOVERNANCE-R1 as the official develop EF baseline` |
| الفرع الرئيس | `main` |

---

## 2. الهجرات (Migrations)

| البُعد | القيمة |
|---|---|
| عدد الهجرات المتتبَّعة عند HEAD | **32** |
| رأس الهجرات | `20260713171040_AdminGovernanceReportKpiCorrection` |
| `dotnet ef migrations has-pending-model-changes` | **No changes have been made to the model since the last migration.** |

ملاحظة: صدرت 4 تحذيرات `query filter` قائمة مسبقًا وغير حاجبة.

---

## 3. البناء والاختبارات (خطّ الأساس)

| البوّابة | النتيجة |
|---|---|
| `dotnet build Reporting.sln` | **Build succeeded — 0 Errors / 12 Warnings** (كلّها مهل قراءة من ذاكرة NuGet — حميدة) |
| اختبارات الوحدة (Backend) | **Passed! Failed: 0, Passed: 91, Skipped: 0, Total: 91** |
| اختبارات التكامل (Backend) | 110 ملفًّا / 1327 `[Fact]`/`[Theory]` — التشغيل الكامل مؤجَّل إلى بوّابة الانحدار W9 |
| `tsc -b` (Frontend) | **TSC_EXIT=0** — صفر مخرجات |
| `vitest run` (Frontend) | **1 failed / 231 passed (232)** — 28 ملفًّا، 1 فشل |

### 3-1. الفشل الوحيد في خطّ الأساس (فشل قائم مسبقًا — ليس انحدارًا من CPW-R3)

- **الملفّ/الاختبار:** `src/pages/pages.test.tsx > module pages render > LeaveRequestsPage shows heading and a leave request row`
- **العَرَض:** `TestingLibraryElementError: Unable to find an element with the text: بانتظار قائد الفريق` (السطر 297).
- **السبب الجذريّ المُثبَت:** استثناء غير مُعالَج `Error: useToast must be used within a ToastProvider` من `src/components/ActionResultToast.tsx:95` عند استدعائه من `src/pages/LeaveRequestsPage.tsx:133` (`CreateLeaveForm`) ⇒ الشجرة لا تُركَّب فيُصبح `<body><div /></body>` فارغًا.
- **المنشأ:** عمل غير مُودَع قائم في شجرة العمل قبل بدء CPW-R3 — `ActionResultToast.tsx` ملفّ **غير متتبَّع** و`LeaveRequestsPage.tsx` ملفّ **معدَّل** ضمن الحالة القذرة الموصوفة في §6. لا علاقة له بأيّ ملفّ من نطاق CPW-R3.
- **الحُكم:** يُسجَّل رسميًّا كـ **`BASELINE-FE-DEFECT-01`** ويُستبعَد من حساب الانحدار في W9. **لا يُصلَح داخل CPW-R3** (خارج النطاق المُصرَّح به).

---

## 4. سطح الـAPI القائم للمشاريع (يجب أن يبقى بلا تغيير)

`reporting-backend/src/Reporting.Api/Controllers/ProjectsController.cs` — `[Route("api/projects")]`، `[Authorize]` على مستوى الصنف، **9 مسارات**:

| # | المسار | الحماية الإضافيّة |
|---|---|---|
| 1 | `GET  /api/projects` | — |
| 2 | `GET  /api/projects/{id:guid}` | — |
| 3 | `GET  /api/projects/{id:guid}/reports` | — |
| 4 | `GET  /api/projects/{id:guid}/summary` | — |
| 5 | `POST /api/projects` | `Policies.ManagementOnly` |
| 6 | `PUT  /api/projects/{id:guid}` | `Policies.ManagementOnly` |
| 7 | `POST /api/projects/{id:guid}/archive` | `Policies.ManagementOnly` |
| 8 | `POST /api/projects/{id:guid}/reactivate` | `Policies.ManagementOnly` |
| 9 | `DELETE /api/projects/{id:guid}` | `Policies.ManagementOnly` |

**التزام (معيار القبول A-06):** صفر تعديل على أيّ من التسعة. الإضافة فقط.

---

## 5. الواجهة القائمة (خطّ الأساس)

| البُعد | القيمة |
|---|---|
| `reporting-frontend/src/pages/ProjectDetailPage.tsx` | **1079 سطرًا** |
| نظام تبويبات فيه | **صفر** — كلّ مطابقات `Tab/tab` هي عناصر HTML `<table>`/`</table>` عند الأسطر 219 و242 و434 و461 |
| مسارات المشاريع في `App.tsx` | 3: `/app/projects` (152)، `/app/projects/:projectId` (153)، `/app/account-portfolio/projects/:id` (156) |
| سكربتات `package.json` | `dev`, `build` (`tsc -b && vite build`), `lint`, `preview`, `test` (`vitest run`), `e2e` |

هذا يؤكّد عمليًّا صحّة §4 (صفّ 10) في تقرير R2 والقرار **D-05** (اللوحة أوّلًا لا التبويبات).

---

## 6. الحالة القذرة القائمة مسبقًا (خارج CPW-R3 تمامًا)

شجرة العمل ليست نظيفة **قبل** بدء CPW-R3. هذه القاعدة المرجعيّة للانحدار في W9:

| البُعد | القيمة |
|---|---|
| `git status --porcelain` | **132 سطرًا** |
| ملفّات كود معدَّلة (`M`) | **24** |
| مسارات كود غير متتبَّعة (`??`) | **8** |
| `git diff --stat` | `24 files changed, 1333 insertions(+), 268 deletions(-)` |

الملفّات المعدَّلة (Backend): `ProjectFirstExecutionSchema.cs`، `EmailNotificationOptions.cs`، `ProjectFirstExecutionModels.cs`، `DependencyInjection.cs`، `TemplateSeeder.cs`، `EmailNotificationService.cs`، `ProjectFirstExecutionAggregationService.cs`، `ReportCalendarService.cs`، `ReportDueService.cs`، `ReportReminderService.cs`، `ReportingService.cs`، `EmployeeProfileScopeTests.cs`، `ReportCalendarTests.cs`، `ReportRemindersTests.cs`.

الملفّات المعدَّلة (Frontend): `ui.tsx`، `lib/api.ts`، `lib/format.ts`، `main.tsx`، `HrRequestsPage.tsx`، `KpiPage.tsx`، `LeaveRequestsPage.tsx`، `ProjectRepeatableGrid.test.tsx`، `SubmissionsPage.tsx`، `types/api.ts`.

غير المتتبَّع: `ReportWorkingDaysPolicy.cs`، `ReportReminderSchedulerOptions.cs`، `ReportReminderSchedulerService.cs`، `ModerationPerformanceV5Tests.cs`، `ReportReminderSchedulerTests.cs`، `ReportWorkingDaysPolicyTests.cs`، `tools/LegacyExecutionFixture/`، `components/ActionResultToast.tsx`.

**حكم مُلزِم:** لا يُدمَج أيّ من هذا في نطاق CPW-R3، ولا يُصلَح، ولا يُعَدّ انحدارًا في W9.

---

## 7. إثبات غياب أيّ أثر لـCPW-R3

| البحث | النتيجة |
|---|---|
| Backend: `ProjectObjective\|ProjectKpi\|ProjectDeliverable\|ProjectStrategy\|ProjectHealth` | **0 ملفّ** |
| Frontend: `projectObjective\|projectKpi\|projectStrategy\|projectDeliverable\|projectHealth` | **0 ملفّ** |

⇒ CPW-R3 يبدأ من الصفر المطلق. لا كود جزئيّ ولا دَين تقنيّ سابق في نطاقه.

---

## 8. كتالوج التصنيفات (ExecutionTaxonomy) — خطّ الأساس

`reporting-backend/src/Reporting.Infrastructure/Services/ExecutionTaxonomyService.cs` — `KnownDomains` = **19 مجالًا**:

```
content_type, content_goal, work_status,
design_type, design_status, design_tool,
video_type, edit_type, video_duration, video_status,
activity_type, interaction_result, response_time,
workstream_type, deliverable, usage_context,
workflow_step, delay_reason, platform_channel
```

CPW-R3 يضيف **مجالين اثنين فقط** (`strategy_field` بـ14 رمزًا، `contract_deliverable` بـ18 رمزًا) ⇒ الهدف **21**. التغيير إضافيّ بحت على `HashSet` واحد.

---

## 9. الملفّات المحرّم مساسها (D-03 / R13 / A-06)

يجب أن يُظهِر `git diff --stat` عند التسليم **صفر تغيير** في الـ10 ملفّات الخلفيّة والـ4 الأماميّة الحاملة نمط `Workstream*` / `WorkstreamDeliverable*`. يُتحقَّق منه في W9.

---

## 10. بوّابة قبول W0

| الشرط | النتيجة |
|---|---|
| الفرع وHEAD مُثبَتان | ✅ |
| عدد الهجرات ورأسها مُثبَتان | ✅ |
| البناء يمرّ بصفر أخطاء | ✅ |
| اختبارات الوحدة الخلفيّة خضراء 91/91 | ✅ |
| `tsc -b` = 0 | ✅ |
| حالة اختبارات الواجهة موثّقة والفشل الوحيد مُشخَّص ومُصنَّف كقائم مسبقًا | ✅ |
| سطح الـAPI القائم موثَّق | ✅ |
| حالة الواجهة القائمة موثّقة (صفر تبويبات) | ✅ |
| صفر أثر لـCPW-R3 | ✅ |
| **صفر تعديل على أيّ ملفّ متتبَّع** | ✅ |

### الحكم: **`READ ONLY / PASS / STOP`**

**التوقّف مُلزَم هنا حتى إذن الانتقال إلى W1.** لا Push / Merge / PR / Deploy / Commit.

---

## ملحق: ملاحظة بيئيّة

كان `reporting-frontend/node_modules` **فارغًا تمامًا** (صفر عنصر) عند بدء W0 ⇒ `tsc`/`vitest` غير قابلين للتشغيل. عولج بـ`npm ci` حصرًا (لا `npm install`) حفاظًا على `package-lock.json`. تحقّق بعديّ: `git status --porcelain package-lock.json package.json` = **فارغ** ⇒ صفر تغيير على ملفّ متتبَّع.
