# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 11: تقرير عناقيد الفشل (Failure Cluster Report)

> ## Historical / Superseded Measurement — قياس تاريخيّ، لا يُستشهد به كحكم حالي
> جولات القياس المذكورة هنا (**`cand3` = 67/1982** وما قبلها، ومنها الرقمان **118** و**130**)
> **باطلة إجرائيًّا** ولا تُعاد إلى أيّ حكم. تُحفَظ لتوثيق مسار التشخيص فقط.
> **الحكم النافذ:** التقرير 23 (النهائيّ) والتقرير 15 (بروتوكول القياس):
> **True Unified Candidate Regression = 0** بجولة `cand10`
> (`Failed 1 / Passed 1992 / Total 1993` + وحدوي `359/359`)، والفشل الوحيد
> `BASELINE-DEFECT-01` مشترك مع الأبوَين ⟹ ليس انحدار مرشَّح.

**التذكرة:** `RECONCILE-PROD-DEVELOP-LINEAGE`
**المرحلة:** G — التشخيص الجذريّ (التقرير 3 في تسلسل المستخدم)
**التاريخ:** 16 أغسطس 2026
**المرشَّح المقيس:** `/tmp/recon-int` (الدمج الموحّد `ac0d86c` + إصلاحات المرحلة G)
**قواعد القياس:** `rr_cand_main` · `rr_cand_cal` · `rr_cand_pfe` — **مُعاد إنشاؤها نظيفة** قبل كلّ قياس

---

## 0) القاعدة المنهجيّة الحاكمة

لا يُصنَّف فشلٌ بناءً على جولة واحدة. كلّ عنقود أدناه مُصنَّف بعد:

1. قياس المرشَّح على قواعد نظيفة (`dropdb`/`createdb` + تمهيد متتابع + عبارة `Classification='Supplementary'`).
2. قياس **الأبوين** (`10c26f7` develop و`ce166662` Production) على قواعد نظيفة متكافئة.
3. المطابقة **بالاسم الكامل للاختبار** لا بالعدد.

---

## 1) الاكتشاف المنهجيّ الأهمّ: عنقود «التلوّث» كان يبتلع التشخيص

| القياس | القاعدة | المدّة | الفشل |
|---|---|---|---|
| `cand3.log` | `rr_cand_main` **292 MB** (3 جولات متراكمة) + `reporting_test` **25 GB** مسرَّبة عبر مصانع غير معزولة | **5 س 11 د** | **67** |
| `cand4.log` | نفس الشجرة، قواعد **مُعاد إنشاؤها نظيفة** + 4 عزلات مصانع مُعاد تطبيقها | **7 د 21 ث** | **13** |

**الفارق 54 فشلًا و5 ساعات ليس تغيّرًا في الكود** — لم يتغيّر سطر واحد بين القياسين. إنّه **أثر تلوّث قاعدة** بحت.

### 1.1 عنقود C0 — Contamination Artifact (54 فشلًا، مُلغى)

| التوقيع | العدد في `cand3` | العدد في `cand4` |
|---|---|---|
| `RoleAwareReminderScheduleTests.*` | 13 | 0 |
| `EmailNotificationsUiTests.*` | 10 | 0 |
| `LeaveRequestsHrTests.*` | 7 | 0 |
| `LeaveDeductionOnTeamLeaderApprovalTests.*` | 6 | 0 |
| `ProjectFirstExecutionAggregationTests.*` | 8 | 0 |
| `RepeatableNumericValidationIntegrationTests.*` | 2 | 0 |
| متفرّقات ذات شكل Timeout | 8 | 0 |

**السبب الجذريّ:** حجم القاعدة يُطيل زمن الاستعلام حتّى يتجاوز مهلة العميل، فيظهر الفشل بشكل تأكيد خاطئ (`Collection: []`, `Timeout`) لا بشكل خطأ منطقيّ.

**التصحيح المُدرَج على تصنيفات سابقة:**
- `ProjectFirstExecutionAggregationTests` (8) كان مصنَّفًا **Class B — عيب أساس إنتاجيّ** ⟹ **خطأ**؛ 8/8 خضراء على قاعدة نظيفة.
- `RepeatableNumericValidationIntegrationTests` (2) كان مصنَّفًا كذلك ⟹ **خطأ**؛ 13/13 خضراء على قاعدة نظيفة.

**قاعدة ثابتة مستخلَصة:** أيّ جولة انحدار تنحرف مدّتها أو يتجمّع فشلها في عناقيد التذكيرات/البريد/الإجازات ⟹ **أثر تلوّث حتّى يثبت العكس**. لا يُقبل قياسٌ إلّا على قاعدة مُعاد إنشاؤها.

---

## 2) العناقيد الحقيقيّة الثلاثة عشر (`cand4.log` — قواعد نظيفة)

المطابقة بالاسم مع الأبوين:

| # | الاختبار | develop `10c26f7` | Production `ce166662` | العنقود |
|---|---|---|---|---|
| 1 | `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete` | **FAIL** | **FAIL** | **K1** |
| 2 | `ComplianceDueLateTests.Compliance_AllSubmittedOnTime_IsCompliant` | PASS | FAIL | **K2** |
| 3 | `ComplianceDueLateTests.Compliance_PartialSubmission_IsNonCompliant` | PASS | FAIL | **K2** |
| 4 | `ComplianceDueLateTests.Compliance_LateSubmission_CountsAsLate` | PASS | FAIL | **K2** |
| 5 | `ComplianceDueLateTests.Compliance_MissingDay_IsOverdue` | PASS | FAIL | **K2** |
| 6 | `SalesAggregationTests.Weekly_Aggregation_SumsDailyReports_UnderWeekKey` | PASS | FAIL | **K3** |
| 7 | `SalesAggregationTests.Monthly_Aggregation_SumsDailyReports_UnderMonthKey` | PASS | FAIL | **K3** |
| 8 | `SalesAggregationTests.Quarterly_Aggregation_SumsDailyReports_UnderQuarterKey` | PASS | FAIL | **K3** |
| 9 | `SalesContextTests.Aggregation_AsRep_CannotSeeColleagueData` | PASS | FAIL | **K3** |
| 10 | `TeamLeaderSalesScopeTests.<حالة 1>` | PASS | FAIL | **K3** |
| 11 | `TeamLeaderSalesScopeTests.<حالة 2>` | PASS | FAIL | **K3** |
| 12 | `SubmissionReminderTests.AlreadySubmitted_DoesNotRemind` | PASS | FAIL | **K4** |
| 13 | `OrgHierarchyTests.PendingReport_Visible_To_Manager_Scope_Not_Other_Branch` | PASS | FAIL | **K5** |

**الملاحظة الحاسمة:** **لا يوجد ولا اختبار واحد** ينجح على **كلا** الأبوين ويفشل على المرشَّح. الثلاثة عشر جميعًا إمّا فاشلة على الأبوين معًا (K1) أو **موروثة من الأب الإنتاجيّ** (K2–K5).

---

## 3) تفكيك العناقيد بالسبب الجذريّ

### K1 — عيب تاريخيّ مشترك (Shared Historical Defect) — اختبار واحد

- **التوقيع:** `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete`
- **السلوك:** ينجح منفردًا، يفشل ضمن المجموعة الكاملة (order-dependent).
- **الدليل:** يفشل بالسلوك ذاته على **قاعدتَين نظيفتَين مستقلّتَين** وعلى **الأبوين معًا** ⟹ ليس تلوّث بيئة وليس انحدارًا للمرشَّح.
- **المُعرّف المفتوح:** `BASELINE-DEFECT-01` — تذكرة عزل اختبارات مستقلّة (في الطابور).
- **الحكم:** **خارج نطاق انحدار المرشَّح.**

### K2 — دَين اختبار إنتاجيّ: أرضيّة انطباق سبت المبيعات — 4 اختبارات

- **العقد الحيّ:** `SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1` —
  `ReportingCalendarPolicy.SalesSaturdayApplicabilityFloor = 2026-07-25`؛
  `IsDailyExpectedBusinessDay(date, saturdayEnabled)` تُرجِع `true` للسبت متى `date >= Floor`.
- **خطأ الاختبار:** كان يستبعد السبت **بلا شرط** ويُصلِّب العدد `5` أيّام عمل.
- **السبب الجذريّ:** الاختبار سبق العقد الحيّ؛ الخادم اليوم يعدّ 6 أيّام لأدوار المبيعات بعد الأرضيّة.
- **العلاج (§11 — الاختبار يُصحَّح، المنتج لا يُمسّ):** تفويض القرار إلى السياسة الحيّة نفسها:
  ```csharp
  for (var d = start; d <= end; d = d.AddDays(1))
      if (ReportingCalendarPolicy.IsDailyExpectedBusinessDay(d, saturdayEnabled: true))
          list.Add(d);
  ```
  وإحلال `days.Count` محلّ كلّ `5` مُصلَّبة، والتسميات صارت استيفاءً نصّيًّا.
- **الملفّ:** `tests/Reporting.IntegrationTests/ComplianceDueLateTests.cs`

### K3 — مفاتيح فترات مستقبليّة/غير صالحة تحجبها بوّابات التقويم الحيّة — 6 اختبارات

- **العقود الحيّة:** `calendar.future_day_locked` · `calendar.cycle_not_open` · `calendar.day_is_holiday`.
- **خطأ الاختبار:** مفاتيح مُصلَّبة بعيدة في المستقبل (`new DateOnly(2027|2028, …)`، `2027-05`، `2027-Q3`) كانت تُستعمل تاريخيًّا لتفادي التصادم على القاعدة المشتركة؛ ومنها زوج (خميس + **جمعة**) والجمعة مرفوضة دائمًا.
- **لماذا نجت من مُرقِّع المفاتيح:** المُرقِّع يطابق سلاسل المفاتيح النصّيّة، وهذه كانت **نداءات مُنشئ `DateOnly`** أو مفاتيح شهريّة/ربعيّة بصيغة غير مطابقة.
- **العلاج:** اشتقاق المفاتيح من `TestCalendar` (المشتقّ بدوره من `ReportingCalendarPolicy.RiyadhToday`)، وإضافة `DaysInPreviousMonth` / `MonthKeyOf` / `QuarterKeyOf`؛ واستبدال يوم الجمعة بالأحد (`start.AddDays(3)`) ضمن نفس الأسبوع التشغيليّ؛ وفرض `CultureInfo.InvariantCulture` لأنّ ثقافة النظام قد تكون هجريّة فتُنتِج مفتاحًا خاطئًا.
- **الملفّات:** `SalesAggregationTests.cs` · `SalesContextTests.cs` · `TeamLeaderSalesScopeTests.cs` · `TestCalendar.cs`

### K4 — تصادم عقد: ترقيم الأسابيع مُصلَّب — اختبار واحد

- **خطأ الاختبار:** `private const string WeekKey = "2026-W25";` بينما `SubmissionReminderService` يشتقّ المفتاح من `ReportCalendarPolicy.WeekKeyFor(dueDate)`. فالتسليم المبذور لم يكن يطابق المفتاح الذي يبحث عنه الخادم ⟹ التذكير يُرسَل رغم وجود التسليم.
- **العلاج:** اشتقاق المفتاح من نفس السياسة: `private static string WeekKey => ReportCalendarPolicy.WeekKeyFor(DueWednesday);`
- **الحكم:** ترقيم الأسابيع **سلوك خادميّ**؛ تصليبه في الاختبار خطأ اختبار لا خطأ منتج.
- **الملفّ:** `SubmissionReminderTests.cs`

### K5 — اختبار مُتجاوَز: دلالة «التقارير المعلَّقة» انقلبت — اختبار واحد

- **العقود الحيّة:** `REPORT-EXPECTED-SUBMISSION-STATUS-R1` (المسار صار **users-first**: LEFT JOIN من المستخدمين المتوقَّع منهم إلى التسليمات) + `REPORT-EXPECTED-ENTITLEMENT-CONTRACT-R1` (لا صفّ «متوقَّع» لقالب لا يستطيع المستخدم تسليمه أصلًا).
- **خطأ الاختبار:** كان يفترض الدلالة القديمة (submission-first)، فبنى مستخدمًا **بلا `JobRoleId`** وقالبًا **بلا `JobRoleId`**؛ والمُحلِّل يتخطّى كليهما:
  ```csharp
  if (u.JobRoleId is not Guid userJobRole) continue;
  if (!templateByJobRole.TryGetValue(userJobRole, out var tpl)) continue;
  ```
  فالنتيجة `Collection: []` — وهو **السلوك الصحيح للعقد الحيّ**.
- **العلاج:** مُساعد `MakeExpectedAsync` يُنشئ مسمّى وظيفيًّا ويربط به القالب والمستخدم معًا، ويُرجِع `CreatedAtUtc` و`PublishedAtUtc` إلى ما قبل `ApplicabilityFloorPolicy.OrganizationalReportingLaunchFloor (2026-07-04)` حتّى تصير الدورة الماضية **مطالَبة ومتأخّرة** لا «قبل الأرضيّة».
- **الملفّ:** `OrgHierarchyTests.cs`

---

## 4) الخلاصة الكمّيّة

| العنقود | العدد | الطبيعة | الجهة المسؤولة | الإجراء |
|---|---|---|---|---|
| C0 | 54 | أثر تلوّث قاعدة | البيئة | مُلغى — أُعيد القياس |
| K1 | 1 | عيب تاريخيّ مشترك | الأبوان معًا | تذكرة مستقلّة (`BASELINE-DEFECT-01`) |
| K2 | 4 | دَين اختبار إنتاجيّ | الاختبار | **أُصلِح** |
| K3 | 6 | مفاتيح فترات باطلة | الاختبار | **أُصلِح** |
| K4 | 1 | تصادم عقد (ترقيم الأسابيع) | الاختبار | **أُصلِح** |
| K5 | 1 | اختبار مُتجاوَز (دلالة جديدة) | الاختبار | **أُصلِح** |
| **انحدار المرشَّح الحقيقيّ** | **0** | — | — | — |

**لم يُعدَّل سطر منتج واحد لإرضاء اختبار.** كلّ الإصلاحات وقعت في `tests/` حصرًا، وكلٌّ منها مُسنَد إلى عقد إنتاجيّ حيّ موثَّق.

---

## 5) الأثر على الحكم السابق

الرقم **118** المُعلَن سابقًا كـ«الانحدار الموحّد» **باطل منهجيًّا**: قيس على قاعدة ملوَّثة وعلى مفهوم «الفشل = انحدار» بلا مطابقة بالاسم مع الأبوين. الحكم الصحيح مذكور في التقرير 13.
