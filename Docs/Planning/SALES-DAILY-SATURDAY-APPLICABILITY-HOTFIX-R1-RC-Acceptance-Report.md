# SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1 — تقرير قبول RC

> **الحالة:** مقبول على RC فقط. **الإنتاج لم يُمَسّ ومحظور حتى تفويض مستقل صريح.** البريد ظلّ متوقفًا (DryRun) طوال العمل.

## 1. الهدف
فتح تقرير السبت اليومي لموظّفي المبيعات (`SALES_B2B` / `SALES_B2C`) ابتداءً من W31، بحيث:
- يمكن للموظّف إنشاء ورفع تقرير يوم السبت `2026-07-25`.
- يُحتسَب السبت يومًا **متوقَّعًا/مُلتزَمًا** (Expected) لهذين المسمّيين فقط، فيصبح الالتزام من السبت إلى الخميس (الجمعة وحدها محجوبة).
- `ExpectedDailyDaysPerWeek = 6` للمبيعات من تاريخ السريان؛ الأسابيع الأقدم تبقى 5 (لا رجعية).
- سبوت التاريخ (قبل `2026-07-25`) لا تُعاد تصنيفها Missing/Overdue وتبقى ظاهرة كـ Actual.

## 2. الأساس والعزل
- **قاعدة RC/الإنتاج الحقيقية:** `21d397d` (= والد الإصلاح؛ رأس RC قبل النشر).
- **Worktree معزول:** `/private/tmp/release-sat-applicability-r1-20260726-012930`.
- **لا Flexible Positions ولا أي محور سابق في سطح الباتش.**

## 3. سجلّ الإصلاح
| الحقل | القيمة |
|---|---|
| Commit | `459f60e278105b9a08563a17a6e59d15639ead54` |
| Parent | `21d397d91eb93814ee21b566faf4f210e328c03b` |
| Tree | `2861c47f1d314166c3ec8aae38c943e8046e3196` |
| العنوان | `fix(daily): SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1 — فتح السبت المتوقَّع/المُلتزَم لموظّفي المبيعات (SALES_B2B/B2C) من W31` |
| Migration | **لا شيء** (code-only، `has-pending-model-changes` = No changes) |

### الملفات المتغيّرة (8، +243/−34)
1. `Reporting.Application/Common/ReportingCalendarPolicy.cs` — مصدر الحقيقة الوحيد (SalesSaturdayApplicabilityFloor=2026-07-25، SaturdayEnabledForJobRole، تحميلات saturdayEnabled/jobRoleCode).
2. `Reporting.Infrastructure/Services/ReportCalendarService.cs` — GetSalesDailyComplianceAsync `saturdayEnabled: true`.
3. `Reporting.Infrastructure/Services/ReportDueService.cs`.
4. `Reporting.Infrastructure/Services/ReportReminderService.cs`.
5. `Reporting.Infrastructure/Services/ReportingCalendarCycleService.cs` — اشتقاق المسمّى + saturdayEnabled في GetMyDaysAsync.
6. `Reporting.Infrastructure/Services/ReportingService.cs`.
7. `Reporting.Infrastructure/Services/SubmissionService.cs` — سطر 947 `IsDailyExpectedBusinessDay(dueDate, saturdayEnabled: true)`؛ **بوابة الرفع سطر 124 `IsDailySubmissionBlockedDay(day)` بلا تغيير (الجمعة فقط)**.
8. `Reporting.UnitTests/ReportingCalendarPolicyTests.cs` — +8 اختبارات D7.

**لا ملفات KPI / Dashboard / Submissions غير ذات صلة / Email-Scheduler / محاور سابقة.**

## 4. البناء والاختبارات (داخل النسخة المعزولة)
- Build ناجح.
- Unit tests: **313/313 أخضر** (101 في ReportingCalendarPolicyTests، منها 8 جديدة تغطّي: الأرضية = السبت 2026-07-25؛ SaturdayEnabledForJobRole للمبيعات فقط؛ أيام العمل/العطلة الواعية بالسبت؛ W31 مبيعات=6؛ W30 مبيعات=5 بلا رجعية؛ W31 غير-مبيعات=5؛ الاشتقاق بالمسمّى).
- `dotnet ef migrations has-pending-model-changes` = No changes.

### تكافؤ اختبارات التكامل (Integration parity vs base)
شُغِّلت مجموعة اختبارات التكامل الخاصة بالتقويم/الدورية على النسخة (`459f60e`) وعلى الأساس (`21d397d`) ضد نفس قاعدة `reporting_test` المشتركة:
- النسخة `459f60e`: **Failed 3 / Passed 136 / Total 139**.
- الأساس `21d397d`: **Failed 3 / Passed 136 / Total 139** — **نفس الاختبارات الثلاثة بالضبط**.
- الثلاثة الفاشلة على الأساس والنسخة معًا: `ReportCadenceTests.SalesUser_DailyAccepted_WeeklyRejected`، `ReportCadenceTests.NonSalesUser_WeeklyAccepted_DailyRejected`، `SubmissionReminderTests.AlreadySubmitted_DoesNotRemind`.
- **الحكم**: هذه الإخفاقات **سابقة على الإصلاح** (تلوّث قاعدة الاختبار المشتركة: تراكم قوالب SALES + حارس إسناد القالب + تواريخ أسبوع قديمة مثبَّتة في SubmissionReminderTests بنموذج الخميس→الأربعاء). **الإصلاح لا يُدخِل أي إخفاق جديد** (3→3، نفس المجموعة)، ولا يمسّ مساراتها (الأسبوعي/قبول الدورية/تذكير SubmissionReminderService القديم خارج ملفاته الثمانية).

## 5. نشر RC
- backend-only عبر rsync. TS نشر = `20260726-020102`.
- health = 200، migration head = `20260724224053_AddReportApproverAndKpiReviewerOverrides` (بلا تغيير)، علامات السبت موجودة في الـDLL المنشور.
- Backups: env `.bak-sat-20260726-020102`، publish `publish-backup-sat-20260726-020102`، DB `/root/rc-backups/reporting_rc-presat-20260726-020102.dump`.

## 6. UAT — 11 إثباتًا (كلها ناجحة)
| # | الإثبات | النتيجة |
|---|---|---|
| 1 | SALES_B2B ينشئ Draft 2026-07-25 | 200 (id 93d1f220) |
| 2 | SALES_B2C ينشئ Draft 2026-07-25 | 200 (id 5ab4931f) |
| 3 | السبت يظهر Expected/مفتوح للمبيعات | my-days: isHoliday=false، isOpenForDraft=true |
| 4 | الجمعة لا تُحتسَب/محجوبة | POST 2026-07-31 = 400 `calendar.day_is_holiday`؛ lockReason يذكر الجمعة فقط |
| 5 | أسبوع W31 = 6 أيام للمبيعات | compliance weekStart 2026-07-25 (expectedDays=1 بسبب حدّ اليوم؛ النطاق سبت→خميس) |
| 6 | الأسابيع الأقدم تبقى 5 | W30 expectedDays=5، لا سبت مضاف |
| 7 | مستخدم غير-مبيعات غير متأثّر | السبت للـnon-sales = Holiday، lockReason الجمعة/السبت |
| 8 | Calendar/MyStatus/Compliance/Overdue متطابقة | my-days 2026-07-18 = Holiday/isOverdue=False/isExpected=False؛ missing-reports فيه 2026-07-25 وليس 2026-07-18 |
| 9 | لا رجعية للسبت التاريخي | سبت 2026-07-18 = Holiday، غير Missing/Overdue، draft ظاهر |
| 10 | لا بريد فعلي | email_outbox 0/0؛ Email__Enabled=false، Mode=DryRun |
| 11 | لا تشغيل مجدول | ReportReminderScheduler معطّل أثناء UAT؛ لا أسطر تذكير/بريد في السجلّ |

## 7. تنظيف بيانات UAT (Control #10) — مكتمل
- حُذفت 4 مسودّات UAT (93d1f220، 5ab4931f، 1decfe4d، 41f998e3) — `drafts_left=0` (لا submission_field_values ولا approval_steps).
- حُذف إسنادا القالب المؤقتان `Notes='SAT-UAT-TEMP-R1'` — `temp_assign_left=0`.
- أُعيد القالبان اليوميّان `29e03b01` و`7851e0f8` إلى حالتهما الأصلية `Status='Archived'` + `IsActive=false`.
- استُعيدت بيانات اعتماد 3 مستخدمي UAT عبر `UatCredentialTool --restore` ثم `--verify` (اللقطة `/root/rc-uat/uat-cred-snapshot-sat-20260726.json`) — PasswordHash/SecurityStamp/تنظيمي/أدوار **مطابقة=True** للثلاثة.
- أُعيد `ReportReminderScheduler__Enabled=true` (حالة RC الأصلية) وأُعيد تشغيل الخدمة — env مطابق للـbackup (NO_DIFF)، الخدمة active.
- تحقّق خط الأساس بعد التنظيف: health=200، email_outbox 0/0، migration head غير مُغيَّر، لا بقايا UAT، القالبان Archived/f.

## 8. عدم المساس
لم يُمَسّ: KPI (evaluations/ComputeScore/Templates)، Dashboard، ScopeResolver، Workflow/CurrentApproverId، قواعد الأسبوعي، بوابة رفع السبت/الجمعة (الجمعة تبقى محجوبة)، ManagerId/TeamId/DepartmentId، بيانات/قوالب التقارير الحقيقية، البريد (ظلّ DryRun). لا رجعية قبل 2026-07-25.

## 9. الحالة والخطوة التالية
- **RC: مقبول.** RC عاد إلى خط الأساس (لا يبقى إلا كود الإصلاح المنشور، وهو الغرض من نشر RC).
- **الإنتاج: محظور** — يتطلّب تفويضًا مستقلًّا صريحًا قبل أي نشر.

سلسلة القبول:
`اكتمل فتح التقارير اليومية يوم السبت لموظفي مبيعات B2B وB2C ابتداءً من W31 — يمكن للموظفين إنشاء ورفع تقرير 2026-07-25، وأصبح الالتزام من السبت إلى الخميس مع بقاء الجمعة محجوبة والدورات التاريخية دون تغيير — البريد متوقف مؤقتًا على DryRun`
