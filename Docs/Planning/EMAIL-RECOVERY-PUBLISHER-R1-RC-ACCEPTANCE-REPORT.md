# EMAIL-RECOVERY-PUBLISHER-R1 — تقرير قبول المرشّح على RC

**التاريخ:** 30 يوليو 2026
**النطاق:** بناء أداة CLI آمنة تُشغَّل لمرّة واحدة لإرسال إشعارات Recovery محدَّدة عبر `EmailNotificationService` القائم + اختبارات محليّة + قبول على RC.
**القرار النهائي:** `EMAIL-RECOVERY-PUBLISHER-R1 RC PASS — PRODUCTION TOOL READY`
**الإنتاج:** لم يُمَسّ — صفر إرسال، صفر كتابة، صفر إعادة تشغيل، صفر هجرة.

---

## 1. لماذا الأداة أصلًا (المبرّر البنيويّ)

تشخيص `EMAIL-MISSED-NOTIFICATIONS-RECOVERY-R1` (المرحلة 5) أثبت أنّه **لا توجد آليّة طبيعيّة وآمنة** لإعادة إرسال الإشعارات المفقودة:

- الصفوف التاريخيّة البالغة 139 صفًّا بوضع DryRun تحمل **المفتاح القانونيّ بلا بادئة `dryrun:`** (سابقة لإصلاح R1C غير الرجعيّ) ⇒ أيّ توليد طبيعيّ لنفس المفتاح ينتهي حتمًا إلى `Duplicate` بفعل الفهرس الفريد على `CorrelationKey`.
- القلب الآمن `EnqueueReportReminderAsync`→`EnqueueAsync` يقبل `CorrelationKey` حرًّا لكنّه خدمة داخليّة، ومُستدعياه الوحيدان لا يصلحان (`ReportReminderService` يُصلِّب المفتاح القانونيّ المحجوز؛ `EmailControlService` يُصلِّب `manual-reminder:{Guid}` و`EventType="manual.reminder"`).
- لا DTO ولا endpoint يقبل مفتاحًا مخصَّصًا.

⇒ **مفتاح `recovery:{original-key}:{batchId}` عبر أداة خارجيّة هو الحلّ الوحيد** الذي يحترم الفهرس الفريد ولا يعدّل صفًّا تاريخيًّا ولا يضيف سطح هجوم للـAPI.

---

## 2. خطّ الأساس والتجميد

| البند | القيمة |
|---|---|
| أساس البناء (الإنتاج) | `f3ee32f24323d61258ef15844f66c66adaf279df` |
| شجرة العمل النظيفة | `/private/tmp/cand-email-recovery-publisher-r1-20260730` |
| شجرة الأساس المرجعيّة | `/private/tmp/base-erp-f3ee32f2` (Tree `7cd0d0ae9ae7aac70283b2678361ee253681da38`) |
| **Candidate SHA** | **`74fd98a8a6216c8a98a2ea7172a099b05f7292a5`** |
| **Parent** | **`f3ee32f24323d61258ef15844f66c66adaf279df`** |
| **Tree** | **`11dadefa6ea82da91c94abd8d55d6f8b5c74af0d`** |
| عدد الـCommits فوق الأساس | 1 (واحد حصرًا) |

**ما لم يُستخدَم كأساس:** `develop`، فروع R1E/R1F، أيّ ملفّ WIP أو غير مُتعقَّب.

### سطح الباتش (5 ملفّات، +2004/−0)

```
reporting-backend/tests/Reporting.IntegrationTests/EmailRecoveryPublisherTests.cs      | 988 +++++
reporting-backend/tests/Reporting.IntegrationTests/Reporting.IntegrationTests.csproj   |   3 +
reporting-backend/tools/Reporting.EmailRecoveryPublisher/Program.cs                    | 305 +++++
reporting-backend/tools/Reporting.EmailRecoveryPublisher/RecoveryPublisher.cs          | 685 +++++
reporting-backend/tools/Reporting.EmailRecoveryPublisher/Reporting.EmailRecoveryPublisher.csproj | 23 +
                                                                          5 files changed, 2004 insertions(+)
```

---

## 3. البراهين السلبيّة (على الـCommit المُجمَّد)

| البرهان | الأمر | النتيجة |
|---|---|---|
| **لا هجرة** | `git diff --name-only f3ee32f2 HEAD -- '*Migrations*'` | `0` ملفّ — عدد الهجرات ثابت **30** |
| **لا Endpoint/Controller** | `git diff --name-only … -- '*Controllers*'` | `0` ملفّ |
| **لا سطح ويب في الأداة** | grep على `[HttpGet\|[Route\|ControllerBase\|MapGet\|MapPost\|WebApplication\|IHostedService\|BackgroundService\|Cron` | مطابقة واحدة يتيمة = **تعليق** `Program.cs:16` («لا تعمل تلقائيًّا، لا تستمع على منفذ، ولا تُسجَّل كـ Service/Cron/HostedService») |
| **لا Frontend** | `git diff --name-only … -- 'reporting-frontend/*'` | `0` ملفّ |
| **لا مساس بكود الإنتاج** | `git diff --name-only … -- 'reporting-backend/src/*'` | `0` ملفّ |
| **الأداة خارج الحلّ** | `grep -c EmailRecoveryPublisher Reporting.sln` | `0` — لا تُبنى ولا تُنشر مع الـAPI |
| **لا نسخ لمنطق SMTP/HTML** | `grep SmtpClient\|MailKit` في مصدر الأداة | `0` — تُعيد استخدام `EmailNotificationService` كما هو |
| **لا سرّ مُصلَّب** | مسح `password\|jwt\|connectionstring\|@gmail\|secret\|apikey` | صفر سرّ؛ المطابقات كلّها **اسم متغيّر بيئيّ** (`ConnectionStrings__Default`)، أو `CancellationToken`، أو رمز التأكيد العلنيّ، أو نصّ عربيّ للمستخدم |
| **لا إرسال إنتاجيّ** | راجع §8 | صفر رسالة، صفر صفّ، صفر تغيير |

**بصمة الأداة (Artifact SHA256):**
`0741d5d7f30552d9bfb87ea2cddaa5858beaa80ee99a915f98eaf79766fd0166`
(`bin/Release/net8.0/Reporting.EmailRecoveryPublisher.dll` — **مطابقة بايتًا ببايت** بين البناء المحلّي والنسخة المرفوعة إلى خادم RC)

---

## 4. تصميم الأداة

### 4.1 الخصائص الحاكمة
- Console خارج `Reporting.sln`، تُشغَّل **يدويًّا لمرّة واحدة**؛ لا تعمل تلقائيًّا، لا تستمع على منفذ، لا Service/Cron/HostedService.
- **Plan هو الوضع الافتراضيّ** (قراءة فقط، صفر `SaveChanges`، صفر SMTP).
- تعيد استخدام `EmailNotificationService` والقوالب والمنطق القائم — **بلا نسخ لمنطق SMTP ولا لقوالب HTML**.

### 4.2 الرايات
```
--plan | --execute | --verify-only
--manifest <path>  --expected-count <N>  --batch-id <id>  --confirm EMAIL-RECOVERY-PUBLISHER-R1
[--json]
```
رموز الخروج: `0` نجاح | `2` خطأ استخدام/بيئة | `3` بيان غير صالح أو بوّابة ناقصة | `4` توقّف/فشل تنفيذ.
**أيّ بوّابة ناقصة عند Execute ⇒ لا كتابة ولا إرسال إطلاقًا.**

### 4.3 البيان (Manifest)
القائمة الفعليّة **لا تُكتب داخل الكود أبدًا**. البيان الخارجيّ يحمل حصرًا:
`schemaVersion`, `recoveryBatchId`, `maxItems`, `originalNotificationId`, `originalCorrelationKey`, `recipientUserId`, `category`, `periodKey`.

الشروط المفروضة برمجيًّا: **حدّ صلب 12 عنصرًا**، لا تكرار، والفئتان المسموحتان حصرًا `report-overdue` و`report-review-overdue-teamleader`. ملفّ الإنتاج يعيش **خارج Git** بملكيّة root وصلاحيّة `600` (طُبِّق حرفيًّا على RC: `-rw------- root root`).

### 4.4 مفتاح الاسترداد
```
recovery:{original-correlation-key}:{batchId}
```
حتميّ، غير عشوائيّ، ضمن حدّ الطول (300)، محميّ بالقيد الفريد القائم ⇒ تشغيل ثانٍ = `AlreadyApplied` بلا صفّ جديد وبلا إرسال جديد.

### 4.5 إعادة التحقّق لحظة التشغيل (من القاعدة، لكلّ عنصر)
ترتيب الفحوص في `AssessCoreAsync` (يتوقّف عند أوّل فشل):

| # | الفحص | القرار عند الفشل |
|---|---|---|
| 1 | الصفّ الأصليّ موجود | `Invalid / original_row_missing` |
| 2 | المفتاح مطابق (بعد تجريد `dryrun:`) | `Invalid / original_key_mismatch` |
| 3 | `EventType` = الفئة | `Invalid / original_category_mismatch` |
| 4 | المستلِم مطابق | `Invalid / original_recipient_mismatch` |
| 5 | `Mode = DryRun` | `Invalid / original_not_dryrun` |
| 6 | الفترة داخل المفتاح | `Invalid / original_period_mismatch` |
| 7 | العنوان/المتن موجودان | `ManualReview / original_content_missing` |
| 8 | مفتاح الاسترداد (أو بنسخته `dryrun:`) غير موجود | `AlreadyApplied / recovery_already_applied` |
| 9 | لا دفعة استرداد أخرى لنفس المفتاح | `ManualReview / recovery_other_batch_exists` |
| 10 | لا صفّ `Enabled` بالمفتاح القانونيّ | `Completed / already_delivered_enabled` |
| 11 | المستلِم موجود ونشط وله بريد | `Invalid / recipient_not_found\|_inactive\|_email_missing` |
| 12a | *(overdue)* التزام قابل للاشتقاق + الفترة قابلة للتحليل | `ManualReview / obligation_unresolved` أو `Invalid / period_key_unparsable` |
| 12b | *(overdue)* لا تسليم يغطّي الفترة | `Completed / submission_exists` |
| 12c | *(overdue)* لا إجازة معتمدة تغطّي الفترة | `Completed / leave_covers_period` |
| 13a | *(review-TL)* مفتاح أسبوع صالح | `Invalid / period_key_unparsable` |
| 13b | *(review-TL)* لا تزال مراجعات معلّقة والمستلِم `CurrentApprover` | `Completed / no_pending_reviews` |
| — | كلّ ما سبق سليم | `Eligible / still_due` |

**أيّ شكّ ⇒ `ManualReview` وبلا إرسال.**

### 4.6 سلوك التنفيذ
- **تسلسليّ** (لا Parallel)، حالة واحدة في المرّة، **إعادة تحقّق قبل كلّ حالة**.
- بعد كلّ إنشاء: تحقّق من الصفّ/الفئة/`EventType`/المستلِم/الفترة/القالب/الحالة (`VerifyPersistedAsync`).
- **توقّف عند أوّل فشل، بلا إعادة محاولة تلقائيّة**؛ ما نجح قبل التوقّف يبقى مُثبَتًا.
- **Production Execute لا يعمل إلّا عند `Mode=Enabled`**؛ RC Execute مسموح عند `Mode=DryRun` ويُنتج صفوف Recovery بوضع DryRun بلا SMTP.

### 4.7 الأمان في المخرجات
لا يُطبع إطلاقًا: كلمة مرور، JWT، ConnectionString، بيانات اعتماد SMTP، متن كامل، بريد كامل، أو إعداد خام. المسموح: بريد مُقنَّع، `UserId`/`CorrelationKey` مُقنَّعان (`e1000000…`)، القرار والسبب، الحصيلة والمدّة. ينطبق على الكونسول وعلى مخرَج `--json` وعلى السجلّات.

---

## 5. الاختبارات المحليّة

### 5.1 اختبارات الأداة المستهدَفة — **26/26 خضراء**
`EmailRecoveryPublisherTests.cs` (Passed 26 / Failed 0 / Duration 2 د 37 ث)

نواة الأداة تُختبَر عبر سابقة `ModerationV6Publisher`: `<Compile Include="..\..\tools\Reporting.EmailRecoveryPublisher\RecoveryPublisher.cs" Link="ToolSources\…" />` — **بلا `Program.cs`** كي لا يتعارض `Program` مع `Reporting.Api`. الأداة نفسها تبقى خارج الحلّ ولا تُنشر مع الـAPI.

| المجموعة | التغطية |
|---|---|
| **M01–M07** | بيان صالح / غير صالح / مكرَّر / أكثر من 12 / فئة غير مسموحة / `schemaVersion` خاطئ / `batch-id` غير مطابق |
| **P01–P08** | Plan = صفر كتابة وصفر SMTP؛ `Eligible` / `Completed` (`submission_exists`, `leave_covers_period`) / `AlreadyApplied` / `ManualReview` / `Invalid` |
| **X01–X07** | Execute يتطلّب كلّ البوّابات؛ عدم تطابق `expected-count` يوقف **قبل** الكتابة؛ DryRun يُنشئ صفّ Recovery بلا SMTP؛ `Mode=Disabled` يرفض؛ **التوقّف عند أوّل فشل**؛ تشغيل ثانٍ = `AlreadyApplied` بلا صفّ جديد؛ حتميّة مفتاح الاسترداد |
| **S01–S02** | لا سرّ في المخرجات؛ التقنيع فعّال (بريد/معرّف/مفتاح) |
| **B01–B02** | لا Endpoint ولا Frontend ولا هجرة؛ الأداة خارج الحلّ؛ لا `SmtpClient` ولا `MailKit` في مصدرها |

**ملاحظة على X05 (التوقّف عند أوّل فشل):** يُثبَت بوضع `Enabled` مع مُرسِل وهميّ يُرجِع `EmailSendResult.Fail(...)` — فيُثبَّت الصفّ بحالة `Failed` بينما `EnqueueAsync` يُرجِع `Created`، فيلتقطه `VerifyPersistedAsync` بـ`verify_status_failed` ويُجهض التشغيل بـ`stopped_on_first_failure`. **صفر اتصال SMTP حقيقيّ** (المُرسِل وهميّ بالكامل).

### 5.2 حزم عدم التراجع

| الحزمة | النتيجة |
|---|---|
| البريد + عزل DryRun + قناة البريد + مركز التحكّم | **132/132 خضراء** (47 ث) |
| المجدول `ReportReminder*` | **35/35 خضراء** (1 د 7 ث) |
| اختبارات الوحدة `Reporting.UnitTests` | **313/313 خضراء** |
| بناء الأداة `dotnet restore` + `build -c Release` | **نجح — 0 تحذير، 0 خطأ** |
| بناء مشروع الاختبارات | **نجح — 4 تحذيرات، 0 خطأ** |

### 5.3 حزمة التكامل الواسعة — **صفر تراجع مُثبَت بالمقارنة**

المرشِّح `Submission|ReportCalendar|RoleAware` على **المرشّح** مقابل **شجرة الأساس النظيفة** `f3ee32f2`:

| | المرشّح | الأساس |
|---|---|---|
| ناجح | 179 | 178 |
| فاشل | **13** | **13** |
| الإجمالي | 192 | 191 |

**مجموعتا الأسماء الفاشلة متطابقتان حرفيًّا** (13 اسمًا مقابل 13 اسمًا):
`NotificationLinkTests.SubmissionNotification_Link_IsAppSubmissionsOpen`، `ProjectRepeatableGridTests.OldSubmission_StillRenders_AfterNewVersionWithGrid`، `ScopeEnforcementTests.TeamLeader_Cannot_Read_OutOfScope_Submission`، `ScopeEnforcementTests.TeamLeader_Submissions_List_Sees_Only_Own_Team`، `SubmissionReminderTests.AlreadySubmitted_DoesNotRemind`، `SubmissionTests.DeleteDraft_AdminCannotDeleteOthersDraft_403`، `SubmissionTests.DeleteDraft_ClosedReport_CannotDelete_409`، `SubmissionTests.DeleteDraft_OtherEmployee_CannotDelete_403`، `SubmissionTests.DeleteDraft_Owner_DeletesOwnDraft_204_AndDisappearsFromList`، `SubmissionTests.DeleteDraft_SubmittedReport_CannotDelete_409`، `SubmissionTests.NonApprover_CannotApprove_403`، `UnifiedReportStatusTests.CurrentCycle_NoSubmission_DueNow`، `UnifiedStatusApiTests.MyCycles_CurrentCycleNoSubmission_UnifiedIsDueNow`.

⇒ **الفشل الثلاثة عشر سابق للمرشّح وبيئيّ** (قاعدة `reporting_test` الدائمة المشتركة)، **والمرشّح لم يُدخِل ولم يُصلِح أيّ فشل**. الفارق `+1` في الإجمالي هو اختبار الأداة `P04_Completed_WhenSubmissionExists` الذي يطابق المرشِّح لاحتوائه كلمة `Submission` — **وقد نجح** (179 = 178 + 1). وهذا متّسق تمامًا مع كون `reporting-backend/src/` **غير مُمَسّ** (صفر ملفّ في الـdiff).

---

## 6. خطّ أساس RC قبل القبول

| البند | القيمة |
|---|---|
| `/health` | **200** |
| `ASPNETCORE_ENVIRONMENT` | **ReleaseCandidate** |
| قاعدة البيانات | **reporting_rc** |
| `EmailNotifications__Mode` | **DryRun** |
| `ReportReminderScheduler__Enabled` | **false** |
| `email_outbox` | **0** |
| عدد الهجرات / الرأس | **30** / `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| `email_notifications` | 104 |
| صفوف `recovery:%` | **0** |
| الخدمة | MainPID **246643**، NRestarts **0**، active |

**الإنتاج (قراءة فقط) قبل البدء:** MainPID `258585`، NRestarts `0`، بدء `Wed 2026-07-29 18:54:53 UTC`، health `200`، هجرات `30` ورأس `20260724224053`، `email_notifications` = 213، `recovery:%` = 0، outbox = 0.

---

## 7. تنفيذ القبول على RC

### 7.1 التجهيزة الخياليّة (Fictional Fixture)

بيانات وهميّة بالكامل معزولة على أسبوع `2071-W20` لتفادي أيّ تصادم مع بيانات RC الحقيقيّة:

| العنصر | المحتوى |
|---|---|
| 5 مستخدمين | `e1000000-…-0001..0005`، `IsActive=true`، بريد `rc-erp-u{n}@rc.invalid`، مسمّى SEO (له قالب أسبوعيّ Primary نشط ومنشور) |
| تسليم مُعلَّق | `e2000000-…-0001` — مُقدِّمه U5، `Submitted`، `CurrentApproverId = U2` ⇒ يمنح U2 مراجعة معلّقة |
| تسليم مُغلَق | `e2000000-…-0002` — مُقدِّمه U3، `Closed` ⇒ يجعل U3 `Completed` |
| 4 صفوف DryRun أصليّة | `e3000000-…-0001..0004` بمفاتيح قانونيّة (بلا بادئة) مطابقة لبنية الإنتاج |

نتيجة الإدراج: `BEGIN / INSERT 0 5 / INSERT 0 1 / INSERT 0 1 / INSERT 0 4 / COMMIT` ⇒ `email_notifications` = 108.
البيان: `/root/erp-rc/manifest-rc.json` — **خارج Git**، `root:root`، **600**، 4 عناصر.

التغطية المطلوبة: **Eligible overdue** (U1) + **Eligible review-overdue** (U2) + **Completed** (U3) + **AlreadyApplied** (التشغيل الثاني) + **Partial failure** (§7.6).

### 7.2 المرحلة 1 — Plan (قراءة فقط) — `EXIT=0`

```
وضع التشغيل : تخطيط (Plan — قراءة فقط)  |  وضع البريد : DryRun  |  الدفعة : r1-20260729
[0] report-overdue                        | 2071-W20 | مستحقّ / still_due
[1] report-review-overdue-teamleader      | 2071-W20 | مستحقّ / still_due
[2] report-overdue                        | 2071-W20 | لم يعد مستحقًّا / submission_exists
[3] report-overdue                        | 2071-W20 | مستحقّ / still_due
الإجمالي 4 | Eligible 3 | Completed 1 | AlreadyApplied 0 | Invalid 0 | ManualReview 0 | Created 0 | Failed 0
ℹ Plan — قراءة فقط: لم يُكتب صفّ واحد ولم يُفتح أيّ اتصال SMTP.
```

- **القرارات تطابق القاعدة حرفيًّا.**
- **صفر كتابة**: القياس بعد Plan = `108 | 0 | 0` (إشعارات | صفوف recovery | outbox).
- **صفر SMTP.**
- مخرَج `--json` مُقنَّع بالكامل: `"RecipientRef": "e1000000…"`، المفاتيح مُقنَّعة، لا بريد، لا متن، `"Ok": true`، `"AbortReason": null`.

### 7.3 المرحلة 2أ — بوّابة `expected-count` — `EXIT=4`

قبل التنفيذ أُدخِل **تغيّر في العالم** عمدًا: تسليم جديد `e2000000-…-0003` لـU4 على نفس الفترة ⇒ لم يعد مستحقًّا ⇒ العدد الفعليّ 2 لا 3.

```
… القرار / السبب : مراجعة بشريّة / expected_count_mismatch   (للأربعة جميعًا)
… الحصيلة / المدّة : aborted / 0ms
سبب التوقّف : ✗ expected_count_mismatch
```

القياس بعد الإجهاض: **`108 | 0 | 0`** ⇒ **توقّف قبل الكتابة بصفر أثر.**

### 7.4 المرحلة 2ب — Execute تحت DryRun — `EXIT=0`

```
[0] report-overdue                   | مستحقّ / still_due            | created / 227ms
[1] report-review-overdue-teamleader | مستحقّ / still_due            | created / 18ms
[2] report-overdue                   | لم يعد مستحقًّا / submission_exists | skipped / 5ms
[3] report-overdue                   | لم يعد مستحقًّا / submission_exists | skipped / 4ms
الإجمالي 4 | Eligible 2 | Completed 2 | AlreadyApplied 0 | Invalid 0 | ManualReview 0 | Created 2 | Failed 0
```

**صفّا الاسترداد المُنشآن (صحيحان تمامًا):**

```
dryrun:recovery:report-overdue:2071-W20:e1000000-…-0001:EmployeeReportNotSubmitted:r1-20260729
        | report-overdue                   | e1000000-…-0001 | DryRun | DryRun | ReportReminder
dryrun:recovery:report-review-overdue-teamleader:2071-W20:e1000000-…-0002:r1-20260729
        | report-review-overdue-teamleader | e1000000-…-0002 | DryRun | DryRun | ReportReminder
```

- مفاتيح الاسترداد **حتميّة ومطابقة للصيغة**؛ بادئة `dryrun:` مضافة من الخدمة نفسها (سلوك R1C الصحيح تحت DryRun).
- **الصفوف الأصليّة الأربعة بلا تغيير**: `Status=DryRun`, `Mode=DryRun`، وبصمة `md5(Subject||BodyText)` ثابتة.
- **صفر SMTP**: `email_outbox = 0`، وصفوف `Status='Sent'` = **0**.
- الإجماليّ 108 → **110** (زيادة صفّين حصرًا).

### 7.5 المرحلة 3 — تشغيل ثانٍ (عدم التكرار) — `EXIT=0`

```
[0] … : مُطبَّق سلفًا / recovery_already_applied | skipped / 1ms
[1] … : مُطبَّق سلفًا / recovery_already_applied | skipped / 1ms
[2] … : لم يعد مستحقًّا / submission_exists      | skipped / 4ms
[3] … : لم يعد مستحقًّا / submission_exists      | skipped / 4ms
الإجمالي 4 | Eligible 0 | Completed 2 | AlreadyApplied 2 | Created 0 | Failed 0
```

القياس بعد التشغيل الثاني: **`110 | 2 | 0 | 0`** (إشعارات | recovery | outbox | Sent) ⇒ **صفر صفّ جديد، صفر إرسال.**

### 7.6 ملاحظة صريحة — «الفشل الجزئيّ» على RC

**لا يمكن استحداث فشل إرسال حقيقيّ تحت `Mode=DryRun`**: المُرسِل لا يُستدعى أصلًا، و`VerifyPersistedAsync` يرى دائمًا `Status=DryRun` صحيحة. لذلك:

- مسار **التوقّف عند أوّل فشل** يُثبَت محليًّا باختبار **X05** (وضع `Enabled` + مُرسِل وهميّ فاشل، صفر SMTP حقيقيّ) — راجع §5.1.
- على RC يُثبَت **النظير الآمن** بأمرين فعليّين: (أ) إجهاض `expected_count_mismatch` بصفر كتابة (§7.3)، و(ب) **إعادة التحقّق لكلّ حالة أثناء الطيران** التي أسقطت العنصر [3] من `Eligible` إلى `Completed` بعد تغيّر العالم بين Plan والتنفيذ (§7.2 مقابل §7.4) — وهو جوهر الحماية المطلوبة.

هذا تحفّظ **مُعلَن ومقصود**، لا ثغرة تغطية.

### 7.7 المرحلة 4 — التنظيف والتحقّق النهائيّ

```
BEGIN / DELETE 2 (صفوف الاسترداد) / DELETE 4 (الصفوف الأصليّة)
      / DELETE 3 (التسليمات) / DELETE 5 (المستخدمين) / COMMIT
rm -rf /root/erp-rc
```

| التحقّق | النتيجة |
|---|---|
| `email_notifications` | **104** (عودة تامّة لخطّ الأساس) |
| صفوف `recovery:%` | **0** |
| `email_outbox` | **0** |
| مستخدمو/تسليمات التجهيزة | **0** / **0** |
| عدد الهجرات / الرأس | **30** / `20260724224053` (بلا انزياح) |
| عمليّات الأداة الباقية | **0** (`ps` = 0) |
| مستمعو منافذ للأداة | **0** |
| مجلّد `/root/erp-rc` | **محذوف** (`No such file or directory`) |
| الخدمة | MainPID **246643**، NRestarts **0**، active (**بلا إعادة تشغيل**) |
| `/health` | **200** |

---

## 8. الإنتاج — إثبات عدم المساس (قراءة فقط، بعد كلّ العمل)

| البند | قبل | بعد | الحكم |
|---|---|---|---|
| MainPID | 258585 | **258585** | بلا تغيير |
| NRestarts | 0 | **0** | بلا إعادة تشغيل |
| بدء الخدمة | `Wed 2026-07-29 18:54:53 UTC` | **مطابق** | بلا مساس |
| `/health` | 200 | **200** | سليم |
| عدد الهجرات / الرأس | 30 / `20260724224053` | **30 / مطابق** | بلا هجرة |
| `email_notifications` | 213 | **213** | صفر صفّ جديد |
| صفوف `recovery:%` | 0 | **0** | **صفر إرسال استرداد** |
| `email_outbox` | 0 | **0** | صفر صادر |

**لم يُنشر شيء على الإنتاج، ولم يُنشأ بيان إنتاجيّ، ولم يُشغَّل Plan إنتاجيّ، ولم تُرسَل أيّ رسالة استرداد، ولم يُشغَّل المجدول، ولم يُبدأ أيّ محور آخر.**

---

## 9. الخلاصة

| المعيار | الحكم |
|---|---|
| أداة آمنة تُشغَّل لمرّة واحدة، Plan افتراضيّ، تنفيذ مُبوَّب | ✅ |
| بيان خارجيّ ≤12، بلا قائمة مُصلَّبة، 600 خارج Git | ✅ |
| إعادة تحقّق كاملة لحظة التشغيل، الشكّ ⇒ ManualReview | ✅ |
| مفتاح استرداد حتميّ محميّ بالقيد الفريد | ✅ |
| تنفيذ تسلسليّ + تحقّق بعديّ + توقّف عند أوّل فشل | ✅ |
| الاختبارات المستهدَفة 26/26 + عدم التراجع (132/35/313) | ✅ |
| صفر تراجع مُثبَت بالمقارنة مع شجرة الأساس | ✅ |
| قبول RC: Plan / بوّابة العدد / Execute / تشغيل ثانٍ / تنظيف | ✅ |
| لا هجرة، لا Endpoint، لا Frontend، لا سرّ، لا إرسال إنتاجيّ | ✅ |
| الإنتاج غير مُمَسّ بالمرّة | ✅ |

---

## 10. ما هو **ممنوع** بعد هذا التقرير (بلا تصريح مستقلّ جديد)

- نشر الأداة على الإنتاج.
- إنشاء بيان إنتاجيّ (Production Manifest).
- تشغيل Plan إنتاجيّ.
- إرسال أيّ رسالة استرداد.
- تشغيل المجدول.
- بدء أيّ تذكرة أخرى.

> **تذكير بالموعد الحرج (من التشخيص السابق):** الحالات المقيَّدة بأسبوع W30 تخرج نهائيًّا من النافذة الطبيعيّة **السبت 2026-08-01**.

---

**EMAIL-RECOVERY-PUBLISHER-R1 RC PASS — PRODUCTION TOOL READY**
