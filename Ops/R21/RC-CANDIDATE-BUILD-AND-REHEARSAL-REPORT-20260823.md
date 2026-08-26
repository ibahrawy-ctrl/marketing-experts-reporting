# PROJECT360-R2.1 — RC CANDIDATE BUILD AND ISOLATED REHEARSAL (إعادة تشغيل — 23 أغسطس 2026)

> **ملاحظة موضع الملفّ:** التقرير الأساسيّ `Ops/R21/RC-CANDIDATE-BUILD-AND-REHEARSAL-REPORT.md` يوثّق تجربة 18 أغسطس ومحفوظ في git بلا مساس. هذا الملفّ يوثّق **إعادة التشغيل بتاريخ 23 أغسطس** بقرار المستخدم: **إعادة استعمال المرشّح الموجود** + **تحقّق no-op على أحدث نسخة (42 هجرة)**. **لم يُلتزَم ولم يُدفَع أيّ شيء** (COMMIT_PUSH=NO لهذه التذكرة).

```
TARGET_SHA = 7e063b493b50ad90ba6131e47042c7cd035fb65b
```

هذه المرحلة **لا تمنح تصريح نشر حيّ على RC**. كلّ العمل على حزمة مرحليّة معزولة وقاعدة مستنسخة قابلة للحذف. RC الحيّ وTEST والإنتاج و`origin/main` لم يُمسّوا.

---

## 0) حسم فرضيّة التذكرة بالقياس (قبل أيّ عمل)

التذكرة افترضت `MIGRATIONS_BEFORE = 40` و`SCHEMA_BEFORE = 78/928`، لكنّ القياس الحيّ أثبت أنّ **المرشّح `7e063b4` مُفعَّل بالفعل على RC الحيّ** منذ نشر 22 أغسطس:

| المصدر | الهجرات | الرأس | الجداول/الأعمدة |
|---|---|---|---|
| RC الحيّ (`reporting_rc`) | **42** | `20260817114129_AddProjectExecutionUpdateProposals` | **79 / 947** |
| نسخة `rc-microclosure-20260823T102841Z` (الأحدث) | 42 (79 TABLE DATA) | — | 79 / 947 |
| نسخة `rc-predeploy-20260822T201749Z-r21` (قبل التفعيل) | 40 (78 TABLE DATA) | — | 78 / 928 |
| نسخة `rc-preflight-20260818T145419Z-r21` | 40 (78 TABLE DATA) | — | 78 / 928 |

**قرار المستخدم:** استعادة **أحدث نسخة (42)** إلى قاعدة معزولة وإثبات أنّ `MigrateAsync` **لا يطبّق شيئًا جديدًا (no-op)** — وهو التحقّق الأدقّ للحالة الراهنة. مسار الإنتاج المختلف `30→(+2 جسر)→42` مُثبَت سابقًا على استنساخ من الإنتاج في تقرير جاهزيّة الإنتاج.

---

## 1) هُويّة المرشّح وفحوص البناء (إعادة استعمال الحزمة الموجودة)

الحزمة المرحليّة: `/opt/reporting-rc/staging-r21-7e063b4-20260818` (غير مرجوعة من الخدمة أو Nginx).

```
SOURCE_SHA               = 7e063b493b50ad90ba6131e47042c7cd035fb65b
CANDIDATE_BUILD          = REUSED (بناء 18 أغسطس المتحقَّق منه؛ لا إعادة بناء بقرار المستخدم)
BACKEND_BUILD_EXIT       = 0 (مثبَت سابقًا؛ الإصدار المخبوز 1.0.0+7e063b493b50ad90ba6131e47042c7cd035fb65b)
FRONTEND_TSC_EXIT        = 0 (مثبَت سابقًا)
FRONTEND_BUILD_EXIT      = 0 (مثبَت سابقًا)
BACKEND_ARTIFACT_HASH    = 36d7f525dc0ee132a4490c08994479b0776e3e89430a7d125dcc0c56f7fc21b4
FRONTEND_ARTIFACT_HASH   = a47e07cf4328ced57075dd5af696908f29b1e880df397f6a6ac45fbf5d448b0b
FRONTEND_FILE_COUNT      = 7
BAKED_RC_API_URL_COUNT   = 1     (>=1 مطلوب — محقَّق)
LOCALHOST_API_URL_COUNT  = 0     (=0 مطلوب — محقَّق)
TEST_API_URL_COUNT       = 0     (=0 مطلوب — محقَّق)
PRODUCTION_API_URL_COUNT = 0     (=0 مطلوب — محقَّق)
```

- البصمة التجميعيّة للـpublish (`find … | sort -z | sha256sum | sha256sum`) = `36d7f525…` مطابقة للمسجَّل.
- البصمة التجميعيّة للـdist = `a47e07cf…` (7 ملفّات) مطابقة للمسجَّل.
- الإصدار المخبوز في `Reporting.Api.dll` = `1.0.0+7e063b4…` = `TARGET_SHA` حرفيًّا.
- `sha256sum -c SHA256SUMS` داخل الحزمة = **OK** (MANIFEST · ACTIVATION-PLAN · أدلّة). `MANIFEST.md` و`ACTIVATION-PLAN.md` حاضران.

---

## 2) تجربة الهجرات المعزولة (تحقّق no-op على 42)

- استعادة أحدث نسخة `rc-microclosure-20260823T102841Z/reporting_rc.dump` إلى قاعدة **`reporting_rc_iso`** (مملوكة لـ`reporting_rc_app`, `pg_restore --no-owner --role=reporting_rc_app`).

```
MIGRATION_DRY_RUN         = N/A (لا هجرات معلَّقة؛ الأساس المستعاد عند 42)
MIGRATION_APPLY_EXIT      = 0 (إقلاع MigrateAsync دون تطبيق أيّ هجرة — no-op)
MIGRATIONS_BEFORE (clone) = 42
MIGRATIONS_AFTER          = 42
MIGRATION_HEAD_AFTER      = 20260817114129_AddProjectExecutionUpdateProposals
TABLES_AFTER              = 79
COLUMNS_AFTER             = 947
DATA_COUNTS_BEFORE_AFTER  = AspNetUsers 49→49 · audit_logs 721→721 · project_execution_update_proposals 14→14 (صفر فقد)
MIGRATION_RETRY_RESULT    = مستقرّ — لا "Applying migration" ولا 42P07 ولا PostgresException في سجلّ الإقلاع
```

**إثباتات:** أسطر «Applying/Applied migration» في سجلّ الإقلاع = **0** · مطابقات `42P07|already exists|PostgresException` = **0** · البنية والبيانات بلا تغيير قبل/بعد الإقلاع (صفر فقد). لا اتصال بقاعدة RC الحيّة أو الإنتاج (انظر §3، حارس سلسلة الاتصال + جرد الاتصالات).

---

## 3) التشغيل المعزول (localhost:5099) والعزل التامّ

- مشغّل `launch.sh` يقرأ `/etc/khubara-reporting-rc.env` **سطرًا-بسطر بلا `source`** (حفاظًا على `;` في سلسلة الاتصال)، ثمّ يُبدّل اسم القاعدة إلى `reporting_rc_iso`، مع **حارس صارم** يرفض الإقلاع ما لم تكن السلسلة تحوي `Database=reporting_rc_iso;` وتخلو من `Database=reporting_rc;` (نتيجة الحارس: `has_iso=YES` · `has_live=NO`). لم يُطبَع أيّ سرّ.
- تعطيلات العزل (فوق البيئة الحيّة): `EmailNotifications__Mode=Disabled` · `Email__Enabled=false` · `BackgroundJobs__Enabled=false` · `Reminders__Enabled=false` · `ReportReminderScheduler__Enabled=false` · `Scheduler__Enabled=false` · `Integrations__Enabled=false` · `DataProtection__KeysPath` و`FileStorage__*` موجَّهة إلى `/tmp/rc-iso-rehearsal/*`.

```
HEALTH               = 200  (خلال ثانيتين؛ {"status":"ok","service":"reporting-api"})
STARTUP_ERRORS       = 0    (لا أسطر fail/error/exception/unhandled عدا لا شيء)
SMOKE_PASS           = YES  (health 200 · /api/clients unauth 401 · invalid login 401 · /hubs/notifications negotiate unauth 401)
EMAIL_DISABLED       = YES  (Mode=Disabled؛ لا نشاط SMTP في السجلّ)
SCHEDULERS_DISABLED  = YES  (Scheduler/Reminder/BackgroundJobs=false؛ لا نشاط مجدول في السجلّ)
OUTBOX_UNSENT        = 0    (email_outbox في الاستنساخ = 0 صفّ)
EXTERNAL_CALLS       = 0    (الاتصال الصادر الوحيد للعمليّة PID 1443474 = 127.0.0.1:5432 فقط؛ الاستماع على 127.0.0.1:5099 فقط)
```

> لم يُنفَّذ تسجيل دخول موثَّق موجب: كلمة مرور UAT المشتركة تُقرأ من stdin وقت التشغيل ولا تُخزَّن، وتجنّبتُ التلاعب بجداول Identity حتّى على الاستنساخ. مسار القاعدة مُثبَت مباشرةً بنجاح `MigrateAsync` (no-op) على الاستنساخ وسلامة البيانات، والسطح الأمنيّ مُثبَت بـ401 على كلّ نقطة محميّة. هذا يفي بـ«Smoke مختصر» المطلوب (بلا UAT كامل).

---

## 4) التنظيف

```
CLEANUP = DONE
```

- إيقاف العمليّة المعزولة (SIGTERM على PID 1443474) — المنفذ 5099 صار **FREE**.
- حذف **قاعدة الاستنساخ فقط** `reporting_rc_iso` (`iso_db_exists=0`) بعد إنهاء الجلسات المعلَّقة.
- حفظ سجلّ الإقلاع كدليل: `…/staging-r21-7e063b4-20260818/evidence/isolated-rehearsal-20260823-app.log` (بلا أسرار).
- إزالة دليل العمل `/tmp/rc-iso-rehearsal` (المشغّل لا يخزّن أسرارًا؛ يقرؤها من `/etc` وقت التشغيل).
- **الإبقاء على** الحزمة المرحليّة + `MANIFEST.md` + `ACTIVATION-PLAN.md` + `SHA256SUMS` + كلّ النسخ الاحتياطيّة (لا حذف نسخ).

---

## 5) إثبات عدم مسّ البيئات الحيّة

| العنصر | قبل | بعد |
|---|---|---|
| RC الحيّ `reporting_rc` الهجرات | 42 / `AddProjectExecutionUpdateProposals` / 79 / 947 | **42 / نفسه / 79 / 947** |
| خدمة `khubara-reporting-rc` | active · NRestarts=0 · MainPID 1385019 | **active · NRestarts=0 · MainPID 1385019** |
| صحّة RC الحيّ (127.0.0.1:5092) | — | **200** |
| Nginx / ملفّ البيئة / الدليل النشط | لم يُوجَّه ولم يُعدَّل | **لم يُمسّ** |
| TEST · Production · `origin/main` | — | **لم تُمسّ** |

---

## الخلاصة النهائيّة

```
TARGET_SHA = 7e063b493b50ad90ba6131e47042c7cd035fb65b
CANDIDATE_BUILD = REUSED (existing verified build; backend 36d7f525 / frontend a47e07cf / 7 files)
ARTIFACT_IDENTITY = VERIFIED (aggregate hashes match; baked version = TARGET_SHA; BAKED_RC=1 / LOCALHOST=0 / TEST=0 / PROD=0; SHA256SUMS OK)
ISOLATED_RESTORE = PASS (latest backup restored to reporting_rc_iso: 42 migrations / 79 tables / 947 columns)
ISOLATED_MIGRATIONS = NO-OP (42→42, head unchanged, zero 42P07, zero data loss)
ISOLATED_HEALTH = 200
ISOLATED_SMOKE = PASS (health 200; unauth 401 on clients/login/SignalR; email+schedulers disabled; outbox 0; external calls 0)
CLEANUP = DONE (isolated process stopped; clone DB dropped; staging+backups kept; /tmp removed)
LIVE_RC_TOUCHED = NO
PRODUCTION_TOUCHED = NO
RC_CANDIDATE_READY_FOR_ACTIVATION = ALREADY ACTIVE (المرشّح مُفعَّل على RC الحيّ منذ 22 أغسطس؛ RC الحيّ = 42/79/947 = المرشّح نفسه)
NEXT_REQUIRED_ACTION = تصريح منفصل لتفعيل المرشّح على RC الحيّ — غير مطلوب فعليًّا لأنّه مُفعَّل بالفعل؛ الخطوة المفتوحة الحقيقيّة هي تصريح نشر الإنتاج المنفصل
```
