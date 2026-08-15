# EMAIL-NOTIFICATIONS-ROLE-AWARE-SCHEDULE-FIX-R1 — تقرير قبول RC (المرشّح R2 فوق 459f60e)

- **التاريخ:** 26 يوليو 2026
- **الحالة:** RC منشور ومُثبَت — **Production لم يُمَسّ، ولا بريد أُرسِل إطلاقًا**
- **المحظور القائم:** ممنوع لمس Production أو تفعيل البريد قبل تصريح مستقل بعد قبول هذا التقرير

---

## 1. مانيفيست المرشّح الجديد

| البند | القيمة |
|---|---|
| Commit | `df9891ceff575298470b5b8301ec4509122be99d` |
| Parent | `459f60e278105b9a08563a17a6e59d15639ead54` |
| Tree | `6bdcc3a127389e1df216283a16624127ccfa2341` |
| الفرع | `candidate/reminder-role-aware-r2-20260726` |
| الـWorktree | `/tmp/cand-reminder-r2-20260726` |
| Migration | **لا شيء** — head يبقى `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| Git tag | **لم يُنشأ** (ممنوع) |

### الملفات المتغيّرة (7 ملفات، +995 / −21)

| الملف | النوع |
|---|---|
| `src/Reporting.Application/Notifications/ReportReminderSchedulerOptions.cs` | جديد (45) |
| `src/Reporting.Infrastructure/Services/ReportReminderSchedulerService.cs` | جديد (174) |
| `src/Reporting.Infrastructure/Services/ReportReminderService.cs` | معدَّل (33) |
| `src/Reporting.Infrastructure/DependencyInjection.cs` | معدَّل (2 سطر: `Configure<ReportReminderSchedulerOptions>` + `AddHostedService`) |
| `tests/Reporting.IntegrationTests/RoleAwareReminderScheduleTests.cs` | جديد (503) |
| `tests/Reporting.IntegrationTests/ReportReminderSchedulerTests.cs` | جديد (210) |
| `tests/Reporting.IntegrationTests/ReportRemindersTests.cs` | معدَّل (49) |

**أداة `tools/ReminderScheduleSimulator`** بقيت خارج الـcommit وخارج `Reporting.sln` (أداة تشخيص محليّة، لا تدخل أيّ نشر).

---

## 2. الدلتا المنطقيّة المطبَّقة (لم تُنسخ ملفّات كاملة من مرشّح 21d397d)

الأساس هو ملفّات `459f60e` كما هي، وطُبِّقت فوقها الدلتا التالية فقط:

1. **حقن `ISystemClock`** في `ReportReminderService` واستبدال النداءات الساكنة الثلاثة لـ`ReportCalendarPolicy.RiyadhToday()` بدالّة `RiyadhToday()` المحقونة (`EmitDueRemindersAsync`، `EvaluateAsync`، `PendingReviews`، و`NormalizeWeekKey` صارت instance).
2. **حارس أهليّة اليوم حسب الدور**: استُبدل `if (e.Overdue) continue;` (حدّ أعلى فقط) بـ `if (e.DueDate != today) continue;` (يُغلق الطرفين). النتيجة: لا `report-weekly-due` مبكرًا منذ اليوم صفر من الدورة.
3. **`CorrelationKey` الأسبوعيّ صار ثلاثيّ المقطع**: `report-weekly-due:{cycleKey}:{dueKey}:{userId}` — رسالة في يوم خاطئ لا تستطيع حجز مفتاح اليوم الصحيح.
4. **`ReportReminderSchedulerService`** جديد: بوابة `Enabled` معطّلة افتراضيًّا، فتحات بتوقيت الرياض، قفل فتحة في الذاكرة + منع تكرار حقيقيّ في القاعدة عبر `CorrelationKey`، ويشغّل **الدورة السابقة ثمّ الحالية** (لأن إزاحات المدير=+8 والمدير العام/الرئيس/الأدمن=+9 تقع خارج نافذة الدورة نفسها).

**ما لم يُمَسّ (تحقّق من الـdiff):** `DailyExpectedDates` ما زالت تمرّر `saturdayEnabled: true`، و`SalesSaturdayApplicabilityFloor = 2026-07-25` كما هي، ولا تغيير في `ReportingCalendarPolicy` / `ReportCadencePolicy` / `ScopeResolver` / `CurrentApproverId` / أيّ منطق اعتماد.

---

## 3. البناء والاختبارات

| البند | النتيجة |
|---|---|
| `dotnet build Reporting.sln -c Debug` | **نجح — 0 أخطاء**، 4 تحذيرات `CS8604` موروثة (موجودة على 459f60e نفسه) |
| `RoleAwareReminderScheduleTests` | **15/15 نجحت** |
| `ReportReminderSchedulerTests` + `ReportRemindersTests` | **35/35 نجحت** |
| الأصناف المجاورة (12 صنفًا: التقويم/اليوميّ/الأرضيّة الأسبوعية/البريد/الالتزام) | 214 نجحت، 3 أخفقت |

### مقارنة control مقابل candidate

الـcontrol = worktree على خط الأساس `459f60e` بالضبط (`/tmp/release-sat-applicability-r1-20260726-012930`).

| الاختبار | Candidate | Control (459f60e) | الحكم |
|---|---|---|---|
| `ReportCadenceTests.SalesUser_DailyAccepted_WeeklyRejected` | FAIL (`Expected OK / Actual BadRequest`) | FAIL (نفس الرسالة) | **موروث** |
| `ReportCadenceTests.NonSalesUser_WeeklyAccepted_DailyRejected` | FAIL (`Expected OK / Actual BadRequest`) | FAIL (نفس الرسالة) | **موروث** |
| `SubmissionReminderTests.AlreadySubmitted_DoesNotRemind` | FAIL (`Expected 0 / Actual 1`) | FAIL (نفس الرسالة) | **موروث** |

⇒ **لا إخفاق واحد ناتج عن الدلتا.** الثلاثة ناتجة عن تلوّث قاعدة الاختبار الدائمة المشتركة `reporting_test` (سلوك سابق للتاسك).

### اختبارات العقد الإلزاميّة (كلّها ضمن `RoleAwareReminderScheduleTests`)

| المطلب | الاختبار | النتيجة |
|---|---|---|
| السبت: daily مبيعات فقط، weekly=0 لكلّ الأدوار | `Saturday_DailySalesOnly_AndNoWeeklyDueForAnyRole` | ✅ |
| الجمعة: daily=0 وweekly=0 | `Friday_NoDailyDue_AndNoWeeklyDue` | ✅ |
| الأربعاء: daily + weekly للموظّفين | `Wednesday_...` | ✅ |
| الخميس: daily + weekly لقادة الفرق | `Thursday_...` | ✅ |
| الأحد: daily + weekly للمديرين | `Sunday_...` | ✅ |
| الاثنين: daily + weekly للتنفيذيين | `Monday_...` | ✅ |
| الثلاثاء: daily فقط | `Tuesday_DailySalesOnly` | ✅ |
| إعادة التشغيل لا تكرّر | `SameDayRerun_CreatesNoNewRows` | ✅ |
| رسالة السبت اليومية لا تمنع رسالة أسبوعية لاحقة | `SaturdayDailyMessage_DoesNotBlockLaterWeeklyMessages` | ✅ |
| فصل فضاءَي المفاتيح اليوميّ والأسبوعيّ | `DailyAndWeeklyCorrelationKeys_AreSeparateNamespaces` | ✅ |
| تشغيل مبكر لا يحجز مفتاح اليوم الصحيح | `EarlyRun_DoesNotSquatCorrectDayKey` | ✅ |
| المجدول معطّل لا يولّد شيئًا | `ReportReminderSchedulerTests.Tick_WhenDisabled_DoesNotRun` | ✅ |
| DryRun لا يستدعي SMTP | `DryRun_DoesNotCallSmtpSender` | ✅ |

---

## 4. نشر RC وإثباته

### النشر
- المصدر: الـworktree المعزول حصرًا؛ `dotnet publish -c Release` ثمّ `rsync -az --delete --exclude appsettings.Development.json` → `/opt/reporting-rc/publish`.
- نسخ احتياطية (TS=`20260726-073823`): `/opt/reporting-rc/publish-backup-reminderr2-20260726-073823` + `/root/khubara-reporting-rc.env.bak-20260726-073823`.
- بعد النشر: `health=200`، `Hosting environment=ReleaseCandidate`، **0 سطر `Applying migration`**، وmigration head على `reporting_rc` ما زال `20260724224053`.
- البوابات عند النشر: `EmailNotifications__Mode=DryRun`، `ReportReminderScheduler__Enabled=false`، `Email__Enabled=false` — والصفوف بقيت 84 (خطّ الأساس) ⇒ **المجدول المعطّل لم يولّد شيئًا على بيئة حيّة**.

### الإثبات الحيّ (تفعيل مؤقّت، DryRun)
اليوم الفعليّ على الخادم = **الأحد 2026-07-26** (توقيت الرياض).

| الإثبات | النتيجة |
|---|---|
| `report-weekly-due` المولَّدة | **4 صفوف، كلّها لدور `Manager` فقط**، بمفتاح `report-weekly-due:2026-W30:2026-07-26:{userId}` |
| `report-daily-due` المولَّدة | **5 صفوف، كلّها `SALES_B2C`/`SALES_B2B`**، بمفتاح `report-daily-due:2026-07-26:{userId}` |
| لا weekly مبكر | لا صفّ أسبوعيّ لأيّ موظّف/قائد فريق/تنفيذيّ في يوم غير يومه |
| حالة كلّ الصفوف | `DryRun` (20/20) |
| إعادة التشغيل | `created=0`، `duplicate=63+16=79`، وإجمالي الصفوف ثابت عند 20 |
| SMTP | **0** (`Email sent to` = 0، `Email send failed` = 0، `email_outbox` = 0) |

### مصفوفة الأيّام (محاكاة ساعة مثبَّتة على نفس `GenerateAsync` وبنفس تسلسل المجدول)

| اليوم المُحاكى | `report-daily-due` | `report-weekly-due` | مستلمو الأسبوعيّ |
|---|---|---|---|
| السبت 2026-08-01 | **5** (مبيعات فقط) | **0** | — |
| الأحد 2026-08-02 | 5 | 4 | `Manager` |
| الاثنين 2026-08-03 | 5 | 2 | `Admin` + `GeneralManager` |
| الأربعاء 2026-08-05 | 5 | 14 | `Employee` |
| الخميس 2026-08-06 | 5 | 5 | `TeamLeader` |
| الجمعة 2026-08-07 | **0** | **0** | — |

⇒ العقد الموحّد مُثبَت على بيئة حيّة، بلا أثر رجعيّ على W28/W29/W30 (المحاكاة على W31/W32 فقط، ولم يُولَّد أيّ `report-weekly-due` بمفتاح سابق للأرضيّة).

---

## 5. الإرجاع والتنظيف

- البوابات أُعيدت إلى خطّ الأساس: `ReportReminderScheduler__Enabled=false`، `RunAtRiyadhHours=21,22`، `EmailNotifications__Mode=DryRun`، `Email__Enabled=false`.
- **حُذفت 193 صفّ محاكاة** (`DELETE 193`) ⇒ `email_notifications` على RC عادت إلى **84 صفًّا** وآخر صفّ `2026-07-25 20:20:14` = خطّ الأساس تمامًا.
- أداة المحاكاة أُزيلت من الخادم (`/root/reminder-sim` غير موجود).
- RC بعد الإرجاع: `active`، `health=200`، migration head `20260724224053`.

### Production — لم يُمَسّ
| البند | القيمة |
|---|---|
| `reporting-api` | `active`، `health=200` |
| `EmailNotifications__Mode` | `DryRun` |
| `ReportReminderScheduler__Enabled` | `false` |
| `Email__Enabled` | `false` |
| migration head | `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| `email_notifications` | 110 صفًّا (بلا تغيير) |

---

## 6. Rollback

- **RC:** استعادة `/opt/reporting-rc/publish-backup-reminderr2-20260726-073823` + `/root/khubara-reporting-rc.env.bak-20260726-073823` ثمّ `systemctl restart khubara-reporting-rc`. لا هجرة لعكسها.
- **Production:** لا شيء — لم يُنشر عليه أيّ شيء.

---

## 7. الطلب

المرشّح `df9891c` جاهز للمراجعة. **لن يُنشر على Production ولن يُفعَّل أيّ بريد إلّا بتصريح مستقل صريح بعد قبول هذا التقرير.**
