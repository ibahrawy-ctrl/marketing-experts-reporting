# EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 — تقرير قبول RC

**القرار النهائيّ: `EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 RC PASS — PRODUCTION READY`**

- التاريخ: 2026-07-29
- النطاق المُنفَّذ: Phase 1 → Local Implementation → Candidate Freeze → RC Deployment → RC Acceptance → Production Readiness (تحضير فقط)
- الإنتاج **لم يُمَسّ إطلاقًا**: لا نشر، لا إعادة تشغيل، لا تعديل إعدادات، لا تدوير App Password، لا إرسال Recovery، لا Job يدويّ.

---

## 1. ملخّص تنفيذيّ

شاشة «مركز التحكم بالبريد» كانت تعرض **«DryRun» مُثبَّتة في الشيفرة** ولا تقرأ الحالة التشغيليّة الحقيقيّة إطلاقًا. النتيجة: على الإنتاج — حيث `EmailNotifications__Mode=Enabled` ويُرسَل بريد حقيقيّ فعلًا (49 رسالة) — كانت الشاشة تُطمئن المستخدم كذبًا بأن النظام في وضع محاكاة.

الإصلاح: مسار قراءة-فقط جديد `GET /api/email-control/status` يُرجِع الحالة التشغيليّة الحيّة من **مصدر الحقيقة الفعليّ** (`EmailNotificationOptions.Mode` + `ReportReminderSchedulerOptions` + `IEmailSender.IsConfigured` + `IHostEnvironment`)، ولوحة عرض جديدة من 7 أقسام تعرض الوضع الحاليّ وتفصل السجلّ التاريخيّ عن الحالة الجاريَة وتُصنّف العلم القديم `Email__Enabled` كإعداد توافق غير حاكم.

نُشر على RC وقُبِل: RC يعرض الآن **DryRun** بصدق، والإنتاج (لو نُشر) سيعرض **LIVE / ENABLED** بصدق.

---

## 2. Phase 1 — أسبقيّة الإعدادات مشتقّة من الشيفرة

| الإعداد | المستهلِك (الشيفرة) | الأثر | مصدر حقيقة؟ | آمن للعرض؟ |
|---|---|---|---|---|
| `EmailNotifications__Mode` | `EmailNotificationService` عبر `IOptions<EmailNotificationOptions>` | يفرّع بين `Enabled` (إرسال SMTP فعليّ) و`DryRun` (تسجيل صفّ بلا إرسال) و`Disabled` (لا شيء) | **نعم — حاكم مطلق للقناة الجديدة** | نعم (قيمة تعداديّة، بلا سرّ) |
| `ReportReminderScheduler__Enabled` | `ReportReminderSchedulerService` (BackgroundService) | يشغّل/يوقف إطلاق النوافذ التلقائيّة | نعم (لجدولة الإطلاق لا لوضع الإرسال) | نعم |
| `ReportReminderScheduler__PollMinutes` + `DailyDueHour`/`WeeklyDueHour`/`OverdueHour`/`SummaryHour`/`ReviewHour` | نفس الخدمة | تحديد نبض الفحص وساعات النوافذ بتوقيت الرياض | نعم (لجدولة) | نعم |
| `Email__SmtpHost` / `Email__SmtpPort` / `Email__FromEmail` | `MailKitEmailSender` عبر `EmailOptions` (Effective*) | تهيئة قناة SMTP؛ `IsConfigured` يُشتقّ منها | نعم (لجاهزيّة النقل) | نعم (Host/Port/From فقط) |
| `Email__Password` / `Email__Username` | `MailKitEmailSender` | اعتماد SMTP | نعم (لكن **سرّ**) | **لا** — يُعرَض `credentialConfigured: bool` حصرًا |
| `Email__Enabled` | `EmailOutboxDispatcher` + `SubmissionReminderService` **فقط** | يحكم المسارات القديمة (صندوق الصادر القديم وتذكير التسليم القديم) | **لا — Legacy فقط، لا يحكم القناة الجديدة** | نعم (مع وسم صريح «ليس مصدر حقيقة») |

**البرهان القاطع على أن `Email__Enabled` ليس حاكمًا:** على الإنتاج `Email__Enabled=false` بينما `EmailNotifications__Mode=Enabled`، وقاعدة الإنتاج تُظهِر **49 صفًّا `Mode=Enabled / Status=Sent`** مع أسطر قبول SMTP في السجلّ. لو كان `Email__Enabled` حاكمًا لكان العدد صفرًا. لذلك **يُمنَع** استنتاج «القناة مطفأة» من هذا العلم.

---

## 3. Phase 2 — الأساس التركيبيّ (Synthetic Production Integration Baseline)

- شجرة معزولة: `/private/tmp/cand-email-control-status-r1-20260729/`
- فرع: `candidate/email-control-status-r1-20260729`
- الأساس أُنشئ من backend الإنتاج ثم دُمج فيه frontend الإنتاج:
  - `git merge --no-ff be07a7a7… -m "chore: integrate current production backend and frontend baselines"`

| البند | القيمة |
|---|---|
| **Baseline SHA** | `ff51825d27f0ac48e784352d11fee96aad5d0155` |
| **Merge Parent 1 (Backend Prod)** | `18207480fdfb4b69d7b1a4ba50eb22bece930524` |
| **Merge Parent 2 (Frontend Prod)** | `be07a7a7fd30e6210f29354039c952b7c4c4cc58` |
| **Baseline Tree** | `ad757833f89f9e1407a906501ca4ea4c6f3c688b` |
| **merge-base(18207480, be07a7a7)** | `21a0ed0cb6fb8f4c59b095007d0339c7b76f28b6` (⇒ شقيقان لا سلف/خلف) |

الأساس **خالٍ تمامًا** من أيّ تعديل وظيفيّ؛ الإصلاح في commit مستقلّ فوقه.
لم يُستخدَم `develop` (`6859ee0`) ولا أيّ ملفّ Scheduler قديم غير متتبَّع ولا أيّ نسخ من WIP.

---

## 4. Phase 3 — اختبار الأساس قبل أيّ تعديل

- Backend: `dotnet restore` + `dotnet build` ناجحان على `ff51825` وحده.
- Frontend: `npm ci` + `vitest run` + `tsc -b` + `vite build` (`VITE_API_BASE_URL=/api`) ناجحة على الأساس وحده.
- **الاستنتاج:** الدمج بمفرده أخضر ⇒ لم نتوقّف، وأيّ فشل لاحق يُنسَب للإصلاح لا للأساس.

---

## 5. Phase 4 — مسار الحالة قراءة-فقط

- المسار: `GET /api/email-control/status`
- السياسة: `Policies.EmailControlManage` (**Admin حصرًا** — لم تُوسَّع الصلاحيات).
- الـDTO: `EmailControlCenterStatusDto` بـ **34 حقلًا** بالضبط.
- **`LastSchedulerRunAtUtc` غير موجود** (مؤكَّد ميدانيًّا: `has_lastSchedulerRunAtUtc = False`).
- التنفيذ: `EmailControlStatusService` مسجَّل في `DependencyInjection.cs`، ولا يُنشئ ولا يُعدّل ولا يحذف أيّ صفّ.

---

## 6. Phase 5 — منع تسريب الأسرار

| قناة الفحص | النتيجة |
|---|---|
| جسم JSON للاستجابة | `secret_key_hits = []` |
| السجلّات (`rc-api.log`) | لا كلمة مرور/توكن/سلسلة اتصال |
| DOM المُصيَّر في المتصفّح | `secrets = []` |
| الحزمة المُقدَّمة `index-Bq08cb54.js` | 0 تطابق |
| ملفّات sourcemap على القرص | 0 |

الاعتماد يُعرَض كـ`credentialConfigured: false` (بوليان) فقط؛ لا `Password`/`Secret`/`Token`/`ConnectionString`/`ApiKey`.

---

## 7. Phase 6 — منطق الحالة والتحذيرات

- الشارات: `LIVE / ENABLED` · `DRY RUN` · `DISABLED`.
- العناوين العربيّة:
  - DryRun ⇒ «وضع المحاكاة مفعّل — لا يتم إرسال رسائل حقيقية».
  - Enabled ⇒ عنوان الإرسال الفعليّ.
- **السجلّ التاريخيّ لا يغيّر الحالة الجاريَة إطلاقًا** — يُعرَض في قسم منفصل تحت «أرقام تراكميّة لصفوف سُجِّلت سابقًا — لا تعبّر عن الوضع الحاليّ للنظام».
- العلم القديم في قسم مستقلّ «إعدادات التوافق القديمة» مع سطر «هل هو مصدر حقيقة لهذه القناة؟ **لا**».
- درجات الخطورة: `Critical` / `Warning` / `Info`.

---

## 8. Phase 7 — العدّادات

- `AsNoTracking` + تجميع على جانب القاعدة (بلا N+1، بلا تحميل صفوف).
- قراءة بحتة: صفر كتابة.
- القيم الصفريّة تظهر صراحةً (`0` لا فراغ) — مُثبَت على RC: قيد الانتظار 0، قيد المعالجة 0، فاشلة 0، صندوق الصادر القديم 0، أُرسِلت فعليًّا 0.

---

## 9. Phase 8 — الهوك الأماميّ

`useEmailControlStatus` في `reporting-frontend/src/lib/useEmailControl.ts`:
`staleTime: 30_000`، `refetchOnWindowFocus: true`، **بلا polling سريع**، مع حالات loading/error/refresh و`checkedAtUtc`.
الهوكات السبعة السابقة لم تُمَسّ.

---

## 10. Phase 9 — إصلاح الشاشة

`EmailControlCenterPage.tsx` صار يستهلك `EmailControlStatusPanel` (7 أقسام). **`ALLOWED_MODES` لم يُضَف إليه `Enabled`** — لأنه يخصّ وضع تسليم القالب/القاعدة لا الوضع التشغيليّ للنظام. أُزيل التعليق القديم المضلِّل والتنبيه الثابت.

---

## 11. Phase 10 — فجوة قياس تشغيل المجدول

قفل الفتحة في `ReportReminderSchedulerService` **في الذاكرة فقط** ⇒ لا يوجد تتبّع تشغيل مُخزَّن، والمسار اليدويّ يُنتج صفوفًا لا تُميَّز عن صفوف المجدول.
لذلك عُرِض `lastScheduledNotificationCreatedAtUtc` مع إخلاء مسؤوليّة صريح على الشاشة، ولم يُعرَض «آخر تشغيل للمجدول».
سُجِّلت التذكرة المستقبليّة `EMAIL-SCHEDULER-EXECUTION-TELEMETRY-R1` بحالة **REGISTERED — NOT STARTED** في
`Docs/Planning/EMAIL-SCHEDULER-EXECUTION-TELEMETRY-R1-FUTURE-TICKET-REGISTRATION.md` — **ولم يُبدأ العمل بها**.

---

## 12. Phase 11 — اختبارات Backend

`reporting-backend/tests/Reporting.IntegrationTests/EmailControlStatusTests.cs`

```
Passed! - Failed: 0, Passed: 22, Skipped: 0, Total: 22, Duration: 6 s
```

تغطّي: التخويل (Anonymous 401 / Employee 403 / Admin 200)، الأوضاع الثلاثة، اشتقاق `isLiveSendingEnabled`، جاهزيّة SMTP، `credentialConfigured` بلا سرّ، فصل السجلّ التاريخيّ، العدّادات الصفريّة، التحذيرات الثلاثة وشرط `Critical` عند Enabled+SMTP غير مهيّأ، وغياب `LastSchedulerRunAtUtc`.

---

## 13. Phase 12 — اختبارات Frontend

`reporting-frontend/src/components/EmailControlStatusPanel.test.tsx`

```
Test Files  1 passed (1)
Tests      24 passed (24)
```

المجموعة الكاملة (عدم تراجع):

```
Test Files  29 passed (29)
Tests      344 passed (344)
tsc -b     exit 0
```

---

## 14. Phase 13 — التحقّق المحليّ والسيناريوهات الثلاثة

| السيناريو | المدخلات | الناتج المُثبَت |
|---|---|---|
| **A — شبيه الإنتاج** | Mode=Enabled، Scheduler=true، SMTP=true، Legacy=false، تاريخيّ DryRun>0 | شارة `LIVE / ENABLED`، عنوان الإرسال الفعليّ، السجلّ التاريخيّ منفصل بلا تأثير على الحالة، بلا تحذير Critical |
| **B — شبيه RC** | Mode=DryRun، Scheduler=false، Legacy=false | شارة `DRY RUN`، «وضع المحاكاة مفعّل — لا يتم إرسال رسائل حقيقية»، تحذير `scheduler_disabled` |
| **C — مكسور** | Mode=Enabled، SMTP=false | تحذير **Critical**: الإرسال الفعليّ مطلوب وSMTP غير مهيّأ |

فُحِص إضافيًّا: Desktop، Mobile 375px، RTL، زرّ التحديث، Console، جسم استجابة الشبكة، غياب الأسرار، وصفر تجاوز أفقيّ.

---

## 15. Phase 14 — تجميد المرشّح

| البند | القيمة |
|---|---|
| **Candidate SHA** | `f3ee32f24323d61258ef15844f66c66adaf279df` |
| **Parent** | `ff51825d27f0ac48e784352d11fee96aad5d0155` |
| **Tree** | `7cd0d0ae9ae7aac70283b2678361ee253681da38` |
| **الرسالة** | `fix(email): surface live operational mode in Email Control Center (EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1)` |
| **commit إصلاح واحد فوق الأساس، بلا squash** | نعم |

**سطح التغيير — 11 ملفًّا، +1933 / −8:**

```
M reporting-backend/src/Reporting.Api/Controllers/EmailControlController.cs               |  15 +-
A reporting-backend/src/Reporting.Application/Notifications/EmailControlStatusModels.cs   | 171 ++++
A reporting-backend/src/Reporting.Application/Notifications/IEmailControlStatusService.cs |  12 +
M reporting-backend/src/Reporting.Infrastructure/DependencyInjection.cs                   |   1 +
A reporting-backend/src/Reporting.Infrastructure/Services/EmailControlStatusService.cs    | 228 ++++
A reporting-backend/tests/Reporting.IntegrationTests/EmailControlStatusTests.cs           | 618 ++++
A reporting-frontend/src/components/EmailControlStatusPanel.test.tsx                      | 514 ++++
A reporting-frontend/src/components/EmailControlStatusPanel.tsx                           | 288 ++++
M reporting-frontend/src/lib/useEmailControl.ts                                           |  20 +-
M reporting-frontend/src/pages/EmailControlCenterPage.tsx                                 |  17 +-
M reporting-frontend/src/types/api.ts                                                     |  57 +
```

| البند | القيمة |
|---|---|
| الحزمة | `index-Bq08cb54.js` |
| الحجم | 1,338,048 بايت |
| SHA256 | `557df8beede0e5a5e6d305b7d0aecbee3e37b9d753b5f10f18302d1422e9e344` |
| **عدد الهجرات** | **30 (بلا تغيير)** — ملفّات الهجرة في الـdiff = **0** |
| رأس الهجرات | `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| فحص الأسرار | 0 تطابق |
| إثبات عدم الكتابة | لا `Add`/`Update`/`Remove`/`SaveChanges` في مسار الحالة |
| إثبات عدم الإرسال | لا استدعاء `IEmailSender.SendAsync` في مسار الحالة |

---

## 16. Phase 15 — RC Preflight

| البند | القيمة قبل النشر |
|---|---|
| الوقت | 2026-07-29 (نافذة `20260729-200400`) |
| `/health` على 5092 | 200 |
| Environment | `ReleaseCandidate` |
| قاعدة البيانات | `reporting_rc` |
| `EmailNotifications__Mode` | `DryRun` |
| `Email__Enabled` | `false` |
| `ReportReminderScheduler__Enabled` | `false` |
| `email_outbox` | 0 |
| Pending / Processing / Failed | 0 / 0 / 0 |
| إرسال SMTP | 0 |
| Job يدويّ | لا يوجد |
| نشر آخر متزامن | لا يوجد |
| عدد الهجرات / الرأس | 30 / `20260724224053` |
| SourceLink للـbackend | (نسخة RC السابقة) |
| الحزمة الحاليّة | `index-DaDCi1OK.js` |

**النسخ الاحتياطيّة (مأخوذة فعليًّا):**
- Backend: `/opt/reporting-rc/publish-backup-emailstatus-20260729-200400`
- Frontend: `/opt/reporting-rc/frontend/dist-backup-emailstatus-20260729-200400`
- طابع النافذة: `/root/emailstatus-rc-ts.txt`
- بصمة الإعدادات مُقنَّعة: md5 لـ`/etc/khubara-reporting-rc.env` = `40c0c0bf37e9cf4970e0937a55377cbd` (أُقنِّعت المفاتيح: Password، Email__Password، Secret، Token، ConnectionString، SmtpPassword، ApiKey).

---

## 17. Phase 16 — نشر RC

- Backend: `dotnet publish -c Release` من `f3ee32f`، ثم `rsync -az --delete --exclude appsettings.Development.json` → `/opt/reporting-rc/publish` + `chown -R www-data:www-data`.
- **SourceLink على الـDLLs الأربعة = `f3ee32f24323d61258ef15844f66c66adaf279df`**، و`appsettings.Development.json` غائب.
- Frontend: بناء بـ`VITE_API_BASE_URL=/api` ثم rsync إلى `/opt/reporting-rc/frontend/dist`.
- إعادة التشغيل: **خدمة RC وحدها** `systemctl restart khubara-reporting-rc.service` عند `2026-07-29T17:08:21Z`.

| البند | بعد النشر |
|---|---|
| MainPID (RC) | 246643، NRestarts=0، active/running |
| `/health` | 200 `{"status":"ok","service":"reporting-api"}` |
| Environment | `ReleaseCandidate` |
| قاعدة البيانات | `reporting_rc` |
| عدد الهجرات / الرأس | 30 / `20260724224053` — «No migrations were applied» |
| `EmailNotifications__Mode` | `DryRun` (بلا تغيير) |
| Scheduler | `false` (بلا تغيير) |
| إرسال SMTP | `grep -ci smtp rc-api.log` = **0** |
| md5 + mtime لملفّ الإعدادات | مطابقان قبل/بعد ⇒ **صفر انزياح إعدادات** |
| **الإنتاج** | MainPID 210497، NRestarts=0 — **لم يُمَسّ** |

**سلسلة التجزئة الثلاثيّة (محليّ = قرص = HTTPS):**
`557df8beede0e5a5e6d305b7d0aecbee3e37b9d753b5f10f18302d1422e9e344` — 1,338,048 بايت — والحزمة السابقة `index-DaDCi1OK.js` ⇒ **404**.

مصيدة صلاحيّات RC (تطبيق الهجرات بدور المالك) **لم تُطلَق** لأن الباتش بلا هجرة.

---

## 18. Phase 17 — قبول RC: الفحوص الثلاثون

| # | الفحص | النتيجة |
|---|---|---|
| 1 | `GET /api/email-control/status` كمجهول ⇒ 401 | PASS |
| 2 | كموظّف ⇒ 403 | PASS |
| 3 | كمدير نظام ⇒ 200 | PASS |
| 4 | عدد حقول الـDTO = 34 | PASS |
| 5 | `lastSchedulerRunAtUtc` غير موجود | PASS |
| 6 | لا مفتاح سرّيّ في JSON | PASS |
| 7 | `mode = "DryRun"` | PASS |
| 8 | `isLiveSendingEnabled = false` | PASS |
| 9 | `environmentName = "ReleaseCandidate"` | PASS |
| 10 | `schedulerEnabled = false` | PASS |
| 11 | `smtpConfigured = false` و`credentialConfigured = false` | PASS |
| 12 | `legacyEmailEnabled = false` و`legacyFlagIsAuthoritative = false` | PASS |
| 13 | العدّادات: pending/processing/failed/outbox = 0 وتظهر صراحةً | PASS |
| 14 | التاريخيّ: total 104 / dryRun 104 / enabled 0 / sent 0 | PASS |
| 15 | التحذيرات: Warning `scheduler_disabled` + Info ×2 | PASS |
| 16 | الشارة المعروضة `DRY RUN` و`LIVE` غير ظاهرة | PASS |
| 17 | العنوان «وضع المحاكاة مفعّل — لا يتم إرسال رسائل حقيقية» | PASS |
| 18 | السطر «الوضع الحاليّ: DryRun · بيئة التشغيل: ReleaseCandidate» | PASS |
| 19 | «المجدول: معطّل» | PASS |
| 20 | «Legacy Email Flag (Email:Enabled): معطّل» + «هل هو مصدر حقيقة لهذه القناة؟ لا» | PASS |
| 21 | لا تناقض مضلِّل بين العلم القديم والوضع الحاليّ | PASS |
| 22 | السجلّ التاريخيّ تحت «أرقام تراكميّة… لا تعبّر عن الوضع الحاليّ» | PASS |
| 23 | إخلاء مسؤوليّة قياس المجدول ظاهر | PASS |
| 24 | الأقسام السبعة موجودة | PASS |
| 25 | RTL: `dir=rtl` و`lang=ar` | PASS |
| 26 | لا سرّ في DOM (`secrets = []`) | PASS |
| 27 | Console بلا أخطاء تطبيقيّة (فقط SignalR negotiate 401 = أثر الجسر) | PASS |
| 28 | زرّ «تحديث» ⇒ طلب جديد 200 و`checkedAt` يتقدّم (٠٨:١٧ م ⇐ ٠٨:١٨ م) | PASS |
| 29 | Mobile 375×812: `scrollWidth == clientWidth == 375` ⇒ صفر تجاوز أفقيّ | PASS |
| 30 | صفر كتابة بعد كلّ التفاعلات: 104 / 0 / 620 ثابتة، وSMTP = 0 | PASS |

**30/30 PASS.**

> ملاحظة منهجيّة: القبول جرى على **بايتات RC الحقيقيّة** (`index-Bq08cb54.js` المُقدَّمة من nginx على RC) عبر جسر محلّيّ قراءة-فقط، لأن BasicAuth مضبوط على مستوى الـserver في nginx (شاملًا `/api/`) ويتصادم مع ترويسة `Authorization: Bearer` للـSPA. الجسر يمرّر `/api/*` و`/health` عبر نفق SSH إلى خلفيّة RC مباشرةً (يحفظ الـBearer) ويحقن BasicAuth للأصول الساكنة فقط. لا شيء يُعاد كتابته.

---

## 19. لقطة الاستجابة الحيّة من RC

```json
{ "mode": "DryRun", "isLiveSendingEnabled": false, "schedulerEnabled": false,
  "pollMinutes": 5, "dailyDueHour": 16, "weeklyDueHour": 9, "overdueHour": 9,
  "summaryHour": 9, "reviewHour": 9, "timeZoneId": "Asia/Riyadh",
  "environmentName": "ReleaseCandidate", "smtpConfigured": false,
  "smtpHost": null, "smtpPort": null, "usesTls": true, "senderAddress": null,
  "credentialConfigured": false, "legacyEmailEnabled": false,
  "legacyFlagIsAuthoritative": false, "totalNotifications": 104,
  "historicalDryRunCount": 104, "enabledCount": 0, "sentCount": 0,
  "pendingCount": 0, "processingCount": 0, "failedCount": 0, "outboxCount": 0,
  "lastNotificationCreatedAtUtc": "2026-07-26T13:21:10.305555Z",
  "lastSentAtUtc": null, "lastFailureAtUtc": null,
  "lastScheduledNotificationCreatedAtUtc": "2026-07-26T13:21:10.305555Z",
  "checkedAtUtc": "2026-07-29T17:18:03.6898702Z",
  "warnings": [ {"severity":"Warning","code":"scheduler_disabled"},
                {"severity":"Info","code":"historical_dryrun_records"},
                {"severity":"Info","code":"legacy_flag_not_authoritative"} ] }
```

---

## 20. إثبات صفر كتابة

| العدّاد | قبل | بعد كلّ الاستدعاءات والتفاعلات |
|---|---|---|
| `email_notifications` | 104 | 104 |
| `email_outbox` | 0 | 0 |
| `audit_logs` | 620 | 620 |

---

## 21. إثبات صفر إرسال

`grep -ci smtp /var/log/reporting-rc/rc-api.log` = **0** قبل النشر وبعده وبعد القبول.
`smtpConfigured=false` على RC ⇒ المُرسِل يُخفق قبل إنشاء `SmtpClient` أصلًا.

---

## 22. إثبات صفر هجرة

- ملفّات الهجرة في diff المرشّح = 0.
- سجلّ إقلاع RC: «No migrations were applied. The database is already up to date».
- العدد 30 والرأس `20260724224053` ثابتان قبل/بعد.

---

## 23. إثبات صفر انزياح إعدادات على RC

md5 لـ`/etc/khubara-reporting-rc.env` = `40c0c0bf37e9cf4970e0937a55377cbd` و`mtime = 2026-07-29 06:58:38.516676134 +0000` — **مطابقان قبل وبعد**. لم يُفعَّل مجدول ولا وضع ولا SMTP.

---

## 24. إثبات عدم توسيع الصلاحيات

السياسة المستخدَمة هي `Policies.EmailControlManage` القائمة (Admin فقط). لم تُضَف سياسة ولا دور ولا عضويّة. مُثبَت حيًّا: 401 / 403 / 200.

---

## 25. الحسابات المؤقّتة على RC

| الحساب | الغرض |
|---|---|
| `emailstatus-uat-admin@rc.local` (`174ce5ff-4df6-4089-8f46-f9bd05d7fb1c`) | إثبات 200 وقبول الشاشة |
| `emailstatus-uat-emp@rc.local` | إثبات 403 |

أُنشئا بدور المالك عبر `sudo -u postgres psql` (لأن `reporting_rc_app` **لا يملك صلاحيّة `SET ROLE reporting_rc_owner`**)، ولم تُمَسّ أيّ حسابات RC حقيقيّة. **حُذفا في التنظيف** (القسم 30).

---

## 26. Phase 18 — جاهزيّة الإنتاج (تحضير فقط — لم يُنفَّذ أيّ نشر)

**حالة الإنتاج الآن (قراءة فقط، مؤكَّدة اليوم):**

| البند | القيمة |
|---|---|
| الخدمة | `reporting-api` — MainPID 210497، NRestarts=0، active/running |
| `/health` | 200 |
| SourceLink للـbackend | `18207480fdfb4b69d7b1a4ba50eb22bece930524` |
| الحزمة الحاليّة | `index-DaDCi1OK.js` |
| `EmailNotifications__Mode` | `Enabled` |
| `ReportReminderScheduler__Enabled` | `true` |
| `Email__Enabled` | `false` (Legacy) |
| SMTP مهيّأ | نعم |
| عدد الهجرات / الرأس | 30 / `20260724224053` |
| `email_notifications` | 188 = **139 DryRun/DryRun** + **49 Enabled/Sent** |
| `email_outbox` | 0 |

**السطح المطلوب نشره:** نفس الـ11 ملفًّا (Backend + Frontend معًا — **يُمنَع** النشر على جانب واحد فقط).

**المرشّح:** `f3ee32f24323d61258ef15844f66c66adaf279df` فوق الأساس التركيبيّ `ff51825d…` الجامع لنَسَبَي الإنتاج.

**مسارات النسخ الاحتياطيّة المطلوب أخذها قبل النشر (بطابع `<TS>`):**
- `/opt/reporting/publish-backup-emailstatus-<TS>`
- `/opt/reporting/reporting-frontend/dist-backup-emailstatus-<TS>`
- `/root/db-backups/reporting_prod-preemailstatus-<TS>.dump`

**التراجُع:**
- Backend: `rsync -az --delete /opt/reporting/publish-backup-emailstatus-<TS>/ /opt/reporting/publish/` + `chown -R www-data:www-data` + `systemctl restart reporting-api` ⇒ يعود SourceLink إلى `18207480…`.
- Frontend: `rsync -az --delete /opt/reporting/reporting-frontend/dist-backup-emailstatus-<TS>/ /opt/reporting/reporting-frontend/dist/` + `chown` ⇒ تعود الحزمة `index-DaDCi1OK.js` (بلا إعادة تشغيل).
- قاعدة البيانات: **لا تراجُع مطلوب** — بلا هجرة وبلا كتابة.

**بوّابات الصحّة بعد النشر (كلها إلزاميّة):**
1. `/health` = 200 داخليًّا وعبر HTTPS.
2. `Hosting environment: Production` و«No migrations were applied».
3. عدد الهجرات = 30 والرأس `20260724224053` بلا تغيير.
4. SourceLink على الـDLLs الأربعة = `f3ee32f…`.
5. `NRestarts = 0` وMainPID جديد واحد فقط.
6. md5 + mtime لـ`/etc/reporting-api.env` بلا تغيير.
7. `EmailNotifications__Mode=Enabled` و`ReportReminderScheduler__Enabled=true` بلا تغيير.
8. صفر صفّ بريد جديد وصفر سطر SMTP إضافيّ منسوب للنشر.
9. تجزئة الحزمة: محليّ = قرص = HTTPS، والحزمة القديمة ⇒ 404.
10. `GET /api/email-control/status`: مجهول 401، موظّف 403، Admin 200.

**العرض المتوقَّع على الإنتاج بعد النشر:**

| العنصر | القيمة المتوقَّعة |
|---|---|
| الشارة | `LIVE / ENABLED` |
| الوضع الحاليّ | `Enabled` · بيئة التشغيل: `Production` |
| المجدول | مفعّل — نبض 15 دقيقة، اليوميّة 16:00، الباقي 9:00 |
| جاهزيّة SMTP | مُهيَّأ · بيانات الاعتماد: مضبوطة |
| قيد الانتظار / المعالجة / الفاشلة / الصادر القديم | 0 / 0 / 0 / 0 |
| السجلّ التاريخيّ | إجمالي 188 · تاريخيّ DryRun 139 · إرسال فعليّ 49 · أُرسِلت فعليًّا 49 |
| Legacy Email Flag | معطّل — «هل هو مصدر حقيقة لهذه القناة؟ **لا**» |
| التنبيهات | بلا Critical؛ Info: 139 سجلًّا تاريخيًّا + العلم القديم غير حاكم |

---

## 27. معايير القبول — التقييم

| المعيار | الحالة |
|---|---|
| الأساس التركيبيّ يجمع نَسَبَي الإنتاج | ✅ |
| الأساس أخضر وحده قبل الإصلاح | ✅ |
| السبب الجذريّ مُصلَح (لا تثبيت للوضع) | ✅ |
| المسار آمن وقراءة-فقط | ✅ |
| الوضع من المصدر الحقيقيّ | ✅ |
| RC يعرض DryRun | ✅ |
| شبيه الإنتاج يعرض Enabled | ✅ |
| العلم القديم غير مضلِّل | ✅ |
| السجلّ التاريخيّ مفصول | ✅ |
| العدّادات صحيحة | ✅ |
| لا سرّ | ✅ |
| لا هجرة | ✅ |
| لا كتابة على القاعدة | ✅ |
| لا إرسال SMTP | ✅ |
| التخويل محفوظ | ✅ |
| قبول RC مكتمل | ✅ |

---

## 28. معايير الإخفاق — كلّها منتفية

بُني من develop؟ **لا**. Scheduler قديم؟ **لا**. خلط commit الدمج بالإصلاح؟ **لا**. الوضع ما زال مثبَّتًا؟ **لا**. `Email__Enabled` يقلب الحالة؟ **لا**. ظهور سرّ؟ **لا**. هجرة أُنشئت؟ **لا**. إرسال SMTP وقع؟ **لا**. تغيير إعدادات RC؟ **لا**. توسيع صلاحيات؟ **لا**. عدّادات خاطئة؟ **لا**. عرض «آخر تشغيل للمجدول» بلا مصدر موثوق؟ **لا**.

---

## 29. المخاطر المتبقّية

1. **قياس تشغيل المجدول غائب بنيويًّا** — مُعالَج بإخلاء مسؤوليّة صريح ومُسجَّل كتذكرة مستقبليّة.
2. **الصفوف التاريخيّة الـ139 على الإنتاج** — تبقى كما هي؛ الشاشة تفصلها بوضوح ولا تُعدَّل أيّ بيانات.
3. **ملفّ `rc-api.err.log`** يحوي أخطاء تاريخيّة (mtime 2026-07-16، 13 يومًا) لا علاقة لها بهذا النشر — لم يُكتب فيه شيء جديد.

---

## 30. التنظيف

- حُذف مستخدما UAT على RC وملفّا معرّفيهما.
- أُوقِف جسر المعاينة، وأُغلق نفق SSH على المنفذ 5186.
- أُزيل `/tmp/rc-proxy-emailstatus/` (بما فيه ملفّ الاعتماد `.ba`).
- أُزيل مدخل `email-status-rc-bridge` من `.claude/launch.json`.

---

## 31. القرار

**`EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 RC PASS — PRODUCTION READY`**

النشر الإنتاجيّ **لم يُنفَّذ** وينتظر تصريحًا صريحًا مستقلًّا. لم يُدَر App Password، ولم يُرسَل أيّ Recovery، ولم يُشغَّل أيّ Job يدويّ، ولم يُبدأ أيّ مسار آخر.
