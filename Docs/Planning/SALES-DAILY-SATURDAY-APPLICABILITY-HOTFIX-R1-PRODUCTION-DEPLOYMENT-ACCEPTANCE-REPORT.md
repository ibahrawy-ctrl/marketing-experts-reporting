# SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1 — تقرير النشر والقبول على الإنتاج

> **الحالة:** منشور ومقبول على **Production** (25 يوليو 2026، ~23:30 UTC، Backend فقط، code-only بلا migration) بتفويض مستقل صريح. **البريد ظلّ متوقفًا (DryRun) والمجدول المسمّى معطّلًا طوال العمل.** نُشر من المرشّح المقبول على RC حصرًا (لا من develop).

## 1. الهدف
فتح تقرير السبت اليومي لموظّفي المبيعات (`SALES_B2B` / `SALES_B2C`) ابتداءً من W31، بحيث:
- يُحتسَب السبت `2026-07-25` يومًا **متوقَّعًا/مُلتزَمًا** لهذين المسمّيين فقط ⇒ الالتزام من السبت إلى الخميس (الجمعة وحدها محجوبة).
- `ExpectedDailyDaysPerWeek = 6` للمبيعات من الأرضية؛ الأسابيع الأقدم تبقى 5 (لا رجعية).
- سبوت التاريخ (قبل `2026-07-25`) لا تُعاد تصنيفها Missing/Overdue وتبقى Actual.

## 2. المرشّح المنشور والمصدر
| الحقل | القيمة |
|---|---|
| Commit | `459f60e278105b9a08563a17a6e59d15639ead54` |
| Parent | `21d397d91eb93814ee21b566faf4f210e328c03b` |
| Tree | `2861c47f1d314166c3ec8aae38c943e8046e3196` |
| Migration | **لا شيء** (code-only) |
| النسخة المعزولة | `/private/tmp/release-sat-applicability-r1-20260726-012930` (detached HEAD على 459f60e، `git status` نظيف) |
| منشأ النشر | النسخة المعزولة حصرًا — **لم يُبنَ/يُنشَر من develop** |

## 3. بوابة ما قبل النشر (قراءة فقط) — اجتازت
- الأساس الفعليّ على الإنتاج قبل النشر = commit `1.0.0+21d397d91eb93814...` (= Parent، مطابق للشرط).
- migration head على الإنتاج = `20260724224053_AddReportApproverAndKpiReviewerOverrides` (30 هجرة) — مطابق للشرط.
- علامات السبت غائبة عن DLL قبل النشر (كما هو متوقَّع).
- البوابات قبل النشر: `Email__Enabled=false`، `EmailNotifications__Mode=DryRun`، `ReportReminderScheduler__Enabled=false`.

## 4. النسخ الاحتياطية (TS=`20260725-233003`)
- DB: `/root/db-backups/reporting_prod-presat-20260725-233003.dump` (922770 bytes).
- Backend: `/opt/reporting/publish-backup-presat-20260725-233003`.
- Env: `/etc/reporting-api.env.bak-presat-20260725-233003`.
- TS مخزَّن: `/root/sat-prod-deploy-ts.txt`.

## 5. النشر (Backend فقط، بلا migration)
- `dotnet publish -c Release` من النسخة المعزولة ⇒ `publish-sat`.
- `rsync -az --delete --exclude appsettings.Development.json` → `/opt/reporting/publish` + `chown -R www-data:www-data` + `systemctl restart reporting-api.service`.
- سجلّ الإقلاع: **«No migrations were applied. The database is already up to date»** + «Hosting environment: Production».
- **الـenv لم يُمَسّ** (مطابق للنسخة الاحتياطية = ENV_IDENTICAL) ⇒ البوابات الثلاث بقيت كما هي.

## 6. إثباتات ما بعد النشر (الشرط #9)
| # | الإثبات | النتيجة |
|---|---|---|
| 1 | الخدمة نشطة | `systemctl is-active reporting-api` = **active** |
| 2 | health داخليّ + عام | كلاهما **200** |
| 3 | لا هجرة طُبِّقت | «No migrations were applied»؛ migration head = `20260724224053` بلا تغيير |
| 4 | الالتزام المنشور | DLL = `1.0.0+459f60e278105b9a08563a17a6e59d15639ead54` (مطابق) |
| 5 | علامات السبت في DLL | `SaturdayEnabledForJobRole` / `SalesSaturdayApplicabilityFloor` موجودة |

## 7. UAT إنتاجي قراءة-فقط (الشرط #10 — بلا إنشاء بيانات تجريبية)
تاريخ الخادم وقت التنفيذ: **2026-07-25 23:37 UTC (السبت)** — أول يوم في W31.

### 7.1 قاعدة الإنتاج (psql قراءة فقط، `reporting_prod`)
- مسمّيات المبيعات وموظّفوها النشطون: `SALES_B2B` = 1، `SALES_B2C` = 4 (إجمالي 5 مندوبين يوميّين حقيقيين).
- قالبا المبيعات اليوميّان (`تقرير مبيعات B2B` / `تقرير مبيعات B2C`) = Primary + IsActive + منشور، مربوطان بمسمّيَيهما ⇒ يُلتقطان في `ExpectedReportersAsync(Daily)`.
- **السبت التاريخي `2026-07-18`**: 4 تسليمات يومية (2 `Closed` + 2 `Returned`) — **سليمة، غير محذوفة ولا معدَّلة** (الرفع الطوعي يوم السبت كان مسموحًا دائمًا).
- `2026-07-25`: **0 تسليم** (لم تُنشأ أي بيانات تجريبية).
- `email_outbox` = 0/0؛ migration head = `20260724224053`.

### 7.2 نقاط النهاية المصادَقة (break-glass admin، التوكن لم يُطبع، عبر 127.0.0.1:5090)
| # | الإثبات | النتيجة |
|---|---|---|
| A | **السبت متوقَّع للمبيعات** — `sales-daily-compliance?weekKey=2026-W31` | 200؛ النطاق `governance`؛ 5 مندوبين؛ **كلّ مندوب `expectedDays=1`** واليوم الوحيد المنقضي في W31 هو السبت `2026-07-25` ⇒ الـ«1» **هو السبت نفسه محتسَبًا متوقَّعًا**. (بدون الإصلاح لكان `expectedDays=0`.) `submitted=0/missing=1` للجميع — لا تسليم (لا بيانات تجريبية) |
| B | **لا رجعية — W30 يبقى 5** — `?weekKey=2026-W30` (منقضٍ كاملًا) | 200؛ **`expectedDays=5` للخمسة** = الأحد→الخميس فقط ⇒ **السبت `07-18` (قبل الأرضية) والجمعة `07-24` مستبعدان**؛ مروة وهيب `submitted=5` complete (تسليماتها التاريخية محتسَبة، غير مُعاد تصنيفها) |
| C | **لا رجعية للأقدم — W29** — `?weekKey=2026-W29` | 200؛ `expectedDays=5` للخمسة (الأحد→الخميس) |
| D | **الجمعة محجوبة وغير متوقَّعة** | مثبَت بنيويًّا: W30 المنقضي كاملًا = 5 بالضبط (الأحد→الخميس)؛ لو حُسبت الجمعة `07-24` لكان 6. السياسة `IsDailyExpectedBusinessDay(Friday)=false` دائمًا (منشورة في DLL). بوابة الرفع `IsDailySubmissionBlockedDay`=الجمعة فقط (بلا تغيير) |
| E | **W31 = 6 للمبيعات (سبت→خميس)** | بنيويًّا: السبت `07-25` محتسَب (إثبات A) + الأحد→الخميس = 5 (إثبات B) − الجمعة مستبعدة ⇒ الأسبوع الكامل = **6**. المنقضي حاليًّا = 1 (السبت وحده، لأن اليوم = السبت) |
| F | **غير-المبيعات غير متأثّرين** | تقرير الالتزام مبيعاتيّ حصريًّا بالبناء (`ExpectedReportersAsync(Daily)` = مسمّيات المبيعات فقط)؛ المسار الأسبوعي `missing-reports?weekKey=2026-W31` = 200 (23 صفًّا) سليم؛ `SaturdayEnabledForJobRole(non-sales)=false` (السبت عطلة لغيرهم) منشورة |
| G | حارس صيغة الأسبوع | `?weekKey=BAD` = **400** `report_calendar.week_format_invalid` |

## 8. عدم إرسال البريد / عدم تشغيل المجدول (الشرط #7/#9)
- `Email__Enabled=false`، `EmailNotifications__Mode=DryRun`، `ReportReminderScheduler__Enabled=false` — البوابات الثلاث المطلوبة صحيحة (الـenv لم يُمَسّ).
- `email_outbox` = **0/0** (أُعيد تأكيده مرّتين).
- إشعارات `submission.reminder` المُنشأة منذ `2026-07-25` = **0** (آخر التذكيرات من `2026-07-23`، قبل النشر) ⇒ لا نشاط تذكير نتج عن النشر ولا أيّ إرسال بريد.
- **ملاحظة شفافية:** `Reminders__Enabled=true` = خدمة `SubmissionReminderService` القديمة (إشعارات داخل التطبيق فقط، لا بريد) — **حالة إنتاج سابقة للنشر** (ظاهرة في PID القديم `67366` الساعة 22:15 UTC قبل نشري 23:30)، الـenv لم يُغيَّر، ولم تُنشئ أيّ إشعار يوم النشر (0). أسطر السجلّ المتعلّقة كلها استعلامات `SELECT` على `email_templates`/`email_rules` (تقييم لا إرسال)؛ **لا SMTP ولا إرسال فعليّ**. المجدول المسمّى في تفويض النشر (`ReportReminderScheduler`) معطّل ولم يُنتِج شيئًا.

## 9. عدم المساس
لم يُمَسّ: KPI (evaluations/ComputeScore/Templates)، Dashboard، ScopeResolver، Workflow/CurrentApproverId، قواعد الأسبوعي، بوابة رفع السبت/الجمعة (الجمعة تبقى محجوبة، السبت قابل للرفع طوعًا)، ManagerId/TeamId/DepartmentId، بيانات/قوالب/تسليمات التقارير الحقيقية، البريد (ظلّ DryRun). لا رجعية قبل `2026-07-25`. لم تُنشأ/تُعدَّل/تُحذَف أيّ بيانات إنتاجية (UAT قراءة فقط بالكامل). سبت `07-18` التاريخي سليم.

## 10. التنظيف
كل سكربتات الـUAT وملفات RC المؤقتة على الخادم (`/tmp/*sat*`, `.mjs`, `.sql`) أُزيلت (المتبقّي = 0). لا بيانات تجريبية على الإنتاج.

## 11. Rollback
- **الكود:** استعادة `/opt/reporting/publish-backup-presat-20260725-233003` → `/opt/reporting/publish` + `chown www-data` + `systemctl restart reporting-api` (لا هجرة لعكسها — code-only).
- **قاعدة البيانات (طارئ فقط):** استعادة `/root/db-backups/reporting_prod-presat-20260725-233003.dump`.
- **البيئة:** `/etc/reporting-api.env.bak-presat-20260725-233003` (مطابقة للحالية — الـenv لم يُمَسّ).

## 12. الحالة النهائية
- **Production: منشور ومقبول.** كل شروط الشرط #9 مثبتة، وUAT قراءة-فقط (#10) اكتمل دون أيّ بيانات تجريبية.

سلسلة القبول:
`تم نشر SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1 على Production — أصبحت التقارير اليومية لمبيعات B2B وB2C مطلوبة من السبت إلى الخميس ابتداءً من W31، مع بقاء الجمعة مستبعدة والدورات التاريخية دون تغيير، والبريد ما زال متوقفًا على DryRun.`
