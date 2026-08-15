# خطة تحضير بيئة UAT على TEST — UAT TEST Environment Preparation Plan

**معرّف الوثيقة:** `UAT-TEST-PREP-RC4-20260712`
**التاريخ:** 2026-07-12
**الطور:** **المرحلة الأولى — تحليل وتجهيز فقط (Analysis + Preparation only).** لا تنفيذ، لا كتابة قاعدة، لا إنشاء قاعدة، لا تغيير بيئة، لا إعادة تشغيل، لا Backup فعليّ، لا Deploy/Build/Push/Commit/Migration/DROP.
**مصدر الحقيقة:** `develop @ ffb511906f0b523ebf59fbfa27a51be66189109a` (`ffb5119`).
**الوثيقة السابقة (مرجع):** `Docs/Planning/TEST_EXPANSION_DEPLOYMENT_READINESS_PLAN.md` + ملحق الفحص الحيّ (2026-07-11).
**المرحلة الثانية (التنفيذ) لا تبدأ إلا بموافقة صريحة من المالك.**

---

## 0) القرارات المعتمَدة (أساس هذه الخطة)

| # | القرار | التطبيق في هذه الخطة |
|---|---|---|
| 1 | **Database Strategy = الخيار B** | قاعدة UAT جديدة نظيفة `reporting_test_uat` + الاحتفاظ بـ`reporting_test_rc` كاملةً كأرشيف مؤقّت + Backup كامل قبل أي خطوة + **لا حذف لأي شيء**. |
| 2 | **Environment: Development → Staging** | تُجهَّز كل المتطلّبات الآن، **ولا يُنفَّذ التحويل** (المرحلة الثانية). |
| 3 | **Legacy** | صون Archived Templates + Historical Submissions + Legacy Reporting عبر **Fixture قابل لإعادة التشغيل** (لا نقل عشوائيّ). |
| 4 | **UAT Residue** | Workstreams/Deliverables القديمة **لا تُنقَل**؛ تبقى فقط داخل Backup القاعدة الحالية. |

---

## اكتشافات كودية حاكمة (Proven-Local من `ffb5119`)

قراءة `reporting-backend/src/Reporting.Api/Program.cs` + `IdentitySeeder.cs` تكشف سلوك البيئة بدقّة — وهو **أساس خطة التحويل والبذر**:

| السطر | السلوك | الأثر على التحويل إلى Staging |
|---|---|---|
| `Program.cs:30-34` | حارس مفتاح JWT: خارج Development/Testing **يرمي استثناءً** إن كان `Jwt:Key` فارغًا أو <32 محرفًا أو يحوي `dev-only`. | **بوّابة إقلاع صارمة.** يجب إثبات أن `Jwt:Key` في env-file قويّ (≥32، بلا `dev-only`) قبل التحويل، وإلا فشل الإقلاع. |
| `Program.cs:144-168` | CORS: خارج Development/Testing **يرمي استثناءً** إن كان `Cors:AllowedOrigins` فارغًا، أو يحوي wildcard، أو localhost. | **بوّابة إقلاع صارمة + متغيّر بيئة جديد إلزاميّ.** يجب إضافة `Cors__AllowedOrigins__0=https://test.emarketingacademy.net` إلى env-file قبل التحويل، وإلا فشل الإقلاع. |
| `Program.cs:185` | `MigrateAsync()` **بلا شرط بيئة** (كل البيئات). | القاعدة الجديدة تتلقّى كل الـ30 هجرة تلقائيًّا عند أول إقلاع تحت Staging. |
| `Program.cs:186-195` | `IdentitySeeder` + `ExecutionTaxonomySeeder` + `TemplateSeeder` + `EmailControlSeeder` + `CourseSeeder` + `ServiceSeeder` **بلا شرط بيئة**. | كل هذه تعمل في Staging ⇒ القاعدة الجديدة تُبذَر: admin + 170 سجلّ Taxonomy/19 نطاق + 35 قالبًا (Published) + قوالب/قواعد البريد + كتالوج الدورات + كتالوج خدمات B2B. |
| `Program.cs:198-199` | `OrgSeeder` **في Development فقط**. | في Staging **لا يُبذَر** الهيكل التمثيليّ (35 مستخدم/6 عملاء/21 مشروع ديمو). ⇒ **بيانات UAT يجب بذرها صراحةً عبر Fixture.** |
| `Program.cs:202-206` | Swagger **في Development فقط**. | في Staging: Swagger مُعطَّل. |
| `Program.cs:217-218` | `UseHsts()` خارج Development/Testing. | في Staging: يُضاف `Strict-Transport-Security`. |
| `IdentitySeeder.cs:41-52` | افتراضيّات الأدمن (`admin@marketingexperts.local`/`Admin#12345`) تُستخدَم إلا حين `env.IsProduction()`. **Staging ليست Production.** | لو غابت `Seed:AdminEmail`/`Seed:AdminPassword` من env-file ⇒ Staging سيُنشئ الأدمن الافتراضيّ. **يجب تأكيد أن env-file يضبط `Seed:AdminEmail=admin@test.local` + كلمة صريحة** (موثّق أنه كذلك). |

**استنتاج محوريّ:** التحويل إلى Staging **لا يتطلّب أي تغيير في كود المصدر** (Program.cs يعالج Staging أصلًا). يتطلّب فقط تعديل **ملف البيئة** (`ASPNETCORE_ENVIRONMENT` + `ConnectionStrings:Default` + `Cors__AllowedOrigins__0`) وتأكيد `Jwt:Key`/`Seed:Admin*`. **صفر ملفّ مصدر يتغيّر.**

**تأكيد DataProtection (Proven-Local):** لا `AddDataProtection`/`PersistKeysToFileSystem`/`SetApplicationName` في المصدر ⇒ مفاتيح **عابرة لكل عمليّة (ephemeral)** ومعزولة تلقائيًّا لكل خدمة/مسار. (يتوافق مع إغلاق B-ISO-2.)

---

# أولًا — جرد كامل لكل ما سيُنفَّذ في المرحلة الثانية

| # | البند | الوصف الموجز | الحالة الآن |
|---|---|---|---|
| 1 | **Backup Database** | `pg_dump` كامل لـ`reporting_test_rc` (custom format) | يحتاج تنفيذ (Phase 2) |
| 2 | **Backup Runtime** | `cp -a /opt/reporting-test/publish` → `publish-backup-<TS>` | يحتاج تنفيذ |
| 3 | **Backup Frontend** | `cp -a /opt/reporting-test/frontend/dist` → `dist-backup-<TS>` | يحتاج تنفيذ |
| 4 | **Backup Config** | نسخ `khubara-reporting-test.env` + موقع Nginx (600، بلا طباعة أسرار) | يحتاج تنفيذ |
| 5 | **إنشاء Database جديدة** | `CREATE DATABASE reporting_test_uat OWNER reporting_test_uat_app` | يحتاج تنفيذ |
| 6 | **إنشاء Role** | `CREATE ROLE reporting_test_uat_app LOGIN PASSWORD '<من ملف أسرار>'` | يحتاج تنفيذ |
| 7 | **صلاحيات** | `GRANT ALL ON DATABASE ... `+ `GRANT` على `public` schema للدور الجديد فقط | يحتاج تنفيذ |
| 8 | **Migrations** | تلقائيّ عند أول إقلاع (`MigrateAsync`) — 30 هجرة على القاعدة الفارغة | آليّ (لا سكربت) |
| 9 | **Seeders** | تلقائيّ عند الإقلاع: Identity/Taxonomy/Template/EmailControl/Course/Service (OrgSeeder **لا**) | آليّ (مدمج بالتطبيق) |
| 10 | **Users** | admin (بذر تلقائيّ) + **حسابات UAT الصريحة** عبر Fixture (لكل دور) | Fixture يحتاج إنشاء |
| 11 | **Legacy Fixture** | أرشفة قوالب محدّدة + إدخال تسليمات تاريخيّة (Closed/Submitted) قابلة لإعادة التشغيل | **يحتاج إنشاء** |
| 12 | **UAT Fixture** | عملاء/مشاريع/Workstreams/Deliverables/تقارير صغيرة واقعيّة لكل سيناريو | **يحتاج إنشاء** |
| 13 | **Environment Change** | تعديل env-file: `ASPNETCORE_ENVIRONMENT=Staging` + `ConnectionStrings:Default`→`reporting_test_uat` + `Cors__AllowedOrigins__0` | يحتاج تنفيذ (Phase 2) |
| 14 | **DataProtection** | لا إجراء (عابر/معزول) — تُوثَّق الحالة فقط | لا تغيير |
| 15 | **JWT** | تأكيد قوّة `Jwt:Key` (≥32/بلا dev-only) + بقاء بصمة `d70dc4e6…` منفصلة عن الإنتاج | تحقّق فقط |
| 16 | **Cookies** | لا كوكيز جلسة خادميّة (JWT في localStorage) — لا إجراء | لا تغيير |
| 17 | **Email** | تأكيد `Email__Enabled=false`/DryRun/outbox=0 على القاعدة الجديدة | تحقّق فقط |
| 18 | **Scheduler** | تأكيد `Reminders__Enabled=false` + بوّابات BackgroundServices مغلقة | تحقّق فقط |
| 19 | **Integrations** | لا تكاملات خارجيّة — تأكيد فقط | لا تغيير |
| 20 | **SignalR** | `/hubs` على 5091 نفسه (لا تغيير) | لا تغيير |
| 21 | **Upload Paths** | إبقاء `/var/lib/reporting-test/...` (معزول عن الإنتاج) — لا تغيير | لا تغيير |
| 22 | **Health Checks** | `/health`=200 بعد الإقلاع على القاعدة الجديدة | تحقّق (Phase 2) |

---

# ثانيًا — تفصيل كل خطوة (الهدف / الزمن / Downtime / Rollback / Risk / Validation)

| # | الخطوة | الهدف | الزمن التقديريّ | Downtime | Rollback | Risk | Validation |
|---|---|---|---|---|---|---|---|
| 1 | Backup DB `reporting_test_rc` | صون كامل قبل أي تغيير | 2–4 د | لا | لا حاجة (قراءة) | منخفض (قراءة فقط) | حجم dump>0 + `pg_restore --list` يقرأ الفهرس |
| 2 | Backup Runtime | استرجاع الخادم القديم | <1 د | لا | لا | منخفض | `diff -r` عيّنة + عدّ DLLs=37 |
| 3 | Backup Frontend | استرجاع الحزمة | <1 د | لا | لا | منخفض | وجود `index-DlS_VbOD.js` |
| 4 | Backup Config (env+nginx) | استرجاع الإعداد | <1 د | لا | لا | منخفض (لا طباعة أسرار) | حجم>0 + صلاحيات 600 |
| 5 | إنشاء Role جديد | عزل صلاحيات UAT | <1 د | لا | `DROP ROLE` (آمن، لا يملك شيئًا بعد) | منخفض | `\du` يُظهر الدور |
| 6 | إنشاء DB جديدة | بيئة UAT نظيفة | <1 د | لا | `DROP DATABASE reporting_test_uat` (جديدة فارغة، آمنة) | **متوسّط** (أمر على مضيف الإنتاج — دقّة الاسم إلزاميّة) | `\l` يُظهر القاعدة، owner صحيح |
| 7 | GRANT صلاحيات | تمكين الدور | <1 د | لا | إلغاء GRANT | منخفض | اتصال تجريبيّ قراءة بالدور الجديد |
| 8 | (تحضير env-file بديل) | ملف env معدّ مسبقًا **بلا تفعيل** | 2 د | لا | حذف الملف المُعَدّ | منخفض | مراجعة المفاتيح (بلا طباعة أسرار) |
| 9 | **تفعيل التحويل**: نسخ env-file المعدّ + `systemctl restart` | تشغيل UAT على Staging+القاعدة الجديدة | 1–2 د | **نعم (~30–60ث لإعادة التشغيل)** | استعادة env-file القديم + restart | **مرتفع** (بوّابات JWT/CORS + قاعدة جديدة) | `is-active`+`/health`=200+آخر هجرة=30 |
| 10 | Migrations (آليّ) | بناء schema | 5–15ث | ضمن الإقلاع | استعادة القاعدة/الاتصال القديم | منخفض (additive مُختبَرة) | سجلّ الإقلاع «Applying …»×30 + `__EFMigrationsHistory`=30 |
| 11 | Seeders (آليّ) | كتالوج + admin | 3–8ث | ضمن الإقلاع | القاعدة الجديدة قابلة للحذف | منخفض | Taxonomy=170/19، templates=35، admin موجود |
| 12 | Legacy Fixture | صون Legacy Reporting | 1–2 د | لا | حذف صفوف Fixture (معلّمة) | متوسّط | 10 قوالب Archived + N تسليم Closed + تقرير تجميع يقرأها |
| 13 | UAT Fixture | بيانات سيناريوهات | 2–4 د | لا | حذف صفوف Fixture (معلّمة) | متوسّط | عملاء/مشاريع/Workstreams/Deliverables موجودة + login لكل دور |
| 14 | Validation شاملة | إثبات الجاهزية | 5–10 د | لا | (تشخيصيّ) | منخفض | القائمة في «رابعًا/الرَنبوك» |

**إجمالي Downtime الفعليّ:** لحظة واحدة فقط عند الخطوة 9 (إعادة تشغيل خدمة TEST) ≈ **30–60 ثانية**. لا تأثير على الإنتاج (خدمة/منفذ/قاعدة منفصلة).

---

# ثالثًا — الرَنبوك الكامل (Runbook)

## 3.1 Pre-Execution Checklist (قبل بدء المرحلة الثانية)
- [ ] موافقة صريحة من المالك على بدء التنفيذ.
- [ ] تأكيد أن العمل كلّه على `khubara-reporting-test` / 5091 / `reporting_test_*` فقط (لا لمس 5090/`reporting_prod`).
- [ ] `Jwt:Key` في env-file: طول ≥32، بلا `dev-only` (تحقّق hash فقط، بلا طباعة). **بوّابة إقلاع Staging.**
- [ ] `Seed:AdminEmail=admin@test.local` + `Seed:AdminPassword` صريحان في env-file (لتفادي أدمن Staging الافتراضيّ).
- [ ] تحديد قيمة `Cors__AllowedOrigins__0=https://test.emarketingacademy.net` (متغيّر جديد إلزاميّ لـStaging).
- [ ] كلمة مرور الدور الجديد `reporting_test_uat_app` مُولَّدة ومخزَّنة في `/root/rc-test-secrets/` (600، بلا طباعة).
- [ ] مساحة قرص كافية للنسخ الاحتياطيّة (`df -h`).
- [ ] نافذة زمنيّة مُعلَنة لفريق UAT (Downtime قصير عند إعادة التشغيل).
- [ ] كل السكربتات (backup/create-db/legacy-fixture/uat-fixture/validation/rollback) مُراجَعة ومُقرّة (انظر «سابعًا»).

## 3.2 Execution Steps (بالترتيب)
1. **Backup**: DB dump → runtime → frontend → config → uploads → لقطة `__EFMigrationsHistory` + `systemctl status` + `/health`. (كلها في `/root/test-backups/…-<TS>`.)
2. **إنشاء الدور**: `CREATE ROLE reporting_test_uat_app LOGIN PASSWORD '<secret>';`
3. **إنشاء القاعدة**: `CREATE DATABASE reporting_test_uat OWNER reporting_test_uat_app ENCODING 'UTF8';` + `GRANT`.
4. **تحضير env-file بديل** (نسخة `.staging` بلا تفعيل): `ASPNETCORE_ENVIRONMENT=Staging`, `ConnectionStrings__Default=Host=127.0.0.1;Database=reporting_test_uat;Username=reporting_test_uat_app;Password=<secret>`, `Cors__AllowedOrigins__0=https://test.emarketingacademy.net`, مع إبقاء `Jwt:*`, `Seed:Admin*`, `Email__Enabled=false`, `Reminders__Enabled=false`, `FileStorage__*`.
5. **تفعيل التحويل**: نسخ env-file البديل فوق الحاليّ + `systemctl restart khubara-reporting-test`. الإقلاع يطبّق 30 هجرة + يبذر الكتالوج + admin.
6. **Legacy Fixture**: تشغيل أداة/سكربت إعادة تجهيز Legacy (أرشفة قوالب محدّدة + تسليمات تاريخيّة).
7. **UAT Fixture**: تشغيل سكربت بذر بيانات UAT (عملاء/مشاريع/Workstreams/Deliverables/تقارير + حسابات الأدوار).
8. **Validation** (3.3) + **Acceptance** (3.5).

## 3.3 Validation Steps
- `systemctl is-active khubara-reporting-test` = active.
- `/health` = 200.
- `__EFMigrationsHistory` = 30، آخرها `20260709231845_AddWorkstreamDeliverables`.
- بيئة الإقلاع في السجلّ = `Staging`؛ Swagger معطّل؛ HSTS مُضاف.
- الاتصال يشير إلى `reporting_test_uat` (لا `reporting_test_rc`).
- Taxonomy=170/19، templates=35، admin=`admin@test.local` يسجّل دخولًا.
- OrgSeeder لم يعمل (0 مستخدم ديمو `@marketingexperts.local` قبل تشغيل UAT Fixture).
- `email_outbox`=0، `Email__Enabled=false`، `Reminders__Enabled=false`.
- Legacy: 10 قوالب Archived + تسليمات Closed مقروءة + تقرير تجميع Legacy يعمل.
- UAT: login لكل دور + Project-First (مشروع→Workstreams→Deliverables) + Rollup صحيح + RBAC 403 للأدوار غير المصرّحة.
- عزل: بصمة `Jwt:Key` = `d70dc4e6…` (≠ إنتاج)، مسارات الملفات `/var/lib/reporting-test/…`.

## 3.4 Rollback Plan
- **عكس التحويل (الأسرع):** استعادة env-file القديم (`ASPNETCORE_ENVIRONMENT=Development` + اتصال `reporting_test_rc`) + `systemctl restart` ⇒ يعود TEST فورًا لحالته الأصليّة على القاعدة القديمة السليمة. (القاعدة القديمة لم تُمَسّ إطلاقًا.)
- **عكس القاعدة الجديدة:** `DROP DATABASE reporting_test_uat;` + `DROP ROLE reporting_test_uat_app;` (آمن — كائنات جديدة لا يعتمد عليها الإنتاج).
- **عكس Runtime/Frontend:** استعادة `publish-backup-<TS>`/`dist-backup-<TS>` (غير مطلوب عادةً — لا Build/Deploy جديد).
- **عكس Fixtures:** حذف الصفوف المُعلَّمة (كل صفوف Fixture تحمل علامة مميّزة) — أو ببساطة `DROP DATABASE` للقاعدة الجديدة.
- **نقطة اللاعودة:** لا توجد — القاعدة القديمة + كل النسخ محفوظة حتى اعتماد UAT.

## 3.5 Acceptance Checklist
- [ ] كل بنود Validation (3.3) خضراء.
- [ ] بوّابة UAT في الوثيقة السابقة (القسم 10) مستوفاة.
- [ ] فريق UAT يملك حسابات كل الأدوار + بيانات كافية لكل سيناريو.
- [ ] Legacy Reporting مُثبَت العمل.
- [ ] القاعدة القديمة + النسخ الاحتياطيّة سليمة ومحفوظة.
- [ ] لا أثر على الإنتاج/RC (فحص `is-active` + `/health` للإنتاج بلا تغيير).

## 3.6 Go / No-Go Gates
| البوّابة | GO | NO-GO |
|---|---|---|
| G1 قبل التحويل | JWT قويّ + CORS محدّد + env بديل مُراجَع + Backups تامّة | أي منها ناقص |
| G2 بعد الإقلاع | active + `/health`=200 + 30 هجرة + Staging + اتصال UAT | فشل إقلاع / بوّابة JWT/CORS / اتصال خاطئ ⇒ Rollback فوريّ |
| G3 بعد Fixtures | Legacy + UAT + RBAC + Rollup خضراء | أي سيناريو مكسور ⇒ تشخيص أو Rollback |
| G4 التسليم لـUAT | Acceptance تامّة + الإنتاج سليم | أي بند أحمر |

---

# رابعًا — خطة بيانات UAT (واقعيّة، صغيرة، تغطّي كل السيناريوهات)

> المبدأ: أصغر مجموعة تكفي لتمرين كل الأدوار والمسارات. **معلَّمة كلها** بعلامة (مثل بادئة `UAT-` أو حقل ملاحظة) لتسهيل التمييز/الحذف.

| المحور | العدد المقترَح | التفصيل | مصدر البذر |
|---|---|---|---|
| **Users** | ~9 (واحد لكل دور) | Admin (تلقائيّ)، CEO، GeneralManager، Manager، TeamLeader، Employee، HR، CeoSupport، Viewer. سلسلة إدارة واضحة (Employee→TL→Manager→GM→CEO) لاختبار الاعتماد/التصعيد وT-WF2. | UAT Fixture (صريح) |
| **Departments** | 2 | «التسويق» + «المبيعات» (تكفي لاختبار النطاق والرؤية) | UAT Fixture |
| **Teams** | 2 | فريق تحت كل إدارة، لكلٍّ TeamLeaderId (لاختبار T-WF2 الدقيق) | UAT Fixture |
| **Clients** | 2 | عميل نشط + عميل (لاختبار الأرشفة/الحذف لاحقًا اختياريًّا) | UAT Fixture |
| **Projects** | 2–3 | مشروع Project-First واحد على الأقل لتمرين Workstreams/Deliverables + مشروع عاديّ | UAT Fixture |
| **Workstreams** | 2–3 | على مشروع Project-First (أنواع مختلفة من `workstream_type`) — **بيانات جديدة، لا نقل من الأرشيف** | UAT Fixture |
| **Deliverables** | 3–5 | تحت الـWorkstreams (حالات/خطوات مختلفة من Taxonomy) | UAT Fixture |
| **Reports (نشطة)** | 3–5 تسليمات | مسودّة + مُرسَل + مُعتمَد (لتمرين دورة الحياة والاعتماد وRBAC) | UAT Fixture |
| **Historical Reports** | 5–10 تسليمات Closed | تسليمات تاريخيّة على قوالب Legacy (لتمرين Legacy Reporting/التجميع) | **Legacy Fixture** |
| **Legacy Templates** | نفس الـ10 القوالب المؤرشفة | تُبذَر Published ثم تُؤرشَف عبر الـFixture (Status=Archived, IsActive=false) | **Legacy Fixture** |
| **Project-First Templates** | من TemplateSeeder | تُبذَر تلقائيًّا (Published) | آليّ (Seeder) |
| **Execution Taxonomy** | 170/19 نطاقًا | تُبذَر تلقائيًّا idempotent | آليّ (Seeder) |
| **KPI Templates** | 9 | تُبذَر تلقائيًّا (لتمرين مسار KPI اختياريًّا) | آليّ (Seeder) |

**تغطية السيناريوهات:** Auth/RBAC (9 أدوار) · دورة حياة التقرير (Draft→Submitted→Approved→Closed) · اعتماد/تصعيد + T-WF2 (فريق بقائد) · Project-First (مشروع→Workstreams→Deliverables) · Rollup (SEO/Media Buyer) · Legacy Reporting (قوالب مؤرشفة + تسليمات Closed) · Execution Taxonomy · أمان البريد (لا إرسال).

---

# خامسًا — خطة DataProtection (توصيف فقط — لا تنفيذ)

| البُعد | الحالة/الخطة |
|---|---|
| **مكان Key Ring** | لا key ring على القرص — المفاتيح **عابرة لكل عمليّة (ephemeral)** (لا `PersistKeysToFileSystem` في المصدر). كل إعادة تشغيل تُولّد مفتاحًا جديدًا في الذاكرة. |
| **الصلاحيات** | لا ملفّات مفاتيح ⇒ لا صلاحيات قرص لإدارتها. الخدمة تعمل `www-data`. |
| **العزل** | معزول تلقائيًّا لكل عمليّة/تطبيق (لا `SetApplicationName` مشترك) ⇒ لا تداخل مع الإنتاج. (يطابق إغلاق B-ISO-2.) |
| **التجديد** | آليّ عند كل إقلاع (مفتاح جديد). **أثر مقبول:** التوكنات المُوقَّعة بـDataProtection لا تصمد عبر إعادة التشغيل — لكن مصادقة النظام تعتمد **JWT** (مفتاح ثابت في env-file) لا DataProtection، فلا أثر على جلسات UAT الفعليّة. |
| **النسخ الاحتياطيّ** | غير مطلوب (لا مفاتيح دائمة). **توصية اختياريّة مستقبليّة (لا تُنفَّذ الآن):** إن رُغب ثبات التوكنات العابرة عبر إعادة التشغيل، يُضاف لاحقًا `PersistKeysToFileSystem` بمسار معزول `/var/lib/reporting-test/dp-keys` (600، www-data) + `SetApplicationName("khubara-reporting-test")` — **تغيير كود، خارج نطاق هذه الخطة، يحتاج قرارًا منفصلًا.** |

**الخلاصة:** لا إجراء DataProtection في المرحلة الثانية سوى **توثيق الحالة**. العزل قائم والأمان غير متأثّر.

---

# سادسًا — خطة تحويل البيئة (Development → Staging)

## ما الذي سيتغيّر
| البُعد | Development (الآن) | Staging (بعد التحويل) |
|---|---|---|
| **Seeders** | OrgSeeder نشط ⇒ 35 مستخدم/6 عملاء/21 مشروع ديمو | OrgSeeder **مُعطَّل** ⇒ لا ديمو (بيانات UAT عبر Fixture) |
| **Exceptions** | Developer Exception Page (تفاصيل stack) | صفحة خطأ عامّة (سلوك إنتاجيّ) |
| **Logging** | مُسهَب (Debug) | أقلّ إسهابًا (أقرب للإنتاج) |
| **Swagger** | مُفعَّل | **مُعطَّل** |
| **HSTS** | غير مُضاف | **مُضاف** (`Strict-Transport-Security`) |
| **JWT guard** | متساهل | **صارم** (يرمي إن كان المفتاح ضعيفًا) |
| **CORS guard** | يقبل localhost + الأصول المُعدّة | **صارم** (يتطلّب أصلًا صريحًا، يرفض wildcard/localhost) |
| **قاعدة البيانات** | `reporting_test_rc` (أرشيف) | `reporting_test_uat` (جديدة نظيفة) |

## ما الذي لن يتغيّر
- **Migrations**: تعمل في كل البيئات (لا فرق في التطبيق).
- **الكتالوج الأساسيّ**: Identity/Taxonomy/Template/EmailControl/Course/Service يعمل في Staging أيضًا.
- **البريد/التذكيرات**: تبقى مُعطَّلة (`Email__Enabled=false`/`Reminders__Enabled=false`).
- **SignalR/المنفذ/المسارات/الملكية**: 5091 / `/hubs` / `/var/lib/reporting-test` / www-data.
- **كود المصدر**: **صفر ملفّ يتغيّر** (Program.cs يعالج Staging أصلًا).
- **الإنتاج/RC**: بلا أي مساس.

## الأثر التفصيليّ
- **Logging**: مستوى أهدأ — كافٍ لـUAT، أقرب لتشخيص الإنتاج.
- **Exceptions**: أخطاء عامّة للمستخدم (لا تسريب stack) — **أكثر واقعيّة لـUAT** وأأمن.
- **Configuration**: يُحمَّل `appsettings.Staging.json` إن وُجد (غير موجود حاليًّا ⇒ يُعتمَد `appsettings.json` + env-file). **بند تحقّق:** لا حاجة لملفّ Staging منفصل ما دام env-file يوفّر المفاتيح.
- **Seeders**: أهمّ فرق — لا OrgSeeder ⇒ بيئة نظيفة ⇒ **إلزاميّة Fixtures** لبيانات UAT وLegacy.
- **Security**: HSTS + إخفاء التفاصيل + حارسا JWT/CORS ⇒ صلابة أعلى (لكن **بوّابتا إقلاع** يجب تجهيزهما مسبقًا).
- **Caching**: لا caching خادميّ يعتمد على البيئة في هذا التطبيق ⇒ لا أثر (DataProtection عابر لا يخزّن حالة).

**بوّابتان إلزاميّتان قبل التحويل (وإلا فشل الإقلاع):**
1. `Cors__AllowedOrigins__0=https://test.emarketingacademy.net` مُضاف إلى env-file.
2. `Jwt:Key` قويّ (≥32، بلا `dev-only`) — مؤكَّد.

---

# سابعًا — مراجعة السكربتات المطلوبة

> **نتيجة الفحص:** لا سكربتات نشر/نسخ/بذر **مُلتزَمة في المستودع** (`find` بلا نتائج لـ`.sh/.sql/.mjs` خارج node_modules). كل النشر التاريخيّ تمّ عبر أوامر مضمّنة موثّقة في تقارير `Docs/*TEST-Deployment-Report.md`. توجد أدوات C# console فقط (`tools/OrgImporter`, `TemplateBinder`, `KpiTemplateBinder`) — لا تفيد UAT/Legacy مباشرةً.

| السكربت | الغرض | الحالة | ملاحظة |
|---|---|---|---|
| **Backup Script** | dump DB + cp runtime/dist/config/uploads + لقطات | **يحتاج إنشاء** | نمط معروف من تقارير النشر السابقة (`pg_dump` redirect stdout؛ postgres لا يكتب /root) — يُكتب كسكربت واحد مُعلَّم بـ`<TS>` |
| **Create-DB Script** | `CREATE ROLE`+`CREATE DATABASE`+`GRANT` | **يحتاج إنشاء** | SQL صغير idempotent (`IF NOT EXISTS`) — **حسّاس (اسم دقيق على مضيف الإنتاج)** |
| **Migration Script** | تطبيق الهجرات | **جاهز (مدمج)** | `MigrateAsync` تلقائيّ عند الإقلاع — لا سكربت منفصل |
| **Seeder Script** | كتالوج + admin | **جاهز (مدمج)** | Seeders تعمل عند الإقلاع (عدا OrgSeeder في Staging) |
| **Legacy Fixture Script** | أرشفة قوالب + تسليمات تاريخيّة قابلة لإعادة التشغيل | **يحتاج إنشاء** | النموذج المرجعيّ = `tests/…/LegacyExecutionFixture.cs` (test-only) — يُحوَّل إلى أداة/سكربت UAT idempotent يكتب صفوفًا معلَّمة |
| **UAT Fixture Script** | بذر users/depts/teams/clients/projects/workstreams/deliverables/reports | **يحتاج إنشاء** | صريح ومُعلَّم؛ يعيد استخدام منطق OrgSeeder/الكيانات لكن بمجموعة UAT صغيرة |
| **Rollback Script** | استعادة env القديم + restart، وحذف DB الجديدة | **يحتاج إنشاء** | بسيط (نسخ env + `systemctl restart` + `DROP DATABASE/ROLE`) |
| **Health Script** | `/health` + `is-active` + آخر هجرة + بيئة | **يحتاج إنشاء** | curl + systemctl + psql قراءة |
| **Validation Script** | فحوص 3.3 مجمّعة | **يحتاج إنشاء** | يجمع Health + عدّات القاعدة + login أدوار + RBAC + Rollup |

**الخلاصة:** 3 عناصر **جاهزة/مدمجة** (Migration + Seeders الأساسيّة)، و**6 سكربتات تحتاج إنشاءً** في المرحلة الثانية (Backup, Create-DB, Legacy Fixture, UAT Fixture, Rollback, Health/Validation). كلها تُكتب وتُراجَع **قبل** أي تنفيذ فعليّ (بوّابة G1).

---

# ثامنًا — الخلاصة التنفيذيّة (بلا أي تنفيذ)

## 1) الخطة التنفيذيّة الكاملة
مذكورة في «الرَنبوك» (القسم ثالثًا): Pre-Checklist → Backup → إنشاء Role/DB → تحضير env بديل → تفعيل التحويل (restart) → Legacy Fixture → UAT Fixture → Validation → Acceptance، محكومة ببوّابات G1–G4.

## 2) قائمة الملفّات التي ستتغيّر
- **كود المصدر:** **لا شيء** (صفر ملفّ — Staging مدعوم أصلًا).
- **إعداد الخادم:** `/etc/khubara-reporting-test.env` (تعديل `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__Default`, إضافة `Cors__AllowedOrigins__0`؛ تأكيد `Jwt:*`/`Seed:Admin*`).
- **سكربتات جديدة (محليّة على الخادم، خارج المستودع):** backup, create-db, legacy-fixture, uat-fixture, rollback, health/validation.
- **Nginx:** **لا تغيير** (النطاق/المنفذ/الجذر ثابتة).

## 3) قواعد البيانات التي ستُنشأ
- `reporting_test_uat` (قاعدة UAT جديدة نظيفة).
- الدور `reporting_test_uat_app` (LOGIN معزول).
- `reporting_test_rc` **تبقى كما هي** (أرشيف مؤقّت، لا تُمَسّ).

## 4) الخدمات التي ستُعاد تشغيلها لاحقًا
- `khubara-reporting-test` فقط (مرّة واحدة عند التحويل، Downtime ~30–60ث).
- **لا** مساس بـ`reporting-api` (الإنتاج) ولا خدمات RC.

## 5) ملفّات الإعداد التي ستتغيّر
- `/etc/khubara-reporting-test.env` (فقط). نسخة احتياطيّة أولًا.

## 6) النسخ الاحتياطيّة المطلوبة (طابع `uat-prep-20260712-<HHMMSS>`)
| النسخة | المسار المقترَح |
|---|---|
| DB `reporting_test_rc` | `/root/test-backups/reporting_test_rc-uat-prep-<TS>.dump` |
| Runtime | `/opt/reporting-test/publish-backup-uat-prep-<TS>` |
| Frontend | `/opt/reporting-test/frontend/dist-backup-uat-prep-<TS>` |
| env-file | `/root/test-backups/khubara-reporting-test.env.bak-<TS>` (600) |
| Nginx | `/root/test-backups/nginx-reporting-test-<TS>.conf` |
| Uploads | أرشيف `/var/lib/reporting-test/…/final-documents` |
| لقطات | `__EFMigrationsHistory` + `systemctl status` + `/health` |

## 7) خطة Rollback الكاملة
- **الأسرع:** استعادة env-file القديم (Development + `reporting_test_rc`) + `systemctl restart` ⇒ عودة فوريّة على القاعدة القديمة السليمة.
- **حذف الجديد:** `DROP DATABASE reporting_test_uat; DROP ROLE reporting_test_uat_app;` (آمن).
- **Runtime/Frontend:** استعادة النسخ (غير متوقّع — لا Build/Deploy جديد).
- **Fixtures:** حذف الصفوف المُعلَّمة أو `DROP DATABASE` للقاعدة الجديدة.
- **لا نقطة لاعودة** — القاعدة القديمة وكل النسخ محفوظة.

## 8) تقدير الزمن الكلّيّ
- Backups: 5–8 د · إنشاء DB/Role/GRANT: 2–3 د · تحضير env: 2 د · التحويل+الإقلاع: 2–3 د · Legacy Fixture: 1–2 د · UAT Fixture: 2–4 د · Validation: 5–10 د.
- **الإجمالي التقديريّ ≈ 20–35 دقيقة** (منها Downtime فعليّ ~30–60 ثانية فقط)، **بعد** جهوزية السكربتات.

## 9) تقييم المخاطر
| الخطر | الاحتمال | الأثر | التخفيف |
|---|---|---|---|
| فشل إقلاع Staging (بوّابة CORS/JWT) | متوسّط لو أُهمل التجهيز | مرتفع (خدمة TEST) | G1 يفرض ضبط CORS/JWT قبل restart؛ Rollback env فوريّ |
| خطأ اسم في أمر DB على مضيف الإنتاج | منخفض | **مرتفع جدًّا** | نسخ/لصق أسماء دقيقة؛ التحقّق `\l`/`\du` قبل/بعد؛ لا `DROP` إلا على `reporting_test_uat` |
| نقص بيانات Legacy ⇒ كسر تقارير Legacy | متوسّط | متوسّط | Legacy Fixture مُختبَر يطابق نموذج `LegacyExecutionFixture` + Validation صريحة |
| بيانات UAT غير كافية لسيناريو | منخفض | منخفض | خطة البيانات (رابعًا) تغطّي كل الأدوار/المسارات |
| فقد بيانات القاعدة القديمة | منخفض جدًّا | مرتفع | **لا تُمَسّ إطلاقًا** + Backup كامل قبل أي خطوة |
| تأثّر الإنتاج | منخفض جدًّا | مرتفع جدًّا | عزل تامّ (خدمة/منفذ/قاعدة/مسار) + فحص إنتاج بعد كل خطوة حسّاسة |

## 10) القرار النهائيّ

**READY FOR UAT PREPARATION** — المرحلة الأولى (التحليل والتجهيز) مكتملة، والخطة جاهزة للاعتماد.

**الأساس:**
- سلوك البيئة محسوم Proven-Local (التحويل إلى Staging **بلا تغيير كود**، بوّابتا JWT/CORS مُحدَّدتان).
- استراتيجية الخيار B واضحة وآمنة (قاعدة جديدة + أرشيف كامل + لا حذف).
- خطة Legacy قابلة لإعادة التشغيل (نموذج مرجعيّ موجود).
- خطة بيانات UAT واقعيّة وصغيرة وتغطّي كل السيناريوهات.
- Rollback فوريّ عبر env-file + القاعدة القديمة محفوظة.

**شروط بدء المرحلة الثانية (بوّابة G1 — يجب استيفاؤها قبل أي تنفيذ):**
1. موافقة صريحة من المالك على التنفيذ.
2. كتابة ومراجعة الـ6 سكربتات الناقصة (Backup, Create-DB, Legacy Fixture, UAT Fixture, Rollback, Health/Validation).
3. إضافة `Cors__AllowedOrigins__0` وتأكيد قوّة `Jwt:Key` + وجود `Seed:Admin*` في env-file.
4. توليد كلمة مرور الدور الجديد وتخزينها بأمان (بلا طباعة).

**ما لم يُنفَّذ (تأكيد):** لا Backup فعليّ، لا إنشاء قاعدة/دور، لا تغيير بيئة، لا restart، لا كتابة على أي قاعدة، لا Deploy/Build/Push/Commit/Migration/DROP، ولا مساس بـTEST/RC/الإنتاج. **هذه الوثيقة لن تُلتزَم/تُدفَع (commit/push) قبل موافقة المالك.**

---

**ملاحظة حَوكميّة:** كل ما ورد أعلاه **تخطيط وتحليل قراءةً-فقط** بُني على فحص المصدر المحليّ (`Program.cs`, `IdentitySeeder.cs`) وملحق الفحص الحيّ السابق. المرحلة الثانية (التنفيذ) **موقوفة على موافقة صريحة**.

---

## ملحق G1 — حزمة السكربتات والتنفيذ (Scripts & Cutover Package) — 2026-07-12

أُنشئت الأدوات والوثائق فقط. **لم يُشغَّل أي سكربت، ولم تُمَسّ أي بيئة/قاعدة، ولم يُعمل Commit/Push.**

### G1-A: الملفات المنشأة (تحت `Ops/TestUatPreparation/`)
| # | ملف | الوظيفة |
|---|---|---|
| 1 | `config.env.template` | قالب إعداد بلا أسرار (placeholders) — يُنسخ لـ`config.env` خارج Git |
| 2 | `00-common.sh` | مكتبة مشتركة: strict mode + guards (منع prod/RC، هدف كتابة = القاعدة الجديدة حصرًا، تأكيد `EXECUTE`، فحص hostname، رفض placeholders) + logging + توقيت |
| 3 | `01-backup-test.sh` | نسخ: DB + publish + dist + env-file + nginx + dpkeys + uploads + migration history + service status + health + SHA256 + Manifest |
| 4 | `02-create-uat-db.sh` | إنشاء `reporting_test_uat` + دور `reporting_test_uat_app` (أقل صلاحيات، كلمة مرور تُولَّد وقت التنفيذ، توقّف بدل overwrite) |
| 5 | `03-validate-uat-db.sh` | تحقّق READ-ONLY: 30 هجرة + آخرها + Seeders + عدم بذر OrgSeeder |
| 6 | `04-seed-legacy-fixture.sh` | قوالب Archived + تقارير تاريخية محدودة بمفاتيح مستقرّة + Verify/Cleanup منفصلان |
| 7 | `05-seed-uat-fixture.sh` | مستخدمو الأدوار + أقسام/فرق/عملاء/مشاريع/Workstreams/Deliverables/Project-First (API، بريد `@uat.local` فقط) |
| 8 | `06-cutover-test-to-uat.sh` | Preflight (بوّابتا JWT/CORS) → تركيب env ذرّيًّا → restart → استدعاء 07 → Rollback عند الفشل |
| 9 | `07-health-validation.sh` | Health/Auth (حرجان) + API/SignalR/Email/Project-First/Legacy/Rollup |
| 10 | `08-rollback-test.sh` | استعادة env السابق + restart + Health (القاعدة القديمة لم تُمَسّ) |
| 11 | `env/staging.env.template` | Delta لـenv-file: `ASPNETCORE_ENVIRONMENT=Staging` + Connection String + `Cors__AllowedOrigins__0` + `Jwt__Key` + `Seed__Admin*` + Cookies/DataProtection مستقلة + قنوات خارجية معطّلة |
| 12 | `README.md` | Runbook كامل: Pre-Checklist / Execution / Validation / Rollback / Acceptance / Gates + أقسام Legacy/UAT/المخاطر |

### G1-B: نتائج الفحص الساكن (لا كتابة على أي بيئة)
- `bash -n` على الـ9 سكربتات: **OK (9/9)**.
- **Secret scan: نظيف** — لا أسرار مكتوبة (المطابقة الوحيدة = رمز التأكيد `EXECUTE`، ليس سرًّا).
- **اختبار الحارس فعليًّا:** يحجب `reporting_prod`/`reporting_rc`/`reports.emarketingacademy.net`، ويسمح `reporting_test_uat`/`reporting_test_uat_app`.
- كل السكربتات `set -Eeuo pipefail` + وضع PLAN افتراضي.
- shellcheck **غير متاح محليًّا** — يُوصى بتشغيله على الخادم قبل apply (بند مفتوح غير حاجز).

### G1-C: القرارات المؤكَّدة من المصدر
- الهجرات = **30** (آخرها `20260709231845_AddWorkstreamDeliverables`)، تُطبَّق تلقائيًّا عبر `MigrateAsync()` عند الإقلاع في كل البيئات (Program.cs:185).
- Seeders تعمل في Staging: Identity/ExecutionTaxonomy/Template/EmailControl/Course/Service (Program.cs:186-195). **OrgSeeder = Development فقط** (Program.cs:198) ⇒ لا بيانات ديمو في Staging.
- Health: `GET /health` ⇒ `{"status":"ok","service":"reporting-api"}` (Program.cs:226).

**الحالة:** G1 مكتمل (أدوات + وثائق + فحص ساكن). **لا تنفيذ، لا Commit/Push — بانتظار موافقة المالك لبدء المرحلة الثانية.**
