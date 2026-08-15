# RESTORE / ARCHIVE GOVERNANCE R1 — RC UAT — تقرير القبول النهائي

**التاريخ:** 2026-07-16
**البيئة:** Release Candidate (`khubara-reporting-rc.service` @ `http://127.0.0.1:5092`، `ASPNETCORE_ENVIRONMENT=ReleaseCandidate`، قاعدة `reporting_rc`، VPS 187.127.72.232)
**النطاق:** تنفيذ معزول + نشر RC + UAT كامل لميزة حوكمة الأرشيف/الاسترجاع. **لا نشر إنتاجيّ، لا Push/Merge، لا حذف نهائي.**
**الحكم النهائي:** ✅ **RESTORE / ARCHIVE GOVERNANCE R1 — RC UAT = GO**

---

## 1) خطّ الأساس (Baseline) — قبل وبعد UAT

| المؤشّر | قبل UAT (Baseline) | بعد UAT + التنظيف | الحالة |
|---|---|---|---|
| حالة الخدمة | active | active | ✅ لا انحراف |
| Health | 200 `{"status":"ok"}` | 200 `{"status":"ok"}` | ✅ |
| عدد الهجرات | 29 | 29 | ✅ لا انحراف |
| رأس الهجرة | `20260716015239_KpiEvaluationPartialUniqueIndex` | نفسه | ✅ |
| الفهرس الجزئي الفريد | حيّ | حيّ | ✅ |
| `audit_logs` | 559 | 559 | ✅ مُعادة |
| `notifications` | 70 | 70 | ✅ لم تتغيّر |
| `email_outbox` | 0 | 0 | ✅ لم تتغيّر |
| عناصر محذوفة (soft) | 0 | 0 | ✅ |
| مستخدمو Fixtures | 0 | 0 | ✅ مُنظَّفون |

**BASELINE_TS = `2026-07-16 09:33:47.964663+00`.** لا انحراف في أيّ من: الهجرة، الـ Workflow، صلاحيات الاسترجاع، عدّادات القاعدة. **لم يُطلَق أيّ `STOP — DRIFT DETECTED`.**

---

## 2) نتيجة مصفوفة UAT (Backend)

**37/37 PASS.**

### أ) الصلاحيات (Policy `ArchiveGovernanceAccess`)
- Admin / CEO / GeneralManager ⟶ **200** (مسموح).
- Manager ⟶ **403**، وكل بقية الأدوار ⟶ **403**.
- Anonymous ⟶ **401**.

### ب) قائمة الأرشيف
- القائمة + الفلاتر + الترقيم (pagination) + تفاصيل العنصر (report/kpi) ⟶ تعمل بشكل صحيح.

### ج) مسارات الاسترجاع (Restore)
| السيناريو | Fixture | النتيجة المتوقّعة | الفعليّة |
|---|---|---|---|
| استرجاع تقرير بمعتمِد تاريخيّ نشط | R_A (2026-W11, Submitted) | `HistoricalApproverRestored` (200) — يضبط `CurrentApproverId` ويُعيد تفعيل الخطوة الملغاة | ✅ |
| استرجاع تقرير مُغلق بلا معتمِد نشط | R_B (2026-W12, Closed) | `NoActiveApprover` (200)، `CurrentApproverId` يبقى null | ✅ |
| تعارض توأم نشط | R_C + R_C_TWIN (2026-W13) | `archive.restore_active_conflict.conflict` (409) | ✅ |
| معتمِد تاريخيّ غير نشط | R_D (2026-W14) | `archive.restore_approver_inactive.conflict` (409) | ✅ |
| تعارض Workflow (يتطلّب حسمًا) | R_E (2026-W15، خطوتان ملغاتان) | `archive.restore_resolution_required.conflict` (409) | ✅ |
| سبب قصير/غير صالح (< 10 أو > 500 حرفًا) | — | `archive.restore_reason_invalid` (400) | ✅ |

### د) استرجاع KPI مع حفظ النتائج والأحداث
- K_A (2026-W11, Approved, TotalScore 80) + نتيجتان (M1، M2) + حدث مراجعة ⟶ الاسترجاع نجح، **حُفظت كل `kpi_results`**، وأُضيف حدث `KpiEvaluationReviewEvent` بـ `Action = "AdminRestored"` + السبب. النتيجة والأوزان بقيت كما هي.

### هـ) سلوك الفهرس الجزئي (Partial Unique Index)
- إدراج **نسخة محذوفة** بنفس المفتاح (RTV+Submitter+PeriodKey) ⟶ **مقبول** (الفهرس مشروط `WHERE IsDeleted=false`).
- نسخة **غير محذوفة** مكرّرة ⟶ مرفوضة (23505). كلا الاتجاهين مؤكَّدان.

### و) التدقيق (Audit)
- كل استرجاع ناجح كتب صفًّا واحدًا `archive_item_restored` (EntityType = `ReportSubmission` أو `KpiEvaluation`)، الإجمالي +3 (2 تقرير + 1 KPI). لا صفوف بفاعل غير-Fixture.

### ز) لا إشعارات / لا بريد
- `notifications` = 70 (لم تتغيّر)، `email_outbox` = 0. **لا مسار استرجاع يُصدِر إشعارًا أو بريدًا.**

---

## 3) نتيجة UAT (Frontend)

- **21/21 vitest** خضراء (`AdminArchivePage.test.tsx` 13 + `useArchive.test.tsx` 8): Toast نجاح/خطأ، تعطيل زر الاسترجاع حتى إدخال سبب صالح، حارس النقر المزدوج (`isPending`)، عرض القائمة، بناء باراميترات الاستعلام، اشتقاق مسار الاسترجاع.
- الـ bundle الحيّ على RC (`index-2bl8xs_4.js`) يحوي كل علامات ميزة الأرشيف — الميزة مُقدَّمة فعليًّا عبر nginx.

---

## 4) التحقّق من التقارير المحذوفة القديمة (Legacy)

**LEGACY DELETED REPORT VALIDATION = NOT APPLICABLE**
**Reason: RC baseline contains zero pre-existing soft-deleted reports.**

(Phase 23 — اختبار أحد التقارير الأربعة المحذوفة قديمًا — أُسقِط بموافقة صريحة لأن خطّ أساس RC لا يحوي أيّ عنصر محذوف مسبقًا.)

---

## 5) التنظيف وإعادة الـ Baseline (Phase 24)

نُفِّذ بالكامل عبر SQL (لتفادي تلويث الـ audit بأحداث حذف المستخدمين):
1. حُذفت أبناء التقارير (`approval_steps`) ثم التقارير الخمسة + `R_C_TWIN`.
2. حُذفت أبناء KPI (`kpi_evaluation_review_events`، `kpi_results`) ثم `K_A`.
3. حُذف 7 مستخدمي Fixtures + admin الـ Fixture (`7e8dbae9-...`) عبر SQL (refresh_tokens + AspNetUserRoles/Claims/Logins/Tokens + AspNetUsers).
4. حُذفت 3 صفوف `archive_item_restored` (≥ Baseline) ⟶ `audit_logs` عادت 559.
5. أُعيد `/etc/khubara-reporting-rc.env` من النسخة الاحتياطية `/root/khubara-reporting-rc.env.bak-archr1uat` (مفاتيح Seed فارغة) + إعادة تشغيل الخدمة ⟶ **admin الـ Fixture login = 401**.
6. حُذفت كل سكربتات UAT (`/root/archr1-*.sh` و`/root/archr1-uat-ids.env`) والسكربتات المحلية (`/tmp/archr1`).

**تحقّق ما بعد التنظيف:** الخدمة active، health 200، migrations = 29 (الرأس نفسه)، العدّادات 559/70/0، عناصر محذوفة = 0، مستخدمو Fixtures = 0.

**آثار مُبقاة عمدًا (ليست Fixtures):** `/root/archr1-rc-deploy-ts.txt` (سجلّ نشر RC من Phase 20)، و`khubara-reporting-rc.env.bak-archr1uat` (نسخة احتياطية للـ rollback).

---

## 6) المحظورات — كلها محترمة

- ❌ لا نشر إنتاجيّ — ✅ محترم (RC فقط).
- ❌ لا Push / Merge — ✅ محترم.
- ❌ لا حذف نهائي (Hard Delete) على بيانات إنتاجية — ✅ (الحذف اقتصر على Fixtures RC فقط).
- ❌ لا Scheduler / لا Email / لا Notifications — ✅ محترم.
- ❌ لا بدء أيّ Hotfix أو Phase أخرى — ✅ محترم.

---

## 7) الحكم النهائي

# ✅ RESTORE / ARCHIVE GOVERNANCE R1 — RC UAT = **GO**

- Backend 37/37 PASS · Frontend 21/21 PASS · Baseline مُعادة بالضبط · لا انحراف · كل المحظورات محترمة.
- النشر الإنتاجيّ يبقى **معلّقًا على موافقة صريحة منفصلة** (خارج نطاق هذا الـ UAT).

**STOP — انتهت Phases 0–25. لا عمل إضافيّ بلا توجيه جديد.**
