# UAT TEST Preparation — حزمة السكربتات والتنفيذ (G1)

**المعرّف:** `UAT-TEST-PREP-RC4` · **الحالة:** G1 — أدوات ووثائق فقط (لم تُشغَّل على أي بيئة) · **مرجع التكافؤ:** `develop@ffb511906f0b523ebf59fbfa27a51be66189109a`

> ⚠️ **لا شيء في هذه الحزمة يُشغَّل إلا في المرحلة الثانية بعد موافقة المالك الصريحة.** كل السكربتات تعمل في وضع **PLAN افتراضيًّا** (تطبع الخطة ولا تكتب). الكتابة تتطلب `--apply` + `OPS_ALLOW_WRITE=1` + تأكيد تفاعلي بكتابة `EXECUTE`.

---

## 1. السياسة الحاكمة
- TEST = بيئة Staging/UAT للتوسعة الكبيرة. RC = Hotfixes فقط. Production لا تُمس.
- استراتيجية القاعدة = **Option B**: قاعدة UAT جديدة نظيفة `reporting_test_uat`؛ القاعدة الحالية تبقى **أرشيفًا مرجعيًّا** دون حذف/تعديل، وتُستخدم للـRollback.
- Environment المستهدف = **Staging** (تحويل env-file فقط، بلا تعديل شيفرة مصدرية — مثبَت من `Program.cs`).
- بقايا Workstreams/Deliverables القديمة **لا تُنقل**؛ تبقى داخل Backup القاعدة الحالية فقط.
- بيانات Legacy تُعاد بصورة محدودة قابلة للتكرار عبر `04-seed-legacy-fixture.sh`.

## 2. الملفات
| الملف | الوظيفة | يكتب؟ |
|---|---|---|
| `config.env.template` | قالب إعداد (لا أسرار) — يُنسخ لـ`config.env` خارج Git | لا |
| `00-common.sh` | مكتبة مشتركة: guards / logging / confirm / helpers | لا (source) |
| `01-backup-test.sh` | نسخ احتياطي شامل + Manifest + SHA256 | ملفات نسخ فقط (apply) |
| `02-create-uat-db.sh` | إنشاء `reporting_test_uat` + دور `reporting_test_uat_app` | القاعدة الجديدة (apply) |
| `03-validate-uat-db.sh` | تحقّق 30 هجرة + Seeders (SELECT فقط) | لا |
| `04-seed-legacy-fixture.sh` | قوالب Archived + تقارير تاريخية محدودة | القاعدة الجديدة عبر API (apply) |
| `05-seed-uat-fixture.sh` | مستخدمون/أقسام/عملاء/مشاريع/Workstreams/Deliverables/Project-First | القاعدة الجديدة عبر API (apply) |
| `06-cutover-test-to-uat.sh` | Preflight → تركيب env جديد ذرّيًّا → restart → تحقّق | env-file + restart (apply) |
| `07-health-validation.sh` | Health/Auth/API/SignalR/Email/Project-First/Legacy/Rollup | لا (قراءة + login) |
| `08-rollback-test.sh` | استعادة env السابق + restart + Health | env-file + restart (apply) |
| `env/staging.env.template` | Delta لِما يتغيّر في env-file (بوّابتا JWT+CORS) | لا |

## 3. الأسرار المطلوبة وقت التنفيذ (لا تدخل Git أبدًا)
| السرّ | متى | كيف |
|---|---|---|
| `NEW_UAT_ROLE_PASSWORD` | 02 | يُولَّد `openssl rand -base64 24`، يُسلَّم لـenv-file يدويًّا |
| `Jwt__Key` (≥32) | env-file | مفتاح قوي مستقل عن prod/RC |
| `Seed__AdminPassword` | env-file | كلمة مرور admin بيئة UAT |
| `ADMIN_TOKEN` | 04/05 | من login admin، عبر البيئة، لا يُطبع |
| `UAT_*_PASSWORD` | 05 | تُولَّد وتُسلَّم عبر قناة آمنة خارج Git |
| `ADMIN_EMAIL`/`ADMIN_PASSWORD` | 07 | عبر البيئة لفحص Auth |

## 4. Runbook (المرحلة الثانية — بعد الموافقة)

### 4.1 Pre-Execution Checklist
- [ ] موافقة المالك الصريحة على البدء.
- [ ] `config.env` مكتمل (لا `REPLACE_ME_*`) خارج Git، `chmod 600`.
- [ ] `env/staging.env` الحقيقي مُجهَّز (JWT≥32، `Cors__AllowedOrigins__0`، `Seed__Admin*`، Connection String للقاعدة الجديدة).
- [ ] `OPS_EXPECTED_HOSTNAME` مضبوط لاسم خادم TEST.
- [ ] تأكيد أن `TEST_SERVICE_NAME`/`TEST_DOMAIN`/`CURRENT_TEST_DB` كلها لبيئة TEST (لا prod/RC).

### 4.2 Execution Steps (بالترتيب)
```
export OPS_CONFIG=/root/uat-prep/config.env
export OPS_ALLOW_WRITE=1
# 1) نسخ احتياطي شامل
bash 01-backup-test.sh --apply
# 2) إنشاء القاعدة والدور (سجّل كلمة المرور المولّدة في env-file)
bash 02-create-uat-db.sh --apply
# 3) تجهيز env-file الجديد ثم Cutover (يطبّق الهجرات + Seeders عند الإقلاع)
NEW_ENV_SRC=/root/uat-prep/staging.env bash 06-cutover-test-to-uat.sh --apply
# 4) تحقّق القاعدة الجديدة
bash 03-validate-uat-db.sh --apply
# 5) بذر Legacy + UAT (بعد login admin ⇒ ADMIN_TOKEN)
ADMIN_TOKEN=... bash 04-seed-legacy-fixture.sh --apply
ADMIN_TOKEN=... bash 05-seed-uat-fixture.sh --apply
```

### 4.3 Validation
```
ADMIN_EMAIL=... ADMIN_PASSWORD=... bash 07-health-validation.sh --apply --yes
```
Health + Auth (حرجان) → API → SignalR → Email safety → Project-First → Legacy → Rollup.

### 4.4 Rollback
```
bash 08-rollback-test.sh --apply       # يلتقط أحدث *.pre-uat-* تلقائيًّا
# أو: ENV_PREV=/etc/reporting-test.env.pre-uat-<stamp> bash 08-rollback-test.sh --apply
```
القاعدة القديمة لم تُمَسّ ⇒ الرجوع = تبديل env + restart (ثوانٍ).

### 4.5 Acceptance Checklist
- [ ] 30 هجرة مطبَّقة، آخرها `20260709231845_AddWorkstreamDeliverables`، لا هجرة معلّقة.
- [ ] Catalog Seeders عملت (Taxonomy 170/19، قوالب موجودة، admin مبذور). OrgSeeder لم يعمل.
- [ ] Health/Auth/SignalR سليمة. Email معطّل و`email_outbox=0`.
- [ ] Legacy: قوالب Archived + تقارير تاريخية تظهر في Pod/Client/Project/Executive بلا احتساب مزدوج.
- [ ] UAT: مستخدمو الأدوار + أقسام/فرق/عملاء/مشاريع/Workstreams/Deliverables/Project-First موجودة.

### 4.6 Go/No-Go Gates
- **G1** أدوات جاهزة + Backup مُخطَّط + بوّابتا JWT/CORS مُجهَّزتان.
- **G2** Backup مكتمل + Manifest.
- **G3** القاعدة الجديدة + الهجرات + Seeders سليمة (03).
- **G4** كل فحوص 07 الحرجة خضراء ⇒ قبول UAT؛ وإلا Rollback.

## 5. Legacy §
- مفاتيح مستقرّة = عناوين القوالب + مفاتيح الفترات `YYYY-Www` (لا IDs عشوائية).
- الحالات النهائية التاريخية التي لا يبلغها مسار الـAPI تُبذَر عبر أداة dotnet fixture مخصّصة (نمط `LegacyExecutionFixture.cs`) بمفاتيح مستقرّة — لا SQL خام يدوي.
- تجنّب الاحتساب المزدوج: التقارير التاريخية على قوالب مؤرشفة منفصلة عن مسار Project-First.

## 6. UAT §
- كل البريد `@uat.local` (حارس يرفض أي بريد إنتاجي). لا بيانات شخصية حقيقية.
- الإنشاء عبر API (Identity-safe)، idempotent بالبحث عن المفتاح المستقر قبل الإنشاء.
- Verification/Cleanup دالّتان منفصلتان لا تُشغَّلان تلقائيًّا؛ Cleanup يحذف فقط ما بذره الـfixture.

## 7. المخاطر والزمن
- **الزمن:** ~20–35 دقيقة، منها **~30–60 ثانية Downtime** (لحظة restart في 06/08).
- **المخاطر:** منخفضة — القاعدة الحالية غير قابلة للحذف/التعديل؛ الخطر الوحيد بوّابتا الإقلاع (مُخفَّفتان في Preflight) وصحّة env-file.
- **الحُرّاس:** رفض أنماط prod/RC، هدف كتابة = القاعدة الجديدة حصرًا، تأكيد تفاعلي، فحص اسم الخادم، رفض placeholders عند apply.

## 8. الفحص الساكن (نُفِّذ في G1)
- `bash -n` على الـ9 سكربتات: **OK**.
- shellcheck: غير متاح محليًّا (يُوصى بتشغيله على الخادم قبل apply).
- Secret scan: نظيف (لا أسرار مكتوبة؛ المطابقة الوحيدة = رمز التأكيد `EXECUTE`).
- اختبار الحارس: يحجب `reporting_prod`/`reporting_rc`/دومين الإنتاج، ويسمح `reporting_test_uat(_app)`.
