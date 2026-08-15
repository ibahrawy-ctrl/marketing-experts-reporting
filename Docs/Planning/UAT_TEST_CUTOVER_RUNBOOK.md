# UAT TEST — Cutover Runbook (Phase 3 — تخطيط ومراجعة فقط)

> **حالة الوثيقة:** Runbook تنفيذيّ مرجعيّ. **لا يُنفَّذ الآن.** التنفيذ يتطلّب موافقة صريحة منفصلة.
> **النطاق:** تحويل **خدمة TEST فقط** من `Development`/`reporting_test_rc` إلى `Staging`/`reporting_test_uat`.
> **ممنوع طوال هذا الـRunbook دون موافقة صريحة:** أي تعديل على Production أو RC · حذف/تعديل أي قاعدة أو بيانات · تعديل Nginx/DNS/SSL · Commit/Push.
> **آخر تثبيت حالة (قراءة فقط):** 2026-07-12.

---

## 0. مبادئ حاكمة

- كل السكربتات تعمل **PLAN افتراضيًّا**؛ الكتابة تتطلّب `--apply` + `OPS_ALLOW_WRITE=1` + تأكيد تفاعليّ.
- آلية التحويل = **تبديل env-file ذرّيًّا + restart لخدمة TEST فقط** — لا نشر runtime، لا بناء frontend، لا Migration يدويّ.
- config الحقيقيّ خارج Git (`/root/uat-prep/config.env`، 600). لا أسرار في المستودع.
- أي فشل في بوّابة حرجة ⇒ **إيقاف قبل أي كتابة** (Preflight) أو **Auto-Rollback** (بعد restart).

---

## 1. الشروط المسبقة (Preconditions) — كلها GO قبل البدء

| # | الشرط | كيفية التحقق (قراءة فقط) |
|---|---|---|
| P1 | Hostname = `srv1747233` | `hostname` |
| P2 | خدمة TEST = `khubara-reporting-test` active، NRestarts=0 | `systemctl is-active khubara-reporting-test` |
| P3 | القاعدة الحالية = `reporting_test_rc` (في env) | `grep '^ConnectionStrings__Default=' /etc/khubara-reporting-test.env` |
| P4 | قاعدة UAT `reporting_test_uat` موجودة + 30 هجرة | `sudo -u postgres psql -d reporting_test_uat -Atc 'SELECT count(*) FROM "__EFMigrationsHistory";'` |
| P5 | عدّادات UAT مطابقة §1 من PLAN (users=6، dept/teams/clients=2/2/2، projects=3، workstreams=1، deliverables=2، legacy=12، PF=1، archived=6، outbox=0، notifications=0) | استعلامات psql قراءة فقط |
| P6 | env جديد كامل جاهز (ليس Delta وحده) ويمرّ بوّابات 06 | مراجعة يدوية + `06 plan` |
| P7 | Admin UAT مؤمَّن؛ حُسِمت مواءمة `Seed__AdminEmail` (R2) | قرار مالك موثّق |
| P8 | Production (`reporting-api`) وRC (`khubara-reporting-rc`) active ومنفصلتان | `systemctl is-active` |
| P9 | Backup طازج للحزمة الجديدة موجود (بعد الخطوة 3) | `ls /root/db-backups/uat-prep` |

> **حاسم (R1):** لأن `06` يُركّب `NEW_ENV_SRC` كملف env **كامل** عبر `install -m600`، يجب أن يحتوي الملف الجديد على **كل المفاتيح الحالية غير المتغيّرة + الـDelta**. أي مفتاح ناقص يُفقَد بعد التركيب.

---

## 2. تجهيز البيئة (خارج Git، قراءة/تحضير فقط)

```bash
# على جهاز التشغيل: مفتاح SSH
ssh -i ~/.ssh/academy_vps_ed25519 -o StrictHostKeyChecking=no root@187.127.72.232

# config الحقيقيّ (موجود مسبقًا، 600، خارج Git)
#   /root/uat-prep/config.env
# بناء env-file الكامل الجديد يدويًّا (دمج الحالي غير المتغيّر + Delta §3 من PLAN):
#   المصدر: /etc/khubara-reporting-test.env (الحالي)
#   الوجهة المؤقتة (600): NEW_ENV_SRC=/root/uat-prep/staging.env.full
# لا تضع أسرارًا في Git. لا تطبع القيم السرّية.
```

الـDelta المطلوب في `NEW_ENV_SRC` (أسماء فقط): `ASPNETCORE_ENVIRONMENT=Staging` · `ConnectionStrings__Default` (Database=`reporting_test_uat`، Username=`reporting_test_uat_app` + كلمة الدور) · `Jwt__Key` (قرار التدوير R3) · `Cors__AllowedOrigins__0=https://test.emarketingacademy.net` · `Seed__AdminEmail`/`Seed__AdminPassword` (مواءمة R2) · `Email__Enabled=false` · `Reminders__Enabled=false` · `EmailNotifications__Mode=DryRun`. **كل المفاتيح الحالية الأخرى تُنسَخ كما هي.**

---

## 3. Backup نهائي طازج (قبل التحويل مباشرة) — لا يُنفَّذ الآن

```bash
# PLAN أولًا (لا كتابة):
cd /root/uat-prep/scripts   # مسار السكربتات على الخادم
./01-backup-test.sh

# APPLY (يتطلّب موافقة صريحة) — Release ID جديد:
RELEASE_ID="UAT-CUTOVER-$(date -u +%Y%m%d-%H%M%S)" \
OPS_ALLOW_WRITE=1 ./01-backup-test.sh --apply
```

يُنتِج: `db-reporting_test_rc.dump` · `backend-publish.tgz` · `frontend-dist.tgz` · `env-file.bak` (600) · `nginx.conf.bak` · `uploads.tgz` · `migration-history.txt` · `backend-assemblies.sha256` · `frontend-bundle.sha256` · `health.json` · `service-status.txt` · `MANIFEST.txt`. **تحقّق من MANIFEST + sha256 قبل المتابعة (G1).**

---

## 4. تسلسل Cutover — عبر `06-cutover-test-to-uat.sh` — لا يُنفَّذ الآن

```bash
# 4.1 PLAN (يعرض الخطة + يشغّل كل بوّابات Preflight بلا كتابة):
NEW_ENV_SRC=/root/uat-prep/staging.env.full ./06-cutover-test-to-uat.sh

# 4.2 APPLY (يتطلّب موافقة صريحة منفصلة):
NEW_ENV_SRC=/root/uat-prep/staging.env.full \
OPS_ALLOW_WRITE=1 ./06-cutover-test-to-uat.sh --apply
```

**ما ينفّذه 06 عند --apply (بالترتيب):**
1. Preflight gates: NEW_ENV_SRC موجود · `ASPNETCORE_ENVIRONMENT=Staging` · CORS (غير فارغ، بلا `*`، بلا localhost/127.0.0.1) · Jwt__Key (طول ≥32، بلا 'dev-only') · Seed Admin (email موجود؛ password ليس placeholder، ليس Admin#12345، ≥12، upper/lower/digit/symbol) · `Database=reporting_test_uat` · لا اسم محظور · القاعدة موجودة · Backup الحزمة موجود. أي فشل ⇒ **إيقاف قبل أي كتابة**.
2. حفظ env السابق: `cp -a /etc/khubara-reporting-test.env /etc/khubara-reporting-test.env.pre-uat-<STAMP>` (600).
3. تركيب ذرّي: `install -m600 $NEW_ENV_SRC /etc/khubara-reporting-test.env`.
4. `systemctl restart khubara-reporting-test`.
5. انتظار active (≤45s)؛ فشل ⇒ **auto_rollback**.
6. انتظار `/health`=ok (≤60s، poll 3s)؛ فشل ⇒ **auto_rollback**.
7. تشغيل `07-health-validation.sh`: rc=0 نجاح · rc=1 حرج ⇒ **auto_rollback** · rc=2 وظيفيّ غير حرج ⇒ **توقّف بلا rollback (قرار بشري)** · غير ذلك ⇒ auto_rollback.

عند الإقلاع (داخل التطبيق): `MigrateAsync` (30 هجرة = no-op) + Catalog/Template/Identity Seeders (idempotent، **OrgSeeder لا يعمل** في Staging).

---

## 5. التحقق (Validation) — عبر `07-health-validation.sh` (قراءة فقط عدا login)

| # | الفحص | الحرجيّة | التوقّع |
|---|---|---|---|
| V1 | `/health` `.status=="ok"` | حرج | 200 ok |
| V2 | `POST /api/auth/login` (admin) → token | حرج | 200 + accessToken |
| V3 | `GET /api/report-templates` | وظيفيّ | 200 |
| V4 | `POST /hubs/notifications/negotiate` | وظيفيّ | 200 |
| V5 | Email safety: `Email__Enabled=false` + `email_outbox` count=0 | وظيفيّ | مطابق |
| V6 | `GET /api/reporting/project-execution/projects` (PF) | وظيفيّ | 200، W28 rowCount=1 |
| V7 | `GET /api/report-templates?status=Archived` (Legacy) | وظيفيّ | 200، 6 مؤرشفة |
| V8 | `GET /api/reporting/project-execution/pods` (Rollup) | وظيفيّ | 200 |

تحقّق قاعدة إضافيّ (قراءة فقط): Environment=Staging · Database=reporting_test_uat · migrations=30 · لا نشاط SMTP.

---

## 6. تسلسل Rollback — عبر `08-rollback-test.sh` — لا يُنفَّذ الآن

```bash
# يدويّ عند اللزوم (أو يستدعيه 06 تلقائيًّا عند فشل حرج):
OPS_ALLOW_WRITE=1 ./08-rollback-test.sh --apply --yes \
  ENV_PREV=/etc/khubara-reporting-test.env.pre-uat-<STAMP>
# إن لم يُمرَّر ENV_PREV، يكتشف آخر .pre-uat-* تلقائيًّا.
```

**ما ينفّذه:** `install -m600 <ENV_PREV> /etc/khubara-reporting-test.env` → `systemctl restart khubara-reporting-test` → active (≤45s) → Health=ok (≤60s). **لا يحذف/يعدّل `reporting_test_uat` ولا `reporting_test_rc`، لا يمسّ Prod/RC.** الرجوع = تبديل env + restart (ثوانٍ).

**تحقّق ما بعد Rollback (يدويّ):** Environment=Development · Database=reporting_test_rc · runtime hash `32d2df74…68088e` · bundle hash `85b58e92…9955ff` · `/health`=ok · login admin 200.

---

## 7. بوّابات القرار (Decision Gates)

| بوّابة | متى | نجاح | فشل |
|---|---|---|---|
| G0 Preflight | قبل أي كتابة | كل بوّابات 06 تمرّ | **NO-GO** (إيقاف) |
| G1 Backup | بعد الخطوة 3 | MANIFEST+sha256 كاملة | **NO-GO** |
| G2 UAT DB | قبل التركيب | 30 هجرة + عدّادات §1 | **NO-GO** |
| G3 env | قبل التركيب | env كامل يمرّ 06 | **NO-GO** |
| G4 cutover | بعد التركيب+restart | تمّ | **Auto-Rollback** |
| G5 healthy | بعد restart | active ≤45s + Health ≤60s | **Auto-Rollback** |
| G6 auth | 07 | login admin 200 | **Auto-Rollback** |
| G7 functional | 07 | rc=0 | rc=2 ⇒ **توقّف + قرار يدوي** |
| G8 legacy | 07 | W10/W11 مقروءة | قرار يدوي |
| G9 email safety | مراقبة | Enabled=false + outbox=0 | **Rollback فوري** |
| G10 acceptance | نهاية | قبول المالك | **NO-GO ⇒ Rollback** |

---

## 8. موافقات المالك (Owner Approvals)

- **A1:** موافقة صريحة على تنفيذ Backup النهائي (الخطوة 3).
- **A2:** موافقة صريحة منفصلة على تنفيذ Cutover (الخطوة 4، --apply).
- **A3:** قرار GO/NO-GO عند G7 (rc=2 غير الحرج).
- **A4:** Final Acceptance (G10) أو أمر Rollback.

كل موافقة موثّقة (مالك + طابع زمني) في ACCEPTANCE_CHECKLIST.

---

## 9. الأزمنة المتوقّعة

| المرحلة | زمن |
|---|---|
| Preflight (06 plan) | ~1–2 دقيقة |
| Backup نهائي | ~2–5 دقائق |
| تركيب env | ~30 ثانية |
| Restart | ~5–15 ثانية |
| Health checks | ≤60 ثانية |
| Smoke (07 + يدوي) | ~3–5 دقائق |
| Rollback (عند اللزوم) | ~15–45 ثانية |

**Downtime المتوقّع:** ~10–30 ثانية. **أقصى مقبول:** 5 دقائق (بعده Rollback).

---

## 10. المخاطر (مختصر — التفصيل في PLAN §13)

| # | المخاطرة | التخفيف |
|---|---|---|
| R1 | env جديد ناقص مفتاحًا (القالب Delta) | بناء env كامل + مراجعة قائمة المفاتيح |
| R2 | `Seed__AdminEmail` يبذر أدمن ثانيًا | مواءمة قبل Cutover |
| R3 | Jwt__Key غير مدوّر | تدوير لمفتاح UAT مستقل (موصى) |
| R4 | Seeders تكتب أول إقلاع | متوقّع (idempotent، OrgSeeder معطّل) — ليس Blocker |
| R5 | تجاوز Downtime | Auto-Rollback بمهل 45/60s |

---

## 11. خطة المراقبة بعد Cutover

- **أول 15 دقيقة:** `systemctl status` · logs مباشرة · 500 errors · auth failures · `email_outbox`=0 · notifications · جاهزية Rollback.
- **أول ساعة:** استقرار · أزمنة الاستجابة · اتصالات DB · تدفّق التسليمات · استثناءات.
- **أول يوم UAT:** ملاحظات المستخدمين · تراكم outbox (يجب 0) · أخطاء متكرّرة · قرار الإبقاء/الرجوع.

---

> **تذكير ختاميّ:** هذا Runbook مرجعيّ فقط. **لا Cutover، لا Backup فعليّ، لا تعديل خدمة/env، لا Commit/Push** قبل موافقة صريحة منفصلة لكل خطوة كتابة.

---

## 12. تحديث Phase 3A — حزمة env جاهزة + Backup ما قبل Cutover (2026-07-12)

**قرارات المالك (تُغلِق R1/R2/R3):** env المستهدف = نسخة كاملة (لا Delta) · Admin موحّد `admin@marketingexperts.local` (**ممنوع** `admin@test.local`) · **عدم تدوير** `Jwt__Key` في أول Cutover (إبقاء الحالي، التدوير مؤجَّل).

**حزمة env المعتمَدة:** `/root/uat-prep-runtime/khubara-reporting-test.uat.env` · `600 root:root` · خارج Git · 22 مفتاحًا · تغيّر 4 فقط (Environment=Staging، ConnectionStrings→uat، Seed__AdminEmail، Seed__AdminPassword) · 18 دون مساس · سطر Jwt__Key مطابق بايتيًّا · بوّابات الإقلاع الساكنة 11/11 PASS.

**اختبار Runtime مؤقت:** على `127.0.0.1:5099` (منفذ حرّ، بلا systemd) — إقلاع Staging ناجح، migrations لا-عمل (30)، `/health`=200، login admin=200، `/me` بالإيميل الصحيح، لا `admin@test.local`، لا حساب Seeder إضافي، users=7. الحيّ لم يُمَسّ.

**Backup النهائي:** `UAT-TEST-FINAL-PRECUTOVER-20260712-100755` — في `/root/db-backups/uat-prep/` (`700`) · DB dump 374 كائن (`pg_restore --list` OK) · كل الأرشيفات مقروءة · migration-history=30 · MANIFEST كامل (11/11) `600` · env-file.bak `600` · لا أسرار مطبوعة · Backup السابق `UAT-TEST-PREP-RC4-20260712-074118` محفوظ.

**ثبات TEST (Before==After):** MainPID=544436/NRestarts=0 · env `7d412075…f59a90` · runtime `32d2df74…68088e` · bundle `85b58e92…9955ff` · health ok · `reporting_test_rc` 12524567/30 · Prod+RC active · `reporting_test_uat` 30/7 لم تُمَسّ.

**بوّابة ما قبل Cutover:** كل بنود P1–P9 المتبقّية مُهيَّأة. **Safe to execute cutover now: NO-GO** — بانتظار موافقة مستقلة نهائية.
