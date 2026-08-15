# G1.5 — تدقيق أمان السكربتات وتشغيل PLAN/Dry-run (Server-Side Dry Run & Script Safety Audit)

**المعرّف:** `UAT-TEST-G1.5` · **الحزمة قيد التدقيق:** `Ops/TestUatPreparation/` (9 سكربتات + README + env template) · **مرجع التكافؤ:** `develop @ ffb511906f0b523ebf59fbfa27a51be66189109a` · **آخر هجرة:** `20260709231845_AddWorkstreamDeliverables` (30 هجرة)

> ⚠️ **مرحلة تدقيق فقط.** لم يُنفَّذ أي كتابة/إعادة تشغيل/نسخ احتياطي/هجرة/بذر/INSERT-UPDATE-DELETE-CREATE-DROP/تغيير env/Commit/Push. لم تُنشأ قاعدة ولا دور ولا نسخة احتياطية. لم تُلمَس بيئة TEST ولا Production ولا RC.

---

## 0. ملخّص تنفيذيّ (Executive Summary)

الحُرّاس البرمجية للحزمة **سليمة ومحكمة**: الوضع الافتراضي PLAN بلا كتابة، الكتابة تتطلّب `--apply` + `OPS_ALLOW_WRITE=1` + تأكيد، حُرّاس prod/RC وهدف الكتابة تعمل، لا `eval`، لا طباعة أسرار. لكن **الحزمة غير مكتملة تشغيليًّا** في محورين حاسمين:

1. **Fixtures غير قابلة للتنفيذ فعليًّا** — السكربت `05` (UAT) وضعُ apply فيه هيكل توثيقي (`log` يشير إلى README) بلا استدعاءات API فعلية؛ والسكربت `04` (Legacy) ينفّذ أرشفة القوالب فقط بينما بذر التقارير التاريخية (جوهر Legacy) غير مُنفَّذ. ⇒ **UAT Fixtures = NO-GO**، **Legacy Fixtures = CONDITIONAL**.
2. **DataProtection عبر الإعدادات = غير مدعوم في الشيفرة** — التطبيق لا يقرأ `DataProtection:KeyPath`/`ApplicationName` (لا يستدعي `AddDataProtection`). المفتاحان في `staging.env.template` **خامدان (inert)**. لكن لأن المصادقة **JWT Bearer عديمة الحالة** (لا Cookies) فإن DataProtection **ليس على المسار الحرج** للـcutover ⇒ ليس حاجزًا وظيفيًّا، لكنه يوجب تصحيح القالب لإزالة إيهام الفعالية.

بند ثانويّ أمنيّ حسّاس: بذّار الأدمن `IdentitySeeder` يستخدم `env.IsProduction()` للتراجع؛ في **Staging** غيابُ `Seed:AdminPassword` يُرجِع للقيمة الضعيفة الافتراضية `Admin#12345`، و`06` preflight يفحص `Seed__AdminEmail` فقط دون كلمة المرور.

**البنود الخادمية (نسخ لـ`/root/uat-prep-review/` + shellcheck على الخادم)** مُعلَّقة على تأكيد بيانات اتصال TEST (كلها placeholders؛ الذاكرة تحمل خادم Production فقط) — لم أُخمِّن هدف SSH تجنّبًا لإصابة الإنتاج.

---

## 1. النسخ إلى مسار مراجعة معزول على خادم TEST — **مُعلَّق (Pending)**

- **الحالة:** لم يُنفَّذ. المطلوب نسخ الحزمة إلى `/root/uat-prep-review/` (خارج مسارات runtime/frontend/systemd) على خادم TEST.
- **السبب:** بيانات اتصال خادم TEST غير مؤكَّدة — كل قيم `config.env.template` الخاصة بالخادم (`OPS_SERVER_HOST`, `TEST_SERVICE_NAME`, `TEST_ENV_FILE`, `TEST_DOMAIN`, `OPS_EXPECTED_HOSTNAME` …) هي `REPLACE_ME_*`. الذاكرة تحمل خادم **Production** فقط (`root@187.127.72.232` / `srv1747233`). تخمين هدف SSH يخالف قاعدة «العناية القصوى على البنية المشتركة» وقد يصيب الإنتاج.
- **القرار:** يُنفَّذ فور تزويد المالك ببيانات خادم TEST المؤكَّدة (host/user/key/hostname/service/env-file path). النسخة ستكون قراءة-فقط (`chmod -R a-w` بعد النسخ)، خارج أي مسار تشغيليّ، ولن تُشغَّل تلقائيًّا (السكربتات كلها PLAN افتراضيًّا).

## 2. ShellCheck على الخادم — **مُعلَّق + بديل محليّ نُفِّذ**

- **على الخادم:** مُعلَّق (تبعًا للبند 1، ومشروط بتأكيد توفّر shellcheck على الخادم؛ لن يُثبَّت بلا موافقة).
- **محليًّا:** `shellcheck` **غير مثبَّت** على جهاز التطوير (تعذّر التشغيل الآليّ). أُجريت **مراجعة يدوية مكافئة** لكل السكربتات التسعة:
  - `set -Eeuo pipefail` في كل ملف تنفيذيّ؛ `00-common.sh` يمنع التشغيل المباشر (`BASH_SOURCE[0]==$0 ⇒ exit 2`).
  - كل توسّعات المتغيّرات المستخدمة في مسارات/أوامر محاطة باقتباس مزدوج (`"${VAR}"`)؛ لا word-splitting غير مقصود لُوحِظ.
  - المصفوفات (`PSQL_SUPER=(...)`, `auth=(...)`, `UAT_USERS=(...)`) تُوسَّع بـ`"${arr[@]}"`.
  - `[[ ... ]]` بدل `[ ... ]` في كل الشروط؛ `$(...)` بدل backticks.
  - لا `SC2086` (توسّع غير مقتبس) في مسارات حسّاسة؛ التعطيل الوحيد المصرّح `# shellcheck disable=SC1090` عند `source "$cfg"` (ديناميكيّ مقصود).
  - **ملاحظات يدوية (منخفضة):** (أ) `guard_name_not_forbidden` مع قيمة فارغة لا يُطلق الحارس (grep على سلسلة فارغة = لا تطابق)؛ مُخفَّف بأن `guard_write_target_is_new_uat` يطابق الاسم حرفيًّا و`FORBIDDEN_NAME_REGEX` يُفحَص غير فارغ. (ب) `NEW_UAT_DB` غير مُدرَج في فحص `require_real_config`؛ مُخفَّف بأنه ثابت في القالب = `reporting_test_uat`. (ج) `--yes` يتخطّى التأكيد التفاعليّ فقط، لا يتخطّى بوّابتَي `--apply` + `OPS_ALLOW_WRITE=1`.
- **أي إصلاح** يُطبَّق على نسخة الريبو المحلية فقط (لا على الخادم)، وبعد موافقة.

## 3. تدقيق الأوضاع الافتراضية والحُرّاس

| فحص | النتيجة | الدليل (المصدر) |
|---|---|---|
| بلا وسائط = PLAN آمن | ✅ | `00-common.sh:18` `OPS_MODE="plan"` |
| لا كتابة في الوضع الافتراضي | ✅ | `require_write_enabled` يُرجِع `1` في plan بلا تنفيذ (`:108-110`) |
| `--apply` يتطلّب `OPS_ALLOW_WRITE=1` | ✅ | `:112` `[[ "$OPS_ALLOW_WRITE"=="1" ]] || die` |
| + تأكيد تفاعليّ | ✅ | `confirm` برمز `EXECUTE` (`:117-127`) |
| + حُرّاس الهدف قبل الكتابة | ✅ | `guard_all_names` + `guard_write_target_is_new_uat` + `require_real_config` + `guard_expected_host` |
| لا تجاوز حارس بمتغيّر فارغ/اقتباس ضعيف | ✅ (مع ملاحظات §2) | الأهداف مقتبسة؛ هدف الكتابة يُطابَق حرفيًّا `== NEW_UAT_DB` |
| لا `eval` | ✅ | `grep eval` = 0 |
| لا `source` غير موثوق | ⚠️ منخفض | `load_config` يعمل `source` على ملف الإعداد (يُنفَّذ كـbash) ⇒ يجب أن يكون `config.env` موثوقًا و`chmod 600` خارج Git |
| لا طباعة connection strings/أسرار/توكنات | ✅ | `mask()` (`***(N chars)`)؛ `02` يطبع CS بـ`Password=<...>` لا القيمة؛ `ADMIN_TOKEN`/`PGPASSWORD` عبر البيئة فقط |

**الخلاصة:** طبقة الأمان البرمجية **GO**.

## 4. تشغيل PLAN/Dry-run للسكربتات الثمانية

نُفِّذت الثمانية **محليًّا** في وضع plan بإعداد placeholder. **مبرّر:** وضع plan لا يتّصل بأي خادم (يطبع الخطة ويخرج `0` قبل أي `psql/curl/systemctl`) ⇒ التشغيل مكافئ للمقصد بلا خطر بنية تحتية. التشغيل على خادم TEST (بند 1) لا يضيف سلوكًا كتابيًّا في plan.

| سكربت | مخرَج PLAN (مُتحقَّق) | يُثبِت عدم التغيير |
|---|---|---|
| `01-backup` | يطبع 11 أمر نسخ (pg_dump/tar/cp/psql-SELECT/curl) لوجهة `BACKUP_ROOT` | لا `mkdir`/كتابة (require_write_enabled=1) |
| `02-create-uat-db` | يطبع SQL: CREATE ROLE/DATABASE/REVOKE/GRANT للهدف `reporting_test_uat` | لا اتصال psql؛ الخطة نصّ فقط |
| `03-validate-uat-db` | يطبع 6 استعلامات **SELECT فقط** | لا اتصال؛ SELECT-only حتى في apply (§10) |
| `04-legacy-fixture` | يطبع خطة أرشفة + بذر تاريخيّ | لا curl؛ نصّ فقط |
| `05-uat-fixture` | يطبع خطوات 1..10 | لا curl؛ نصّ فقط |
| `06-cutover` | يطبع Preflight+Cutover+Validate + مسار env السابق `.pre-uat-<stamp>` | لا cp/install/systemctl |
| `07-health` | يطبع 8 فحوص (Health/Auth/API/SignalR/Email/Project-First/Legacy/Rollup) | لا curl؛ قراءة فقط أصلًا |
| `08-rollback` | يطبع خطة استعادة env السابق + restart + health | لا install/systemctl |

**إثبات عدم التغيير:** كل تشغيل خرج بـ`mode=plan` عبر `on_exit`؛ لم تُنشأ قاعدة/دور/نسخة؛ لم يتغيّر env-file؛ لا restart؛ قاعدة TEST لم تُلمَس (لا اتصال). أهداف كل سكربت ظهرت كـplaceholders (`REPLACE_ME_*`) مؤكِّدةً غياب التنفيذ الفعليّ.

## 5. تصنيف كل عبارات SQL

| المصدر | العبارة | التصنيف | الهدف |
|---|---|---|---|
| `01:32,46` | `pg_dump -Fc`؛ `SELECT "MigrationId" FROM "__EFMigrationsHistory"` | **Read-only** | القاعدة الحالية (نسخ/قراءة) |
| `02:34-35` | `CREATE ROLE reporting_test_uat_app LOGIN … NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION` | **Administrative creation** | دور جديد |
| `02:36` | `CREATE DATABASE reporting_test_uat OWNER … TEMPLATE template0` | **Administrative creation** | قاعدة UAT جديدة |
| `02:37-38` | `REVOKE ALL … FROM PUBLIC`؛ `GRANT CONNECT … TO role` | **Write-on-new-UAT-only** | القاعدة الجديدة |
| `02:51,59` | `SELECT 1 FROM pg_database/pg_roles WHERE …` | **Read-only** (حارس وجود) | postgres |
| `02:65` | `ALTER ROLE … LOGIN PASSWORD …` (فرع idempotent) | **Administrative** (على الدور الجديد فقط) | دور UAT |
| `03:29-34` | `SELECT count(*)/MigrationId` ×6 | **Read-only** | القاعدة الجديدة |
| `06:66` | `SELECT 1 FROM pg_database WHERE datname='reporting_test_uat'` | **Read-only** | postgres |
| `07:67` | `SELECT count(*) FROM email_outbox` | **Read-only** | القاعدة الجديدة |
| `04`/`05` | لا SQL خام (عبر API فقط؛ INSERT يقع خادميًّا في القاعدة الجديدة) | **Write via API → new UAT** | القاعدة الجديدة |

**التحقّقات المطلوبة:** ✅ لا SQL يستهدف Production/RC. ✅ لا SQL على القاعدة القديمة إلا `SELECT`/`pg_dump`. ✅ لا `DROP DATABASE`/`DROP ROLE`. ✅ لا `UPDATE`/`DELETE` على القاعدة القديمة. ✅ كل INSERT للـfixture يمرّ عبر API للقاعدة الجديدة. ⚠️ **أسماء الجداول:** `execution_taxonomy_items` + عمود `domain` في `03:31-32` **تقديريّة** (السكربت نفسه يحمل تذكيرًا بمراجعتها مقابل السكيمة الفعلية `:37,59`)؛ يجب التحقّق من الاسم الفعليّ قبل apply. باقي الأسماء (`__EFMigrationsHistory`, `report_templates`, `AspNetUsers`, `email_outbox`, `pg_database`, `pg_roles`) مؤكَّدة صحيحة.

## 6. مراجعة الـFixtures (تفصيليّة) — **النقطة الحاسمة**

### 6.1 Legacy (`04`)
- **إيجاد القوالب:** ✅ منفَّذ فعليًّا — `archive_template()` (`:75-86`) يبحث بالعنوان (مفتاح مستقر) عبر `GET /api/report-templates?search=`، يقرأ الحالة، ويؤرشف بـ`POST /{id}/archive` إن لم تكن `Archived`. idempotent (يتخطّى المؤرشف).
- **بذر Historical Submissions:** ❌ **غير منفَّذ** — `:91-92` مجرّد `log` يشير إلى «المسار A (API workflow) أو أداة dotnet fixture (المسار B)»، بلا شيفرة إنشاء فعلية. **المسار B (أداة dotnet على نمط `LegacyExecutionFixture.cs`) غير موجودة** — موصوفة فقط.
- **الحفاظ على Archived/IsActive=false:** ✅ الأرشفة عبر endpoint الحوكمة الرسميّ (لا كتابة خام).
- **idempotency / منع الازدواج:** ✅ للأرشفة (lookup بالعنوان)؛ ❔ للتقارير التاريخية غير قابل للتقييم (غير منفَّذ).
- **Cleanup:** موصوف (`cleanup_legacy`) غير منفَّذ كدالة فعلية.
- **الحكم:** **Legacy Fixtures = CONDITIONAL** — الأرشفة جاهزة؛ بذر التقارير التاريخية + أداة المسار B ناقصان.

### 6.2 UAT (`05`)
- **حارس البريد:** ✅ منفَّذ (`:46-49` يرفض أي بريد غير `@uat.local`).
- **إنشاء مستخدمين/أقسام/فرق/عملاء/مشاريع/Workstreams/Deliverables/Project-First:** ❌ **غير منفَّذ إطلاقًا في وضع apply** — `:82-85` أربعة أسطر `log` تشير إلى README، بلا أي `curl POST`. البيانات (`UAT_USERS`, `UAT_DEPARTMENTS`, …) معرّفة لكنها غير مُستهلَكة في apply.
- **كلمات المرور / النطاقات / الصلاحيات / idempotency / cleanup:** موصوفة في خطة plan فقط، غير منفَّذة.
- **الحكم:** **UAT Fixtures = NO-GO** — وضع apply هيكل توثيقيّ (skeleton) غير قابل للتنفيذ. حسب الشرط الصريح: *«إذا كانت السكربتات مجرّد skeleton أو placeholders وغير قابلة للتنفيذ، صنّف UAT Fixtures = NO-GO ولا تبدأ المرحلة الثانية»* ⇒ **لا يجوز بدء المرحلة الثانية بحالتها الراهنة**.

## 7. جدول تكافؤ Cutover ↔ Rollback

| خطوة Cutover | الأثر | خطوة Rollback المقابلة | عكوس؟ | زمن الرجوع | دليل التحقّق |
|---|---|---|---|---|---|
| `06:74` `cp -a env ⇒ .pre-uat-<stamp>` (chmod 600) | لقطة env السابق | `08:22` يلتقط أحدث `*.pre-uat-*` تلقائيًّا | ✅ | — | وجود الملف |
| `06:77` `install -m600 <new> env` (ذرّي) | Connection String + Environment + CORS + JWT الجديدة | `08:48` `install -m600 <prev> env` | ✅ | ثوانٍ | diff env |
| صلاحيات بعد الاستبدال | `-m 600` مضمونة الطرفين | نفسها | ✅ | — | `stat` |
| `06:80` `systemctl restart` خدمة TEST | إقلاع على القاعدة الجديدة (MigrateAsync+Seeders) | `08:51` `systemctl restart` | ✅ | ثوانٍ | health |
| ASPNETCORE_ENVIRONMENT Development→Staging | تفعيل بوّابتَي JWT/CORS + HSTS | استعادة env السابق | ✅ | — | `/health` + سلوك env |
| Connection String → `reporting_test_uat` | تحويل القراءة/الكتابة للقاعدة الجديدة | استعادة CS السابق | ✅ (القاعدة القديمة لم تُمَسّ) | ثوانٍ | login + query |
| DataProtection ApplicationName/KeyPath | **لا أثر فعليّ** (env خامد، §8) | لا شيء لاستعادته | ✅ (لا حالة) | — | — |
| Cookie name | **لا أثر** (JWT، لا Cookies) | — | ✅ | — | — |

**ثغرات مرصودة:**
- **Rollback يدويّ لا آليّ:** `06:84-86` عند فشل `07` يطبع توصية بتشغيل `08` ثم `die` — **لا يستدعي `08` تلقائيًّا**. القرار للمشغّل.
- **فحص Health بلا timeout/retry:** `06:81`/`08:52` = `sleep 5` ثم `curl` واحد. لا حلقة انتظار/مهلة؛ إقلاع بطيء قد يُقيَّم فشلًا زائفًا.
- **سلوك فشل restart:** `set -e` يُجهِض عند `systemctl restart` غير صفريّ **بعد** استبدال env ⇒ env الجديد مركَّب والخدمة قد تكون ساقطة ⇒ يلزم `08` يدويّ. مقبول لكن يجب توثيقه في Runbook.
- **الحكم:** Cutover **قابل للعكس** (env-swap + restart، القاعدة القديمة سليمة)؛ التحسينات (auto-rollback + health-retry) موصى بها لا حاجزة.

## 8. جدوى DataProtection — **حاجز شيفرة مصدرية (Source Change) للميزة، غير حاجز للـCutover**

**النتيجة المصدرية (مؤكَّدة):**
- التطبيق **لا** يستدعي `AddDataProtection`/`PersistKeysToFileSystem`/`SetApplicationName` في الـAPI (المطابقة الوحيدة في أداة `tools/OrgImporter` المنفصلة).
- التطبيق **لا يقرأ** `DataProtection:KeyPath` ولا `DataProtection:ApplicationName` من الإعدادات (grep = 0 في `src`).
- المصادقة **JWT Bearer عديمة الحالة** (`Program.cs:36-64`)؛ **لا Cookie auth** (grep AddCookie/Cookie:Name = 0). التوكن موقَّع بـ`Jwt:Key` بلا DataProtection.

**الاستنتاج:** المفتاحان `DataProtection__KeyPath` و`DataProtection__ApplicationName` (و`Cookie__Name`) في `env/staging.env.template` **خامدة (inert)** — التطبيق يتجاهلها. حسب الشرط الصريح: *«إذا كان التطبيق لا يقرأ DataProtection key path/ApplicationName من الإعدادات، صنّف ذلك كحاجز Source Change»* ⇒ **تفعيل DataProtection مستمرّ عبر env وحده = مستحيل؛ يتطلّب تعديل مصدر** (`AddDataProtection().PersistKeysToFileSystem(...).SetApplicationName(...)` في `Program.cs`).

**لكن:** بما أن المسار الحرج للمصادقة JWT عديم الحالة، فإن DataProtection يُستخدَم فقط لرموز Identity العابرة (إعادة تعيين كلمة مرور/تأكيد بريد)، وهي تُولَّد عند الطلب. لذا:
- **DataProtection ليس حاجزًا لِـCutover** الوظيفيّ (لا Cookies/antiforgery حرجة).
- **إجراء واجب:** إزالة المفتاحين الخامدين + `Cookie__Name` من `staging.env.template` (لتفادي إيهام الفعالية)، **أو** — إن أُريد ثبات مفاتيح DP فعليًّا — إدراج تعديل مصدر مُعتمَد (يخالف ركيزة «بلا تغيير مصدر» ⇒ يُسقَط الطلب).
- ملاحظة على `01:42,79`: نسخ `DATAPROTECTION_KEYPATH` احتياطيًّا سيتخطّى بأمان إن غاب المسار (`[[ -d ]]`)، لكنه على الأرجح لن يجد شيئًا (المسار الافتراضي لـDP غير مُهيّأ عبر الإعدادات).

## 9. جدول بوّابات إقلاع Staging

| الإعداد | مطلوب؟ | المصدر | Placeholder موجود؟ | سرّ؟ | تحقّق وقت التشغيل؟ | الحالة |
|---|---|---|---|---|---|---|
| `Jwt__Key` (≥32، لا "dev-only") | **نعم (يُجهِض الإقلاع)** | `Program.cs:30-34` | ✅ `__SET_STRONG_UNIQUE_32PLUS__` | ✅ | ✅ `throw` | GO (املأ) |
| `Cors__AllowedOrigins__0` (لا wildcard/localhost) | **نعم (يُجهِض)** | `Program.cs:155-165` | ✅ `https://__TEST_DOMAIN__` | لا | ✅ `throw` | GO (املأ) |
| `ConnectionStrings__Default` (Database=reporting_test_uat) | **نعم** | DI/EF | ✅ `Password=__SET_AT_RUNTIME__` | ✅ | جزئيّ (EF يفشل عند Migrate) | GO (املأ) |
| `ASPNETCORE_ENVIRONMENT=Staging` | **نعم** | env-file | ✅ | لا | ضمنيّ (يفعّل البوّابات) | GO |
| `Seed__AdminEmail` | نعم (لأدمن صالح) | `IdentitySeeder:37` | ✅ | لا | ❌ لا إجهاد | ⚠️ (§9 أدناه) |
| `Seed__AdminPassword` | **نعم (أمنيًّا)** | `IdentitySeeder:38` | ✅ | ✅ | ❌ لا إجهاد | ⚠️ **حاجز أمنيّ ناعم** |
| `Email__Enabled=false` | لا (افتراضي false) | `EmailOptions:9` | ✅ | لا | لا | GO |
| `Reminders__Enabled=false` | لا (افتراضي false) | `ReminderOptions:13` | ✅ | لا | لا | GO |
| `Scheduler__Enabled` | — | **غير موجود بالمصدر** | ✅ | لا | — | ❌ **خامد — يُحذَف** |
| `ExternalIntegrations__Enabled` | — | **غير موجود بالمصدر** | ✅ | لا | — | ❌ **خامد — يُحذَف** |
| `Cookie__Name` | — | **لا Cookie auth** | ✅ | لا | — | ❌ **خامد — يُحذَف** |
| `DataProtection__KeyPath` / `__ApplicationName` | — | **لا يُقرَأ** (§8) | ✅ | لا | — | ❌ **خامد — يُحذَف** |

**بند أمنيّ حسّاس (Staging Admin Seed):** `IdentitySeeder:40-45` يستخدم `env.IsProduction() ? null : DEFAULT`. بما أن **Staging ليس Production**، فإن غياب `Seed:AdminPassword` يُرجِع القيمة الضعيفة `Admin#12345` (والبريد `admin@marketingexperts.local`)، ويُنشأ أدمن بها بلا أي تحذير/إجهاد. **و`06:62` preflight يفحص `^Seed__AdminEmail=` فقط، لا كلمة المرور** ⇒ ثغرة: env يمرّ الـpreflight بينما كلمة مرور الأدمن الفعلية = الافتراضية الضعيفة. **توصية:** إضافة فحص `Seed__AdminPassword` (غير فارغ + ≠ افتراضيّ) إلى preflight `06`، وملؤه إلزاميًّا.

## 10. مراجعة `03-validate-uat-db.sh` تحديدًا (READ-only مع `--apply`)

- **لماذا `--apply`؟** ليس لأنه يكتب، بل لأن `parse_common_args` يجعل الوضع الافتراضي PLAN لكل السكربتات؛ فبلا `--apply` يطبع `03` خطة الاستعلامات ويخرج (`:26-38`) دون الاتصال بالقاعدة. `--apply` هنا يعني «شغِّل الاستعلامات فعلًا»، وكلها **SELECT** (`:47-57`).
- **هل يكتب؟** ❌ لا — لا `INSERT/UPDATE/DELETE/CREATE/DROP`؛ يتّصل بـ`NEW_UAT_ROLE` (دور تطبيق بلا صلاحيات عليا) على القاعدة الجديدة فقط.
- **هل الاسم مضلِّل؟** جزئيًّا — «apply» يوحي بكتابة بينما السلوك قراءة. مصدر اللبس اصطلاحيّ (توحيد الأعلام).
- **التوصية (بلا تعديل سلوك الآن):**
  1. **الأنسب:** جعل `03` يشغّل استعلاماته في وضع plan أيضًا (قراءة فقط آمنة بطبيعتها) أو استبدال بوّابته بـ`--run`/تشغيل مباشر، مع إبقاء `--apply` مقبولًا كمرادف.
  2. **أو** توثيق صريح في README أن `03 --apply` = «قراءة فقط، لا يكتب».
  3. **فصل مقترح:** `03a validate-schema` (بعد Cutover مباشرة: 30 هجرة + آخر هجرة + عدم تعليق) و`03b validate-seeders` (بعد الإقلاع الكامل: Taxonomy/Templates/Admin + غياب OrgSeeder) — يوضّح توقيت كل فحص.
  - **لم أُعدِّل السلوك** — التوصية معروضة للاعتماد فقط.

## 11. البوّابة النهائية (Final Gate)

| البند | الحالة | التبرير |
|---|---|---|
| **Scripts syntactically safe** | **GO** | `bash -n` سليم؛ مراجعة يدوية مكافئة لـshellcheck؛ strict mode + guards + no-eval + masking. (shellcheck الخادميّ مُعلَّق على بند 1/2.) |
| **Scripts operationally complete** | **NO-GO** | `05` apply = skeleton؛ `04` بذر التقارير التاريخية + أداة المسار B ناقصان؛ اسم جدول Taxonomy في `03` غير مؤكَّد. |
| **Fixtures executable** | **NO-GO** | UAT (`05`) غير قابل للتنفيذ؛ Legacy (`04`) جزئيّ (CONDITIONAL). |
| **Cutover reversible** | **CONDITIONAL GO** | العكس بـenv-swap + restart والقاعدة القديمة سليمة؛ مشروط بـ(auto-rollback + health-retry + توثيق فشل restart). |
| **DataProtection ready** | **NO-GO (كميزة) / غير حاجز للـCutover** | env وحده لا يكفي (لا يُقرأ بالمصدر)؛ يجب حذف المفاتيح الخامدة؛ ثبات DP فعليّ = تغيير مصدر (يُسقَط). المصادقة JWT لا تتطلّبه. |
| **Safe to begin Phase 2** | **NO-GO** | مشروط بإغلاق: (1) تفعيل `05` فعليًّا + إكمال `04`؛ (2) تأكيد اسم جدول Taxonomy؛ (3) تنظيف env القالب من المفاتيح الخامدة + فرض `Seed__AdminPassword` في preflight؛ (4) بيانات خادم TEST المؤكَّدة لتنفيذ بندَي 1/2 الخادميين. |

### البنود المطلوب تعديلها قبل المرحلة الثانية (على نسخة الريبو، بعد موافقة)
1. `05-seed-uat-fixture.sh` — تنفيذ استدعاءات API الفعلية للخطوات 1..10 + `verify_uat`/`cleanup_uat` كدوال حقيقية.
2. `04-seed-legacy-fixture.sh` — تنفيذ بذر التقارير التاريخية (المسار A) و/أو إنشاء أداة dotnet fixture (المسار B) + `verify_legacy`/`cleanup_legacy`.
3. `03-validate-uat-db.sh` — تأكيد اسم/أعمدة جدول Execution Taxonomy مقابل السكيمة الفعلية (وتوضيح دلالة `--apply`).
4. `env/staging.env.template` — حذف `DataProtection__KeyPath`/`__ApplicationName`/`Cookie__Name`/`Scheduler__Enabled`/`ExternalIntegrations__Enabled` (خامدة)؛ الإبقاء على الفعّالة فقط.
5. `06-cutover-test-to-uat.sh` — إضافة فحص `Seed__AdminPassword` للـpreflight؛ (اختياريّ) auto-invoke `08` عند فشل `07` + health-retry.

### تقدير زمنيّ واقعيّ لإغلاق الفجوات (تطوير + اختبار، بلا تنفيذ حيّ)
- تنفيذ `05` كاملًا: متوسط–كبير. تنفيذ `04` (المسار A) + أداة المسار B: كبير. تأكيد سكيمة Taxonomy + تنظيف env + preflight: صغير. تحسينات Cutover: صغير–متوسط. **الإجمالي: عمل تطويريّ ملموس قبل أي أهلية للمرحلة الثانية** (ليست تعديلات تجميلية).

### البنود الخادمية المُعلَّقة (تتطلّب تأكيد المالك)
- نسخ الحزمة إلى `/root/uat-prep-review/` على خادم TEST (§1).
- تشغيل `shellcheck` على الخادم (§2).
- كلاهما محجوب على تزويد بيانات اتصال خادم TEST المؤكَّدة (host/user/key/hostname/service/env-file). **لن أُخمِّن هدف SSH** تجنّبًا لإصابة الإنتاج.

---

## 12. إغلاق الحواجز المحلية (G1.5 Local Blocker Closure)

> **النطاق:** إغلاق الحواجز المحلية التي كشفها التدقيق أعلاه — **بلا اتصال بأي خادم، بلا إنشاء قاعدة/دور، بلا نسخ احتياطي حيّ، بلا تعديل env حيّ، بلا Restart، بلا هجرة، بلا أي كتابة DB، بلا تشغيل أي API/DB/خدمة/أداة، بلا Commit/Push.** كل العمل على نسخة الريبو المحلية فقط؛ الفحوص كلها ثابتة (static) أو compile-only.

### 12.1 ما أُغلِق (مقابل §6/§8/§9/§10)

| # | الحاجز (من التدقيق) | الإغلاق المُنفَّذ محليًّا | دليل |
|---|---|---|---|
| 1 | `05` UAT apply = skeleton (§6.2 NO-GO) | أُعيدت كتابة `05-seed-uat-fixture.sh` باستدعاءات API فعلية للخطوات 1..10 عبر `POST /api/directory/users` وبقيّة الموارد، مع `admin_login`/`api`/`api_or_die`، دوال `seed/verify/cleanup` حقيقية، حارس بريد `@uat.local`، مفاتيح مستقرّة، idempotency، وضع افتراضي `plan`. | `bash -n` = OK؛ `ACTION="plan"` افتراضيًّا |
| 2 | `04` بذر Historical Submissions + أداة المسار B ناقصان (§6.1 CONDITIONAL) | (أ) أُعيدت كتابة `04-seed-legacy-fixture.sh` بالعناوين الستّة الصحيحة + بحث قائمة (لا `?search=` غير المدعوم) + مسارات verify صحيحة (`/api/reporting/project-execution/{projects,pods}` + `?status=Archived`) + دوال `seed/verify/cleanup`. (ب) أُنشئت أداة .NET المخصّصة `reporting-backend/tools/LegacyExecutionFixture` تكتب Historical Submissions (`Status=Closed`) مباشرةً عبر `AppDbContext` على القوالب الستّة المؤرشفة (الـAPI الرسميّ يعجز — حارس الإسناد يمنع القالب غير المُسنَد)؛ مفاتيح مستقرّة `(versionId+periodKey+submitterId)`، idempotent، seed/verify/cleanup، dry-run افتراضيّ، حارس سلسلة اتصال (يرفض prod/RC)، معاملة تُلغى في dry-run، لا تعطيل أيّ حارس runtime، لا تغيير قالب/إصدار/مستخدم. | **`dotnet build -c Release` = Build succeeded، 0 Warning، 0 Error**؛ الأداة خارج `Reporting.sln` (مثل KpiTemplateBinder) |
| 3 | اسم جدول Taxonomy في `03` تقديريّ (§5) | صُحِّح إلى السكيمة الفعلية `execution_taxonomy_values."Domain"` (بدل `execution_taxonomy_items`/`domain` المُخمَّن)؛ القيم المتوقّعة 170 صفًّا / 19 بُعدًا مطابقة لـ`ExecutionTaxonomySeeder`. | `03:74-75,100-103`؛ `EXPECTED_TAXONOMY_ROWS=170`/`DOMAINS=19` |
| 4 | مفاتيح DataProtection/Cookie خامدة توهم الفعالية (§8) | حُذفت من `env/staging.env.template`؛ استُبدلت بتعليق توثيقيّ يوضّح أن الـruntime يعتمد JWT Bearer فقط ولا يقرؤها. | `staging.env.template:23-25` — لا مفاتيح، تعليق فقط |
| 5 | مفاتيح `Scheduler__*`/`ExternalIntegrations__*` خامدة (§9) | حُذفت من القالب مع تعليق يوضّح أن الكود الحالي لا يقرؤها. | `staging.env.template:34` |
| 6 | `06` preflight يفحص `Seed__AdminEmail` فقط دون كلمة المرور — قد يقلِع بـ`Admin#12345` الضعيفة (§9) | أُضيف فحص `Seed__AdminPassword` للـpreflight: يرفض الفارغ/placeholder (`__SET_AT_RUNTIME__`/`__*__`/`REPLACE_ME_*`) والافتراضية الضعيفة `Admin#12345`، مطابقةً لحارس `Program.cs`. | `06:83-87` |
| 7 | Cutover بلا health-retry / auto-rollback موثّق (§7) | حُسّنت مسارات `06`/`08` (انتظار active + health بمهلة/poll) وتوثيق فشل restart؛ `08` يلتقط أحدث `*.pre-uat-*` تلقائيًّا للرجوع الذرّي بـenv-swap + restart. | `08:47-73`؛ `06` preflight/validate |
| 8 | UAT residue / أداة legacy وسِكيمة | UAT Workstreams/Deliverables القديمة **لا تُهاجَر** (قرار صريح)؛ الأداة الجديدة معزولة عن Project-First هيكليًّا (القوالب الستّة ≠ قوالب التنفيذ الأربعة ⇒ versionIds مختلفة ⇒ **لا احتساب مزدوج**، مؤكَّد بالمصدر `ProjectFirstExecutionAggregationService:167,181`). | فحص ثابت للمصدر |

### 12.2 الفحوص المحلية الثابتة المُنفَّذة (Item 10)

- **`bash -n` (صحّة نحوية):** 9/9 سكربتات = OK.
- **strict-mode:** `set -Eeuo pipefail` حاضر في 9/9.
- **`eval`:** 0 استخدام. **`rm -rf`/`DROP DATABASE`:** 0. **`curl -k` (TLS غير آمن):** 0.
- **حُرّاس الكتابة:** 35 إشارة إلى `require_write_enabled`/`OPS_ALLOW_WRITE`/`OPS_MODE`؛ الوضع الافتراضي `plan` في كل السكربتات، و`ACTION="plan"` في `04`/`05`.
- **مسح الأسرار (Item 9):** كل المطابقات = placeholders (`__SET_AT_RUNTIME__`/`__SET_STRONG_UNIQUE_32PLUS__`/`REPLACE_ME_*`) أو مراجع متغيّرات بيئة أو حارس **يرفض** الافتراضية `Admin#12345`. لا سرّ حقيقيّ مخزَّن. أداة .NET لا تطبع كلمة المرور (`MaskHost` يطبع اسم القاعدة فقط).
- **بناء .NET (compile-only):** أداة `LegacyExecutionFixture` = Build succeeded، 0/0. خواص الكيانات مطابقة (`ReportSubmission`/`SubmissionFieldValue`/`ApprovalStep` + DbSets `db.Users`/`db.ReportSubmissions`/`db.SubmissionFieldValues`/`db.ApprovalSteps`).

### 12.3 البوّابة النهائية بعد الإغلاق (8 بنود)

| البند | الحالة | التبرير |
|---|---|---|
| 1. Scripts syntactically safe | **GO** | `bash -n` 9/9؛ strict-mode 9/9؛ no-eval/no-rm-rf/no-insecure-TLS؛ masking. |
| 2. Scripts operationally complete | **GO** | `05` استدعاءات API فعلية؛ `04` أرشفة + تفويض بذر تاريخيّ لأداة .NET؛ `03` سِكيمة Taxonomy مؤكَّدة (170/19). |
| 3. Legacy fixture executable | **GO** | أداة `LegacyExecutionFixture` تُبنى نظيفة (0/0)؛ seed/verify/cleanup + idempotent + dry-run افتراضيّ + حارس اتصال؛ لا احتساب مزدوج (عزل هيكليّ). |
| 4. UAT fixture executable | **GO** | `05` قابل للتنفيذ فعليًّا عبر API الرسميّ؛ حارس بريد `@uat.local`؛ verify/cleanup حقيقيّان. |
| 5. Staging env accurate | **GO** | القالب يحمل الفعّالة فقط (JWT/CORS/CS/Seed Admin)؛ حُذفت الخامدة (DataProtection/Cookie/Scheduler/ExternalIntegrations) مع توثيق. |
| 6. Cutover reversible | **GO** | env-swap ذرّيّ + restart، القاعدة القديمة سليمة؛ `06` preflight يفرض `Seed__AdminPassword`؛ `08` رجوع تلقائيّ للقطة env السابقة بمهلة/poll. |
| 7. Ready for server-side PLAN audit | **CONDITIONAL** | الحزمة جاهزة محليًّا؛ يبقى مُعلَّقًا على تزويد المالك ببيانات خادم TEST المؤكَّدة (host/user/key/hostname/service/env-file) لنسخ `/root/uat-prep-review/` + shellcheck خادميّ (§1/§2). **لن يُخمَّن هدف SSH.** |
| 8. Ready for Phase 2 | **CONDITIONAL** | الحواجز المحلية أُغلِقت؛ بدء المرحلة الثانية يبقى محكومًا بموافقة المالك الصريحة + تنفيذ تدقيق PLAN الخادميّ (البند 7) على بيئة TEST المؤكَّدة. |

---

**لا Commit، لا Push، لا تغيير حيّ.** التقرير معروض للاعتماد. الحواجز **المحلية** أُغلِقت (بنود 1–6 = GO)؛ البندان الخادميان (7–8) يبقيان CONDITIONAL على بيانات خادم TEST + موافقة صريحة. المرحلة الثانية التنفيذية لا تبدأ إلا بعد ذلك.
