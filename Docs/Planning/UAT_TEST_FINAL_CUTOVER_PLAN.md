# UAT TEST — Final Cutover Plan (Phase 3 — تخطيط ومراجعة فقط)

> **حالة الوثيقة:** خطة معتمدة للمراجعة فقط. **لا Cutover، لا Backup فعلي، لا تعديل خدمة/env، لا Commit/Push.** التنفيذ يتطلّب موافقة صريحة منفصلة.
> **آخر تثبيت حالة (قراءة فقط):** 2026-07-12.

---

## 1. الحالة الحالية (مُثبَّتة قراءةً فقط)

| العنصر | القيمة |
|---|---|
| Hostname | `srv1747233` |
| خدمة TEST | `khubara-reporting-test` (active، MainPID=544436، NRestarts=0) |
| المنفذ الداخلي | `127.0.0.1:5091` |
| البيئة الحالية | `ASPNETCORE_ENVIRONMENT=Development` |
| القاعدة الحالية | `reporting_test_rc` |
| env-file | `/etc/khubara-reporting-test.env` (hash `7d412075…f59a90`) |
| runtime | `/opt/reporting-test/publish` (Reporting.Api.dll hash `32d2df74…68088e`) |
| frontend bundle | `/opt/reporting-test/frontend/dist/assets/index-DlS_VbOD.js` (hash `85b58e92…9955ff`) |
| health | `{"status":"ok","service":"reporting-api"}` |
| Production / RC | `reporting-api` active / `khubara-reporting-rc` active — منفصلتان، لم تُمسّا |

### قاعدة UAT الجديدة `reporting_test_uat` (جاهزة)

| البند | القيمة | البند | القيمة |
|---|---|---|---|
| migrations | 30 | Legacy submissions (W10+W11) | 12 |
| UAT users (@uat.local) | 6 | Project-First submissions (W28) | 1 |
| departments | 2 | Archived Legacy templates | 6 |
| teams | 2 | email_outbox | 0 |
| clients | 2 | notifications | 0 |
| projects | 3 (مؤرشف ×1) | UAT Admin | مؤمَّن |
| workstreams | 1 | Fixture idempotency | مُتحقَّق (Run#1=Run#2، 0 إنشاءات، 0×409) |
| deliverables | 2 (post ×1 + reel ×1) | | |

كل العناصر مطابقة للمواصفة ⇒ لا فرق ⇒ لا NO-GO على تثبيت الحالة.

---

## 2. الهدف النهائي للـCutover

تحويل **خدمة TEST فقط**:

| البُعد | من | إلى |
|---|---|---|
| Environment | `Development` | `Staging` |
| Database | `reporting_test_rc` | `reporting_test_uat` |

**يبقى دون تغيير:** نفس Backend runtime (`/opt/reporting-test/publish`) · نفس Frontend bundle (`index-DlS_VbOD.js`) · نفس Nginx/SSL/DNS · نفس وحدة systemd · Email disabled · Reminders disabled · EmailNotifications=DryRun · القنوات الخارجية معطّلة · RC وProduction دون أي تغيير.

آلية التحويل الوحيدة = **تبديل env-file ذرّيًّا + restart لخدمة TEST فقط**. لا نشر runtime جديد، لا بناء frontend جديد، لا Migration يدوي (الـ30 مطبّقة سلفًا؛ الإقلاع = no-op عليها).

---

## 3. مراجعة env-file (Delta) — أسماء المفاتيح وحالتها فقط (بلا قيم سرّية)

| المفتاح | الحالي | المستهدف | القرار |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Development | **Staging** | **تغيير** |
| `ConnectionStrings__Default` (Database=) | reporting_test_rc | **reporting_test_uat** (Username=`reporting_test_uat_app` + كلمة الدور) | **تغيير** |
| `Jwt__Key` | موجود (طول 64، قويّ) | مستقل وقويّ | **قرار**: تدوير لمفتاح UAT مستقل (موصى) أو إبقاء الحالي (يمرّ بوّابة ≥32) |
| `Jwt__Issuer` / `Jwt__Audience` | khubara-reporting-test | كما هو | إبقاء |
| `Cors__AllowedOrigins__0` | https://test.emarketingacademy.net | كما هو | إبقاء (يمرّ البوّابة: غير فارغ، بلا wildcard/localhost) |
| `Seed__AdminEmail` | admin@test.local | **مواءمة** | **قرار (مهم)**: أدمن UAT المؤمَّن = `admin@marketingexperts.local`؛ إبقاء `admin@test.local` سيبذر أدمن ثانيًا عند الإقلاع |
| `Seed__AdminPassword` | موجود | قويّ (≥12، تعقيد، ليس Admin#12345) | إبقاء/تحقّق — يفرضه حارس 06 |
| `Email__Enabled` | false | false | إبقاء |
| `Reminders__Enabled` | false | false | إبقاء |
| `EmailNotifications__Mode` | DryRun | DryRun | إبقاء |
| `Email__*` (SmtpHost/Provider فارغان، Password/Username/From*/Ssl/StartTls) | موجودة (خاملة) | كما هي | إبقاء (خاملة لأن Enabled=false) |
| `App__BaseUrl` | https://test.emarketingacademy.net | كما هو | إبقاء |
| `ASPNETCORE_URLS` | http://127.0.0.1:5091 | كما هو | إبقاء (نفس المنفذ) |
| DataProtection__* / Cookie__* | غير موجودة | غير موجودة | إبقاء (الـruntime يعتمد JWT Bearer فقط — لا تُضَف مفاتيح وهمية) |
| Scheduler__* / ExternalIntegrations__* | غير موجودة | غير موجودة | إبقاء (لا يقرؤها الكود) |

**ملاحظة بنيوية حرجة:** `staging.env.template` هو **Delta توضيحي** لا ملفًّا كاملًا، لكن `06` يُركّب `NEW_ENV_SRC` **كملف env كامل** (`install -m600 $NEW_ENV_SRC $TEST_ENV_FILE`). لذا يجب على المنفّذ بناء **env-file كامل** = (المفاتيح الحالية غير المتغيّرة + الـDelta أعلاه)، لا استخدام القالب وحده. أي مفتاح حالي مفقود من الملف الجديد سيُفقَد بعد التركيب الذرّي.

**البوّابات التي يفرضها 06 قبل التركيب:** ASPNETCORE_ENVIRONMENT=Staging · CORS غير فارغ/بلا wildcard/localhost · Jwt__Key ≥32 وبلا 'dev-only' · Seed__AdminEmail موجود · Seed__AdminPassword قويّ (≥12+تعقيد، ليس placeholder، ليس Admin#12345) · Database=reporting_test_uat · لا اسم محظور (prod/rc) · القاعدة موجودة · Backup للحزمة موجود.

---

## 4. تسلسل Cutover (من `06-cutover-test-to-uat.sh`)

Preflight → حفظ env السابق (`${TEST_ENV_FILE}.pre-uat-<STAMP>`، 600) → تركيب env الجديد ذرّيًّا (`install -m600`) → `systemctl restart khubara-reporting-test` → انتظار active (≤45s) → انتظار Health=ok (≤60s، poll 3s) → تشغيل `07-health-validation.sh` (Health/Auth/API/SignalR/Email-safety/Project-First/Legacy/Rollup).

عند الإقلاع: `MigrateAsync` (30 هجرة = no-op) + Catalog/Template/Identity Seeders (idempotent، OrgSeeder لا يعمل).

---

## 5. تسلسل Rollback (من `08-rollback-test.sh`)

استعادة env السابق (`install -m600 <pre-uat> $TEST_ENV_FILE`) → `systemctl restart khubara-reporting-test` → انتظار active (≤45s) → Health=ok (≤60s). القاعدة القديمة `reporting_test_rc` **لم تُحذف/تُعدَّل** ⇒ الرجوع = تبديل env + restart (ثوانٍ). **لا يحذف `reporting_test_uat` ولا `reporting_test_rc`، لا يعدّل بيانات، لا يمسّ Prod/RC.**

---

## 6. جدول Cutover ↔ Rollback

| # | خطوة Cutover | الأثر | Rollback المقابل | زمن الرجوع | دليل التحقق |
|---|---|---|---|---|---|
| 0 | Backup نهائي (Release ID جديد) | قراءة فقط | لا حاجة (لا تغيير) | — | MANIFEST + sha256 |
| 1 | حفظ env السابق → `.pre-uat-<STAMP>` | إنشاء نسخة | حذف النسخة (اختياري) | فوري | ملف موجود 600 |
| 2 | تركيب env الجديد ذرّيًّا | env يشير لـuat/Staging | استعادة `.pre-uat-<STAMP>` | ثوانٍ | hash env |
| 3 | restart الخدمة | إقلاع على uat/Staging | restart بعد استعادة env | ≤45s | `is-active` |
| 3.1 | انتظار active | — | — | — | systemctl |
| 3.2 | انتظار Health | — | — | — | `/health`=ok |
| 4 | 07: Auth/API/SignalR/Email/PF/Legacy/Rollup | قراءة (login) | — (فشل حرج ⇒ auto-rollback) | ≤60s | rc=0 |
| 5 | Final Acceptance | قبول بشري | استعادة env + restart | ثوانٍ | Checklist |

لا خطوة بلا Rollback مقابل ⇒ **لا Blocker من جهة التغطية**.

---

## 7. نافذة التنفيذ (تقديرات)

| المرحلة | زمن تقديري |
|---|---|
| Preflight (06 plan + بوّابات) | ~1–2 دقيقة |
| Backup نهائي (01 apply) | ~2–5 دقائق (حسب حجم dump/tgz) |
| تعديل/تركيب env | ~30 ثانية |
| Restart | ~5–15 ثانية |
| Health checks | ≤60 ثانية |
| Smoke tests (07 + يدوي) | ~3–5 دقائق |
| Decision Gate | حسب المالك |
| Rollback (عند اللزوم) | ~15–45 ثانية |

- **Downtime المتوقّع:** من restart حتى Health=ok ≈ **10–30 ثانية**.
- **أقصى Downtime مقبول:** **5 دقائق** (بعده = قرار Rollback).
- **نقطة قرار Rollback:** فشل active خلال 45s أو Health خلال 60s أو فشل Auth الحرج في 07.
- **مسؤول GO/NO-GO:** مالك النظام (صاحب الموافقة الصريحة).

---

## 8. Final Pre-Cutover Backup (المطلوب قبل التحويل مباشرة)

عبر `01-backup-test.sh --apply` بـ **Release ID جديد** (مثلًا `UAT-CUTOVER-<STAMP>`): Database (`reporting_test_rc` custom dump) · Backend runtime (tgz) · Frontend dist (tgz) · env-file (600) · Nginx conf · uploads · migration history · runtime SHA256 · frontend bundle SHA256 · health.json · service-status · MANIFEST. آخر Backup موجود = `UAT-TEST-PREP-RC4-20260712-074118` (يحوي كل المكوّنات) — **لكن يلزم Backup طازج لحظة الـCutover.** **لا يُنفَّذ الآن.**

---

## 9. Smoke Tests بعد Cutover

**Critical:** `/health` · Login Admin · `/api/auth/me` · Database=reporting_test_uat · migrations=30 · Environment=Staging · لا إرسال Email · لا Reminder jobs · لا تكامل خارجي.

**Functional:** users/roles · clients · projects · workstreams · deliverables · taxonomy · Project-First submission · Project-First aggregation (W28 rowCount=1) · Legacy historical read (W10/W11) · archived-template guard · role/scope visibility · SignalR.

**UI:** login page · dashboard · project pages · workstream pages · deliverables · reporting pages · لا تسريب localhost/prod.

(تفاصيل الأوامر في RUNBOOK.)

---

## 10. GO / NO-GO Gates

| بوّابة | الشرط | فشلها |
|---|---|---|
| G0 Preflight | حالة مطابقة + بوّابات 06 تمرّ | NO-GO (إيقاف قبل أي تغيير) |
| G1 Backup verified | Backup طازج كامل + sha256 | NO-GO (لا تكمل) |
| G2 UAT DB ready | القاعدة موجودة + 30 هجرة + عدّادات §1 | NO-GO |
| G3 env ready | env كامل يمرّ كل بوّابات 06 | NO-GO |
| G4 cutover applied | تركيب ذرّي + restart | فشل ⇒ **Auto-Rollback** |
| G5 service healthy | active ≤45s + Health ≤60s | فشل ⇒ **Auto-Rollback** |
| G6 auth healthy | login admin 200 + token | فشل ⇒ **Auto-Rollback** |
| G7 functional smoke | 07 rc=0 (API/SignalR/PF/Rollup) | rc=2 ⇒ **توقّف + قرار يدوي** (لا auto) |
| G8 legacy verification | W10/W11 مقروءة، خارج PF | فشل ⇒ قرار يدوي |
| G9 email safety | Enabled=false + outbox=0 | ظهور نشاط ⇒ **Rollback فوري** |
| G10 final acceptance | قبول المالك | NO-GO ⇒ Rollback |

---

## 11. معايير Rollback الفوري

Rollback فوري عند: الخدمة لا تُقلع · Health لا يصل 200 ضمن المهلة · login Admin يفشل · اتصال بقاعدة خاطئة · migrations ≠ 30 · فشل بوّابة JWT/CORS عند الإقلاع · ظهور نشاط Email/SMTP · runtime/frontend hash غير متوقّع · 500 متكرر في مسارات حرجة · Legacy أو Project-First غير مقروءين. الملاحظات غير الحرجة تُسجَّل ولا تستدعي Rollback إلا بقرار المالك.

---

## 12. خطة المراقبة بعد Cutover

- **أول 15 دقيقة:** service status · logs مباشرة · 500 errors · auth failures · email_outbox (=0) · notifications · جاهزية Rollback.
- **أول ساعة:** استقرار الخدمة · أزمنة الاستجابة · اتصالات DB · تدفّق التسليمات · أي استثناءات.
- **أول يوم UAT:** ملاحظات المستخدمين · تراكم outbox (يجب 0) · أخطاء متكرّرة · قرار الإبقاء/الرجوع.

---

## 13. المخاطر و Blockers

| # | المخاطرة | التخفيف | مستوى |
|---|---|---|---|
| R1 | env جديد ناقص مفتاحًا حاليًّا (القالب Delta لا كامل) | بناء env كامل + مراجعة قائمة المفاتيح قبل التركيب | **عالٍ (قرار)** |
| R2 | `Seed__AdminEmail=admin@test.local` يبذر أدمن ثانيًا مخالفًا للمؤمَّن `admin@marketingexperts.local` | مواءمة Seed__AdminEmail/Password مع الأدمن المؤمَّن قبل الـCutover | **عالٍ (قرار)** |
| R3 | Jwt__Key غير مدوّر بين rc/uat | تدوير لمفتاح UAT مستقل (موصى) | متوسط (قرار) |
| R4 | Seeders تكتب عند أول إقلاع | متوقّع ومقصود (idempotent، OrgSeeder معطّل) — ليس Blocker | منخفض |
| R5 | تجاوز أقصى Downtime | Auto-Rollback مضبوط بمهل 45/60s | منخفض |

**لا Blocker تقني في السكربتات** (تغطية Rollback كاملة). البنود R1/R2/R3 = **قرارات env تُحسم قبل GO**، ليست عيوبًا في الأدوات.

---

## 14. Phase 3A — حزمة env النهائية + Backup ما قبل Cutover (مُنفَّذة، قراءة/تجهيز فقط — لا Cutover)

> **آخر تثبيت (Phase 3A):** 2026-07-12. تُغلَق قرارات R1/R2/R3 بموافقة المالك، ويُجهَّز env المستهدف الكامل + Backup نهائي طازج. **لم يُنفَّذ Cutover، ولم يُعدَّل env الحي، ولم تُعَد تشغيل الخدمة.**

### 14.1 قرارات المالك المعتمَدة (إغلاق R1/R2/R3)

| # | القرار | الأثر |
|---|---|---|
| R1 (مُغلَق) | env المستهدف **نسخة كاملة** من env الحالي (لا Delta ناقصة) | نُسِخ 22 مفتاحًا كاملًا، تغيّر 4 فقط، 18 دون مساس |
| R2 (مُغلَق) | Admin الموحّد في UAT = `admin@marketingexperts.local`؛ **مُنِع** استخدام/بذر `admin@test.local` | `Seed__AdminEmail=admin@marketingexperts.local` + كلمة UAT المؤمَّنة |
| R3 (مُغلَق) | **عدم تدوير** `Jwt__Key` في أول Cutover — إبقاء JWT الحالي لـTEST (معزول عن Prod/RC؛ تقليل المتغيّرات) | سطر `Jwt__Key` مطابق بايتيًّا (sha256 `379cb770…c828d1ad`) قبل/بعد؛ التدوير مؤجَّل لمرحلة لاحقة بعد استقرار UAT |

### 14.2 حزمة env المستهدفة المعتمَدة

| البند | القيمة |
|---|---|
| المسار | `/root/uat-prep-runtime/khubara-reporting-test.uat.env` (خارج Git) |
| الصلاحيات/الملكية | `600` · `root:root` |
| عدد المفاتيح | 22 (مطابق للحيّ؛ md5 مجموعة المفاتيح متطابق) |
| المتغيّر (4 فقط) | `ASPNETCORE_ENVIRONMENT=Staging` · `ConnectionStrings__Default → reporting_test_uat / reporting_test_uat_app` · `Seed__AdminEmail=admin@marketingexperts.local` · `Seed__AdminPassword=<UAT المؤمَّنة>` |
| غير المتغيّر (18) | URLs · App__BaseUrl · CORS · كل مفاتيح Email (معطّلة) · Reminders (معطّل) · EmailNotifications (DryRun) · Jwt (Key/Issuer/Audience) |
| المقارنة الآمنة (§3) | added=0 · removed=0 · changed=4 · unchanged=18 — لا فرق غير متوقّع، لا Blocker |
| بوّابات الإقلاع الساكنة (§4) | 11/11 PASS (Jwt قوي غير placeholder · CORS صالح · اتصال كامل · Seed قوي ليس Admin#12345 · Email/Reminders معطّلة · DryRun · Staging) |

### 14.3 اختبار Runtime المؤقت (§5 — على منفذ حرّ، بلا systemd، بلا مساس بالحيّ)

عملية مؤقتة بـenv المستهدف على `127.0.0.1:5099` (منفذ حرّ)، Staging، JWT الحالي، DB=`reporting_test_uat`:
الإقلاع ناجح · migrations لا-عمل (30 مطبَّقة) · `/health`=200 · login `admin@marketingexperts.local`=200 · `/me`=200 بالإيميل الصحيح · Email معطّل · **لا `admin@test.local`** · لا حساب Seeder إضافي · users=7 (6 @uat.local + admin) · Legacy=12 · Project-First=1 · ثم أُوقِفت العملية وحُرِّر المنفذ. **الحيّ لم يُمَسّ** (MainPID/NRestarts ثابتان، health=ok).

### 14.4 Backup النهائي الطازج (§6 — عملية الكتابة المصرّح بها الوحيدة)

| البند | القيمة |
|---|---|
| Release ID | `UAT-TEST-FINAL-PRECUTOVER-20260712-100755` |
| الوجهة | `/root/db-backups/uat-prep/UAT-TEST-FINAL-PRECUTOVER-20260712-100755` (`700`) |
| المصدر | القاعدة الحيّة `reporting_test_rc` (اعتماد التطبيق، لا postgres superuser) |
| المكوّنات | DB dump (`453938` bytes، 374 كائن، `pg_restore --list` OK) · backend-publish.tgz (182) · frontend-dist.tgz (9) · uploads.tgz (3) · env-file.bak (`600`) · nginx.conf.bak · migration-history (30) · service-status · health(ok) · SHA256(backend 37/frontend 1) · MANIFEST (`600`، 11/11) |
| سلامة (§7) | كل الأرشيفات مقروءة · dump غير فارغ وقابل للاستعادة · env-file.bak `600` · MANIFEST كامل ومتطابق sha (env-file.bak=`7d412075…f59a90`) · لا أسرار مطبوعة |
| Backup السابق | `UAT-TEST-PREP-RC4-20260712-074118` — لم يُحذَف (محفوظ) |

### 14.5 إثبات ثبات TEST (§8 — Before/After)

| العنصر | Before | After | الحالة |
|---|---|---|---|
| MainPID / NRestarts | 544436 / 0 | 544436 / 0 | ثابت |
| env-file hash | `7d412075…f59a90` | `7d412075…f59a90` | ثابت |
| runtime hash | `32d2df74…68088e` | `32d2df74…68088e` | ثابت |
| frontend bundle | `85b58e92…9955ff` (index-DlS_VbOD.js) | نفسه | ثابت |
| health | ok | ok | ثابت |
| `reporting_test_rc` size / migrations | 12524567 / 30 | 12524567 / 30 | ثابت |
| Production / RC | active / active | active / active | ثابت |
| `reporting_test_uat` (migrations/users) | 30 / 7 | 30 / 7 | لم تُمَسّ |

التغييرات المسموحة الوحيدة التي حدثت: (1) إنشاء env المستهدف خارج Git، (2) إنشاء Backup النهائي، (3) سجلّ اختبار runtime مؤقت مُنقّى. **لا تعديل env حي، لا Restart، لا Seeder متعمّد، لا مساس Prod/RC.**

### 14.6 بوّابة ما قبل Cutover

- ✅ حزمة env النهائية جاهزة ومعتمَدة (كاملة، 4 تغييرات فقط).
- ✅ مواءمة بذر الأدمن (admin@marketingexperts.local، لا admin@test.local).
- ✅ قرار JWT مُطبَّق (إبقاء الحالي).
- ✅ اختبار الإقلاع المؤقت ناجح (Staging + UAT DB).
- ✅ Backup نهائي طازج مكتمل ومُتحقَّق السلامة.
- ✅ TEST الحيّة دون تغيير.
- **Safe to execute cutover now: NO-GO** — التنفيذ ينتظر موافقة مستقلة نهائية منفصلة.
