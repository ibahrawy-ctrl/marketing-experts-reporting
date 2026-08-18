# بوّابة القبول النهائيّة — `PROJECT360-R2-UAT-EVIDENCE-AND-GUIDE-CLOSURE-R2.1`

التاريخ: ١٨ أغسطس ٢٠٢٦ · رأس الفرع المقيس: `1e91317` (`origin/develop`) · `RC_TOUCHED = NO` · `PRODUCTION_TOUCHED = NO`

## 1) بوّابة التكامل الكاملة — قاعدة جديدة فارغة مخصّصة لهذه الجولة

### إجراء منع سباق الهجرات (تهيئة تسلسليّة مرّة واحدة)

| الخطوة | الأمر | النتيجة |
|---|---|---|
| إنشاء ثلاث قواعد فارغة | `createdb reporting_gate_{main,pfe,cal}` | 3 قواعد جديدة (المجموعة تستعمل ثلاث قواعد: `TEST_DB_CONNECTION` · `TEST_DB_CONNECTION_PFE` · `TEST_DB_CONNECTION_CAL`) |
| تطبيق الهجرات **تسلسليًّا** | `dotnet ef database update --connection …` لكلّ قاعدة على حدة | `EF_EXIT=0` ×3 · **40 صفًّا** في `__EFMigrationsHistory` لكلّ قاعدة · الرأس `20260817114129_AddProjectExecutionUpdateProposals` |
| البذر **تسلسليًّا** | تشغيل `Reporting.Api.dll` مرّة واحدة على كلّ قاعدة (بيئة `Testing`) ثمّ إيقافه | `HEALTH=200` ×3 · `report_templates = 34` لكلّ قاعدة · **بلا أيّ بيانات اختبار** |
| شرط قاعدة نظيفة الإلزاميّ | `UPDATE report_templates SET "Classification"='Supplementary' WHERE "Title" IN (…)` | `UPDATE 5` لكلّ قاعدة |

**لماذا التسلسل ضروريّ:** المجموعة فيها أربع تجميعات xUnit تتوازى، واثنتان منها (`ProjectFirstIsolated` و`PfeNumericIsolated`)
تتقاسمان قاعدة `PFE` نفسها ⟹ تشغيل الهجرات من مضيفَين في آنٍ واحد أنتج سابقًا `42701 column "ProgressCalculatedAtUtc" … already exists`.
تطبيق الهجرات والبذر خارج المجموعة يجعل `MigrateAsync()` قراءةً بلا DDL فينتفي السباق من أصله.

### التشغيل والنتيجة — رمز الخروج ملتقَط مباشرةً بلا `tee` وبلا Pipeline

```
dotnet test tests/Reporting.IntegrationTests -c Release --no-build > /tmp/gate-integration.log 2>&1
RC=$?; printf 'INTEGRATION_EXIT=%s\n' "$RC" > /tmp/gate-integration.exit
```

| المقدار | القيمة المقيسة |
|---|---|
| `INTEGRATION_TOTAL` | **2011** |
| `INTEGRATION_PASSED` | **2010** |
| `INTEGRATION_FAILED` | **1** |
| `INTEGRATION_EXIT` | **1** |
| المدّة | 7 دقائق 50 ثانية |

> **مُحدَّث:** هذه أرقام البوّابة قبل إغلاق `BASELINE-DEFECT-01`. القياس النافذ الآن في **§2-أ**:
> `2011/2011` بـ`INTEGRATION_EXIT = 0`. تُبقى الأرقام أدناه سجلًّا للقياس الأوّل لا تُمحى.

**الجولة التأكيديّة** على مجموعة قواعد ثانية جديدة تمامًا (`reporting_gate2_{main,pfe,cal}`، أُنشئت وهُوجرت وبُذِرت بالإجراء نفسه):
`Failed: 1, Passed: 2010, Total: 2011`، `INTEGRATION_EXIT=1`، 7 دقائق 51 ثانية — **نفس الاختبار حرفيًّا**.
السجلّان: `/tmp/gate-integration.log` و`/tmp/gate2-integration.log`.

## 2) تشخيص الفشل الوحيد — ليس تلوّث قياس

الفاشل: `Reporting.IntegrationTests.AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete`
عند `AdminGovernanceTests.cs:366` — `Expected: OK` / `Actual: NotFound`.

### سلسلة القياس

| # | القياس | النتيجة |
|---|---|---|
| 1 | المجموعة الكاملة على `reporting_gate_main` (جديدة) | فشل |
| 2 | المجموعة الكاملة على `reporting_gate2_main` (جديدة أخرى) | فشل — نفس الاختبار |
| 3 | الاختبار **وحده** على `reporting_gate_solo` (جديدة، مستخدمان فقط) | **فشل** |
| 4 | صنف `AdminGovernanceTests` كاملًا على القاعدة نفسها | `Failed: 1, Passed: 17, Total: 18` — الفاشل نفسه، ونفَّذ أوّلًا (00:00:02.11) |
| 5 | إعادة الاختبار **وحده** على القاعدة نفسها بعدما صار فيها **45 مستخدمًا** | **`Passed! Failed: 0, Passed: 1`** |

### السبب الجذريّ (مقيس لا مستنتَج)

الاستعلام على قاعدة الجولة المنفردة بعد الفشل:

```
select "Id","Status","ReviewerId","PeriodKey" from kpi_evaluations;
→ 98375b65-… | InProgress | (null) | 2026-W29        ·  AspNetUsers = 2
```

التقييم **أُنشئ ولم يُرسَل**: بقي `InProgress` و`ReviewerId` فارغ. أي أنّ `POST /submit` فشل، لا `POST /flag`.
السبب في `KpiEvaluationService.SubmitAsync` (السطر 296–298): إسناد المُراجِع إلزاميّ، والمُراجِع لا يجوز أن يكون
المُدخِل نفسه ولا الموضوع. على قاعدة بكرًا لا يوجد سوى `admin` (وهو المُدخِل) وحساب HR (وهو الموضوع)، والموضوع
بلا `ManagerId` ⟹ `kpi_eval.no_reviewer.conflict`.

ثمّ إنّ المساعد `SubmitEvalAsync` (السطر 56–57) لا يتحقّق من نجاح الاستدعاء، فيعيد DTO افتراضيًّا بـ`Id = Guid.Empty`،
فيأتي `POST /kpi-evaluations/{Guid.Empty}/flag` بـ404 صحيحًا تمامًا. **التأكيد في السطر 366 يبلّغ العرَض لا العلّة.**

### الحكم على طبيعة الفشل

- **ليس انحدارًا من R2.1**: `git diff 7611f0e..HEAD` لا يمسّ أيّ شيفرة تقييم KPI، و`AdminGovernanceTests.cs` غير معدَّل.
  المُعدَّل في الخلفيّة سبعة ملفّات فقط، كلّها في نطاق Project 360 (مسارات العمل والمخرَجات).
- **ليس عيبًا في المنتج**: الخادم يردّ بالعقد الصحيح في الحالتين (409 عند تعذّر المُراجِع، 404 على معرّف غير موجود).
- **عيب تجهيزة اختبار**: الاختبار لا يبني شرطه القبْليّ (وجود مسؤول أعلى صالح للمراجعة)، فيعتمد على مستخدمين
  تُنشئهم اختبارات أخرى. لذلك ينجح على قاعدة متراكمة ويفشل على قاعدة بكر.
- **تصحيح لقيد سابق:** كان مسجَّلًا أنّ `BASELINE-DEFECT-01` «ينجح منفردًا ويفشل ضمن المجموعة». القياس أعلاه يُثبت
  **العكس**: يفشل منفردًا على قاعدة نظيفة، وينجح متى امتلأت القاعدة. الاتّجاه معكوس في الوصف القديم.
- **لم يُصلَح في هذه البوّابة عمدًا**: `BASELINE-DEFECT-01` كان محجوزًا لتذكرة عزل مستقلّة. **أُغلِق بعدها** في
  التذكرة `BASELINE-DEFECT-01 — CLEAN TEST-FIXTURE CLOSURE ONLY` (القسم 2-أ أدناه).

## 2-أ) إغلاق `BASELINE-DEFECT-01` — تجهيز اختبار فقط (بتصريح مستقلّ)

الالتزام الذرّيّ: **`cc4c387`** — ملفّ واحد `reporting-backend/tests/Reporting.IntegrationTests/AdminGovernanceTests.cs`
(+39/−8). `PRODUCT_CODE_CHANGE = NONE` · `MIGRATION = NONE` · `FRONTEND_CHANGE = NONE` · `TEST_DEPLOYMENT = NONE`.

### ما تغيّر

1. **هرميّة حتميّة داخل الاختبار**: يُنشأ مدير مباشر للموضوع (`CreateUserAsync("Manager")`) ويُمرَّر
   `managerId` عند إنشاء حساب HR. بذلك يتوقّف `ResolveReviewerAsync` عند خطوته الأولى (مدير الموضوع)
   على قاعدة فارغة أو مأهولة سواء — بلا اعتماد على مستخدمين تُنشئهم اختبارات أخرى ولا على ترتيب التنفيذ.
   ويؤكَّد الإسناد صراحةً: `Assert.Equal(hrManagerId, submitted.ReviewerId)` + `Status == UnderReview`.
2. **تأكيد فوريّ في `SubmitEvalAsync`**: كلّ استدعاء (إنشاء · حفظ النتائج · الإرسال) يمرّ على
   `EnsureSuccessAsync` التي تُفشِل الاختبار برمز الحالة وجسم خطأ الخادم. ويُمنع تمرير DTO افتراضيّ:
   `Assert.NotNull` + `Assert.NotEqual(Guid.Empty, …)` + `Assert.NotNull(ReviewerId)`.
   العلّة كانت أنّ `TestJson.ReadAsync<T>` يفكّ ترميز الجسم أيًّا كان رمز الحالة.

### القياس بعد الإصلاح — رموز الخروج ملتقَطة مباشرةً بلا `tee` وبلا Pipeline

| المقياس | القاعدة | الأمر | النتيجة | رمز الخروج |
|---|---|---|---|---|
| الاختبار وحده | `reporting_fix_solo` (جديدة فارغة) | `--filter FullyQualifiedName~Hr_CanFlagCommentRequestReopen` | `Passed! Failed: 0, Passed: 1, Total: 1` | **0** |
| الصنف كاملًا | `reporting_fix_class` (جديدة أخرى) | `--filter FullyQualifiedName~AdminGovernanceTests` | `Passed! Failed: 0, Passed: 18, Total: 18` | **0** |
| المجموعة الكاملة | `reporting_fix_{main,pfe,cal}` (ثلاث جديدة، مهيّأة بالتتابع بالإجراء نفسه في §1) | `dotnet test tests/Reporting.IntegrationTests -c Release --no-build` | `Passed! Failed: 0, Passed: 2011, Skipped: 0, Total: 2011` · 7م56ث | **0** |

تهيئة مجموعة القواعد الثلاث: `EF_EXIT=0` ×3 · **40 صفًّا** في `__EFMigrationsHistory` لكلّ قاعدة ·
`HEALTH=200` ×3 · `report_templates = 34` · `UPDATE 5` لعبارة `Classification='Supplementary'` ·
`AspNetUsers = 1` (الأدمن المبذور وحده) قبل التشغيل.

السجلّات: `/tmp/fix-single.log` · `/tmp/fix-class.log` · `/tmp/fix-integration.log` ورموزها في `*.exit`.

```
SINGLE_TEST      = PASS
CLASS_TESTS      = 18/18
FULL_INTEGRATION = 2011/2011
INTEGRATION_EXIT = 0
```

## 3) ملخّص UAT الحقيقيّ

القاعدة المطبَّقة: لا تُعَدّ الحالة `PASS` إلّا بنتيجة فعليّة مسجَّلة ودليل تنفيذ حقيقيّ (لقطة متصفّح أو مسبار خادم أو نصّ مقروء من الصفحة).

| المقدار | القيمة |
|---|---|
| `UAT_TOTAL` | **141** |
| `UAT_PASSED` | **26** |
| `UAT_FAILED` | **0** |
| `UAT_BLOCKED` | **0** |
| `UAT_NOT_EXECUTED` | **115** |
| `UAT_NA` | **0** |

- الـ26 الناجحة كلّها حزمة `UAT-R21`، ولكلّ حالة منها لقطة مرجعيّة في المعرض (خريطة `CASE_FIG_R21` في
  `Docs/Guides/builders/figures-r21.mjs:83-110`) ودليل تنفيذ خام في `Ops/R21/uat/evidence/`.
- الـ115 غير المنفَّذة هي حزم R1 (**90** حالة) وحزمة `UAT-R2` (**25** حالة): لم تُعَد تنفيذها في هذه الدورة،
  وكرّاسة R2.1 تحملها **نموذجًا فارغًا** (حقول «النتيجة الفعليّة» و«الحالة» فارغة عمدًا في `ch-r21.mjs:330`).
  اعتبارها ناجحة بلا نتيجة مسجَّلة سيكون ادّعاءً لا قياسًا.

### التصنيف النافذ (تصحيح صياغة لا تصحيح رقم)

```
R21_TARGETED_UAT          = PASS (26/26)
LEGACY_REGRESSION_CATALOG = 115 NOT EXECUTED
```

الـ115 **مكتبة حالات UAT/انحدار قابلة للتنفيذ**، لا نتائج ناجحة ولا حالات فاشلة. عدم تنفيذها **لا يمنع
نشر RC**، لكنّه يمنع منعًا باتًّا الادّعاء بأنّ النتيجة `141/141 PASS`.

## 4) مصفوفة الأدلّة النهائيّة — 13 شرطًا

الأرقام في عمود «اللقطة» هي أرقام الأشكال في دليل R2.1 (`…-Guide-Assets-R2.1/raw/INDEX.md`).

| # | الشرط | الحكم | حالة الاختبار | اللقطة | دليل الـAPI | دليل الشيفرة |
|---|---|---|---|---|---|---|
| 1 | `TEAM_LEADER_SEES_ASSIGNED_PROJECT_DETAILS` | **PASS** | `UAT-R21-TL-001` · `UAT-R21-TL-002` · تكامل `Project360FoundationTests.TeamLeader_CanUpdateOperationalState_ButCannotManageStructure` | 86 `01-tl-project-details.png` · 118 `01-r21-tl-project-overview.png` · 105 · 73 | `notes-stage-q.json → tl.overview` (تقدّم ٤٠٪ · صحّة ٣٥٫٦٪ · 8 تبويبات) · `tl.tabs` | `Project360Authorization.cs:100-109` (`CanUpdateProject360ProgressAsync`) · `BuildCapabilitiesAsync:123-127` |
| 2 | `TEAM_LEADER_CAN_REVIEW_EXECUTION_CLAIM` | **PASS** | `UAT-R21-TL-003` · `UAT-R21-TL-004` · تكامل `Project360ExecutionBridgeTests.Review_Accept_AppliesToDeliverable_RollsUpProject_AndSnapshotsThePreviousValue:171` · `List_CanReview_ReflectsTheReadersOwnOperationalPermission:138` | 88 `03-tl-sees-pending-claim.png` · 96 `02-tl-claim-accepted.png` · 97 `03-tl-deliverable-after-accept.png` | `probes-stage-j.json → IDEMPOTENT-SAME-DECISION status 200` على `…/execution-proposals/{id}/review` | `ProjectExecutionBridgeService.cs:58,123,200` (`CanUpdateProject360ProgressAsync`) |
| 3 | `TEAM_LEADER_CAN_REJECT_WITH_REQUIRED_REASON` | **PASS** | `UAT-R21-TL-005` · تكامل `Review_Reject_RequiresAReason_AndLeavesTheDeliverableUntouched:196` · واجهة «يمنع الرفض بلا سبب ويسمح به بعد كتابة الملاحظة» | 89 `04-tl-reject-disabled-no-reason.png` · 90 · 91 | `probes-stage-h.json → SRV-REJECT-NO-REASON status 400 code project_execution_proposal.reject_reason_required` · `uiGuard {found:true, disabled:true}` | `Project360Codes.cs:65` (`ProposalRejectReasonRequired`) |
| 4 | `TEAM_LEADER_CAN_UPDATE_OPERATIONAL_DELIVERABLE` | **PASS** | `UAT-R21-TL-006` · تكامل `DirectProgressUpdate_LeavesATrailRow_CarryingTheActorTheReasonAndThePreviousValue:300` · `DirectProgressUpdate_WhenForbidden_WritesNoTrailRowAtAll:336` | 101 `02-tl-direct-update-form.png` · 102 `03-tl-deliverable-b-after.png` · 103 `04-tl-direct-update-audit.png` | `notes-stage-k.json` بلا أخطاء HTTP · النسبة المطبَّقة مقروءة في `notes-stage-q.json` | `ProjectContractDeliverableService.cs:175` |
| 5 | `TEAM_LEADER_CAN_RECORD_KPI_READING` | **PASS** | `UAT-R21-TL-007` | 106 `02-tl-kpi-reading-form.png` · 107 `03-tl-kpi-reading-saved.png` · 108 `04-tl-kpi-current-value.png` | `notes-stage-l.json` بلا أخطاء HTTP · «نتيجة المؤشّرات ٧٫٢٪» مقروءة في `notes-stage-q.json` | `ProjectKpiService.cs:293` |
| 6 | `TEAM_LEADER_CAN_MANAGE_WORKSTREAM` | **PASS** | `UAT-R21-TL-008` · تكامل `WorkstreamDeliverablesTests.AssignedTeamLeader_Can_Manage_Plan_Deliverables:444` · `TeamLeader_Leading_OwnerTeam_Only_Cannot_Manage_Plan:420` | 109 `01-tl-workstreams-card.png` · 110 · 111 `03-tl-workstream-saved.png` | `notes-stage-m/n.json → uiGuard.workstreamManageVisible = true` · `notes-stage-q.json → tl.workstreamManageVisible = true` | `ProjectWorkstreamService.cs:61,110,149` · `ProjectObjectiveService.cs:151` |
| 7 | `TEAM_LEADER_CANNOT_DELETE_PROJECT` | **PASS** | `UAT-R21-DENY-002` · تكامل `TeamLeader_CanUpdateOperationalState_ButCannotManageStructure:349` | 112 `01-tl-no-structural-buttons.png` | `notes-stage-q.json → probes[TL-DELETE-PROJECT] expect 403 / status **403**` (و`notes-stage-n.json` يوثّق `DELETE /api/projects/{id}` = 403) | `ProjectsController.cs:57-59` (`[Authorize(Policy = ProjectStructuralManage)]`) · `Roles.cs:62-66` (لا `TeamLeader`) · `ProjectService.cs:48` (دفاع بالعمق) |
| 8 | `TEAM_LEADER_CANNOT_ARCHIVE_PROJECT` | **PASS** | `UAT-R21-DENY-001` · `UAT-R21-DENY-005` | 112 `01-tl-no-structural-buttons.png` | `notes-stage-q.json → probes[TL-ARCHIVE-PROJECT] expect 403 / status **403**` · `notes-stage-n.json → POST …/archive = 403` · `uiGuard {editProjectVisible:false, archiveProjectVisible:false}` · `tl.structuralButtons = []` | `ProjectsController.cs:47-50` · واجهة `lib/auth.tsx:82-86` (`PROJECT_STRUCTURAL_ROLES` بلا `TeamLeader`) |
| 9 | `TEAM_LEADER_OUT_OF_SCOPE_RETURNS_404` | **PASS** | `UAT-R21-DENY-004` · تكامل `Project360ExecutionBridgeTests.Create_ForeignProject_Returns404_NotForbidden:83` · `Review_ProposalFromAnotherProject_Returns404:240` | — (المنع لا يُصوَّر؛ الدليل ردّ الخادم) | `notes-stage-q.json → probes[TL-OUT-OF-SCOPE-PROJECT] expect 404 / status **404** · code `project.not_found` · «المشروع غير موجود.»` | `Project360Authorization.cs:17-24` (عقد واحد لا يُميَّز — منع التعداد) · `ProjectService.cs:35-36` · `Project360Codes.cs:16` |
| 10 | `TEAM_LEADER_ACTION_VISIBLE_IN_PROJECT360` | **PASS** | `UAT-R21-PROP-001` · `UAT-R21-PROP-003` · `UAT-R21-TL-010` | 104 `05-tl-progress-after-direct.png` · 87 `02-tl-p360-overview.png` · 98 `04-tl-project-progress-after-accept.png` | `notes-stage-q.json → tl.overview`: «نسبة تقدّم المشروع ٤٠٪ · متوسّط موزون بأوزان المخرَجات · صحّة ٣٥٫٦٪ متأخّر» — والتحقّق الحسابيّ `٠٫٦×٥٠ + ٠٫٤×٢٥ = ٤٠` | مصدر واحد للرقم: `ProjectHealthService.cs:61` |
| 11 | `TEAM_LEADER_ACTION_VISIBLE_IN_CLIENT360` | **PASS** | `UAT-R21-PROP-002` | 120 `03-r21-am-client360-projects-summary.png` · 115 `04-am-client360-projects-summary.png` | `notes-stage-q.json → propagation.projectsTabClicked=true` · رؤوس الجدول `[… التقدّم · الصحّة …]` · الصفّ `٤٠٪ متوسّط موزون بأوزان المخرَجات` · `متأخّر ٣٥٫٦٪` | `ClientDetailPage.tsx:918-919` (العمودان) · `:934-945` (الرقم يصل من `ProjectDto` نفسه بلا احتساب في المتصفّح) |
| 12 | `TEAM_LEADER_ACTION_VISIBLE_TO_ACCOUNT_MANAGER` | **PASS** | `UAT-R21-PROP-004` | 113 `01-am-p360-overview.png` · 114 `02-am-deliverables-after-accept.png` | `notes-stage-o.json → observed.overviewText` بحساب «مدير العميل UAT»: «نسبة تقدّم المشروع ٤٠٪ … صحّة المشروع ٣٥٫٦٪ متأخّر» — مطابقة لعين قائد الفريق | `Project360Authorization.cs:104-107` (`AccountManagerId` ضمن القدرة التشغيليّة) |
| 13 | `TEAM_LEADER_ACTION_VISIBLE_TO_MANAGEMENT` | **PASS** | `UAT-R21-PROP-005` · `UAT-R21-PROP-006` · `UAT-R21-NAR-002` | 116 `01-mgmt-p360-overview.png` · 117 `02-mgmt-deliverables.png` · 121 `04-r21-mgmt-execution-history-fixed.png` | `notes-stage-p.json → observed.header` بحساب «الرئيس التنفيذي UAT»: نفس الأرقام حرفيًّا · `notes-stage-q.json → propagation.managementBridge.rows` **مطابق سطرًا بسطر** لـ`tl.bridgeNarration.rows` | `Roles.cs:62-66` · `ProjectExecutionBridgeService.cs:200` |

### سرد جسر التنفيذ كما قرأه المتصفّح بعد إغلاق `GAP-R21-07` (من الحسابين معًا)

```
من ٠٪ إلى ٢٥٪ · الحالة المقترحة لم يبدأ
من ٠٪ إلى ٥٠٪ · الحالة المقترحة قيد التنفيذ
نسبة مُدَّعاة ٣٥٪ — لم تُطبَّق على المخرَج · الحالة المقترحة قيد التنفيذ
```

## 5) بوّابة الوثائق R2.1

| الملفّ | الحجم | بصمة SHA-256 (مُعاد قياسها الآن) |
|---|---|---|
| `Docs/Guides/Marketing-Experts-Client360-Project360-Operating-and-UAT-Guide-AR-R2.1.docx` | 23,941,387 B | `ca0747ae7359d3581b40d04002ced632d7c70b065e99fad9b56ba8acc672ec21` |
| `Docs/Guides/Marketing-Experts-Client360-Project360-Operating-and-UAT-Guide-AR-R2.1.pdf` | 21,160,722 B | `553d5035669d26d37df28604798fc480b076bba32dc5637074c2b78084e269c7` |

البصمتان **مطابقتان** لما سُجِّل عند التوليد ⟹ الملفّان لم يتغيّرا. 240 صفحة · 121 شكلًا · 0 صفحة فارغة.

## 6) ما لم يُنفَّذ في هذه البوّابة

- `NOT EXECUTED`: نشر RC · نشر الإنتاج · أيّ وسم · تحريك `origin/main` · دفع قسريّ · أيّ لمس لـTEST ·
  أيّ هجرة · أيّ تغيير في شيفرة المنتج أو الواجهة.
- `CLAUDE.md` بقي معدَّلًا وغير ملتزَم كما هو (تعديل المستخدم السابق لم يُمسّ).
- **مُحدَّث:** `BASELINE-DEFECT-01` **أُغلِق** بتجهيز اختبار فقط (§2-أ · `cc4c387`)، ودُفِع إلى `origin/develop`
  تقديمًا سريعًا بلا `--force`. `BASELINE-DEFECT-02` ما زال مفتوحًا خارج نطاق هذه التذكرة.
