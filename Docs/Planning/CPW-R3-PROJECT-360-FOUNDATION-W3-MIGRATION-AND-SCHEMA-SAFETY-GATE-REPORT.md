# CPW-R3 — PROJECT 360 FOUNDATION — W3: MIGRATION GENERATION + SCHEMA SAFETY GATE

**التاريخ:** 11 أغسطس 2026
**الفرع:** `develop` · **الأساس:** `c157829f750ce98b7e7aad451a23183b58462cb4`
**النطاق:** توليد الهجرة الفعليّة والتحقّق منها **فقط**. لا Application Services · لا API · لا Frontend · لا Commit · لا Push · لا Deploy · لا تحديث لأيّ قاعدة حيّة.

---

## §1 — عزل مصدر الهجرة (إلزاميّ)

الشجرة الرئيسة **قذرة** (27 ملفًّا معدَّلًا + عشرات الملفّات غير المتعقَّبة من تذاكر سابقة) ⟹ لم تُولَّد الهجرة منها.

| البند | القيمة |
|---|---|
| آليّة العزل | `git worktree add --detach /tmp/cpw-r3-w3-20260811 c157829f…` |
| HEAD الشجرة المعزولة | `c157829f750ce98b7e7aad451a23183b58462cb4` (detached) |
| محتوى الشجرة المعزولة | الأساس + **ملفّات CPW-R3 حصرًا** |
| `git status --short` في الشجرة المعزولة | **12 مدخلة، كلّها CPW-R3** |
| `grep -i workstream` على الملفّات المتغيّرة | **فارغ** |

**ISOLATION_PROVEN = YES**

---

## §2 — Model Pre-Gate (قبل التوليد)

| الفحص | النتيجة |
|---|---|
| `dotnet build` | **0 Errors** · 1 Warning (`CS8604` في `ReportTemplateTests.cs:121` — سابق الوجود على الأساس) |
| `has-pending-model-changes` | **"Changes have been made to the model since the last migration."** ✓ |
| عدد الهجرات المتعقَّبة | **32** ✓ |
| آخر هجرة | `20260713171040_AdminGovernanceReportKpiCorrection` ✓ |

---

## §3 — التوليد

هجرة **واحدة** لا غير:

```
20260811142239_AddProject360Foundation.cs           (518 سطرًا)
20260811142239_AddProject360Foundation.Designer.cs
AppDbContextModelSnapshot.cs                        (محدَّث)
```

عدد الهجرات بعد التوليد = **33**. **No second migration.**

---

## §4 — دلتا `Up()` المتوقَّعة مقابل الفعليّة

### الأعمدة على `projects` — **12 عمودًا**

`Summary` · `Background` · `BusinessContext` · `ScopeText` · `OutOfScope` · `SuccessDefinition` · `ProgressPercent` · `HealthPercent` · `HealthStatus` · `HealthComputedAtUtc` · `ProjectOwnerId` · `TeamLeaderId`

### `decisions.ProjectId`

`uuid` · **nullable = YES** ✓ · بلا مفتاح أجنبيّ ✓ · القرارات القائمة تبقى `NULL` ⟹ **صفر Backfill**.

### الجداول — **بالضبط ستّة**

`project_objectives` · `project_kpis` · `project_kpi_readings` · `project_deliverables` · `project_strategies` · `project_strategy_attributes`

**NO SEVENTH TABLE = CONFIRMED**

### إحصاء عمليّات `Up()`

| العمليّة | العدد |
|---|---|
| `AddColumn` | **13** (12 على `projects` + 1 على `decisions`) |
| `CreateTable` | **6** |
| `CreateIndex` | **22** |
| `AddForeignKey` | **0** — كلّ المفاتيح الأجنبيّة الثمانية مُصرَّحة **داخل** `CreateTable` (نمط EF المعتاد) |

---

## §5 — التحقّق من العلاقات (مثبَت على قاعدة حقيقيّة)

### المفاتيح الأجنبيّة الثمانية وسلوك الحذف

| الجدول | العمود | المرجع | ON DELETE |
|---|---|---|---|
| `project_objectives` | `ProjectId` | `projects` | **CASCADE** |
| `project_kpis` | `ProjectId` | `projects` | **CASCADE** |
| `project_kpis` | `ObjectiveId` | `project_objectives` | **CASCADE** |
| `project_kpi_readings` | `ProjectKpiId` | `project_kpis` | **CASCADE** |
| `project_deliverables` | `ProjectId` | `projects` | **CASCADE** |
| `project_deliverables` | `ObjectiveId` | `project_objectives` | **SET NULL** |
| `project_strategies` | `ProjectId` | `projects` | **CASCADE** |
| `project_strategy_attributes` | `ProjectStrategyId` | `project_strategies` | **CASCADE** |

> **ملاحظة**: PostgreSQL يسمح بمسارات Cascade متعدّدة ⟹ الـCascade المزدوج على `project_kpis` (من المشروع ومن الهدف) مقبول بلا تحفّظ.
> اسم المفتاح الأخير مقصوص إلى `FK_project_strategy_attributes_project_strategies_ProjectStrat~` بفعل حدّ **63 حرفًا** لأسماء PostgreSQL — سلوك طبيعيّ لا عيب.

### الأعمدة المرجعيّة بلا مفتاح أجنبيّ (مقصود)

`WorkstreamId` · `OwnerUserId` · `RecordedByUserId` · `ProjectOwnerId` · `TeamLeaderId` · `Decision.ProjectId` ⟹ **0 مفتاح أجنبيّ لكلٍّ منها** (مثبَت باستعلام `information_schema` على القاعدة المؤقّتة).

---

## §6 — عقد الهدف ⟵ المؤشّر (D-02، غير قابل للتفاوض)

`project_kpis.ObjectiveId` · `is_nullable = **NO**`

**إثبات وظيفيّ على قاعدة حقيقيّة (لا استنتاج):**

| الاختبار | النتيجة |
|---|---|
| إدراج مؤشّر بـ`ObjectiveId = NULL` | `ERROR: null value in column "ObjectiveId" … violates not-null constraint` ✓ |
| إدراج مؤشّر بحذف العمود كلّيًّا | نفس الرفض ✓ |
| إدراج مؤشّر بهدف غير موجود | `ERROR: … violates foreign key constraint "FK_project_kpis_project_objectives_ObjectiveId"` ✓ |
| إدراج مؤشّر بهدف حقيقيّ | نجح ✓ |
| حذف الهدف | المؤشّر **زال بالتعاقب** (0 صفوف) ✓ |
| المُخرَجات بلا هدف (D-03) | أُدرِجت بنجاح وبقيت بعد حذف الهدف مع `ObjectiveId → NULL` ✓ |

**NO DATABASE-VALID PATH FOR AN ORPHAN KPI = CONFIRMED**

---

## §7 — حدّ المخرجات التعاقديّة

| الفحص | النتيجة |
|---|---|
| تعديل على `ProjectWorkstream` / `WorkstreamDeliverable` | **صفر** |
| `grep -i Workstream` على الملفّات المتغيّرة | **فارغ** |
| ذكر `Workstream` في دلتا الـSnapshot | 4 أسطر فقط: `Guid? WorkstreamId` + فهرس عاديّ على **الجدولين الجديدين** (`project_objectives` · `project_deliverables`) — بلا مفتاح أجنبيّ وبلا مساس بالكيان القائم |
| `project_workstreams` في القاعدة المؤقّتة | قائم بلا تغيير ✓ |

---

## §8 — جاهزيّة إصدارات الاستراتيجيّة

```csharp
migrationBuilder.CreateIndex(
    name: "IX_project_strategies_ProjectId_Active",
    table: "project_strategies",
    column: "ProjectId",
    unique: true,
    filter: "\"IsActive\" = true");
```

**إثبات وظيفيّ**: الاستراتيجيّة النشطة الثانية على نفس المشروع رُفضت بـ
`duplicate key value violates unique constraint "IX_project_strategies_ProjectId_Active"`،
والاستراتيجيّة **غير النشطة** قُبلت (المجموع 2 · النشط 1) ✓

**No `VersionNo` · No strategy history table** — مؤجَّلان بقرار المالك.

---

## §9 — حقول الترتيب والوزن

| البند | النتيجة |
|---|---|
| `SortOrder` في الـSnapshot | **11 موضعًا** |
| `Weight` (`numeric(9,2)`) | `project_objectives` · `project_kpis` |
| **`DisplayOrder` في سكيمة CPW-R3** | **ZERO** ✓ (قرار W1-A) |

---

## §10 — نموذج الصحّة

`ProgressPercent` · `HealthPercent` · `HealthStatus` · `HealthComputedAtUtc` — أعمدة قياسيّة على `projects`.

**`HEALTH_REASONS_STORAGE = NONE`** — لا عمود `Reasons` · لا JSON · لا جدول أسباب.
(`jsonb` في الـSnapshot = 5 مواضع، **كلّها خارج نطاق Projects360** بالكامل.)

---

## §11 — الدقّة العدديّة (مثبَتة على القاعدة)

| `numeric(9,2)` | `numeric(18,2)` |
|---|---|
| `projects.ProgressPercent` | `project_kpis.BaselineValue` |
| `projects.HealthPercent` | `project_kpis.TargetValue` |
| `project_objectives.Weight` | `project_kpis.CurrentValue` |
| `project_kpis.Weight` | `project_kpi_readings.Value` |
| `project_deliverables.ProgressPercent` | `project_kpi_readings.TargetSnapshot` |
| `project_kpi_readings.AchievementSnapshot` | |

> **ملاحظة تقنيّة**: يظهر `precision: 18, scale: 2` بجانب `type: "numeric(9,2)"` في نصّ الهجرة بفعل `ConfigureConventions ⟹ HavePrecision(18,2)`؛ الـ`type` هو الحاكم، وقد أثبت الفحص على القاعدة أنّ الناتج الفعليّ **`numeric(9,2)`**.

---

## §12 — تدقيق الفهارس

على القاعدة المؤقّتة: **22 فهرسًا + 6 مفاتيح أساسيّة**.

الفهارس **الفريدة** غير الأساسيّة — **ثلاثة فقط**:

1. `IX_project_strategies_ProjectId_Active` — `UNIQUE … WHERE ("IsActive" = true)`
2. `IX_project_kpi_readings_ProjectKpiId_ReadingDate` — `UNIQUE`
3. `IX_project_strategy_attributes_StrategyId_FieldCode` — `UNIQUE`

**صفر قيد `UNIQUE` إضافيّ غير متوقَّع** ✓

---

## §13 — تدقيق Additive-Only

| المسموح في `Up()` | العدد |
|---|---|
| `AddColumn` · `CreateTable` · `CreateIndex` · `AddForeignKey` | 13 · 6 · 22 · 0 |

| المحظور في `Up()` | العدد |
|---|---|
| `Drop*` · `Rename*` · `Alter*` · `DeleteData` · `UpdateData` · `InsertData` · `Sql(` | **0 لكلٍّ منها** |

**فحص آليّ**: عدّ كلّ العمليّات المحظورة ضمن أسطر `Up()` (1–428) = **0**.

**ADDITIVE_ONLY = PASS**

---

## §14 — سلامة الصفوف القائمة

### تصحيح واحد مقصود داخل `AddColumn` (عمليّة مسموحة)

EF ولّد آليًّا `defaultValue: ""` لعمود `HealthStatus` — وهو عمود تعداد **مُحوَّل نصًّا** (`HasConversion<string>()`)، والسلسلة الفارغة **ليست عضوًا صالحًا** في `ProjectHealthStatus {Green=0, Yellow=1, Red=2}` ⟹ كانت صفوف `projects` القائمة ستحمل قيمة لا تُحوَّل عند القراءة.

**التصحيح** (تعديل نقطيّ وحيد، بلا `UpdateData`/`Sql(`/Backfill):

```csharp
defaultValue: "Green"   // العضو ذو القيمة 0، وهو نفسه الافتراضيّ في الكيان
```

الـSnapshot خالٍ من `HasDefaultValue` ⟹ صفر أثر على تزامن النموذج (أُثبت لاحقًا بـ`has-pending-model-changes`).

### إثبات وظيفيّ

إدراج صفّ `projects` بأعمدة **ما قبل الهجرة حصرًا** على القاعدة المؤقّتة أعطى:

```
HealthStatus = Green | HealthPercent = 0.00 | ProgressPercent = 0.00 | Summary = NULL
```

> **Existing projects require data rewrite: NO**

---

## §15 — مراجعة `Down()`

`Down()` يبدأ عند السطر 429 ويحوي حصرًا:

| العمليّة | العدد |
|---|---|
| `DropTable` | 6 |
| `DropIndex` | 4 |
| `DropColumn` | 13 |

عكس كامل ونظيف بلا أيّ عمليّة على بيانات. **لا يُشغَّل في أيّ بيئة** (`EF Down` محظور بلا تصريح).

---

## §16 — التحقّق من الـSnapshot

| الفحص | النتيجة |
|---|---|
| الكيانات الستّة الجديدة | موجودة (السطور 2593 · 2687 · 2794 · 2842 · 2915 · 2987) |
| `project_workstreams` | قائم بلا مساس (السطر 580) |
| `ProjectKpi.ObjectiveId` | `Guid` (**NOT NULL**) — السطر 2753 |
| `ProjectDeliverable.ObjectiveId` | `Guid?` (مرن) — السطر 2632 |
| الفهرس الجزئيّ | `IsUnique()` + `HasFilter("\"IsActive\" = true")` — السطور 2979–2982 |
| `DisplayOrder` | **0** |
| `HealthReasons` / `"Reasons"` | **0** |
| `HasDefaultValue` | **0** |
| `jsonb` | 5 مواضع، كلّها خارج نطاق Projects360 (2593–3030) |
| دلتا الـSnapshot مقابل الأساس | **+582 سطرًا · −0 سطر** ⟹ إضافيّة بحتة |

---

## §17 — بوّابة تزامن النموذج

| الفحص | الشجرة المعزولة | الشجرة الرئيسة |
|---|---|---|
| `dotnet build` | 0 Errors | **0 Errors** |
| `has-pending-model-changes` | **"No changes have been made to the model since the last migration."** | **نفس النتيجة** ✓ |
| عدد الهجرات | 33 | **33** |
| آخر هجرة | `20260811142239_AddProject360Foundation` | **نفسها** ✓ |

**لم تُولَّد هجرة ثانية.**

---

## §18 — إثبات القاعدة المؤقّتة (نُفِّذ)

| البند | القيمة |
|---|---|
| اسم القاعدة | `reporting_cpwr3_w3_tmp_20260811` (خارج قائمة الأسماء المحظورة كلّيًّا) |
| الأصل | قاعدة **فارغة** أُنشئت لهذا الغرض حصرًا |
| الهجرات المطبَّقة | **33** · الرأس `20260811142239_AddProject360Foundation` |
| القواعد المحظورة (`prod`/`rc`/`test`/`test_uat`/`dev`) | **لم تُمَسّ إطلاقًا** |
| المصير | **أُسقطت** بعد الانتهاء — تحقّق: غائبة عن `pg_database` ✓ |

كلّ ما ورد في §4–§14 من أدلّة على القاعدة استُخرِج من هذه القاعدة ثمّ زالت.

---

## §19 — سلامة الشجرة الرئيسة

| الفحص | النتيجة |
|---|---|
| HEAD الشجرة الرئيسة | `c157829f…` — **مطابق** لـHEAD المعزولة |
| حالة `Migrations/` قبل النسخ | **نظيفة تمامًا** (0 مدخلة في `git status`) |
| حالة `AppDbContextModelSnapshot.cs` قبل النسخ | **نظيفة** ⟹ **صفر تصادم** |
| الملفّات المُعادة | **ثلاثة فقط**، وكلّها byte-identical مع المصدر المعزول |
| ملفّات الهجرة في الشجرة الرئيسة | 65 ⟵ **67** (= 33×2 + Snapshot) |
| أيّ ملفّ آخر نُسخ من الشجرة المعزولة | **لا شيء** |
| الشجرة المعزولة | **باقية كما هي** (لم تُحذف — إجراء غير مدمّر) |

**COLLISION = NONE**

---

## §20 — بوّابة W3 النهائيّة

| البند | القرار |
|---|---|
| §1 عزل مصدر الهجرة | **GO** |
| §2 Model Pre-Gate | **GO** |
| §3 هجرة واحدة فقط | **GO** |
| §4 دلتا `Up()` (12 عمودًا · `decisions.ProjectId` · 6 جداول) | **GO** |
| §5 العلاقات وسلوك الحذف (8 مفاتيح · صفر مفتاح على المراجع المرنة) | **GO** |
| §6 عقد الهدف ⟵ المؤشّر (D-02) | **GO** |
| §7 حدّ المخرجات التعاقديّة (صفر مساس بـWorkstream) | **GO** |
| §8 جاهزيّة إصدارات الاستراتيجيّة (فهرس فريد جزئيّ) | **GO** |
| §9 الترتيب والوزن (`SortOrder`، صفر `DisplayOrder`) | **GO** |
| §10 نموذج الصحّة (`HEALTH_REASONS_STORAGE = NONE`) | **GO** |
| §11 الدقّة العدديّة | **GO** |
| §12 تدقيق الفهارس (ثلاثة فريدة لا رابع) | **GO** |
| §13 Additive-Only | **GO** |
| §14 سلامة الصفوف القائمة (**data rewrite: NO**) | **GO** |
| §15 مراجعة `Down()` | **GO** |
| §16 التحقّق من الـSnapshot | **GO** |
| §17 بوّابة تزامن النموذج | **GO** |
| §18 إثبات القاعدة المؤقّتة | **GO** |
| §19 سلامة الشجرة الرئيسة | **GO** |

### القرار النهائيّ

```
CPW-R3 · W3 = GO
MIGRATION: 20260811142239_AddProject360Foundation
MIGRATIONS TOTAL: 33
ADDITIVE_ONLY: PASS
EXISTING PROJECTS REQUIRE DATA REWRITE: NO
HEALTH_REASONS_STORAGE: NONE
NO SEVENTH TABLE: CONFIRMED
ORPHAN KPI PATH: NONE (D-02 ENFORCED AT DATABASE LEVEL)
WORKSTREAM SCHEMA: UNTOUCHED
TEMPORARY DATABASE: DROPPED
MAIN WORKTREE COLLISION: NONE
```

### مقفلة على NO-GO (بلا تصريح مستقلّ)

`Commit` · `Push` · `Merge` · `PR` · `Tag` · نشر TEST · نشر RC · نشر Production · تحديث أيّ قاعدة حيّة · `EF Down` · Backfill · بذر مجالات الكتالوج الثلاثة · بدء W4 (Application/API/Frontend).

**توقّف تامّ بعد هذا التقرير.**
