# تقرير نشر TEST وبوّابة الدخان — `PROJECT360-R2.1`

**التاريخ:** 2026-08-18 (UTC) · **النطاق:** بيئة TEST وحدها.
`TARGET_SHA = 7e063b493b50ad90ba6131e47042c7cd035fb65b` · `TEST_URL = https://test.emarketingacademy.net/` · `TEST_API_URL = https://test.emarketingacademy.net/api`
**لم يُلمس RC ولا الإنتاج ولا git.** لا سرّ واحد في هذا التقرير.

---

## 1) لقطة ما قبل النشر

| المتغيّر | القيمة |
|---|---|
| `TEST_CURRENT_SHA` | `1.0.0+f9bca63e5b2f2564a06a729d6059b4ed3002e94b` |
| `TEST_BACKEND_HASH` | `0c517e6604cce6c9d5e6b048c32c87a8eeb7952cfb257a59015ad3404ee70adf` (86 ملفًّا) |
| `TEST_FRONTEND_HASH` | `12a5d309f2b7a88ba42c8f6931719e7a433d25e63a69810335729ea91fad47d5` (7 ملفّات · `index-D7JHWCts.js`) |
| `TEST_MIGRATION_COUNT` | `40` |
| `TEST_MIGRATION_HEAD` | `20260817114129_AddProjectExecutionUpdateProposals` |
| `TEST_TABLE_COUNT` | `79` |
| `TEST_COLUMN_COUNT` | `947` |
| `TEST_HEALTH` | `200` (محلّيّ `127.0.0.1:5091` والعامّ `https://test.emarketingacademy.net/health`) |
| `TEST_PID` | `1235735` |
| `TEST_RESTART_COUNT` | `0` (نشط منذ `2026-08-17 22:38:22 UTC`) |
| `TEST_DOCUMENTS_ROOT` | `/var/lib/reporting-test/documents` (`750 www-data:www-data` · 23 ملفًّا · **خارج `publish`**) |
| `TEST_EMAIL_MODE` | `EmailNotifications__Mode=DryRun` + `Email__Enabled=false` |
| `TEST_SCHEDULERS` | `Reminders__Enabled=false` |

`TEST_DATA_COUNTS` (قبل): `AspNetUsers=17` · `audit_logs=522` · `client_document_versions=23` · `client_documents=17` · `clients=8` · `email_outbox=0` · `kpi_evaluations=0` · `project_execution_update_proposals=3` · `project_kpi_readings=12` · `project_workstreams=8` · `projects=10` · `refresh_tokens=1425` · `report_submissions=13` · `report_template_versions=46` · `report_templates=34` · `workstream_deliverables=2`.

**تأكيد القاعدة والعزل:** `Database=reporting_test_uat` · `Username=reporting_test_uat_app` · إشارات RC في ملفّ البيئة = **0** · إشارات الإنتاج = **0** · اتّصالات العمليّة الخارجيّة الوحيدة `127.0.0.1:5432`.

---

## 2) النسخ الاحتياطيّة الجديدة

`/opt/backups/test-predeploy-20260818T160622Z-r21` (`700 root:root`) — **لم تُحذف أيّ نسخة سابقة** (`rc-preflight-20260818T145419Z-r21` · `test-20260815-cpwr2r3` · `test-20260818-r21` باقية كما هي).

| الملفّ | الحجم | SHA-256 |
|---|---|---|
| `reporting_test_uat.dump` (`600`) | 467,163 | `598694c62c402354dac8259f22a376ebbd2a2f6bb4f78396055b89a25445d388` |
| `backend-publish.tar.gz` | 47,396,826 | `1eb8bd7828fac9ccc963ac0158acb71178b64da98e21af7f3ce59938e6c240c6` |
| `frontend-dist.tar.gz` | 394,529 | `58a22091c8931f70c42083029e1da5ec2903cf3cef3b081660edae93f363a40d` |
| `documents-store.tar.gz` | 9,199,212 | `78b0019ec1aa36564aa414c36607aedfd965892a012f9ae593efa8205570dca0` |
| `khubara-reporting-test.env` (`600`) | 1,207 | `263a07ff7257049fe893dc2a56465ba09a7eb42f0d2b4c46bfdeb4843c3c7e28` |
| `khubara-reporting-test.service` | 391 | `6870055f50b8cb7d0a1ece78e63fb9131c3a37946f20a39872ad702b2693f554` |
| `nginx-reporting-test.conf` | 2,842 | `2b84642ebf7f5a3041c16e493b1965310369a59562a8573ddd5d7d6c7b5f8808` |
| `htpasswd-rc-test` (`600`) | 95 | `d81db4963053f88aa5933c204663c8d71cb50303d1ca3f9933d5d5fc1a60b905` |

+ `MANIFEST.md` · `ROLLBACK-STEPS.md` · `SHA256SUMS`.

**التحقّق:** `sha256sum -c` = **10/10 OK** (خروج 0) · `pg_restore --list` = **482 مدخل TOC** بلا استعادة · `gzip -t` = 0 للأرشيفات الثلاثة · `tar -tzf` = 106 و9 و… مدخلًا.

---

## 3) الحزم

### الخلفيّة — إعادة استخدام مُبرَّرة (بلا إعادة بناء)

| الشرط | الإثبات |
|---|---|
| `Embedded SHA = 7e063b4` | `1.0.0+7e063b493b50ad90ba6131e47042c7cd035fb65b` من `Reporting.Api.dll` |
| `Build exit = 0` | `BACKEND_BUILD_EXIT=0` (مسجَّل في `RC-CANDIDATE-BUILD-AND-REHEARSAL-REPORT.md`) |
| `Tree checksum matches staged candidate` | `36d7f525dc0ee132a4490c08994479b0776e3e89430a7d125dcc0c56f7fc21b4` (86 ملفًّا) + `sha256sum -c` للحزمة المرحليّة = **9/9 OK** |
| `No environment secrets inside artifact` | ملفّ إعداد واحد `appsettings.json` (393 بايتًا): بلا `ConnectionStrings` وبلا `Jwt.Key` وبلا `Password` (`PASSWORD_KEY_HITS=0` · `JWTKEY_NONEMPTY=0`) · صفر سلسلة اعتماد داخل الـDLL |

> ملاحظة: `App.BaseUrl` الافتراضيّ داخل الحزمة هو عنوان الإنتاج، لكنّه **يُتجاوَز** بـ`App__BaseUrl=https://test.emarketingacademy.net` من ملفّ بيئة TEST (وقد أثبت السجلّ `Hosting environment: Staging`).

### الواجهة — بناء جديد خاصّ بـTEST

```
FRONTEND_TSC_EXIT   = 0    (tsc -b --force)
FRONTEND_BUILD_EXIT = 0    (VITE_API_BASE_URL=https://test.emarketingacademy.net/api)
FRONTEND_FILE_COUNT = 7
FRONTEND_ARTIFACT_HASH = 12a5d309f2b7a88ba42c8f6931719e7a433d25e63a69810335729ea91fad47d5
```

| العدّاد | القيمة | التفسير |
|---|---|---|
| `TEST_API_URL_COUNT` | **1** | `…Us=\`https://test.emarketingacademy.net/api\`, L=Ls.create({baseURL…` — قاعدة API للتطبيق |
| `LOCALHOST_API_URL_COUNT` | **0** | ظهر `localhost` مرّتين نصًّا داخل **مكتبات طرف ثالث فقط**: احتياطيّ محلّل العناوين في الموجِّه (`let r=\`http://localhost\``) واحتياطيّ `window.location.href||\`http://localhost\`` في طبقة الشبكة. لا واحدة منهما قاعدة API |
| `RC_API_URL_COUNT` | **0** | |
| `PRODUCTION_API_URL_COUNT` | **0** | |
| `127.0.0.1` | **0** | |

**البناء المحلّيّ طابق المنشور بايتًا:** بصمة الشجرة المبنيّة = بصمة `dist` المرفوعة على الخادم = `12a5d309…` (وهي أيضًا بصمة ما كان منشورًا) ⟹ شحنة الواجهة **متطابقة** ولا انحراف بين البيئة المحلّيّة والخادم. حُزّمت بـ`COPYFILE_DISABLE=1 tar --no-xattrs --no-mac-metadata` ⟹ **0** ملفّ `._*` بعد الفكّ.

---

## 4) الهجرات — **No-op مُثبَت**

**حالة TEST قبل النشر لم تكن مطابقة لافتراض التذكرة، والفارق موثَّق هنا صراحةً:**

| المقارنة | العدد | الحكم |
|---|---|---|
| ملفّات الهجرات في شجرة `TARGET_SHA` | **40** | — |
| صفوف `__EFMigrationsHistory` على TEST | **40** | — |
| هجرات في الشيفرة غير مطبَّقة على TEST | **0** | لا شيء ينتظر التطبيق |
| صفوف في TEST بلا ملفّ مقابل | **0** | لا صفوف جسر متبقّية على TEST |

⟹ **مجموعتا المعرّفات متطابقتان حرفيًّا**، والهجرتان الأحدث (`20260817101108_AddProjectProgressAndHealthStates` و`20260817114129_AddProjectExecutionUpdateProposals`) **مطبَّقتان أصلًا** على TEST ومُضمَّنتان في الحزمة (`IN_ARTIFACT=1` لكلتيهما).

**لماذا 40 لا 42:** الرقم `42` مشتقّ من حسابيّة **RC** (40 صفًّا منها **صفّا جسر النَسَب** + الهجرتان الجديدتان). سجلّ TEST **لا يحوي صفوف جسر**، فمكافئه الصحيح هو **40 صفًّا** بالرأس نفسه والبنية نفسها (79/947) — أي **نفس الحالة النهائيّة** التي أنتجتها التجربة المعزولة على RC. **لم يُدرَج ولم يُعدَّل صفٌّ واحد يدويًّا في `__EFMigrationsHistory`.**

**إثبات No-op وقت التشغيل:** السجلّ بعد الإقلاع يحوي `Applying migration` = **0** مرّة، مع بقاء `MIGRATIONS=40` و`HEAD` و`TABLES=79` و`COLUMNS=947` كما هي.

```
TEST_MIGRATIONS_AFTER     = 40   (لا 42 — انظر التفسير أعلاه؛ لا هجرة معلّقة ولا تعديل يدويّ)
TEST_MIGRATION_HEAD_AFTER = 20260817114129_AddProjectExecutionUpdateProposals
TEST_TABLES_AFTER         = 79
TEST_COLUMNS_AFTER        = 947
DATA_LOSS                 = 0
```

---

## 5) النشر

| الخطوة | التنفيذ | الإثبات |
|---|---|---|
| رفع مرحليّ | `/opt/reporting-test/staging-r21-7e063b4-20260818/{publish,dist}` | `publish` 86 ملفًّا `36d7f525…` · `dist` 7 ملفّات `12a5d309…` |
| نفس نظام الملفّات | جهاز `2049` لكلٍّ من `publish` و`dist` الحيّين والمرحليّين | استبدال `mv` **ذرّيّ** |
| مقارنة البصمات | البناء ⟷ المرحليّ ⟷ المنشور | متطابقة في الثلاثة |
| إيقاف الخدمة | `systemctl stop khubara-reporting-test` | `STOP_EXIT=0` ⟹ `inactive` |
| استبدال ذرّيّ | `mv publish → publish.prev-20260818T161121Z` ثمّ `mv publish.new → publish` (ومثله للواجهة) | `SWAP_BACKEND_EXIT=0` · `SWAP_FRONTEND_EXIT=0` · ملكيّة `www-data` |
| مخزن المستندات | `/var/lib/reporting-test/documents` لم يُلمس | 23 ملفًّا كما هي · **0** دليل `documents` داخل `publish` |
| التشغيل | `systemctl start` | `START_EXIT=0` · PID `1264134` · `NRestarts=0` |
| Nginx/DNS/الشهادات | **لم تُمسّ** | `mtime` موقع Nginx `2026-07-06 17:34:24` · `mtime` ملفّ البيئة `2026-08-07 15:45:13` |
| بيانات اعتماد TEST | **لم تُغيَّر** (الاتّصال نجح من أوّل محاولة) | صفر `28P01` |

`DEPLOYED_SHA = 1.0.0+7e063b493b50ad90ba6131e47042c7cd035fb65b` · النسختان السابقتان محفوظتان في `publish.prev-20260818T161121Z` و`dist.prev-20260818T161121Z`.

---

## 6) بوّابة الدخان

```
SERVICE_ACTIVE               = YES   (active/running · PID 1264134 · NRestarts 0)
HEALTH                       = 200   (محلّيّ وعامّ · {"status":"ok","service":"reporting-api"})
STARTUP_ERRORS               = 0     (1,432 سطر سجلّ · صفر fail:/crit:/Unhandled exception/28P01)
DATABASE_CONNECTION          = PASS  (reporting_test_uat | reporting_test_uat_app)
MIGRATIONS                   = 40    (= رأس المرشّح · صفر "Applying migration")
INVALID_LOGIN                = 401   (محلّيًّا وعبر النطاق العامّ)
PROTECTED_ANONYMOUS_REQUEST  = 401   (/api/projects و/api/clients بلا رمز)
FRONTEND_PAGE                = 200   (الصفحات الخمس في المتصفّح — §7)
FRONTEND_BAKED_API           = TEST ONLY  (1 لـTEST · 0 لـRC/الإنتاج/localhost)
CONSOLE_ERRORS               = 0
FAILED_NETWORK_REQUESTS      = 0
EMAIL_DISABLED_OR_SAFE_MODE  = YES   (DryRun + Email__Enabled=false · صفر محاولة SMTP)
SCHEDULERS_DISABLED          = YES   (Reminders__Enabled=false · صفر سطر مجدول في السجلّ)
OUTBOX_UNSENT                = 0
REQUESTS_TO_LOCALHOST        = 0
REQUESTS_TO_RC               = 0
REQUESTS_TO_PRODUCTION       = 0
```

**بيانات ما بعد النشر:** كلّ العدّادات الستّة عشر مطابقة لما قبله عدا `refresh_tokens` **1425 ⟵ 1427** (رمزا تحديث من عمليّتَي تسجيل الدخول ضمن الدخان نفسه). **فقدان بيانات = 0.**

**الطبقة الوحيدة غير المُمارَسة:** بوّابة `auth_basic` على جذر النطاق — كلمتها غير متاحة (تجزئة فقط في `/etc/nginx/.htpasswd-rc-test`)، وقد تحقّقنا منها سلبيًّا: طلب الجذر بلا اعتماد = **401**، بينما `/api` و`/hubs` و`/health` (عليها `auth_basic off`) مُمارَسة فعليًّا بالكامل.

---

## 7) دخان المتصفّح الحقيقيّ

**الطريقة:** متصفّح Chromium حقيقيّ على الأصل الحقيقيّ `https://test.emarketingacademy.net`؛ الملفّات الساكنة تُقدَّم من **بايتات الواجهة المنشورة نفسها** (بصمة الشجرة `12a5d309…` مطابقة لما على الخادم)، بينما **`/api/**` و`/hubs/**` تذهب فعليًّا إلى خدمة TEST وقاعدة `reporting_test_uat`** — لأنّ جذر النطاق خلف `auth_basic` بكلمة غير متاحة. الدخول بحساب `admin@marketingexperts.local` وكلمته تُقرأ من ملفّ بيئة الخادم عبر stdin ولا تُطبع ولا تُكتب.

| المسار | الحالة | العنوان المعروض | أخطاء جديدة |
|---|---|---|---|
| `/login` | 200 | «تسجيل الدخول» | 0 |
| تسجيل الدخول | — | هبط على `/app` | 0 |
| `/app` | 200 | «لوحة الإدارة والحوكمة» | 0 |
| `/app/projects` | 200 | «المشاريع» | 0 |
| `/app/projects/767e67da…/360` (Project 360) | 200 | «مشروع UAT سوشيال نشط» | 0 |
| `/app/clients/cc877dc2…` (Client 360) | 200 | «عميل UAT ألفا» | 0 |

- **48 طلبًا فعليًّا** إلى الخادم الحقيقيّ (21 مسارًا فريدًا) شملت `/api/auth/login` · `/api/dashboard/me` · `/api/directory/*` · `/api/submissions` · `/api/reports/kpi-summary` · `/api/escalations` · `/api/decisions` · و`/hubs/notifications/negotiate` (SignalR).
- ظهور أسماء بيانات TEST الحقيقيّة في العناوين يُثبت أنّ العرض من القاعدة الحيّة لا من ذاكرة مؤقّتة.
- `CONSOLE_ERRORS=0` · `PAGE_ERRORS=0` · `FAILED_NETWORK_REQUESTS=0`.
- طلبات خارج الأصل: **1 فريد** — خطوط Google (`fonts.googleapis.com`) وهي سلوك المنتج المعتاد. **صفر** طلب إلى localhost أو RC أو الإنتاج.
- 6 لقطات شاشة محفوظة في `/tmp/test-smoke-out/` (خارج المستودع عمدًا).

> هذه جولة دخان فقط — **لم تُعَد حالات UAT الستّ والعشرون**.

---

## 8) التراجع والعزل

- **لم يُستدعَ التراجع**: لا فشل صحّة، ولا فشل إقلاع، ولا فشل هجرة، ولا أخطاء متكرّرة، ولا عنوان API خاطئ، ولا صفحة متعذّرة.
- خطّة التراجع الكاملة جاهزة في `…/test-predeploy-20260818T160622Z-r21/ROLLBACK-STEPS.md` (خلفيّة ⟵ واجهة ⟵ قاعدة عند الحاجة ⟵ إعدادات ⟵ تشغيل وتحقّق).
- **RC لم يُمسّ:** `khubara-reporting-rc` نشطة · PID `1261073` · `NRestarts=0` · بصمة الخلفيّة `be9c0fec…` وبصمة الواجهة `de3166e6…` كما هما · `reporting_rc` = **40 هجرة** بالرأس `AddProject360Foundation` · العامّ 401 (سلوك `auth_basic` المعتاد).
- **الإنتاج لم يُمسّ:** `reporting-api` نشطة · PID `654185` · `NRestarts=0` · `1.0.0+ce166662…` · `reporting_prod` = **30 هجرة** بالرأس `AddReportApproverAndKpiReviewerOverrides` · `/health` العامّ = 200.
- **git:** لا التزام ولا دفع ولا وسم ولا تغيير على `origin/main`.
- القرص: 52 GB متاحة (46% استعمال) بعد كلّ ما سبق.

## 9) ملاحظات للمرحلة التالية (لم تُصلَح هنا عمدًا)

1. **`OBS-TEST-1`** — تحذير `No XML encryptor configured` في `DataProtection` على TEST (تحذير معروف غير حاجب؛ نظيره على RC هو خطر إبطال الجلسات عند التفعيل).
2. **`OBS-TEST-2`** — كلمة `auth_basic` لنطاق TEST غير متاحة لأيّ أتمتة (تجزئة فقط) ⟹ أيّ دخان متصفّح مستقبليّ يحتاج إمّا الكلمة وإمّا الطريقة الموصوفة في §7.

---

## 10) الخلاصة

```
TARGET_SHA                    = 7e063b493b50ad90ba6131e47042c7cd035fb65b
TEST_BACKUP                   = PASS   (10/10 sha256 · pg_restore --list 482 · لا نسخة محذوفة)
TEST_BACKEND_DEPLOYMENT       = PASS   (36d7f525… · 1.0.0+7e063b4…)
TEST_FRONTEND_DEPLOYMENT      = PASS   (12a5d309… · TEST API فقط)
TEST_MIGRATIONS               = NO-OP  (40 صفًّا · صفر معلّق · صفر تعديل يدويّ · البنية 79/947)
TEST_HEALTH                   = 200
TEST_SMOKE                    = PASS
TEST_BROWSER_SMOKE            = PASS   (5 مسارات · صفر خطأ كونسول · صفر طلب فاشل)
TEST_ROLLBACK_REQUIRED        = NO
TEST_READY_FOR_TARGETED_UAT   = YES

RC_LIVE_TOUCHED               = NO
PRODUCTION_TOUCHED            = NO
NEXT_REQUIRED_ACTION          = تنفيذ UAT مختصر على TEST قبل أي تصريح نشر RC
```
