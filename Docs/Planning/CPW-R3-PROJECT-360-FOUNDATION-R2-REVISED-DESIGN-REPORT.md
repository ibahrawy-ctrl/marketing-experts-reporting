# CPW-R3 — PROJECT 360 FOUNDATION + STRATEGY + OBJECTIVES + KPIs + DELIVERABLES + HEALTH

## تقرير التصميم المُحدَّث (R2) — قراءة-فقط، بلا سطر كود واحد

> **حالة الوثيقة**: مراجعة ثانية (R2) تَنسخ وتَحلّ محلّ R1 في كلّ موضع تعارض.
> **سبب المراجعة**: `CPW-R3 — DESIGN REVIEW RESPONSE V1 / Owner Design Decisions (Mandatory Before Implementation)`.
> **مرجع R1**: `Docs/Planning/CPW-R3-PROJECT-360-FOUNDATION-OBJECTIVES-KPIS-HEALTH-R1-DIAGNOSIS-AND-DESIGN-REPORT.md` (885 سطرًا) — يبقى محفوظًا كسجلّ تشخيصيّ؛ أقسام التشخيص القرائيّ (§1) ومراجعة المعمار (§2) فيه **ما زالت سارية بالكامل ولم تتغيّر**، ولا تُكرَّر هنا إلّا عند الحاجة.
> **الحالة**: **لم يُكتب سطر كود واحد. صفر هجرة. صفر Commit. صفر Push. صفر نشر. صفر بيئة مُلامَسة.**

---

## 0. سجلّ القرارات المُعتمَدة (Owner Decisions Register)

القرارات الثمانية أدناه **مُلزِمة** ومُدمَجة في كلّ أقسام هذه الوثيقة. كلّ قرار له معرّف يُستشهَد به لاحقًا.

| # | المعرّف | القرار | الأثر البنيويّ | القسم المُنفِّذ |
|---|---|---|---|---|
| 1 | `D-01` | §9-ب **محسوم = الخيار (أ)**: البناء فوق `develop` الحاليّة، **تأجيل تبويب المستندات بالكامل**، وعدم ربط CPW-R3 بفرع صلاحيات المستندات | يسقط المتطلَّب 8 من النطاق ⟶ تسليم **10 تبويبات من 11** | §1 |
| 2 | `D-02` | **رفض** استقلال المؤشّرات: التسلسل الإلزاميّ `Project ↓ Objectives ↓ KPIs`. **كلّ مؤشّر يتبع هدفًا واحدًا بالضبط، ومؤشّر بلا هدف غير مسموح** | `ProjectKpi.ObjectiveId` يصبح **NOT NULL** + مسارات API متداخلة تحت الهدف | §5-5، §6-3، §7-3، §9-4، §10 |
| 3 | `D-03` | طبقة جديدة **Project Deliverables** (مُخرَجات تعاقديّة — ليست Tasks ولا محرّك تنفيذ)، ومستقبلًا `Deliverable ↓ Tasks ↓ Subtasks` **بلا إعادة تصميم قاعدة البيانات** | جدول جديد `project_deliverables` + مجال تصنيف جديد + تسوية صريحة مع `WorkstreamDeliverable` القائم | §3، §5-7، §6-4، §9-5 |
| 4 | `D-04` | **Project Strategy** على مستوى **المشروع لا العميل**، بجزء ثابت وجزء **مشروط بنوع المشروع** (SEO / Ads / Social) — «لا يوجد نموذج ثابت لكلّ المشاريع» | جدولان: `project_strategies` (1:1) + `project_strategy_attributes` (مفتاح/قيمة محكوم بكتالوج) | §5-3، §6-5، §9-6 |
| 5 | `D-05` | Project 360 **لوحة قيادة حقيقيّة** لا مجرّد تبويبات: النظرة التنفيذيّة والملخّصات تظهر فور فتح المشروع، والتفاصيل داخل التبويبات بعدها | نقطة نهاية تجميع واحدة `GET /projects/{id}/overview` + إعادة كتابة تصميم الواجهة | §6-2، §8 |
| 6 | `D-06` | **خارطة تطوّر المؤشّر** موثَّقة صراحةً: مرحلة 1 يدويّ (قائد الفريق **أو مدير العميل**) ⟶ مرحلة 2 مشتقّ من المهامّ ⟶ مرحلة 3 تكاملات خارجيّة. **المؤشّر لا يتغيّر، يتغيّر مصدر بياناته فقط** | `ProjectKpiSourceType` + ثلاثة أعمدة مرجع خارجيّ Nullable مُهيَّأة من الآن | §5-5، §13 |
| 7 | `D-07` | **توسيع دور قائد الفريق**: مسؤول عن تقدّم الأهداف + تقدّم المؤشّرات + **تقدّم المخرَجات** + صحّة المشروع؛ وهذه البيانات تُغذّي لوحات الفريق ⟶ الإدارة ⟶ التنفيذيّة ⟶ الشركة | قاعدة وصول مبنيّة على المورد لتحديث التقدّم (بلا دور/سياسة جديدة) + سلسلة تغذية موثَّقة | §7-4، §12 |
| 8 | `D-08` | **توثيق التسلسل النهائيّ المستقبليّ** بحيث لا تتطلّب إضافة إدارة المهامّ/سير العمل/الاعتمادات/الأتمتة/التقارير/اللوحات التنفيذيّة أيّ إعادة بناء | §2 + §14 (إثبات الامتداد بعمود واحد لكلّ حلقة) | §2، §14 |

### 0-1. ما لم يتغيّر عن R1 (يبقى ساريًا حرفيًّا)

- **الممنوعات** كما هي: لا CRM، لا إدارة مهامّ، لا محرّك سير عمل، لا محرّك تخطيط، لا وحدة ماليّة، لا تخطيط موارد، لا Project Workspace الجديدة، لا تكاملات خارجيّة.
- **Manual-First**: كلّ القيم تُدخَل يدويًّا؛ المشتقّ حصرًا هو الاحتساب (نسبة الإنجاز، الانحراف، الاتّجاه، الصحّة).
- **Additive-Only**: صفر `RenameColumn`/`RenameTable`/`DropColumn`/`DropTable`/`AlterColumn` على أيّ كيان قائم، وصفر Backfill.
- **لا نشر**: التسليم = كود + هجرة + اختبارات خضراء محلّيًّا. TEST/RC/Production تحتاج تصاريح مستقلّة.
- **فصل فضاء الأسماء**: منظومة مؤشّرات الموظّفين (`Entities/Kpi`, `Application/Kpi`, `KpiEvaluationService`) **لا تُمَسّ بحرف واحد** — مؤشّرات المشروع منظومة منفصلة تمامًا تحت `Entities/Projects360`.

---

## 1. حسم البند الحاجب (D-01) — المستندات مؤجَّلة رسميًّا

**القرار المعتمَد: الخيار (أ).** ويُثبَّت كالآتي:

| البند | القرار |
|---|---|
| قاعدة البناء | `develop` عند `c157829` — **32 هجرة** (مُتحقَّق منها بعدّ ملفّات الهجرة المتتبَّعة عند `HEAD`)، الرأس `20260713171040_AdminGovernanceReportKpiCorrection` |
| المتطلَّب 8 (تبويب المستندات) | **مؤجَّل بالكامل خارج نطاق CPW-R3** |
| الارتباط بفرع صلاحيات المستندات (`3344f78`) | **ممنوع** — لا مرجع، لا استيراد، لا `IDocumentAccessEvaluator`، لا `client_documents` |
| التسليم | **10 تبويبات من 11** (النظرة التنفيذيّة، الاستراتيجيّة، البريف، الأهداف، المؤشّرات، المخرَجات، المخاطر، القرارات، الملاحظات، الصحّة) |
| بعد دمج CPW-R2 لاحقًا | تبويب المستندات يصبح **Wiring + UI فقط** — صفر جدول، صفر هجرة، صفر خدمة جديدة (الخادم في ذلك الفرع يدعم الفلترة سلفًا) |
| ثبات القرار | **لا يتغيّر أثناء التنفيذ.** أيّ طلب لإعادة إدخال المستندات ⟶ تذكرة مستقلّة `CPW-R3-DOCS-WIRING-R1` بعد الدمج |

**أثر جانبيّ مُعلَن**: خطر `R9` في R1 (حاجب المستندات) و`R10` (اصطدام دمج CPW-R2) ينحلّان جزئيًّا — يُعاد تقييمهما في §11.

---

## 2. التسلسل النهائيّ المستهدَف (D-08) — العمود الفقريّ للتصميم

```
Client
 └── Project
       ├── Strategy                    ← جديد في CPW-R3 (1:1)
       ├── Objectives                  ← جديد في CPW-R3
       │     └── KPIs                  ← جديد في CPW-R3 (إلزاميّ التبعيّة)
       │           └── KPI Readings     ← جديد في CPW-R3 (لاشتقاق الاتّجاه)
       ├── Deliverables                ← جديد في CPW-R3 (تعاقديّ)
       │     └── Tasks                  ← مستقبليّ (خارج النطاق)
       │           └── Subtasks          ← مستقبليّ (خارج النطاق)
       ├── Risks                       ← قائم (ProjectId موجود)
       ├── Decisions                   ← قائم + عمود ProjectId جديد
       ├── Notes                       ← قائم (ManagementNote polymorphic)
       ├── Documents                   ← مؤجَّل (D-01)
       ├── External Links              ← مؤجَّل (يعيش على فرع CPW-R2)
       ├── Health                      ← مشتقّ ومخزَّن على Project
       └── Dashboard (360)             ← تجميع قرائيّ فوق كلّ ما سبق
```

### 2-1. إثبات «صفر إعادة هيكلة مستقبلًا»

| الطبقة المستقبليّة | ما يلزم إضافته | إعادة هيكلة؟ |
|---|---|---|
| `Task` | جدول جديد بعمود `DeliverableId` (FK ⟶ `project_deliverables`) | **لا** |
| `Subtask` | جدول جديد بعمود `TaskId` | **لا** |
| Workflow / Approvals | جدول `*_transitions` يشير إلى `EntityType`+`EntityId` (نمط `ManagementNote` القائم) | **لا** |
| Automation | `ProjectKpi.SourceType = TaskDerived` + قراءة من المهامّ | **لا** (العمود مُهيَّأ من الآن — D-06) |
| Integrations | `ProjectKpi.SourceType = Integration` + `ExternalSourceKey`/`ExternalMetricCode` | **لا** (الأعمدة مُهيَّأة من الآن — D-06) |
| Executive Dashboards | استعلامات تجميع فوق `projects.HealthPercent` + `project_objectives` + `project_kpis` + `project_deliverables` | **لا** |
| Documents Tab | Wiring فقط بعد دمج CPW-R2 | **لا** |

**القاعدة الحاكمة**: كلّ حلقة مستقبليّة تُربَط بـ**عمود مفتاح أجنبيّ واحد يشير للأعلى**، ولا تتطلّب تعديل أيّ جدول من CPW-R3.

---

## 3. تسوية تضارب التسمية: `ProjectDeliverable` مقابل `WorkstreamDeliverable` (D-03)

### 3-1. الحقيقة القرائيّة (مُثبَتة على `develop` عند `c157829`)

طبقة **مخرَجات تيّار العمل قائمة ومنشورة بالفعل** بحزمة كاملة من 24 ملفًّا:

```
Domain/Entities/Clients/ProjectWorkstream.cs        Domain/Entities/Clients/WorkstreamDeliverable.cs
Application/Clients/WorkstreamModels.cs             Application/Clients/DeliverableModels.cs
Application/Clients/IProjectWorkstreamService.cs    Application/Clients/IWorkstreamDeliverableService.cs
Infrastructure/Services/ProjectWorkstreamService.cs Infrastructure/Services/WorkstreamDeliverableService.cs
Persistence/Configurations/ProjectWorkstreamConfiguration.cs
Persistence/Configurations/WorkstreamDeliverableConfiguration.cs
Api/Controllers/ProjectWorkstreamsController.cs     Api/Controllers/WorkstreamDeliverablesController.cs
Migrations/20260709222126_AddProjectWorkstreams     Migrations/20260709231845_AddWorkstreamDeliverables
tests/…/ProjectWorkstreamsTests.cs                  tests/…/WorkstreamDeliverablesTests.cs
frontend: useProjectWorkstreams.ts, useWorkstreamDeliverables.ts,
          ProjectWorkstreams.test.tsx, ProjectDeliverables.test.tsx
```

وتعليق الكيان القائم حرفيًّا: *«هذا سجلّ **تخطيط** فقط — لا يُسجَّل هنا أيّ تنفيذ فعليّ (يأتي التنفيذ في مرحلة لاحقة P4)»*، وهو معلَّق تحت **تيّار عمل** لا تحت المشروع مباشرةً.

### 3-2. لماذا لا يصلح إعادة الاستعمال، ولماذا لا يصلح إعادة التسمية

| الخيار | الحكم | السبب |
|---|---|---|
| إعادة استعمال `WorkstreamDeliverable` كطبقة تعاقديّة | **مرفوض** | أبوه `WorkstreamId` **إلزاميّ**؛ جعل المخرَج التعاقديّ تابعًا لتيّار عمل يُلزِم إنشاء تيّار عمل وهميّ لكلّ عقد، ويكسر دلالة «مخرَج مُتّفق عليه مع العميل» |
| إعادة تسمية `WorkstreamDeliverable` | **مرفوض قطعًا** | يخرق قاعدة Additive-Only: `RenameTable`/`RenameColumn` + كسر عقد API منشور + كسر 2 ملفّ اختبار خلفيّ و2 أماميّ |
| توسعة `WorkstreamDeliverable` بـ`ProjectId` وجعل `WorkstreamId` اختياريًّا | **مرفوض** | `AlterColumn` على عمود قائم NOT NULL — خرق صريح لـAdditive-Only |
| **كيانان متمايزان بتسمية صريحة** | **معتمَد** | صفر مساس بالقائم، ودلالتان مختلفتان فعلًا |

### 3-3. الحدّ الفاصل المعتمَد (يُكتب في تعليق XML على كلا الكيانين)

| البُعد | `ProjectDeliverable` (**جديد — تعاقديّ**) | `WorkstreamDeliverable` (**قائم — إنتاجيّ/تخطيطيّ**) |
|---|---|---|
| الأب | `Project` (إلزاميّ) | `ProjectWorkstream` (إلزاميّ) |
| المعنى | **ما التزمنا بتسليمه للعميل** (بند العقد) | **كم وحدة إنتاج يخطّط لها تيّار العمل** |
| المستوى | مستوى المشروع/العقد | مستوى التنفيذ الداخليّ |
| أمثلة | خطّة محتوى شهريّة، تقويم شهريّ، تقرير SEO شهريّ، استراتيجيّة العلامة، دليل الهويّة | 20 منشورًا، 12 ريلز، 30 ستوري، صفحة هبوط |
| كتالوج النوع | مجال جديد `contract_deliverable` | مجال قائم `deliverable` (21 قيمة) |
| التتبّع | حالة + نسبة إنجاز + كمّيّة منجَزة (تقدّم فعليّ) | كمّيّة مخطَّطة فقط (بلا تنفيذ — بالتصميم) |
| المستقبل | **أب المهامّ** (`Task.DeliverableId`) | يبقى طبقة تخطيط إنتاج (P4 لاحقًا) |
| الربط بينهما | عمود `WorkstreamId` **اختياريّ (Nullable)** على `ProjectDeliverable` — جسر لا إلزام | لا تغيير إطلاقًا |

**الضمانة الصارمة**: `git diff --stat` عند التسليم يجب أن يُظهر **صفر تغيير** في العشرة ملفّات الخلفيّة و4 ملفّات الواجهة الخاصّة بـ`Workstream*`/`WorkstreamDeliverable*`. يُدرَج هذا كمعيار قبول (§15) وكخطر (§11 · `R13`).

---

## 4. تحليل الفجوات المُحدَّث (Gap Analysis — R2)

| # | المتطلَّب | القائم على `develop` | الفجوة | الأثر السكيميّ |
|---|---|---|---|---|
| 1 | Executive Overview | `Project` أساسيّ فقط | ملخّص + مالك + قائد فريق + نسبة تقدّم + صحّة | **أعمدة على `projects`** |
| 2 | Project Brief | لا شيء | 5 حقول نصّيّة | **أعمدة على `projects`** |
| 3 | **Project Strategy** (D-04) | لا شيء | 11 حقلًا ثابتًا + سمات مشروطة بالنوع | **جدولان جديدان + مجال كتالوج** |
| 4 | Project Objectives (D-02) | لا شيء | كيان كامل | **جدول جديد** |
| 5 | Project KPIs (D-02) | لا شيء (مؤشّرات الموظّفين منفصلة تمامًا) | كيان كامل **تابع إلزاميًّا لهدف** | **جدول جديد** |
| 6 | KPI Categories | لا شيء | تعداد 10 فئات | صفر سكيمة إضافيّة (عمود ضمن الجدول الجديد) |
| 7 | KPI Progress (إنجاز/صحّة/انحراف/اتّجاه) | لا شيء | محرّك احتساب + سجلّ قراءات | **جدول قراءات جديد** |
| 8 | **Project Deliverables** (D-03) | `WorkstreamDeliverable` — دلالة مختلفة | كيان تعاقديّ مستقلّ | **جدول جديد + مجال كتالوج** |
| 9 | Project Health | لا شيء | محرّك + قيمة مخزَّنة | **أعمدة على `projects`** |
| 10 | **Project 360 Dashboard** (D-05) | `ProjectDetailPage` بلا تبويبات إطلاقًا | لوحة + 10 تبويبات | صفر سكيمة (تجميع قرائيّ) |
| 11 | Documents Tab | **غير موجود على `develop`** | — | **مؤجَّل (D-01)** |
| 12 | Notes Tab | `ManagementNote` + `EntityType.Project = 8` | ربط واجهة فقط | **صفر سكيمة** |
| 13 | Decisions Tab | `Decision` بلا `ProjectId` | عمود ربط واحد | **عمود واحد** |
| 14 | Risks Tab | `Risk.ProjectId` موجود | فلترة وعرض فقط | **صفر سكيمة** |
| 15 | Team Leader Integration (D-07) | `IClientProjectAccess` يغطّي «مشاريع الفرق التي يقودها» | قاعدة كتابة للتقدّم + عرض | **صفر نطاق جديد، صفر دور، صفر سياسة** |
| 16 | KPI Evolution Roadmap (D-06) | لا شيء | نوع مصدر + مراجع خارجيّة مُهيَّأة | عمودان + مرجعان (كلّها بقيمة افتراضيّة/Nullable) |

**الخلاصة السكيميّة**: **6 جداول جديدة** + **توسعة أعمدة على `projects`** + **عمود واحد على `decisions`** + **مجالان جديدان في كتالوج التصنيفات**. لا أكثر. صفر تعديل على أيّ جدول قائم عدا الإضافة المحضة.

---

## 5. نموذج الدومين (Domain Model — R2)

كلّ الكيانات الجديدة تحت فضاء الأسماء `Reporting.Domain.Entities.Projects360` وترث `BaseEntity` (يوفّر `Id`, `CreatedAtUtc`, `UpdatedAtUtc`). **لا يُمَسّ فضاء `Entities.Kpi` (مؤشّرات الموظّفين) ولا `Entities.Clients` (تيّارات العمل ومخرَجاتها).**

### 5-1. التعدادات (Enums)

تُضاف في `Reporting.Domain/Enums.cs` كما هي عادة المشروع، وتُخزَّن **نصًّا** (`HasConversion<string>()` + `varchar(20)`) اتّساقًا مع النمط القائم.

```csharp
public enum ProjectObjectiveStatus  { NotStarted = 0, InProgress = 1, AtRisk = 2, Completed = 3, Cancelled = 4 }

public enum ProjectKpiCategory      { Marketing = 0, Seo = 1, PaidAds = 2, SocialMedia = 3, Sales = 4,
                                      Brand = 5, Content = 6, CustomerService = 7, Operations = 8, Custom = 9 }

public enum ProjectKpiUnit          { Percentage = 0, Number = 1, Currency = 2, Duration = 3, Score = 4, Custom = 5 }

public enum ProjectKpiFrequency     { Weekly = 0, Monthly = 1, Quarterly = 2 }

public enum ProjectKpiDirection     { HigherIsBetter = 0, LowerIsBetter = 1 }

public enum ProjectKpiTrend         { Unknown = 0, Up = 1, Flat = 2, Down = 3 }   // مُشتقّ — لا يُخزَّن

// جديد في R2 (D-06) — مصدر بيانات المؤشّر. المرحلة 1 كلّها Manual.
public enum ProjectKpiSourceType    { Manual = 0, TaskDerived = 1, Integration = 2 }

// جديد في R2 (D-03) — حالة المخرَج التعاقديّ.
public enum ProjectDeliverableStatus { NotStarted = 0, InProgress = 1, Delivered = 2, Delayed = 3, Cancelled = 4 }

public enum ProjectHealthStatus     { Green = 0, Yellow = 1, Red = 2 }
```

**ملاحظات إلزاميّة**:
- `ProjectKpiTrend` **مُشتقّ وقت القراءة** من آخر قراءتين ولا يُخزَّن في القاعدة (لا عمود له) — يمنع بيانات بائتة.
- `ProjectDeliverableStatus.Delayed` **حالة يدويّة معلنة** لا مُشتقّة من التاريخ (Manual-First). يجوز للواجهة أن تُظهر تلميحًا «تجاوز تاريخ الاستحقاق» بصريًّا دون تغيير الحالة المخزَّنة.
- `Priority` على المخرَجات والأهداف **يُعاد استخدام `DeliverablePriority` القائم** — صفر تعداد جديد، صفر ازدواج.

### 5-2. توسعة كيان `Project` (أعمدة إضافيّة فقط)

| الحقل | النوع | إلزاميّ | الغرض | المتطلَّب |
|---|---|---|---|---|
| `Summary` | `string?` (1000) | لا | ملخّص تنفيذيّ سطر–فقرة | 1 |
| `ProjectOwnerId` | `Guid?` | لا | مالك المشروع (مرجع مستخدم بلا FK صلب — كنمط `AccountManagerId` القائم) | 1 |
| `TeamLeaderId` | `Guid?` | لا | قائد الفريق المسؤول تشغيليًّا (D-07) | 1، 15 |
| `ProgressPercent` | `decimal` (5,2) | نعم، افتراضيّ `0` | نسبة التنفيذ **يدويّة** | 1، 9 |
| `Background` | `string?` (4000) | لا | خلفيّة المشروع | 2 |
| `BusinessContext` | `string?` (4000) | لا | السياق التجاريّ | 2 |
| `ScopeText` | `string?` (4000) | لا | النطاق — **سُمّي `ScopeText` عمدًا** لأنّ كلمة `Scope` محجوزة دلاليًّا لنطاق الرؤية الأمنيّ في هذا النظام | 2 |
| `OutOfScope` | `string?` (4000) | لا | خارج النطاق | 2 |
| `SuccessDefinition` | `string?` (2000) | لا | تعريف النجاح | 2 |
| `HealthStatus` | `ProjectHealthStatus` | نعم، افتراضيّ `Green` | لون الصحّة المخزَّن | 9 |
| `HealthPercent` | `decimal` (5,2) | نعم، افتراضيّ `0` | نسبة الصحّة المخزَّنة | 9 |
| `HealthComputedAtUtc` | `DateTime?` | لا | ختم آخر احتساب — يكشف البيانات البائتة | 9 |

**تبرير التخزين لا الاشتقاق الكامل للصحّة**: النظرة التنفيذيّة وقوائم المشاريع تحتاج فرزًا وفلترة على الصحّة؛ احتسابها لكلّ صفّ في كلّ استعلام قائمة يعني ضربًا في عدد المشاريع (N+1 على القراءات والمؤشّرات). القيمة تُخزَّن وتُعاد كتابتها **حتميًّا** عند كلّ حدث يؤثّر عليها (إضافة/تعديل/حذف قراءة أو مؤشّر أو هدف، تعديل `ProgressPercent`، تعديل التواريخ)، مع `HealthComputedAtUtc` كإثبات حداثة. **لا وظيفة خلفيّة، لا مجدول، لا Backfill** — هذا شرط `Additive-Only` وشرط «لا محرّك تنفيذ».

**ثبات مضمون**: `Project` القائم يحتفظ بكلّ أعمدته وعلاقاته بلا استثناء (`ClientId`, `Name`, `Code`, `ServiceType`, `Status`, `StartDate`, `EndDate`, `AccountManagerId`, `Description`, `IsActive`, …). كلّ الأعمدة أعلاه **إضافيّة** وإمّا `NULL`-قابلة أو ذات قيمة افتراضيّة ⟶ **صفر Backfill، صفر صفّ يتغيّر عند الهجرة**.

### 5-3. `ProjectStrategy` + `ProjectStrategyAttribute` (D-04)

**المبدأ المعماريّ الحاكم**: «لا يوجد نموذج ثابت لكلّ المشاريع». لذلك تُقسَم الاستراتيجيّة إلى **نواة ثابتة** (11 حقلًا موجودًا في كلّ مشروع مهما كان نوعه) + **سمات مشروطة بالنوع** (مفتاح/قيمة محكوم بكتالوج). البديل — عمود لكلّ حقل لكلّ نوع خدمة — يعني تعديل سكيمة عند كلّ نوع مشروع جديد، وهو انتهاك مباشر لـ`Additive-Only` على المدى الطويل.

#### أ) النواة الثابتة — `ProjectStrategy` (علاقة 1:1 مع `Project`)

| الحقل | النوع | إلزاميّ | الغرض |
|---|---|---|---|
| `ProjectId` | `Guid` | **نعم — فريد** | المشروع (1:1) |
| `Vision` | `string?` (2000) | لا | رؤية المشروع |
| `StrategySummary` | `string?` (4000) | لا | ملخّص الاستراتيجيّة |
| `TargetAudience` | `string?` (2000) | لا | الجمهور المستهدَف |
| `CustomerPersona` | `string?` (4000) | لا | شخصيّة العميل |
| `Positioning` | `string?` (2000) | لا | التموضع |
| `ValueProposition` | `string?` (2000) | لا | القيمة المقدَّمة |
| `Competitors` | `string?` (4000) | لا | المنافسون |
| `ToneOfVoice` | `string?` (1000) | لا | نبرة الصوت |
| `Messaging` | `string?` (4000) | لا | الرسائل الأساسيّة |
| `MarketingApproach` | `string?` (4000) | لا | التوجّه التسويقيّ |
| `SuccessFactors` | `string?` (2000) | لا | عوامل النجاح |
| `IsActive` | `bool` | نعم | تعطيل بدل حذف |

**كلّ الحقول اختياريّة عمدًا**: الاستراتيجيّة تُبنى تدريجيًّا، ولا يجوز أن يمنع حقل ناقص حفظ ما اكتمل. الحقل الإلزاميّ الوحيد هو `ProjectId`.

#### ب) السمات المشروطة — `ProjectStrategyAttribute`

| الحقل | النوع | إلزاميّ | الغرض |
|---|---|---|---|
| `ProjectStrategyId` | `Guid` | نعم | الأب (Cascade) |
| `FieldCode` | `string` (60) | نعم | رمز الحقل من كتالوج `strategy_field` |
| `ValueText` | `string?` (4000) | لا | القيمة النصّيّة |
| `SortOrder` | `int` | نعم | ترتيب العرض |

**الفهرس الفريد**: `(ProjectStrategyId, FieldCode)` — سمة واحدة لكلّ رمز حقل لكلّ استراتيجيّة.

#### ج) مجال الكتالوج الجديد `strategy_field`

يُضاف إلى `ExecutionTaxonomyValue.KnownDomains` (إضافة سطر واحد إلى `HashSet` قائم) ويُبذَر عبر `ExecutionTaxonomySeeder` (النمط القائم: idempotent بمطابقة `(Domain, Code)`، `SortOrder` = عدّاد × 10، `NameEn ?? NameAr`). الرموز **مُسمّاة بمساحة نوع الخدمة** لمنع التصادم:

| نوع الخدمة | الرموز المبذورة |
|---|---|
| `seo` | `seo.keywords`، `seo.search_intent`، `seo.priority_pages`، `seo.competitors` |
| `ads` | `ads.campaign_goal`، `ads.budget`، `ads.target_audience`، `ads.channels`، `ads.offer`، `ads.conversion_goal` |
| `social` | `social.content_pillars`، `social.publishing_frequency`، `social.brand_voice`، `social.platforms` |

**كيف تُشتقّ السمات المعروضة؟** خريطة قراءة-فقط في طبقة التطبيق: `ServiceType.Seo → "seo."`، `ServiceType.MediaBuying → "ads."`، `ServiceType.Social → "social."`، وأيّ نوع آخر (`Website`, `Video`, `Branding`, `Other`) ⟶ **النواة الثابتة فقط بلا سمات مشروطة**. نقطة نهاية `GET /projects/{id}/strategy/schema` تُرجِع الرموز المتاحة وأسماءها العربيّة، فتبني الواجهة النموذج ديناميكيًّا. **إضافة نوع مشروع جديد لاحقًا = بذر رموز جديدة + سطر في الخريطة — صفر هجرة.**

**الحدّ الأمنيّ**: `FieldCode` يُتحقَّق منه مقابل الكتالوج على **الخادم** قبل الحفظ ⟶ `project_strategy.field_code_invalid` (400). لا يُقبَل رمز حرّ.

### 5-4. `ProjectObjective` (D-02 — أب المؤشّرات)

| الحقل | النوع | إلزاميّ | الغرض |
|---|---|---|---|
| `ProjectId` | `Guid` | نعم | المشروع (Cascade) |
| `WorkstreamId` | `Guid?` | لا | جسر اختياريّ إلى تيّار عمل قائم — **لا إلزام، لا تغيير على `ProjectWorkstream`** |
| `Name` | `string` (300) | نعم | اسم الهدف |
| `Description` | `string?` (2000) | لا | الوصف |
| `Priority` | `DeliverablePriority` | نعم | يُعاد استخدام التعداد القائم |
| `Weight` | `decimal` (5,2) | نعم، افتراضيّ `0` | وزن الهدف في احتساب الصحّة |
| `Status` | `ProjectObjectiveStatus` | نعم، افتراضيّ `NotStarted` | الحالة |
| `StartDate` / `DueDate` | `DateOnly?` | لا | الإطار الزمنيّ |
| `OwnerUserId` | `Guid?` | لا | مالك الهدف |
| `Notes` | `string?` (2000) | لا | ملاحظات |
| `SortOrder` | `int` | نعم | ترتيب العرض |
| `IsActive` | `bool` | نعم | تعطيل بدل حذف |

**قرار الأوزان (مُثبَّت من R1 وما زال ساريًا)**: مجموع الأوزان **لا يُفرَض** أن يساوي 100. الاحتساب يُطبِّع تلقائيًّا: `w_i / Σw`. السبب: فرض المجموع يجعل إضافة هدف جديد عمليّة تحرير جماعيّ إجباريّ لكلّ الأهداف القائمة، وهو عائق تشغيليّ حقيقيّ. الواجهة تعرض تنبيهًا إرشاديًّا حين `Σw ≠ 100` **دون منع الحفظ**. إذا كانت كلّ الأوزان صفرًا ⟶ يُعامَل الجميع بوزن متساوٍ.

**حارس الحذف (Anti-Orphan — نتيجة مباشرة لـD-02)**: بما أنّ المؤشّر لا يجوز أن يوجد بلا هدف، فحذف هدف يحمل مؤشّرات يُرفَض بـ`project_objective.has_kpis.conflict` (409) مع رسالة تُرشد إلى نقل المؤشّرات أو تعطيلها أوّلًا. **البديل المرفوض** = حذف تعاقبيّ صامت يُبيد قراءات تاريخيّة. الحذف الناعم (`IsActive=false`) متاح دائمًا بلا قيد.

### 5-5. `ProjectKpi` (D-02 + D-06) — **تابع إلزاميًّا لهدف**

| الحقل | النوع | إلزاميّ | الغرض |
|---|---|---|---|
| `ProjectId` | `Guid` | نعم | المشروع — **مُكرَّر عمدًا** (انظر التبرير أدناه) |
| **`ObjectiveId`** | **`Guid`** | **نعم — NOT NULL (D-02)** | **الهدف الأب. مؤشّر بلا هدف مرفوض بنيويًّا لا منطقيًّا** |
| `Name` | `string` (300) | نعم | اسم المؤشّر |
| `Description` | `string?` (2000) | لا | الوصف |
| `Category` | `ProjectKpiCategory` | نعم | الفئة (10 فئات) |
| `Unit` | `ProjectKpiUnit` | نعم | وحدة القياس |
| `CustomUnitLabel` | `string?` (50) | لا | تسمية الوحدة حين `Unit = Custom` |
| `Direction` | `ProjectKpiDirection` | نعم، افتراضيّ `HigherIsBetter` | اتّجاه التحسّن |
| `Frequency` | `ProjectKpiFrequency` | نعم، افتراضيّ `Monthly` | دوريّة القياس |
| `BaselineValue` | `decimal?` (18,2) | لا | خطّ الأساس |
| `TargetValue` | `decimal` (18,2) | نعم | القيمة المستهدَفة |
| `CurrentValue` | `decimal?` (18,2) | لا | آخر قيمة (لقطة سريعة من آخر قراءة) |
| `LastReadingDate` | `DateOnly?` | لا | تاريخ آخر قراءة |
| `Weight` | `decimal` (5,2) | نعم، افتراضيّ `0` | وزن المؤشّر داخل هدفه |
| `SourceType` | `ProjectKpiSourceType` | نعم، افتراضيّ **`Manual`** | **D-06** — مصدر البيانات |
| `ExternalSourceKey` | `string?` (100) | لا | **D-06 مرحلة 3** — مُهيَّأ الآن، غير مستعمل |
| `ExternalMetricCode` | `string?` (100) | لا | **D-06 مرحلة 3** — مُهيَّأ الآن، غير مستعمل |
| `LastSyncedAtUtc` | `DateTime?` | لا | **D-06 مرحلة 3** — مُهيَّأ الآن، غير مستعمل |
| `Notes` | `string?` (2000) | لا | ملاحظات |
| `SortOrder` | `int` | نعم | ترتيب العرض |
| `IsActive` | `bool` | نعم | تعطيل بدل حذف |

**لماذا `ProjectId` مُكرَّر رغم أنّه مُشتقّ عبر `ObjectiveId`؟** ثلاثة أسباب هندسيّة:
1. **الأداء**: لوحة النظرة التنفيذيّة (D-05) تجلب كلّ مؤشّرات المشروع؛ بدون العمود يلزم `JOIN` على الأهداف في كلّ استعلام تجميع.
2. **الأمن**: التحقّق من الرؤية يتمّ على `ProjectId` مباشرة قبل أيّ استعلام آخر — أبسط وأقلّ عرضة لخطأ IDOR.
3. **التكامل المرجعيّ**: يُفرَض **حارس اتّساق على الخادم** عند الإنشاء والتحديث: `objective.ProjectId == kpi.ProjectId` وإلّا `project_kpi.objective_mismatch.conflict` (409). هذا يمنع «مؤشّرًا في مشروع تابعًا لهدف في مشروع آخر» بنيويًّا.

**سلوك الحذف**: `ObjectiveId` بـ`OnDelete(Cascade)` على مستوى السكيمة، **لكن** الحذف الفعليّ محجوب بحارس §5-4 (`has_kpis.conflict`) في طبقة الخدمة. الـCascade هنا شبكة أمان سكيميّة لا مسار تشغيليّ.

**`SourceType` والقفل التشغيليّ (D-06)**: في المرحلة 1، إدخال القراءات مسموح **فقط** حين `SourceType == Manual`. أيّ محاولة إدخال يدويّ لمؤشّر `TaskDerived`/`Integration` تُرفَض بـ`project_kpi.source_not_manual.conflict` (409). بما أنّ كلّ المؤشّرات في المرحلة 1 تُنشأ `Manual` افتراضيًّا، فالحارس **خامل عمليًّا اليوم** لكنّه يجعل المرحلتين 2 و3 تغييرًا في المصدر لا تغييرًا في العقد.

### 5-6. `ProjectKpiReading` — سجلّ القراءات

| الحقل | النوع | إلزاميّ | الغرض |
|---|---|---|---|
| `ProjectKpiId` | `Guid` | نعم | المؤشّر (Cascade) |
| `ReadingDate` | `DateOnly` | نعم | تاريخ القراءة |
| `Value` | `decimal` (18,2) | نعم | القيمة المُدخَلة يدويًّا |
| `TargetSnapshot` | `decimal?` (18,2) | لا | **لقطة الهدف وقت القراءة** |
| `AchievementSnapshot` | `decimal?` (5,2) | لا | **لقطة نسبة الإنجاز وقت القراءة** |
| `RecordedByUserId` | `Guid` | نعم | من سجّل القراءة (مساءلة) |
| `Notes` | `string?` (1000) | لا | ملاحظة القراءة |

**الفهرس الفريد** `(ProjectKpiId, ReadingDate)` ⟶ `project_kpi_reading.duplicate_date.conflict` (409). قراءة واحدة لكلّ تاريخ لكلّ مؤشّر؛ التصحيح يتمّ بتحديث القراءة لا بإضافة ثانية.

**لماذا اللقطتان (`TargetSnapshot` / `AchievementSnapshot`)؟** لأنّ `TargetValue` قابل للتعديل لاحقًا. بدون اللقطة، تعديل الهدف يُعيد كتابة التاريخ بأثر رجعيّ فيبدو أداء الماضي مختلفًا عمّا اعتُمِد فعلًا. هذا **نفس مبدأ `KpiTemplateVersionId`** في منظومة مؤشّرات الموظّفين القائمة — اتّساق معماريّ لا اختراع جديد.

**تحديث اللقطة السريعة**: عند إضافة/تعديل/حذف قراءة، تُعاد كتابة `ProjectKpi.CurrentValue` و`LastReadingDate` من **أحدث قراءة باقية** (لا من القراءة المُدخَلة، حتّى يصحّ الحذف والإدخال بأثر رجعيّ)، ثمّ تُعاد كتابة صحّة المشروع. كلّ ذلك داخل **معاملة واحدة** و`SaveChanges` واحد.

### 5-7. `ProjectDeliverable` (D-03) — المخرَج التعاقديّ

> يُقرأ مع §3 (تسوية التسمية). هذا كيان **جديد تمامًا**، ولا يمسّ `WorkstreamDeliverable` بحرف.

| الحقل | النوع | إلزاميّ | الغرض |
|---|---|---|---|
| `ProjectId` | `Guid` | نعم | المشروع (Cascade) |
| `ObjectiveId` | `Guid?` | **لا — اختياريّ** | ربط اختياريّ بهدف (المخرَج التزام تعاقديّ قد لا يخدم هدفًا بعينه) |
| `WorkstreamId` | `Guid?` | لا | جسر اختياريّ إلى تيّار العمل المنفِّذ |
| `DeliverableTypeCode` | `string` (60) | نعم | رمز من كتالوج **`contract_deliverable`** (جديد) — **لقطة ثابتة لا تتغيّر بعد الإنشاء** (نفس قاعدة `WorkstreamDeliverable`) |
| `Name` | `string` (300) | نعم | اسم المخرَج |
| `Description` | `string?` (2000) | لا | الوصف |
| `PlannedQuantity` | `int` | نعم، افتراضيّ `1` | الكمّيّة المتعاقَد عليها |
| `CompletedQuantity` | `int` | نعم، افتراضيّ `0` | الكمّيّة المُسلَّمة |
| `Status` | `ProjectDeliverableStatus` | نعم، افتراضيّ `NotStarted` | الحالة |
| `ProgressPercent` | `decimal` (5,2) | نعم، افتراضيّ `0` | نسبة الإنجاز **يدويّة** |
| `StartDate` / `DueDate` | `DateOnly?` | لا | الإطار الزمنيّ |
| `DeliveredAtUtc` | `DateTime?` | لا | ختم التسليم الفعليّ |
| `Priority` | `DeliverablePriority` | نعم | يُعاد استخدام التعداد القائم |
| `OwnerUserId` | `Guid?` | لا | المسؤول |
| `Notes` | `string?` (2000) | لا | ملاحظات |
| `SortOrder` | `int` | نعم | ترتيب العرض |
| `IsActive` | `bool` | نعم | تعطيل بدل حذف |

**لماذا `ObjectiveId` اختياريّ هنا بينما هو إلزاميّ على المؤشّر؟** لأنّ D-02 نصّ صراحةً على المؤشّرات فقط. المخرَج التزام تعاقديّ قائم بذاته (مثل «تقرير شهريّ») قد يخدم أهدافًا متعدّدة أو لا يخدم هدفًا محدَّدًا؛ إلزامه بهدف يفرض أهدافًا صوريّة. حين يوجد ربط، يُفرَض نفس حارس الاتّساق: `objective.ProjectId == deliverable.ProjectId` وإلّا `project_deliverable.objective_mismatch.conflict` (409). ونفس الحارس على `WorkstreamId` مقابل `workstream.ProjectId`.

**`ProgressPercent` و`CompletedQuantity` منفصلان عمدًا**: الكمّيّة قد لا تصفُ التقدّم (خطّة محتوى واحدة قد تكون 60% منجَزة). لا اشتقاق آليّ بينهما — **Manual-First**. الواجهة تعرض النسبة المحسوبة من الكمّيّة كـ**اقتراح** فقط.

**مجال الكتالوج الجديد `contract_deliverable`** (يُضاف إلى `KnownDomains` ويُبذَر عبر `ExecutionTaxonomySeeder` بنفس النمط القائم) — الرموز الأوّليّة مأخوذة حرفيًّا من أمثلة المالك في D-03:

| المحور | الرموز |
|---|---|
| Social Media | `monthly_content_plan`، `monthly_calendar`، `posts_package`، `reels_package`، `stories_package`، `monthly_report` |
| SEO | `keyword_research_doc`، `technical_audit`، `monthly_seo_report`، `onpage_optimization` |
| Branding | `brand_strategy`، `brand_identity`، `logo`، `brand_guidelines` |
| Ads | `campaign_structure`، `creatives_package`، `landing_page_delivery`، `performance_report` |

**تنبيه تسمية مقصود**: `landing_page_delivery` وليس `landing_page` — لأنّ `landing_page` مستعمل سلفًا في مجال `deliverable` القائم. المجالان منفصلان تقنيًّا (الفهرس الفريد على `(Domain, Code)`)، لكنّ التمييز اللفظيّ يمنع الخلط البشريّ في التقارير والسجلّات.

### 5-8. `Decision.ProjectId` — عمود ربط واحد

الكيان `Decision` القائم يُوسَّع بعمود واحد `ProjectId` من نوع `Guid?` (اختياريّ، فهرس عاديّ، `OnDelete(SetNull)` أو بلا FK صلب اتّساقًا مع النمط القائم في الكيان). القرارات القائمة تبقى بلا ربط (`NULL`) ⟶ **صفر Backfill**. تبويب القرارات يفلتر على `ProjectId == id`.

**لماذا لا يُستعمل نمط `EntityType + EntityId` كما في `ManagementNote`؟** لأنّ `Decision` كيان مباشر بأعمدة مرجع صريحة، وإدخال نمط متعدّد الأشكال عليه الآن تغيير معماريّ غير مطلوب. عمود واحد يحقّق المتطلَّب بأقلّ سطح.

### 5-9. مخطّط العلاقات النهائيّ (ER)

```
                                  ┌──────────────┐
                                  │    Client    │  (قائم — بلا تغيير)
                                  └──────┬───────┘
                                         │ 1..*
                                  ┌──────▼───────────────────────────────┐
                                  │             Project                  │
                                  │  (قائم + 12 عمودًا إضافيًّا)          │
                                  └──┬───┬───┬───┬───┬────────┬──────────┘
                    1:1 ┌────────────┘   │   │   │   │        │
        ┌───────────────▼──────────┐     │   │   │   │        │  (قائمة — بلا تغيير)
        │     ProjectStrategy      │     │   │   │   │        ├──► ProjectWorkstream ──► WorkstreamDeliverable
        └───────────┬──────────────┘     │   │   │   │        ├──► Risk (ProjectId قائم)
                    │ 1..*               │   │   │   │        └──► ManagementNote (EntityType=Project)
        ┌───────────▼──────────────┐     │   │   │   │
        │ ProjectStrategyAttribute │     │   │   │   └──────────► Decision (+ ProjectId جديد)
        │  (FieldCode ← كتالوج)    │     │   │   │
        └──────────────────────────┘     │   │   │
                                         │   │   └───────────► ProjectDeliverable
                          1..* ┌─────────┘   │                  ├─ ObjectiveId? (اختياريّ)
              ┌───────────────▼──────────┐   │                  ├─ WorkstreamId? (جسر)
              │    ProjectObjective      │   │                  └─ TypeCode ← كتالوج contract_deliverable
              │  (WorkstreamId? جسر)     │   │
              └───────────┬──────────────┘   │
                          │ 1..*             │
              ┌───────────▼──────────────┐   │
              │       ProjectKpi         │◄──┘  ProjectId مُكرَّر (أداء + أمن)
              │  ObjectiveId **NOT NULL**│      + حارس اتّساق على الخادم
              │  SourceType = Manual     │
              └───────────┬──────────────┘
                          │ 1..*
              ┌───────────▼──────────────┐
              │    ProjectKpiReading     │  فريد (ProjectKpiId, ReadingDate)
              └──────────────────────────┘
```

**قراءة المخطّط**: الأعمدة/الجداول الجديدة كلّها **أوراق أو فروع مضافة**؛ لا حافّة قائمة غُيِّر اتّجاهها أو نوعها. `WorkstreamDeliverable` معلَّق على فرعه القديم تمامًا كما هو.

---

## 6. تصميم الـAPI (R2)

### 6-1. المبادئ الحاكمة (بلا استثناء)

1. كلّ المسارات تحت `ProjectsController` القائم أو كنترولرات جديدة **بنفس الأساس** `ApiControllerBase` ونمط `FromResult()`.
2. النمط `Result<T>.Success(...)` / `Result<T>.Failure(message, errorCode)` حصرًا — بلا استثناءات كمسار تحكّم.
3. **مصيدة الرؤية أوّلًا**: كلّ مسار يبدأ بالتحقّق من رؤية المشروع؛ الرفض ⟶ **404 `project.not_found`** لا 403 (مضادّ التعداد).
4. **مضادّ IDOR**: كلّ مورد ابن يُتحقَّق من أبوّته صراحةً قبل أيّ عمليّة (`objective.ProjectId == id`, `kpi.ObjectiveId == objectiveId`, `reading.ProjectKpiId == kpiId`).
5. كلّ تعديل يُسجَّل تدقيقًا عبر `IAuditService.LogAsync` بالنمط `{entity}.{verb}`.
6. صفر تغيير على أيّ مسار قائم — الإضافة فقط.

### 6-2. نقطة النظرة التنفيذيّة المجمَّعة (D-05)

```
GET /api/projects/{id}/overview        → ProjectOverviewDto
```

استجابة **واحدة** تُغذّي كامل شاشة الفتح، لتجنّب 8 نداءات متوازية عند فتح المشروع:

```
ProjectOverviewDto
├── project        : { id, name, code, serviceType, status, clientId, clientName,
│                      startDate, endDate, progressPercent,
│                      healthStatus, healthPercent, healthComputedAtUtc,
│                      projectOwnerId/Name, teamLeaderId/Name, accountManagerId/Name, summary }
├── objectives     : { total, notStarted, inProgress, atRisk, completed,
│                      averageAchievementPercent, items[] ← أعلى 5 حسب الترتيب }
├── kpis           : { total, onTrack, atRisk, offTrack, withoutReadings,
│                      lastUpdatedAtUtc, averageAchievementPercent }
├── deliverables   : { total, completed, pending, delayed, overallProgressPercent }
├── risks          : { total, open, mitigated, closed, highSeverityOpen }
├── decisions      : { total, latest[] ← أحدث 3 }
├── notes          : { total, latest[] ← أحدث 3 }
├── strategy       : { exists, filledCoreFields, totalCoreFields, attributesCount }
└── recentActivity : [] ← أحدث 10 أحداث مُوحَّدة (قراءة مؤشّر / تغيير حالة هدف /
                          تسليم مخرَج / قرار / ملاحظة / مخاطرة) — قراءة-فقط
```

**«النشاط الأخير» بلا جدول جديد**: يُبنى بضمّ (`UNION`) استعلامات صغيرة مُرتَّبة زمنيًّا فوق الجداول القائمة والجديدة، محدودة بـ`Take(10)` بعد الترتيب. **لا `activity_log` جديد، لا Event Sourcing، لا مُشغِّلات قاعدة بيانات** — ذلك يُعَدّ محرّك تنفيذ وهو من الممنوعات.

**حدّ الأداء المُلزِم**: هذه النقطة **قراءة-فقط** وتستعمل `AsNoTracking()` وتجميعات على مستوى القاعدة (`GroupBy`/`Count`) لا تحميلًا كاملًا للمجموعات. **صفر N+1** — يُدرَج كمعيار قبول (§15).

### 6-3. الأهداف والمؤشّرات — التداخل الإلزاميّ (D-02)

```
# الأهداف
GET    /api/projects/{id}/objectives                 → قائمة (فلتر: status, isActive, q)
POST   /api/projects/{id}/objectives                 → إنشاء
GET    /api/projects/{id}/objectives/{objectiveId}   → تفصيل (يشمل مؤشّراته)
PUT    /api/projects/{id}/objectives/{objectiveId}   → تعديل
DELETE /api/projects/{id}/objectives/{objectiveId}   → حذف (محروس: 409 إن كان يحمل مؤشّرات)

# المؤشّرات — **متداخلة تحت الهدف حصرًا** (لا يمكن إنشاء مؤشّر بلا هدف بنيويًّا)
GET    /api/projects/{id}/objectives/{objectiveId}/kpis          → قائمة مؤشّرات الهدف
POST   /api/projects/{id}/objectives/{objectiveId}/kpis          → إنشاء مؤشّر
GET    /api/projects/{id}/objectives/{objectiveId}/kpis/{kpiId}  → تفصيل + القراءات + الاتّجاه
PUT    /api/projects/{id}/objectives/{objectiveId}/kpis/{kpiId}  → تعديل
DELETE /api/projects/{id}/objectives/{objectiveId}/kpis/{kpiId}  → حذف

# قراءة أفقيّة واحدة لكامل المشروع (لتبويب المؤشّرات واللوحة) — **قراءة فقط**
GET    /api/projects/{id}/kpis                       → كلّ مؤشّرات المشروع مجمَّعة حسب الهدف
                                                       (فلتر: category, objectiveId, status)

# القراءات
GET    /api/projects/{id}/objectives/{objectiveId}/kpis/{kpiId}/readings
POST   /api/projects/{id}/objectives/{objectiveId}/kpis/{kpiId}/readings
PUT    /api/projects/{id}/objectives/{objectiveId}/kpis/{kpiId}/readings/{readingId}
DELETE /api/projects/{id}/objectives/{objectiveId}/kpis/{kpiId}/readings/{readingId}
```

**القرار الحاسم في هذا التصميم**: **لا يوجد `POST /api/projects/{id}/kpis`**. إنشاء مؤشّر يمرّ عبر مسار الهدف حصرًا ⟶ D-02 مفروض على مستوى **شكل الـAPI** لا على مستوى تحقّق مُبرمَج فقط. القراءة الأفقيّة (`GET /projects/{id}/kpis`) موجودة لأنّ اللوحة والتبويب يحتاجانها، وهي **قراءة-فقط بلا أيّ فعل كتابة**.

**نقل مؤشّر بين هدفين**: مسار صريح واحد
```
PATCH /api/projects/{id}/objectives/{objectiveId}/kpis/{kpiId}/objective   { newObjectiveId }
```
يُتحقَّق أنّ الهدفين في نفس المشروع، ويُسجَّل تدقيقًا `project_kpi.objective_changed`. هذا هو المسار الرسميّ لتفريغ هدف قبل حذفه (حارس §5-4).

**أكواد الأخطاء المُعرَّفة**:

| الكود | HTTP | الحالة |
|---|---|---|
| `project.not_found` | 404 | المشروع غير موجود أو خارج نطاق الرؤية |
| `project_objective.not_found` | 404 | الهدف غير موجود أو لا يتبع المشروع |
| `project_objective.has_kpis.conflict` | 409 | حذف هدف يحمل مؤشّرات |
| `project_kpi.not_found` | 404 | المؤشّر غير موجود أو لا يتبع الهدف |
| `project_kpi.objective_required` | 400 | محاولة إنشاء/تحديث بلا هدف |
| `project_kpi.objective_mismatch.conflict` | 409 | الهدف يتبع مشروعًا آخر |
| `project_kpi.source_not_manual.conflict` | 409 | إدخال قراءة يدويّة لمؤشّر غير يدويّ (D-06) |
| `project_kpi.target_invalid` | 400 | `TargetValue ≤ 0` مع اتّجاه يتطلّب قسمة |
| `project_kpi_reading.duplicate_date.conflict` | 409 | قراءة ثانية لنفس التاريخ |
| `project_kpi_reading.not_found` | 404 | القراءة لا تتبع المؤشّر |

### 6-4. المخرَجات التعاقديّة (D-03)

```
GET    /api/projects/{id}/deliverables            → قائمة (فلتر: status, objectiveId, typeCode, isActive, q)
POST   /api/projects/{id}/deliverables            → إنشاء
GET    /api/projects/{id}/deliverables/{delivId}  → تفصيل
PUT    /api/projects/{id}/deliverables/{delivId}  → تعديل
PATCH  /api/projects/{id}/deliverables/{delivId}/progress   → تحديث التقدّم فقط (D-07)
DELETE /api/projects/{id}/deliverables/{delivId}  → حذف
GET    /api/projects/{id}/deliverables/types      → رموز كتالوج contract_deliverable النشطة
```

**فصل `PATCH …/progress` عن `PUT`**: مقصود ومطلوب أمنيًّا (§7-4). `PUT` تعديل بنيويّ (اسم/نوع/كمّيّة متعاقَد عليها/تواريخ) محكوم بـ`ManagementOnly`؛ `PATCH …/progress` يقبل `{ completedQuantity, progressPercent, status, deliveredAtUtc, notes }` فقط ومتاح لقائد الفريق ومدير العميل. **`DeliverableTypeCode` غير قابل للتغيير بعد الإنشاء** (لقطة ثابتة) ⟶ `project_deliverable.type_immutable.conflict` (409) — نفس قاعدة `WorkstreamDeliverable` القائمة.

| الكود | HTTP | الحالة |
|---|---|---|
| `project_deliverable.not_found` | 404 | غير موجود أو لا يتبع المشروع |
| `project_deliverable.type_invalid` | 400 | رمز خارج كتالوج `contract_deliverable` أو معطَّل |
| `project_deliverable.type_immutable.conflict` | 409 | محاولة تغيير النوع بعد الإنشاء |
| `project_deliverable.objective_mismatch.conflict` | 409 | الهدف المرتبط يتبع مشروعًا آخر |
| `project_deliverable.workstream_mismatch.conflict` | 409 | تيّار العمل يتبع مشروعًا آخر |
| `project_deliverable.quantity_invalid` | 400 | `CompletedQuantity < 0` أو `> PlannedQuantity` |

### 6-5. الاستراتيجيّة (D-04)

```
GET /api/projects/{id}/strategy          → الاستراتيجيّة (النواة + السمات). تُرجِع 200 بجسم فارغ
                                           منطقيًّا (exists=false) لا 404 — عدم وجود استراتيجيّة حالة طبيعيّة.
PUT /api/projects/{id}/strategy          → Upsert (إنشاء إن لم توجد، تحديث إن وُجدت) — النواة + السمات معًا
GET /api/projects/{id}/strategy/schema   → مخطّط الحقول المشروطة المشتقّ من ServiceType
```

**لماذا `PUT` واحد بدل CRUD كامل للسمات؟** لأنّ الاستراتيجيّة تُحرَّر كنموذج واحد في الواجهة. `PUT` واحد داخل معاملة واحدة يضمن اتّساق النواة مع السمات، ويتجنّب حالات نصف-محفوظة. السمات تُزامَن **تفاضليًّا** (حذف الزائد، إضافة الناقص، تحديث المتغيّر) — لا `RemoveRange` ثمّ `AddRange` (درس مُستفاد موثَّق من `CPWR2-DEF-01`: الحذف-ثمّ-الإضافة على فهرس فريد داخل نفس المعاملة يصطدم بالفهرس ويُنتج `DbUpdateConcurrencyException`).

**استجابة `/schema`**:
```
{ serviceType: "Seo",
  coreFields: [ { code: "vision", nameAr: "رؤية المشروع", maxLength: 2000 }, … 11 حقلًا ],
  conditionalFields: [ { code: "seo.keywords", nameAr: "الكلمات المفتاحيّة", sortOrder: 10 }, … ] }
```

| الكود | HTTP | الحالة |
|---|---|---|
| `project_strategy.field_code_invalid` | 400 | رمز سمة خارج الكتالوج أو معطَّل |
| `project_strategy.field_not_applicable` | 400 | رمز صحيح لكنّه لا ينتمي لنوع خدمة المشروع |
| `project_strategy.duplicate_field.conflict` | 409 | تكرار الرمز نفسه في الحمولة |

### 6-6. البريف والصحّة والتبويبات القائمة

```
PUT   /api/projects/{id}/brief            → { summary, background, businessContext,
                                              scopeText, outOfScope, successDefinition }
PATCH /api/projects/{id}/progress         → { progressPercent }   (D-07 — قائد الفريق/مدير العميل)
GET   /api/projects/{id}/health           → تفصيل الصحّة ومكوّناتها الثلاثة وتاريخ الاحتساب
POST  /api/projects/{id}/health/recompute → إعادة احتساب صريحة (يدويّة، محكومة، مُدقَّقة)

# التبويبات القائمة — إعادة استعمال بلا كيان جديد
GET   /api/projects/{id}/risks            → فلترة على Risk.ProjectId (قائم)
GET   /api/projects/{id}/decisions        → فلترة على Decision.ProjectId (العمود الجديد)
GET   /api/projects/{id}/notes            → ManagementNote بـ EntityType=Project, EntityId=id (قائم)
```

**`POST /health/recompute` ليس مجدولًا ولا خلفيًّا**: هو مسار مساءلة يدويّ يستعمله المستخدم إن شكّ في حداثة القيمة (مثلًا بعد تحرير جماعيّ). الاحتساب التلقائيّ يقع أصلًا داخل كلّ عمليّة كتابة مؤثّرة.

**البريف عبر `PUT` مستقلّ لا عبر `PUT /projects/{id}` القائم**: حتّى **لا يُمَسّ عقد تعديل المشروع القائم بحرف واحد** (صفر انحدار على `UpdateProjectRequest`). هذا قرار صريح لخفض سطح المخاطرة.

---

## 7. التصميم الأمنيّ (R2)

### 7-1. الأساس: صفر دور جديد، صفر سياسة جديدة

| المكوّن | القرار |
|---|---|
| أدوار Identity | **لا يُضاف أيّ دور** — `AccountManager` ليس دورًا في هذا النظام بل عمود `Project.AccountManagerId` |
| السياسات | **لا تُضاف سياسة جديدة** — يُعاد استعمال `Policies.ManagementOnly` القائم |
| النطاق | يُعاد استعمال `IClientProjectAccess` ⟶ `ClientProjectVisibility(SeesAll, ProjectIds, ClientIds)` — **بلا تعديل بحرف** |
| `EXEC_ROLES` في الواجهة | **لا يُوسَّع** — التبويبات الجديدة داخل صفحة المشروع القائمة المحكومة سلفًا |

### 7-2. طبقات الحماية الأربع (بهذا الترتيب حرفيًّا)

1. **المصادقة** — `[Authorize]` على كلّ مسار؛ المجهول ⟶ 401.
2. **الرؤية** — `IClientProjectAccess` يقرّر إن كان المستخدم يرى المشروع أصلًا. الرفض ⟶ **404 `project.not_found`** (مضادّ التعداد؛ نفس المبدأ المُثبَت في `client_document.not_found` بفرع CPW-R2).
3. **التخويل** — `ManagementOnly` للتعديل البنيويّ؛ قاعدة مبنيّة على المورد لتحديث التقدّم (§7-4).
4. **الأبوّة (مضادّ IDOR)** — كلّ ابن يُطابَق مع أبيه صراحةً قبل أيّ قراءة أو كتابة، مهما بدا المسار محكومًا.

### 7-3. مصفوفة التخويل الكاملة

| العمليّة | من يملكها |
|---|---|
| قراءة كلّ شيء (لوحة، تبويبات، تفاصيل) | كلّ من يرى المشروع عبر `IClientProjectAccess` |
| إنشاء/تعديل/حذف **هدف** | `ManagementOnly` |
| إنشاء/تعديل/حذف **مؤشّر** | `ManagementOnly` |
| نقل مؤشّر بين هدفين | `ManagementOnly` |
| إنشاء/تعديل/حذف **مخرَج** (بنيويًّا) | `ManagementOnly` |
| تحرير **البريف** | `ManagementOnly` |
| تحرير **الاستراتيجيّة** | `ManagementOnly` |
| **إدخال/تعديل/حذف قراءة مؤشّر** | `ManagementOnly` **أو** قائد فريق المشروع **أو** مدير العميل (D-06/D-07) |
| **تحديث تقدّم المخرَج** (`PATCH …/progress`) | `ManagementOnly` **أو** قائد فريق المشروع **أو** مدير العميل |
| **تحديث نسبة تقدّم المشروع** (`PATCH …/progress`) | `ManagementOnly` **أو** قائد فريق المشروع **أو** مدير العميل |
| **تحديث حالة الهدف** (`PATCH …/status`) | `ManagementOnly` **أو** قائد فريق المشروع **أو** مدير العميل |
| إعادة احتساب الصحّة | نفس صلاحيّة تحديث التقدّم |

### 7-4. قاعدة الوصول المبنيّة على المورد (D-06 مرحلة 1 + D-07)

المطلوب من المالك: «قائد الفريق **أو مدير العميل** يُحدِّث». `AccountManager` ليس دورًا، وقائد الفريق دور موجود لكنّه **عامّ** (كلّ قادة الفرق) بينما المطلوب **قائد فريق هذا المشروع تحديدًا**. الحلّ الوحيد الذي لا يوسّع الأدوار:

```
مسارات تحديث التقدّم:  [Authorize]  (مصادقة فقط)
        ثمّ داخل الخدمة، بعد فحص الرؤية:

  bool canUpdateProgress =
        IsManagement(currentUser.Roles)                 // Admin/Ceo/GeneralManager/Manager/TeamLeader
     || project.TeamLeaderId    == currentUserId        // قائد فريق هذا المشروع بعينه
     || project.AccountManagerId == currentUserId;      // مدير عميل هذا المشروع بعينه

  if (!canUpdateProgress) → Result.Failure("…", "auth.forbidden")  // 403
```

**ثلاث ضمانات صريحة**:
1. **توسّع محصور ومُعلَن**: هذه هي التوسعة الأمنيّة الوحيدة في CPW-R3، وهي **على مستوى المورد لا على مستوى الدور**؛ لا تمنح المستخدم أيّ صلاحيّة خارج مشروعه.
2. **لا كتابة بنيويّة**: مدير العميل وقائد الفريق **لا يستطيعان** إنشاء/حذف هدف أو مؤشّر أو مخرَج، ولا تعديل البريف أو الاستراتيجيّة. حقلهما هو التقدّم والقراءات والحالة فقط.
3. **قابليّة الاختبار**: تُكتب اختبارات تكامل صريحة لكلّ فرع: مدير عميل مشروع آخر ⟶ **404** (لا يرى)، مستخدم يرى ولا يملك ⟶ **403**، قائد الفريق المعنيّ ⟶ **200**، محاولته تعديل بنيويّ ⟶ **403**.

### 7-5. مضادّ التعداد ومضادّ IDOR — قواعد ملزِمة

- كلّ فشل رؤية ⟶ **404** لا 403. الفرق بينهما يسرّب وجود المورد.
- كلّ ابن مطلوب بمعرّفه يُحمَّل **مقيَّدًا بأبيه في نفس الاستعلام** (`Where(x => x.Id == kpiId && x.ObjectiveId == objectiveId && x.ProjectId == id)`) لا بتحميله ثمّ مقارنته — يمنع نافذة الوقت ويجعل الحماية بنيويّة.
- لا يظهر أيّ معرّف داخليّ لا يخصّ المستخدم في أيّ استجابة أو رسالة خطأ.
- التدقيق يُسجَّل بعد نجاح الكتابة لا قبلها، ولا يحمل أسرارًا.

### 7-6. ما لا يتغيّر أمنيًّا (ضمانة انحدار صفريّ)

`IScopeResolver`، `IClientProjectAccess`، `Roles.cs`، `Policies.cs`، `Program.cs` (تسجيل السياسات)، وكلّ مسارات `ProjectsController` القائمة — **صفر تعديل**. يُثبَت بـ`git diff` عند التسليم ويُدرَج كمعيار قبول (§15).

---

## 8. تصميم الواجهة (UI/UX) — لوحة أوّلًا (D-05)

### 8-1. القرار البصريّ الحاكم

القرار **D-05** يغيّر طبيعة الشاشة لا محتواها: `ProjectDetailPage` لم تعد «صفحة تفاصيل بتبويبات»، بل **لوحة مشروع (Project 360 Dashboard)** يُرى فيها وضع المشروع كاملًا **قبل** لمس أيّ تبويب. التبويبات تبقى موجودة لكنّها صارت **طبقة التعمّق** لا طبقة الاكتشاف.

هذا يُترجَم إلى قاعدة تنفيذيّة واحدة:

> عند فتح المشروع يُنفَّذ **نداء واحد** `GET /api/projects/{id}/overview` يملأ كامل اللوحة العلويّة. لا يُنفَّذ أيّ نداء تبويب إلّا عند النقر عليه فعليًّا (`enabled: tab === '…'` في TanStack Query).

**لماذا نداء واحد لا تسعة**: تسعة نداءات متوازية عند الفتح = تسعة اتّصالات + تسعة فحوص رؤية + تسعة مسارات تدقيق محتملة، وهو بالضبط ما تسبّب في بطء الشاشات الأخرى. النداء الواحد يُجمِّع خادميًّا بـ`AsNoTracking()` + تجميع على مستوى قاعدة البيانات (§6-2)، ويُعَدّ **صفر N+1** معيار قبول (§15).

### 8-2. بنية الشاشة من أعلى إلى أسفل

```
┌──────────────────────────────────────────────────────────────────────┐
│ 1) ترويسة المشروع                                                     │
│    الاسم · العميل (رابط إلى Client 360) · النوع · الحالة · التواريخ    │
│    مدير العميل · قائد الفريق · شارة الصحّة (أخضر/أصفر/أحمر + %)        │
├──────────────────────────────────────────────────────────────────────┤
│ 2) شريط النظرة التنفيذيّة — أربع بطاقات إحصائيّة                       │
│    [تقدّم التنفيذ %] [تحقيق المؤشّرات %] [الالتزام بالجدول] [الصحّة %] │
├──────────────────────────────────────────────────────────────────────┤
│ 3) شبكة اللوحة (بطاقات ملخَّص — كلّ بطاقة تفتح تبويبها)                │
│    ┌────────────┬────────────┬────────────┐                          │
│    │ الأهداف     │ المؤشّرات   │ المخرَجات   │                          │
│    │ n إجمالي    │ n إجمالي    │ n إجمالي    │                          │
│    │ n متعثّر     │ متوسّط %    │ n متأخّر    │                          │
│    ├────────────┼────────────┼────────────┤                          │
│    │ المخاطر     │ القرارات    │ الملاحظات   │                          │
│    │ n مفتوح     │ n معلَّق     │ n حديث      │                          │
│    └────────────┴────────────┴────────────┘                          │
├──────────────────────────────────────────────────────────────────────┤
│ 4) عمودان: [ملخّص الاستراتيجيّة]        [النشاط الأخير — 10 عناصر]     │
├──────────────────────────────────────────────────────────────────────┤
│ 5) شريط التبويبات (طبقة التعمّق)                                       │
│  النظرة التنفيذيّة | الاستراتيجيّة | البريف | الأهداف | المؤشّرات |     │
│  المخرَجات | المخاطر | القرارات | الملاحظات | الصحّة                     │
└──────────────────────────────────────────────────────────────────────┘
```

**تبويب «المستندات» غائب تمامًا في CPW-R3** تنفيذًا لـD-01: لا عنصر تبويب معطَّل، ولا بطاقة «قريبًا»، ولا مسار مُعلَّق. الغياب الكامل أنظف من الحضور المعطَّل، ويجعل إعادة الإدخال لاحقًا (`CPW-R3-DOCS-WIRING-R1`) إضافةً خالصة.

### 8-3. نمط حالة التبويب (مطابق للنمط القائم في المستودع)

يُعاد استعمال النمط نفسه المستقرّ في `ClientDetailPage`:

```tsx
type ProjectTabKey =
  | 'overview' | 'strategy' | 'brief' | 'objectives' | 'kpis'
  | 'deliverables' | 'risks' | 'decisions' | 'notes' | 'health';

const [tab, setTab] = useState<ProjectTabKey>('overview');
```

- التبويب الافتراضيّ `overview` دائمًا.
- بيانات كلّ تبويب تُجلَب **كسولًا** (`enabled: tab === 'objectives'` …) ⟶ فتح المشروع لا يُحمِّل ما لا يُرى.
- بعد كلّ كتابة يُبطَل مفتاحان فقط: مفتاح التبويب المعنيّ **ومفتاح `overview`** (لأنّ العدّادات والصحّة تتغيّر) — لا إبطال شامل.
- التبويبات كلّها داخل `ProjectDetailPage` — **لا شاشة جديدة ولا مسار توجيه جديد** (شرط التذكرة الأصليّ).

### 8-4. التبويبات العشرة — المحتوى والصلاحيّة

| # | التبويب | المحتوى | من يكتب |
|---|---|---|---|
| 1 | النظرة التنفيذيّة | نسخة موسَّعة من اللوحة + مقارنة الأهداف بالمؤشّرات | قراءة فقط |
| 2 | الاستراتيجيّة | 11 حقلًا أساسيًّا + الحقول المشروطة بنوع المشروع (§5-3) | إدارة فقط |
| 3 | البريف | الخلفيّة · سياق العمل · النطاق · خارج النطاق · تعريف النجاح | إدارة فقط |
| 4 | الأهداف | قائمة الأهداف + وزن + حالة + مؤشّراتها المطويّة تحته | إدارة (بنيويّ) / TL+AM (الحالة) |
| 5 | المؤشّرات | عرض أفقيّ لكلّ المؤشّرات مع هدفها الأب ظاهرًا دائمًا | إدارة (بنيويّ) / TL+AM (القراءات) |
| 6 | المخرَجات | مخرَجات العقد + كميّة مخطَّطة/منجَزة + نسبة + حالة | إدارة (بنيويّ) / TL+AM (التقدّم) |
| 7 | المخاطر | الجدول القائم `risks` بلا تغيير في العقد | كما هو اليوم |
| 8 | القرارات | الجدول القائم `decisions` مفلترًا بـ`ProjectId` | كما هو اليوم |
| 9 | الملاحظات | `ManagementNote` بـ`EntityType=Project` | كما هو اليوم |
| 10 | الصحّة | تفصيل المعادلة: المكوّنات الثلاثة + وزن كلٍّ + النتيجة + زرّ إعادة الاحتساب | إدارة / TL+AM |

### 8-5. القاعدة البصريّة الحاكمة للهرميّة (D-02)

المؤشّر **لا يُعرَض أبدًا بلا هدفه**:

- في تبويب **الأهداف**: كلّ هدف يُظهِر مؤشّراته مطويّةً تحته مباشرةً.
- في تبويب **المؤشّرات**: كلّ صفّ يحمل عمودًا إلزاميًّا «الهدف» غير قابل للإخفاء.
- **زرّ «مؤشّر جديد» غير موجود إطلاقًا خارج سياق هدف**. الإنشاء يبدأ دائمًا من هدف، مطابقًا لشكل الـAPI الذي لا يملك `POST /projects/{id}/kpis` (§6-3).
- إن لم يكن للمشروع أهداف بعد، يعرض تبويب المؤشّرات حالة فارغة نصّها: «أضف هدفًا أوّلًا — كلّ مؤشّر يتبع هدفًا.» مع زرّ ينقل إلى تبويب الأهداف.

هذه ليست تفضيلًا بصريًّا بل **فرضًا للهرميّة على مستوى الواجهة**، بحيث تستحيل حالة «مؤشّر يتيم» حتّى في ذهن المستخدم.

### 8-6. النماذج والحقول المشروطة (D-04)

نموذج الاستراتيجيّة يُبنى ديناميكيًّا من `GET /api/projects/{id}/strategy/schema`:

1. تُعرَض الأحد عشر حقلًا الأساسيّة دائمًا.
2. ثمّ قسم «حقول خاصّة بنوع المشروع» يُبنى من `conditionalFields[]` العائدة من الخادم — **الواجهة لا تحمل أيّ خريطة أنواع مُصلَّبة**.
3. إضافة نوع مشروع جديد لاحقًا = بذر رموز في الكتالوج + سطر ربط في الخادم ⟶ **صفر تعديل واجهة وصفر هجرة** (§14).

### 8-7. التزامات العرض العربيّ

RTL كامل، خطّ Tajawal، كلّ التسميات والحالات والوحدات عربيّة عبر خرائط في `lib/format.ts` (`projectObjectiveStatusLabel`، `projectKpiCategoryLabel`، `projectKpiUnitLabel`، `projectKpiFrequencyLabel`، `projectDeliverableStatusLabel`، `projectHealthStatusLabel`) على النمط القائم. الأرقام والنسب بصيغة موحَّدة عبر الدوالّ الموجودة. **لا مكتبة رسوم جديدة**: أشرطة التقدّم والشارات من مكوّنات `ui.tsx` القائمة — صفر اعتماديّة جديدة في `package.json`.

---

## 9. تصميم قاعدة البيانات

### 9-1. الاتّفاقيّات المُلزِمة (مطابقة للمستودع الحاليّ)

| البند | القاعدة |
|---|---|
| اسم الجدول | snake_case جمع (`project_objectives`) |
| اسم العمود | PascalCase مقتبَس (`"ProjectId"`) |
| المفتاح | `uuid` من `BaseEntity` |
| التعدادات | `.HasConversion<string>()` + `HasMaxLength(20)` ⟶ `varchar(20)` |
| التواريخ الزمنيّة | `timestamptz` |
| التواريخ اليوميّة | `date` (لـ`DateOnly`) |
| الأرقام العشريّة | `numeric(5,2)` للنِّسَب، `numeric(18,2)` للقيم |
| النصوص | `varchar(n)` بحدّ صريح دائمًا — لا `text` مفتوح |

### 9-2. توسعة `projects` + عمود `decisions`

```sql
ALTER TABLE projects
  ADD "Summary"             varchar(1000) NULL,
  ADD "ProjectOwnerId"      uuid          NULL,
  ADD "TeamLeaderId"        uuid          NULL,
  ADD "ProgressPercent"     numeric(5,2)  NOT NULL DEFAULT 0,
  ADD "Background"          varchar(4000) NULL,
  ADD "BusinessContext"     varchar(4000) NULL,
  ADD "ScopeText"           varchar(4000) NULL,
  ADD "OutOfScope"          varchar(4000) NULL,
  ADD "SuccessDefinition"   varchar(2000) NULL,
  ADD "HealthStatus"        varchar(20)   NOT NULL DEFAULT 'Green',
  ADD "HealthPercent"       numeric(5,2)  NOT NULL DEFAULT 0,
  ADD "HealthComputedAtUtc" timestamptz   NULL;

ALTER TABLE decisions ADD "ProjectId" uuid NULL;
CREATE INDEX "IX_decisions_ProjectId" ON decisions ("ProjectId");
CREATE INDEX "IX_projects_TeamLeaderId" ON projects ("TeamLeaderId");
CREATE INDEX "IX_projects_HealthStatus"  ON projects ("HealthStatus");
```

كلّ الأعمدة غير الاختياريّة لها **قيمة افتراضيّة** ⟶ الصفوف القائمة تُقرأ فورًا بلا Backfill (المشاريع القائمة تبدأ `Green / 0%` وهو التمثيل الصادق لمشروع لم تُدخَل بياناته بعد). `ProjectOwnerId`/`TeamLeaderId` **بلا مفتاح أجنبيّ** إلى `AspNetUsers` اتّساقًا مع نمط `AccountManagerId` القائم في نفس الجدول — تجنّبًا لتقييد حذف المستخدم.

### 9-3. `project_objectives`

```sql
CREATE TABLE project_objectives (
  "Id"          uuid          PRIMARY KEY,
  "ProjectId"   uuid          NOT NULL REFERENCES projects("Id") ON DELETE CASCADE,
  "WorkstreamId" uuid         NULL     REFERENCES workstreams("Id") ON DELETE SET NULL,
  "Name"        varchar(300)  NOT NULL,
  "Description" varchar(2000) NULL,
  "Priority"    varchar(20)   NOT NULL DEFAULT 'Medium',
  "Weight"      numeric(5,2)  NOT NULL DEFAULT 0,
  "Status"      varchar(20)   NOT NULL DEFAULT 'NotStarted',
  "StartDate"   date          NULL,
  "DueDate"     date          NULL,
  "OwnerUserId" uuid          NULL,
  "Notes"       varchar(2000) NULL,
  "SortOrder"   integer       NOT NULL DEFAULT 0,
  "IsActive"    boolean       NOT NULL DEFAULT true,
  "CreatedAtUtc" timestamptz  NOT NULL,
  "UpdatedAtUtc" timestamptz  NULL
);
CREATE INDEX "IX_project_objectives_ProjectId_SortOrder" ON project_objectives ("ProjectId","SortOrder");
CREATE INDEX "IX_project_objectives_ProjectId_Status"    ON project_objectives ("ProjectId","Status");
CREATE INDEX "IX_project_objectives_WorkstreamId"        ON project_objectives ("WorkstreamId");
```

### 9-4. `project_kpis` + `project_kpi_readings` (تنفيذ D-02 على مستوى السكيمة)

```sql
CREATE TABLE project_kpis (
  "Id"                 uuid          PRIMARY KEY,
  "ProjectId"          uuid          NOT NULL REFERENCES projects("Id")           ON DELETE CASCADE,
  "ObjectiveId"        uuid          NOT NULL REFERENCES project_objectives("Id") ON DELETE CASCADE,
  "Name"               varchar(300)  NOT NULL,
  "Description"        varchar(2000) NULL,
  "Category"           varchar(20)   NOT NULL,
  "Unit"               varchar(20)   NOT NULL,
  "CustomUnitLabel"    varchar(50)   NULL,
  "Direction"          varchar(20)   NOT NULL DEFAULT 'HigherIsBetter',
  "Frequency"          varchar(20)   NOT NULL DEFAULT 'Monthly',
  "BaselineValue"      numeric(18,2) NULL,
  "TargetValue"        numeric(18,2) NOT NULL,
  "CurrentValue"       numeric(18,2) NULL,
  "LastReadingDate"    date          NULL,
  "Weight"             numeric(5,2)  NOT NULL DEFAULT 0,
  "SourceType"         varchar(20)   NOT NULL DEFAULT 'Manual',
  "ExternalSourceKey"  varchar(100)  NULL,
  "ExternalMetricCode" varchar(100)  NULL,
  "LastSyncedAtUtc"    timestamptz   NULL,
  "Notes"              varchar(2000) NULL,
  "SortOrder"          integer       NOT NULL DEFAULT 0,
  "IsActive"           boolean       NOT NULL DEFAULT true,
  "CreatedAtUtc"       timestamptz   NOT NULL,
  "UpdatedAtUtc"       timestamptz   NULL
);
CREATE INDEX "IX_project_kpis_ProjectId_SortOrder" ON project_kpis ("ProjectId","SortOrder");
CREATE INDEX "IX_project_kpis_ProjectId_Category"  ON project_kpis ("ProjectId","Category");
CREATE INDEX "IX_project_kpis_ObjectiveId"         ON project_kpis ("ObjectiveId");

CREATE TABLE project_kpi_readings (
  "Id"                  uuid          PRIMARY KEY,
  "ProjectKpiId"        uuid          NOT NULL REFERENCES project_kpis("Id") ON DELETE CASCADE,
  "ReadingDate"         date          NOT NULL,
  "Value"               numeric(18,2) NOT NULL,
  "TargetSnapshot"      numeric(18,2) NULL,
  "AchievementSnapshot" numeric(5,2)  NULL,
  "RecordedByUserId"    uuid          NOT NULL,
  "Notes"               varchar(1000) NULL,
  "CreatedAtUtc"        timestamptz   NOT NULL,
  "UpdatedAtUtc"        timestamptz   NULL
);
CREATE UNIQUE INDEX "IX_project_kpi_readings_ProjectKpiId_ReadingDate"
  ON project_kpi_readings ("ProjectKpiId","ReadingDate");
CREATE INDEX "IX_project_kpi_readings_ProjectKpiId_ReadingDate_Desc"
  ON project_kpi_readings ("ProjectKpiId","ReadingDate" DESC);
```

**`"ObjectiveId" uuid NOT NULL` هو التجسيد الحرفيّ لـD-02**: «مؤشّر بلا هدف» تصبح حالة **مستحيلة على مستوى قاعدة البيانات**، لا مجرّد ممنوعة بالخدمة. والفهرس الفريد `(ProjectKpiId, ReadingDate)` يجعل ازدواج قراءة اليوم الواحد مستحيلًا كذلك، ويُترجَم `23505` إلى `project_kpi_reading.duplicate_date.conflict` (409).

### 9-5. `project_deliverables` (D-03)

```sql
CREATE TABLE project_deliverables (
  "Id"                   uuid          PRIMARY KEY,
  "ProjectId"            uuid          NOT NULL REFERENCES projects("Id")           ON DELETE CASCADE,
  "ObjectiveId"          uuid          NULL     REFERENCES project_objectives("Id") ON DELETE SET NULL,
  "WorkstreamId"         uuid          NULL     REFERENCES workstreams("Id")        ON DELETE SET NULL,
  "DeliverableTypeCode"  varchar(60)   NOT NULL,
  "Name"                 varchar(300)  NOT NULL,
  "Description"          varchar(2000) NULL,
  "PlannedQuantity"      integer       NOT NULL DEFAULT 1,
  "CompletedQuantity"    integer       NOT NULL DEFAULT 0,
  "Status"               varchar(20)   NOT NULL DEFAULT 'NotStarted',
  "ProgressPercent"      numeric(5,2)  NOT NULL DEFAULT 0,
  "StartDate"            date          NULL,
  "DueDate"              date          NULL,
  "DeliveredAtUtc"       timestamptz   NULL,
  "Priority"             varchar(20)   NOT NULL DEFAULT 'Medium',
  "OwnerUserId"          uuid          NULL,
  "Notes"                varchar(2000) NULL,
  "SortOrder"            integer       NOT NULL DEFAULT 0,
  "IsActive"             boolean       NOT NULL DEFAULT true,
  "CreatedAtUtc"         timestamptz   NOT NULL,
  "UpdatedAtUtc"         timestamptz   NULL
);
CREATE INDEX "IX_project_deliverables_ProjectId_SortOrder" ON project_deliverables ("ProjectId","SortOrder");
CREATE INDEX "IX_project_deliverables_ProjectId_Status"    ON project_deliverables ("ProjectId","Status");
CREATE INDEX "IX_project_deliverables_ObjectiveId"         ON project_deliverables ("ObjectiveId");
CREATE INDEX "IX_project_deliverables_DeliverableTypeCode" ON project_deliverables ("DeliverableTypeCode");
```

`ObjectiveId` هنا **اختياريّ** عمدًا: D-02 قيّد المؤشّرات بالأهداف ولم يقيّد المخرَجات؛ ومخرَج العقد قد يكون التزامًا تعاقديًّا لا يخدم هدفًا تسويقيًّا بعينه (تقرير شهريّ مثلًا). الربط متاح لمن أراد، وغير مفروض.

### 9-6. `project_strategies` + `project_strategy_attributes` (D-04)

```sql
CREATE TABLE project_strategies (
  "Id"                uuid          PRIMARY KEY,
  "ProjectId"         uuid          NOT NULL REFERENCES projects("Id") ON DELETE CASCADE,
  "Vision"            varchar(4000) NULL,
  "StrategySummary"   varchar(4000) NULL,
  "TargetAudience"    varchar(4000) NULL,
  "CustomerPersona"   varchar(4000) NULL,
  "Positioning"       varchar(2000) NULL,
  "ValueProposition"  varchar(2000) NULL,
  "Competitors"       varchar(4000) NULL,
  "ToneOfVoice"       varchar(2000) NULL,
  "Messaging"         varchar(4000) NULL,
  "MarketingApproach" varchar(4000) NULL,
  "SuccessFactors"    varchar(4000) NULL,
  "IsActive"          boolean       NOT NULL DEFAULT true,
  "CreatedAtUtc"      timestamptz   NOT NULL,
  "UpdatedAtUtc"      timestamptz   NULL
);
CREATE UNIQUE INDEX "IX_project_strategies_ProjectId" ON project_strategies ("ProjectId");

CREATE TABLE project_strategy_attributes (
  "Id"                uuid          PRIMARY KEY,
  "ProjectStrategyId" uuid          NOT NULL REFERENCES project_strategies("Id") ON DELETE CASCADE,
  "FieldCode"         varchar(60)   NOT NULL,
  "ValueText"         varchar(4000) NULL,
  "SortOrder"         integer       NOT NULL DEFAULT 0,
  "CreatedAtUtc"      timestamptz   NOT NULL,
  "UpdatedAtUtc"      timestamptz   NULL
);
CREATE UNIQUE INDEX "IX_project_strategy_attributes_StrategyId_FieldCode"
  ON project_strategy_attributes ("ProjectStrategyId","FieldCode");
```

الفهرس الفريد `(ProjectStrategyId, FieldCode)` هو سبب إلزاميّة **المزامنة التفاضليّة** في `PUT /strategy` (§6-5): الحذف ثمّ الإدراج لنفس المفتاح داخل معاملة واحدة هو بالضبط النمط الذي أنتج `DbUpdateConcurrencyException` في `CPWR2-DEF-01`. القاعدة المستفادة تُطبَّق هنا **قبل** وقوع العيب لا بعده.

### 9-7. مجالا الكتالوج الجديدان

يُضافان إلى `KnownDomains` في `ExecutionTaxonomy` ويُبذَران بالبذّار الموجود (idempotent على `(Domain, Code)`) — **بلا جدول جديد وبلا تعديل بنية الكتالوج**:

| المجال | عدد الرموز | الرموز |
|---|---|---|
| `contract_deliverable` | 18 | `monthly_content_plan, monthly_calendar, posts_package, reels_package, stories_package, monthly_report` · `keyword_research_doc, technical_audit, monthly_seo_report, onpage_optimization` · `brand_strategy, brand_identity, logo, brand_guidelines` · `campaign_structure, creatives_package, landing_page_delivery, performance_report` |
| `strategy_field` | 14 | `seo.keywords, seo.search_intent, seo.priority_pages, seo.competitors` · `ads.campaign_goal, ads.budget, ads.target_audience, ads.channels, ads.offer, ads.conversion_goal` · `social.content_pillars, social.publishing_frequency, social.brand_voice, social.platforms` |

`landing_page_delivery` اختير بدل `landing_page` تفاديًا للاصطدام برمز قائم في مجال `deliverable`.

### 9-8. محرّك الاحتساب — ثوابت مركزيّة (`ProjectHealthPolicy`)

كلّ ثابت أدناه يعيش في **صنف واحد** `Reporting.Application.Projects360.ProjectHealthPolicy` — لا رقم سحريّ متناثر، ولا منطق مكرَّر بين الخدمة والواجهة (الواجهة تعرض ما يحسبه الخادم فقط).

**(أ) تحقيق المؤشّر**

```
HigherIsBetter : achievement = (Current / Target) × 100
LowerIsBetter  : achievement = (Target  / Current) × 100
achievement    = Clamp(achievement, 0, 200)
Target ≤ 0  أو  Current = null   ⟶  achievement = null   (لا صفر — «غير محتسَب» ≠ «فشل»)
variance       = achievement − 100
```

**(ب) الاتّجاه** — من آخر قراءتين فقط، بعتبة `ε = ±2%` تمنع الضجيج:

```
Δ = achievement(آخر) − achievement(ما قبلها)
Δ > +2 ⟶ Up   |   Δ < −2 ⟶ Down   |   غير ذلك ⟶ Flat
أقلّ من قراءتين ⟶ Unknown
```

**(ج) نتيجة المؤشّرات على مستوى المشروع** — موزونة، مع تجاهل غير المحتسَب:

```
kpiScore = Σ(achievement_i × w_i) / Σ(w_i)      لكلّ مؤشّر نشط achievement_i ≠ null
كلّ الأوزان صفر ⟶ أوزان متساوية
لا مؤشّر محتسَب ⟶ kpiScore = null (يُستبعَد المكوّن ويُعاد توزيع وزنه)
```

**(د) نتيجة الجدول الزمنيّ**

```
expected      = (today − StartDate) / (EndDate − StartDate) × 100     (مقصوصة 0..100)
gap           = ProgressPercent − expected
gap ≥ 0        ⟶ scheduleScore = 100
−10 ≤ gap < 0  ⟶ 75
−25 ≤ gap < −10⟶ 50
gap < −25      ⟶ 25
تواريخ ناقصة  ⟶ scheduleScore = null (يُستبعَد المكوّن)
```

**(هـ) الصحّة النهائيّة**

```
HealthPercent = 0.50 × kpiScore + 0.30 × ProgressPercent + 0.20 × scheduleScore
HealthPercent ≥ 80 ⟶ Green   |   ≥ 55 ⟶ Yellow   |   غير ذلك ⟶ Red
```

عند غياب مكوّن (`null`) تُعاد تسوية الأوزان على المكوّنات المتاحة بحيث يبقى المجموع 1.00 — مشروع بلا مؤشّرات بعد يُقاس بتقدّمه وجدوله لا يُعاقَب بصفر.

**متى يُعاد الاحتساب**: داخل نفس المعاملة لكلّ كتابة تؤثّر فعليًّا — قراءة مؤشّر (إنشاء/تعديل/حذف)، تعديل هدف/وزن/حالة، تقدّم مخرَج، تقدّم المشروع، تعديل تواريخه — بالإضافة إلى استدعاء صريح `POST /health/recompute`. **لا مهمّة خلفيّة، لا مجدول، لا Backfill** — أيّ منها يخالف حظر «محرّك تنفيذ» في التذكرة.

---

## 10. خطّة الهجرة

### 10-1. هجرة واحدة فقط

| البند | القيمة |
|---|---|
| الاسم | `AddProject360Foundation` |
| الأساس | `20260713171040_AdminGovernanceReportKpiCorrection` (الهجرة **الثانية والثلاثون** على `develop`) |
| الترتيب | الهجرة **الثالثة والثلاثون** |
| `Up` | `CreateTable` × **6** + `AddColumn` × **12** على `projects` + `AddColumn` × **1** على `decisions` + `CreateIndex` × 14 |
| `Down` | `DropTable` × 6 + `DropColumn` × 13 |

الجداول الستّة بترتيب الإنشاء (احترامًا للمفاتيح الأجنبيّة): `project_objectives` ⟶ `project_kpis` ⟶ `project_kpi_readings` ⟶ `project_deliverables` ⟶ `project_strategies` ⟶ `project_strategy_attributes`.

### 10-2. إثبات «إضافيّ بحت» (Additive-Only)

| المعيار | الحالة |
|---|---|
| `DropTable` على جدول قائم | **صفر** |
| `DropColumn` على عمود قائم | **صفر** |
| `AlterColumn` على عمود قائم | **صفر** |
| `RenameColumn` / `RenameTable` | **صفر** |
| عمود جديد `NOT NULL` بلا `defaultValue` | **صفر** (كلّها إمّا `NULL` أو بقيمة افتراضيّة) |
| Backfill / سكربت بيانات | **صفر** |
| مساس بجداول `workstream*` | **صفر** (تُذكَر `workstreams` كـ`principalTable` لمفتاح أجنبيّ فقط) |
| مساس بجداول `client_documents*` | **غير موجودة أصلًا على `develop`** (D-01) |

⟹ **التراجع بلا فقد بيانات قائمة**: `Down` يحذف ما أنشأته الهجرة حصرًا. صفوف `projects` و`decisions` تعود إلى شكلها السابق تمامًا؛ البيانات المفقودة هي بيانات CPW-R3 وحدها.

### 10-3. البوّابات الإلزاميّة قبل التسليم

1. `dotnet ef migrations has-pending-model-changes` ⟵ يجب أن يُخرِج **`No changes`**.
2. مراجعة يدويّة لملفّ الهجرة المُولَّد للتأكّد من غياب أيّ `Alter/Drop/Rename` على كيان قائم.
3. `dotnet ef migrations script <previous> <new>` ومراجعة SQL الناتج سطرًا سطرًا.
4. تطبيق ثمّ تراجع ثمّ إعادة تطبيق على قاعدة معزولة نظيفة (`Up → Down → Up`) بلا خطأ.
5. تشغيل الانحدار الكامل على قاعدة معزولة نظيفة بعد التطبيق (§16).

### 10-4. سؤال §9-ب في R1 — **محسوم**

كان تقرير R1 يترك مفتوحًا سؤال «هل تُبنى CPW-R3 فوق `develop` أم فوق فرع صلاحيّات المستندات `3344f78`؟». القرار **D-01** حسمه نهائيًّا لصالح **`develop`**، ومن ثمّ:

- سلسلة الهجرات خطّيّة بلا تفرّع ولا مخاطرة تصادم معرّفات.
- لا اعتماديّة على هجرة `20260809165617_ClientDocumentVisibility` غير المدموجة.
- عند دمج فرع المستندات لاحقًا، تتجاور الهجرتان بلا تعارض لأنّ **مجموعتَي الجداول منفصلتان تمامًا** (`client_document*` مقابل `project_*`).

**هذا القرار مجمَّد**: لا يُعاد فتحه أثناء التنفيذ مهما بدا مغريًا (شرط D-01 الحرفيّ).

---

## 11. تحليل المخاطر (إعادة تقييم R2)

| # | الخطر | الاحتمال | الأثر | التخفيف | التغيّر عن R1 |
|---|---|---|---|---|---|
| R1 | تضخّم النطاق نحو إدارة المهامّ | متوسّط | عالٍ | `Deliverable` كيان **تعاقديّ** بلا إسناد/حالة سير عمل؛ لا `Task` في السكيمة | ثابت |
| R2 | التباس `ProjectDeliverable` مع `WorkstreamDeliverable` | عالٍ | متوسّط | حدود قاطعة (§3-3) + ضمانة `git diff` صفريّة | ثابت |
| R3 | N+1 في `/overview` | متوسّط | عالٍ | تجميع على مستوى القاعدة + `AsNoTracking()` + معيار قبول صريح | ثابت |
| R4 | انحراف الصحّة المخزَّنة عن الواقع | متوسّط | متوسّط | إعادة احتساب حتميّة داخل كلّ معاملة مؤثّرة + `POST /health/recompute` | ثابت |
| R5 | ثقل نموذج الاستراتيجيّة على المستخدم | متوسّط | منخفض | كلّ الحقول اختياريّة + الحقول المشروطة تُبنى من الخادم | جديد التفصيل |
| R6 | تسرّب رؤية عبر المسارات المتداخلة | منخفض | **حرج** | 404 قبل 403 + تحميل مقيَّد بالأب في استعلام واحد (§7-5) | ثابت |
| R7 | كسر عقد `UpdateProjectRequest` القائم | منخفض | عالٍ | `PUT /brief` و`PATCH /progress` منفصلان تمامًا | ثابت |
| R8 | تعارض بذّار الكتالوج مع رموز قائمة | منخفض | متوسّط | بذّار idempotent على `(Domain, Code)` + `landing_page_delivery` بدل `landing_page` | ثابت |
| R9 | تصادم هجرات مع فرع المستندات | ~~عالٍ~~ **منخفض** | متوسّط | **خُفِّض بـD-01**: البناء فوق `develop` وحده، ومجموعتا الجداول منفصلتان | **مُخفَّض** |
| R10 | اعتماديّة تبويب المستندات على فرع غير مدموج | ~~عالٍ~~ **مُلغى** | — | **أُلغي بـD-01**: التبويب مؤجَّل بالكامل إلى `CPW-R3-DOCS-WIRING-R1` | **مُلغى** |
| R11 | سوء فهم «يدويّ» كأنّه نقص دائم | متوسّط | منخفض | خارطة تطوّر المؤشّر موثَّقة (§13) + `SourceType` مُهيّأ من اليوم | ثابت |
| R12 | ضخامة الباتش تعيق المراجعة | عالٍ | متوسّط | تنفيذ على مراحل ذرّيّة (§17)، كلّ مرحلة قابلة للبناء والاختبار وحدها | ثابت |
| **R13** | **تعديل عرضيّ لملفّات `Workstream*`** | منخفض | عالٍ | `git diff --stat` على `*Workstream*` يجب أن يكون **فارغًا** — معيار قبول (§15) | **جديد** |
| **R14** | **`ObjectiveId` الإلزاميّ يكسر ترتيب إنشاء موجود** | متوسّط | متوسّط | لا يوجد مسار إنشاء مؤشّر مشروع اليوم أصلًا (ميزة جديدة كلّيًّا) ⟹ لا كسر؛ والواجهة تفرض «هدف أوّلًا» (§8-5) | **جديد** |
| **R15** | **انحراف رموز `strategy_field` بين الكتالوج والواجهة** | منخفض | متوسّط | الواجهة **لا تحمل خريطة أنواع**؛ تُبنى من `GET /strategy/schema` حصرًا + تحقّق خادميّ على `FieldCode` | **جديد** |

---

## 12. تكامل قائد الفريق وسلسلة اللوحات (D-07)

### 12-1. مسؤوليّات قائد الفريق داخل CPW-R3

| المسؤوليّة | المكان | الآليّة |
|---|---|---|
| تقدّم الأهداف | تبويب الأهداف | `PATCH …/objectives/{id}/status` |
| تقدّم المؤشّرات | تبويب المؤشّرات | `POST …/kpis/{id}/readings` |
| **تقدّم المخرَجات** | تبويب المخرَجات | `PATCH …/deliverables/{id}/progress` |
| صحّة المشروع | تبويب الصحّة | تُحتسَب تلقائيًّا + `POST …/health/recompute` |

هذه الأربع هي **حقل الكتابة الكامل** لقائد الفريق ومدير العميل. كلّ ما عداها (إنشاء/حذف/تعديل بنيويّ) محكوم بـ`ManagementOnly` (§7-3).

### 12-2. سلسلة التغذية: مشروع ⟵ فريق ⟵ إدارة ⟵ تنفيذيّ ⟵ شركة

```
قراءة مؤشّر / تقدّم مخرَج  (قائد الفريق)
        ↓  إعادة احتساب حتميّة داخل المعاملة
Project.HealthPercent + Project.HealthStatus + Project.ProgressPercent
        ↓  عمود مخزَّن قابل للتجميع والفرز (سبب تخزينه — §5-2)
لوحة الفريق (مشاريع قائد الفريق)
        ↓
لوحة الإدارة (مشاريع الإدارة)
        ↓
اللوحة التنفيذيّة (كلّ المشاريع)
        ↓
لوحة الشركة
```

**قرار حاسم لتفادي تضخّم النطاق**: CPW-R3 **تُنتِج** الإشارة (`HealthStatus` / `HealthPercent` / `ProgressPercent` مخزَّنة ومفهرَسة) ولا **تبني** اللوحات الأربع. بناؤها تذكرة مستقلّة لاحقة تستهلك أعمدةً جاهزة بـ`GROUP BY` بسيط — بلا هجرة وبلا إعادة تصميم. هذا هو المعنى العمليّ لـ«التغذية» في D-07: توفير المصدر، لا بناء المستهلِك.

### 12-3. لماذا لا مسار موافقة على القراءات

لا سير عمل اعتماد لقراءة المؤشّر في CPW-R3 (يخالف حظر «Workflow Engine»). الضبط يتحقّق بثلاث طبقات موجودة: `RecordedByUserId` على كلّ قراءة، سجلّ تدقيق لكلّ كتابة، وحصر الكتابة بقائد فريق المشروع أو مدير عميله حصرًا.

---

## 13. خارطة تطوّر المؤشّر (D-06)

### 13-1. المراحل الثلاث

| المرحلة | `SourceType` | مصدر `CurrentValue` | مَن يُدخِل | حالة CPW-R3 |
|---|---|---|---|---|
| **1 — يدويّ** | `Manual` | `project_kpi_readings` | قائد الفريق **أو مدير العميل** | **مُنفَّذ** |
| **2 — مشتقّ من المهامّ** | `TaskDerived` | تجميع من طبقة المهامّ المستقبليّة | النظام | مُهيَّأ فقط |
| **3 — تكاملات خارجيّة** | `Integration` | CRM · GA4 · Meta · Google Ads · Search Console · Email | مزامنة خارجيّة | مُهيَّأ فقط |

### 13-2. الضمانة الجوهريّة: **المؤشّر لا يتغيّر — مصدر بياناته فقط**

صفّ `project_kpis` يحمل الهويّة الثابتة (الاسم، الفئة، الوحدة، الاتّجاه، التواتر، خطّ الأساس، **الهدف**، الوزن، الهدف الأب). لا شيء من ذلك يتعلّق بمن يملأ `CurrentValue`. لذلك:

- الانتقال من مرحلة إلى أخرى = **تحديث عمود `SourceType` لصفّ قائم**. لا صفّ جديد، لا هجرة، لا فقد تاريخ.
- تاريخ القراءات اليدويّة **يبقى** بعد التحوّل — الاستمراريّة التاريخيّة محفوظة بالتصميم.
- الأعمدة الثلاثة `ExternalSourceKey` / `ExternalMetricCode` / `LastSyncedAtUtc` **مُنشأة اليوم وفارغة** حتّى لا تحتاج المرحلتان 2 و3 إلى أيّ `AddColumn` لاحق.

### 13-3. الحارس الذي يجعل الوعد قابلًا للفرض

`project_kpi.source_not_manual.conflict` (409): ما إن يصبح `SourceType ≠ Manual` حتّى تُرفَض القراءة اليدويّة. هذا يمنع اختلاط مصدرين للحقيقة في نفس المؤشّر — وهو الفشل النمطيّ الذي يُفسِد أنظمة المؤشّرات عند أوّل تكامل.

**ما لا تفعله CPW-R3**: لا عميل HTTP، لا مفاتيح API، لا مزامنة، لا مجدول، لا جدول تكاملات. المرحلتان 2 و3 **موصوفتان لا مُنفَّذتان** — التنفيذ يخالف حظر «Integration» الصريح.

---

## 14. إثبات التوسّع المستقبليّ (D-08)

### 14-1. الهرميّة النهائيّة المستهدَفة

```
Client
 └── Project
      ├── Strategy
      ├── Objectives
      │    └── KPIs            ← إلزاميّ (D-02)
      ├── Deliverables
      │    └── Tasks           ← مستقبل
      │         └── Subtasks   ← مستقبل
      ├── Risks · Decisions · Notes
      ├── Documents · External Links   ← مؤجَّل (D-01)
      └── Health · Dashboard
```

### 14-2. إثبات «صفر إعادة هيكلة» لكلّ توسعة مستقبليّة

| التوسعة المستقبليّة | ما تحتاجه | ما **لا** تحتاجه |
|---|---|---|
| `Tasks` تحت المخرَج | جدول `project_tasks` بـ`ProjectDeliverableId` (ورقة جديدة) | صفر تعديل على `project_deliverables` |
| `Subtasks` | `ParentTaskId` داخل نفس جدول المهامّ | صفر تعديل على أيّ جدول من الستّة |
| تبويب المستندات | ربط بـ`client_documents` بعد الدمج | صفر تعديل سكيمة (`CPW-R3-DOCS-WIRING-R1`) |
| نوع مشروع جديد | بذر رموز `strategy_field` + سطر ربط خادميّ | صفر هجرة، صفر تعديل واجهة (§8-6) |
| مخرَج تعاقديّ جديد | بذر رمز في `contract_deliverable` | صفر هجرة |
| مؤشّر من تكامل خارجيّ | `UPDATE "SourceType"` على صفّ قائم | صفر هجرة، صفر فقد تاريخ |
| لوحات الفريق/الإدارة/التنفيذيّ | `GROUP BY` على أعمدة مخزَّنة مفهرَسة | صفر هجرة، صفر إعادة احتساب |

**السبب البنيويّ الواحد** الذي يجعل الجدول أعلاه ممكنًا: كلّ ما أضافته CPW-R3 هو **أوراق وفروع جديدة على شجرة قائمة**، ولم تُعدَّل أيّ عقدة قائمة سوى بإضافة أعمدة اختياريّة أو ذات قيمة افتراضيّة. الشجرة تنمو ولا تُعاد زراعتها.

---

## 15. معايير القبول

### 15-1. معايير حاكمة (فشل أيّ منها = **NO-GO**)

| # | المعيار | كيف يُثبَت |
|---|---|---|
| A-01 | هجرة **واحدة** إضافيّة بحتة | مراجعة الملفّ + `migrations script` |
| A-02 | `has-pending-model-changes` = `No changes` | مخرَج الأمر حرفيًّا |
| A-03 | **صفر تعديل** على أيّ ملفّ `*Workstream*` | `git diff --stat -- '*Workstream*'` فارغ |
| A-04 | **صفر تعديل** على `IScopeResolver` / `IClientProjectAccess` / `Roles.cs` / `Policies.cs` | `git diff` فارغ لهذه الملفّات |
| A-05 | **صفر دور جديد وصفر سياسة جديدة** | `git diff` على `Roles.cs`/`Program.cs` |
| A-06 | **صفر تعديل** على مسارات `ProjectsController` القائمة وعقودها | مراجعة `git diff` + اختبارات الانحدار |
| A-07 | **صفر ارتباط** بفرع صلاحيّات المستندات `3344f78` | لا مرجع لـ`client_document*` في الباتش |
| A-08 | لا يوجد مسار `POST /api/projects/{id}/kpis` | مراجعة الكنترولر (تنفيذ D-02) |
| A-09 | `project_kpis."ObjectiveId"` = `NOT NULL` | مراجعة الهجرة |
| A-10 | `Up → Down → Up` على قاعدة نظيفة بلا خطأ | تنفيذ فعليّ |

### 15-2. معايير وظيفيّة

| # | المعيار |
|---|---|
| F-01 | فتح المشروع يُنفِّذ **نداء `/overview` واحدًا** ويعرض اللوحة كاملة (D-05) |
| F-02 | `/overview` بلا N+1: عدد استعلامات ثابت لا يتناسب مع عدد الأهداف/المؤشّرات |
| F-03 | العشرة تبويبات تعمل داخل `ProjectDetailPage` بلا شاشة جديدة ولا مسار توجيه جديد |
| F-04 | تبويب المستندات **غائب تمامًا** (لا معطَّل ولا «قريبًا») |
| F-05 | إنشاء مؤشّر بلا هدف **مستحيل** واجهةً وAPIًّا وسكيمةً |
| F-06 | حذف هدف يحمل مؤشّرات ⟶ `project_objective.has_kpis.conflict` (409) |
| F-07 | قراءتان بنفس التاريخ لنفس المؤشّر ⟶ `project_kpi_reading.duplicate_date.conflict` (409) |
| F-08 | حقول الاستراتيجيّة المشروطة تتبدّل بتبدّل `ServiceType` بلا أيّ خريطة في الواجهة |
| F-09 | `PUT /strategy` يزامن السمات **تفاضليًّا** (لا `RemoveRange` + `AddRange`) |
| F-10 | تعديل `DeliverableTypeCode` بعد الإنشاء ⟶ `project_deliverable.type_immutable.conflict` (409) |
| F-11 | الصحّة تُعاد حسابها داخل نفس معاملة كلّ كتابة مؤثّرة |
| F-12 | مشروع بلا مؤشّرات لا يُعطى صفرًا — يُعاد توزيع أوزان المكوّنات المتاحة |

### 15-3. معايير أمنيّة (مصفوفة اختبار إلزاميّة)

| # | السيناريو | المتوقَّع |
|---|---|---|
| S-01 | مستخدم لا يرى المشروع ⟵ أيّ مسار من مسارات CPW-R3 | **404** |
| S-02 | مدير عميل **مشروع آخر** ⟵ قراءة مؤشّر | **404** |
| S-03 | مستخدم يرى المشروع ولا يملكه ⟵ `POST readings` | **403** |
| S-04 | قائد فريق المشروع ⟵ `POST readings` | **200** |
| S-05 | مدير عميل المشروع ⟵ `PATCH deliverables/{id}/progress` | **200** |
| S-06 | قائد الفريق ⟵ `POST objectives` (بنيويّ) | **403** |
| S-07 | مؤشّر يخصّ هدفًا آخر ⟵ عبر مسار الهدف الأوّل | **404** |
| S-08 | هدف يخصّ مشروعًا آخر ⟵ عبر مسار هذا المشروع | **404** |
| S-09 | `ObjectiveId` من مشروع مختلف عند الإنشاء | `project_kpi.objective_mismatch.conflict` **409** |
| S-10 | غير مصادَق ⟵ أيّ مسار | **401** |

### 15-4. معايير الجودة

| # | المعيار |
|---|---|
| Q-01 | `dotnet build` بـ**0 أخطاء** |
| Q-02 | اختبارات الوحدة كلّها خضراء (محرّك الاحتساب مغطًّى بحالات حدّيّة) |
| Q-03 | اختبارات التكامل الجديدة كلّها خضراء (تغطّي §15-2 و§15-3 كاملتين) |
| Q-04 | `tsc` بـ**0 أخطاء** + `vite build` ناجح |
| Q-05 | `vitest` كلّه أخضر بما فيه اختبارات الواجهة الجديدة |
| Q-06 | **صفر اعتماديّة جديدة** في `package.json` أو `*.csproj` |
| Q-07 | تدقيق مسجَّل لكلّ كتابة، بلا أسرار وبلا معرّفات لا تخصّ المستخدم |

---

## 16. تحليل الانحدار

### 16-1. لماذا الانحدار متوقَّع أن يكون صفريًّا — بالبناء لا بالأمل

| الطبقة | التغيير | لماذا لا ينحدر |
|---|---|---|
| قاعدة البيانات | 6 جداول + 13 عمودًا | كلّها إضافيّة بقيم افتراضيّة؛ لا استعلام قائم يقرأ ما لا يعرفه |
| الدومين | كيانات جديدة في فضاء `Projects360` | لا تعديل على كيان قائم عدا إضافة خصائص اختياريّة |
| التطبيق | خدمات وعقود جديدة | `UpdateProjectRequest` وعقود المشاريع القائمة **بلا تعديل** (R7) |
| الأمن | قاعدة على مستوى المورد | لا دور ولا سياسة جديدة؛ `EXEC_ROLES` بلا توسيع |
| الـAPI | مسارات جديدة تحت `/api/projects/{id}/…` | صفر تغيير على مسار قائم (A-06) |
| الواجهة | تبويبات داخل صفحة قائمة | لا مسار توجيه جديد؛ الشاشات الأخرى لم تُمَسّ |
| `Workstream*` | **لا شيء** | ضمانة `git diff` فارغة (A-03) |

### 16-2. نطاق الانحدار المطلوب تشغيله

1. **الانحدار الكامل** لطبقة الخلفيّة على قاعدة معزولة نظيفة بعد تطبيق الهجرة.
2. **الانحدار الكامل** لطبقة الواجهة (`vitest`).
3. مقارنة النتيجة بخطّ أساس `develop @ c157829` **قبل** الباتش: أيّ فشل حصريّ على المرشّح = حاجب.
4. تدقيق يدويّ على شاشات: تفاصيل المشروع، قائمة المشاريع، Client 360، لوحات التقارير — بحثًا عن انحدار بصريّ.

### 16-3. العيوب الأساسيّة المعروفة (لا تُصلَح هنا)

`BASELINE-DEFECT-01` و`BASELINE-DEFECT-02` قائمان على خطّ الأساس بدلتا صفريّة. إن ظهرا فهما **ليسا انحدارًا** ولا يحجبان CPW-R3؛ يُوثَّقان في تقرير التسليم ويبقيان في تذكرتيهما المستقلّتين. **حاسم**: أيّ فشل آخر لا يظهر على خطّ الأساس = حاجب مطلق.

### 16-4. درس تشغيليّ مُلزَم من CPW-R2

نتائج المهامّ الخلفيّة قد تكون **بائتة**. قبل تصديق أيّ فشل، تُقارَن `mtime` ملفّ المخرجات بوقت آخر تعديل للكود. هذا الدرس كلّف CPW-R2 تحقيقًا كاملًا في ثلاثة «فشلات» كان تقريرها أقدم من الإصلاح بثلاث دقائق.

---

## 17. خارطة التنفيذ (R2 — موسَّعة عن R1)

خطّة R1 كانت `W0–W10`. إدخال **الاستراتيجيّة** (D-04) و**المخرَجات** (D-03) و**اللوحة** (D-05) وإلزاميّة **الهدف للمؤشّر** (D-02) يوسّعها إلى **`W0–W14`**. كلّ مرحلة **ذرّيّة**: تُبنى وتُختبَر وحدها، ولا تبدأ التالية قبل خضرة سابقتها.

| المرحلة | المحتوى | مخرَج التحقّق |
|---|---|---|
| **W0** | تجميد الأساس: التحقّق من `develop @ c157829` و**32 هجرة** ورأسها `20260713171040`، وإنشاء فرع العمل | لقطة نَسَب موثَّقة |
| **W1** | الدومين: التعدادات التسعة + الكيانات الستّة + توسعة `Project` و`Decision` | `dotnet build` = 0 |
| **W2** | إعدادات EF الستّة + `DbSet`s + الفهارس | `has-pending-model-changes` = `No changes` |
| **W3** | **الهجرة الوحيدة** `AddProject360Foundation` + مراجعة SQL + `Up→Down→Up` | سكربت SQL مُراجَع |
| **W4** | بذر مجالَي الكتالوج (`contract_deliverable` · `strategy_field`) | بذّار idempotent مُثبَت بإعادة التشغيل |
| **W5** | `ProjectHealthPolicy` + محرّك الاحتساب + **اختبارات وحدة كاملة للحالات الحدّيّة** | اختبارات الوحدة خضراء |
| **W6** | عقود التطبيق: DTOs + واجهات الخدمات (الأهداف/المؤشّرات/القراءات) | `dotnet build` = 0 |
| **W7** | خدمة الأهداف + خدمة المؤشّرات + القراءات (**فرض D-02** + إعادة احتساب الصحّة) | اختبارات تكامل الهرميّة |
| **W8** | خدمة المخرَجات (D-03) + تحقّق رمز النوع + عدم قابليّة تغييره | اختبارات تكامل المخرَجات |
| **W9** | خدمة الاستراتيجيّة (D-04) + `schema` + **المزامنة التفاضليّة** | اختبارات تكامل الاستراتيجيّة |
| **W10** | خدمة البريف + التقدّم + الصحّة + `/overview` المُجمَّع | اختبار **صفر N+1** |
| **W11** | طبقة الـAPI كاملة + مصفوفة الأمن (§15-3) | اختبارات تكامل الأمن العشرة |
| **W12** | الواجهة — **اللوحة أوّلًا** (D-05): الترويسة + النظرة التنفيذيّة + شبكة البطاقات + النشاط | `tsc` = 0 |
| **W13** | الواجهة — التبويبات العشرة + النماذج + الحقول المشروطة | `vitest` أخضر |
| **W14** | الانحدار الكامل + مقارنة خطّ الأساس + حزمة التسليم | تقرير قبول نهائيّ |

**بوّابات إلزاميّة**: لا تبدأ `W3` قبل خضرة `W2`؛ ولا تبدأ `W11` قبل اكتمال `W5`–`W10`؛ ولا يُسلَّم شيء قبل `W14`.

---

## 18. الخلاصة التنفيذيّة

### 18-1. ما الذي تغيّر عن R1

| القرار | الأثر الجوهريّ على التصميم |
|---|---|
| **D-01** | البناء فوق `develop` وحده · تبويب المستندات **مؤجَّل بالكامل** · `R9` مُخفَّض و`R10` مُلغى · تُسلَّم **10 من 11** متطلّبًا |
| **D-02** | `ObjectiveId` صار **`NOT NULL`** · لا مسار `POST /projects/{id}/kpis` · الواجهة تمنع «مؤشّر يتيم» بنيويًّا |
| **D-03** | طبقة **`ProjectDeliverable`** جديدة + مجال كتالوج `contract_deliverable` (18 رمزًا) بحدود قاطعة عن `WorkstreamDeliverable` |
| **D-04** | **`ProjectStrategy`** على مستوى المشروع: 11 حقلًا أساسيًّا + سمات مشروطة بنوع المشروع من الكتالوج (14 رمزًا) |
| **D-05** | `ProjectDetailPage` صارت **لوحة** بنداء `/overview` واحد قبل التبويبات |
| **D-06** | `SourceType` + ثلاثة أعمدة خارجيّة مُهيّأة اليوم · «المؤشّر لا يتغيّر — مصدره فقط» |
| **D-07** | قائد الفريق مسؤول عن أربعة حقول كتابة، والصحّة المخزَّنة تُغذّي سلسلة اللوحات |
| **D-08** | الهرميّة النهائيّة موثَّقة مع إثبات «صفر إعادة هيكلة» لسبع توسّعات مستقبليّة |

### 18-2. الأثر الكمّيّ النهائيّ

| البند | العدد |
|---|---|
| جداول جديدة | **6** |
| أعمدة مضافة على `projects` | **12** |
| أعمدة مضافة على `decisions` | **1** |
| هجرات | **1** (إضافيّة بحتة) |
| تعدادات جديدة | **9** |
| مجالات كتالوج جديدة | **2** (32 رمزًا) |
| أدوار جديدة | **0** |
| سياسات جديدة | **0** |
| اعتماديّات جديدة | **0** |
| شاشات جديدة | **0** |
| مسارات توجيه جديدة | **0** |
| جداول قائمة عُدِّلت بنيويًّا | **0** |
| ملفّات `Workstream*` مُعدَّلة | **0** |

### 18-3. الحالة

```
CPW-R3 — PROJECT 360 FOUNDATION — REVISED DESIGN REPORT (R2)
STATUS: DESIGN COMPLETE — AWAITING OWNER APPROVAL

OWNER DECISIONS D-01…D-08 : ALL INCORPORATED
CODE WRITTEN               : 0 LINES
MIGRATIONS CREATED         : 0
COMMITS                    : 0
DEPLOYMENTS                : 0
ENVIRONMENTS TOUCHED       : NONE (TEST / RC / PRODUCTION ALL UNTOUCHED)

SUPERSEDES : CPW-R3-PROJECT-360-FOUNDATION-OBJECTIVES-KPIS-HEALTH-R1-DIAGNOSIS-AND-DESIGN-REPORT.md (R1)
NEXT GATE  : OWNER APPROVAL OF THIS REPORT
ON APPROVAL: BEGIN W0 (BASELINE FREEZE) — NOT BEFORE
```

**لا يُكتب سطر كود واحد قبل اعتماد هذا التقرير.**
