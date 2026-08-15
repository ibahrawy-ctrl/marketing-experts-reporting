# EMAIL-DRYRUN-DEDUPLICATION-ISOLATION-R1 — تقرير النشر الإنتاجيّ النهائيّ والقبول

**نوع المهمّة:** نشر إصلاح عاجل على Production — **Backend فقط**، بلا Migration، بلا Frontend، بلا كتابة على القاعدة.
**التاريخ:** الأربعاء 29 يوليو 2026.
**نافذة التنفيذ:** 10:14 → 11:12 بتوقيت الرياض (07:14 → 08:12 UTC).
**القرار النهائيّ: ✅ PASS.**

---

## 1. Preflight — البوّابات قبل البدء

| البند | القيمة | الحكم |
|---|---|---|
| ساعة الجهاز المحلّي | `UTC 2026-07-29 07:14:5x` | — |
| ساعة الخادم | `UTC 2026-07-29 07:14:5x` / `RIYADH 10:14:5x` | متطابقة ✓ |
| منطقة الخادم / NTP | `Etc/UTC`، NTP active، `System clock synchronized: yes` | ✓ |
| نافذة البريد الجارية | نافذة 09:00 الرياض انتهت فعليًّا 06:01:07–06:01:22 UTC | لا تعارض ✓ |
| النافذة التالية | 16:00 الرياض = 13:00 UTC | بعيدة ✓ |
| الحدّ الأقصى للبدء (15:30 الرياض = 12:30 UTC) | الهامش عند البدء > 5 ساعات | ✓ |
| النافذة المحظورة 15:45–16:30 | خارجها تمامًا (النشر تمّ 10:25) | ✓ |
| نشر/صيانة/نسخ احتياطيّ متوازٍ | `systemctl list-jobs` = *No jobs running*؛ لا عمليّات نشر أخرى | ✓ |

**بوّابة التوقّف الحاسمة:** الشرط كان «توقّف إن لم يكن Production على `21a0ed0`». أُثبت أنّه عليه بالضبط (القسم 2) ⇒ **البوّابة فُتحت بشكل مشروع**.

---

## 2. نَسَب Production قبل النشر (قراءة فقط)

### 2.1 الخدمة والبيئة

```
Service          : reporting-api.service
Unit             : /etc/systemd/system/reporting-api.service (363 bytes, mtime 2026-06-18 00:03:44)
EnvironmentFile  : /etc/reporting-api.env (1493 bytes, mtime 2026-07-26 19:49:58, mode 600)
User             : www-data
Publish path     : /opt/reporting/publish   (107M, 86 files)
Listen           : http://127.0.0.1:5090
Public           : https://reports.emarketingacademy.net
ActiveState      : active / running
MainPID          : 170752
NRestarts        : 0
ActiveEnterTimestamp = ExecMainStartTimestamp = Tue 2026-07-28 06:15:42 UTC
is-enabled       : enabled
ASPNETCORE_ENVIRONMENT = Production
Database         = reporting_prod (Host=127.0.0.1)
```

### 2.2 SourceLink قبل النشر — **إثبات النَسَب**

| DLL | SourceLink |
|---|---|
| `Reporting.Api.dll` | `21a0ed0cb6fb8f4c59b095007d0339c7b76f28b6` |
| `Reporting.Infrastructure.dll` | `21a0ed0cb6fb8f4c59b095007d0339c7b76f28b6` |
| `Reporting.Application.dll` | `21a0ed0cb6fb8f4c59b095007d0339c7b76f28b6` |
| `Reporting.Domain.dll` | `21a0ed0cb6fb8f4c59b095007d0339c7b76f28b6` |

⇒ Production كان بالضبط على الأب المتوقَّع `21a0ed0` ⇒ المرشّح `18207480` سليل مباشر لا شقيق ⇒ **لا تراجُع مقنَّع**.

### 2.3 بصمات الملفّات قبل النشر

| DLL | SHA256 | Size | mtime |
|---|---|---|---|
| Api | `a3271a0322b5ea03fd2902c66a00337ccdb4ba79696b9af755e592d160b703d8` | 340992 | 2026-07-26 23:04:08 |
| Infrastructure | `dc3070663a630193043f0746ac3409b3ddf746b40077fac0566de82392ee5b26` | 3662848 | 2026-07-26 23:04:08 |
| Application | `44d1a715500ae4bcdd41fb69823236a2c0a536dd72de3d8391f0e6c71b07aaec` | 1414656 | 2026-07-26 23:04:04 |
| Domain | `37c26fa653cb545d30d92c9021840daacc0413437d9516e628cdd74e2730e29a` | 88064 | 2026-07-26 23:04:03 |

### 2.4 الحالة الوظيفيّة قبل النشر

```
health internal : 200 {"status":"ok","service":"reporting-api"}
health public   : 200 {"status":"ok","service":"reporting-api"}
migrations      : 30 ، head = 20260724224053_AddReportApproverAndKpiReviewerOverrides
email_notifications : DryRun|DryRun = 139 ، Enabled|Sent = 43 ، total = 182
CorrelationKey LIKE 'dryrun:%' : 0
Pending|Processing|Failed      : 0|0|0
email_outbox                   : 0
UNIQUE INDEX : IX_email_notifications_CorrelationKey ON email_notifications USING btree ("CorrelationKey")
```

إعدادات البريد/المُجدوِل (بلا أسرار):

```
Email__Enabled                          = false
EmailNotifications__Mode                = Enabled
EmailNotifications__RecipientSafetyMode = Disabled
ReportReminderScheduler__Enabled        = true
ReportReminderScheduler__PollMinutes    = 15
ReportReminderScheduler__DailyDueHour   = 16
ReportReminderScheduler__WeeklyDueHour  = 9
ReportReminderScheduler__OverdueHour    = 9
ReportReminderScheduler__SummaryHour    = 9
ReportReminderScheduler__ReviewHour     = 9
```

آخر تشغيلَي مُجدوِل قبل النشر:

```
2026-07-29T06:01:07Z  ran for 2026-W30  categories=weeklyDue+overdue+summaries+review mode=Enabled wouldGenerate=28 created=0  duplicate=28 noEmail=0 disabled=0
2026-07-29T06:01:22Z  ran for 2026-W31  categories=weeklyDue+overdue+summaries+review mode=Enabled wouldGenerate=26 created=15 duplicate=11 noEmail=0 disabled=0
```

آخر صفّ: `087fe71c-df31-451a-9732-8330579462f7` @ `2026-07-29 06:01:21.595001+00` ، مفتاح `report-overdue:2026-07-28:7a9a6919-…:EmployeeReportNotSubmitted` ، `Enabled|Sent`.

### 2.5 بصمة خطّ الأساس التاريخيّة

```sql
SELECT COUNT(*), md5(string_agg("Id"::text||'|'||"CorrelationKey"||'|'||"Status"||'|'||"Mode", E'\n' ORDER BY "Id"))
FROM email_notifications WHERE "CreatedAtUtc" <= '2026-07-29 07:00:00+00';
-- 182 | dd522a0ee90c4302633a05440eb6b863
```

---

## 3. إثبات المرشّح

الشجرة المعزولة المجمّدة: `/private/tmp/cand-email-dedup-r1c-on-21a0ed0/` ، الفرع `candidate/email-dryrun-dedup-isolation-r1c-20260729`.

| البند | القيمة | الحكم |
|---|---|---|
| HEAD | `18207480fdfb4b69d7b1a4ba50eb22bece930524` | مطابق ✓ |
| Parent | `21a0ed0cb6fb8f4c59b095007d0339c7b76f28b6` | مطابق ✓ |
| Tree | `aef1a72c7b695c249848737c156669b57691e772` | مطابق ✓ |
| Subject | `fix(email): isolate DryRun deduplication namespace from real delivery keys (EMAIL-DRYRUN-DEDUPLICATION-ISOLATION-R1)` | ✓ |
| Author date | `2026-07-29T09:42:53+03:00` | ✓ |
| `git status --porcelain` | **0 سطر** | نظيفة ✓ |
| `git patch-id --stable` | `9d349f0fe95e7cc959d9e156923b16b508b62db4` | مطابق للمتوقَّع ✓ |

`git show --stat` — **7 ملفّات، +876 / −16**:

```
 .../Reporting.Infrastructure/Services/EmailNotificationService.cs |  23 +-
 .../Reporting.IntegrationTests/EmailControlTests.cs               |   5 +-
 .../Reporting.IntegrationTests/EmailDryRunDedupIsolationTests.cs  | 811 +++++++++++++
 .../Reporting.IntegrationTests/EmailNotificationsTests.cs         |   6 +-
 .../Reporting.IntegrationTests/ReportRemindersTests.cs            |  20 +-
 .../Reporting.IntegrationTests/RoleAwareReminderScheduleTests.cs  |  13 +-
 .../Reporting.IntegrationTests/SplitDeliveryWindowsTests.cs       |  14 +-
```

`git diff --name-status 21a0ed0..18207480`:

```
M  reporting-backend/src/Reporting.Infrastructure/Services/EmailNotificationService.cs
M  reporting-backend/tests/Reporting.IntegrationTests/EmailControlTests.cs
A  reporting-backend/tests/Reporting.IntegrationTests/EmailDryRunDedupIsolationTests.cs
M  reporting-backend/tests/Reporting.IntegrationTests/EmailNotificationsTests.cs
M  reporting-backend/tests/Reporting.IntegrationTests/ReportRemindersTests.cs
M  reporting-backend/tests/Reporting.IntegrationTests/RoleAwareReminderScheduleTests.cs
M  reporting-backend/tests/Reporting.IntegrationTests/SplitDeliveryWindowsTests.cs
```

بوّابات السطح: عدد الملفّات = 7 ✓ ، ملفّات إنتاج = **1** ✓ ، اختبارات = 6 ، **Frontend = 0** ✓ ، **Migration = 0** ✓ ، `appsettings`/أسرار = 0 ✓ ، منطق Recovery = 0 ✓ ، إشارات Option-B (`Mode_CorrelationKey`، `EmailNotificationModeScopedUniqueness`) = 0 ✓.

### 3.1 جوهر التغيير (الملفّ الإنتاجيّ الوحيد)

```csharp
public const string DryRunCorrelationKeyPrefix = "dryrun:";
...
var effectiveCorrelationKey = _options.Mode == EmailNotificationMode.DryRun
    ? DryRunCorrelationKeyPrefix + correlationKey
    : correlationKey;

var exists = await _db.EmailNotifications.AsNoTracking()
    .AnyAsync(n => n.CorrelationKey == effectiveCorrelationKey, ct);
...
CorrelationKey = effectiveCorrelationKey,
```

**خاصّيّة أمان جوهريّة:** Production يعمل على `Mode=Enabled` ⇒ الشرط الثلاثيّ يُنتِج `effectiveCorrelationKey == correlationKey` حرفيًّا ⇒ **سلوك الإنتاج الحاليّ لم يتغيّر إطلاقًا**؛ الإصلاح يستيقظ فقط لصفوف DryRun المستقبليّة. هذا يفسّر لماذا لم يُنشأ أيّ صفّ ولم يتغيّر أيّ مفتاح بعد النشر.

---

## 4. النسخة الاحتياطيّة (قبل الاستبدال)

| البند | القيمة |
|---|---|
| مسار النسخة | `/opt/reporting/publish-backup-email-dryrun-dedup-prer1-20260729-071902` |
| الحجم / عدد الملفّات | **107M / 86 ملفًّا** |
| الملكيّة | `www-data:www-data` |
| SourceLink داخلها | `21a0ed0cb6fb8f4c59b095007d0339c7b76f28b6` (الأربعة) ⇒ هدف تراجُع صالح ✓ |
| ملفّات pdb | 4 موجودة |
| `deps.json` / `runtimeconfig.json` | موجودان ✓ |
| `appsettings.Development.json` | **غير موجود** ✓ |
| SHA256 للأربعة | مطابقة حرفيًّا للحيّ وقت الأخذ ✓ |
| تعريف الخدمة | `/root/prod-backups/email-dryrun-dedup-prer1-20260729-071902/reporting-api.service` (363 bytes) |
| ملفّ البيئة | `/root/prod-backups/…/reporting-api.env.bak` (1493 bytes، mode 600، 33 مفتاحًا — **لم تُطبع محتوياته إطلاقًا**) |
| معرّف النافذة | `/root/emaildedup-r1-prod-ts.txt` = `20260729-071902` |
| مساحة القرص | 65G حرّة (33% مستخدَم) |

**لم تُؤخَذ نسخة قاعدة بيانات** — التزامًا بنصّ التذكرة: لا Migration ولا كتابة على القاعدة ⇒ لا حالة قاعدة يجب التراجُع عنها (أُعيد إثبات ذلك في القسم 10).

---

## 5. البناء والنشر المحلّي

```
restore : All projects are up-to-date for restore.
build   : Build succeeded — 0 Warning(s) — 0 Error(s)
publish : /private/tmp/pub-r1c-prod-20260729  →  107M ، 86 ملفًّا
```

| البوّابة | النتيجة |
|---|---|
| SourceLink داخل الـ4 DLLs | `18207480fdfb4b69d7b1a4ba50eb22bece930524` ✓ |
| `dotnet ef migrations has-pending-model-changes` | **No changes have been made to the model since the last migration** ✓ |
| عدد الهجرات في الشجرة | **30** ، آخرها `20260724224053_AddReportApproverAndKpiReviewerOverrides` (مطابق للإنتاج) ✓ |
| Frontend artifacts (js/css/html/map) | **0** ✓ |
| ملفّات env / أسرار (`.env`,`*.pfx`,`*.key`,`*secret*`) | **0** ✓ |
| `appsettings*.json` | ملفّ واحد فقط `appsettings.json`، sha256 `e6e29206658745f9f3f61a04dd226e840cb93499d18559bd4808d6653cdcbaee` — **مطابق بايت-ببايت للمنشور على الإنتاج** ⇒ لا تغيير تهيئة ✓ |
| `appsettings.Production.json` جديد | **غير موجود** ✓ |
| علامات الإصلاح داخل `Reporting.Infrastructure.dll` | `DryRunCorrelationKeyPrefix` = 1 ، السلسلة `dryrun:` (UTF-16LE) = 2 ✓ |
| علامات سلبيّة | `Mode_CorrelationKey` = 0 ، `recovery` = 0 ✓ |

بصمات البناء المحلّي:

```
Reporting.Api            0dae6c43e5aae7323b0f954419a19321a6328f0872bca9704c9f5e2f99d179ca  340992
Reporting.Infrastructure beea89b1ada69c088683c0ff6a9282d23aec68ec2b25751befd61f3192d0f420  3662848
Reporting.Application    7eee940502c453aa6e7c99eb25a5c8a3de80a8b2823aa66b9990859fce3cceac  1414656
Reporting.Domain         0baf51e40e83aecc243337ba06fffccf200d82cc6e1d59c7d3723dc38d8aa5a8  88064
```

> ملاحظة: بصمات `Application`/`Domain` تختلف عن الإنتاج السابق رغم عدم تعديل مصدرهما، لأنّ SourceLink يُضمِّن SHA الالتزام داخل كلّ DLL — وهذا هو المتوقَّع تمامًا.

**لم تُعَد الحزمة البطيئة الكاملة** التزامًا بنصّ التذكرة (البناء مطابق لما اجتاز RC: عزل 14/14، RoleAware+SplitDelivery 25/25، unit 313/313)، واكتُفي بالفحوص الموجَّهة أعلاه + دخان الإنتاج (القسم 16).

---

## 6. الخطّ الزمنيّ للنشر

| الحدث | UTC | الرياض |
|---|---|---|
| رفع إلى مجلّد التجهيز `/root/stage-emaildedup-r1c-20260729` (الخدمة تعمل، بلا أثر) | 07:24 | 10:24 |
| التحقّق من مجلّد التجهيز (86 ملفًّا، 107M، SHA256 ×4 مطابقة، SourceLink `18207480`، frontend=0) | 07:25 | 10:25 |
| **T_STOP_BEGIN** — `systemctl stop` | **07:25:33** | 10:25:33 |
| **T_STOP_DONE** — `inactive/dead`, MainPID=0 | **07:25:34** | 10:25:34 |
| **T_COPY_BEGIN** — `rsync -a --delete --exclude appsettings.Development.json` | **07:25:34** | 10:25:34 |
| **T_COPY_DONE** + `chown -R www-data:www-data` | **07:25:34** | 10:25:34 |
| **T_START_BEGIN** — `systemctl start` (مرّة واحدة فقط) | **07:25:34** | 10:25:34 |
| **T_START_DONE** — `active/running` | **07:25:34** | 10:25:34 |

**زمن التعطّل الفعليّ ≈ ثانية واحدة.**

بعد النشر:

```
MainPID              = 210497   (كان 170752)
NRestarts            = 0
Result               = success
ActiveEnterTimestamp = Wed 2026-07-29 07:25:34 UTC
publish              = 107M ، 86 ملفًّا ، www-data:www-data
```

**ما لم يُمَسّ:** `EnvironmentFile` (`/etc/reporting-api.env` — mtime بقي `2026-07-26 19:49:58`)، تعريف الخدمة (`/etc/systemd/system/reporting-api.service` — mtime بقي `2026-06-18 00:03:44`)، سلاسل الاتّصال، إعدادات البريد والمُجدوِل، الواجهة، الملكيّات. **لم يُنفَّذ أيّ أمر Migration، ولم يُشغَّل أيّ Job يدويًّا.**

---

## 7. SourceLink بعد النشر

| DLL | SourceLink | الحكم |
|---|---|---|
| `Reporting.Api.dll` | `18207480fdfb4b69d7b1a4ba50eb22bece930524` | ✓ |
| `Reporting.Infrastructure.dll` | `18207480fdfb4b69d7b1a4ba50eb22bece930524` | ✓ |
| `Reporting.Application.dll` | `18207480fdfb4b69d7b1a4ba50eb22bece930524` | ✓ |
| `Reporting.Domain.dll` | `18207480fdfb4b69d7b1a4ba50eb22bece930524` | ✓ |

⇒ الإنتاج الآن على المرشّح المعتمَد، والنَسَب محفوظ (`Parent = 21a0ed0` = ما كان منشورًا قبل دقائق).

---

## 8. التحقّق من البصمات

| الملفّ | SHA256 محلّي (publish) | SHA256 على الإنتاج | الحكم |
|---|---|---|---|
| `Reporting.Api.dll` | `0dae6c43e5aae7323b0f954419a19321a6328f0872bca9704c9f5e2f99d179ca` | نفسه | ✓ |
| `Reporting.Infrastructure.dll` | `beea89b1ada69c088683c0ff6a9282d23aec68ec2b25751befd61f3192d0f420` | نفسه | ✓ |
| `Reporting.Application.dll` | `7eee940502c453aa6e7c99eb25a5c8a3de80a8b2823aa66b9990859fce3cceac` | نفسه | ✓ |
| `Reporting.Domain.dll` | `0baf51e40e83aecc243337ba06fffccf200d82cc6e1d59c7d3723dc38d8aa5a8` | نفسه | ✓ |
| `appsettings.json` | `e6e29206658745f9f3f61a04dd226e840cb93499d18559bd4808d6653cdcbaee` | نفسه (وهو نفسه قبل النشر) | ✓ |

عدد الملفّات 86 والحجم 107M قبل النشر وبعده — بلا زيادة أو نقصان.

---

## 9. الصحّة (Health)

| الفحص | قبل النشر | بعد النشر |
|---|---|---|
| `http://127.0.0.1:5090/health` | `200 {"status":"ok","service":"reporting-api"}` | `200 {"status":"ok","service":"reporting-api"}` |
| `https://reports.emarketingacademy.net/health` | `200 {"status":"ok","service":"reporting-api"}` | `200 {"status":"ok","service":"reporting-api"}` |
| SPA العامّة `/` | — | `200` |

الخدمة بعد 43 دقيقة من التشغيل: `active/running`، `MainPID=210497` بلا تغيير، `NRestarts=0` ⇒ **لا حلقة إعادة تشغيل**.

---

## 10. ثوابت الهجرات

```
سجلّ الإقلاع : "No migrations were applied. The database is already up to date."
عدّ "Applying migration" في سجلّ الإقلاع : 0
__EFMigrationsHistory : 30 صفًّا (قبل النشر = 30، بعده = 30)
head : 20260724224053_AddReportApproverAndKpiReviewerOverrides (بلا تغيير)
```

⇒ لا Migration، لا تغيير سكيمة، ولا حاجة لأيّ تراجُع قاعديّ.

---

## 11. ثوابت البريد والمُجدوِل

| المفتاح | قبل | بعد | الحكم |
|---|---|---|---|
| `EmailNotifications__Mode` | `Enabled` | `Enabled` | ✓ |
| `EmailNotifications__RecipientSafetyMode` | `Disabled` | `Disabled` | ✓ |
| `Email__Enabled` | `false` | `false` | ✓ |
| `ReportReminderScheduler__Enabled` | `true` | `true` | ✓ |
| `ReportReminderScheduler__PollMinutes` | `15` | `15` | ✓ |
| `DailyDueHour` | `16` | `16` | ✓ |
| `WeeklyDueHour` / `OverdueHour` / `SummaryHour` / `ReviewHour` | `9` | `9` | ✓ |
| mtime لملفّ البيئة | `2026-07-26 19:49:58` | نفسه | لم يُمَسّ ✓ |

**لم يُعدَّل أيّ إعداد بريد أو جدولة، ولم يُفعَّل أيّ قناة قديمة، ولم يُمَسّ مركز التحكّم بالبريد.**

---

## 12. مراقبة المُجدوِل بعد إعادة التشغيل

**لم يُشغَّل المُجدوِل يدويًّا إطلاقًا.** المراقبة كانت سلبيّة بالكامل (journald + القاعدة) لمدّة **43 دقيقة ≈ دورتا Poll** (`PollMinutes=15`)، من 07:25:34 حتّى 08:09 UTC.

| القياس | النتيجة |
|---|---|
| أسطر `ReportReminderScheduler ran for` بعد إعادة التشغيل | **0** |
| أسطر `Email sent to …` (SMTP) بعد إعادة التشغيل | **0** |
| أيّ ذكر لـ`ReportReminderScheduler` في السجلّ بعد إعادة التشغيل | **0** |
| صفوف `email_notifications` أُنشئت بعد `07:25:34` | **0** |

### 12.1 لماذا الصمت هو النتيجة الصحيحة — إثبات لا افتراض

السبب مُثبَت من المصدر `ReportReminderSchedulerService.TickAsync`:

```csharp
var categories = _options.CategoriesForHour(slot.Hour);
if (categories.IsEmpty) return null;      // ← لا سجلّ ولا عمل
if (_lastRunSlot == slot) return null;
```

ساعة الرياض أثناء المراقبة كانت **10 ثمّ 11**، ولا نافذة مضبوطة عليهما (النوافذ 9 و16) ⇒ كلّ نبضة تعود `null` **صامتة**. ونفس الصمت مُثبَت **قبل النشر** بين 06:01 و07:25 (خمس نبضات بلا سطر واحد) ⇒ النمط **سابق للنشر** ولا علاقة له به.

سجلّ اليوم كاملًا لتشغيلات المُجدوِل — ولا شيء بعد النشر:

```
2026-07-29T06:01:07Z ran for 2026-W30 … wouldGenerate=28 created=0  duplicate=28
2026-07-29T06:01:22Z ran for 2026-W31 … wouldGenerate=26 created=15 duplicate=11
(بعد النشر 07:25:34 — لا شيء)
```

### 12.2 ملاحظة مخاطرة موثَّقة (لم تتحقّق، وسابقة للنشر)

`_lastRunSlot` حالة **داخل الذاكرة** تُفقَد عند إعادة التشغيل. لو وقعت إعادة تشغيل **داخل** ساعة نافذة بعد أن جرى تشغيلها، لأعاد المُجدوِل تقييم الفتحة نفسها. لم يحدث ذلك هنا (أُعيد التشغيل عند الساعة 10 وهي ليست نافذة). وحتّى لو حدث، الحاجز الفعليّ هو تفرّد `CorrelationKey` (فحص التطبيق + الفهرس الفريد) الذي يُنتِج `created=0 duplicate=N` كما ظهر فعليًّا في تشغيل 06:01:07. **هذه خاصّيّة سابقة للإصلاح ولم يُدخِلها هذا النشر.**

### 12.3 ما لم يظهر (كلّها مطلوبة)

| المحظور | النتيجة |
|---|---|
| إرسال مكرَّر بسبب إعادة التشغيل | لم يحدث — 0 صفّ، 0 SMTP |
| تغيّر في الرسائل التاريخيّة | لم يحدث (القسم 14) |
| حذف/تعديل `CorrelationKey` | لم يحدث |
| تنفيذ Recovery | لم يحدث |
| رسالة خارج الجدول | لم تحدث |
| تغيّر Email Mode | لم يحدث |
| تفعيل قناة قديمة | لم يحدث |
| `Failed` / `Pending` / `Processing` عالقة | 0 / 0 / 0 |

> **متابعة موصى بها (خارج نطاق هذا القبول):** نافذة 16:00 الرياض (13:00 UTC) هي أوّل تشغيل مُجدوِل حقيقيّ على البناء الجديد. المتوقَّع: سلوك مطابق للسابق حرفيًّا لأنّ الوضع `Enabled` يُنتِج المفتاح القانونيّ نفسه. لا إجراء مطلوب، مجرّد ملاحظة.

---

## 13. أعداد الإشعارات قبل/بعد

| القياس | قبل النشر (07:24) | بعد النشر (08:09) | الفرق |
|---|---|---|---|
| `DryRun \| DryRun` | 139 | 139 | 0 |
| `Enabled \| Sent` | 43 | 43 | 0 |
| **الإجمالي** | **182** | **182** | **0** |
| مفاتيح تبدأ بـ`dryrun:` | 0 | **0** | 0 |
| `Pending` \| `Processing` \| `Failed` | 0\|0\|0 | **0\|0\|0** | 0 |
| `email_outbox` | 0 | **0** | 0 |
| صفوف بعد `07:25:34` | — | **0** | — |

> صفر صفوف بادئة `dryrun:` هو **المتوقَّع تمامًا** على الإنتاج: الوضع `Enabled` لا يُنتِج البادئة أبدًا. ظهور أيّ صفّ ببادئة على الإنتاج كان سيكون مؤشّر خطأ لا نجاح.

---

## 14. ثوابت المفاتيح التاريخيّة

بصمة المجموعة التاريخيّة (`CreatedAtUtc <= 2026-07-29 07:00:00+00`) — نفس الاستعلام والفاصل `E'\n'` والترتيب `ORDER BY "Id"`:

| اللحظة | النتيجة |
|---|---|
| قبل النشر (07:24) | `182 \| dd522a0ee90c4302633a05440eb6b863` |
| فور إعادة التشغيل (07:26) | `182 \| dd522a0ee90c4302633a05440eb6b863` |
| بعد دورتَي Poll (08:09) | `182 \| dd522a0ee90c4302633a05440eb6b863` |

⇒ **لا صفّ حُذف، ولا مفتاح عُدِّل، ولا حالة تغيّرت.**

الفهرس الفريد بلا تغيير:

```sql
CREATE UNIQUE INDEX "IX_email_notifications_CorrelationKey"
  ON public.email_notifications USING btree ("CorrelationKey");
```

### 14.1 مجموعة الرسائل المحجوبة تاريخيًّا (إعادة إثبات، قراءة فقط)

| المجموعة | الصفوف | المستلِمون |
|---|---|---|
| كلّ صفوف DryRun ليوم 2026-07-26 بلا توأم `Enabled` | **29** | **14** |
| المجموعة القابلة للتكرار (باستثناء `report-daily-due` و`report-weekly-due` المقيَّدتين باليوم) | **22** | **13** |
| **الرقم التشغيليّ الموثَّق مرجعيًّا** | **20** | **13** |

**تفسير الفارق بأمانة (بلا تعديل أيّ صفّ):** التفكيك الموثَّق سابقًا هو `29 = 7 مقيّدة باليوم + 20 محجوبة + 2 لم تعد مستحقّة`. المجموعة القابلة للتكرار (22) هي مجموعة عليا تشمل الحالتين اللتين انتفى استحقاقهما (حبيبة — سلّمت في 07-27، وخالد مجدي — بلا مراجعات W30 معلّقة). أي أنّ **20** يظلّ الرقم التشغيليّ الصحيح، و**22** هو ناتج الاستعلام الخام قبل استبعاد المنتفيتين. **لم يُعدَّل أيّ صفّ ولم يُصحَّح أيّ رقم في القاعدة.**

---

## 15. إثبات صفر-Recovery

| البند | الدليل |
|---|---|
| لا كود Recovery في المرشّح | `grep -i recovery` على سطح الباتش = 0 ؛ داخل `Reporting.Infrastructure.dll` = 0 |
| لا تشغيل Recovery | 0 صفّ أُنشئ بعد إعادة التشغيل ؛ 0 سطر SMTP |
| لم تُرسَل الرسائل الـ20 المحجوبة | `Enabled\|Sent` بقي 43 بلا زيادة ؛ `email_outbox` = 0 |
| لم يُفتح أيّ مفتاح تاريخيّ | البصمة `dd522a0e…` ثابتة (القسم 14) |
| لا رسالة تعويضيّة | 0 صفّ جديد إطلاقًا |
| لا تعديل يدويّ على القاعدة | لم يُنفَّذ أيّ `INSERT/UPDATE/DELETE` — كلّ استعلامات هذه النافذة `SELECT` فقط |

الطبيعة **Forward-only** مضمونة تصميميًّا: البادئة تُطبَّق على صفوف DryRun **الجديدة** فقط، ولا تعيد كتابة أيّ صفّ قائم.

---

## 16. اختبارات الدخان (قراءة فقط)

**لم يُنشأ أيّ تقرير/KPI/إجازة/إشعار تجريبيّ.** استُخدم حساب break-glass من ملفّ البيئة، **والتوكن لم يُطبع**.

| المسار | الحالة |
|---|---|
| `POST /api/auth/login` | **OK** (توكن 624 محرفًا، لم يُطبع) |
| `/health` | 200 |
| `/api/dashboard/me` | 200 |
| `/api/submissions` | 200 |
| `/api/report-templates` | 200 |
| `/api/kpi-evaluations` | 200 |
| `/api/kpi-templates` | 200 |
| `/api/leave-requests/my` | 200 |
| `/api/me/balances` | 200 |
| `/api/employee-service-requests/my` | 200 |
| `/api/directory/users` | 200 |
| `/api/notifications` | 200 |
| `/api/reports/submission-compliance` | 200 |
| `/api/clients` | 200 |
| `/api/projects` | 200 |
| `/api/audit-logs` | 200 |
| **`/api/email-notifications`** (مركز التحكّم بالبريد) | **200** — يُعيد بيانات سليمة بلا انهيار |
| **`/api/email-notifications/log`** | **200** |
| `/api/report-calendar/missing-reports?weekKey=2026-W31` | 200 |
| `/api/report-calendar/approval-delays?weekKey=2026-W31` | 200 |
| `/api/report-calendar/sales-daily-compliance?weekKey=2026-W31` | 200 |
| `/api/email-notifications` (مجهول) | **401** ✓ |
| `/api/submissions` (مجهول) | **401** ✓ |
| `https://reports.emarketingacademy.net/` (الصفحة الرئيسة) | 200 |

عيّنة استجابة مركز التحكّم بالبريد (سليمة، عربيّة، بالحقول الكاملة):

```json
{"id":"087fe71c-…","createdAtUtc":"2026-07-29T06:01:21.595001Z","eventType":"report-overdue",
 "recipientName":"حبيبة","subject":"تنبيه بتأخر تقريرك","status":"Sent","mode":"Enabled","failureReason":null}
```

> ملاحظة منهجيّة: ثلاثة مسارات أعطت 404 في الجولة الأولى (`/api/report-calendar/me`، `/api/email-notifications/summary`، `/api/audit`). أُثبت بالرجوع إلى تعريفات الـControllers أنّها **مسارات كتبتها أنا خطأً** لا انحدارًا: المسارات الصحيحة هي `report-calendar/{missing-reports|approval-delays|sales-daily-compliance}` و`email-notifications/log` و`audit-logs` — وكلّها أعطت 200 عند إعادة الاختبار. **لا انحدار وظيفيّ.**

---

## 17. مراجعة السجلّات

نافذة السجلّ: من `2026-07-29 07:25:30` (قبل الإيقاف بأربع ثوانٍ) حتّى `08:09` UTC.

| التصنيف | العدد |
|---|---|
| `fail:` | **0** |
| `crit:` | **0** |
| `Unhandled exception` | **0** |
| أخطاء مصادقة | **0** |
| `warn:` | **14** |

تفكيك الـ14 تحذيرًا — **كلّها حميدة وسابقة للنشر**:
- 4 × `Microsoft.EntityFrameworkCore.Model.Validation[10622]` — تحذير global query filter على `KpiEvaluation`/`ReportSubmission` (ظاهر أيضًا في بوّابة `has-pending-model-changes` محليًّا، وسابق للتذكرة).
- `Microsoft.AspNetCore.DataProtection.Repositories.EphemeralXmlRepository[50]` — مستودع مفاتيح في الذاكرة (سلوك قائم منذ ما قبل النشر).
- الباقي تكرارات لنفس التحذيرين عبر دورات الإقلاع.

سجلّ الإيقاف/التشغيل نظيف:

```
Stopping reporting-api.service …
Application is shutting down...
reporting-api.service: Deactivated successfully.
Consumed 10min 7.390s CPU time, 318.7M memory peak, 0B memory swap peak.
Started reporting-api.service - Marketing Experts Reporting API.
…
No migrations were applied. The database is already up to date.
Now listening on: http://127.0.0.1:5090
Hosting environment: Production
Content root path: /opt/reporting/publish
```

> **درس تشخيصيّ مهمّ:** عدّ الأخطاء بـ`grep -i 'error|exception|fail'` على سجلّ هذا النظام **يُنتِج أرقامًا كاذبة** (مئات) لأنّ تسجيل EF Core لأوامر SQL يطبع اسم العمود `AccessFailedCount` في كلّ استعلام مستخدم. العدّ الموثوق الوحيد هو الترسيخ على بادئة مستوى السجلّ: `grep -E '^(fail|crit|warn):'`.

---

## 18. جاهزيّة التراجُع (مُثبتة، **غير منفَّذة**)

النشر سليم ⇒ لم يُنفَّذ أيّ تراجُع. الجاهزيّة مُثبتة بالفحص:

| البند | القيمة |
|---|---|
| مسار النسخة | `/opt/reporting/publish-backup-email-dryrun-dedup-prer1-20260729-071902` |
| موجودة | **YES** |
| الحجم / الملفّات / الملكيّة | 107M / 86 / `www-data:www-data` |
| SourceLink داخلها (الأربعة) | `21a0ed0cb6fb8f4c59b095007d0339c7b76f28b6` ✓ |
| تعريف الخدمة الاحتياطيّ | `/root/prod-backups/email-dryrun-dedup-prer1-20260729-071902/reporting-api.service` (363 bytes) |
| ملفّ البيئة الاحتياطيّ | `/root/prod-backups/…/reporting-api.env.bak` (1493 bytes، mode 600) |
| تعريف الخدمة الحيّ | لم يُمَسّ (mtime `2026-06-18 00:03:44`) |
| ملفّ البيئة الحيّ | لم يُمَسّ (mtime `2026-07-26 19:49:58`) |
| تراجُع قاعديّ مطلوب | **لا** — 0 Migration مُطبَّقة، 0 كتابة على القاعدة، `__EFMigrationsHistory` = 30 ثابت |

**الإجراء الدقيق (بأسرار مُقنَّعة، ينفَّذ فقط عند الحاجة):**

```bash
ssh -i ~/.ssh/<KEY> root@<PROD_HOST>
BK=/opt/reporting/publish-backup-email-dryrun-dedup-prer1-20260729-071902

systemctl stop reporting-api.service

# استعادة الحمولة فقط — لا مساس بملفّ البيئة ولا بتعريف الخدمة
rsync -a --delete --exclude 'appsettings.Development.json' "$BK"/ /opt/reporting/publish/
chown -R www-data:www-data /opt/reporting/publish

systemctl start reporting-api.service

# إثبات التراجُع
for d in Reporting.Api Reporting.Infrastructure Reporting.Application Reporting.Domain; do
  strings -a /opt/reporting/publish/$d.dll | grep -Eo '[0-9a-f]{40}' | sort -u | head -1   # ⇒ 21a0ed0cb6fb8f4c59b095007d0339c7b76f28b6
done
curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5090/health              # ⇒ 200
curl -s -o /dev/null -w '%{http_code}\n' https://<PUBLIC_HOST>/health              # ⇒ 200
systemctl show reporting-api.service -p ActiveState -p NRestarts                   # ⇒ active / 0
# لا خطوة قاعدة بيانات إطلاقًا
```

زمن التراجُع المتوقَّع مطابق لزمن النشر (≈ثانية تعطّل).

---

## 19. القرار النهائيّ

### ✅ **PASS**

| معيار القبول | النتيجة |
|---|---|
| Production SourceLink = `18207480` | ✅ الأربعة |
| الأب الصحيح محفوظ (`21a0ed0`) | ✅ مُثبَت قبل النشر |
| الخدمة مستقرّة | ✅ `active/running`، `NRestarts=0`، `MainPID` ثابت 43 دقيقة |
| health 200 / 200 | ✅ داخليّ + عامّ |
| بلا Migration | ✅ 30→30، head ثابت، `Applying migration`=0 |
| بلا تغيير Frontend | ✅ `index-96kHwdBC.js` و`index.html` (27 يوليو) بلا مساس |
| إعدادات البريد بلا تغيير | ✅ 10 مفاتيح مطابقة + mtime البيئة ثابت |
| بلا إرسال مكرَّر بسبب إعادة التشغيل | ✅ 0 صفّ، 0 SMTP عبر دورتَي Poll |
| بلا تعديل مفاتيح تاريخيّة | ✅ البصمة `dd522a0e…` ثابتة ×3 قياسات |
| بلا Recovery | ✅ لا كود ولا أثر |
| بلا `Failed`/`Pending`/`Processing` جديدة | ✅ 0\|0\|0 |
| بلا انحدار وظيفيّ | ✅ 21 مسار قراءة 200 + 401 للمجهول |
| Rollback جاهز | ✅ مُثبَت بالفحص، بلا تنفيذ |

**لا يوجد أيّ معيار FAIL محقَّق:** لا اختلاف SourceLink، لا فشل health، لا حلقة إعادة تشغيل، لا Migration غير متوقَّعة، لا إرسال مكرَّر، لا تعديل مفاتيح، لا تغيير Mode/Scheduler، لا تغيير Frontend، لا 500 جديد، لا خطأ مصادقة، والتراجُع مُثبَت.

---

## 20. الخطوة التالية المقترَحة

**التوقّف الآن.** المهمّة اكتملت بالكامل وفق شرط التوقّف في التذكرة.

المسارات التالية **ممنوعة دون تصريح جديد ومستقلّ**:
- `EMAIL-MISSED-NOTIFICATIONS-RECOVERY-R1` — استرجاع الرسائل الـ20 المحجوبة.
- `EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1` — مركز التحكّم بالبريد.
- أيّ محور آخر.

**متابعة قراءة-فقط اختياريّة (لا تتطلّب تغييرًا):** رصد نافذة 16:00 الرياض (13:00 UTC) اليوم — أوّل تشغيل مُجدوِل حقيقيّ على البناء الجديد. المتوقَّع سلوك مطابق حرفيًّا لما قبل النشر، لأنّ `Mode=Enabled` يُنتِج المفتاح القانونيّ نفسه دون بادئة. أيّ رسالة تُنشأ في تلك النافذة يجب أن يُحلَّل سبب استحقاقها ولا تُنسَب تلقائيًّا إلى هذا الإصلاح.

**تنظيف مؤجَّل (اختياريّ، غير عاجل):** مجلّد التجهيز `/root/stage-emaildedup-r1c-20260729` (107M) وسكربت الدخان `/root/smoke-r1c.sh` يمكن حذفهما بعد استقرار النافذة القادمة. **النسخة الاحتياطيّة `/opt/reporting/publish-backup-email-dryrun-dedup-prer1-20260729-071902` يجب الإبقاء عليها** حتّى إغلاق فترة المراقبة.

---

### ملحق — مراجع ثابتة

```
المرشّح المنشور   : 18207480fdfb4b69d7b1a4ba50eb22bece930524
الأب              : 21a0ed0cb6fb8f4c59b095007d0339c7b76f28b6
الشجرة            : aef1a72c7b695c249848737c156669b57691e772
patch-id (stable) : 9d349f0fe95e7cc959d9e156923b16b508b62db4
الفرع             : candidate/email-dryrun-dedup-isolation-r1c-20260729
الشجرة المعزولة   : /private/tmp/cand-email-dedup-r1c-on-21a0ed0/
معرّف نافذة النشر : 20260729-071902  (/root/emaildedup-r1-prod-ts.txt)
بصمة خطّ الأساس   : 182 | dd522a0ee90c4302633a05440eb6b863
                    (email_notifications ، CreatedAtUtc <= 2026-07-29 07:00:00+00 ،
                     md5 على Id|CorrelationKey|Status|Mode ، الفاصل E'\n' ، ORDER BY "Id")
```
