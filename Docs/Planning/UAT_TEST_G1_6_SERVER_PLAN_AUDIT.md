# UAT_TEST — G1.6: تدقيق PLAN على خادم TEST الموثّق (Server-Side PLAN Audit)

> **الطبيعة:** تدقيق **قراءة فقط** على خادم TEST الحيّ. لم يُنفَّذ أي كتابة، ولا Backup فعلي، ولا إنشاء قاعدة/دور، ولا Restart، ولا Migration، ولا Seed، ولا Cutover، ولا Rollback، ولا `--apply`، ولا `OPS_ALLOW_WRITE=1`، ولا إدخال كلمة مرور حقيقية، ولا تعديل env-file، ولا Commit/Push.
> **التاريخ:** 2026-07-12 · **المرجع:** `develop @ ffb511906f0b523ebf59fbfa27a51be66189109a` · **يسبقه:** G1.5 (إغلاق الحواجز المحلية — معتمَد).

---

## 1. هدف الخادم الموثّق (إثبات قاطع)

الاتصال تمّ بنفس الطريقة الموثّقة في الفحص الحيّ السابق حصرًا (لا تخمين):
`ssh -i ~/.ssh/academy_vps_ed25519 -o StrictHostKeyChecking=no root@187.127.72.232`

| البند | القيمة المُثبَتة | المصدر |
|---|---|---|
| Host alias الموثّق | `root@187.127.72.232` (مفتاح `academy_vps_ed25519`) | ذاكرة العمل الحيّ السابق |
| Hostname الفعلي بعد الاتصال | **`srv1747233`** | `hostname` |
| الدومين المرتبط بـTEST | `test.emarketingacademy.net` | نشر سابق موثّق |
| اسم خدمة TEST | **`khubara-reporting-test`** (active, enabled, MainPID=544436, NRestarts=0) | `systemctl` |
| منفذ TEST الداخلي | `5091` (health = `{"status":"ok","service":"reporting-api"}`) | `/etc/khubara-reporting-test.env` + curl |
| env-file TEST | `/etc/khubara-reporting-test.env` (600 root:root، `ASPNETCORE_ENVIRONMENT=Development`) | `systemctl show` + قراءة مفتاح واحد بلا سرّ |
| **اسم قاعدة TEST الحالية** | **`reporting_test_rc`** (30 هجرة) | `ConnectionStrings__Default` (اسم القاعدة فقط، بلا كلمة مرور) |

**تأكيد أن الهدف ليس Production ولا RC:**
- Production منفصلة: خدمة `reporting-api` (5090)، قاعدة `reporting_prod`، env `/etc/reporting-api.env`، دومين `reports.emarketingacademy.net` — **لم تُمَسّ**.
- RC منفصلة: خدمة `khubara-reporting-rc` (5092)، قاعدة `reporting_rc`، env `/etc/khubara-reporting-rc.env` — **لم تُمَسّ**.
- العزل على مستوى الخدمة/القاعدة/الـenv على VPS مشترك واحد. كل عمليات هذا التدقيق استهدفت TEST (`reporting_test_rc` / `khubara-reporting-test`) فقط، وكلها قراءة.

قوائم PostgreSQL المُثبَتة (6 قواعد): `LMS_EMA`, `LMS_EMA_STAGING`, `postgres`, `reporting_prod`, `reporting_rc`, `reporting_test_rc`.
- `reporting_test_uat` = **غير موجودة** (0) ✓ · دور `reporting_test_uat_app` = **غير موجود** (0) ✓ (الحالة الصحيحة قبل المرحلة 2).

**الحكم: الهدف مُثبَت بشكل قاطع = TEST (`reporting_test_rc` / `khubara-reporting-test` / `srv1747233`).**

---

## 2. نقل نسخة المراجعة + تطابق SHA256

نُقلت حزمة `Ops/TestUatPreparation/` إلى `/root/uat-prep-review/` (معزولة، غير runtime، غير مرتبطة بـsystemd، بلا Secrets، كل السكربتات **644 غير قابلة للتنفيذ**). كل الـ12 ملفًا SHA256 **متطابق محلي == خادم**:

| الملف | SHA256 | تطابق |
|---|---|---|
| 00-common.sh | `83c7a66b…7ba` | ✓ |
| 01-backup-test.sh | `3fb42a13…4f4` | ✓ |
| 02-create-uat-db.sh | `c7614fe7…73a` | ✓ |
| 03-validate-uat-db.sh | `4b98715d…640` | ✓ |
| 04-seed-legacy-fixture.sh | `24c9925d…d6c` | ✓ |
| 05-seed-uat-fixture.sh | `10dcd660…fb9` | ✓ |
| 06-cutover-test-to-uat.sh | `f66a3f17…97e` | ✓ |
| 07-health-validation.sh | `2695cda9…5d5` | ✓ |
| 08-rollback-test.sh | `9a65c99d…1ba` | ✓ |
| README.md | `6af6d779…471` | ✓ |
| config.env.template | `b917e412…8d8` | ✓ |
| env/staging.env.template | `d65d3f60…1d9` | ✓ |

ملاحظة: نظّفت ملفات AppleDouble (`._*`) الناتجة عن tar على macOS — ليست جزءًا من الحزمة.

---

## 3. ShellCheck

**`shellcheck` غير مُثبَّت على الخادم ⇒ `UNAVAILABLE`.** لم يُثبَّت (التزامًا بالتعليمات). نُفِّذت **مراجعة يدوية مكافئة**:

| فحص | النتيجة |
|---|---|
| `bash -n` (صحّة نحوية) لكل الـ9 سكربتات | **PASS 9/9** |
| وجود `set -Eeuo pipefail` | **9/9** |
| `eval` | 0 |
| `rm -rf` | 0 |
| `curl -k` / `--insecure` / `--no-check-certificate` | 0 |
| مراجع الحُرّاس (guard_/require_write_enabled/require_real_config/FORBIDDEN_NAME_REGEX) | 43 |
| `rm -f` الوحيدة (04/05:152) | على ملف `mktemp` مؤقّت مقتبس — آمنة |

---

## 4. نتيجة PLAN لكل سكربت (بلا `--apply`، بلا `OPS_ALLOW_WRITE`، config placeholder افتراضي)

| السكربت | Exit | القاعدة/الخدمة المستهدفة | أسرار؟ | مسارات؟ | حُرّاس prod/rc | أمر غير متوقع؟ | قابلية العكس | التصنيف |
|---|---|---|---|---|---|---|---|---|
| 01-backup-test | **1** | `reporting_test_rc` (قراءة) + كتابة نسخ في مرحلة apply | لا | صحيحة | فعّالة | **متغيّر غير مُعرَّف** `DATAPROTECTION_KEYPATH` تحت `set -u` (heredoc العرض) | نسخ فقط | **CONDITIONAL GO** |
| 02-create-uat-db | 0 | ينشئ `reporting_test_uat` + دور `reporting_test_uat_app` (apply فقط) | كلمة الدور = `<generated-at-runtime>` | صحيحة | فعّالة | لا | إنشاء فقط (لا DROP) | **GO** |
| 03-validate-uat-db | 0 | `reporting_test_uat` (SELECT فقط) | لا | صحيحة | فعّالة | لا | قراءة فقط | **GO** |
| 04-seed-legacy-fixture | 0 | API TEST + أداة dotnet خارجية (تاريخي) | لا (توكن لا يُطبع) | صحيحة | فعّالة | لا | cleanup للـfixture فقط | **GO** |
| 05-seed-uat-fixture | 0 | API TEST (بريد `@uat.local` حصرًا) | كلمات المرور تُكتب لملف 600 لا تُطبع | صحيحة | فعّالة | لا | cleanup للـfixture فقط | **GO** |
| 06-cutover-test-to-uat | 0 | env TEST + restart `khubara-reporting-test` | لا (كل الأسرار مُقنَّعة/مفحوصة بلا طبع) | صحيحة | فعّالة | لا | Auto-Rollback عبر 08 | **GO** |
| 07-health-validation | 0 | API TEST (قراءة + login admin) | لا | صحيحة | فعّالة | لا | قراءة فقط | **GO** |
| 08-rollback-test | 0 | استعادة env السابق + restart TEST | لا | صحيحة | فعّالة | لا | استعادة ذرّية | **GO** |

**العيب الوحيد (01):** `config.env.template` لا يعرّف `DATAPROTECTION_KEYPATH` (المفتاح الوحيد الناقص من أصل 19 مرجعًا في 01)، فيفشل توسّع heredoc خطة العرض تحت `set -u`. **ليس سرًّا ولا كتابة** (انهار قبل أي كتابة؛ PLAN لا يكتب أصلًا). إصلاح محلّي لاحق: إضافة `DATAPROTECTION_KEYPATH=` للقالب أو استخدام `${DATAPROTECTION_KEYPATH:-}` — ويجب أن يعرّفه `config.env` الحقيقي في المرحلة 2.

---

## 5. إثبات عدم حدوث كتابة (Before == After)

| الحقل | قبل | بعد | مطابقة |
|---|---|---|---|
| عدد قواعد PostgreSQL | 6 | 6 | ✓ |
| `reporting_test_uat` موجودة؟ | 0 | 0 | ✓ |
| دور `reporting_test_uat_app`؟ | 0 | 0 | ✓ |
| SHA256 لـenv-file TEST | `7d412075…a90` | `7d412075…a90` | ✓ |
| خدمة TEST — ActiveSince / MainPID | 2026-07-10 01:22:54Z / 544436 | نفسها | ✓ |
| Restart count | 0 | 0 | ✓ |
| SHA256 لـbackend runtime (`Reporting.Api.dll`) | `32d2df74…08e` | `32d2df74…08e` | ✓ |
| SHA256 لـfrontend bundle (`index-DlS_VbOD.js`) | `85b58e92…5ff` | `85b58e92…5ff` | ✓ |
| SHA256 لـfrontend index.html | `bfc7716b…3e6` | `bfc7716b…3e6` | ✓ |
| عدد هجرات قاعدة TEST | 30 | 30 | ✓ |
| report_templates / report_submissions / AspNetUsers | 35 / 16 / 36 | 35 / 16 / 36 | ✓ |
| مجلد Backup جديد (uat-prep) | لا يوجد | لا يوجد | ✓ |
| ملفات runtime جديدة | لا | لا | ✓ |

الإضافة الوحيدة = 8 ملفات `_plan-logs/*.log` **داخل مجلد المراجعة** (سجلّات محلية = الاستثناء المسموح، خالية من الأسرار). **PLAN غير متلِف — مؤكَّد.**

---

## 6. مراجعة PLAN Output (ملخّص التصنيف)

- **GO (7):** 02, 03, 04, 05, 06, 07, 08 — أهداف صحيحة (`reporting_test_uat` للإنشاء/التحقّق، API TEST للـfixtures، خدمة `khubara-reporting-test` للـcutover/rollback)، لا أسرار مطبوعة، حُرّاس prod/rc فعّالون، لا أوامر غير متوقّعة، قابلة للعكس.
- **CONDITIONAL GO (1):** 01 — عيب متانة `DATAPROTECTION_KEYPATH` تحت `set -u` (يتطلّب اكتمال `config.env` الحقيقي). لا كتابة ولا سرّ.
- **NO-GO:** لا يوجد.

---

## 7. مراجعة Fixtures (بلا تشغيل)

| بند | النتيجة |
|---|---|
| أداة `.NET LegacyExecutionFixture` قابلة للبناء | نعم — مُثبَت البناء محليًّا في G1.5 (0 Warning/0 Error، خارج `Reporting.sln`) |
| artifact/مصدر الأداة على الخادم | **غير موجود** — تُشغَّل من checkout خارجيًّا في المرحلة 2 (`dotnet run --project …`) → **CONDITIONAL** |
| Connection guard يرفض Production/RC | نعم — يرفض `reporting_prod`/`reporting_rc`/`_rc_live`/`production` |
| هل يرفض القاعدة الحالية `reporting_test_rc`؟ | **لا** — الحارس **قائمة حظر لا سماح**: `reporting_test_rc` لا يحوي `reporting_rc` فيُسمَح بها. لا يفرض أن يكون الهدف `reporting_test_uat` تحديدًا → **CONDITIONAL** (اعتماد على انضباط المشغّل + توصية تحصين: إضافة تأكيد allowlist للهدف = `reporting_test_uat`) |
| UAT Fixture تستخدم Routes صحيحة | **نعم** — كل الـ11 مسارًا ترجع **HTTP 401 (موجودة + محميّة)** لا 404، على API الحيّة TEST (5091): report-templates، directory/{departments,teams,users}، clients، projects، submissions، reporting/project-execution/{projects,pods}، projects/{id}/workstreams، …/deliverables |
| DTO payloads متوافقة مع الحيّ | **نعم** — `CreateUserRequest(Email,FullName,Password,Roles,DepartmentId,TeamId,ManagerId)`، `CreateProjectWorkstreamRequest(WorkstreamTypeCode,ResponsibleTeamId)`، `CreateWorkstreamDeliverableRequest(DeliverableTypeCode,UsageContextCode,PlannedQuantity)` — كلها تطابق مصدر الـAPI |
| Cleanup محصور بمفاتيح الـfixture | **نعم** — 05: بريد `@uat.local` + أسماء بادئة UAT فقط؛ 04: تسليمات تاريخية للفترات `2026-W10`/`2026-W11` فقط، لا يمسّ أرشفة القوالب |
| IDs/Credentials حقيقية في السكربتات | **لا يوجد** (0 GUID مضمَّن، 0 كلمة مرور نصّية) |
| كتابة على قاعدة TEST الحالية | **لا** في السكربتات؛ الأداة تكتب لِما يشير إليه `ConnectionStrings__Default` (يجب توجيهه لـ`reporting_test_uat` في المرحلة 2 — انظر تحصين allowlist أعلاه) |

---

## 8. مراجعة Cutover (06) ↔ Rollback (08)

| الجانب | Cutover (06) | Rollback (08) | التطابق |
|---|---|---|---|
| env backup path | `${TEST_ENV_FILE}.pre-uat-${STAMP}` (`cp -a` + chmod 600) | يقرأ `ENV_PREV` (أو أحدث `.pre-uat-*`) | متناظر |
| atomic replacement | `install -m 600 $NEW_ENV_SRC ${TEST_ENV_FILE}` | `install -m 600 $ENV_PREV ${TEST_ENV_FILE}` | متناظر (install ذرّي) |
| ownership/permissions | 600 | 600 | متطابق |
| service name | `systemctl restart ${TEST_SERVICE_NAME}` | `systemctl restart ${TEST_SERVICE_NAME}` | محصور بـTEST فقط (محروس) |
| health retries | active 45s + health 60s/3s | active 45s + health 60s/3s | متناظر |
| auth validation | نعم (عبر 07: login admin) | health فقط (رجوع لبيئة معروفة-سليمة) | مقبول |
| auto-rollback conditions | فشل active / فشل health / 07 rc=1 ⇒ Auto-Rollback؛ rc=2 ⇒ توقّف بلا رجوع (قرار بشري) | — (الرجوع هو الفعل الآمن النهائي) | صحيح |
| الحفاظ على القاعدة القديمة | `reporting_test_rc` لا تُمَسّ | صريح: القاعدة القديمة لم تُمَسّ ⇒ رجوع فوري | مضمون |
| حذف تلقائي لأي قاعدة | **لا** — 0 `DROP DATABASE/ROLE/TABLE`، 0 `dropdb`، 0 `DELETE FROM`، 0 `TRUNCATE` في كل السكربتات | لا | مضمون |

**Cutover قابل للعكس بالكامل.**

---

## 9. تنظيف مسار المراجعة

- **الحجم:** 168K · **المحتوى:** 12 ملف نسخة مراجعة (644، بلا أسرار) + 8 سجلّات PLAN محلية (`_plan-logs/`، خالية من الأسرار — فحص = `NO_SECRETS_IN_LOGS`).
- **التصنيف:** **Safe to delete** (معزول، لا يعتمد عليه runtime، لا أسرار). يُمكن **الإبقاء اختياريًّا** كمرجع للمرحلة 2.
- **لم يُحذف** — بانتظار أمر منفصل.

---

## 10. الاختلافات / المسائل / الزمن المتوقّع

**اختلافات بين العقود المحلية والنسخة الحية:** لا اختلاف جوهري. السكربتات مطابقة (SHA256)، وكل مسارات الـAPI و DTO متوافقة مع الحيّ. الفروق الوحيدة تشغيلية متوقّعة: القيم في PLAN كانت placeholder (لا `config.env` حقيقي)؛ منفذ الحيّ 5091؛ قاعدة الحيّ `reporting_test_rc`.

**مسائل Secret/Path:** لا تسريب أسرار في أي مخرجات أو سجلّات. المسار الوحيد ذو الملاحظة = `DATAPROTECTION_KEYPATH` الناقص من القالب (§4).

**Blockers:** **لا يوجد Blocker** (لا كتابة في PLAN، لا سرّ مطلوب).

**الزمن الفعلي المتوقّع للمرحلة التنفيذية (على TEST فقط، متتابع + تحقّق):**
- 01 Backup (DB dump + tar publish/dist + env + hashes): ~3–6 د
- 02 إنشاء UAT DB + دور: ~1 د
- إقلاع أول ضد UAT (MigrateAsync 30 هجرة + Catalog seeders): ~1–3 د
- 03 تحقّق UAT (قراءة): ~1 د
- 04 Legacy fixture (أرشفة 6 قوالب + أداة dotnet للتاريخي): ~3–5 د
- 05 UAT fixture (6 مستخدمين/2 قسم/2 فريق/2 عميل/3 مشاريع + workstreams/deliverables/submission): ~5–10 د
- 06 Cutover (تبديل env + restart + health + 07): ~3–5 د
- 07 Health validation: ~1–2 د
- **الإجمالي:** ~20–35 د عمل فعّال، ونافذة واقعية **45–60 د** مع التحقّق والمراجعة.

---

## Final Gate

| البند | التصنيف | السبب |
|---|---|---|
| **Verified TEST host** | **GO** | `srv1747233` / `khubara-reporting-test` / `reporting_test_rc` / `test.emarketingacademy.net` — ليس prod ولا rc، وكلاهما لم يُمَسّ |
| **Server-side scripts match local hashes** | **GO** | 12/12 SHA256 متطابق تمامًا |
| **PLAN mode non-destructive** | **GO** | Before==After في كل حقل؛ 0 كتابة/قاعدة/دور/restart/backup جديد |
| **Shell safety** | **CONDITIONAL GO** | bash -n 9/9 + strict 9/9 + 0 أنماط خطرة + 43 حارس؛ لكن `shellcheck` UNAVAILABLE (مراجعة يدوية مكافئة) + عيب متانة 01 (`DATAPROTECTION_KEYPATH`) |
| **Fixtures compatible with live API** | **CONDITIONAL GO** | 11/11 route = 401 + DTO مطابقة + cleanup محصور + 0 IDs/creds؛ لكن أداة .NET تحتاج checkout في المرحلة 2 + حارس الاتصال قائمة حظر لا سماح (توصية allowlist للهدف `reporting_test_uat`) |
| **Cutover reversible** | **GO** | 06↔08 متناظران؛ env backup ذرّي، restart محصور بـTEST، auto-rollback على الفشل الحرج، القاعدة القديمة محفوظة، 0 DROP |
| **Safe to begin Phase 2 Backup step only** | **CONDITIONAL GO** | جاهز بعد: (1) إصلاح عيب 01 محليًّا، (2) `config.env` حقيقي مكتمل (يعرّف `DATAPROTECTION_KEYPATH` وكل المفاتيح)، (3) موافقة صريحة — كلها خارج نطاق هذا التدقيق القرائي |
| **Safe to perform full cutover** | **NO-GO** | لم يُجهَّز شيء بعد (لا Backup، لا UAT DB، لا env-file حقيقي، لا fixture)؛ يتطلّب إتمام المراحل السابقة + موافقة صريحة |

---

**لم يُنفَّذ Commit أو Push. لم يبدأ Backup ولا المرحلة 2. التوقّف بعد التقرير.**

---

# ملحق G1.6-B — إغلاق ملاحظات التدقيق (Blocker/Robustness Remediation)

> اعتُمد إغلاق ملاحظات G1.6 قبل أي تنفيذ فعلي. **لم يُبدأ Cutover، ولا إنشاء قاعدة، ولا Fixtures، ولا Restart.** كل العمل أدناه = تعديل مصدر محلي + فحص محلي + تحديث نسخة المراجعة القرائية + إعادة PLAN قرائية.

## ب.1 الملفات المعدَّلة (3)

| الملف | التغيير |
|---|---|
| `Ops/TestUatPreparation/01-backup-test.sh` | جعل `DATAPROTECTION_KEYPATH` اختياريًّا بقيمة فارغة آمنة (`${DATAPROTECTION_KEYPATH:-}`)؛ سطر الخطة يعرض `not configured / not applicable (JWT Bearer only)`؛ وضع apply يسجّل `DP_STATUS` بلا نسخ مسار غير موجود؛ الـManifest يوثّق `DataProtection key ring: <status>` |
| `Ops/TestUatPreparation/config.env.template` | إضافة `DATAPROTECTION_KEYPATH=""` صريحًا (اختياري، فارغ، غيابه ليس Blocker) بتعليق موضِّح |
| `reporting-backend/tools/LegacyExecutionFixture/Program.cs` | استبدال Blocklist بـ**Allowlist صارمة**: القاعدة الوحيدة المسموح بها `reporting_test_uat`؛ override عبر `LEGACY_FIXTURE_ALLOWED_DATABASE` بشرط ألّا يحوي prod/rc ولا يساوي القاعدة القديمة؛ رفض صريح لأي قاعدة غير الهدف؛ helper `ExtractDatabase`؛ يعرض اسم القاعدة فقط لا سلسلة الاتصال |

## ب.2 إصلاح DATAPROTECTION_KEYPATH

- الجذر: تحت `set -u`، heredoc الخطة كان يشير لمتغيّر غير مُعرَّف في القالب ⇒ `unbound variable` (exit 1).
- الحل: قيمة افتراضية فارغة آمنة + عدم محاولة نسخ مسار غير موجود + تسجيل «not configured / not applicable» في الـManifest. **لم يُضَف أي إعداد DataProtection وهميّ إلى الـruntime** (الـruntime يبقى JWT Bearer فقط).
- النتيجة: **PLAN 01 لا يفشل بأي unbound variable** (محليًّا وعلى الخادم، exit=0).

## ب.3 نتيجة Allowlist (حارس Legacy Fixture)

بناء الأداة: **Build succeeded — 0 Warning / 0 Error**. اختبارات الحارس (الحارس يعمل قبل أي اتصال بالقاعدة):

| الحالة | الاسم | النتيجة |
|---|---|---|
| مسموح | `reporting_test_uat` | يمرّ الحارس (يطبع الترويسة + اسم القاعدة المموّه) |
| مرفوض | `reporting_test_rc` (القديمة) | **رُفض، exit=2** — «ليست القاعدة المسموح بها» |
| مرفوض | `reporting_prod` | **رُفض، exit=2** |
| مرفوض | `reporting_rc` | **رُفض، exit=2** |
| تجاوز مرفوض | `LEGACY_FIXTURE_ALLOWED_DATABASE=reporting_rc` | **رُفض** — «القاعدة المسموح بها … تحوي prod/rc» |
| override صالح | اسم UAT بديل (`uat_staging_fresh`) | يمرّ الحارس (لا prod/rc، ليس القديمة) |

## ب.4 نتائج الفحص المحلي

- `bash -n` لكل السكربتات: **9/9 OK** (منها 01 المعدَّل).
- Build `LegacyExecutionFixture` (Release): **0/0**.
- Secret scan للملفات الثلاثة: **لا أسرار** (المطابقة الوحيدة = تعليق «NO SECRETS IN GIT» حميد).
- Guard tests: كما في ب.3 (مسموح=يمرّ، rc/prod/test_rc=رفض exit 2).
- PLAN محلي لـ01: **exit=0**، بلا unbound var، السطر 6 يعرض DataProtection «not configured / not applicable».
- **لا اتصال بالخادم أثناء الفحص المحلي.**

## ب.5 تحديث نسخة المراجعة على TEST + تطابق SHA256

نُقل **الملفان المعدَّلان فقط** (`01-backup-test.sh`، `config.env.template`) إلى `/root/uat-prep-review/`. `Program.cs` ملفّ شجرة مصدر (ليس ضمن حزمة Ops) — يُراجَع/يُبنى محليًّا ويُجلَب عبر checkout في المرحلة 2. **لم يُنفَّذ أي سكربت بوضع apply.**

| الملف | SHA256 محلي | SHA256 خادم | تطابق |
|---|---|---|---|
| `01-backup-test.sh` | `c52c0e04…84cb909b` | `c52c0e04…84cb909b` | ✅ |
| `config.env.template` | `4db13a53…f0cdea37` | `4db13a53…f0cdea37` | ✅ |

## ب.6 إعادة PLAN لسكربت Backup على الخادم

`cd /root/uat-prep-review && bash 01-backup-test.sh` (بلا `--apply`، بلا `OPS_ALLOW_WRITE`، بلا credentials حقيقية — يقع على `config.env.template` مع WARN). **exit=0**. عرض PLAN كل المكوّنات: DB dump، Backend runtime، Frontend dist، env backup، Nginx backup، migration history، SHA256 hashes، Manifest، و**DataProtection = not configured / not applicable**. **لا فشل بأي unbound variable.**

## ب.7 إثبات عدم التغيير (Before == After حول PLAN على الخادم)

| المقياس | Before | After | تطابق |
|---|---|---|---|
| عدد القواعد | 6 | 6 | ✅ |
| وجود UAT database/role | غير موجودَين | غير موجودَين | ✅ |
| env hash (TEST) | `7d412075…f8f59a90` | `7d412075…f8f59a90` | ✅ |
| service MainPID | 544436 | 544436 | ✅ |
| NRestarts | 0 | 0 | ✅ |
| migration count (test_rc) | 30 | 30 | ✅ |
| runtime DLL hash | `32d2df74…368088e` | `32d2df74…368088e` | ✅ |
| frontend bundle hash | `85b58e92…be9955ff` | `85b58e92…be9955ff` | ✅ |
| نسخة Backup فعلية جديدة | 0 | 0 | ✅ (لا نسخ) |

الإضافة الوحيدة داخل مجلد المراجعة = `_plan-logs` (سجلّات PLAN، استثناء مسموح). **لم تُمَسّ Production/RC. لم تُنشأ قاعدة/دور. لا restart. لا env تغيّر.**

## ب.8 Final Gate (بعد الإغلاق)

| البند | التصنيف | السبب |
|---|---|---|
| **Backup script ready** | **GO** | PLAN 01 نظيف (exit=0)، بلا unbound var، DataProtection مُعالَج + مُوثَّق في الـManifest، bash -n سليم |
| **Legacy fixture guard ready** | **GO** | Allowlist صارمة (build 0/0)، `reporting_test_uat` وحدها مسموحة، rc/prod/test_rc مرفوضة exit=2، override محكوم، يعرض اسم القاعدة فقط |
| **Safe to execute Backup step only** | **CONDITIONAL GO** | العيبان أُغلقا؛ يبقى شرطان تشغيليان: (1) `config.env` حقيقي مكتمل خارج Git، (2) موافقة صريحة + `--apply` + `OPS_ALLOW_WRITE=1` |
| **Safe to create UAT database** | **NO-GO** | خارج نطاق هذه الخطوة — لا إنشاء قاعدة الآن |
| **Safe to cut over** | **NO-GO** | لم يُجهَّز شيء بعد — يتطلّب Backup + UAT DB + Fixtures + موافقة صريحة |

---

**لم يُنفَّذ Commit أو Push. لم يُنفَّذ Backup فعليًّا (لا `--apply`، لا `OPS_ALLOW_WRITE`). التوقّف بعد التقرير.**
