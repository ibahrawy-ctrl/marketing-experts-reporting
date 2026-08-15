# CPW-R3 — PROJECT 360 FOUNDATION + PROJECT OBJECTIVES + PROJECT KPIs + PROJECT HEALTH
## تقرير التشخيص والتصميم (R1) — قراءة-فقط، بلا سطر كود واحد

> ## ⚠️ هذا التقرير **مُتجاوَز (SUPERSEDED)**
> **البديل المعتمَد للقراءة والتنفيذ**: [`CPW-R3-PROJECT-360-FOUNDATION-R2-REVISED-DESIGN-REPORT.md`](./CPW-R3-PROJECT-360-FOUNDATION-R2-REVISED-DESIGN-REPORT.md)
>
> صدر R2 بعد رسالة المالك **«CPW-R3 — DESIGN REVIEW RESPONSE V1»** التي أقرّت R1 مبدئيًّا وأضافت ثمانية قرارات مُلزِمة `D-01 … D-08`
> (حسم §9-ب بالخيار «أ» وتأجيل تبويب المستندات، وإلزام تبعيّة كلّ مؤشّر لهدف، وطبقة **مخرَجات المشروع**، و**استراتيجيّة المشروع** على مستوى المشروع لا العميل،
> و«لوحة أوّلًا» بدل التبويبات المجرّدة، وخارطة تطوّر مصدر بيانات المؤشّر، ومسؤوليّة قائد الفريق، والتسلسل النهائيّ المستقبليّ).
>
> **يُحتفَظ بهذا الملفّ كمرجع تاريخيّ للتشخيص القرائيّ فقط. أيّ تعارض بينه وبين R2 ⟶ R2 هو الحاكم.**
> **لا يُبنى أيّ كود على أساس R1.**

| البند | القيمة |
|---|---|
| التذكرة | `CPW-R3` |
| المرحلة | `READ → DESIGN` (اكتملت). `IMPLEMENT → TEST` **موقوفة بانتظار الاعتماد** |
| الفرع | `develop` |
| الـHEAD وقت التشخيص | `c157829f750ce98b7e7aad451a23183b58462cb4` |
| عدد الهجرات على `develop` | **32** (صُحِّح في R2 — انظر اللافتة أعلاه)، الرأس `20260713171040_AdminGovernanceReportKpiCorrection` |
| ما نُفِّذ في هذه الجلسة | قراءة الكود + تحليل + تصميم فقط |
| ما لم يُنفَّذ | **صفر كود، صفر هجرة، صفر Commit، صفر نشر، صفر مساس بأيّ بيئة** |
| القرار المطلوب | اعتماد هذا التقرير + حسم **السؤال الحاجب (§9-ب)** قبل بدء التنفيذ |

> **التزام مُلزِم**: لا يُكتب أيّ سطر كود قبل اعتماد هذا التقرير صراحةً.

---

## 1. التشخيص القرائيّ (Read-only Diagnosis)

### 1-1. كيان المشروع الحاليّ — الأساس الذي سنبني فوقه

`reporting-backend/src/Reporting.Domain/Entities/Clients/Project.cs` — **قُرئ حرفيًّا**:

```csharp
public class Project : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }
    public string Name { get; set; } = string.Empty;
    public ServiceType ServiceType { get; set; } = ServiceType.Other;
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid? OwnerTeamId { get; set; }        // الفريق المسؤول
    public Guid? AccountManagerId { get; set; }   // مدير الحساب
    public string? Notes { get; set; }
}
```

**الحكم**: الكيان يغطّي من «النظرة التنفيذيّة» المطلوبة أربعة عناصر فقط (Status، Start، End، Account Manager). ولا يغطّي إطلاقًا: Summary، Health، Progress، Project Owner، Team Leader، ولا أيًّا من حقول الـBrief الخمسة.

### 1-2. طبقتا التخطيط القائمتان فعلًا (P1/P2) — يجب ألّا تنكسرا

| الطبقة | الكيان | الحالة |
|---|---|---|
| P1 | `ProjectWorkstream` | قائم ومنشور. يحمل `ProjectId`, `WorkstreamTypeCode`, `ResponsibleTeamId`, `ResponsibleManagerId`, `Status`, `SortOrder`, `IsActive` |
| P2 | `WorkstreamDeliverable` | قائم ومنشور. يحمل `WorkstreamId`, `DeliverableTypeCode`, `UsageContextCode`, `PlannedQuantity`, `EstimatedHours`, `StartDate`, `DueDate`, `Priority`, `ResponsibleUserId`, `SortOrder`, `IsActive` |

نصّ التعليق داخل `WorkstreamDeliverable.cs` حرفيًّا:
> «هذا سجلّ **تخطيط** فقط — لا يُسجَّل هنا أيّ تنفيذ فعليّ (يأتي التنفيذ في مرحلة لاحقة P4).»

**استنتاج حرج للتذكرة**: بند «ممنوع إنشاء Deliverables» في CPW-R3 **لا يتعارض** مع الواقع — الـDeliverables موجودة سلفًا كطبقة **تخطيط**، والممنوع هو إنشاء طبقة تنفيذ/مهامّ جديدة. سنبني فوقها لا بجانبها.

### 1-3. الحوكمة — المخاطر والقرارات والملاحظات

| الكيان | يحمل `ProjectId`؟ | الأثر على CPW-R3 |
|---|---|---|
| `Risk` | **نعم** (`Guid? ProjectId`, و`Guid? ClientId`) | تبويب المخاطر **بلا أيّ تغيير سكيمة** |
| `ManagementNote` | **نعم منطقيًّا** عبر `EntityType = ManagementNoteEntityType.Project (=8)` + `EntityId` | تبويب الملاحظات **بلا أيّ تغيير سكيمة** |
| `Decision` | **لا** — لا `ProjectId` ولا `ClientId` | تبويب القرارات **يحتاج عمودًا إضافيًّا واحدًا** |

`Decision.cs` قُرئ حرفيًّا ولا يحمل سوى: `Title, Description, MadeById, Status, RelatedSubmissionId, RelatedRiskId, RelatedEscalationId, RelatedKpiEvaluationId, DecidedAtUtc, NextAction`.

### 1-4. عقود الحوكمة (`GovernanceModels.cs`) — فجوة الفلترة

```csharp
public record RiskFilter(RiskStatus? Status = null, RiskSeverity? Severity = null,
    Guid? DepartmentId = null, Guid? OwnerId = null);      // ⚠ لا ProjectId
public record DecisionFilter(DecisionStatus? Status = null); // ⚠ لا ProjectId
```

بينما `RiskDto` و`CreateRiskRequest` **يحملان** `ClientId`/`ProjectId` فعلًا. أي أنّ البيانات تُكتب وتُقرأ لكن **لا تُفلتَر** بالمشروع.

### 1-5. الـControllers القائمة

`Reporting.Api/Controllers/` = **46 كنترولر**. المهمّ منها:
`ProjectsController`, `ProjectWorkstreamsController`, `WorkstreamDeliverablesController`, `ManagementNotesController`, `GovernanceController`, `KpiEvaluationsController`, `KpiTemplatesController`, `ProjectFirstExecutionAggregationController`, `PodExecutionAggregationController`, `AccountPortfolioController`.

**لا يوجد `RisksController` ولا `DecisionsController` مستقلّان** — كلاهما داخل `GovernanceController` (`CreateRisk/UpdateRisk/GetRisk/ListRisks` و`CreateDecision/UpdateDecision/GetDecision/ListDecisions`).

`ManagementNotesController` جاهز تمامًا لإعادة الاستعمال:
```csharp
[HttpGet] List([FromQuery] ManagementNoteEntityType entityType, [FromQuery] Guid entityId, ...)
[HttpPost] [Authorize(Policy = Policies.ManagementOnly)] Create(...)
[HttpPost("{id:guid}/resolve")] [Authorize(Policy = Policies.ManagementOnly)] Resolve(...)
```

### 1-6. نظام KPI القائم — **ليس** ما تطلبه التذكرة

`Reporting.Domain/Entities/Kpi/` = `KpiEvaluation, KpiEvaluationReviewEvent, KpiMetric, KpiResult, KpiTemplate, KpiTemplateAssignment, KpiTemplateVersion`.

هذه المنظومة كلّها **تقييم أداء الموظّف الفرد** (قوالب/نسخ/إسنادات/تقييمات/نتائج بمعادلة `Round(Σ(score*Weight)/100,2)`)، ولا صلة لها بمؤشّرات **نتائج المشروع** المطلوبة في CPW-R3.

**قرار معماريّ مُلزِم**: مؤشّرات المشروع تُبنى في **مساحة أسماء منفصلة تمامًا** (`Entities/Projects/ProjectKpi*`) وبأسماء جداول `project_kpis` — ممنوع أيّ لمس أو توسيع لمنظومة `Kpi` القائمة (تجنّبًا للاصطدام الدلاليّ والانحدار).

### 1-7. النطاق والصلاحيّات — أهمّ اكتشاف في التشخيص

`ClientProjectAccess.ResolveAsync` قُرئ حرفيًّا. يُرجِع `ClientProjectVisibility(SeesAll, ProjectIds, ClientIds)` ويحسب المشاريع المرئيّة من ثلاثة مصادر:

1. `p.AccountManagerId ∈ scope.UserIds`
2. `p.OwnerTeamId ∈ teamSet` حيث `teamSet` = الفِرق التي **يقودها** مستخدم داخل النطاق (`t.TeamLeaderId ∈ uids`) + فريق المستخدم نفسه + عضويّاته الإضافيّة النشطة
3. المشاريع التي سلّم عليها أحد داخل النطاق (`ReportSubmissions.ProjectId`)

**النتيجة الحاسمة**: مطلب «لوحة قائد الفريق — كلّ قائد يرى كلّ مشاريعه» **مُغطّى بنيويًّا بالفعل وبصفر منطق نطاق جديد**. قائد الفريق يرى مشاريع الفِرق التي يقودها تلقائيًّا.

### 1-8. الأدوار والسياسات

```csharp
Roles.Management        = { Admin, Ceo, GeneralManager, Manager, TeamLeader };   // يشمل TeamLeader
Roles.ClientCoreManagers= { Admin, Ceo, GeneralManager, Manager };               // بلا TeamLeader
// ExecutionPlanManagers: Admin/CEO/GM/Manager فقط — عمدًا لا تشمل TeamLeader
Policies.ManagementOnly       // p.RequireRole(Roles.Management)
Policies.ClientCoreManagement // p.RequireRole(Roles.ClientCoreManagers)
```

**النتيجة الحاسمة الثانية**: مطلب «Manual First — قائد الفريق يُحدِّث المؤشّرات يدويًّا» يُغطّى بـ`Policies.ManagementOnly` **بصفر سياسة جديدة وصفر توسيع أدوار**.

### 1-9. الواجهة — اكتشاف مؤثّر على التصميم

`reporting-frontend/src/pages/ProjectDetailPage.tsx` (≈40 KB). **بحث `TabKey`/`TABS` أعطى «No matches found»**.

الصفحة **لا تحوي تبويبات إطلاقًا** — هي مكدّس بطاقات مسطّح:
`breadcrumb + h1 + Badge + أزرار` ⟶ `Card` بيانات المشروع أو `EditProjectForm` ⟶ شريط `StatCard` خماسيّ ⟶ `WorkstreamsCard` ⟶ `LinkedReportsCard`.

المكوّنات الداخليّة وأسطرها: `WorkstreamsCard:161`, `WorkstreamRow:249`, `DeliverablesPanel:345`, `DeliverableRow:468`, `DeliverableForm:549`, `WorkstreamForm:759`, `Info:902`, `SummaryTile:912`, `ArchiveProjectButton:930`, `EditProjectForm:969`.

بينما `ClientDetailPage.tsx` **يحوي** النمط المطلوب:
```tsx
type TabKey = 'overview' | 'contacts' | 'channels' | 'brand' | 'projects' | 'reports';
```

**الأثر**: تحويل صفحة المشروع إلى تبويبات = تغيير بنيويّ في الواجهة، وله أثر مباشر على اختبارات قائمة (انظر §11).

### 1-10. مسارات الواجهة والأدوار

```tsx
EXEC_ROLES = ['Admin','CEO','GeneralManager','Manager','TeamLeader','CeoSupport','Viewer'];
{ path: '/app/projects/:projectId', element: <ProjectDetailPage />, roles: EXEC_ROLES }   // TeamLeader مشمول
```
⇒ قائد الفريق يصل إلى صفحة المشروع **الآن**. لا حاجة لأيّ تعديل توجيه.

---

## 2. مراجعة المعمار (Architecture Review)

### 2-1. الطبقات ونمط التدفّق المُلزَم

```
Domain (كيانات + Enums)
  ↓
Application (واجهة الخدمة I*Service + سجلّات Dto/Request/Filter)
  ↓
Infrastructure (تنفيذ الخدمة + EF Configuration + تسجيل DI)
  ↓
Api (Controller رقيق: FromResult(await _service.X(...)))
```

نمط النتيجة: `Result` / `Result<T>` بـ`Succeeded` / `Error` / `ErrorCode`.
تحويل الخطأ إلى HTTP في `ApiControllerBase.ToProblem()`: `.not_found`⟶404، `.conflict`⟶409، `auth.*`⟶401/403، الباقي⟶400.

### 2-2. أعراف EF المستقرّة في المستودع

- التعدادات: `.HasConversion<string>()` + `HasMaxLength(20)`
- النصوص: `varchar(n)` بحدّ صريح دائمًا
- `DateOnly` ⟶ `date`، `DateTime` ⟶ `timestamp with time zone`
- الجداول: snake_case بصيغة الجمع؛ الأعمدة PascalCase
- الهجرة: `yyyyMMddHHmmss_PascalCaseName`

### 2-3. أنماط مستقرّة يجب إعادة استعمالها لا اختراع بديل لها

| النمط | المرجع القائم | الاستعمال في CPW-R3 |
|---|---|---|
| حارس الحذف النهائيّ | `CanHardDelete` + `DeleteBlockReason` في `ClientDto`/`ProjectDto` | الأهداف والمؤشّرات |
| التعطيل بدل الحذف | `IsActive` في `ProjectWorkstream`/`WorkstreamDeliverable` | الأهداف والمؤشّرات |
| التدقيق على كلّ كتابة | `_audit.LogAsync(uid, "project.updated", nameof(Project), id, ct)` | كلّ نقاط الكتابة الجديدة |
| مضادّ التعداد | إرجاع `*.not_found` (404) بدل 403 عند رفض الرؤية (نمط CPW-R2) | كلّ قراءات المشروع |
| الترتيب اليدويّ | `SortOrder` | الأهداف والمؤشّرات |

### 2-4. التقييم المعماريّ العامّ

المعمار **جاهز لاستقبال CPW-R3 بلا إعادة هيكلة**:
- بُعد المشروع موجود ومربوط بالعميل، وبطبقتَي تخطيط فوقه.
- النطاق مركزيّ وموحَّد (`IClientProjectAccess`) — لا يحتاج توسيعًا.
- السياسات كافية — لا تحتاج إضافة.
- الحوكمة (ملاحظات/مخاطر) مربوطة بالمشروع سلفًا — ينقصها فلترة فقط.

الفجوة الحقيقيّة **بيانيّة لا معماريّة**: لا توجد كيانات للأهداف ولا لمؤشّرات المشروع ولا لصحّته.

---

## 3. تحليل الفجوات (Gap Analysis)

| # | المطلوب في التذكرة | الحالة الفعليّة | الفجوة | حجم العمل |
|---|---|---|---|---|
| 1 | Executive Overview (Summary/Status/Health/Progress/Start/End/Owner/TL/AM/Team) | Status/Start/End/AM فقط | Summary, Health, Progress, ProjectOwnerId, TeamLeaderId, Team Members | أعمدة إضافيّة + نقطة قراءة تجميعيّة |
| 2 | Project Brief (Background/BusinessContext/Scope/OutOfScope/SuccessDefinition) | **غير موجود إطلاقًا** | 5 حقول نصّيّة | 5 أعمدة إضافيّة + `PUT /brief` |
| 3 | Project Objectives (وحدة مستقلّة) | **غير موجودة** | جدول + CRUD كامل + واجهة | جدول جديد |
| 4 | Project KPIs (وحدة جديدة كلّيًّا) | **غير موجودة** (منظومة KPI القائمة = تقييم موظّفين) | جدول + CRUD + قراءات + واجهة | جدولان جديدان |
| 5 | KPI Categories (عشر فئات) | غير موجودة | `enum ProjectKpiCategory` | تعداد جديد |
| 6 | KPI Progress (احتساب Achievement/Health/Variance/Trend) | غير موجود | محرّك احتساب نقيّ (Pure) | خدمة/دالّة نقيّة + اختبارات وحدة |
| 7 | Project Health (Progress + KPI Achievement + Schedule ⟶ أخضر/أصفر/أحمر + %) | غير موجود | محرّك بسيط + تخزين مُخبَّأ | دالّة نقيّة + 3 أعمدة |
| 8 | Project Documents في تبويب مخصّص | **حاجب — انظر §9-ب** | `client_documents` غير موجود على `develop` | **قرار مطلوب** |
| 9 | Project Notes (تبويب) | البنية جاهزة 100% | ربط واجهة فقط | **صفر سكيمة** |
| 10 | Project Decisions (تبويب) | `Decision` بلا `ProjectId` | عمود واحد + فلتر | عمود إضافيّ واحد |
| 11 | Project Risks (تبويب) | `Risk.ProjectId` موجود | فلتر `ProjectId` فقط | **صفر سكيمة** |
| 12 | Team Leader Integration (متوسّط الصحّة/العدد/المتأخّر/الحرِج) | النطاق جاهز 100% | نقطة تجميع قرائيّة واحدة | **صفر نطاق جديد** |
| 13 | جاهزيّة السلسلة المستقبليّة `Objectives ↓ KPIs ↓ Milestones ↓ Deliverables ↓ Tasks` | `Workstream→Deliverable` قائم منفصلًا | جسر اختياريّ بين الهرمَين | عمود `WorkstreamId` قابل للإهمال |

**خلاصة الفجوة**: 3 جداول جديدة + توسعة أعمدة على `projects` + عمود واحد على `decisions`. لا أكثر.

---

## 4. نموذج البيانات (Data Model)

### 4-1. التعدادات الجديدة (`Reporting.Domain/Enums/Enums.cs` — إضافة فقط، بلا تعديل أيّ تعداد قائم)

```csharp
public enum ProjectObjectiveStatus { NotStarted = 0, InProgress = 1, AtRisk = 2, Completed = 3, Cancelled = 4 }

public enum ProjectKpiCategory {
    Marketing = 0, Seo = 1, PaidAds = 2, SocialMedia = 3, Sales = 4,
    Brand = 5, Content = 6, CustomerService = 7, Operations = 8, Custom = 9 }

public enum ProjectKpiUnit { Percentage = 0, Number = 1, Currency = 2, Duration = 3, Score = 4, Custom = 5 }

public enum ProjectKpiFrequency { Weekly = 0, Monthly = 1, Quarterly = 2 }

public enum ProjectKpiDirection { HigherIsBetter = 0, LowerIsBetter = 1 }

public enum ProjectKpiTrend { Unknown = 0, Up = 1, Flat = 2, Down = 3 }

public enum ProjectHealthStatus { Green = 0, Yellow = 1, Red = 2 }
```

**تبرير `ProjectKpiDirection` (وهو خارج قائمة التذكرة الحرفيّة — يُعلَن صراحةً لا يُهرَّب)**: بلا اتجاه، لا يمكن احتساب Achievement % صحيحًا. مؤشّر مثل «معدّل الارتداد» أو «تكلفة الاستحواذ» يتحسّن بالانخفاض؛ ومعادلة `Current/Target` وحدها تعطيه نتيجة معكوسة تمامًا. هذا **متطلَّب حسابيّ ضمنيّ في البند 6** لا توسّعًا في النطاق. **إن رُفض، يجب حينها قصر الاحتساب على `HigherIsBetter` وإعلان ذلك قيدًا وظيفيًّا.**

**إعادة استعمال بلا تعداد جديد**: أولويّة الهدف تستعمل `DeliverablePriority { Low, Medium, High, Urgent }` القائم — لا يُنشأ `ObjectivePriority` مكرّر.

### 4-2. توسعة `Project` (إضافيّة بحتة — كلّ الحقول Nullable أو بقيمة افتراضيّة)

```csharp
// ── النظرة التنفيذيّة ─────────────────────────────────────────
public string? Summary { get; set; }                 // ملخّص المشروع
public Guid? ProjectOwnerId { get; set; }            // مالك المشروع (مستخدم)
public Guid? TeamLeaderId { get; set; }              // قائد الفريق المسؤول (مستخدم)
public decimal ProgressPercent { get; set; }         // 0–100، افتراضي 0 — يدويّ

// ── ملفّ التعريف (Brief) ──────────────────────────────────────
public string? Background { get; set; }
public string? BusinessContext { get; set; }
public string? ScopeText { get; set; }               // Scope اسم محجوز دلاليًّا ⟶ ScopeText
public string? OutOfScope { get; set; }
public string? SuccessDefinition { get; set; }

// ── صحّة المشروع (قيمة مُخبَّأة مشتقّة — ليست مصدر حقيقة) ─────
public ProjectHealthStatus HealthStatus { get; set; } = ProjectHealthStatus.Green;
public decimal HealthPercent { get; set; }           // 0–100، افتراضي 0
public DateTime? HealthComputedAtUtc { get; set; }
```

**ملاحظة تسمية**: `ScopeText` وليس `Scope` — لأنّ كلمة Scope في هذا المستودع محمّلة دلاليًّا بمعنى «نطاق الرؤية» (`ScopeContext`, `IScopeResolver`, `ScopeType`)، واستعمالها لمعنى «نطاق العمل» يُنتج لبسًا خطيرًا في المراجعة المستقبليّة.

**تبرير تخزين الصحّة (Denormalised Cache)**: لوحة قائد الفريق تحتاج «متوسّط صحّة المشاريع» عبر N مشروعًا. احتسابها طيرانًا يعني N×(قراءة كلّ المؤشّرات) لكلّ فتح للوحة. التخزين المُخبَّأ يجعلها استعلامًا تجميعيًّا واحدًا. **مصدر الحقيقة يبقى المؤشّرات والتقدّم والجدول** — والقيمة المخزَّنة تُعاد حوسبتها عند كلّ كتابة تمسّ مدخلاتها، مع `HealthComputedAtUtc` لكشف البيات.

### 4-3. `ProjectObjective` (جدول جديد)

```csharp
public class ProjectObjective : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>جسر اختياريّ إلى طبقة التخطيط القائمة (P1). قابل للإهمال تمامًا.</summary>
    public Guid? WorkstreamId { get; set; }
    public ProjectWorkstream? Workstream { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DeliverablePriority Priority { get; set; } = DeliverablePriority.Medium;

    /// <summary>الوزن النسبيّ داخل المشروع (0–100). لا يُفرَض مجموعه = 100 في هذه المرحلة.</summary>
    public decimal Weight { get; set; }

    public ProjectObjectiveStatus Status { get; set; } = ProjectObjectiveStatus.NotStarted;
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
```

**قرار الوزن**: لا يُفرَض `Σ Weight = 100`. السبب: الأهداف تُضاف تدريجيًّا وفرض المجموع يمنع حفظ أوّل هدف. الوزن يُستعمل مُطبَّعًا (`w_i / Σw`) عند الاحتساب. يبقى **حارس نطاق** فقط: `0 ≤ Weight ≤ 100`.

### 4-4. `ProjectKpi` (جدول جديد)

```csharp
public class ProjectKpi : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>ربط اختياريّ بهدف — يحقّق سلسلة Objectives ↓ KPIs بلا إلزام.</summary>
    public Guid? ObjectiveId { get; set; }
    public ProjectObjective? Objective { get; set; }

    public string Name { get; set; } = string.Empty;
    public ProjectKpiCategory Category { get; set; } = ProjectKpiCategory.Marketing;
    /// <summary>اسم الفئة الحرّ حين Category = Custom.</summary>
    public string? CustomCategoryName { get; set; }

    public ProjectKpiUnit Unit { get; set; } = ProjectKpiUnit.Number;
    /// <summary>رمز الوحدة الحرّ حين Unit = Custom أو Currency (مثل SAR).</summary>
    public string? CustomUnitLabel { get; set; }

    public ProjectKpiDirection Direction { get; set; } = ProjectKpiDirection.HigherIsBetter;

    public decimal? BaselineValue { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? TargetValue { get; set; }

    /// <summary>مشتقّة ومخزَّنة — تُعاد حوسبتها عند كلّ كتابة قيمة.</summary>
    public decimal? AchievementPercent { get; set; }
    public decimal? VariancePercent { get; set; }
    public ProjectKpiTrend Trend { get; set; } = ProjectKpiTrend.Unknown;

    public ProjectKpiFrequency Frequency { get; set; } = ProjectKpiFrequency.Monthly;
    public Guid? OwnerUserId { get; set; }
    public DateTime? LastUpdatedAtUtc { get; set; }

    /// <summary>الوزن النسبيّ داخل احتساب صحّة المشروع (0–100).</summary>
    public decimal Weight { get; set; }

    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
```

### 4-5. `ProjectKpiReading` (جدول جديد — سجلّ القراءات)

```csharp
public class ProjectKpiReading : BaseEntity
{
    public Guid ProjectKpiId { get; set; }
    public ProjectKpi? ProjectKpi { get; set; }

    /// <summary>تاريخ القراءة (نهاية الفترة المقيسة).</summary>
    public DateOnly ReadingDate { get; set; }
    public decimal Value { get; set; }

    /// <summary>لقطة الهدف وقت القراءة — كي لا يفسد التاريخ عند تغيير الهدف لاحقًا.</summary>
    public decimal? TargetSnapshot { get; set; }
    public decimal? AchievementSnapshot { get; set; }

    public Guid RecordedByUserId { get; set; }
    public string? Notes { get; set; }
}
```

**تبرير الوجود** (وهو ليس توسّعًا في النطاق): البند 6 يطلب **Trend** صراحةً. الاتجاه مستحيل رياضيًّا من قيمة حاليّة واحدة — يلزم قيمتان على الأقلّ عبر الزمن. القراءات هي الحدّ الأدنى المطلق لتحقيق المطلوب. ولقطة الهدف تمنع تشوّه التاريخ عند تعديل الهدف.

### 4-6. توسعة `Decision` (عمود واحد)

```csharp
public Guid? ProjectId { get; set; }
```

### 4-7. مخطّط العلاقات النهائيّ

```
Client
  └── Project ──────────────────────────────────────┐
        ├── ProjectWorkstream (P1، قائم)            │
        │      └── WorkstreamDeliverable (P2، قائم) │
        ├── ProjectObjective (جديد) ─ WorkstreamId? ─┘   ← الجسر
        │      └── ProjectKpi (جديد، ObjectiveId?)
        │             └── ProjectKpiReading (جديد)
        ├── Risk (قائم، ProjectId)
        ├── Decision (قائم + ProjectId جديد)
        └── ManagementNote (قائم، EntityType=Project)
```

**كيف تُستقبَل المرحلة المستقبليّة بلا إعادة هيكلة**: `Milestone` مستقبليّ يحمل `ProjectId` + `ObjectiveId?`؛ و`WorkstreamDeliverable` القائم يُضاف إليه لاحقًا `MilestoneId?` إضافيًّا؛ و`Task` مستقبليّ يحمل `DeliverableId`. كلّ حلقة تُضاف بعمود Nullable واحد — **صفر إعادة هيكلة**.

---

## 5. تصميم الـAPI (API Design)

### 5-1. الأهداف

| الفعل | المسار | السياسة |
|---|---|---|
| GET | `/api/projects/{projectId}/objectives` | `[Authorize]` + نطاق خادميّ |
| GET | `/api/projects/{projectId}/objectives/{id}` | `[Authorize]` + نطاق خادميّ |
| POST | `/api/projects/{projectId}/objectives` | `ManagementOnly` |
| PUT | `/api/projects/{projectId}/objectives/{id}` | `ManagementOnly` |
| POST | `/api/projects/{projectId}/objectives/{id}/deactivate` | `ManagementOnly` |
| POST | `/api/projects/{projectId}/objectives/{id}/reactivate` | `ManagementOnly` |
| DELETE | `/api/projects/{projectId}/objectives/{id}` | `ManagementOnly` + حارس `CanHardDelete` |

### 5-2. المؤشّرات

| الفعل | المسار | السياسة |
|---|---|---|
| GET | `/api/projects/{projectId}/kpis` (فلاتر: `category`, `objectiveId`, `frequency`, `includeInactive`) | `[Authorize]` + نطاق |
| GET | `/api/projects/{projectId}/kpis/{id}` | `[Authorize]` + نطاق |
| POST | `/api/projects/{projectId}/kpis` | `ManagementOnly` |
| PUT | `/api/projects/{projectId}/kpis/{id}` | `ManagementOnly` |
| POST | `/api/projects/{projectId}/kpis/{id}/deactivate` / `/reactivate` | `ManagementOnly` |
| DELETE | `/api/projects/{projectId}/kpis/{id}` | `ManagementOnly` + حارس |
| GET | `/api/projects/{projectId}/kpis/{id}/readings` | `[Authorize]` + نطاق |
| POST | `/api/projects/{projectId}/kpis/{id}/readings` | `ManagementOnly` |
| DELETE | `/api/projects/{projectId}/kpis/{id}/readings/{readingId}` | `ManagementOnly` |

`POST /readings` هو **نقطة التحديث اليدويّ الأساسيّة**: يكتب القراءة ⟶ يُحدِّث `CurrentValue` ⟶ يُعيد حوسبة `Achievement/Variance/Trend` ⟶ يُعيد حوسبة صحّة المشروع — في **معاملة واحدة و`SaveChanges` واحد**.

### 5-3. النظرة التنفيذيّة والصحّة والـBrief

| الفعل | المسار | الوصف |
|---|---|---|
| GET | `/api/projects/{projectId}/overview` | كائن واحد: بيانات المشروع + الصحّة + عدّادات الأهداف/المؤشّرات/المخاطر/القرارات/الملاحظات + أعضاء الفريق |
| PUT | `/api/projects/{projectId}/brief` | الحقول الخمسة + `Summary` |
| PUT | `/api/projects/{projectId}/execution` | `ProgressPercent`, `ProjectOwnerId`, `TeamLeaderId` |
| GET | `/api/projects/{projectId}/health` | تفصيل الصحّة ومكوّناتها الثلاثة (شفافيّة الاحتساب) |
| POST | `/api/projects/{projectId}/health/recompute` | إعادة حوسبة صريحة (Idempotent) — `ManagementOnly` |

**قرار**: لا نُضيف حقول الـBrief إلى `UpdateProjectRequest` القائم — بل مسار مستقلّ. السبب: تقليل سطح الانحدار على `ProjectsController`/`ProjectService` القائمَين إلى الصفر تقريبًا.

### 5-4. لوحة قائد الفريق

```
GET /api/projects/portfolio-health
```
قرائيّة بحتة. تُرجِع — **ضمن نطاق `IClientProjectAccess` بلا استثناء**:
`averageHealthPercent`, `projectCount`, `greenCount`, `yellowCount`, `redCount`, `lateProjectCount` (`EndDate < today && Status ∉ {Completed, Closed}`), `criticalProjectCount` (`HealthStatus = Red` أو `Status = AtRisk`), وقائمة مختصرة لكلّ مشروع (`id, name, clientName, healthStatus, healthPercent, progressPercent, kpiCount, objectiveCount, endDate, isLate`).

**بلا شاشة جديدة**: تُستهلَك داخل `ProjectsPage` كشريط علويّ ظاهر لأدوار `Roles.Management`.

### 5-5. توسعة الحوكمة (تعديل أدنى ما يمكن)

```csharp
public record RiskFilter(RiskStatus? Status = null, RiskSeverity? Severity = null,
    Guid? DepartmentId = null, Guid? OwnerId = null,
    Guid? ProjectId = null, Guid? ClientId = null);          // ← إضافة اختياريّة في الذيل

public record DecisionFilter(DecisionStatus? Status = null, Guid? ProjectId = null);  // ← إضافة

public record DecisionDto(..., Guid? ProjectId = null);       // ← إضافة في الذيل
public record CreateDecisionRequest(..., Guid? ProjectId = null);  // ← إضافة في الذيل
```

كلّها **بارامترات اختياريّة في نهاية السجلّ** ⟶ توافق خلفيّ كامل، وصفر كسر لأيّ مستدعٍ قائم.

### 5-6. الملاحظات — صفر API جديد

يُستهلَك `GET /api/management-notes?entityType=Project&entityId={projectId}` كما هو.

### 5-7. أكواد الأخطاء

| الكود | HTTP |
|---|---|
| `project.not_found` | 404 (يُستعمل أيضًا عند رفض الرؤية — مضادّ التعداد) |
| `project_objective.not_found` / `project_kpi.not_found` / `project_kpi_reading.not_found` | 404 |
| `project_objective.name_required` / `project_kpi.name_required` | 400 |
| `project_objective.weight_invalid` / `project_kpi.weight_invalid` | 400 |
| `project_kpi.target_required` (عند طلب احتساب الإنجاز بلا هدف) | 400 |
| `project.progress_invalid` (خارج 0–100) | 400 |
| `project_objective.delete_forbidden.conflict` (له مؤشّرات) | 409 |
| `project_kpi.delete_forbidden.conflict` (له قراءات) | 409 |
| `project_kpi_reading.duplicate_date.conflict` | 409 |
| `auth.forbidden` | 403 |

---

## 6. تصميم الأمن (Security Design)

### 6-1. المبدأ الحاكم

> **لا يُعتمَد على الواجهة إطلاقًا.** كلّ قرار رؤية وكلّ قرار كتابة يُفرَض في الخادم، داخل الخدمة، قبل أيّ استعلام يُرجِع بيانات.

### 6-2. طبقات الفرض الثلاث

**الطبقة 1 — المصادقة والسياسة على الكنترولر**
```csharp
[Authorize]                                        // كلّ القراءات
[Authorize(Policy = Policies.ManagementOnly)]       // كلّ الكتابات
```
`ManagementOnly = { Admin, CEO, GeneralManager, Manager, TeamLeader }` — يطابق تمامًا مطلب «قائد الفريق يُحدِّث المؤشّرات يدويًّا». **صفر سياسة جديدة، صفر دور جديد، صفر توسيع لأيّ مصفوفة أدوار قائمة.**

**الطبقة 2 — بوّابة المشروع الموحَّدة (حارس واحد لا مكرَّر)**
```csharp
private async Task<Result<Project>> LoadVisibleProjectAsync(Guid projectId, CancellationToken ct)
{
    var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
    if (project is null) return Result<Project>.Fail("المشروع غير موجود.", "project.not_found");

    var vis = await _access.ResolveAsync(ct);
    if (!vis.SeesAll && !vis.ProjectIds.Contains(projectId))
        return Result<Project>.Fail("المشروع غير موجود.", "project.not_found");   // مضادّ التعداد

    return Result<Project>.Ok(project);
}
```
كلّ نقطة نهاية في CPW-R3 — قراءةً أو كتابةً — تمرّ من هنا **أوّلًا**. لا استثناء.

**قرار 404 لا 403**: متطابق مع ما اعتُمِد وأُثبِت في CPW-R2 (`client_document.not_found`). الفارق أمنيًّا جوهريّ: 403 يؤكّد وجود المورد ⟶ يسمح بتعداد المشاريع؛ 404 لا يؤكّد شيئًا.

**الطبقة 3 — ملكيّة الابن للأب**
كلّ عمليّة على هدف/مؤشّر/قراءة تتحقّق من `entity.ProjectId == projectId` (والقراءة من `reading.ProjectKpiId == kpiId`) وإلّا `*.not_found`. هذا يمنع IDOR عبر تمرير معرّف ابن يخصّ مشروعًا آخر مرئيًّا أو غير مرئيّ.

### 6-3. التدقيق

كلّ كتابة تُنتج صفّ تدقيق:
`project.brief_updated`, `project.execution_updated`, `project.health_recomputed`,
`project_objective.created|updated|deactivated|reactivated|deleted`,
`project_kpi.created|updated|deactivated|reactivated|deleted`,
`project_kpi_reading.created|deleted`.

### 6-4. تحصينات إضافيّة

- كلّ الاستعلامات القرائيّة `AsNoTracking()`.
- الحقول النصّيّة محدودة الطول على مستوى EF **وعلى مستوى التحقّق في الخدمة** (لا اعتماد على القاعدة وحدها).
- لا حقل جديد يعرض معرّفات داخليّة حسّاسة.
- الفلترة بالنطاق تُطبَّق **داخل استعلام SQL** لا بعد التحميل (`IQueryable` قابلة للترجمة) — كي لا يُحمَّل ما لا يُرى أصلًا.
- `POST /health/recompute` **Idempotent** ولا يقبل قيمًا من العميل — يحتسب من البيانات المخزّنة حصرًا (لا يمكن حقن صحّة مزيّفة).

---

## 7. تصميم الواجهة (UI/UX Design)

### 7-1. القيد المُلزَم

> «تبويبات جديدة داخل تفاصيل المشروع — **ممنوع إنشاء شاشة جديدة**.»

### 7-2. البنية المقترحة داخل `ProjectDetailPage.tsx`

```tsx
type TabKey = 'overview' | 'brief' | 'objectives' | 'kpis' | 'execution'
            | 'notes' | 'decisions' | 'risks' | 'documents' | 'reports';
```

بنفس نمط `ClientDetailPage.tsx` حرفيًّا (نفس أصناف Tailwind، نفس `border-b-2 border-orange-500`).

| التبويب | المحتوى | مصدره |
|---|---|---|
| `overview` | ترويسة الصحّة (Green/Yellow/Red + %) + شريط التقدّم + الحقول التنفيذيّة + `StatCard` الخماسيّ القائم | جديد + قائم |
| `brief` | الحقول الخمسة + الملخّص، مع نموذج تحرير محكوم بالصلاحيّة | جديد |
| `objectives` | جدول الأهداف + نموذج إضافة/تعديل | جديد |
| `kpis` | جدول المؤشّرات مجمَّعًا بالفئة + نموذج + لوحة إدخال قراءة سريعة | جديد |
| `execution` | **`WorkstreamsCard` القائم كما هو، منقولًا بلا أيّ تعديل داخليّ** | قائم |
| `notes` | ملاحظات `entityType=Project` | قائم (API) |
| `decisions` | قرارات المشروع | قائم + فلتر جديد |
| `risks` | مخاطر المشروع | قائم + فلتر جديد |
| `documents` | **مشروط — انظر §9-ب** | معلّق |
| `reports` | **`LinkedReportsCard` القائم كما هو** | قائم |

### 7-3. قرار حاسم للحدّ من الانحدار

**`WorkstreamsCard` و`LinkedReportsCard` يُنقَلان كما هما، بصفر تعديل في محتواهما الداخليّ.** التغيير الوحيد هو الحاوية التي تُعرَض داخلها. هذا يقصر أثر التبويبات على مستوى الصفحة الأمّ فقط.

### 7-4. التبويب الافتراضيّ — نقطة قرار صريحة

الخيار المعتمَد في التصميم: الافتراضيّ = `overview`.

الأثر: اختبارات الواجهة القائمة التي تفترض ظهور تيّارات العمل مباشرةً عند فتح الصفحة ستحتاج نقرة تبويب. هذا **تغيير في سطح الاختبار لا انحدار سلوكيّ** — مُفصَّل في §11.

بديل ذو انحدار أقلّ (متاح إن فُضّل): الافتراضيّ = `execution`. لكنّه يخالف روح «النظرة التنفيذيّة أوّلًا» في التذكرة. **التوصية: `overview` مع تحديث الاختبارات صراحةً.**

### 7-5. لوحة قائد الفريق — بلا شاشة جديدة

شريط تجميعيّ أعلى `ProjectsPage` (الشاشة القائمة) يعرض: متوسّط الصحّة، عدد المشاريع، أخضر/أصفر/أحمر، المتأخّرة، الحرِجة. يُستهلَك من `GET /api/projects/portfolio-health`. يظهر لأدوار `Roles.Management` فقط.

### 7-6. اللغة والاتّجاه

عربيّة كاملة، RTL، خطّ Tajawal، بمعجم متّسق:
الصحّة = «سليم / تحت المراقبة / حرِج»؛ الفئات والوحدات والتكرار بمخطّطات تسمية في `lib/format.ts` على نمط `clientStatusLabel`/`projectStatusLabel` القائم.

---

## 8. تصميم قاعدة البيانات (Database Design)

### 8-1. `projects` — أعمدة مُضافة فقط

| العمود | النوع | Null | افتراضي |
|---|---|---|---|
| `Summary` | `varchar(1000)` | نعم | — |
| `ProjectOwnerId` | `uuid` | نعم | — |
| `TeamLeaderId` | `uuid` | نعم | — |
| `ProgressPercent` | `numeric(5,2)` | لا | `0` |
| `Background` | `varchar(4000)` | نعم | — |
| `BusinessContext` | `varchar(4000)` | نعم | — |
| `ScopeText` | `varchar(4000)` | نعم | — |
| `OutOfScope` | `varchar(4000)` | نعم | — |
| `SuccessDefinition` | `varchar(2000)` | نعم | — |
| `HealthStatus` | `varchar(20)` | لا | `'Green'` |
| `HealthPercent` | `numeric(5,2)` | لا | `0` |
| `HealthComputedAtUtc` | `timestamptz` | نعم | — |

**كلّ الأعمدة غير الفارغة لها قيمة افتراضيّة ⟶ صفر Backfill، والمشاريع القائمة تبقى صالحة فورًا.**

### 8-2. `project_objectives`

`Id uuid PK` · `ProjectId uuid NOT NULL` (FK⟶`projects`, **Cascade**) · `WorkstreamId uuid NULL` (FK⟶`project_workstreams`, **SetNull**) · `Name varchar(300) NOT NULL` · `Description varchar(2000)` · `Priority varchar(20) NOT NULL` · `Weight numeric(5,2) NOT NULL DEFAULT 0` · `Status varchar(20) NOT NULL` · `StartDate date` · `DueDate date` · `OwnerUserId uuid` · `Notes varchar(2000)` · `SortOrder int NOT NULL DEFAULT 0` · `IsActive bool NOT NULL DEFAULT true` · `CreatedAtUtc timestamptz NOT NULL` · `UpdatedAtUtc timestamptz`

فهارس: `(ProjectId, SortOrder)`، `(ProjectId, Status)`، `(WorkstreamId)`.

### 8-3. `project_kpis`

`Id uuid PK` · `ProjectId uuid NOT NULL` (FK⟶`projects`, **Cascade**) · `ObjectiveId uuid NULL` (FK⟶`project_objectives`, **SetNull**) · `Name varchar(300) NOT NULL` · `Category varchar(20) NOT NULL` · `CustomCategoryName varchar(100)` · `Unit varchar(20) NOT NULL` · `CustomUnitLabel varchar(50)` · `Direction varchar(20) NOT NULL` · `BaselineValue numeric(18,4)` · `CurrentValue numeric(18,4)` · `TargetValue numeric(18,4)` · `AchievementPercent numeric(9,2)` · `VariancePercent numeric(9,2)` · `Trend varchar(20) NOT NULL` · `Frequency varchar(20) NOT NULL` · `OwnerUserId uuid` · `LastUpdatedAtUtc timestamptz` · `Weight numeric(5,2) NOT NULL DEFAULT 0` · `Notes varchar(2000)` · `SortOrder int NOT NULL DEFAULT 0` · `IsActive bool NOT NULL DEFAULT true` · `CreatedAtUtc` · `UpdatedAtUtc`

فهارس: `(ProjectId, SortOrder)`، `(ProjectId, Category)`، `(ObjectiveId)`.

**`SetNull` على `ObjectiveId`** مقصود: حذف هدف لا يجوز أن يمحو مؤشّرًا قِيس فعليًّا — يفقد الارتباط فقط. ومع ذلك يبقى حارس `project_objective.delete_forbidden.conflict` طبقةً أولى تمنع الوصول إلى هذه الحالة أصلًا.

### 8-4. `project_kpi_readings`

`Id uuid PK` · `ProjectKpiId uuid NOT NULL` (FK⟶`project_kpis`, **Cascade**) · `ReadingDate date NOT NULL` · `Value numeric(18,4) NOT NULL` · `TargetSnapshot numeric(18,4)` · `AchievementSnapshot numeric(9,2)` · `RecordedByUserId uuid NOT NULL` · `Notes varchar(1000)` · `CreatedAtUtc` · `UpdatedAtUtc`

فهارس: **فريد** `(ProjectKpiId, ReadingDate)` — قراءة واحدة لكلّ مؤشّر في اليوم (يُنتِج `project_kpi_reading.duplicate_date.conflict`)؛ و`(ProjectKpiId, ReadingDate DESC)` لسرعة اشتقاق الاتّجاه.

### 8-5. `decisions`

عمود واحد: `ProjectId uuid NULL` (FK⟶`projects`, **SetNull**) + فهرس `(ProjectId)`.

### 8-6. محرّك الاحتساب (منطق نقيّ — قابل لاختبار الوحدة بلا قاعدة بيانات)

**إنجاز المؤشّر**
```
HigherIsBetter:  achievement = (Current / Target) × 100
LowerIsBetter :  achievement = (Target  / Current) × 100
Clamp(0, 200)                       // سقف 200% لمنع تشويه المتوسّط بمؤشّر شاذّ
Target ≤ 0 أو Current = null ⟶ achievement = null (يُستبعَد من الاحتساب)
```

**الانحراف**
```
variance = achievement − 100
```

**الاتّجاه** (من آخر قراءتين لنفس المؤشّر، بعتبة ±2% لتفادي الضجيج)
```
Δ = latest − previous،  ثمّ يُعدَّل بالاتّجاه:
  HigherIsBetter: Δ > +ε ⟶ Up | Δ < −ε ⟶ Down | غير ذلك ⟶ Flat
  LowerIsBetter : معكوس
أقلّ من قراءتين ⟶ Unknown
```

**صحّة المشروع** — ثلاثة مكوّنات بأوزان صريحة معلَنة:
```
kpiScore      = Σ(achievement_i × w_i) / Σ(w_i)      // الأوزان المُطبَّعة، تجاهُل null
                (بلا مؤشّرات فعّالة ⟶ يُستبعَد المكوّن ويُعاد توزيع وزنه)
progressScore = ProgressPercent
scheduleScore = دالّة انحراف التقدّم عن الزمن المنقضي:
                expected = (اليوم − StartDate) / (EndDate − StartDate) × 100
                gap = ProgressPercent − expected
                gap ≥ 0 ⟶ 100 | −10 ≤ gap < 0 ⟶ 75 | −25 ≤ gap < −10 ⟶ 50 | gap < −25 ⟶ 25
                (بلا StartDate/EndDate ⟶ يُستبعَد المكوّن)

HealthPercent = 0.5×kpiScore + 0.3×progressScore + 0.2×scheduleScore   (مُطبَّعة على المكوّنات المتاحة)
HealthStatus  = HealthPercent ≥ 80 ⟶ Green | ≥ 55 ⟶ Yellow | غير ذلك ⟶ Red
```

الأوزان والعتبات تُعرَّف **ثوابت مسمّاة في مكان واحد** (`ProjectHealthPolicy`) لا أرقامًا سحريّة متناثرة، وتُوثَّق في الواجهة عبر `GET /health` كي يفهم المستخدم سبب اللون.

---

## 9. خطة الهجرة (Migration Plan)

### 9-أ. الهجرة الوحيدة

**الاسم**: `AddProject360Foundation`
**العدد**: **هجرة واحدة لا غير.**

**Up**
1. `AddColumn` ×12 على `projects`
2. `CreateTable` `project_objectives` + 3 فهارس
3. `CreateTable` `project_kpis` + 3 فهارس
4. `CreateTable` `project_kpi_readings` + فهرسان (أحدهما فريد)
5. `AddColumn` `ProjectId` على `decisions` + فهرس

**Down**
`DropTable` ×3 + `DropColumn` ×13.

**التزام إضافيّ بحت مُثبَت**:
- صفر `RenameColumn` / `RenameTable`
- صفر `DropColumn` / `DropTable` على أيّ كيان قائم
- صفر `AlterColumn` على أيّ عمود قائم
- صفر Backfill وصفر سكربت بيانات
- صفر كسر عقد: كلّ إضافات الـDTO/Request/Filter بارامترات اختياريّة في ذيل السجلّ

**بوّابة تحقّق إلزاميّة قبل الـCommit**: `dotnet ef migrations has-pending-model-changes` ⟶ `No changes`.

### 9-ب. ⛔ السؤال الحاجب — يجب حسمه قبل أيّ كود

**متطلَّب رقم 8 «Project Documents» غير قابل للتنفيذ على `develop` كما هي.**

الدليل:
- `develop` عند `c157829` بـ**32 هجرة** (صُحِّح لاحقًا في R2؛ الرقم 31 هنا كان خطأً ناتجًا عن خلطه بعدد الهجرات المُطبَّقة على بيئة TEST)، الرأس `20260713171040_AdminGovernanceReportKpiCorrection`.
- منظومة المستندات (`client_documents`, `client_document_versions`, `client_document_allowed_roles`, `client_document_allowed_users`) + هجرتها `20260809165617_ClientDocumentVisibility` + `IDocumentAccessEvaluator` **تعيش حصرًا على فرع غير مدموج** رأسه `3344f78` (عمل CPW-R1B2/CPW-R2).
- أي أنّ **جدول المستندات غير موجود أصلًا على `develop`** — ولا شيء لعرضه في تبويب.

الخيارات الثلاثة، ومطلوب اختيار واحد صراحةً:

| # | الخيار | الأثر |
|---|---|---|
| **أ** | بناء CPW-R3 فوق `develop` مع **تأجيل التبويب 8** وتسليم 10 تبويبات من 11 | الأنظف والأقلّ خطرًا. تبويب المستندات يُضاف لاحقًا بواجهة فقط بعد الدمج |
| **ب** | بناء CPW-R3 فوق فرع المستندات `3344f78` | يُسلَّم البند 8 كاملًا، لكن يربط CPW-R3 بنَسَب غير مدموج ويؤخّر نشرها بنشره |
| **ج** | دمج فرع المستندات في `develop` أوّلًا ثمّ بناء CPW-R3 | يحلّ الجذر، لكنّه **عمل خارج نطاق CPW-R3 ويحتاج تصريحًا مستقلًّا** (المذكّرة تمنع Merge بلا تصريح جديد) |

**التوصية الهندسيّة: (أ)** — تسليم CPW-R3 كاملًا ونظيفًا على `develop`، مع تعليم البند 8 «مؤجَّل بقرار مالك المنتج، مسبَّبه نَسَب غير مدموج»، وتنفيذه لاحقًا بتغيير واجهة فقط (لأنّ الخادم في فرع المستندات يدعم الفلترة سلفًا).

### 9-ج. النشر

**لا نشر ضمن هذه التذكرة.** التسليم = كود + هجرة + اختبارات خضراء على البيئة المحلّيّة. TEST/RC/Production تحتاج تصاريح مستقلّة، ولا تُلمَس هنا مطلقًا.

---

## 10. تحليل المخاطر (Risk Analysis)

| # | الخطر | الاحتمال | الأثر | التخفيف |
|---|---|---|---|---|
| R1 | لبس دلاليّ بين مؤشّرات المشروع ومنظومة KPI الموظّف | **مرتفع** | مرتفع | مساحة أسماء وأسماء جداول مختلفة تمامًا (`ProjectKpi*`/`project_kpis`)؛ **صفر لمس** لأيّ ملفّ داخل `Entities/Kpi` أو `Application/Kpi` — يُتحقَّق منه بـ`git diff --stat` |
| R2 | زحف النطاق نحو إدارة المهامّ | متوسّط | **حرِج** | صفر كيان `Task/Milestone`؛ الأهداف بلا نسبة إنجاز محسوبة من مهامّ؛ `ProgressPercent` يدويّ صراحةً |
| R3 | كسر اختبارات الواجهة بسبب التبويبات | **مرتفع** | متوسّط | تحديث صريح ومعلن لملفّي اختبار (§11) + نقل المكوّنات بلا تعديل داخليّ |
| R4 | تخبئة الصحّة تصبح بائتة | متوسّط | متوسّط | إعادة حوسبة عند كلّ كتابة تمسّ المدخلات + `HealthComputedAtUtc` + مسار `recompute` صريح |
| R5 | قسمة على صفر / هدف صفريّ في الاحتساب | متوسّط | مرتفع | حارس صريح: `Target ≤ 0` أو `Current = null` ⟶ `achievement = null` ويُستبعَد؛ اختبارات وحدة للحالات الحديّة |
| R6 | تسرّب رؤية مشروع خارج النطاق | منخفض | **حرِج** | حارس `LoadVisibleProjectAsync` موحَّد على كلّ نقطة + اختبارات نطاق سلبيّة صريحة |
| R7 | IDOR عبر معرّف ابن من مشروع آخر | متوسّط | **حرِج** | التحقّق من `ProjectId` الأب في كلّ عمليّة ابن + اختبار مخصّص |
| R8 | تضخّم `project_kpi_readings` | منخفض | منخفض | إدخال يدويّ منخفض التواتر (أسبوعيّ/شهريّ/ربعيّ) + فهرس فريد يمنع الإغراق |
| R9 | مطلب المستندات يوقف التسليم | **مؤكَّد قائم** | مرتفع | §9-ب — قرار صريح مطلوب قبل البدء |
| R10 | تصادم مع عمل CPW-R2 غير المدموج عند الدمج لاحقًا | متوسّط | متوسّط | CPW-R3 لا يلمس أيّ ملفّ من ملفّات CPW-R2 الـ31؛ التصادم الوحيد المحتمل = `Enums.cs` و`AppDbContext.cs` (إضافات في نهاية الملفّ ⟶ حلّ يدويّ تافه) |
| R11 | فرض `Σ Weight = 100` يمنع الحفظ التدريجيّ | متوسّط | متوسّط | قرار صريح: لا فرض للمجموع؛ التطبيع عند الاحتساب |
| R12 | `ProjectKpiDirection` يُعدّ توسّعًا في النطاق | متوسّط | متوسّط | مُعلَن صراحةً في §4-1 مع تبريره الحسابيّ ومسار الرفض البديل |

---

## 11. تحليل الانحدار (Regression Analysis)

### 11-1. الملفّات التي **لن تُمَسّ** إطلاقًا (تُتحقَّق بـ`git diff`)

`ScopeResolver.cs` · `ClientProjectAccess.cs` · `SubmissionService.cs` · `KpiEvaluationService.cs` · `KpiTemplateService.cs` · `ReportingService.cs` · `LeaveRequestService.cs` · `EmailNotificationService.cs` · `ReportReminderService.cs` · كامل `Entities/Kpi/` و`Application/Kpi/` · `Roles.cs` (لا دور ولا مصفوفة جديدة) · `Program.cs` (لا سياسة جديدة).

### 11-2. الملفّات التي ستُعدَّل، بأدنى سطح ممكن

| الملفّ | التعديل | مستوى الخطر |
|---|---|---|
| `Enums.cs` | إضافة 7 تعدادات في النهاية | منخفض جدًّا |
| `Project.cs` | 12 خاصّيّة إضافيّة | منخفض |
| `Decision.cs` | خاصّيّة واحدة | منخفض جدًّا |
| `GovernanceModels.cs` | بارامترات اختياريّة في الذيل | منخفض |
| `GovernanceService.cs` | تطبيق فلترَي `ProjectId` | منخفض |
| `GovernanceController.cs` | بارامترا Query اختياريّان | منخفض جدًّا |
| `AppDbContext.cs` | 3 `DbSet` | منخفض جدًّا |
| `DependencyInjection.cs` | تسجيل 2–3 خدمات | منخفض جدًّا |
| `ProjectDetailPage.tsx` | **إعادة تنظيم إلى تبويبات** | **مرتفع** |
| `types/api.ts` · `format.ts` · `useClients.ts` | أنواع وتسميات وهوكات جديدة | منخفض |

### 11-3. الاختبارات القائمة المهدَّدة — تحديد صريح

مجلّد `tests/Reporting.IntegrationTests/` = **110 ملفًّا**. الملفّات ذات الصلة المباشرة:
`Client360FoundationTests.cs` · `ClientProjectArchiveDeleteTests.cs` · `Phase6ClientProjectTests.cs` · `ProjectWorkstreamsTests.cs` · `ProjectFirstExecutionAggregationTests.cs` · `ProjectRepeatableGridTests.cs` · `MultiTeamProjectVisibilityTests.cs` · `MultiProjectSectionTests.cs` · `ManagementNotesTests.cs` · `GovernanceTests.cs`.

**التقييم**:
- اختبارات الخادم: **لا يُتوقَّع أيّ فشل**. كلّ الإضافات إضافيّة بحتة، والفلاتر الجديدة اختياريّة، ولا يتغيّر أيّ سلوك قائم. `GovernanceTests` القائمة تستدعي `ListRisks` بلا `projectId` ⟶ السلوك مطابق حرفيًّا.
- اختبارات الواجهة: **`ProjectWorkstreams.test.tsx` و`ProjectDeliverables.test.tsx` ستفشلان** حال صار التبويب الافتراضيّ `overview`، لأنّهما تفترضان ظهور تيّارات العمل فور التحميل.

**التصنيف**: هذا **تغيير في سطح الاختبار لا انحدار سلوكيّ**. الإصلاح = إضافة نقرة `fireEvent.click(screen.getByText('التنفيذ'))` قبل التأكيدات، بلا أيّ تعديل على منطق الإنتاج. يُعلَن في تقرير التنفيذ صراحةً ولا يُمرَّر بصمت.

### 11-4. بوّابة الانحدار الإلزاميّة قبل إعلان الإنجاز

1. `dotnet build` ⟶ **0 أخطاء**
2. `Reporting.UnitTests` ⟶ **69/69** + الاختبارات الجديدة لمحرّك الاحتساب
3. الاختبارات المستهدَفة الجديدة (`Project360FoundationTests`) ⟶ **100%**
4. الانحدار المستهدَف: `Phase6ClientProjectTests` + `ProjectWorkstreamsTests` + `ClientProjectArchiveDeleteTests` + `GovernanceTests` + `ManagementNotesTests` + `MultiTeamProjectVisibilityTests` ⟶ **صفر فشل**
5. `tsc` ⟶ **0**؛ `vitest` الكامل ⟶ صفر فشل غير مُعلَن
6. `has-pending-model-changes` ⟶ **No changes**
7. مقارنة قائمة الفشل قبل/بعد على قاعدة معزولة: **Candidate-only = []**

> **درس تشغيليّ مُلزَم من CPW-R2**: نتائج المهامّ الخلفيّة قد تكون بائتة — يُقارَن `mtime` ملفّ المخرجات بوقت آخر تعديل للكود قبل تصديق أيّ فشل.

---

## 12. معايير القبول (Acceptance Criteria)

### أ — البيانات والهجرة
- [ ] هجرة **واحدة** فقط، إضافيّة بحتة (لا Rename/Drop/Alter/Backfill)
- [ ] `has-pending-model-changes` = `No changes`
- [ ] المشاريع القائمة تبقى صالحة بلا أيّ سكربت بيانات
- [ ] `Down` تعكس بالكامل وتُترَك القاعدة كما كانت

### ب — الأهداف
- [ ] CRUD كامل + تعطيل/إعادة تفعيل + حذف نهائيّ محروس
- [ ] `Weight` خارج 0–100 ⟶ 400؛ `Name` فارغ ⟶ 400
- [ ] حذف هدف له مؤشّرات ⟶ 409 `project_objective.delete_forbidden.conflict`
- [ ] هدف من مشروع آخر ⟶ 404

### ج — المؤشّرات
- [ ] CRUD كامل + تعطيل/إعادة تفعيل + حذف محروس
- [ ] الفئات العشر والوحدات الستّ والتكرارات الثلاثة كلّها مدعومة ومختبَرة
- [ ] `POST /readings` يُحدِّث `CurrentValue` و`Achievement` و`Variance` و`Trend` في معاملة واحدة
- [ ] `HigherIsBetter` و`LowerIsBetter` يعطيان نتيجتين صحيحتين ومتعاكستين على نفس المدخلات
- [ ] `Target = 0` أو `Current = null` ⟶ `achievement = null` بلا استثناء وبلا 500
- [ ] قراءة مكرّرة بنفس التاريخ ⟶ 409
- [ ] أقلّ من قراءتين ⟶ `Trend = Unknown`

### د — الصحّة
- [ ] تُحتسَب من المكوّنات الثلاثة بأوزان معلَنة
- [ ] العتبات 80/55 تُنتِج Green/Yellow/Red بدقّة
- [ ] مشروع بلا مؤشّرات وبلا تواريخ ⟶ يُحتسَب من التقدّم وحده بلا خطأ
- [ ] `recompute` **Idempotent**: تشغيلان متتاليان ⟶ نفس القيمة بالضبط
- [ ] لا يقبل أيّ قيمة صحّة من العميل

### هـ — الأمن
- [ ] كلّ قراءة وكلّ كتابة تمرّان بحارس النطاق الموحَّد
- [ ] مشروع خارج النطاق ⟶ **404 لا 403** (مضادّ التعداد)
- [ ] الكتابة محكومة بـ`ManagementOnly`؛ `Employee` ⟶ 403؛ المجهول ⟶ 401
- [ ] `TeamLeader` يقرأ **ويكتب** مؤشّرات مشاريع فرقه (جوهر Manual First)
- [ ] `TeamLeader` **لا** يرى مشروع فريق آخر ⟶ 404
- [ ] معرّف ابن من مشروع آخر ⟶ 404 (اختبار IDOR صريح)
- [ ] كلّ كتابة تُنتج صفّ تدقيق مطابق الاسم
- [ ] صفر سياسة جديدة، صفر دور جديد، صفر تعديل على `Roles.cs`/`Program.cs`

### و — لوحة قائد الفريق
- [ ] `portfolio-health` قرائيّة بحتة، مقيَّدة بالنطاق
- [ ] متوسّط الصحّة والعدد والمتأخّر والحرِج صحيحة حسابيًّا
- [ ] قائدان مختلفان يريان مجموعتين مختلفتين — بلا تسرّب
- [ ] لا شاشة جديدة

### ز — الواجهة
- [ ] التبويبات داخل `ProjectDetailPage` نفسه — **لا مسار جديد ولا شاشة جديدة**
- [ ] `WorkstreamsCard` و`LinkedReportsCard` يعملان كما كانا حرفيًّا
- [ ] الملاحظات/المخاطر/القرارات تعمل داخل تبويباتها
- [ ] `tsc` = 0، البناء ناجح، RTL وعربيّة كاملة

### ح — عدم الانحدار
- [ ] بوّابة §11-4 كاملة خضراء
- [ ] صفر تعديل على منظومة KPI الموظّف (يُثبَت بـ`git diff --stat`)
- [ ] صفر كيان `Task`/`Milestone`/`Deliverable` جديد
- [ ] كلّ تغيير في اختبار قائم مُعلَن ومبرَّر صراحةً في تقرير التنفيذ

---

## 13. خطة التنفيذ (Execution Plan)

**تبدأ فقط بعد**: (1) اعتماد هذا التقرير، (2) حسم §9-ب.

| المرحلة | المحتوى | بوّابة الخروج |
|---|---|---|
| **W0 — قرار** | حسم نَسَب المستندات (أ/ب/ج) وتثبيت قرار `ProjectKpiDirection` | قرار مكتوب |
| **W1 — الدومين** | 7 تعدادات + 3 كيانات + توسعة `Project` و`Decision` + 4 EF Configurations + `DbSet` | `dotnet build` = 0 |
| **W2 — الهجرة** | توليد `AddProject360Foundation` ومراجعتها سطرًا سطرًا | `has-pending-model-changes` = No changes |
| **W3 — المحرّك** | `ProjectHealthPolicy` + `ProjectMetricsCalculator` (دوالّ نقيّة بلا EF) + اختبارات وحدة شاملة للحالات الحديّة | Unit خضراء 100% |
| **W4 — الخدمات** | `IProjectObjectiveService` / `IProjectKpiService` / `IProjectHealthService` + التنفيذ + حارس النطاق الموحَّد + التدقيق | `build` = 0 |
| **W5 — الـAPI** | `ProjectObjectivesController` + `ProjectKpisController` + توسعة `ProjectsController` (overview/brief/execution/health/portfolio-health) + فلترة الحوكمة | Swagger سليم |
| **W6 — اختبارات التكامل** | `Project360FoundationTests` — تغطية CRUD والاحتساب والنطاق وIDOR والأدوار | 100% خضراء |
| **W7 — الواجهة** | تحويل `ProjectDetailPage` إلى تبويبات + تبويبات الأهداف/المؤشّرات/Brief/الملاحظات/القرارات/المخاطر + شريط لوحة قائد الفريق + الأنواع والتسميات | `tsc` = 0 + build |
| **W8 — اختبارات الواجهة** | `Project360.test.tsx` جديد + تحديث معلَن لاختبارَي تيّارات العمل والمخرَجات | `vitest` خضراء |
| **W9 — بوّابة الانحدار** | تنفيذ §11-4 بالكامل على قاعدة معزولة نظيفة | Candidate-only = [] |
| **W10 — التوثيق والتسليم** | تقرير تنفيذ + إعلان صريح لكلّ تغيير اختبار + سطح الباتش | **توقّف — بلا Commit/Push/Deploy بلا تصريح** |

**سطح الباتش المتوقّع**: ≈ 12 ملفًّا معدَّلًا + ≈ 18 ملفًّا جديدًا (كيانات، إعدادات، خدمات، كنترولرات، هجرة، اختبارات، مكوّنات واجهة). **هجرة واحدة. صفر ملفّ خارج `reporting-backend/` و`reporting-frontend/` عدا هذا التقرير.**

---

## خلاصة تنفيذيّة

1. **المعمار جاهز**. النطاق والسياسات والتدقيق ونمط النتيجة كلّها كافية — **صفر سياسة جديدة، صفر دور جديد، صفر توسيع لمحرّك النطاق**.
2. **ثلاثة من أحد عشر مطلبًا مُغطّاة سلفًا بصفر سكيمة**: الملاحظات، والمخاطر، ولوحة قائد الفريق.
3. **الفجوة الحقيقيّة بيانيّة**: 3 جداول + 12 عمودًا على `projects` + عمود على `decisions` = **هجرة واحدة إضافيّة بحتة**.
4. **مؤشّرات المشروع منفصلة تمامًا** عن منظومة KPI الموظّف — وهذا أهمّ قرار معماريّ في التصميم.
5. **أعلى خطر تقنيّ**: تحويل صفحة المشروع إلى تبويبات (لا تبويبات فيها اليوم) وأثره على اختبارَي واجهة — مُعلَن ومُخطَّط لا مُفاجئ.
6. **⛔ حاجب**: `client_documents` غير موجود على `develop`. **يلزم قرار صريح (أ/ب/ج) قبل كتابة أيّ كود.**

---

**الحالة النهائيّة لهذا التقرير**

```
CPW-R3 R1 — READ + DESIGN COMPLETE
STATUS: SUPERSEDED BY R2
SUPERSEDED BY : CPW-R3-PROJECT-360-FOUNDATION-R2-REVISED-DESIGN-REPORT.md
REASON        : OWNER DESIGN DECISIONS D-01 … D-08 (DESIGN REVIEW RESPONSE V1)
§9-ب          : RESOLVED IN R2 — OPTION (أ) — BASE develop @ c157829 / DOCUMENTS TAB DEFERRED
ZERO CODE / ZERO MIGRATION / ZERO COMMIT / ZERO DEPLOY / ZERO ENVIRONMENT TOUCHED
AUTHORITATIVE DESIGN OF RECORD : R2 — NOT THIS DOCUMENT
```
