# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 17: سجلّ الإصلاح الجذريّ (Root-Cause Repair Ledger)

**التذكرة:** `RECONCILE-PROD-DEVELOP-LINEAGE`
**المرحلة:** G/H — التقرير 6 في تسلسل المستخدم
**التاريخ:** 16 أغسطس 2026
**الحكم:** **12 فشلًا أُصلِح بجذره · صفر تعديل منتج لإرضاء اختبار · صفر حلّ التفافيّ (Skip/Ignore/Retry)**

---

## 0) القاعدة الحاكمة (§11 من التذكرة)

> «إذا كان العيب في Test/Fixture: صحّح الاختبار **مع دليل** أنّ توقّعه قديم أو خاطئ. **لا تغيّر المنتج لإرضائه.**»

لذلك كلّ سطر في هذا السجلّ يحمل ثلاثة أعمدة إلزاميّة: **العَرَض** · **السبب الجذريّ** · **العقد الإنتاجيّ الحيّ الذي يُثبت قِدَم توقّع الاختبار**.

**ما لم يُستعمل إطلاقًا:** `[Skip]` · `[Trait("Category","Flaky")]` · `try/catch` حول تأكيد · إعادة محاولة · تخفيف تأكيد إلى `Assert.True(true)` · تعديل `src/` لتمرير اختبار.

---

## 1) لوحة القيادة

| المؤشّر | القيمة |
|---|---|
| فشل أُصلِح بجذره | **12** |
| ملفّات `src/` عُدِّلت لإرضاء اختبار | **0** |
| اختبارات حُذفت أو عُطِّلت | **0** |
| اختبارات أُضعِفت تأكيداتها | **0** |
| ملفّات اختبار مُعدَّلة (المجموع) | 33 |
| ملفّ اختبار جديد (بنية تحتيّة) | 1 (`TestCalendar.cs`) |
| صافي تغيير الاختبارات | +314 / −260 سطرًا |

---

## 2) السجلّ التفصيليّ

### R-01 — انطباق يوم السبت لأدوار المبيعات (Class F · CC-01 · 4 اختبارات)

| البند | التفصيل |
|---|---|
| **الملفّ** | `ComplianceDueLateTests.cs` |
| **الاختبارات** | `DailySales_AllWorkingDays_FullCompliance` · `DailySales_PartialDays_LateAndMissingOverdue` · `DailySales_SubmittedAfterDay_IsLateSubmitted` · `DailySales_Draft_DoesNotCountAsSubmitted` |
| **العَرَض** | `Assert.Equal(5, summary.Expected)` ⟹ `Actual: 6`، و`"سلّم 5 من 5 يوم"` ⟹ `"سلّم 6 من 6 يوم"` |
| **السبب الجذريّ** | الاختبار يُصلِّب الرقم 5 (الأحد→الخميس). العقد الحيّ يجعل **السبت يوم عمل** لأدوار المبيعات ابتداءً من `ReportingCalendarPolicy.SalesSaturdayApplicabilityFloor = 2026-07-25` ⟹ 6 أيّام متوقَّعة |
| **الدليل على قِدَم التوقّع** | `SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1` منشورة على الإنتاج؛ `IsDailyExpectedBusinessDay(date, saturdayEnabled)`؛ الأسطح الأربعة (`ReportDueService`, `ReportReminderService`, `ReportingService`, `SubmissionService`) تفوّض إليها بـ`saturdayEnabled: true` |
| **الإصلاح** | استبدال الثابت 5 بـ`days.Count` المشتقّ من نفس مجموعة الأيّام التي بذرها الاختبار، وتحويل نصوص التأكيد إلى قوالب: `$"سلّم {days.Count} من {days.Count} يوم"` |
| **لماذا هذا جذريّ لا التفافيّ** | التأكيد صار يقيس **العلاقة** (سلّم كلّ المتوقَّع / سلّم 3 من المتوقَّع) بدل عدد مُصلَّب؛ فهو يظلّ صحيحًا على جانبَي أرضيّة السبت ولا يُخفي أيّ خطأ |
| **تعديل منتج** | لا شيء |

### R-02 — ترقيم الأسابيع سلوك خادميّ (Class E · CC-04 · 1 اختبار)

| البند | التفصيل |
|---|---|
| **الملفّ** | `SubmissionReminderTests.cs` |
| **العَرَض** | التذكير لا يُولَّد؛ التسليم المبذور لا يطابق ما يبحث عنه الخادم |
| **السبب الجذريّ** | `private const string WeekKey = "2026-W25";` مُصلَّب، بينما `SubmissionReminderService` يشتقّ المفتاح عبر `ReportCalendarPolicy.WeekKeyFor(...)` (أسابيع خميس→أربعاء). السلسلتان اختلفتا ⟹ لا تطابُق |
| **الدليل** | وجود فئتَي تقويم متمايزتَين: `ReportCalendarPolicy` (خميس→أربعاء) و`ReportingCalendarPolicy` (سبت→جمعة، ترقيم ISO الثلاثائيّ). المفتاح النصّيّ لا يُطابق إلّا صدفةً |
| **الإصلاح** | `private static string WeekKey => ReportCalendarPolicy.WeekKeyFor(DueWednesday);` — نفس مصدر الحقيقة الذي يستعمله الخادم |
| **تعديل منتج** | لا شيء |

### R-03 — دلالة «تحتاج إجراء» users-first وأرضيّة الانطباق (Class G · CC-02 + CC-03 · 1 اختبار)

| البند | التفصيل |
|---|---|
| **الملفّ** | `OrgHierarchyTests.cs` — `PendingReport_Visible_To_Manager_Scope_Not_Other_Branch` |
| **العَرَض** | المسودّة لا تظهر في `/api/dashboard/pending-reports` للمدير ⟹ `Assert.Contains` يفشل |
| **السبب الجذريّ** | العقد القديم submission-first (تُقرأ التسليمات ويُشتقّ منها المعلَّق). العقد الحيّ **users-first**: يُبدأ من المستخدمين **المطالَبين**، ثمّ LEFT JOIN إلى التسليمات. والمطالَبة تتطلّب شرطَين لم تُحقّقهما التجهيزة: (أ) مسمّى وظيفيّ مشترك بين المستخدم والقالب، (ب) دورة **بعد** أرضيّة الانطباق |
| **الدليل** | `REPORT-EXPECTED-SUBMISSION-STATUS-R1` · `REPORT-EXPECTED-ENTITLEMENT-CONTRACT-R1` · `ExpectedSubmissionStatusResolver.cs:158-163` (تخطّي بلا `JobRoleId` أو بلا قالب مطابق)، `:174-191` (أرضيّة الانطباق)، `:274` و`:374-378` (`IsActionable`) · `ApplicabilityFloorPolicy.Resolve = max(UserCreatedAt, TemplateFirstPublishedAt, AuditedJobRoleAssignedAt)` |
| **الإصلاح** | إضافة مساعد `MakeExpectedAsync(templateId, userId)` يُنشئ `JobRole` ويربطه بالمستخدم **وبالقالب**، ويُرجِع `user.CreatedAtUtc` و`ReportTemplateVersions.PublishedAtUtc` إلى `2026-07-01` (قبل الأرضيّة) — فتصير الدورة الماضية **مطالَبة فعلًا** بدل «قبل الأرضيّة» |
| **سابقة قائمة في الشجرة** | نفس النمط مستعمل أصلًا في `DailyApplicabilityUnifiedOverdueTests` — أي أنّ الإصلاح يتبع عرفًا موجودًا لا يخترعه |
| **تعديل منتج** | لا شيء |

### R-04 — بوّابات التقويم الحيّة ترفض المفاتيح المستقبليّة (Class H · CC-05 · 6 اختبارات)

| البند | التفصيل |
|---|---|
| **الملفّات** | `SalesAggregationTests.cs` · `SalesContextTests.cs` · `TeamLeaderSalesScopeTests.cs` |
| **العَرَض** | `calendar.cycle_not_open` / `calendar.future_day_locked` صراحةً، أو أثرها اللاحق `Rows = 0` بعد رفض إنشاء التسليم بصمت |
| **السبب الجذريّ** | عرف قديم في الاختبارات: استعمال تواريخ بعيدة في المستقبل (`2027`, `2028`, `2099`, `2026-W201`) لضمان التفرّد على قاعدة الاختبار **المشتركة**. ثمّ أضاف خطّ الإنتاج بوّابات تمنع الإنشاء على فترة لم تبدأ |
| **الدليل** | `ROLE-AWARE-REPORTING-CALENDAR §2.4` · `DAILY-BUSINESS-DAY-COMPLIANCE-R1` — رموز `calendar.cycle_not_open`, `calendar.future_day_locked`, `calendar.day_is_holiday`. هذه **حماية أعمال حقيقيّة** (لا يُنشأ تقرير عن فترة لم تبدأ) ⟹ الخطأ في الاختبار |
| **الإصلاح** | المولّد الجديد `TestCalendar.cs` — انظر §3 |
| **بديل التفرّد** | لم يعد التفرّد يعتمد على بُعد التاريخ: كلّ اختبار ينشئ مستخدميه الخاصّين، والقياس صار على قواعد نظيفة معزولة |
| **تعديل منتج** | لا شيء |

---

## 3) البنية التحتيّة المضافة — `TestCalendar.cs`

ملفّ **جديد واحد** (100 سطر، `internal static class`، داخل `tests/` حصرًا) يشتقّ كلّ مفاتيح الفترات من **نفس سياسة الخادم** بدل تصليبها:

| العضو | الدلالة | الضمان |
|---|---|---|
| `Today` | `ReportingCalendarPolicy.RiyadhToday()` | مرجع كلّ الاشتقاقات |
| `Cycle(weeksBack)` | `CycleKeyFor(Today − 7·weeksBack)` | **لا تُرجِع دورة مستقبليّة أبدًا** |
| `CycleStart(key)` | `CycleRange(key).Start` | — |
| `Day(daysBack)` | يتخطّى `IsDailySubmissionBlockedDay` | ليس مستقبلًا وليس جمعة/عطلة |
| `Days(count, startDaysBack)` | متتالية أيّام صالحة | — |
| `DaysInPreviousMonth(count)` | أيّام داخل الشهر المنقضي | نفس المفتاح الشهريّ **ونفس الربعيّ** |
| `MonthKeyOf` / `QuarterKeyOf` | اشتقاق من مفتاح يوم | — |
| `DayInCycle(cycleKey, i)` | يوم صالح داخل دورة، مقيَّد بـ`min(end, Today)` | يرمي استثناءً واضحًا بدل الفشل الغامض |

**الانتشار المُقاس:** 230 استدعاءً في 26 ملفّ اختبار
(`Cycle` 205 · `Day` 17 · `Today` 4 · `DaysInPreviousMonth` 2 · `MonthKeyOf` 1 · `QuarterKeyOf` 1).

**نطاق الإصلاح بدقّة:** الممنوع ليس كلّ مفتاح مُصلَّب، بل **المفتاح المستقبليّ** الذي ترفضه بوّابة تقويم حيّة. المفاتيح الماضية المُصلَّبة (مثل `"2026-W20"` في `AdminGovernanceTests`) تمرّ من البوّابات وتظلّ صحيحة، ولم تُمسّ (221 مفتاحًا ماضيًا باقيًا عبر الملفّات كلّها).

**اختبار الانحدار المضادّ (Regression Guard):**
```bash
grep -hoE '"20(2[7-9]|[3-9][0-9])-[^"]*"' tests/Reporting.IntegrationTests/*.cs
```
**النتيجة الحاليّة على المرشَّح: تطابُق واحد فقط** —
`TemplateVersionManagementTests.cs:57` ‏`PeriodKey = $"2099-W{Random.Shared.Next(1, 52):D2}"`.
وهو **استثناء مشروع**: الصفّ يُدرَج مباشرةً عبر `db.ReportSubmissions.Add(...)` لا عبر `POST /api/submissions`، فلا يمرّ ببوّابة التقويم أصلًا؛ والغرض ضمان تفرّد صفّ يمنع حذف نسخة قالب. **صفر مفتاح مستقبليّ يمرّ عبر واجهة برمجيّة محكومة بالتقويم.**

وهذه العبارة **تُدرَج في بروتوكول القياس الملزم** (التقرير 15 §3).

---

## 4) ما ليس إصلاحًا — وتحذير إلزاميّ قبل الالتزام

أربعة تعديلات في الشجرة هي **أدوات قياس مؤقّتة** لعزل قواعد البيانات، و**يجب التراجع عنها قبل أيّ التزام أو دفع**:

| الملفّ | التعديل الحاليّ | الأصل الواجب استعادته |
|---|---|---|
| `CalendarIsolatedFactory.cs` | `Database=rr_cand_cal` | `Database=reporting_calendar_iso` |
| `PfeNumericIsolatedFactory.cs` | `Database=rr_cand_pfe` | `Database=reporting_pfe_iso` |
| `ProjectFirstIsolatedFactory.cs` | `Database=rr_cand_pfe` | `Database=reporting_pfe_iso` |
| `Project360ApiSurfaceTests.cs` | `Database=rr_cand_main` | `Database=reporting_test` |

⚠️ **الالتزام بها كما هي يُثبِّت أسماء قواعد قياس مؤقّتة في المستودع.** تُدرَج هذه الاستعادة كخطوة أولى إلزاميّة في المرحلة I.

---

## 5) الالتزامات المنهجيّة المُتحقَّق منها

| الالتزام | التحقّق | النتيجة |
|---|---|---|
| صفر تعديل `src/` لإرضاء اختبار | `git diff --stat -- reporting-backend/src` | ملفّان فقط، وكلاهما **استعادة ميزة إنتاجيّة** لا إرضاء اختبار: `ScopeResolver.cs` (+60) و`RoleCapabilities.cs` (+1) — مُبرَّران ببرهان حياديّة بنيويّ في التقرير 10/CC-06 |
| صفر `[Skip]` مضاف | فحص الفرق | 0 |
| صفر اختبار محذوف | فحص الفرق | 0 |
| كلّ إصلاح مُسنَد إلى عقد منشور | §2 أعلاه | 4/4 مجموعات |
| الأثر مُقاس مرّتين على قاعدتَين نظيفتَين | التقرير 15 | `cand5` = `cand8` = **Failed 1 / 1982** |

---

## 6) اختبارات الانحدار المُضافة للميزات الإنتاجيّة المستعادة (H2)

استُقدِمت ثلاث هجرات إنتاجيّة إلى المرشَّح (التقرير 16 §1.2). فحصُ التغطية أظهر **فجوة حقيقيّة**:

| الميزة المستعادة | الهجرة | التغطية قبل | التغطية بعد |
|---|---|---|---|
| `BypassTeamLeaderApproval` | `20260715162851` | **9 اختبارات** في `FatmaDirectReportingTests` | كما هي (لا تكرار) |
| فهرس `kpi_evaluations` الفريد **الجزئيّ** | `20260716015239` | **صفر** | **2** |
| `ReportApproverOverrideUserId` | `20260724224053` | **صفر** | **4** |
| `KpiReviewerOverrideUserId` | `20260724224053` | **صفر** | **5** |

**الملفّ الجديد:** `tests/Reporting.IntegrationTests/RestoredProductionOverridesTests.cs` — **11 اختبارًا، 11/11 خضراء**.

| # | الاختبار | العقد المُثبَت |
|---|---|---|
| 1 | `ReportApproverOverride_TakesPriority_OverTeamLeaderAndManager` | التجاوز الصريح يصير `CurrentApproverId` مباشرةً؛ لا خطوة قائد فريق ولا مدير |
| 2 | `ReportApproverOverride_PointingToSelf_IsExplicitConfigurationError` | `approval.override_invalid` — لا اعتماد ذات ولا سقوط صامت |
| 3 | `ReportApproverOverride_PointingToInactiveUser_IsExplicitConfigurationError` | `approval.override_invalid` — لا تجاهل صامت |
| 4 | `NoReportApproverOverride_FallbackChain_Unchanged` | **حارس عدم الانحدار**: بلا تجاوز، أوّل معتمِد = قائد الفريق كما قبل الاستعادة |
| 5 | `KpiReviewerOverride_RoutesReview_ToExplicitReviewer` | التجاوز يفوز على سلسلة الاعتماد؛ الحالة `UnderReview` |
| 6 | `KpiReviewerOverride_IsEvaluator_ApprovesDirectlyOnSubmit` | `KPI-REVIEWER-OVERRIDE-R1` — `SelfOverride` ⟹ `Approved` فورًا عند الإرسال |
| 7 | `KpiReviewerOverride_PointingToSubject_IsExplicitConfigurationError` | `kpi.reviewer_override_invalid` |
| 8 | `NoKpiReviewerOverride_FallbackChain_Unchanged` | **حارس عدم الانحدار**: بلا تجاوز، المُراجِع = قائد فريق الموضوع (الخطوة الأولى في `ResolveReviewerAsync`) |
| 9 | `KpiReviewerOverride_GrantsEvaluationScope_ToExplicitReviewer` | حدّ الصلاحيّة: التجاوز يوسّع «القابلين للتقييم» للمُراجِع الصريح **فقط**، ولا يظهر قبل ضبطه |
| 10 | `KpiEvaluation_DuplicateActiveRow_IsRejectedByPartialUniqueIndex` | لا صفّان نشطان لنفس (إصدار، موظّف، فترة) |
| 11 | `KpiEvaluation_SoftDeletedRow_DoesNotBlockNewActiveRow` | **جزئيّة الفهرس** (`"IsDeleted" = false`) — الصفّ المحذوف منطقيًّا لا يحجز المفتاح |

**ملاحظة منهجيّة:** الاختباران 4 و8 مُصمَّمان عمدًا كحارسَي «بلا تجاوز ⟹ السلوك القديم حرفيًّا»، فهما اللذان يمنعان أن تتحوّل الاستعادة إلى انحدار صامت في مسار الاعتماد الافتراضيّ.

**تصحيح موثَّق أثناء الكتابة:** كانت النسخة الأولى من الاختبار 8 تتوقّع المدير العامّ. الفشل كشف أنّ `ResolveReviewerAsync` يبدأ من **سلسلة اعتماد الموضوع** (قائد فريقه) لا من سلسلة مدير المُقيّم — فصُحِّح **التوقّع** لا المنتج، وفق §11.

---

## 7) الأثر التراكميّ للإصلاحات

| المرحلة | الفشل التكامليّ على قاعدة نظيفة |
|---|---|
| المرحلة | الفشل | النجاح | المجموع | الجولة |
|---|---|---|---|---|
| المرشَّح قبل إصلاحات G | 13 | — | — | `cand3` |
| بعد R-01 … R-04 | **1** | 1981 | 1982 | `cand5` ثمّ `cand8` (متطابقتان) |
| بعد إضافة 11 اختبار H2 | **1** | **1992** | **1993** | `cand10-full.log` — 7 د 29 ث · الوحدوي 359/359 |

المجموع ارتفع بمقدار **11 بالضبط** والنجاح بمقدار **11 بالضبط** والفشل **لم يتحرّك**: الاختبارات الأحد عشر خضراء كلّها ولم تُحدث ارتدادًا في أيّ اختبار قائم.

| المتبقّي | `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete` — **Class C**، يفشل بنفس التوقيع على الأبوين معًا ⟹ `BASELINE-DEFECT-01`، خارج نطاق هذه التذكرة |
|---|---|

**True Unified Candidate Regression = 0.**
