# RESTORE / ARCHIVE GOVERNANCE R1 — النشر الإنتاجيّ — تقرير القبول النهائي

**التاريخ:** 2026-07-16
**البيئة:** الإنتاج (`reporting-api.service` @ `http://127.0.0.1:5090`، `ASPNETCORE_ENVIRONMENT=Production`، قاعدة `reporting_prod`، VPS 187.127.72.232، الدومين العام `https://reports.emarketingacademy.net`)
**النطاق:** نشر إنتاجيّ للمرشّح المعتمَد (RC UAT = GO): هجرة واحدة + Backend + Frontend، إعادة تشغيل واحدة محكومة، دخان بلا تحوّر، عدم انحدار، مراقبة، تقرير. **لا استرجاع لأيّ تقرير/KPI حقيقيّ، لا Fixtures، لا Hard Delete، لا Push/Merge.**
**معرّف النشر (TS):** `20260716-111459`
**الحكم النهائي:** ✅ **RESTORE / ARCHIVE GOVERNANCE R1 — PRODUCTION DEPLOYMENT SUCCESSFUL**

---

## 1) خطّ الأساس (Baseline) — قبل وبعد النشر

| المؤشّر | قبل النشر | بعد النشر + الدخان + المراقبة | الحالة |
|---|---|---|---|
| حالة الخدمة | active | active | ✅ لا انحراف |
| NRestarts | 0 | 0 | ✅ |
| Health داخليّ / عام | 200 / 200 | 200 / 200 | ✅ |
| عدد الهجرات | 28 | 29 | ✅ (+1 مقصودة) |
| رأس الهجرة | `AddBypassTeamLeaderApproval` | `20260716015239_KpiEvaluationPartialUniqueIndex` | ✅ |
| فهرس KPI | كامل فريد (بلا شرط) | فريد جزئيّ `WHERE ("IsDeleted"=false)` | ✅ |
| مالك الفهرس | reporting_app | reporting_app | ✅ |
| `AspNetUsers` | 35 | 35 | ✅ |
| `report_submissions` | 72 | 72 | ✅ |
| تقارير محذوفة (soft) | 4 | 4 | ✅ لم تُمَسّ |
| `kpi_evaluations` | 24 | 24 | ✅ |
| `kpi_results` | 182 | 182 | ✅ |
| `kpi_evaluation_review_events` | 8 | 8 | ✅ |
| `audit_logs` | 657 | 657 | ✅ |
| `archive_item_restored` (تدقيق) | 0 | 0 | ✅ لا استرجاع |
| `notifications` | 152 | 152 | ✅ |
| `email_outbox` | 0 | 0 | ✅ لا بريد |

**لا انحراف في أيّ بيانات إنتاجية. التغيير الوحيد المقصود = الهجرة (28→29) والفهرس الجزئيّ.**

---

## 2) البوابات (Phases 0–3)

- **Phase 0 — Pre-Flight (قراءة فقط):** GO — لا انحراف (28 هجرة، الرأس AddBypassTeamLeaderApproval، فهرس كامل، 0 تكرار KPI، الجداول مملوكة لـ reporting_app).
- **Phase 1 — هوية المرشّح:** PASS — نفس مرشّح RC. build 0 أخطاء، unit 133/133، ArchiveGovernanceTests 31/31، `has-pending-model-changes`=No، frontend 182/182، دلتا الهجرة = هجرة الفهرس الجزئيّ لـ KPI فقط، bundle إنتاجيّ نظيف (0 تسريب localhost).
- **Phase 2 — النسخ الاحتياطية:** مكتملة (TS `20260716-111459`): DB dump (736974 bytes، sha256 موثّق) + backend backup + frontend backup + config snapshot (بلا أسرار) + baseline manifest.
- **Phase 3 — بوابة الهجرة:** PASS — الفهرس الحاليّ كامل، 0 تكرار، SQL الهجرة = Drop/Create فهرس KPI الجزئيّ فقط. `PRODUCTION KPI PARTIAL INDEX MIGRATION GATE = PASS`.

---

## 3) تطبيق الهجرة (Phase 4)

طُبِّقت الهجرة الوحيدة `20260716015239_KpiEvaluationPartialUniqueIndex` يدويًّا داخل **معاملة واحدة** كـ `reporting_app` (للحفاظ على ملكية الفهرس)، ثم سُجِّلت في `__EFMigrationsHistory` (ProductVersion=8.0.11):

```
BEGIN → SET ROLE reporting_app → DROP INDEX → CREATE UNIQUE INDEX ... WHERE "IsDeleted"=false
→ INSERT __EFMigrationsHistory → RESET ROLE → COMMIT (EXIT=0)
```

**التحقّق:** count=29، الرأس=`20260716015239...`، تعريف الفهرس الآن `WHERE ("IsDeleted" = false)`، المالك=reporting_app، **كل عدّادات البيانات ثابتة** (لا صفّ تغيّر). لم يُطلَق `STOP — PRODUCTION MIGRATION FAILED`.

---

## 4) النشر (Phases 5–7)

- **Backend (Phase 5):** إيقاف الخدمة → rsync للـ publish المعتمَد إلى `/opt/reporting/publish` (استبعاد appsettings.Development.json) → chown www-data → تشغيل. النتيجة: active، NRestarts=0، بيئة Production، «No migrations were applied» (29 مسبقًا)، لا خطأ/42501/exception، health داخليّ+عام=200.
- **Frontend (Phase 6):** rsync للـ dist المعتمَد إلى `/opt/reporting/reporting-frontend/dist` → chown. index.html يشير إلى `index-BbXihVZO.js` (موجود)، 0 تسريب localhost، prod api base موجود، index عام=200.
- **إثبات النشر (Phase 7):** كل الـ hashes **byte-identical** للمرشّح المعتمَد:
  - `Reporting.Api.dll`=`805fb6a3…`، `Reporting.Application.dll`=`616bf9ee…`، `Reporting.Infrastructure.dll`=`9fbce778…`، `Reporting.Domain.dll`=`9766f5ce…`
  - `index-BbXihVZO.js`=`640095a5…`، `index.html`=`81dd0f19…`
  - markers الأرشيف مؤكَّدة: مسار `api/admin/archive`=1 (UTF-8)، سياسة `ArchiveGovernanceAccess`=1، حدث `archive_item_restored`=1.
  - **`RESTORE ARCHIVE PRODUCTION DEPLOYMENT PROOF = PASS`**

---

## 5) الدخان بلا تحوّر (Phase 8)

- نقاط الأرشيف الخمس بلا JWT ⟶ **401** (لا 404): GET `/`, GET `report/{id}`, GET `kpi/{id}`, POST `report/{id}/restore`, POST `kpi/{id}/restore`.
- مسار مجهول `/api/admin/nonexistent` ⟶ **404** (يُثبت أن الـ401 مصادقة حقيقية لا مسار مفقود).
- **بلا أيّ تحوّر:** كل العدّادات مطابقة للأساس بالضبط بعد الدخان، `archive_item_restored`=0، `email_outbox`=0.
- الفحص المصادَق للقراءة يُعتمَد على **RC UAT (37/37)** حفاظًا على zero-impact صارم (تجنّب حتى إنشاء refresh_token من تسجيل دخول).

---

## 6) عدم الانحدار (Phase 9)

- كل الموديولات حيّة (401 غير مصادَق، لا 500): التقارير، KPI (التقييمات/القوالب)، قوالب التقارير، Dashboard، الحوكمة (items/escalations/action-items)، الإجازات، طلبات HR، الأرصدة، الرواتب (payroll)، KPI finance-export، الإشعارات، التدقيق (audit-logs). قاعدة `api/report-calendar` مسجّلة في الـ DLL (لم يمسّها النشر).
- **الحذف الإداريّ = soft** (soft_deleted_reports=4 ثابت، لا Hard Delete).
- **البريد معطّل:** `Email__Enabled=false`، `Reminders__Enabled` غير مضبوط، `Email__IncludedTypes` غير مضبوط، `email_outbox`=0.
- سلوك الفهرس الجزئيّ + مسارات الاعتماد (Approval UX) + Fatma Direct Reporting = مُعتمَدة على RC UAT 37/37 (نفس شجرة الكود، DLLs byte-identical).

---

## 7) المراقبة (Phase 10)

**5 جولات، كلها نظيفة:**

| الجولة | active | NRestarts | داخليّ | عام | migrations | email_outbox |
|---|---|---|---|---|---|---|
| 1–5 | active | 0 | 200 | 200 | 29 | 0 |

مسح الأخطاء منذ إعادة التشغيل: **0 مطابقة حرجة** (42501/deadlock/Unhandled exception/HTTP 500). أسطر EF SQL (Information) طبيعية وموجودة قبل النشر — ليست أخطاء.

---

## 8) التنظيف (Phase 11)

- حُذف مجلدا الـ staging المؤقتان (`/root/restore-archive-r1-staging-publish`, `.../staging-dist`).
- **مُبقاة عمدًا (للـ rollback):** DB dump + `publish-backup-restore-archive-r1-20260716-111459` + `dist-backup-restore-archive-r1-20260716-111459` + config snapshot + baseline manifest + `restore-archive-r1-deploy-ts.txt`.
- لا سكربتات/SQL/توكنات متبقّية.

---

## 9) المحظورات — كلها محترمة

- ❌ لا استرجاع لأيّ تقرير/KPI حقيقيّ — ✅ (التقارير الأربعة المحذوفة soft بقيت 4، لم تُمَسّ).
- ❌ لا Fixtures على الإنتاج — ✅.
- ❌ لا تعديل/حذف بيانات حقيقية، لا Hard Delete — ✅.
- ❌ لا Scheduler / لا Email / لا Notifications — ✅ (email_outbox=0، البوابات معطّلة).
- ❌ لا Push / Merge / Hotfix / استخدام develop — ✅.
- ❌ لا تعديل Workflow/Calendar/Admin Delete خارج الكود المعتمَد — ✅.

---

## 10) خطة الـ Rollback (عند الحاجة، غير مُستدعاة)

- **Frontend/Backend:** استعادة `dist-backup-…-20260716-111459` و`publish-backup-…-20260716-111459` + chown www-data + restart.
- **DB:** عكس الهجرة (DropIndex الجزئيّ ثم CreateIndex كامل فريد) — آمن فقط بعد فحص عدم وجود تكرار غير-محذوف يمنع الفهرس الكامل (الأساس الحاليّ 0 تكرار)، أو استعادة الـ dump.

---

## 11) الحكم النهائي

# ✅ RESTORE / ARCHIVE GOVERNANCE R1 — PRODUCTION DEPLOYMENT **SUCCESSFUL**

- Phases 0–12 مكتملة · هجرة واحدة مطبَّقة (28→29) · Backend+Frontend byte-identical للمرشّح · دخان بلا تحوّر · عدم انحدار · 5 جولات مراقبة نظيفة · كل العدّادات مطابقة للأساس · كل المحظورات محترمة.
- ميزة حوكمة الأرشيف/الاسترجاع **مُقدَّمة فعليًّا على الإنتاج** ومحميّة بالسياسة (401 بلا مصادقة).
- **التقارير الأربعة المحذوفة قديمًا لم تُسترجَع** (خارج نطاق هذا النشر — يتطلّب موافقة تشغيلية منفصلة).

**STOP — انتهى النشر الإنتاجيّ. لا عمل إضافيّ بلا توجيه جديد.**
