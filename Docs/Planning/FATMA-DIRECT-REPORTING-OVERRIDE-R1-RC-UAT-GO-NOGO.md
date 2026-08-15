# FATMA-DIRECT-REPORTING-OVERRIDE-R1 — تقرير RC UAT النهائي (GO / NO-GO)

> بيئة التنفيذ: **RC فقط** (`khubara-reporting-rc.service` :5092، قاعدة `reporting_rc`). **لم يُمَسّ الإنتاج إطلاقًا.**
> التاريخ: 2026-07-15. النسخة المعزولة: `/tmp/fatma-dr-worktree` (detached @ 4a3ff8c).

## 1. الحكم النهائي

### `FATMA DIRECT REPORTING RC UAT = GO`

الميزة العامّة `BypassTeamLeaderApproval` (قاعدة «التبعية المباشرة للمدير») نُشرت على RC، طُبّقت الهجرة الواحدة بنجاح (migrations = 28)،
وأثبت UAT الحيّ على RC أن التوجيه يعمل تمامًا كما صُمِّم. لا يوجد أي شرط مقيَّد باسم فاطمة/بريدها/معرّفها في الكود (شرط منطقيّ boolean فقط).

**ثم: `STOP — DO NOT DEPLOY TO PRODUCTION`** (النشر الإنتاجي يتطلّب موافقة صريحة منفصلة، مع مراعاة اكتشاف صلاحيات القاعدة أدناه).

## 2. Phase 8 — السبب الجذري لانهيار RC وحلّه

- **العرَض**: بعد نشر الـ backend، انهارت خدمة RC في حلقة إعادة تشغيل (SIGABRT) بلا أي إخراج في `journalctl`.
- **لماذا لا إخراج**: الوحدة تكتب stdout/stderr إلى ملفّات (`StandardOutput=append:/var/log/reporting-rc/rc-api.log`،
  `StandardError=append:/var/log/reporting-rc/rc-api.err.log`) لا إلى الـ journal.
- **السبب الجذري (من err.log)**: عند `MigrateAsync` (Program.cs:192):
  `Npgsql.PostgresException 42501: must be owner of table AspNetUsers`.
- **التفسير**: دور اتصال تطبيق RC هو `reporting_rc_app` (تسجيل دخول/DML فقط، بلا عضوية في دور المالك)، بينما جميع الجداول
  (ومنها `AspNetUsers` و`__EFMigrationsHistory`) مملوكة للدور `reporting_rc_owner` (NOLOGIN). دور التطبيق لا يملك صلاحية DDL
  (`ALTER TABLE`)، فتفشل الهجرة عند الإقلاع. الهجرات الـ27 السابقة طُبِّقت بدور المالك لا بالتطبيق.
- **الحل (نمط RC الصحيح)**: طُبِّقت الهجرة يدويًّا بدور المالك ضمن معاملة واحدة ثم سُجِّلت في `__EFMigrationsHistory`:

  ```sql
  BEGIN;
  SET ROLE reporting_rc_owner;
  ALTER TABLE "AspNetUsers" ADD "BypassTeamLeaderApproval" boolean NOT NULL DEFAULT FALSE;
  INSERT INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion")
    VALUES ('20260715162851_AddBypassTeamLeaderApproval','8.0.11');
  RESET ROLE;
  COMMIT;
  ```

  بعدها أُعيد نشر الـ backend؛ عند الإقلاع رأى EF الهجرة مسجّلة ⇒
  **«No migrations were applied. The database is already up to date»** ⇒ إقلاع نظيف، `NRestarts=0`، health=200.

> **⚠️ اكتشاف حرج لخطة الإنتاج**: نموذج صلاحيات RC (دور تطبيق بلا DDL) **يختلف عن الإنتاج** (حيث يطبّق التطبيق الهجرات عند
> الإقلاع «Applying migration…»). قبل أي نشر إنتاجي يجب التحقّق من مالك جداول `reporting_prod` ودور `reporting_app`:
> إمّا (أ) التطبيق يملك صلاحية DDL فيُطبّق الهجرة تلقائيًّا، أو (ب) تُطبَّق الهجرة يدويًّا بدور المالك كما في RC. **لا تفترض السلوك — تحقّق أولًا.**

## 3. بوابة الهجرة (Migration Gate) — PASS

- SQL المولّد = **AddColumn فقط**: `ALTER TABLE "AspNetUsers" ADD "BypassTeamLeaderApproval" boolean NOT NULL DEFAULT FALSE;` + تحديث الـsnapshot.
- لا Drop/Alter/Rename، لا جداول أخرى، لا مساس بأي بيانات قائمة. Down = `DropColumn` (آمن، إضافي بحت).
- migrations: 27 → **28**. العمود موجود: `boolean NOT NULL DEFAULT false`.

## 4. دليل RC UAT الحيّ (Phase 9–10)

منظومة اختبار مؤقتة (كل الكيانات موسومة `fatmadr-uat-`، أُنشئت مباشرةً في `reporting_rc`، **بلا مساس بأي حساب RC حقيقي**):
مدير M، قائد فريق TL، موظّفان في فريق واحد يقوده TL وكلاهما `ManagerId=M` — الفارق الوحيد بينهما هو علم `BypassTeamLeaderApproval`.

| # | السيناريو | العلم | النتيجة (Status / CurrentStep) | الخطّ الزمني | متوقّع؟ |
|---|-----------|-------|--------------------------------|--------------|---------|
| A | موظّف عادي ينشئ إجازة | `bypass=false` | `Submitted` / **`TeamLeader`** | `submitted (Draft→Submitted)` | ✅ السلوك القائم محفوظ — يُوجَّه لقائد الفريق |
| B | موظّف تبعية مباشرة ينشئ إجازة | `bypass=true` | `TeamLeaderApproved` / **`Manager`** | `submitted` ثم **`team_leader_step_skipped` (Submitted→TeamLeaderApproved)** | ✅ تخطّي قائد الفريق والتوجيه للمدير المباشر |

- مقارنة محكومة: نفس الفريق، نفس المدير، نفس قائد الفريق ⇒ الفارق السلوكي يعود حصرًا لعلم `BypassTeamLeaderApproval`.
- السيناريو B لم يتعطّل ولم يُترك بلا معتمِد (CurrentStep=Manager صريح) — لا deadlock، والتوجيه للمدير المباشر (M) لا لقائد الفريق.
- **محلّل تقارير الأداء (SubmissionService)** يشترك في الآلية ذاتها (نفس علم boolean، نفس منطق التخطّي) وهو مغطّى باختبارات
  التكامل المعزولة (24/24 توجيه أخضر). لم يُنفَّذ عبر RC API لأنه يتطلّب قالبًا مُسنَدًا (TemplateRoleGuard) — التغطية عبر الاختبارات كافية.
- **KPI غير متأثّر**: `KpiEvaluationService.ResolveReviewerAsync` يتسلّق سلسلة `ManagerId` فقط ولا يقرأ `TeamLeaderId` إطلاقًا.

## 5. تنظيف RC (Phase 11) — مكتمل

- حُذف كل ما هو موسوم `fatmadr-uat-`: 4 مستخدمين، فريق واحد، إدارة واحدة، 2 طلب إجازة، 3 أحداث، 4 إسنادات أدوار.
- التحقّق: `users=0, teams=0, depts=0, leaves=0`. مجلّد التشخيص `/opt/reporting-rc/publish-fatmadr-diag` أُزيل.
- **الباقي المقصود (خطّ الأساس الجديد لـ RC)**: الميزة نفسها منشورة (migrations=28، العمود موجود، الـ backend يحمل الدلتا) —
  هذا هو هدف نشر RC، والاستعادة تعني إزالة بيانات UAT المؤقتة لا التراجع عن الميزة. RC: active، health=200.

## 6. إعلان الدلتا — backend فقط، 8 ملفات

1. `Reporting.Infrastructure/Identity/ApplicationUser.cs` — `+ bool BypassTeamLeaderApproval` (بعد ManagerId).
2. `Reporting.Infrastructure/Services/LeaveRequestService.cs` — تخطّي خطوة قائد الفريق عند bypass + حدث `team_leader_step_skipped`.
3. `Reporting.Infrastructure/Services/SubmissionService.cs` — `ResolveFirstApproverAsync`/`ResolveSubmitterTeamLeaderIdAsync` يحترمان bypass.
4. `…/Migrations/20260715162851_AddBypassTeamLeaderApproval.cs` (+ `.Designer.cs`).
5. `…/Migrations/AppDbContextModelSnapshot.cs` — إضافة الخاصية.
6. `tests/…/TestAuth.cs` — مساعد `SetBypassTeamLeaderApprovalAsync`.
7. `tests/…/FatmaDirectReportingTests.cs` — 9 اختبارات (كلها خضراء).

- **لا شرط مقيَّد بالاسم/البريد/المعرّف في الكود** (التعليقات فقط تذكر فاطمة؛ الكود يفحص boolean).
- لم يُمَسّ: ScopeResolver، Workflow، CurrentApproverId، KPI، Flexible Positions، Workstreams/Client360/ExecutionTaxonomy.
- الواجهة الأمامية محفوظة بحكم التعريف (الدلتا backend فقط). لا endpoint API لضبط العلم (يُضبط بيانيًّا بـ SQL محكوم).

## 7. Phase 12 — خطة إصلاح طلب فاطمة `ac360154` (تُنتَج ولا تُنفَّذ)

> **حالة**: مسوّدة تُنفَّذ لاحقًا على الإنتاج **تحت ضبط تغييرات منفصل وبموافقة صريحة**، **بعد** نشر الميزة وضبط `BypassTeamLeaderApproval=true` لفاطمة على الإنتاج.
> **لا تُنفَّذ الآن. لا تمسّ الإنتاج.** الأنواع النصّية مؤكّدة من RC (`Status`/`CurrentStep`/أعمدة الأحداث = `character varying`).

**السياق**: طلب فاطمة `ac360154…` قُدِّم قبل توفّر قاعدة التبعية المباشرة، فوُجِّه (خطأً وظيفيًّا) لخطوة قائد الفريق بدل مديرها المباشر (إبراهيم البحراوي). الإصلاح يحاكي ما كان الكود سيفعله لو كان العلم مضبوطًا: تخطّي خطوة قائد الفريق ونقل الطلب للمدير — **بشرط أن يكون الطلب ما يزال عالقًا عند خطوة قائد الفريق ولم يُعتمَد/يُرفَض بعد**.

```sql
-- =========================================================================
-- Fatma request ac360154 — repair runbook (PRODUCTION, DO NOT RUN NOW)
-- Prereqs: (1) full DB backup taken; (2) feature deployed; (3) Fatma
--          BypassTeamLeaderApproval already set true on prod.
-- =========================================================================

-- الخطوة 0 — نسخة احتياطية (خارج نطاق SQL): pg_dump reporting_prod أولًا.

-- الخطوة 1 — فحص (قراءة فقط): تأكيد الهوية والحالة الحالية.
SELECT lr."Id", u."Email", lr."Status", lr."CurrentStep",
       lr."TeamLeaderReviewerId", lr."ManagerReviewerId"
FROM leave_requests lr
JOIN "AspNetUsers" u ON u."Id" = lr."RequesterUserId"
WHERE lr."Id"::text LIKE 'ac360154%';
-- تحقّق يدويًّا: البريد = بريد فاطمة، Status='Submitted'، CurrentStep='TeamLeader'.
-- إن كان Status ≠ Submitted أو CurrentStep ≠ TeamLeader ⇒ توقّف (الطلب تجاوز الخطوة؛ لا حاجة/لا صلاحية للإصلاح الآلي).

-- الخطوة 2 — الإصلاح المحروس (معاملة واحدة، بدور مالك الجداول على الإنتاج).
BEGIN;
-- SET ROLE <prod_owner_role>;   -- إن كان دور التطبيق بلا صلاحية UPDATE/INSERT كافية
WITH tgt AS (
  SELECT lr."Id" AS lrid, lr."RequesterUserId" AS uid
  FROM leave_requests lr
  WHERE lr."Id"::text LIKE 'ac360154%'
    AND lr."Status" = 'Submitted'
    AND lr."CurrentStep" = 'TeamLeader'
)
UPDATE leave_requests lr
SET "Status" = 'TeamLeaderApproved',
    "CurrentStep" = 'Manager',
    "UpdatedAtUtc" = now()
FROM tgt
WHERE lr."Id" = tgt.lrid;

INSERT INTO leave_request_events
  ("Id","LeaveRequestId","ActorUserId","Action","Step","FromStatus","ToStatus","Comment","CreatedAtUtc","UpdatedAtUtc")
SELECT gen_random_uuid(), tgt.lrid, tgt.uid, 'team_leader_step_skipped', 'Employee',
       'Submitted', 'TeamLeaderApproved',
       'تصحيح إداري: تبعية مباشرة للمدير — تخطّي خطوة قائد الفريق وتوجيه الطلب للمدير المباشر.',
       now(), now()
FROM ( SELECT lr."Id" AS lrid, lr."RequesterUserId" AS uid
       FROM leave_requests lr
       WHERE lr."Id"::text LIKE 'ac360154%'
         AND lr."Status" = 'TeamLeaderApproved'   -- بعد UPDATE أعلاه ضمن نفس المعاملة
         AND lr."CurrentStep" = 'Manager' ) tgt;

-- تحقّق داخل المعاملة قبل الالتزام:
SELECT "Id","Status","CurrentStep" FROM leave_requests WHERE "Id"::text LIKE 'ac360154%';
-- إن كانت النتيجة TeamLeaderApproved/Manager بالضبط ⇒ COMMIT، وإلا ⇒ ROLLBACK.
COMMIT;
```

**Rollback للخطة**: إمّا `ROLLBACK` قبل الالتزام، أو استعادة نسخة القاعدة الاحتياطية، أو عكس يدويّ
(`Status='Submitted'`, `CurrentStep='TeamLeader'` + حذف حدث `team_leader_step_skipped` المُدرَج).
بديل غير-SQL أنظف إن توفّر: إلغاء الطلب وإعادة تقديمه بعد ضبط العلم ليمرّ بالتوجيه الصحيح طبيعيًّا.

## 8. وقفة إلزامية

**تمّ RC UAT بنجاح = GO.**
**`STOP — DO NOT DEPLOY TO PRODUCTION`** — لا نشر إنتاجي ولا تنفيذ خطة `ac360154` دون موافقة صريحة منفصلة،
ومع التحقّق المسبق من نموذج صلاحيات قاعدة الإنتاج (اكتشاف §2).
