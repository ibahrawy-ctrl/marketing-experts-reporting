# مراجعة حقيقة الكود — العشرون سطحًا (§7)

**التذكرة:** `PROJECT360-R2-UAT-EVIDENCE-AND-GUIDE-CLOSURE-R2.1` · **الأساس:** `7611f0e` · **التاريخ:** ١٧ أغسطس ٢٠٢٦

تصنيف كلّ حقيقة أدناه: `[CURRENT CODE REALITY]` ما لم يُذكر خلافه. الأحكام مبنيّة على قراءة الكود مباشرةً لا على تلخيص.

---

## 1) جدول الأسطح العشرين

| # | السطح | الموضع (برهان) | الحكم |
|---|---|---|---|
| 1 | رؤية المشروع | `Project360Authorization.cs:43-64` `LoadVisibleProjectAsync` | PASS |
| 2 | تصريح الكتابة على المشروع | طبقتان: `CanManageProject360Async:83` (بنيويّ) · `CanUpdateProject360ProgressAsync` (تشغيليّ) | PASS |
| 3 | `TeamLeaderId` | `Project` + `ProjectService.cs:374` يحتسب `canOperate` في DTO القائمة | PASS |
| 4 | `ProjectOwnerId` | مشمول صراحةً في التصريح البنيويّ (`Project360Authorization.cs:96`) | PASS |
| 5 | Workstream CRUD (خادم) | `ProjectWorkstreamsController.cs` + `ProjectWorkstreamService.cs:179-185` — `CanManagePlanAsync` **يمنح `p.TeamLeaderId == uid`** | PASS |
| 5ب | Workstream CRUD (واجهة) | **لا يوجد أيّ مكوّن** — `components/project360/` ثمانية تبويبات بلا Workstreams | **FAIL ⟹ `GAP-R21-01`** |
| 6 | تحديث المخرَج التعاقديّ التشغيليّ | `ProjectContractDeliverableService.cs:170-198` (`UpdateProgressAsync` ⟵ `CanUpdateProject360ProgressAsync`) + `ProjectContractDeliverablesTab.tsx:187-220` مقيَّد بـ`access.canOperate` | PASS |
| 7 | ادّعاءات التنفيذ | `ProjectExecutionBridgeService.cs:88-116` — `Pending` لا يمسّ رقمًا ولا يستدعي `SaveWithHealthAsync` | PASS |
| 8 | مراجعة الادّعاء والتعادليّة | `…:145-151` حارس: نفس القرار ⟹ نجاح بلا أثر؛ قرار مخالف ⟹ `ProposalAlreadyReviewed` | PASS |
| 9 | إنشاء قراءة مؤشّر | `ProjectKpiService.cs:242` + `ProjectKpiReading.RecordedByUserId:40` + `ProjectKpisTab.tsx:385` | PASS |
| 10 | احتساب تقدّم المشروع | `ProjectHealthService.SaveWithHealthAsync` مصدر وحيد يُستدعى بعد كلّ طفرة؛ الربط بـGUID (`ProjectDeliverable.ProjectId`) لا بالاسم | PASS |
| 11 | احتساب الصحّة | `ProjectHealthService.cs` — درجة وحالة وأسباب ووقت احتساب | PASS |
| 12 | ربط الحوكمة بـ`ProjectId` | `Risk.cs:26` · `Decision.cs:32` + حمولة الإنشاء (أُصلِح في R2) | PASS |
| 13 | ملخّصات المشاريع في Client 360 | `ClientDetailPage.tsx:900-945` — الجدول: المشروع/الخدمة/الحالة/الفريق/البداية/النهاية **فقط**، بينما `ProjectDto` (`types/api.ts:2174-2187`) يحمل `progressPercent` و`progressMode` و`healthStatus` و`healthPercent` | **FAIL ⟹ `GAP-R21-02`** |
| 14 | لوحة الإدارة التنفيذيّة | `ExecutiveDashboardService.cs:207-220` يجمّع على `(r.Client, r.Project)` **نصًّا** من `PodExecutionAggregationService`، و`ProgressPercent` فيها = متوسّط `ProgressPercentAvg` من صفوف التقارير — **رقم مختلف عن تقدّم Project 360** | **مقيَّد ⟹ `GAP-R21-04`** (انظر §3) |
| 14ب | التجميع المرتبط بالمعرّف | `ProjectFirstExecutionAggregationService.cs` — يجمّع على `ProjectId` (GUID) ويربط بجدول `Projects` (`:311-348`) | PASS |
| 15 | التدقيق والتاريخ | جسر التنفيذ: `PreviousProgressPercent:178` + `ReviewedById/AtUtc/Note` معروضة (`ProjectExecutionBridgeTab.tsx:248,280-286`) → PASS. **التحديث المباشر** للمخرَج: `_audit.LogAsync(...)` بلا `dataJson` (`ProjectContractDeliverableService.cs:195`) ⟹ **لا قيمة سابقة مخزَّنة ولا معروضة** | **FAIL ⟹ `GAP-R21-03`** |
| 16 | حرّاس القدرات في الواجهة | `overview.access` مصدر وحيد؛ لا اشتقاق من الدور في العميل | PASS |
| 17 | حرّاس المسارات | `App.tsx:61` `EXEC_ROLES` يضمّ `TeamLeader` · `:98` `PROJECT_360_ROLES` يضمّ `Employee` | PASS |
| 18 | اكتمال الـDTO | `ProjectDto` يحمل التقدّم والصحّة والقدرات؛ لا نقص خادميّ | PASS |
| 19 | حمولات الـAPI | `projectId` في إنشاء الخطر · `ProjectOwnerId`/`TeamLeaderId` في إنشاء/تعديل المشروع (أُصلِحت في R2) | PASS |
| 20 | منع التعداد | `404 project.not_found` موحّد لغير المرئيّ مقابل `403 auth.forbidden` للمرئيّ الممنوع | PASS |

**16 PASS · 3 FAIL · 1 مقيَّد.**

---

## 2) العيوب المفتوحة في هذه التذكرة

| المعرّف | الخرق | الأثر على §8 |
|---|---|---|
| `GAP-R21-01` | **لا شاشة Workstreams إطلاقًا** في الواجهة رغم أنّ الخادم يفتحها لقائد الفريق المُسنَد | `TEAM_LEADER_CAN_MANAGE_WORKSTREAM` يسقط طرف-إلى-طرف؛ وخطوتا §12 رقم 7 و28 غير قابلتَين للتنفيذ |
| `GAP-R21-02` | ملخّص مشاريع العميل لا يعرض التقدّم ولا الصحّة رغم حملهما في الـDTO | سهم §5.4 `→ Client 360 / Projects Summary` غير مرئيّ ⟹ `ACCOUNT_MANAGER_SEES_PROPAGATION` بلا دليل بصريّ |
| `GAP-R21-03` | التحديث التشغيليّ المباشر للمخرَج لا يحفظ القيمة السابقة ولا يُظهر من غيّر ماذا ومتى | §5.5 مكسور لمسار «مباشر»؛ `AUDIT_TRAIL_IDENTIFIES_ACTOR` جزئيّ |

---

## 3) `GAP-R21-04` — حدّ معماريّ مُصرَّح به لا عيب

`ExecutiveDashboardService` و`PodExecutionAggregationService` يعملان على الحقلين النصّيّين `Client`/`Project` في صفوف
التقارير التشغيليّة (`POD`)، و`ProgressPercent` فيهما = **متوسّط تقدّم المهامّ المُبلَّغ من الموظّفين في التقارير**،
لا **المتوسّط الموزون لمخرَجات المشروع التعاقديّة**. رقمان مختلفان بمصدرين مختلفين.

**القرار المتّخذ داخل هذه التذكرة:** لا يُوحَّد الرقمان ولا تُعاد هندسة تجميع `POD` (خارج النطاق، ويمسّ مسار تقارير
كامل لا علاقة له بـProject 360). المطلوب من §5.2 هو **التمييز الواضح** لا التوحيد ⟹ يُنفَّذ التمييز في التسمية
داخل دليل R2.1 وفي هذا التقرير صراحةً، ويُثبَت أنّ المسار المرتبط بالمعرّف موجود فعلًا وهو
`ProjectFirstExecutionAggregationService` (`AggregateByProjectAsync` على `ProjectId`).
**الرقم الرسميّ الوحيد لتقدّم المشروع في Project 360 هو `Project.ProgressPercent` المحتسَب في `ProjectHealthService`.**

---

## 4) مصفوفة `ROLE × ACTION × SCOPE × UI × API × EXPECTED`

`IN` = داخل النطاق (قائد الفريق/المالك/عضو الفريق للمشروع نفسه) · `OUT` = خارجه.

| الدور | الفعل | النطاق | UI | API | المتوقَّع |
|---|---|---|---|---|---|
| Admin / CEO / GM | كلّ الأفعال | الكلّ | ظاهر | مسموح | `200` |
| Project Owner | تعديل بنية المشروع (موجز/استراتيجيّة/أهداف/مؤشّرات/مخرَجات/Workstream) | IN | ظاهر | `CanManageProject360Async` | `200` |
| Project Owner | حذف/أرشفة المشروع | IN | ظاهر | `Policies.ProjectStructuralManage` | `200` |
| Team Leader | عرض تفاصيل المشروع | IN | ظاهر | `LoadVisibleProjectAsync` | `200` |
| Team Leader | مراجعة ادّعاء (قبول/رفض) | IN | ظاهر | `CanUpdateProject360ProgressAsync` | `200` |
| Team Leader | رفض بلا ملاحظة | IN | زرّ معطَّل | حارس خادم | `400 proposal.reject_reason_required` |
| Team Leader | تحديث تقدّم مخرَج تعاقديّ | IN | ظاهر (`canOperate`) | `CanUpdateProject360ProgressAsync` | `200` |
| Team Leader | تسجيل قراءة مؤشّر يدويّ | IN | ظاهر (`canOperate`) | `CanUpdateProject360ProgressAsync` | `200` |
| Team Leader | إدارة Workstream | IN | **مفقود ⟹ `GAP-R21-01`** | `CanManagePlanAsync` يمنحه | `200` بعد الإصلاح |
| Team Leader | حذف/أرشفة المشروع | IN | مخفيّ | `ProjectStructuralManage` | `403` |
| Team Leader | إنشاء/تعديل مخرَج تعاقديّ (بنية) | IN | مخفيّ | `CanManageProject360Async` | `403` |
| Team Leader | أيّ فعل | OUT | لا مسار | `LoadVisibleProjectAsync` | **`404 project.not_found`** |
| Account Manager | عرض Client 360 / Project 360 وتطوّر المخرَجات | IN | ظاهر | مرئيّ | `200` |
| Account Manager | تحديث تشغيليّ (تقدّم/قراءة) | IN | ظاهر (`canOperate`) | `CanUpdateProject360ProgressAsync` | `200` |
| Account Manager | حذف/أرشفة | IN | مخفيّ | `ProjectStructuralManage` | `403` |
| Employee (عضو الفريق) | رفع ادّعاء تنفيذ | IN | ظاهر | جسر التنفيذ | `201` |
| Employee (عضو الفريق) | حسم ادّعائه | IN | مخفيّ | `CanUpdateProject360ProgressAsync` | `403` |
| Employee (عضو الفريق) | أيّ كتابة بنيويّة | IN | مخفيّ | `CanManageProject360Async` | `403` |
| Employee (خارج الفريق) | أيّ فعل | OUT | لا مسار | `LoadVisibleProjectAsync` | **`404`** |
| Viewer | قراءة | OUT | لا مسار | — | **`404`** |

**قاعدة حاكمة مُثبَتة في الكود:** إخفاء الزرّ في الواجهة ليس حماية — كلّ صفّ «مخفيّ» أعلاه له حارس خادم مستقلّ
في `Project360Authorization` أو `Policies`، وهو الحارس الذي تُقاس عليه الحالة.
