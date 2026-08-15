# CPW-R3 · Project 360 Foundation — W6 · Integration Gate

> **التاريخ:** 15 أغسطس 2026 · **الفرع:** `develop` · **HEAD:** `c157829f750ce98b7e7aad451a23183b58462cb4`
> **النتيجة:** `W6 = GO` مع **4 Findings مرفوعة لقرار المالك** · **صفر تعديل كود** · **صفر Commit/Push/نشر**

---

## 1) الخلاصة التنفيذيّة

W6 نُفِّذت **بوّابة تحقّق قرائيّة بحتة**: صفر ملفّ كُتِب، صفر هجرة، صفر تعديل مخطّط، صفر مساس بأيّ بيئة مشتركة أو منشورة. كلّ الإثبات جرى على **قاعدتين مؤقّتتين معزولتين** أُنشئتا وحُذفتا داخل الجلسة.

**الرقم الحاسم:** الانحدار الكامل على قاعدة نظيفة معزولة أعطى **1461 اختبارًا · 1455 نجاحًا · 6 إخفاقات · 4 د 26 ث** (مقابل 23 إخفاقًا و1 س 48 د على `reporting_test` المشتركة). اختبارات الوحدة **137/137**. اختبارات Project 360 الأربعة والثلاثون **نجحت جميعًا**.

**تصنيف الإخفاقات الستّة بتجربة خطّ أساس مباشرة على `c157829`:**

| العدد | التصنيف | الإثبات |
|---|---|---|
| **5** | **Pre-existing Dirty Worktree** | تنجح **60/60 على خطّ الأساس** وتفشل على المرشَّح بنفس التأكيدات ⇒ سببها ملفّات غير متتبَّعة/معدَّلة من 2026-07-25 خارج CPW-R3 |
| **1** | **Baseline Defect** (`BASELINE-DEFECT-01`) | تفشل **على خطّ الأساس أيضًا** بنفس الرسالة على قاعدة بكر ⇒ دلتا صفريّة |
| **0** | **Candidate Regression** | — |
| **0** | **Unresolved** | — |

**تصحيح لتصنيف W5:** الإخفاقات الخمس صُنِّفت في W5 «Baseline Defect»؛ تجربة خطّ الأساس في W6 تُثبت أنّها **Pre-existing Dirty Worktree** (تنجح فعليًّا على `c157829`). التصنيف الأدقّ مُسجَّل هنا ويستلزم إعادة تسجيل `BASELINE-BE-DEFECT-03` تحت التصنيف الصحيح.

---

## 2) التصريح والنطاق

**المصرَّح به:** `CPW-R3 / W6 — Integration Gate` فقط.

**المستبعَد صراحةً وقد التُزم به بالكامل (إثبات في §18):** Frontend Project 360 · W7 وما بعدها · إصلاح عيوب Baseline · تنظيف `reporting_test` · Migration جديدة · تعديل Schema · Commit · Push · Merge/PR/Tag · نشر TEST/UAT/RC/Production · أيّ تعامل مع Production أو RC.

---

## 3) المصادر المُراجَعة

| # | المصدر | ما استُخرِج منه |
|---|---|---|
| 1 | `CPW-R3-...-R2-REVISED-DESIGN-REPORT.md` | §17 خارطة W0–W14 (س1284–1302) · §15-3 مصفوفة الأمن S-01…S-10 (س1222–1233) · D-01…D-08 (س18–25، 1312–1319) · معادلة الصحّة (س1005–1009) · `SourceType` (س187) · idempotency الكتالوج (س954–961) |
| 2 | `...-W0-BASELINE-FREEZE-REPORT.md` | خطّ الأساس `c157829` · 32 هجرة · المسارات التسعة المجمَّدة · §6 الحالة المتّسخة المسبقة |
| 3 | `...-W1-DOMAIN-IMPLEMENTATION-REPORT.md` | نطاق الدومين (س20) |
| 4 | `...-W1-A-OWNER-AMENDMENTS-REPORT.md` | التعديلات السبعة + قيد W2 (§6-1) |
| 5 | `...-W3-MIGRATION-AND-SCHEMA-SAFETY-GATE-REPORT.md` | الهجرة الوحيدة ومراجعتها (س5) |
| 6 | `...-W4-BUSINESS-AND-APPLICATION-ENGINE-REPORT.md` | §7 قرارات DN-01…DN-08 · §160 «الصحّة تُحسَب حيًّا ولا تُكتَب» · DEC-W4-03/04 · RM-01 (س224) |
| 7 | `...-W5-API-SURFACE-AND-CLOSING-GATE-REPORT.md` | جميع الأقسام: Scope · Route Inventory · Authorization Contract · Catalog Bootstrap · §31 · §33 · §35 · §39 |
| 8 | `Docs/BRD-SRS-PerformanceReportingSystem-AR_v1.1.docx` | الفصول 4 · 5 · 7 · 10 · 13 · 14 (مستخرَجة إلى `/tmp/brd-v11.txt` ثمّ حُذفت البيئة المؤقّتة) |
| 9 | واقع Git والكود | 47 فحصًا مباشرًا موثّقة في §6 و§9–§14 |

### 3-1. ترتيب الحقيقة المطبَّق

`Latest frozen Owner Decisions → CPW-R3 R2 → تقارير W0–W5 → BRD → واقع Git/الكود`. عند كلّ تعارض رُفِع Finding ولم يُحسَم باجتهاد صامت (§19).

### 3-2. `[BRD]` — نتيجة مراجعة الفصول المطلوبة

**BRD v1.1 صامت تمامًا عن Project 360.** الوثيقة تنصّ حرفيًّا (السطر 21) على أنّ النظام **«ليس نظام إدارة مشاريع كامل (Project Management)»**. لا يوجد فيها فصل لأهداف المشروع ولا لمؤشّراته ولا للاستراتيجيّة ولا للمخرَجات التعاقديّة ولا لصحّة المشروع ولا للوحة تفاصيل المشروع. المشروع/العميل يَرِدان **كحقول تصنيف فقط**: مركز المخاطر (5.11) · جودة الربط (5.13.8) · تقرير التصعيدات (5.13.13). وفي الفصل 13 قوالب مرتبطة بأدوار وظيفيّة (Account Manager · Content Writer متعدّد المشاريع · SEO TL) لا بكيانات Project 360.

**الأثر:** CPW-R3 **توسّع ما بعد الوثيقة** تحكمه R2 وقرارات المالك حصرًا ⇒ **صفر تعارض مع BRD** (الصمت ليس تعارضًا)، ولا يوجد متطلّب BRD غير مُلبّى في هذه الحزمة.

---

## 4) خطّ أساس Git وشجرة العمل

| البند | القيمة | التحقّق |
|---|---|---|
| Current branch | `develop` | `git rev-parse --abbrev-ref HEAD` |
| Current HEAD | `c157829f750ce98b7e7aad451a23183b58462cb4` | `git rev-parse HEAD` |
| Baseline المعتمد | `c157829f750ce98b7e7aad451a23183b58462cb4` | **مطابق لـHEAD تمامًا** — صفر Commit منذ التجميد |
| ملفّات متتبَّعة معدَّلة | **33** | `git status --short` |
| مسارات غير متتبَّعة | **138** | `git status --short` |
| عدد الهجرات | **33** | عدّ ملفّات `Migrations/*.cs` |
| رأس الهجرات | `20260811142239_AddProject360Foundation` | لا `#34` |
| تغييرات لا تخصّ CPW-R3 | **نعم — موجودة مسبقًا** | مفصَّلة في §8 |
| تقرير W5 النهائيّ | موجود ومعتمَد من المالك | `...-W5-API-SURFACE-AND-CLOSING-GATE-REPORT.md` |

**التزامات شجرة العمل المتّسخة:** لم يُستخدم `git add -A` ولا `add` مطلقًا · لم يُنظَّف أيّ تغيير قائم · لم يُنفَّذ `reset` ولا `checkout` · لم تُنسَب أيّ دلتا موجودة إلى W6.

---

## 5) `[R2 / Owner Decisions]` — مصفوفة متطلّبات W6

### 5-1. FINDING-W6-01 — تعريف «W6» في R2 ليس «Integration Gate»

> رُفِع قبل أيّ تعديل كود، التزامًا بـ§3 من التصريح.

**Finding** — R2 §17 السطر 1292 يعرّف `W6` حرفيًّا: «**عقود التطبيق: DTOs + واجهات الخدمات (الأهداف/المؤشّرات/القراءات)**» ومخرَج تحقّقه `dotnet build = 0`. **لا توجد في R2 أيّ مرحلة اسمها Integration Gate.** المصطلح نشأ في تقرير W5 نفسه (السطران 439 و447).

**Impact** — الترقيم المنفَّذ (W0 · W1 · W1-A · W2 · W3 · W4 · W5) **مضغوط** ولا يطابق R2 (W0–W14):

| R2 | غُطِّيت في | الدليل |
|---|---|---|
| W0 تجميد الأساس | W0 | W0-REPORT س1، س6 |
| W1 الدومين | W1 | W1-REPORT س20 |
| W2 إعدادات EF | W1-A + W2 | W1-A-REPORT §6-1 (س119–127) |
| W3 الهجرة | W3 | W3-REPORT س5 |
| W4 بذر الكتالوج | W4 + W5 | `ExecutionTaxonomySeeder` — 38 قيمة |
| W5 `ProjectHealthPolicy` | W4 | `ProjectHealthPolicyTests` |
| **W6 عقود التطبيق (DTOs + واجهات)** | **W4 — مُنجَزة** | `Project360Models.cs` · `IProject360Services.cs` · `IProject360Authorization.cs` · Build = 0 |
| W7 خدمات الأهداف/المؤشّرات/القراءات | W4 | `ProjectObjectiveService` · `ProjectKpiService` |
| W8 خدمة المخرَجات | W4 | `ProjectContractDeliverableService` |
| W9 خدمة الاستراتيجيّة | W4 | `ProjectStrategyService` |
| W10 `/overview` + صفر N+1 | W4 + W5 | `OVERVIEW_QUERY_COUNT = 12` ثابت |
| W11 API + مصفوفة الأمن | W5 | 33 مسارًا + 14 اختبار سطح |
| **W12 · W13 · W14** | **لم تُغطَّ** | `Ready for Frontend: NO-GO` |

أي: **R2-W6 مُغلَقة أصلًا داخل W4**، و**مرحلة R2 التالية فعليًّا هي W12 (الواجهة)**.

**Options** — (1) اعتبار «W6 Integration Gate» بوّابة تحقّق قرائيّة فوق W1–W5. (2) إعادة الترقيم رسميًّا في R2 §17. (3) التخطّي إلى R2-W12.

**Recommendation** — **الخيار 1**: تصريحك §4 و§6 و§9 و§10 كلّها تحقّق وتقرير، وR2-W6 مُنجَزة سلفًا ⇒ **صفر كود يُكتَب وصفر تعارض عمليّ**. نُفِّذت البوّابة بـ`W6 Files Changed = 0`. الترقيم الرسميّ (الخيار 2) يبقى قرارك.

**Owner Decision Required** — إعادة الترقيم في R2 §17، وتثبيت أنّ المرحلة التالية بعد اعتماد W6 هي **R2-W12**.

**الجزء المتوقّف** — لا شيء؛ التعارض لا يحجب التحقّق القرائيّ.

### 5-2. مصفوفة متطلّبات W6 المطبَّقة فعليًّا

| # | المتطلّب (من §4 و§5 و§6 من التصريح + R2) | النتيجة | القسم |
|---|---|---|---|
| 1 | Project Overview يعمل | ✅ | §14 |
| 2 | Project Strategy + Dynamic Attributes | ✅ | §9 |
| 3 | Project Objectives | ✅ | §9 |
| 4 | Project KPIs + Readings | ✅ | §9 |
| 5 | Contract Deliverables | ✅ | §9 |
| 6 | Project Health calculation | ⚠️ محسوبة لا مخزَّنة | §9 · FINDING-W6-03 |
| 7 | Catalog bootstrap / idempotency | ✅ | §13 |
| 8 | Authorization across approved scopes | ⚠️ S-01/S-02 بـ403 لا 404 | §10 · FINDING-W6-04 |
| 9 | Parent-child integrity | ✅ | §9 |
| 10 | لا Project-level orphan KPI creation | ✅ | §9 · §11 |
| 11 | لا تسريب بين المشروعات أو خارج النطاق | ✅ | §10 |
| 12 | توافق Routes/DTOs/HTTP/Error Contracts | ✅ | §11 |
| 13 | عدم كسر Existing Project Routes | ✅ | §11 |
| 14 | عدم إدخال Project Documents | ✅ | §18 |
| 15 | عدم إدخال Tasks/Workstream schema changes | ✅ | §18 |
| 16 | `SourceType` بأربع قيم | ⚠️ ثلاث وفق R2 | FINDING-W6-02 |

---

## 6) `[Code Reality]` — واقع الكود

| # | الفحص | النتيجة |
|---|---|---|
| 1 | `dotnet build Reporting.sln` | **0 Errors** · 12 Warnings (تحذيرات cache NuGet فقط) |
| 2 | `dotnet ef migrations has-pending-model-changes` | `No changes have been made to the model since the last migration.` |
| 3 | عدد الهجرات | **33** · الرأس `20260811142239_AddProject360Foundation` · **لا #34** |
| 4 | عمليّات `Up()` في الهجرة | 13 `AddColumn` + 6 `CreateTable` + 22 `CreateIndex` · **صفر Drop/Alter/Rename** |
| 5 | عمليّات `Down()` | 13 `DropColumn` + 6 `DropTable` + 4 `DropIndex` (متماثلة عكسيًّا) |
| 6 | `DbSet`s المضافة | 6 فقط، كلّها `Projects360` |
| 7 | `Project.cs` + `Decision.cs` | **58 سطر إضافة · صفر حذف** |
| 8 | `ProjectKpi.ObjectiveId` | `Guid` غير قابل للـnull ⇒ **لا مؤشّر يتيم بنيويًّا** |
| 9 | طبقة `Application/Projects360` | **صفر** `using` لـEF أو Infrastructure ⇒ فصل طبقات سليم |
| 10 | تسريب كيانات في الاستجابات | **صفر** — كلّ المسارات تُعيد `Result<TDto>` |
| 11 | تفرّع صلب `SEO/Ads/Social` في `ProjectStrategyService` | **صفر** — المخطّط مُشتقّ من الكتالوج (DEC-W4-02) |
| 12 | مراجع `WorkstreamDeliverable` داخل طبقة Project 360 | **صفر** — عدا تعليق حدوديّ واحد في `ProjectContractDeliverableService.cs:14` |
| 13 | تسجيل DI | 8 تسجيلات كاملة (`DependencyInjection.cs:109–118`) |
| 14 | كتابة إلى `HealthStatus`/`HealthPercent`/`HealthComputedAtUtc` | **صفر** ⇒ FINDING-W6-03 |

---

## 7) الملفّات المتغيّرة في W6

**العدد: صفر.**

| الدليل | القيمة |
|---|---|
| `git status --short` عند بدء W6 | 33 `M` + 138 `??` |
| `git status --short` عند ختام W6 | **33 `M` + 138 `??` — مطابق تمامًا** |
| `git rev-parse HEAD` قبل/بعد | `c157829f750…` بلا تغيّر |
| أحدث `mtime` لأيّ ملفّ `.cs` في `src/` و`tests/` | **2026-08-13 12:33** (`Project360ApiSurfaceTests.cs`) |
| تاريخ جلسة W6 | **2026-08-15** |
| أحدث `mtime` في `reporting-frontend/src` | **2026-07-18 21:11** |

أحدث ملفّ مصدر أقدم من جلسة W6 بيومين كاملين ⇒ **إثبات زمنيّ قاطع أنّ صفر ملفّ كُتِب أثناء W6**.

---

## 8) الملفّات القائمة مسبقًا — المستبعَدة من نسبة W6

الشجرة كانت متّسخة **قبل** W6 وقبل CPW-R3 كلّها (موثَّق في W0 §6). التوزيع الحاليّ:

| المجموعة | العدد | النسبة |
|---|---|---|
| متتبَّع معدَّل — Backend | 23 | 10 منها CPW-R3 (W1–W5) · 13 سابقة لـCPW-R3 |
| متتبَّع معدَّل — Frontend | 10 | **كلّها سابقة** — أحدثها 2026-07-18 |
| غير متتبَّع | 138 | 36 منها CPW-R3 · الباقي تقارير `Docs/Planning` و`Ops/` وملفّات سابقة |
| **W6 Delta** | **0** | — |

**الملفّات السابقة الحاسمة لتصنيف الإخفاقات الخمسة** (كلّها **خارج CPW-R3** وتعود إلى 2026-07-25):
`reporting-backend/src/Reporting.Application/Common/ReportWorkingDaysPolicy.cs` (غير متتبَّع) · `ReportDueService.cs` · `ReportReminderService.cs` · `ReportCalendarService.cs` (معدَّلة).

---

## 9) `[W6 Implementation]` — مصفوفة التكامل

السلسلة الكاملة `Domain → EF → Schema → Services → Authorization → Health → Catalog → API → Serialization/Errors → Tests` أُثبتت طرفًا لطرف:

| الحلقة | الإثبات | الحالة |
|---|---|---|
| Domain → EF | `has-pending-model-changes` = `No changes` | ✅ |
| EF → Schema | 33 هجرة طُبِّقت على قاعدة بكر بلا خطأ · الرأس مطابق | ✅ |
| Schema → Services | 6 خدمات تقرأ/تكتب الجداول الستّة الجديدة · 34 اختبار تكامل | ✅ |
| Services → Authorization | كلّ خدمة تستدعي `LoadVisibleProjectAsync` أوّلًا | ✅ |
| Authorization → Visibility | المصدر الوحيد `IClientProjectAccess` — **صفر توسيع نطاق** | ✅ |
| Health ← Objectives ← KPIs | `RollUpObjectiveScores` على نتائج `BuildAsync` (ترجيح مستويين) | ✅ |
| Catalog → Strategy/Deliverables | `strategy_field` يقيّد السمات · `contract_deliverable` يقيّد الأنواع | ✅ |
| Services → API | 33 مسارًا، كلّها عبر `FromResult` الموحَّد | ✅ |
| API → Error Contract | 25 رمزًا تُخطَّط حتميًّا (§11) | ✅ |
| Health → Persistence | **لا يُكتَب** | ⚠️ FINDING-W6-03 |

### 9-1. سلامة الأب-الابن

| القاعدة | الفرض | الدليل |
|---|---|---|
| المؤشّر تحت هدف إلزامًا (D-02) | `ObjectiveId : Guid` + `KpiObjectiveRequired` | `ProjectKpiService.cs:94` |
| الهدف يتبع المشروع | `o.Id == objectiveId && o.ProjectId == projectId` | `ProjectKpiService.cs:292` |
| هدف من مشروع آخر | 409 `project_kpi.objective_mismatch.conflict` | `ProjectKpiService.cs:303–307` |
| مؤشّر من مشروع آخر | 409 نفسه | `ProjectKpiService.cs:317` |
| مؤشّر من هدف آخر | 409 نفسه | `ProjectKpiService.cs:318` |
| قائمة المؤشّرات مقيّدة بالمشروع | `Where(k => k.ProjectId == projectId)` | `ProjectKpiService.cs:375` |
| مخرَج بهدف من مشروع آخر | 409 `project_deliverable.objective_mismatch.conflict` | `ProjectContractDeliverableService.cs:261` |
| مخرَج بتيّار عمل من مشروع آخر | 409 `project_deliverable.workstream_mismatch.conflict` | `ProjectContractDeliverableService.cs:267` |
| حذف هدف له مؤشّرات | 409 `project_objective.has_kpis.conflict` | اختبار `Objective_DeleteWithKpis_IsRejectedAsConflict` ✅ |
| قراءة مكرّرة لنفس اليوم | 409 `project_kpi_reading.duplicate_date.conflict` | فهرس فريد `(ProjectKpiId, ReadingDate)` |

### 9-2. FINDING-W6-02 — `ProjectKpiSourceType` ثلاث قيم لا أربعًا

**Finding** — §5 من التصريح يفرض `Manual / TaskDerived / IntegrationDerived / Calculated` (**4**). الواقع `Enums.cs:761–769`: `Manual = 0, TaskDerived = 1, Integration = 2` (**3**). وR2 السطر 187 ينصّ حرفيًّا: `public enum ProjectKpiSourceType { Manual = 0, TaskDerived = 1, Integration = 2 }`، وD-06 (R2 س23) يذكر **ثلاث** مراحل.

**Impact** — الكود **مطابق لـR2 تمامًا**. الفارق عن نصّ §5 في: (أ) العدد، (ب) التسمية `Integration` مقابل `IntegrationDerived`. أيّ تعديل = تغيير عمود `varchar(20)` مخزَّن ⇒ **هجرة #34**، وهي محظورة في §1 و§6.

**Options** — (1) اعتماد R2 (3 قيم) وتصحيح صياغة §5. (2) التوسيع إلى 4 قيم بتصريح هجرة لاحق. (3) التأجيل إلى R2-W12.

**Recommendation** — **الخيار 1**. R2 السطر 1140 يضمن أنّ الانتقال بين المصادر = `UPDATE` على صفّ قائم **بلا هجرة ولا فقد تاريخ** ⇒ `Calculated` قابلة للإضافة مستقبلًا بصفر إعادة تصميم، وهو جوهر D-06.

**Owner Decision Required** — تثبيت العدد النهائيّ والتسمية. **الجزء المتوقّف: صفر مساس بالتعداد.**

### 9-3. FINDING-W6-03 — الصحّة لا تُكتَب ولا يوجد `POST /health/recompute`

**Finding** — R2 يفرض ثلاثة أمور غير منجَزة: §1074 (R4) «إعادة احتساب حتميّة داخل كلّ معاملة مؤثّرة + `POST /health/recompute`» · §84 لوحات تنفيذيّة تُجمِّع فوق `projects.HealthPercent` · D-07 (س1318) «الصحّة المخزَّنة تُغذّي سلسلة اللوحات». الواقع: الأعمدة الثلاثة موجودة (`Project.cs:66–72`) لكن **صفر كتابة** إليها، و**لا مسار recompute** ضمن الـ33 مسارًا.

**Impact** — `projects.HealthPercent` يبقى `0` و`HealthStatus` يبقى `Green` أبدًا في القاعدة. أيّ لوحة تنفيذيّة تقرأ العمود مباشرةً ستقرأ **أصفارًا**. **ليس انحدارًا** ولا يكسر مسارًا حاليًّا (`/overview` يقرأ القيمة المحسوبة لحظيًّا)، لكنّه **حاجب لـR2-W12**.

**سياق مهمّ** — قرار **مسجَّل مسبقًا** في W4 §160 («الصحّة تُحسَب حيًّا ولا تُكتب أبدًا»)، لا اكتشاف جديد في W6.

**Options** — (1) إبقاء الاحتساب الحيّ واعتبار الأعمدة مُهيّأة للمستقبل (وتصحيح R2 §84/§1074). (2) تنفيذ الكتابة + مسار recompute في مرحلة لاحقة بتصريح — **صفر هجرة لأنّ الأعمدة موجودة**. (3) حذف الأعمدة ⇒ هجرة متلِفة، **مرفوض**.

**Recommendation** — **الخيار 2 قبل R2-W12**، لأنّ اللوحات التنفيذيّة تعتمد على العمود المخزَّن.

**Owner Decision Required** — نعم. **الجزء المتوقّف: صفر كود صحّة كُتِب.**

---

## 10) مصفوفة التخويل

### 10-1. طبقات الكتابة الثلاث (`Project360Authorization.cs`)

| الطبقة | من يملكها | الكود |
|---|---|---|
| قراءة | كلّ من يرى المشروع عبر `IClientProjectAccess` | س40–42 |
| كتابة بنيويّة (إنشاء/حذف أهداف · مؤشّرات · مخرَجات) | `Roles.ProjectPlanManagers` **حصرًا** | س60–61 |
| كتابة تشغيليّة (تقدّم · حالة · قراءات) | الإدارة **أو** `TeamLeaderId` **أو** `AccountManagerId` **لهذا المشروع بعينه** | س67–72 |

**ترتيب الحرّاس:** مصادقة → رؤية → وجود. `auth.forbidden` يُعاد **قبل** الاستعلام عن الوجود ⇒ منع تسريب الوجود (IDOR/BOLA).

### 10-2. مصفوفة R2 §15-3 — التحقّق بندًا ببند

| # | السيناريو | R2 يتوقّع | الواقع | الحالة |
|---|---|---|---|---|
| S-01 | مستخدم لا يرى المشروع ⟵ أيّ مسار | **404** | **403** `auth.forbidden` (لا يُميَّز عن غير الموجود) | ⚠️ FINDING-W6-04 |
| S-02 | مدير عميل مشروع آخر ⟵ قراءة مؤشّر | **404** | **403** نفسه | ⚠️ FINDING-W6-04 |
| S-03 | يرى ولا يملك ⟵ `POST readings` | 403 | 403 | ✅ `Employee_InOwnerTeam_CanRead_ButCannotWriteAnyTier` |
| S-04 | قائد فريق المشروع ⟵ `POST readings` | 200 | 200 | ✅ `TeamLeader_CanUpdateOperationalState_…` |
| S-05 | مدير عميل المشروع ⟵ `PATCH deliverables/{id}/progress` | 200 | 200 | ✅ `AccountManager_CanUpdateOperationalState_…` |
| S-06 | قائد الفريق ⟵ `POST objectives` | 403 | 403 | ✅ `…ButCannotManageStructure` |
| S-07 | مؤشّر يخصّ هدفًا آخر ⟵ مسار الهدف الأوّل | **404** | **404** | ✅ |
| S-08 | هدف يخصّ مشروعًا آخر ⟵ مسار هذا المشروع | **404** | **404** | ✅ `Idor_ForeignChildResource_…` |
| S-09 | `ObjectiveId` من مشروع مختلف عند الإنشاء | **409** | **409** | ✅ (RM-01 المعتمد) |
| S-10 | غير مصادَق ⟵ أيّ مسار | 401 | 401 `auth.unauthenticated` | ✅ |

**النتيجة: 8/10 مطابقة حرفيًّا · 2 انحراف واحد مشترك.**

### 10-3. FINDING-W6-04 — S-01/S-02 تُعيدان 403 لا 404

**Finding** — R2 §15-3 (س1226–1227) يفرض **404** للمشروع خارج النطاق. الكود يُعيد **403** `auth.forbidden` (`Project360Authorization.cs:42`). الاختبار `Idor_ExistingOutOfScopeAndNonExistent_AreIndistinguishable` يؤكّد صراحةً `HttpStatusCode.Forbidden` لكلا الحالتين.

**Impact** — **هدف مكافحة العدّ مُحقَّق بالكامل**: «موجود لكن ممنوع» و«غير موجود إطلاقًا» يُعادان بنفس الحالة ونفس الرمز بالضبط ⇒ لا يستطيع المهاجم عدّ المشاريع ولا التمييز. الانحراف **في الرقم لا في الأثر الأمنيّ**. لكنّه يخالف نصّ R2 حرفيًّا، ويخالف عرف المستودع المسجَّل «الوصول المرفوض للمستندات يُرجِع 404 لا 403» ⇒ **تنافر بين موديولين** في نفس النظام.

**Options** — (1) تعديل R2 §15-3 لاعتماد 403 المتّسق (توثيق فقط، صفر كود). (2) تغيير `auth.forbidden` إلى `project.not_found` لمسارات Project 360 (تعديل كود + تعديل 3 اختبارات) — يُعيد التوافق مع R2 وعرف المستندات. (3) الإبقاء مع تسجيل انحراف معتمَد صريح.

**Recommendation** — **الخيار 2 قبل R2-W12**، لأنّ الواجهة ستعالج الحالتين، والاتّساق بين موديولَي «المستندات» و«Project 360» يمنع سلوكًا متناقضًا أمام المستخدم نفسه. **لا يُنفَّذ داخل W6** (يتطلّب كتابة كود، وW6 قرائيّة).

**Owner Decision Required** — نعم. **الجزء المتوقّف: صفر مساس بطبقة التخويل.**

---

## 11) التحقّق من عقود API وDTO والأخطاء

### 11-1. جرد المسارات

| المجموعة | العدد | الحالة |
|---|---|---|
| `ProjectsController` (قائم قبل CPW-R3) | **9** | مطابقة حرفيًّا لقائمة W0 المجمَّدة · **صفر تعديل على الملفّ** |
| `ProjectObjectivesController` | 8 | جديد |
| `ProjectKpisController` | 10 | جديد |
| `ProjectContractDeliverablesController` | 8 | جديد |
| `ProjectStrategyController` | 3 | جديد |
| `ProjectGovernanceReadController` | 3 | جديد (قراءة فقط) |
| `ProjectOverviewController` | 1 | جديد |
| **إجمالي الجديد** | **33** | — |

```
EXISTING PROJECT ROUTES REMOVED  = 0
EXISTING PROJECT ROUTES CHANGED  = 0
PROJECT_LEVEL_KPI_CREATE_ROUTE   = NONE
```

**فرض D-02 على مستوى التوجيه:** `GET api/projects/{projectId}/kpis` هو **المسار الوحيد** على مستوى المشروع وهو **قراءة فقط**. كلّ عمليّات الإنشاء/التعديل/التفعيل تحت `objectives/{objectiveId}/kpis` حصرًا ⇒ **المؤشّر اليتيم مستحيل عبر الـAPI بنيويًّا**، ويؤكّده `ProjectLevelKpiRoute_IsReadOnly_NoCreateRouteExists` ✅.

### 11-2. عقد الأخطاء

جميع المسارات الـ33 تمرّ عبر مُخطِّط واحد مشترك `ApiControllerBase.FromResult` → `ToProblem` (س15–37): 8 + 3 + 10 + 8 + 1 + 3 = **33 استدعاءً** ⇒ صفر تباين بين وحدات التحكّم.

| الفئة | العدد | HTTP |
|---|---|---|
| `*.not_found` | 5 | **404** |
| `*.conflict` | 7 | **409** |
| تحقّق (`*_invalid` · `*_required` · `field_not_applicable`) | 13 | **400** |
| `auth.forbidden` | — | **403** |
| `auth.unauthenticated` | — | **401** |
| **الإجمالي** | **25 رمزًا** | حتميّة بلا استثناء |

الاختبار `ErrorCodes_MapToProvenHttpStatuses` يثبتها على المسار الحيّ ✅.

### 11-3. سلامة DTO والتسلسل

- **صفر** كيان EF يعبر حدود الـAPI — كلّ الاستجابات `Result<TDto>` بسجلّات `record` من `Project360Models.cs`.
- **صفر** `using Microsoft.EntityFrameworkCore` أو `Reporting.Infrastructure` في `Reporting.Application/Projects360` ⇒ فصل طبقات Clean Architecture سليم.
- ملفّات العقد الخمسة: `IProject360Authorization.cs` · `IProject360Services.cs` · `Project360Codes.cs` · `Project360Models.cs` · `ProjectHealthPolicy.cs` — وهي **بعينها** مخرَج R2-W6.

---

## 12) التحقّق من القاعدة والهجرات

| البند | النتيجة |
|---|---|
| إنشاء قاعدة معزولة `reporting_test_w6iso` | ✅ `createdb` |
| الهجرات المطبَّقة عليها | **33** |
| رأس الهجرات في القاعدة | `20260811142239_AddProject360Foundation` |
| هجرة #34 | **غير موجودة** |
| تعديل هجرة قائمة | **صفر** |
| تعديل `AppDbContextModelSnapshot` أثناء W6 | **صفر** (`mtime` 2026-08-13) |
| `has-pending-model-changes` | `No changes have been made to the model since the last migration.` |
| قاعدة خطّ الأساس `reporting_test_w6base` | **32** هجرة (بلا `AddProject360Foundation`) — يؤكّد أنّ الهجرة هي الدلتا الوحيدة |
| المساس بـ`reporting_test` | **صفر** — لم يُنفَّذ عليها أيّ استعلام كتابة ولا قراءة تحقّقيّة |
| المساس بـTEST/RC/Production | **صفر مطلق** |

---

## 13) idempotency الكتالوج

| القياس | العدد |
|---|---|
| `strategy_section` | **6** |
| `strategy_field` | **14** |
| `contract_deliverable` | **18** |
| **الإجمالي** | **38** |

**Run 1** (أوّل إقلاع على قاعدة بكر): 38 قيمة أُنشئت.
**Run N** (البذّار عمل مرّة لكلّ `WebApplicationFactory` عبر الانحدار الكامل — مئات المرّات):

```
contract_deliverable | 18      ← بلا تغيّر
strategy_field       | 14      ← بلا تغيّر
strategy_section     |  6      ← بلا تغيّر
DUPLICATE (Domain,Code) COUNT = 0
```

**آليّة الضمان** (`ExecutionTaxonomySeeder`): `if (existing.Contains((def.Domain, def.Code))) continue;` — إدراج فقط، **صفر تحديث لصفّ قائم**، ⇒ صفر تكرار وصفر تحديث غير ضروريّ.

**ملاحظة تشخيصيّة مسجَّلة:** أوّل قياس أعطى 7/16/19 = 42 لأنّ `Project360FoundationTests` كان قد أدرج 4 قيم تجهيزيّة بادئتها `w4_test_` وهي **تبقى في القاعدة** بعد الاختبار. بعد استبعادها العدد **38 بالضبط** مطابقًا لـW5. هذا سلوك تجهيزات اختبار لا عيب منتج، لكنّه جدير بالتسجيل لأنّه يُربك أيّ عدّ مستقبليّ.

---

## 14) `/overview` والتحقّق من الاستعلامات

| البند | النتيجة |
|---|---|
| D-05 نداء واحد | `GET /api/projects/{projectId}/overview` — مسار واحد يملأ اللوحة كاملة |
| عدد استعلامات SQL | **ثابت = 12** مهما بلغ عدد الأهداف/المؤشّرات/المخرَجات |
| الاختبار | `Overview_SqlQueryCount_IsConstant_RegardlessOfObjectiveCount` ✅ |
| صفر N+1 | **مُثبَت** — لا استعلام داخل حلقة، لا `Include` متشعّب، كلّ التجميعات في الذاكرة على صفوف مجلوبة مرّة |
| الترجيح على مستويين (DEC-W4-03) | **65** لا 66.67 المسطّح · `Overview_TwoLevelWeightedAggregation_Returns65_NotFlatAverage` ✅ |
| اتّساق الرقم الواحد | `objectives.AverageAchievementPercent == kpis.AverageAchievementPercent == health.kpiScore` — رقم واحد للمفهوم الواحد عبر اللوحة |
| القراءة خالصة | `AsNoTracking()` في كلّ الاستعلامات العشرة · صفر كتابة أثناء العرض |
| مشروع بلا أيّ مكوّن | **غير مُقيَّم** لا صفر · `Overview_ProjectWithoutAnyComponent_IsNotEvaluated_NotZero` ✅ |

---

## 15) جرد الاختبارات ونتائجها

### 15-1. الأرقام الكلّيّة

| المجموعة | البيئة | إجمالي | ناجح | فاشل | المدّة |
|---|---|---|---|---|---|
| Unit | معزولة | **137** | **137** | 0 | 14 مل.ث |
| Integration | **قاعدة نظيفة معزولة** | **1461** | **1455** | **6** | **4 د 26 ث** |
| Integration (مرجع W5) | `reporting_test` المشتركة | 1461 | 1438 | 23 | 1 س 48 د |

### 15-2. اختبارات Project 360 — 34/34 ناجحة

**`Project360ApiSurfaceTests` (14/14)**
`CatalogBootstrap_SecondRun_CreatesNothing_AndUpdatesNothing` · `StrategySchema_IsServiceScoped_ByCatalogDataOnly` · `StrategyUpsert_FieldOutsideProjectService_IsRejectedWith400` · `ProjectLevelKpiRoute_IsReadOnly_NoCreateRouteExists` · `Overview_TwoLevelWeightedAggregation_Returns65_NotFlatAverage` · `Overview_SqlQueryCount_IsConstant_RegardlessOfObjectiveCount` · `AuthorizationMatrix_ReadTier_AcrossAllSixResources` · `AuthorizationMatrix_StructuralWrite_IsManagementOnly` · `AuthorizationMatrix_OperationalWrite_ReachesOwnersButNotPlainEmployee` · `Idor_ExistingOutOfScopeAndNonExistent_AreIndistinguishable` · `Idor_ForeignChildResource_IsNotReachableThroughVisibleProject` · `ErrorCodes_MapToProvenHttpStatuses` · `ContractDeliverables_HaveTheirOwnRouteAndCatalog_WithoutTouchingWorkstreamDeliverables` · `GovernanceRead_IsProjectFiltered_AndWriteless`

**`Project360FoundationTests` (20/20)**
`Strategy_Upsert_Then_Get_RoundTrips_And_SyncsAttributesDifferentially` · `Strategy_UnknownFieldCode_IsRejected` · `Strategy_UnknownSectionCode_IsRejected` · `Strategy_DuplicateFieldCode_IsConflict` · `Strategy_Schema_ExposesElevenCoreFields_AndCatalogDrivenDynamicFields` · `Objective_Crud_RoundTrips_AndProgressIsDerivedNotStored` · `Objective_DeleteWithKpis_IsRejectedAsConflict` · `Kpi_IsAlwaysCreatedUnderObjective_WithMatchingProjectId` · `Kpi_TargetValueNotPositive_IsRejected` · `Kpi_ObjectiveFromAnotherProject_IsConflict` · `Reading_Added_UpdatesSnapshot_AndRejectsDuplicateDate` · `Reading_OnNonManualKpi_IsConflict` · `ContractDeliverable_Crud_RoundTrips_AndValidatesTypeCode` · `ContractDeliverable_ObjectiveFromAnotherProject_IsConflict` · `TeamLeader_CanUpdateOperationalState_ButCannotManageStructure` · `AccountManager_CanUpdateOperationalState_ButCannotManageStructure` · `Employee_InOwnerTeam_CanRead_ButCannotWriteAnyTier` · `ProjectOutsideVisibility_IsForbidden_BeforeExistenceIsRevealed` · `Overview_AggregatesEverySection_AndReflectsComputedHealth` · `Overview_ProjectWithoutAnyComponent_IsNotEvaluated_NotZero`

---

## 16) أدلّة القاعدة النظيفة المعزولة

### 16-1. الإجراء غير المتلِف

1. `rsync -a --exclude bin/ --exclude obj/ --exclude .git/` للشجرة إلى `/tmp/w6-iso`.
2. `sed` على **النسخة فقط**: `CustomWebApplicationFactory.cs:16` و`Project360ApiSurfaceTests.cs:30` ⟵ `reporting_test_w6iso`.
3. تحقّق فوريّ أنّ الشجرة الحقيقيّة لم تتغيّر (`git status` مطابق).
4. `createdb reporting_test_w6iso` ⟶ تحقّق `COUNT = 1`.
5. `dotnet build` (0 أخطاء) ⟶ تشغيل يُطلق الهجرات الـ33 والبذّارات.
6. تصحيح `Classification='Supplementary'` لخمسة قوالب (**بيئة اختبار حصرًا** — لا يُمَسّ `TemplateSeeder` ولا الإنتاج).
7. الانحدار الكامل ⟶ قياس الكتالوج بعده ⟶ فحص التكرارات.

### 16-2. تجربة خطّ الأساس (الحاسمة)

`git archive c157829f750… reporting-backend | tar -x -C /tmp/w6-base` ⟶ شجرة خطّ الأساس **بلا أيّ ملفّ CPW-R3** (`Projects360/` غير موجود · `ReportWorkingDaysPolicy.cs` غير موجود) · **32 هجرة** ⟶ قاعدة `reporting_test_w6base` مستقلّة.

### 16-3. تنظيف مُثبَت

| الإجراء | التحقّق |
|---|---|
| `dropdb reporting_test_w6iso` | `psql -lqt \| grep -c` = **0** |
| `dropdb reporting_test_w6base` | `psql -lqt \| grep -c` = **0** |
| `rm -rf /tmp/w6-iso` | `No such file or directory` |
| `rm -rf /tmp/w6-base` | `No such file or directory` |
| `reporting_test` المشتركة | **ما زالت موجودة سليمة** |
| الشجرة الحقيقيّة | 33 `M` + 138 `??` · HEAD `c157829f750…` — بلا تغيّر |

---

## 17) تصنيف الإخفاقات

### 17-1. مصفوفة التقاطع الحاسمة

| # | Test Class · Method | Assertion | Shared DB | Clean Isolated (المرشَّح) | **Clean Isolated (خطّ الأساس `c157829`)** | التصنيف |
|---|---|---|---|---|---|---|
| 1 | `ComplianceDueLateTests.DailySales_PartialDays_LateAndMissingOverdue` | `Expected 5 / Actual 6` | FAIL | FAIL | **PASS** | **Pre-existing Dirty Worktree** |
| 2 | `ComplianceDueLateTests.DailySales_AllWorkingDays_FullCompliance` | `Expected 5 / Actual 6` | FAIL | FAIL | **PASS** | **Pre-existing Dirty Worktree** |
| 3 | `ComplianceDueLateTests.DailySales_SubmittedAfterDay_IsLateSubmitted` | `Expected 1 / Actual 2` | FAIL | FAIL | **PASS** | **Pre-existing Dirty Worktree** |
| 4 | `ComplianceDueLateTests.DailySales_Draft_DoesNotCountAsSubmitted` | `Expected 5 / Actual 6` | FAIL | FAIL | **PASS** | **Pre-existing Dirty Worktree** |
| 5 | `ReportRemindersTests.Generate_DailyOverdue_PastWeek_CreatesRowPerWorkingDay` | `Expected 5 / Actual 6` | FAIL | FAIL | **PASS** | **Pre-existing Dirty Worktree** |
| 6 | `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete` | `Expected OK / Actual NotFound` | PASS | FAIL (أوّل تشغيل) · PASS (إعادة) | **FAIL (أوّل تشغيل) · PASS (إعادة)** | **Baseline Defect** (`BASELINE-DEFECT-01`) |

### 17-2. تفصيل السبب الجذريّ

**المجموعة أ — الإخفاقات 1–5 · Pre-existing Dirty Worktree**

- **الأمر المُعيد للإنتاج:** `dotnet test tests/Reporting.IntegrationTests --filter "FullyQualifiedName~ComplianceDueLateTests|FullyQualifiedName~ReportRemindersTests"`
- **النتيجة على خطّ الأساس:** `Passed! - Failed: 0, Passed: 60, Total: 60` (مع `AdminGovernanceTests`) ⇒ **تنجح جميعًا**.
- **النتيجة على المرشَّح منفردة:** `Failed! - Failed: 5, Passed: 40, Total: 45` ⇒ **تفشل بنفس التأكيدات** بلا اعتماد على ترتيب.
- **السبب:** `ReportWorkingDaysPolicy.SaturdayApplicabilityFloor = 2026-07-25` يجعل السبت يوم عمل متوقَّعًا لأدوار المبيعات من W31، بينما الاختبارات ما زالت تتوقّع 5 أيّام.
- **الملفّات المسؤولة (كلّها خارج CPW-R3، بتاريخ 2026-07-25):** `ReportWorkingDaysPolicy.cs` (غير متتبَّع) · `ReportDueService.cs` · `ReportReminderService.cs` · `ReportCalendarService.cs` (معدَّلة).
- **علاقتها بملفّات W6:** **صفر** — W6 لم تُنشئ ولا تعدّل أيّ ملفّ.
- **علاقتها بملفّات CPW-R3 (W1–W5):** **صفر** — لا يستورد أيّ منها `Projects360`.

**المجموعة ب — الإخفاق 6 · Baseline Defect**

- **الأمر المُعيد للإنتاج:** `dotnet test --filter "FullyQualifiedName~AdminGovernanceTests"` على قاعدة **بكر**.
- **خطّ الأساس:** يفشل بـ`Expected OK / Actual NotFound` على أوّل تشغيل ضدّ قاعدة بكر · ينجح 15/15 على إعادة التشغيل.
- **المرشَّح:** **سلوك مطابق تمامًا** — يفشل داخل الانحدار الكامل، وينجح 15/15 على إعادة التشغيل.
- **الاستنتاج:** الدلتا بين خطّ الأساس والمرشَّح **صفر**. هذا `BASELINE-DEFECT-01` المسجَّل مسبقًا، ويُضاف إليه أنّه **معتمد على حالة أوّل تهيئة للقاعدة**.
- **علاقته بـProject 360:** **صفر** — يختبر تدفّق تعليق/إعادة فتح تقرير لدور HR.

### 17-3. الحصيلة

```
Candidate Regression         : 0
Baseline Defect              : 1
Pre-existing Dirty Worktree  : 5
Shared-State / Order Dependent: 0   (على قاعدة نظيفة — الـ18 المسجَّلة في W5 اختفت كلّها)
Environment / Infrastructure : 0
Flaky / Timeout              : 0   (صفر TimeoutException على قاعدة نظيفة)
Unresolved                   : 0
```

**تحقّق مضادّ لـW5:** الـ18 إخفاقًا التي صنّفها W5 «Shared-State» (`EmailNotificationsUiTests` 13 · `ReportTemplateAssignmentTests` 2 · `V102TemplateAdminTests` 2 · `ReportReminderSchedulerTests` 1) **نجحت جميعًا** هنا ⇒ تصنيف W5 كان صحيحًا وسبب التلوّث هو تضخّم `reporting_test` لا الكود.

**ملاحظة إيجابيّة:** `BASELINE-DEFECT-02` (`EmployeeProfileScopeTests.Profile_Summary_Reflects_Submitted_Kpi`) **نجح** على القاعدة النظيفة ⇒ هو أيضًا مشتبه به «تلوّث بيئة» ويستحقّ إعادة تقييم في تذكرة البيئة.

---

## 18) تدقيق النطاق

| البند المحظور | النتيجة | الدليل |
|---|---|---|
| تعديلات Frontend | **صفر** | 10 ملفّات معدَّلة + 1 غير متتبَّع، **كلّها بتاريخ ≤ 2026-07-18** أي قبل CPW-R3 بأسابيع؛ ومطابقة لقائمة W0 §6 حرفيًّا |
| Project Documents | **صفر** | لا ملفّ كود يطابق `Document`؛ المطابقات الثلاث كلّها `Docs/Planning/*.md` |
| Task Management | **صفر** | لا ملفّ كود يطابق `TaskManagement` |
| Milestones | **صفر** | لا مطابقة |
| Workstream schema | **صفر** | `ProjectWorkstreamsController.cs` و`WorkstreamDeliverablesController.cs` **غير معدَّلين**؛ المرجع الوحيد تعليق حدوديّ |
| Workflow / CRM | **صفر** | لا مطابقة |
| Employee KPI | **صفر** | كيانات Project 360 منفصلة تمامًا عن منظومة مؤشّرات الموظّفين (تعليق `AppDbContext`) |
| `ProjectsController` القائم | **صفر مساس** | غير مُدرَج في `git status` · 9 مسارات سليمة |
| هجرات بعد W3 | **صفر** | 33 هجرة، الرأس `20260811142239` |
| Commit / Push / Merge / PR / Tag | **صفر** | HEAD `c157829f750…` بلا تغيّر |
| نشر TEST / UAT / RC / Production | **صفر** | لم يُنفَّذ أيّ `ssh` ولا `systemctl` ولا `rsync` إلى خادم |
| المساس بـ`reporting_test` | **صفر** | كلّ العمل على `reporting_test_w6iso` و`reporting_test_w6base` المحذوفتين |
| تنظيف/تدوير `reporting_test` | **صفر** | لم يُنفَّذ عليها أيّ `DELETE`/`TRUNCATE`/`VACUUM` |
| إصلاح عيوب Baseline | **صفر** | العيوب موثَّقة لا مُعالَجة |

---

## 19) المخاطر وFindings

| # | Finding | الشدّة | حاجب لـW6؟ | حاجب لما بعدها؟ |
|---|---|---|---|---|
| **W6-01** | تعريف R2 لـ`W6` ≠ Integration Gate؛ الترقيم المنفَّذ مضغوط | متوسّطة (حوكمة) | **لا** | يستلزم قرار الترقيم قبل بدء المرحلة التالية |
| **W6-02** | `ProjectKpiSourceType` 3 قيم (مطابق R2) مقابل 4 في نصّ §5 | منخفضة | **لا** | يستلزم قرارًا قبل أيّ توسيع مصادر |
| **W6-03** | الصحّة لا تُكتَب · لا `POST /health/recompute` | **عالية** | **لا** | **حاجب لـR2-W12** (اللوحات التنفيذيّة ستقرأ أصفارًا) |
| **W6-04** | S-01/S-02 تُعيدان 403 بدل 404 المطلوب في R2 §15-3 | متوسّطة | **لا** | يُفضَّل حسمه قبل R2-W12 (اتّساق مع موديول المستندات) |

**مخاطر خارج نطاق W6 (لا تُعالَج هنا):**
- `BASELINE-DEFECT-01` — قائم على خطّ الأساس بدلتا صفريّة.
- إعادة تسجيل الإخفاقات الخمس تحت **Pre-existing Dirty Worktree** بدل `BASELINE-BE-DEFECT-03`.
- دَين البيئة: تضخّم `reporting_test` (`email_notifications` 10,292,083 · `AspNetUsers` 136,323) — الأثر مُقاس بدقّة هنا: **1 س 48 د ⟶ 4 د 26 ث** و**23 إخفاقًا ⟶ 6**.
- تراكم قواعد `reporting_*` المؤقّتة (36 قاعدة) من تذاكر سابقة — خارج نطاق W6.

---

## 20) ملاحظات التراجع/الإزالة لدلتا W6 غير المُلتزَمة

**دلتا W6 = صفر ⇒ لا يوجد ما يُتراجَع عنه.**

| البند | الحالة |
|---|---|
| ملفّات كُتِبت في الشجرة | **0** (عدا هذا التقرير `Docs/Planning/CPW-R3-...-W6-INTEGRATION-GATE-REPORT.md`) |
| قواعد بيانات أُنشئت | 2 — **كلتاهما محذوفة ومُتحقَّق من حذفها** |
| نسخ شجرة مؤقّتة | 2 — **كلتاهما محذوفة ومُتحقَّق من حذفها** |
| ملفّات `/tmp` مساعدة | `/tmp/brd-v11.txt` (استخراج BRD) — خارج المستودع تمامًا |
| إجراء التراجع الوحيد الممكن | حذف ملفّ هذا التقرير |

**دلتا CPW-R3 (W1–W5) غير المُلتزَمة** باقية كما هي بلا مساس: 10 ملفّات متتبَّعة معدَّلة + 36 مسارًا غير متتبَّع + الهجرة `20260811142239_AddProject360Foundation`. إزالتها — إن طُلبت يومًا — تتمّ بحذف المسارات غير المتتبَّعة و`git checkout` للملفّات العشرة، **ولا تُنفَّذ إلّا بتصريح صريح** لأنّها ستمسّ شجرة عمل المستخدم.

---

## 21) قرار البوّابة النهائيّ

| معيار §10 | مطلوب | النتيجة |
|---|---|---|
| متطلّبات W6 في R2 مكتملة | نعم | ✅ (R2-W6 = DTOs + واجهات، مُنجَزة في W4، Build = 0) |
| Candidate Regression | 0 | ✅ **0** |
| Unresolved | 0 | ✅ **0** |
| اختبارات Project 360 على قاعدة نظيفة | ناجحة | ✅ **34/34** |
| Authorization contract مثبت | نعم | ✅ 8/10 حرفيًّا · انحراف 403/404 مُوثَّق بـFINDING-W6-04، والأثر الأمنيّ محقَّق |
| Existing Project Routes | لم تُكسر | ✅ 9/9 سليمة · REMOVED = 0 · CHANGED = 0 |
| Project-level KPI create route مخالف | لا يوجد | ✅ `NONE` |
| Catalog idempotent | نعم | ✅ 38 ثابتة · تكرارات = 0 |
| Model Sync نظيف | نعم | ✅ `No changes…` |
| Migration count | 33 بلا #34 | ✅ **33** |
| Scope Audit | نظيف | ✅ 13 بندًا صفريًّا |
| تغييرات Frontend | صفر | ✅ |
| Project Documents | صفر | ✅ |
| Task Management | صفر | ✅ |
| مساس بالبيئات المشتركة/المنشورة | صفر | ✅ |

```
W6 = GO

Candidate Regression:            0
Baseline Defects:                1   (BASELINE-DEFECT-01 — دلتا صفريّة مُثبَتة على c157829)
Shared-State / Environment:      5   (Pre-existing Dirty Worktree — تنجح على c157829، ملفّات 2026-07-25 خارج CPW-R3)
Unresolved:                      0

Ready for Next CPW-R3 Gate: GO   (مشروط بقرار المالك في FINDING-W6-01 حول الترقيم)
Ready for Frontend:         NO-GO (FINDING-W6-03 حاجب: اللوحات التنفيذيّة ستقرأ أصفارًا)
Ready for Commit:           NO-GO
Ready for Push:             NO-GO
Ready for TEST Deployment:  NO-GO
Ready for RC/Production:    NO-GO
```

---

## 22) بيان التوقّف الصريح

**توقّفت الآن.**

لن أبدأ أيّ مرحلة تالية، ولن ألمس Frontend، ولن أُنشئ هجرة، ولن أُعدّل مخطّطًا، ولن أُصلح عيب Baseline، ولن أنظّف `reporting_test`، ولن أُنفّذ Commit أو Push أو Merge أو PR أو Tag، ولن أنشر على TEST أو UAT أو RC أو Production — **حتّى يعتمد المالك هذا التقرير ويصدر تصريحًا صريحًا جديدًا لكلّ عمليّة على حدة.**

**أربعة قرارات مطلوبة من المالك قبل المرحلة التالية:**

1. **FINDING-W6-01** — إعادة ترقيم المراحل في R2 §17، وتثبيت أنّ المرحلة التالية هي R2-W12.
2. **FINDING-W6-02** — تثبيت عدد قيم `ProjectKpiSourceType` وتسميتها (التوصية: اعتماد R2 بثلاث قيم).
3. **FINDING-W6-03** — كتابة الصحّة المخزَّنة + `POST /health/recompute` (التوصية: التنفيذ قبل R2-W12 بصفر هجرة).
4. **FINDING-W6-04** — 403 مقابل 404 لـS-01/S-02 (التوصية: التوحيد على 404 قبل R2-W12).

**بندان مؤجَّلان بتصريح مستقلّ (لم يُفتحا ولم يُعدَّلا):** إعادة تسجيل الإخفاقات الخمس تحت `Pre-existing Dirty Worktree`، وتذكرة دَين البيئة لتدوير `reporting_test`.
