# EMAIL-NOTIFICATIONS-ROLE-AWARE-SCHEDULE-FIX-R1-R2 — تقرير النشر الإنتاجيّ والتفعيل الكامل الداخليّ

- **التاريخ:** 26 يوليو 2026
- **البيئة:** Production — `reports.emarketingacademy.net` / `reporting-api.service` / DB `reporting_prod`
- **الطابع الزمنيّ للنافذة:** `20260726-103049`
- **نوع النشر:** Backend فقط، **code-only بلا أيّ هجرة**، **بلا Git tag**

---

## 1) المرشّح المنشور

| البند | القيمة |
|---|---|
| commit | `df9891ceff575298470b5b8301ec4509122be99d` |
| parent | `459f60e278105b9a08563a17a6e59d15639ead54` |
| tree | `6bdcc3a127389e1df216283a16624127ccfa2341` |
| branch | `candidate/reminder-role-aware-r2-20260726` |
| مصدر البناء | Worktree معزول `/private/tmp/cand-reminder-r2-20260726` — **لم يُبنَ من `develop` إطلاقًا** |
| Migration | لا توجد |

---

## 2) المرحلة 1 — بوّابة ما قبل النشر (baseline)

قُرئت في 2026-07-26 10:29 UTC (13:29 الرياض):

| البند | القيمة | الحكم |
|---|---|---|
| `reporting-api.service` | active منذ 2026-07-25 23:30:33 UTC | مطابق |
| commit المنشور (InformationalVersion) | `459f60e278105b9a08563a17a6e59d15639ead54` | مطابق |
| migration count / head | 30 / `20260724224053_AddReportApproverAndKpiReviewerOverrides` | مطابق |
| `EmailNotifications__Mode` | `DryRun` | مطابق |
| `ReportReminderScheduler__Enabled` | `false` | مطابق |
| `Email__Enabled` | `false` | مطابق |
| health داخليّ / عامّ | 200 / 200 | مطابق |
| `email_notifications` | 110 صفًّا، **كلّها `Status=DryRun`** | مطابق |
| `SentAt IS NOT NULL` | 0 | مطابق |
| `email_outbox` | 0 | مطابق |
| markers في DLL المنشور | `ReportReminderSchedulerService`=0، `SalesSaturdayApplicabilityFloor`=1 | متّسق مع 459f60e |

لا اختلاف غير متوقّع ⇒ فُتحت البوّابة.

---

## 3) المرحلة 2 — النسخ الاحتياطية

| النوع | المسار | الحجم |
|---|---|---|
| Database dump | `/root/db-backups/reporting_prod-prereminderr2-20260726-103049.dump` | 929,566 بايت |
| Backend publish | `/opt/reporting/publish-backup-reminderr2-20260726-103049` | 107M |
| EnvironmentFile | `/root/reporting-api.env.backup-reminderr2-20260726-103049` (chmod 600) | 1,339 بايت |

الطابع الزمنيّ مخزَّن في `/root/reminder-r2-deploy-ts.txt`. **لم تُعدَّل قاعدة البيانات يدويًّا في أيّ لحظة.**

---

## 4) المرحلة 3 — نشر الكود فقط

`dotnet publish -c Release` من الـWorktree المعزول ⇒ `rsync -az --delete --exclude appsettings.Development.json` ⇒ `chown -R www-data:www-data` ⇒ `systemctl restart reporting-api`. البوّابات الثلاث بقيت كما هي أثناء النشر (`DryRun` / `false` / `false`).

| الإثبات | النتيجة |
|---|---|
| service | active |
| health داخليّ / عامّ | 200 / 200 |
| سجلّ الإقلاع | **`No migrations were applied. The database is already up to date.`** |
| Hosting environment | Production |
| commit في DLL المنشور | `df9891ceff575298470b5b8301ec4509122be99d` |
| migration count / head | 30 / `20260724224053` — **بلا تغيير** |
| إصلاح السبت | `SalesSaturdayApplicabilityFloor` = موجود |
| markers المجدول | 5 |
| `appsettings.Development.json` | غائب |
| SMTP / `SentAt` / `email_outbox` | 0 / 0 / 0 |

---

## 5) المرحلة 4 — DryRun إنتاجيّ تلقائيّ مضبوط

ضُبطت **نافذة واحدة**: `ReportReminderScheduler__RunAtRiyadhHours=14`، `Enabled=true`، `Mode=DryRun` ثابت. **لم يُستدعَ أيّ endpoint يدويّ.**

المجدول انطلق **تلقائيًّا** في نافذته: `Sun Jul 26 14:02:48 +03` (11:02:30 UTC) — ولم ينطلق قبل 14:00 (بوّابة التوقيت مُثبَتة عمليًّا).

```
ReportReminderScheduler ran for 2026-W30 mode=DryRun wouldGenerate=21 created=15 duplicate=6 noEmail=0 disabled=0
ReportReminderScheduler ran for 2026-W31 mode=DryRun wouldGenerate=14 created=14 duplicate=0 noEmail=0 disabled=0
```

### إثبات العقد — اليوم = الأحد

| النوع | العدد | المستلمون |
|---|---|---|
| `report-weekly-due` | **2** | **Manager حصرًا** — `FIN_MGR` (FinanceManager+Manager)، `SALES_MGR` (Manager)؛ المفتاح `report-weekly-due:2026-W30:2026-07-26:{userId}` |
| `report-daily-due` | **5** | **SALES_B2C ×4 + SALES_B2B ×1 حصرًا**، جميعهم نشطون |
| `report-overdue` | 11 | — |
| ملخّصات (team/department/executive) | 9 | — |
| `report-review-overdue-teamleader` | 2 | — |

لا `weekly-due` لأيّ موظّف أسبوعيّ ولا قائد فريق ولا تنفيذيّ يوم الأحد ⇒ **لا استحقاق أسبوعيّ مبكر**.

### إثبات السلامة

| البند | النتيجة |
|---|---|
| `Status` | **DryRun فقط (29/29)** |
| `SentAt` | 0 |
| SMTP (سجلّ الإرسال) | 0 |
| `email_outbox` | 0 |
| مفاتيح CorrelationKey مكرّرة | 0 |

### إعادة التشغيل مرّة واحدة (عدم التكرار)

```
ReportReminderScheduler ran for 2026-W30 mode=DryRun wouldGenerate=21 created=0 duplicate=21 noEmail=0 disabled=0
ReportReminderScheduler ran for 2026-W31 mode=DryRun wouldGenerate=14 created=0 duplicate=14 noEmail=0 disabled=0
```

عدد الصفوف بقي **139** (110 أساس + 29)، `SentAt`=0، `email_outbox`=0، مفاتيح مكرّرة=0، SMTP=0.

**لا مستلم غير متوقّع ولا انفجار في العدد ⇒ عُبرت البوّابة إلى المرحلة 5.**

---

## 6) المرحلة 5 — التفعيل الكامل الداخليّ

فُحصت البيئة أوّلًا للتأكّد من غياب أيّ Canary أو Allowlist:

- لا وجود لـ`Email__IncludedTypes` ولا `Email__ExcludedTypes`.
- `EmailNotifications__RecipientSafetyMode=Disabled` (مفتاح غير مستهلَك في الكود أصلًا).
- `App__BaseUrl=https://reports.emarketingacademy.net` — رابط Production صحيح.
- قناة SMTP: `GoogleWorkspace` / `smtp.gmail.com:587` وبيانات الاعتماد موجودة (لم تُطبَع).

الإعداد المطبَّق:

```
EmailNotifications__Mode=Enabled
ReportReminderScheduler__Enabled=true
ReportReminderScheduler__RunAtRiyadhHours=8
ReportReminderScheduler__PollMinutes=15
Email__Enabled=false
```

بعد إعادة التشغيل: service active، health 200/200، `No migrations were applied`، Hosting environment=Production، commit `df9891c`.

**الشروط المحقَّقة:** لا Canary، لا Allowlist، جميع المستخدمين الداخليّين النشطين مشمولون، القناة القديمة `email_outbox` مغلقة عبر `Email__Enabled=false` (وهي فعليًّا 0 صفّ)، لا تشغيل يدويّ للمولّد، **لم تُضَف الساعة الحالية** ⇒ أوّل إرسال فعليّ في أقرب نافذة **08:00 بتوقيت الرياض**.

### تنبيه تشغيليّ مسجَّل

`Reminders__Enabled=true` على الإنتاج — وهي بوّابة الخدمة القديمة `SubmissionReminderService` التي تُنشئ إشعارات داخل التطبيق فقط، ومسارها البريديّ محكوم بـ`Email__Enabled=false`. الشاهد العمليّ: `email_outbox` = 0 صفّ في كلّ القراءات.

### توقّع حجم أوّل إرسال (قراءة فقط، وقائيّ)

| البند | العدد |
|---|---|
| مستخدمون نشطون | 33 |
| نشطون بلا بريد | 0 |
| مستخدمون غير نشطين | 0 |
| مستلمو الاستحقاق الأسبوعيّ يوم الاثنين (Admin 3 + CEO 1 + GeneralManager 1) | حتّى 5 |
| مستلمو الاستحقاق اليوميّ (SALES_B2B/B2C نشطون) | 5 |

النطاق المتوقَّع من رتبة تشغيل الأحد (29 صفًّا) ⇒ **لا مؤشّر على انفجار في العدد**. ويُتوقَّع أن يكون **الإرسال الفعليّ أقلّ** من التوليد، لأنّ مفاتيح الترابط ذات مفتاح الدورة (`report-overdue` والملخّصات لدورة W30) استُهلكت بالفعل كصفوف `DryRun` ⇒ تُرجِع Duplicate ولا تُرسَل — وهو السلوك المقصود «عدم إعادة إرسال أيّ CorrelationKey سابق».

---

## 7) المرحلة 6 — مراقبة أوّل إرسال (مجدولة)

النظام **مسلَّح**؛ أوّل نافذة إرسال فعليّ هي **الاثنين 2026-07-27 الساعة 08:00 بتوقيت الرياض (05:00 UTC)**. المتوقَّع لذلك اليوم: `report-daily-due` للمبيعات + `report-weekly-due` للتنفيذيّين (GM/CEO/Admin) حصرًا.

بنود المراقبة المطلوبة عند النافذة: عدد تشغيلات المجدول، generated/sent/failed/skippedDuplicate/skippedNoEmail، المستلمون غير الصالحين أو غير النشطين، مفاتيح الترابط المكرّرة، استجابة المزوّد، قيم `SentAt`، إخفاقات SMTP، صحّة الخدمة، وصول اليوميّ للمبيعات فقط، عدم وصول أسبوعيّ مبكر، صحّة يوم كلّ دور، وعدم تراجع إصلاح Saturday Applicability.

---

## 8) المرحلة 7 — Rollback (جاهز، لم يُستدعَ)

عند أيّ تكرار فعليّ أو مستلم خاطئ مُثبَت أو إرسال لمستخدم غير نشط أو نسبة فشل غير طبيعيّة أو انفجار في العدد أو رابط Production غير صحيح:

```
sed -i 's/^EmailNotifications__Mode=.*/EmailNotifications__Mode=DryRun/' /etc/reporting-api.env
sed -i 's/^ReportReminderScheduler__Enabled=.*/ReportReminderScheduler__Enabled=false/' /etc/reporting-api.env
systemctl restart reporting-api
```

مع: **عدم حذف أيّ سجلّ `Sent`**، وعدم إعادة إرسال أيّ `CorrelationKey` سابق (مضمون بنيويًّا بمنع التكرار)، والاحتفاظ بكلّ السجلّات للتحليل، ثمّ إثبات توقّف الإرسال الجديد.

**Rollback الكود** (إن لزم): استعادة `/opt/reporting/publish-backup-reminderr2-20260726-103049` ثمّ `chown -R www-data:www-data` ثمّ إعادة التشغيل. لا توجد هجرة تُعكَس.

---

## 9) ما لم يُمَسّ

قاعدة البيانات (لا تعديل يدويّ)، سكيمة الهجرات (30 هجرة، head ثابت)، الواجهة الأمامية (لم تُنشَر)، `email_outbox` (0)، سجلّات `Sent` (0 حتّى الآن)، إصلاح Saturday Applicability، `ManagerId`/`TeamId`/`DepartmentId`/`BypassTeamLeaderApproval`.
