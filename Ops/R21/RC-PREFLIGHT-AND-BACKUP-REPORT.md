# PROJECT360-R2.1 — تقرير بوّابة ما قبل نشر RC والنسخ الاحتياطيّة

**التاريخ:** 2026-08-18 · **النطاق:** فحص RC قراءةً فقط + أخذ نسخ احتياطيّة + خطّة استعادة.
**لم يُنشر شيء، ولم تُطبَّق أيّ هجرة، ولم تُعَد تشغيل أيّ خدمة، ولم يُغيَّر أيّ إعداد، ولم يُمَسّ الإنتاج، ولم يُنشأ أيّ التزام أو وسم.**

---

## 1) الحالة المرصودة على RC (قراءة فقط)

| المتغيّر | القيمة المقيسة |
|---|---|
| `RC_PUBLIC_URL` | `https://rc-report.emarketingacademy.net` |
| `RC_SERVICE` | `khubara-reporting-rc` · `/etc/systemd/system/khubara-reporting-rc.service` · مستخدم `www-data` |
| `RC_PORT` | `127.0.0.1:5092` (مؤكَّد بـ`ss -lntp`: 5090 إنتاج · 5091 TEST · 5092 RC — ثلاث عمليّات منفصلة) |
| `RC_ENVIRONMENT` | `ReleaseCandidate` |
| `RC_DATABASE` | `reporting_rc` · مستخدم التطبيق `reporting_rc_app` · `127.0.0.1:5432` |
| `RC_CURRENT_SHA` | `4fddc20ad23757636c54f3a5baa94fec08a84c61` (مستخرَجة من `InformationalVersion` داخل `Reporting.Api.dll`: `1.0.0+4fddc20…`) |
| `RC_BACKEND_HASH` | شجرة `publish` التجميعيّة `be9c0fec20b9a1134c7c4b396369127255fd71e3efee167099c199c38e2ffc4b` · `Reporting.Api.dll` = `85f9296c9b1d0ba040821ac04813a891dc0f03a45375e54edfc52e6b0529f42b` (86 ملفًّا · 109MB) |
| `RC_FRONTEND_HASH` | شجرة `dist` التجميعيّة `de3166e684e62397e2d6e27d972023b5366d41e102dffaf68e83f239c80bbb29` · `assets/index-ccSnFxKJ.js` = `47129d4d2d8a1ec2104fb37a5f8ccc386862fe0ac5ea37a5e4d979c06f4d06cf` (1.6MB · جذر nginx `/opt/reporting-rc/frontend/dist`) |
| `RC_MIGRATION_COUNT` | `40` صفًّا في `__EFMigrationsHistory` (38 هجرة كود + صفّا جسر النَسَب) |
| `RC_MIGRATION_HEAD` | `20260811142239_AddProject360Foundation` |
| `RC_SCHEMA_TABLES` | `78` |
| `RC_SCHEMA_COLUMNS` | `928` |
| `RC_HEALTH` | `http://127.0.0.1:5092/health = 200` · العامّ `https://rc-report.emarketingacademy.net/health = 401` **بالتصميم** (`auth_basic` على RC لا يستثني `/health`) |
| `RC_PID` | `1142569` (RSS ≈ 197MB · عمر التشغيل 1d20h) |
| `RC_RESTART_COUNT` | `NRestarts = 0` · `active (running)` منذ `2026-08-16 18:35:15 UTC` |
| `RC_DISK_FREE` | `56,151,154,688` بايت (≈52.3 GiB متاح من 96G · استخدام 46%) بعد أخذ النسخ |
| `RC_DOCUMENTS_ROOT` | `FileStorage__DocumentsRootPath=/opt/reporting-rc/storage/documents` (+ `FileStorage__EmployeeServiceFinalDocumentsPath=/opt/reporting-rc/storage/employee-service-requests/final-documents`) |
| `RC_DOCUMENTS_ROOT_PERSISTENT` | **YES** — خارج `publish` القابل للاستبدال ⟹ RC **غير مصاب** بحاجب `PROD-READINESS-01` |
| `TARGET_SHA` | `7e063b493b50ad90ba6131e47042c7cd035fb65b` |

قاعدة `reporting_rc`: 15,948,823 بايت · 36 مستخدمًا في `AspNetUsers`.

---

## 2) الفحوص الستّة المطلوبة

| الفحص | النتيجة | الدليل |
|---|---|---|
| اتّصال مستخدم التطبيق بقاعدة RC | **PASS** | `psql` بهويّة `reporting_rc_app` نجح: `reporting_rc_app@reporting_rc:5432` |
| صلاحيّة تطبيق الهجرات **دون تنفيذها** | **PASS** | `has_schema_privilege(public, CREATE)=t` · `has_database_privilege(CREATE)=t` · مالك **78/78** جدولًا هو `reporting_rc_app` · الدور غير مُتميّز (`rolsuper/rolcreatedb/rolcreaterole = false/false/false`) — **لم تُنفَّذ أيّ هجرة** |
| لا Job/Email/Scheduler يُرسل بيانات خارجيًّا | **PASS** | `EmailNotifications__Mode=DryRun` (المكبح الموثوق) · `Email__Enabled=false` · `Email__Provider=none` · `Reminders__Enabled=false` · `Scheduler__Enabled=false` · `BackgroundJobs__Enabled=false` · `Integrations__Enabled=false` · `ReportReminderScheduler__Enabled=false` — وبعد آخر إقلاع (سطر `Now listening on` رقم 121944): **0** سطر `ReportReminderScheduler ran` و**0** محاولة SMTP في السجلّ · لا مهامّ `cron` ولا `systemd timers` تخصّ RC |
| عدم اتّصال RC بقاعدة Production | **PASS** | `pg_stat_activity` لمستخدم RC: `reporting_rc` فقط (اتّصال واحد) · `grep -c reporting_prod` في ملفّ بيئة RC = **0** · `pg_hba.conf` يحوي ثلاثة أسطر `reject` صريحة لـ`reporting_rc_app → reporting_prod` (local + IPv4 + IPv6) · مالك `reporting_prod` هو `reporting_app` لا `reporting_rc_app` |
| لا ملفّات مرفوعة داخل `publish` القابل للاستبدال | **PASS** | 86 ملفًّا كلّها مصنوعات بناء (`.dll/.json/.pdb/native runtimes/LatoFont/web.config`) · مجلّدات `publish` الفرعيّة = `LatoFont` + `runtimes` فقط · شجرة التخزين المستقلّة `/opt/reporting-rc/storage/{documents,employee-service-requests,dataprotection-keys}` بها **0 ملفّ** حاليًّا |
| ملاءمة مساحة القرص للنسخ والنشر والRollback | **PASS** | المطلوب ≈47MB للنسخة + ≈110MB للنشر + هامش الاستعادة · المتاح ≈52.3 GiB (فائض >300 ضعفًا) |

---

## 3) النسخ الاحتياطيّة المأخوذة

**المجلّد:** `/opt/backups/rc-preflight-20260818T145419Z-r21` (صلاحيّات `700`) · **لم تُحذف أيّ نسخة سابقة** (`test-20260815-cpwr2r3` و`test-20260818-r21` سليمتان، وكذلك 33 مجلّد `publish-backup-*` تحت `/opt/reporting-rc`).

| الملفّ | الحجم (بايت) | SHA-256 |
|---|---|---|
| `reporting_rc.dump` (`pg_dump -Fc`) | 484,644 | `0ab3603e313a911d7b6c97c17d87cd69699abca91bb6f4296a570fd314ae005a` |
| `backend-publish.tar.gz` | 47,347,342 | `b6c0f35705ece0f1c9d8f97f4f6369138354c9f5a870dde043ad2dbba790abe5` |
| `frontend-dist.tar.gz` | 390,545 | `7f8bf9386348d35f1db6e7161f4e024a908a20017741b3ebc5744e29075b14d1` |
| `khubara-reporting-rc.env` (أسرار · `600`) | 1,428 | `1a402e54d98a0584bf0cc0122069a096ad6e34936b69fa339ec990dbcbb31729` |
| `nginx-reporting-rc.conf` | 3,086 | `2faf5fc1f642278b54696241899f53aa0a71894fa442a4b98eaa750f4cbbc514` |
| `nginx-reporting-rc-acme.conf` | 324 | `33002c3d492e737a0c24518ca9f3c43e5107d372378ad75c55a12fdcb05e62f7` |
| `htpasswd-reporting-rc` (سرّ · `600`) | 48 | `b247cf3f57d2b494786ffe395ee6390c35477a94ff310ce732d7cea140d76ddd` |
| `khubara-reporting-rc.service` | 536 | `41093b8482ad22d8faae8671a86c0ff0c936164c220dd1f7f8e7066f70d23a67` |

مرفقات المجلّد: `MANIFEST.md` (4,686B) · `SHA256SUMS` (10 مدخلات) · `ROLLBACK-STEPS.md` (5,099B). الحجم الكلّيّ 47MB.

**إثبات القراءة بلا استعادة:**
- `pg_restore --list reporting_rc.dump` ⟹ خروج **0** · 473 مدخلة فهرس · **78** `TABLE DATA` · ترويسة الأرشيف `dbname: reporting_rc` بتاريخ `2026-08-18 14:54:19 UTC`. **لم يُنفَّذ `pg_restore` على أيّ قاعدة.**
- `gzip -t` للأرشيفين ⟹ خروج **0** لكليهما · `tar -tzf` سردًا فقط: 106 مدخلة خلفيّة · 9 واجهة.
- ملفّات الإعداد الخمسة مقروءة (34 · 86 · 19 · 1 · 11 سطرًا).
- `sha256sum -c SHA256SUMS` ⟹ **10/10 OK** وخروج **0**.

**خطّة الاستعادة** (`ROLLBACK-STEPS.md`) بالترتيب المُلزِم: التحقّق من البصمات → **إيقاف خدمة RC** → **استعادة القاعدة عند الحاجة فقط** (مع نسخة سلامة قبلها) → **استعادة الخلفيّة** → **استعادة الواجهة** → **استعادة الإعدادات عند تغييرها** → **تشغيل الخدمة** → **التحقّق الصحّيّ والدخانيّ** (بما فيه دخان متصفّح إلزاميّ على `/app/...`). كلّ أوامرها مكتوبة ولم يُنفَّذ منها شيء.

---

## 4) المرشَّح المطلوب نشره (لم يُنشر)

- `TARGET_SHA = 7e063b493b50ad90ba6131e47042c7cd035fb65b` = `origin/develop` (2026-08-18 17:04:30 +0300 — «سجّل بوّابة القبول النهائيّة وإغلاق BASELINE-DEFECT-01 بأرقامها المقيسة»).
- الفارق عن المنشور على RC (`4fddc20`): **167 ملفًّا · +21,690/−243**.
- **هجرتان جديدتان** ستُطبَّقان عند النشر (تلقائيًّا عند الإقلاع):
  - `20260817101108_AddProjectProgressAndHealthStates`
  - `20260817114129_AddProjectExecutionUpdateProposals`
- الأثر المتوقَّع: `__EFMigrationsHistory` 40 ⟵ 42 صفًّا · الرأس يصير `20260817114129_AddProjectExecutionUpdateProposals` (مطابقًا لما هو منشور على TEST).
- بوّابة المرشَّح المقيسة سابقًا: تكامل **2011/2011** (خروج 0) · وحدوي 359/359 · واجهة 588/588 · UAT مستهدَف 26/26.

---

## 5) ملاحظات مرصودة (لا تُصلَح في هذه المرحلة)

| # | الملاحظة | الأثر | التصنيف |
|---|---|---|---|
| OBS-1 | `/opt/reporting-rc/storage/dataprotection-keys` **فارغ** رغم ضبط `DataProtection__KeysPath` عليه | مفاتيح حماية البيانات غير مُثبَّتة على القرص ⟹ كلّ إعادة تشغيل تُبطِل جلسات المستخدمين (إعادة تسجيل دخول) | غير حاجب — يُتوقَّع أثره أثناء النشر |
| OBS-2 | امتياز `CONNECT` على `reporting_prod` لمستخدم `reporting_rc_app` ما زال ممنوحًا على مستوى `GRANT` (`has_database_privilege = true`) | الحاجز الفعلّي هو `pg_hba` بثلاثة أسطر `reject`، والاتّصال الفعليّ الوحيد هو `reporting_rc` | غير حاجب — تصليب مُقترَح لاحقًا (`REVOKE CONNECT`) |
| OBS-3 | `rc-api.err.log` يحوي `42501: permission denied for schema public` | آثار قديمة بتاريخ `2026-08-16 17:56` (نافذة النشر قبل منح الملكيّة)، والوضع الحاليّ: 78/78 جدولًا مملوكة و`CREATE` ممنوحة | غير حاجب — تاريخيّ |
| OBS-4 | `/health` العامّ يعيد 401 على RC | مقصود (`auth_basic` لا يستثني `/health` على RC خلافًا لـTEST) — المراقبة تتمّ عبر `127.0.0.1:5092/health` | غير حاجب — بالتصميم |

**لم يُرصد أيّ حاجب (Blocker).**

---

## 6) الخلاصة

```
RC_PREFLIGHT              = PASS
RC_BACKUP                 = PASS (8 مصنوعات + MANIFEST + SHA256SUMS + ROLLBACK-STEPS · 10/10 OK)
RC_ISOLATION              = PASS (اتّصال واحد بـreporting_rc · pg_hba reject تجاه الإنتاج · 0 إشارة للإنتاج في البيئة)
RC_STORAGE                = PASS (جذر المستندات دائم خارج publish · 52.3 GiB متاح)
RC_MIGRATION_PERMISSION   = PASS (ملكيّة 78/78 + CREATE ممنوحة · لم تُنفَّذ أيّ هجرة)
ROLLBACK_READY            = YES
TARGET_SHA                = 7e063b493b50ad90ba6131e47042c7cd035fb65b
RC_READY_FOR_DEPLOYMENT   = YES (تقنيًّا — موقوف على تصريح نشر صريح جديد)

RC_DEPLOYED               = NO
RC_MIGRATIONS_APPLIED     = NO
RC_SERVICE_RESTARTED      = NO
PRODUCTION_TOUCHED        = NO
NEXT_REQUIRED_ACTION      = تصريح صريح جديد من المستخدم لنشر 7e063b4 على RC وحده (بناء حزمة الواجهة بـ VITE_API_BASE_URL=https://rc-report.emarketingacademy.net/api ثمّ tsc -b && vite build، نشر الخلفيّة، تطبيق الهجرتين تلقائيًّا عند الإقلاع، ثمّ دخان متصفّح إلزاميّ على /app/...). الإنتاج يبقى محجوبًا بـPROD-READINESS-01 واعتماد مالك المنتج.
```
