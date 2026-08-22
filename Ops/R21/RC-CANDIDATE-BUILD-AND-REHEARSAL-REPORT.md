# PROJECT360-R2.1 — تقرير بناء مرشّح RC والتجربة المعزولة

**التاريخ:** 2026-08-18 · **النطاق:** بناء المرشّح + تجربة معزولة فقط.
**لم يُلمَس RC الحيّ ولا الإنتاج ولا TEST، ولم يُعدَّل Nginx ولا ملفّ البيئة، ولم يُنشأ التزام أو وسم.**

---

## 1) بناء المرشّح

| المتغيّر | القيمة |
|---|---|
| `SOURCE_SHA` | `7e063b493b50ad90ba6131e47042c7cd035fb65b` (worktree منفصل نظيف `/tmp/rc-cand-7e063b4` · `git status` نظيف) |
| `BACKEND_BUILD_EXIT` | **0** (`dotnet publish -c Release` · 86 ملفًّا · 109MB · محمول مطابق لنمط النشر الحاليّ) |
| `FRONTEND_TSC_EXIT` | **0** (`npx tsc -b` — البوّابة الصحيحة لا `vite build` وحده) |
| `FRONTEND_BUILD_EXIT` | **0** (`npx vite build` بعد `npm ci` بخروج 0) |
| `BACKEND_ARTIFACT_HASH` | شجرة `publish` = `36d7f525dc0ee132a4490c08994479b0776e3e89430a7d125dcc0c56f7fc21b4` · `Reporting.Api.dll` = `892250af835eaa9f395a33cf188ebdf703ef39eb9ee33f077c09ddfe057651e9` |
| `FRONTEND_ARTIFACT_HASH` | شجرة `dist` = `a47e07cf4328ced57075dd5af696908f29b1e880df397f6a6ac45fbf5d448b0b` · `assets/index-DwS6KfdO.js` = `ba7f6b230e7cd7579ab18f1d33a7c59e5d6db9eab2c460204f243c9310f32412` · `assets/index-BejvRoEu.css` = `b209606d08842372e0d5c419f7d714372fc233d6084fe573a88af9dabdf79e3d` |
| `FRONTEND_FILE_COUNT` | **7** (`index.html`, `favicon.svg`, `icons.svg`, `logo-arabic.png`, `logo-mark.png`, `assets/*.js`, `assets/*.css`) |
| `BAKED_RC_API_URL_COUNT` | **1** (`https://rc-report.emarketingacademy.net/api`) ✅ ≥ 1 |
| `LOCALHOST_API_URL_COUNT` | **0** (لا `localhost:5090` ولا أيّ `localhost:<port>` في الحزمة) ✅ |
| `TEST_API_URL_COUNT` | **0** (لا `test.emarketingacademy.net`) ✅ |
| `PRODUCTION_API_URL_COUNT` | **0** (لا `reports.emarketingacademy.net`) ✅ |

بصمة الكود المخبوزة داخل الحزمة: **`1.0.0+7e063b493b50ad90ba6131e47042c7cd035fb65b`** — مطابقة لـ`TARGET_SHA` حرفيًّا.

**الدليل المرحليّ الجديد:** `/opt/reporting-rc/staging-r21-7e063b4-20260818` (157MB) — **لا تشير إليه الخدمة ولا Nginx** (`root /opt/reporting-rc/frontend/dist` و`ExecStart …/opt/reporting-rc/publish/…` بلا تغيير · `mtime` ملفّ Nginx `2026-08-16 18:24:39` وملفّ البيئة `2026-08-16 18:34:17` كما كانا).
بصمتا الشجرتين على الخادم بعد الرفع **مطابقتان حرفيًّا** لبصمتيهما على جهاز البناء.

---

## 2) تجربة الهجرات المعزولة

القاعدة المستنسخة `reporting_rc_rehearsal` أُنشئت من نسخة `/opt/backups/rc-preflight-20260818T145419Z-r21/reporting_rc.dump` (`pg_restore` خروج **0**) — **لم تُلمَس `reporting_rc` الحيّة ولا `reporting_prod`**.

| المتغيّر | القيمة |
|---|---|
| `MIGRATIONS_BEFORE` | **40** · الرأس `20260811142239_AddProject360Foundation` |
| `SCHEMA_BEFORE` | **78 جدولًا / 928 عمودًا** ✅ مطابق للمطلوب |
| `MIGRATION_DRY_RUN` | سكربت `--idempotent` مُولَّد (`migration-dryrun.sql` · 134 سطرًا): **4 `ADD COLUMN`** (`ProgressCalculatedAtUtc`, `ProgressMode`, `ProgressSourceDeliverableCount` على `projects` · `WeightPercentage` على `project_deliverables`) + **جدول واحد** `project_execution_update_proposals` + **5 فهارس** — **صفر `DROP`/`TRUNCATE`/`DELETE`** ⟹ إضافيّ بحت |
| `MIGRATION_APPLY_EXIT` | **0** (طُبِّقتا بنفس آليّة الإنتاج: `MigrateAsync()` عند إقلاع الحزمة — السجلّ يُظهر `Applying migration '20260817101108_AddProjectProgressAndHealthStates'` ثمّ `'20260817114129_AddProjectExecutionUpdateProposals'`) |
| `MIGRATIONS_AFTER` | **42** ✅ |
| `MIGRATION_HEAD_AFTER` | **`20260817114129_AddProjectExecutionUpdateProposals`** ✅ |
| `TABLES_AFTER` | **79** (+1) |
| `COLUMNS_AFTER` | **947** (+19: 4 أعمدة الجداول القائمة + 15 عمود الجدول الجديد) |

### `DATA_COUNTS_BEFORE_AFTER` — صفر فقدان

| الجدول | قبل | بعد الهجرة | بعد إعادة التشغيل |
|---|---|---|---|
| `AspNetUsers` | 36 | 36 | 36 |
| `projects` | 32 | 32 | 32 |
| `project_deliverables` | 0 | 0 | 0 |
| `clients` | 8 | 8 | 8 |
| `report_submissions` | 39 | 39 | 39 |
| `kpi_evaluations` | 4 | 4 | 4 |
| `report_templates` | 41 | 41 | 41 |
| `email_outbox` | 0 | 0 | 0 |
| `project_execution_update_proposals` | — (غير موجود) | 0 (جديد فارغ) | 0 |

الأعمدة الأربعة الجديدة موجودة فعلًا بعد التطبيق: `ProgressCalculatedAtUtc, ProgressMode, ProgressSourceDeliverableCount, WeightPercentage`.

### `MIGRATION_RETRY_RESULT` — لا أثر إضافيّ

1. **إعادة تنفيذ السكربت الإدمبوتنت** على القاعدة نفسها ⟹ خروج **0** · النتيجة ثابتة **42/79/947**.
2. **إعادة إقلاع الحزمة** ⟹ **0** سطر `Applying migration` · **0** خطأ · صحّة **200** · النتيجة ثابتة **42/79/947** وكلّ العدادات كما هي.

### إثبات العزل عن RC الحيّة والإنتاج

- المقبس الشبكيّ الوحيد للعمليّة المعزولة: `127.0.0.1:33816 → 127.0.0.1:5432` (لا شيء غيره).
- `pg_stat_activity` (رؤية superuser) وقت التجربة: `reporting_rc_rehearsal|reporting_rc_app|1` فقط لهذه العمليّة · اتّصالات `reporting_prod` تخصّ `reporting_app` (خدمة الإنتاج) ولم تُلمَس.
- `pg_hba` يرفض أصلًا `reporting_rc_app → reporting_prod` بثلاثة أسطر.

---

## 3) التشغيل المعزول (المنفذ 5099 على `127.0.0.1` فقط)

| المتغيّر | النتيجة |
|---|---|
| `HEALTH` | **200** (`http://127.0.0.1:5099/health`) — وبعد إعادة التشغيل **200** أيضًا |
| `STARTUP_ERRORS` | **0** (`grep -c '^fail:|Unhandled exception'` = 0 في كلا الإقلاعين؛ تحذيرات EF المعتادة عن `global query filter` فقط) |
| `SMOKE_PASS` | **PASS** — `/health = 200` · `/api/projects` بلا اعتماد = **401** (الحماية نافذة) · محاولة دخول بكلمة سرّ خاطئة = **401** (خطّ Identity والقاعدة يعملان) · `/swagger/index.html` = **404** (معطّل خارج التطوير) · مسار غير موجود = **404** |
| `EMAIL_DISABLED` | **YES** — `EmailNotifications__Mode=Disabled` · `Email__Enabled=false` · `Email__Provider=none` · **0** سطر SMTP/MailKit في السجلّ |
| `SCHEDULERS_DISABLED` | **YES** — `Scheduler__Enabled=false` · `Reminders__Enabled=false` · `BackgroundJobs__Enabled=false` · `ReportReminderScheduler__Enabled=false` · `Integrations__Enabled=false` · **0** سطر `ReportReminderScheduler ran` |
| `OUTBOX_UNSENT` | **0** (`email_outbox = 0` صفًّا قبل التجربة وبعدها — لم تُنشأ رسالة واحدة) |
| `EXTERNAL_CALLS` | **0** (لا مقبس صادر غير `127.0.0.1:5432`) |

لم يُنفَّذ UAT كامل في هذه المرحلة — بحسب نطاق التذكرة.

---

## 4) التنظيف

- العمليّة المعزولة **موقوفة**: `port 5099 listeners = 0` · `pgrep -f staging-r21-7e063b4 = 0`.
- قاعدة التجربة **محذوفة**: `dropdb reporting_rc_rehearsal` ⟹ القواعد المتبقّية `reporting_prod`, `reporting_rc`, `reporting_test_rc`, `reporting_test_uat` (كما كانت).
- **لم تُحذف أيّ نسخة احتياطيّة**: `/opt/backups/{rc-preflight-20260818T145419Z-r21, test-20260815-cpwr2r3, test-20260818-r21}` سليمة (11 ملفًّا في نسخة RC).
- الحزمة المرحليّة **محفوظة** مع `MANIFEST.md` و`ACTIVATION-PLAN.md` و`SHA256SUMS` (9 مدخلات · `sha256sum -c` = **9/9 OK**، خروج 0) وسجلّات `evidence/`.
- ملفّ البيئة المعزول وسكربت تصديره (يحويان سرّ القاعدة) **مُحيا بـ`shred`**؛ فحص الدليل كلّه ⟹ **0** ملفّ يحوي كلمة السرّ.
- **RC الحيّ بعد كلّ ما سبق:** `active` · `MainPID=1142569` (لم يتغيّر) · `NRestarts=0` · صحّة `200` · `Reporting.Api.dll = 85f9296c…` (كما كان) · واجهة `index-ccSnFxKJ.js` (كما كانت).

---

## 5) ملاحظات ودروس مرصودة

| # | الملاحظة | الحالة |
|---|---|---|
| L-1 | أرشيف `tar` من macOS يُدخِل ملفّات `._*` (AppleDouble) عند فكّه على لينكس ⟹ 191 ملفًّا بدل 86. أُعيد التحزيم بـ`COPYFILE_DISABLE=1 --no-xattrs --no-mac-metadata` وأُعيد الرفع، وتُحقِّق تطابق البصمة التجميعيّة بين الجهاز والخادم. **درس مُلزِم لأيّ نشر لاحق.** | عولج |
| L-2 | تحميل ملفّ بيئة بـ`source` في bash يكسر `ConnectionStrings__Default` لاحتوائه `;` (تُفسَّر فاصل أوامر) ⟹ فشل الإقلاع الأوّل بـ`No password has been provided`. الحلّ: توليد سطور `export KEY='VALUE'` مقتبسة. `systemd` لا يعاني هذا (يقرأ `EnvironmentFile` بنفسه). السجلّ محفوظ في `evidence/isolated-run-attempt1-failed.log`. | عولج |
| L-3 | `dataprotection-keys` فارغ على RC ⟹ تفعيل المرشّح سيُبطِل جلسات المستخدمين (إعادة تسجيل دخول). | غير حاجب — مُدرَج في خطّة التفعيل |
| L-4 | **حادثة انكشاف سرّ:** أثناء طباعة ملفّ البيئة المعزول للتحقّق، ظهرت **كلمة سرّ قاعدة `reporting_rc`** نصًّا في مخرجات الجلسة (قناع الإخفاء طابق أسماء المفاتيح لا القيم داخل سلسلة الاتّصال). لم تُكتب في أيّ ملفّ من ملفّات التقرير أو الحزمة. **التوصية: تدوير كلمة سرّ `reporting_rc_app` — ويحتاج تصريحًا صريحًا لأنّه تغيير على قاعدة/إعداد حيّ.** | مفتوح — بانتظار قرار |

---

## 6) الخلاصة

```
TARGET_SHA                          = 7e063b493b50ad90ba6131e47042c7cd035fb65b
CANDIDATE_BUILD                     = PASS (backend exit 0 · tsc exit 0 · vite exit 0)
ARTIFACT_IDENTITY                   = PASS (1.0.0+7e063b49… · RC API URL=1 · localhost/TEST/PROD=0 · بصمة الشجرتين مطابقة بين الجهاز والخادم)
ISOLATED_RESTORE                    = PASS (40 هجرة · 78/928 مطابقة للأصل)
ISOLATED_MIGRATIONS                 = PASS (42 هجرة · الرأس AddProjectExecutionUpdateProposals · 79/947 · صفر فقدان بيانات · إعادة التطبيق بلا أثر)
ISOLATED_HEALTH                     = PASS (200 · صفر خطأ إقلاع)
ISOLATED_SMOKE                      = PASS (401 للمحمي · 401 لدخول خاطئ · 404 لسواجر · 404 لمسار مجهول · بريد ومجدولات صفر · مكالمات خارجيّة صفر)
CLEANUP                             = PASS (العمليّة موقوفة · قاعدة التجربة محذوفة · لا نسخة احتياطيّة حُذفت · الحزمة والبيان والبصمات محفوظة · الأسرار مُمحاة)
LIVE_RC_TOUCHED                     = NO
PRODUCTION_TOUCHED                  = NO
RC_CANDIDATE_READY_FOR_ACTIVATION   = YES
NEXT_REQUIRED_ACTION                = تصريح منفصل لتفعيل المرشّح على RC الحيّ (وفق /opt/reporting-rc/staging-r21-7e063b4-20260818/ACTIVATION-PLAN.md) + قرار بشأن تدوير كلمة سرّ reporting_rc_app (L-4)
```
