# PROJECT360-WORKFLOW-R1 — 01 — تدقيق الوضع القائم (AS-IS)

**التذكرة:** `PROJECT360-OPERATIONAL-WORKFLOW-CLOSURE-R1` · **التاريخ:** ١٧ أغسطس ٢٠٢٦ · **الطبيعة:** قراءة وتحليل فقط — صفر تعديل شيفرة · صفر هجرة · صفر التزام · صفر نشر · صفر كتابة على أيّ قاعدة.

---

## 0) قاعدة الحكم المطبَّقة في هذا التقرير

| المصدر | وزنه |
|---|---|
| **الكود الحاليّ + سلوك TEST الحيّ** | **حقيقة الإصدار الحاليّ** — الحكم النهائيّ |
| **BRD** | الرؤية والهدف المطلوب — يُقاس عليه الانحراف لا الواقع |
| **الدليل المصوَّر ومرجع UAT** | شاهد على السلوك والفجوات — **ليس بديلًا عن فحص الكود** |

كلّ تعارض بين الثلاثة **مسجَّل صراحةً** في التقرير 02 ولم يُحسَم بالتخمين.

---

## 1) المرحلة A — خطّ الأساس المُثبَت

### 1-1 المستودع المحلّيّ

| البند | القيمة | كيف أُثبت |
|---|---|---|
| الفرع | `develop` | `git rev-parse --abbrev-ref HEAD` |
| `HEAD` | `c9c0cb249f78e6337207ebcf1ae3a1e710db16ee` | `git rev-parse HEAD` |
| `origin/develop` | `c9c0cb249f78e6337207ebcf1ae3a1e710db16ee` — **مطابق تمامًا** | `git rev-parse origin/develop` |
| `origin/main` | `508509ad8474b321c80cbdd48eb84ecb54bee212` — **لم يتحرّك** | `git rev-parse origin/main` |
| نظافة الشجرة | **`git status --short` = 0 سطر** | عدّ الأسطر |
| مخبّأ (stash) | واحد سابق للتذكرة، لم يُمسّ | — |

### 1-2 البيئات الثلاث

| البيئة | الخدمة | القاعدة | الهجرات | الحالة | ملاحظة الإثبات |
|---|---|---|---|---|---|
| TEST | `khubara-reporting-test` (5091) | `reporting_test_uat` | **38** — الرأس `20260811142239_AddProject360Foundation` | نشِطة | `Reporting.Api.dll` = 434,176 بايت · 16 أغسطس 16:27 · حزمة الواجهة `index-pKBm-h-L.js` |
| RC | `khubara-reporting-rc` (5092) | `reporting_rc` | **40** | نشِطة · `NRestarts=0` · قائمة منذ الأحد 16 أغسطس 18:35:15 UTC | **لم تُمسّ في هذه التذكرة** |
| الإنتاج | `reporting-api` (5090) | `reporting_prod` | **30** | نشِطة · `NRestarts=0` · قائمة منذ الجمعة 7 أغسطس 08:57:45 UTC | **لم تُمسّ في هذه التذكرة** |

### 1-3 ملفّات الدليل الثلاثة — موجودة ومطابقة

| الملفّ | الحجم |
|---|---|
| `Docs/Guides/Marketing-Experts-Client360-Project360-Operating-and-UAT-Guide-AR-R1.docx` | 12,490,367 |
| `Docs/Guides/…-Operating-and-UAT-Guide-AR-R1.pdf` | 10,165,164 |
| `Docs/Guides/Marketing-Experts-Client360-Project360-UAT-Workbook-AR-R1.docx` | 100,953 |

**قُرئت كاملة** (نصًّا عبر `pdftotext -layout` — 6,443 سطرًا — ومصدرًا عبر `Docs/Guides/builders/*.mjs`)، مع تركيز على: الفصل 8 · الفصل 9 · الفصل 12 · حزمة `UAT-I` · ملحق حدود النظام · حقول التنفيذ في الحالات التسعين.

### 1-4 مهامّ خلفيّة عالقة
لا يوجد. جميع المهامّ الخلفيّة من الجلسة السابقة منتهية أو مُنهاة، ولم تُشغَّل أيّ مهمّة كتابة.

---

## 2) الحقيقة البنيويّة — سلسلة العمل كما هي فعلًا في الكود

### 2-1 كيان المشروع — الحقول الحاكمة

`reporting-backend/src/Reporting.Domain/Entities/Clients/Project.cs`

| الحقل | السطر | النوع | الحالة الفعليّة |
|---|---|---|---|
| `AccountManagerId` | 24 | `Guid?` | **قابل للكتابة** من الواجهة والـAPI |
| `ProjectOwnerId` | 36 | `Guid?` | مخزَّن · معروض · **لا مسار كتابة** |
| `TeamLeaderId` | 39 | `Guid?` | مخزَّن · مستعمَل في التفويض · **لا مسار كتابة** |
| `ProgressPercent` | 42 | `decimal` | موصوف نصًّا بأنّه «المعلَنة **يدويًّا** … Manual-First» — **ولا يوجد أيّ موضع كتابة في الشيفرة كلّها** |
| `HealthStatus` | 66 | `ProjectHealthStatus` | **قيمته الافتراضيّة `Green`** |
| `HealthPercent` | 69 | `decimal` | يُكتَب من `SaveWithHealthAsync` |
| `HealthComputedAtUtc` | 72 | `DateTime?` | `NULL` ⟹ لم تُحتسَب بعد |

### 2-2 إثبات «صفر مسار كتابة» لـ`Project.ProgressPercent`

بحث شامل على `ProgressPercent\s*=` في `reporting-backend/src` أعاد **نتيجتَين فقط**:
1. `Migrations/20260811142239_AddProject360Foundation.cs:188` — تعريف العمود (DDL).
2. `Services/ProjectContractDeliverableService.cs:176` — وهو `ProjectDeliverable.ProgressPercent` **لا** `Project.ProgressPercent`.

⟹ **لا توجد نقطة نهاية واحدة، ولا خدمة واحدة، ولا مهمّة خلفيّة واحدة تكتب نسبة تقدّم المشروع.** الحقل مُعرَّف ليُملأ يدويًّا، ومدخل الملء لم يُبنَ إطلاقًا.

**تأكيد من البيانات الحيّة (TEST · قراءة فقط):** من **9 مشاريع**، عدد المشاريع بـ`ProgressPercent <> 0` = **صفر**.

### 2-3 إثبات «صفر إسناد» لقائد الفريق ومالك المشروع

| السلسلة | الحالة | الإثبات |
|---|---|---|
| الدومين | ✅ موجود | `Project.cs:36,39` |
| قاعدة البيانات | ✅ موجود + فهرسان | الهجرة `…AddProject360Foundation.cs:71-75, 98-102, 319-321, 324-326` |
| `CreateProjectRequest` / `UpdateProjectRequest` | ❌ **غائبان** | `Application/Clients/ProjectModels.cs:25-34, 36-44` — `AccountManagerId` فقط |
| `ProjectDto` (الإرجاع) | ❌ **غائبان** | `ProjectModels.cs:6-23` |
| `ProjectsController` | ❌ لا مسار | `Api/Controllers/ProjectsController.cs:37-45` — ولا نقطة نهاية مستقلّة للإسناد |
| `ProjectService.CreateAsync` / `UpdateAsync` | ❌ لا يكتبهما | `ProjectService.cs:78-89`, `:108-115` |
| أنواع الواجهة | ❌ غائبان من طلبات الكتابة | `reporting-frontend/src/types/api.ts:2252-2272` |
| الواجهة — نموذج التعديل | ❌ لا حقل | `ProjectDetailPage.tsx:977-1086` (يرسل `accountManagerId` فقط) |
| الواجهة — العرض | ✅ للقراءة فقط | `ProjectBriefTab.tsx:40,42` من `types/project360.ts:409-410` |
| القراءة في الخادم | ✅ | `ProjectOverviewService.cs:63` · `Project360Authorization.cs:58` |
| الاستعمال في التفويض | ✅ | `Project360Authorization.cs:87` |

**تأكيد من البيانات الحيّة (TEST · قراءة فقط):** من **9 مشاريع** — `TeamLeaderId IS NOT NULL` = **0** · `ProjectOwnerId IS NOT NULL` = **0** · `AccountManagerId IS NOT NULL` = **5**.

⟹ الحقلان موجودان في الطبقات الثلاث السفلى ومقطوعان عند طبقة العقد (DTO/Controller/UI). العمود موجود منذ ١١ أغسطس ولم يُملأ ولا مرّة واحدة، لأنّه **لا يمكن ملؤه**.

---

## 3) نموذج التفويض الفعليّ في الخادم (مصدر الحقيقة)

`reporting-backend/src/Reporting.Infrastructure/Services/Project360Authorization.cs`

| المستوى | الميثود:السطر | القاعدة الحرفيّة |
|---|---|---|
| **الرؤية** | `LoadVisibleProjectAsync:43-64` | مصدرها الوحيد `IClientProjectAccess` — لا توسيع. خارج النطاق ⟹ **404 بنفس الرمز والرسالة** `project.not_found` (FINDING-W6-04، السطور 18-22 و70) |
| **الكتابة البنيويّة** | `CanManageProject360Async:76-77` | `Roles.ProjectPlanManagers` **حصرًا** = `Admin, CEO, GeneralManager, Manager` (`Roles.cs:45-48`) |
| **الكتابة التشغيليّة** | `CanUpdateProject360ProgressAsync:83-88` | الأدوار أعلاه **أو** `project.TeamLeaderId == uid` **أو** `project.AccountManagerId == uid` |

الواجهة تعكس القاعدتين نفسيهما: `Project360Page.tsx:48-55` (`canManage` بالدور · `canOperate = canManage || isResponsible`).

### 3-1 مصدر النطاق — `ScopeResolver` لا يعرف `Project.TeamLeaderId` إطلاقًا

`ClientProjectAccess.cs:25-90` — المشروع يصير مرئيًّا بأحد أربعة أسباب فقط:
1. `SeesAll` (company/governance) — سطر 28-29.
2. `AccountManagerId` داخل النطاق — سطر 63.
3. `OwnerTeamId` من فريق **يقوده** أحد داخل النطاق، أو فريق ينتمي إليه المستخدم الحاليّ — سطور 34-38 و43-59 و64.
4. وُجد تسليم على المشروع من داخل النطاق — سطور 69-73.

⟹ **`Project.TeamLeaderId` لا يمنح رؤية.** الرؤية تأتي من `Team.TeamLeaderId` (كيان الفريق) — وهما شيئان مختلفان تمامًا يتشابهان في الاسم.

### 3-2 خريطة نقاط الكتابة الكاملة

| النقطة | البوّابة على المتحكّم | الفحص داخل الخدمة | من يُسمَح له فعليًّا |
|---|---|---|---|
| `POST /api/projects` | `ManagementOnly` | `CanOwnAsync` (`ProjectService.cs:75`) | Admin/CEO/GM/Manager/**TeamLeader** ضمن نطاقه |
| `PUT /api/projects/{id}` | `ManagementOnly` | **`CanViewProject` فقط** (`:106`) | كلّ من يرى المشروع من الأدوار الخمسة |
| `POST …/archive` | `ManagementOnly` | **`CanViewProject` فقط** (`:130`) | نفسه |
| `POST …/reactivate` | `ManagementOnly` | **`CanViewProject` فقط** (`:147`) | نفسه |
| `DELETE /api/projects/{id}` | `ManagementOnly` | **`CanViewProject` فقط** (`:172`) + حارس تبعيّات | نفسه |
| `PUT …/strategy` | `[Authorize]` | بنيويّ | Admin/CEO/GM/Manager |
| `POST/PUT/DELETE …/objectives` | `[Authorize]` | بنيويّ | Admin/CEO/GM/Manager |
| `PATCH …/objectives/{id}/status` | `[Authorize]` | **تشغيليّ** | + قائد فريق المشروع + مدير حسابه |
| `POST/PUT …/kpis` · تفعيل/تعطيل | `[Authorize]` | بنيويّ | Admin/CEO/GM/Manager |
| `POST/PUT …/kpis/{id}/readings` | `[Authorize]` | **تشغيليّ** | + قائد فريق المشروع + مدير حسابه |
| `POST/PUT …/contract-deliverables` | `[Authorize]` | بنيويّ | Admin/CEO/GM/Manager |
| `PATCH …/contract-deliverables/{id}/progress` | `[Authorize]` | **تشغيليّ** (`ProjectContractDeliverableService.cs:160-163`) | + قائد فريق المشروع + مدير حسابه |
| `POST/PUT/PATCH …/workstreams` | `[Authorize]` | `CanManagePlanAsync` | Admin/CEO/GM/Manager **+ مدير حساب المشروع** (لا قائد الفريق) — رسالة الرفض في `ProjectWorkstreamService.cs:60,109,148` |
| `POST …/health/recompute` | `[Authorize]` | **تشغيليّ** | + قائد فريق المشروع + مدير حسابه |

### 3-3 التناقض الحاكم المكتشَف (لم يُذكر في الدليل)

`Policies.ManagementOnly` (`Roles.cs:429`) يُترجَم إلى `Roles.Management` (`Roles.cs:34-37`) وهي تشمل **`TeamLeader`**.
و`ProjectService.UpdateAsync/ArchiveAsync/DeleteAsync` لا تفحص سوى `CanViewProject`.

⟹ **قائد فريق (بالدور) يستطيع تعديل مشروع وأرشفته وحذفه** ما دام يراه (لأنّه يقود الفريق المالك، أو لأنّ مرؤوسًا له قدّم تسليمًا عليه) — **بينما لا يستطيع تحديث نسبة إنجاز مخرَج واحد في المشروع نفسه.**

وتعليق الشيفرة داخل `ClientProjectAccess.cs:40-42` ينصّ حرفيًّا على العكس: «دون أيّ صلاحية إدارة/تعديل/اعتماد — إدارة المشاريع تبقى محكومة بسياسة `ManagementOnly`». الافتراض المكتوب هو أنّ `ManagementOnly` **تستبعد** قائد الفريق، وهي **لا تستبعده**.

**هذا نموذج صلاحيّات مقلوب**: البنيويّ الخطير مفتوح، والتشغيليّ البسيط مغلق. وهو ما شخّصه الدليل خطأً على أنّه «أزرار ظاهرة يرفضها الخادم» (حدّ #11) — الرفض الذي رصده الدليل كان سببه **خروج المشروع عن نطاق رؤية المختبِر**، لا منع الدور.

---

## 4) سلاسل الانتشار — القفزة تلو القفزة

### 4-1 معادلة الصحّة (ثوابت مُثبَتة من `ProjectHealthPolicy.cs`)

```
Health = ( 0.50 × kpiScore  +  0.30 × ProgressPercent  +  0.20 × scheduleScore )
         ÷ Σ(أوزان المكوّنات المتاحة)
```
`KpiComponentWeight=0.50` (:102) · `ProgressComponentWeight=0.30` (:105) · `ScheduleComponentWeight=0.20` (:108)
`GreenThreshold=80` (:111) · `YellowThreshold=55` (:114) — دونها أحمر.
`scheduleScore`: `expected = clamp((today−Start)/(End−Start)×100, 0, 100)` ثمّ `gap = ProgressPercent − expected` ⟹ `gap≥0→100` · `≥−10→75` · `≥−25→50` · `<−25→25` (:117-132).
`achievement = clamp((Current/Target)×100, 0, 200)` (:90-96).

### 4-2 التحقّق العدديّ على بيانات TEST الحيّة (المشروع التدريبيّ `ceea1351…`)

| المرحلة | `kpiScore` | `ProgressPercent` | `scheduleScore` | الحساب | الناتج المخزَّن |
|---|---|---|---|---|---|
| 1 | 62.50 | **0.00** | 50 | `0.5×62.5 + 0.3×0 + 0.2×50` | **41.25** (الدليل: 41.3) |
| 2 | 77.50 | **0.00** | 50 | `0.5×77.5 + 0.3×0 + 0.2×50` | **48.75** (الدليل: 48.8) |
| 3 | 92.50 | **0.00** | 50 | `0.5×92.5 + 0.3×0 + 0.2×50` | **56.25 · Yellow** ✅ مطابق للصفّ الحيّ في `projects` |

**النتيجة الرياضيّة الحاسمة:** بما أنّ `ProgressPercent` مشدود إلى صفر بنيويًّا، فالسقف النظريّ للصحّة هو `0.5×kpiScore + 0.2×scheduleScore`. ولمشروع جارٍ فعلًا (تاريخ بدء مضى) يستحيل أن يكون `scheduleScore = 100` لأنّ `gap = 0 − expected < 0`، فالسقف العمليّ `≤ 0.5×kpiScore + 15`.
⟹ **لا يمكن لأيّ مشروع جارٍ أن يبلغ اللون الأخضر (80) إلّا إذا تجاوز تحقيق مؤشّراته 130٪.** المشروع الصحّيّ تمامًا يُعرَض «يحتاج متابعة» أو «متعثّر» إلى الأبد.

### 4-3 خريطة القفزات

| السلسلة | القفزة | الحالة |
|---|---|---|
| **المؤشّر** | قراءة جديدة ⟶ `ProjectKpiReading` + لقطة التحقيق | **[آليّة]** |
| | ⟶ `kpi.CurrentValue` + `LastReadingDate` (`ProjectKpiService.cs:385`) | **[آليّة]** |
| | ⟶ `SaveWithHealthAsync` في نفس المعاملة (`ProjectKpiService.cs:390`) | **[آليّة]** |
| | ⟶ حالة الهدف | **[مفقودة — بقرار مُوثَّق]** (`ProjectObjectiveService.cs:160-163`: الحالة تُعلَن ولا تُشتقّ) |
| **بنية الخطّة** | إنشاء/تعديل/تفعيل/حذف هدف أو مؤشّر ⟶ إعادة احتساب الصحّة | **[آليّة]** (`ProjectObjectiveService.cs:100,137,180,200` · `ProjectKpiService.cs:129,162,179`) |
| **المخرَج التعاقديّ** | تحديث الحالة والنسبة ⟶ `ProjectDeliverable` نفسه | **[يدويّة]** (`:175-179`) |
| | ⟶ صحّة المشروع | **[مفقودة]** — لا استدعاء لـ`SaveWithHealthAsync` في هذا الملفّ إطلاقًا |
| | ⟶ `Project.ProgressPercent` | **[مفقودة]** — لا كتابة |
| | ⟶ مسار العمل الأب | **[مفقودة]** — `ProjectWorkstream` بلا حقل تقدّم أصلًا |
| | ⟶ بطاقة «المخرَجات» في النظرة العامّة | **[آليّة لكنّها عرض فقط]** `ProjectOverviewService.cs:152-154` تحسب `OverallProgressPercent = متوسّط نسب المخرَجات النشطة` لحظة القراءة ولا تُخزَّن |
| **تقرير عضو الفريق** | تسليم ⟶ `ReportSubmission.ProjectId` (اختياريّ) | **[يدويّة وغير مستعملة]** — على TEST: 13 تسليمًا · **0 منها يحمل `ProjectId`** |
| | ⟶ اعتماد قائد الفريق ثمّ التسلسل | **[آليّة]** (`SubmissionService.cs:410,554,629`) |
| | ⟶ أثر على `Project.ProgressPercent` أو الصحّة | **[مفقودة]** — صفر مرجع |
| | ⟶ لوحة التنفيذيّ | **[آليّة لكن عبر مسار مواز]** `PodExecutionAggregationService.cs:437-509` يقرأ خلايا `TableGrid` داخل نصّ التسليم (JSON) لا `ReportSubmission.ProjectId` |
| **الإشعارات** | أيّ كتابة في Project 360 ⟶ إشعار | **[مفقودة]** — صفر مرجع لأيّ خدمة إشعار/SignalR في كلّ ملفّات `Project*.cs` |
| **التدقيق** | كلّ كتابة ⟶ `_audit.LogAsync` | **[آليّة]** — مُثبَتة في كلّ الخدمات المفحوصة |

### 4-4 تناقض معروض على الشاشة نفسها

للمشروع التدريبيّ على TEST الآن (قراءة مباشرة من `project_deliverables`):
3 مخرَجات `Delivered` بـ100٪ · 1 `InProgress` بـ75٪ · 2 `NotStarted` بـ0٪ ⟹ `OverallProgressPercent = 375/6 = **62.50٪**`
بينما `Project.ProgressPercent = **0.00٪**`.
⟹ **رقمان متناقضان لتقدّم المشروع نفسه في الشاشة نفسها**، أحدهما محسوب حيًّا والآخر ميت.

---

## 5) الحوكمة داخل المشروع

| الكيان | `ProjectId`؟ | الملفّ:السطر | الواقع على TEST |
|---|---|---|---|
| `Risk` | ✅ `Guid?` (+ `ClientId`) | `Governance/Risk.cs:25-26` | 2 خطر · **1 يحمل `ProjectId`** |
| `Decision` | ✅ `Guid?` | `Governance/Decision.cs:32` | 9 قرارات · **5 تحمل `ProjectId`** |
| `ManagementNote` | ✅ عبر `EntityType+EntityId` | `Governance/ManagementNote.cs:13-14` | 9 ملاحظات · **6 منها `EntityType = Project`** |
| `Escalation` | ❌ **غائب كليًّا** | `Governance/Escalation.cs` | — |
| `GovernanceItem` | ❌ **غائب كليًّا** | `Governance/GovernanceItem.cs` | — |

- **الكتابة (API):** `CreateRiskRequest` و`CreateDecisionRequest` **يستقبلان `ProjectId`** ويحفظانه (`GovernanceModels.cs:38-39,112` · `GovernanceService.cs:48-49,218`).
- **القراءة المرشَّحة على المشروع:** موجودة وتعمل — `ProjectGovernanceReadService.cs:43,72,103-104`.
- **الواجهة العامّة للحوكمة:** `GovernancePage.tsx` — نموذج الخطر يرسل `title, severity, nextAction` فقط، ونموذج القرار يرسل `title, description, relatedXxx` فقط. **كلمة `projectId` لا ترد في الملفّ إطلاقًا** (بحث نصّيّ: صفر تطابق). و`GET /risks` بلا مرشّح مشروع (`GovernanceController.cs:33-35`).
- **تبويب الحوكمة داخل Project 360:** `ProjectGovernanceTab.tsx:22-25` — يستدعي المسارات الثلاثة المرشَّحة، و**للقراءة فقط بلا استثناء** (لا زرّ إنشاء ولا تعديل).

⟹ **تصحيح جوهريّ للدليل:** حدّ الدليل رقم 3 («المخاطر والقرارات لا تُربَط بمشروع») و#4 («لا ملاحظات إداريّة على مستوى المشروع») **يصفان قصورًا في الواجهة لا في الدومين**. العلاقة الدوميّة موجودة وتعمل، والدليل من `EntityType=Project` في 6 ملاحظات حيّة على TEST. الإصلاح **توصيل واجهة، بلا هجرة ولا تعديل دومين** — عدا `Escalation` و`GovernanceItem`.

---

## 6) الحالة الافتراضيّة المضلّلة للصحّة

`ProjectHealthStatus` (`Enums.cs:770-775`) = `{ Green = 0, Yellow = 1, Red = 2 }` — **لا عضو `NotEvaluated`**، والعمود غير قابل لـNULL، والقيمة الابتدائيّة في الكيان `= ProjectHealthStatus.Green` (`Project.cs:66`).

**الواقع الحيّ على TEST:**

| المشروع | `ProgressPercent` | `HealthPercent` | `HealthStatus` | `HealthComputedAtUtc` |
|---|---|---|---|---|
| مشروع تدريبي — تنمية العملاء | 0.00 | 56.25 | Yellow | مُحتسَبة |
| CPW-UNIFIED-UAT-R1-PRJ-GREEN | 0.00 | 54.00 | Red | مُحتسَبة |
| CPW-UNIFIED-UAT-R1-PRJ-ATRIS | 0.00 | 7.50 | Red | مُحتسَبة |
| CPW-UNIFIED-UAT-R1-PRJ-OUTSC | 0.00 | 0.00 | Red | مُحتسَبة |
| مشروع UAT سيو نشط | 0.00 | 0.00 | **Green** | **NULL** |
| مشروع UAT إدارة مباشرة | 0.00 | 0.00 | **Green** | **NULL** |
| مشروع UAT سوشيال نشط | 0.00 | 0.00 | **Green** | **NULL** |
| مشروع UAT مؤرشف | 0.00 | 0.00 | **Green** | **NULL** |
| test | 0.00 | 0.00 | **Green** | **NULL** |

⟹ **5 من 9 مشاريع على TEST تُعرَض «أخضر» وهي لم تُحتسَب صحّتها قطّ.** أيّ قائمة أو لوحة إداريّة تقرأ `HealthStatus` بلا فحص `HealthComputedAtUtc` تعرض على الإدارة **صحّة كاذبة**. تمييز «لم تُقيَّم» موجود في طبقة السياسة/الـDTO لكنّه **غير موجود في العمود المخزَّن الذي بُني أصلًا للفرز والفلترة بلا N+1** (تعليق `Project.cs:62-65`).

---

## 7) الانحراف عن BRD (تسجيل لا حسم)

| البند | ما تقوله BRD | ما ينفّذه الكود | الحكم |
|---|---|---|---|
| مصدر التقدّم | ف7 §7.14 + ف8 (قرار معماريّ صريح): «**لا يتم احتساب التقدم من تغيير الحالة يدويًا فقط. التقدم الحقيقي يأتي من إنهاء وحدات تنفيذ مرتبطة بمخرجات معتمدة**» — موزون بـPlanned/Completed Quantity وActual Hours وOn-Time Rate | `Project.cs:41` يعلنه «يدويًّا … Manual-First» ثمّ **لا يبني حتّى مدخل اليدويّ** | **تعارض مزدوج**: انحراف عن الرؤية + عجز عن تنفيذ البديل المُعلَن |
| صحّة المشروع | ف7 §7.12: أربع حالات وصفيّة `Healthy / At Risk / Delayed / Blocked` — **لا معادلة وزن رقميّة ولا عتبات مئويّة وُجدت نصًّا** | ثلاثة ألوان بمعادلة موزونة 50/30/20 وعتبتَي 80/55 | **تعارض في النموذج نفسه** — يحتاج قرار مالك منتج |
| محرّك المهامّ | ف7 §7.20: تسلسل إلزاميّ `Project → Goal → Deliverable → Task`؛ ف8: المحرّك في المرحلة **P8** بعد P0→P7 | **صفر كيان مهامّ في الدومين** (لا `Task`/`Assignment`/`WorkItem`/`WorkLog`) | **مؤجَّل بالتصميم** — مطابق للتسلسل المرحليّ، لا عيب |
| الحوكمة | ف11 §11.1: «أيّ قرار أو خطر أو مخالفة **يجب** أن يكون قابلًا للربط بالمشروع والعميل والفريق والتقرير» | `Risk`/`Decision`/`ManagementNote` ✅ · `Escalation`/`GovernanceItem` ❌ · والواجهة لا تربط | **فجوة توصيل** + **فجوة دومين جزئيّة** |
| أدوار المشروع | ف7 §7.9/§7.16: `Account/Project Manager` يملك الصورة · `Execution Manager` لكلّ هدف يدير مخرَجاته · `Team Leader` نطاق فريق + «توزيع وتنفيذ **لاحق**» + اعتماد مراجعة · ف7 §7.17 قاعدة 2: «**لا يبدأ التخطيط قبل تعيين مدير حساب/مشروع**» | لا يمكن تعيين مالك المشروع أصلًا · ولا يوجد دور `Execution Manager` · وقائد الفريق يملك تعديل/حذف المشروع بينما لا يملك تحديث مخرَج | **تعارض بنيويّ** |
| تقرير الموظّف ← المشروع | ف8 §8.3: المهمّة تحمل السياق الكامل · دورة `Self → Peer → Team Lead → QA → Manager → Client` · **لا نصّ صريح** يربط التقرير الدوريّ بالمخرَج مباشرةً؛ الربط استنتاجيّ عبر Task | `ReportSubmission.ProjectId` موجود واختياريّ و**غير مستعمَل عمليًّا** (0/13 على TEST) | **فجوة تشغيل**، والحلقة المفقودة (Task) مؤجَّلة بالتصميم |

---

## 8) خلاصة تدقيق حالات UAT التسعين

الحقول المرجعيّة العشرة مملوءة في الحالات التسعين. أمّا **حقول التنفيذ السبعة** — «النتيجة الفعليّة» · «الحالة» · «الدليل» · «معرّف العيب» · «تعليق المختبِر» · «نُفِّذ بواسطة» · «تاريخ التنفيذ» — فهي **فارغة في 90 من 90 حالة بلا استثناء**.

⟹ بحسب شرط الدقّة المُلزِم: **صفر حالة اختبار قبول ناجحة حتّى الآن.** الدليل نفسه يصنّف ما نُفِّذ أثناء إعداده بأنّه «تنفيذ مرجعيّ / دليل توثيقيّ» لا اعتمادًا. وجود 45 لقطة و90 حالة مكتوبة ونجاح الاختبارات التقنيّة السابقة **لا يُقرأ اعتمادًا** ولا يُغني عن دورة تنفيذ متعدّدة الأدوار.

---

## 9) كتلة تأكيد النطاق

```
CODE CHANGED        = NO
MIGRATIONS CREATED  = NO
COMMIT / PUSH       = NO
TEST DATA CHANGED   = NO   (استعلامات SELECT فقط)
TEST DEPLOYED       = NO
RC TOUCHED          = NO
PRODUCTION TOUCHED  = NO
NGINX/SYSTEMD/ENV   = NO
SECRETS EXPOSED     = NO
```
`git status --short` = 0 سطر على `develop` عند `c9c0cb2` بعد انتهاء الجولة.
