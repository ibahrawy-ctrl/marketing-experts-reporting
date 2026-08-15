# RESTORE / ARCHIVE GOVERNANCE R1 — CURRENT DELETE BEHAVIOR DIAGNOSIS

**التاريخ:** 2026-07-16 · **النوع:** تشخيص قراءة-فقط (Read-Only) للإنتاج + مراجعة مصدر مطابق · **الحالة:** RC غير منفَّذ بعد — توقف قبل التنفيذ بانتظار الموافقة.

---

## Phase 0 — Source Provenance Gate (إثبات المصدر المطابق للإنتاج)

**الحكم: `PRODUCTION SOURCE PROVENANCE = PROVEN`.**

سلسلة الـ commits خطّية:

```
11a31b1  ADMIN-GOVERNANCE-R1 (منشور على الإنتاج)
  ↓
dc23bdd  Role-Aware Reporting Calendar Phase 2
  ↓
4a3ff8c  Daily calendar (Friday-only holiday)   ← ذروة Role-Aware Calendar
  ↓  (+ دلتا Backend غير مُودَعة: Fatma Direct Reporting Override = migration 20260715162851)
50145fd  NAVIGATION-REGRESSION-HOTFIX-R1 (منشور — Frontend فقط)
  ↓
92b8c01  APPROVAL-ACTION-UX-R1 (منشور — Frontend فقط)
```

**المصدر المطابق للإنتاج = `92b8c01` + دلتا Fatma Direct Reporting Override (Backend فقط) من `fatma-dr-worktree`.**

| البُعد | القيمة | مطابقة الإنتاج |
|---|---|---|
| Frontend/Backend tree | `92b8c01` (approval-action-ux-r1) | ✓ (bundle الإنتاج = Approval UX) |
| Migrations مُودَعة في 92b8c01 | 27 (آخرها `20260713171040_AdminGovernanceReportKpiCorrection`) | — |
| دلتا Fatma (Backend فقط) | +1 migration `20260715162851_AddBypassTeamLeaderApproval` | ✓ |
| **الإجمالي** | **28 migration، head = `20260715162851`** | ✓ = الإنتاج تمامًا |
| nav-hotfix + approval-ux مقابل 4a3ff8c | **Frontend فقط** (لا ملفات backend) | ✓ متعامد مع دلتا Fatma الـBackend-only |

**التحقق من الإنتاج (قراءة-فقط):** `SELECT count(*) FROM __EFMigrationsHistory` = **28**؛ head = **`20260715162851_AddBypassTeamLeaderApproval`**. مطابق للمصدر.

**عناصر النَّسَب المطلوبة (كلها حاضرة):** Admin Governance ✓ · Role-Aware Calendar ✓ · Nav Hotfix ✓ · Fatma Direct Reporting (migration 20260715162851) ✓ · Approval Action UX (ActionResultToast.tsx) ✓.

**العمل الاستراتيجي (كله غائب — لا تسريب):** Workstreams ✗ · Deliverables ✗ · Client360 ✗ · Execution Taxonomy ✗ · Flexible Positions ✗. (فحص مجلد الهجرات + `git diff --name-only` = صفر تطابق.)

**ملاحظة حاسمة:** دلتا Fatma موجودة كتغييرات **غير مُودَعة** في `/private/tmp/fatma-dr-worktree` (على 4a3ff8c): 5 ملفات معدَّلة (ApplicationUser, AppDbContextModelSnapshot, LeaveRequestService, SubmissionService, TestAuth) + migration جديدة + FatmaDirectReportingTests. بما أن 50145fd/92b8c01 = Frontend فقط فوق 4a3ff8c، فإن دلتا Fatma الـBackend تنطبق نظيفةً فوق 92b8c01 ⇒ تُعيد بناء حالة الإنتاج بالضبط. **هذا هو الأساس المُستخدَم لعزل RC في Phase 13.**

---

## Phase 1 — Production Read-Only Diagnosis

### 1.1 أعمدة الحذف الناعم (موجودة على الجدولين — متطابقة)

| العمود | report_submissions | kpi_evaluations |
|---|---|---|
| `IsDeleted` | boolean NOT NULL | boolean NOT NULL |
| `DeletedAtUtc` | timestamptz NULL | timestamptz NULL |
| `DeletedByUserId` | uuid NULL | uuid NULL |
| `DeletionReason` | varchar NULL | varchar NULL |

### 1.2 Global Query Filter (كلاهما مُفعَّل)
- `SubmissionConfigurations.cs:31` → `HasQueryFilter(x => !x.IsDeleted)`
- `KpiConfigurations.cs:68` → `HasQueryFilter(x => !x.IsDeleted)`

كلا الكيانين يُخفي الصفوف المحذوفة ناعمًا من كل القوائم/التجميعات تلقائيًّا. استرجاعها يتطلب `IgnoreQueryFilters()`.

### 1.3 منطق الحذف في الكود

**Reports — مساران:**
1. `SubmissionService.DeleteDraftAsync` (486–507): **HARD DELETE** (`_db.ReportSubmissions.Remove`) — لكن **حصرًا لصاحب المسودة نفسه وحالة Draft فقط** (غير مُرسَل). الأدمن لا يحذف مسودة غيره. Audit `submission.draft_deleted`. **غير مشمول بالأرشفة** (المسودة لم تُرسَل/تُجمَّع قط).
2. `SubmissionService.AdminDeleteAsync` (628–667): **SOFT DELETE** — `IsDeleted=true` + DeletedAtUtc/DeletedByUserId/DeletionReason؛ خطوات الاعتماد المعلّقة → `CancelledByAdministrativeDeletion`؛ `CurrentApproverId=null`. Audit `submission.admin_deleted`. الصلاحية: `Roles.AdminReportKpiDeleters` (Admin/CEO/GM).

**KPI — مسار واحد:**
- `KpiEvaluationService.AdminDeleteAsync` (450–472): **SOFT DELETE فقط** — `IsDeleted=true` + الحقول؛ ReviewEvent `AdminDeleted`. الصلاحية: `Roles.AdminReportKpiDeleters`.
- **لا يوجد أي مسار Hard Delete للـKPI إطلاقًا** (`grep KpiEvaluations.Remove` = صفر).

### 1.4 الفهارس الفريدة — **تباين حرج**

| الجدول | الفهرس الفريد للفترة | مُدرِك للحذف الناعم؟ |
|---|---|---|
| report_submissions | `(ReportTemplateVersionId, SubmitterId, PeriodKey)` **`WHERE IsDeleted = false`** (جزئيّ) | **نعم** ✓ |
| kpi_evaluations | `(KpiTemplateVersionId, SubjectUserId, PeriodKey)` — **فريد كامل، بلا فلتر** | **لا** ✗ |

**الأثر:**
- **Reports:** الحذف الناعم يُحرِّر فتحة الفترة ⇒ يُمكن إعادة تسليم نفس الفترة. لكن الاستعادة قد **تتعارض** إن وُجد تسليم نشط لنفس (النسخة، المُرسِل، الفترة) ⇒ حالة `restore_active_conflict` (409) يجب معالجتها.
- **KPI:** الصفّ المحذوف ناعمًا **يبقى يشغل** فتحة الفهرس الفريد. النتيجة: (أ) الاستعادة آمنة من التعارض (الفتحة ما زالت له)، لكن (ب) **لا يمكن إنشاء تقييم KPI جديد لنفس الفترة بينما يوجد محذوف ناعمًا** — الفهرس يمنع بغضّ النظر عن IsDeleted. هذا تباين مع سلوك Reports ويُعالَج في Phase 8 بتحويل الفهرس إلى جزئيّ `WHERE IsDeleted = false`.

### 1.5 البيانات المحذوفة حاليًّا على الإنتاج (مرشّحات الاستعادة)

| الجدول | محذوف ناعمًا | الإجمالي |
|---|---|---|
| report_submissions | **4** | 67 |
| kpi_evaluations | **0** | 23 |

الأربعة تقارير المحذوفة (كلها `Submitted`، سبب «التقرير كان تجربة»، بتاريخ 2026-07-14):

| Id | Status | PeriodKey | DeletedAtUtc |
|---|---|---|---|
| 416de42c-628b-4fa6-ade8-e9097d2f2a33 | Submitted | 2026-W27 | 2026-07-14 18:37:51Z |
| 4c883e36-2949-4db7-8705-86b5f741fef0 | Submitted | 2026-W26 | 2026-07-14 18:37:37Z |
| 2ada052d-a2eb-4e17-928b-37e08e7f5063 | Submitted | 2026-W25 | 2026-07-14 18:37:09Z |
| e840df55-8628-4af0-b2a0-5129a1ab42a9 | Submitted | 2026-W23 | 2026-07-14 18:36:39Z |

---

## الحكم (Phase 2)

```text
CURRENT ADMINISTRATIVE DELETE (Reports + KPI) = SOFT DELETE (RECOVERABLE)
NO DESTRUCTIVE ADMIN HARD-DELETE FOUND
```

- **الحذف الإداريّ للتقارير و KPI = ناعم بالكامل** عبر `IsDeleted` + الحقول المصاحبة + Global Query Filter. **قابل للاستعادة بنيويًّا.**
- **مسار الـHard Delete الوحيد** = `DeleteDraftAsync` المقصور على **مسودة المستخدم نفسه غير المُرسَلة** — ليس حذفًا هدّامًا لبيانات حيّة/مُجمَّعة، وخارج نطاق الأرشفة.
- **البيانات آمنة:** لا توجد إزالة صفوف للبيانات المُرسَلة/المُجمَّعة. الأربعة تقارير المحذوفة ما زالت في القاعدة قابلة للاستعادة.

**⇒ لا حذف هدّام ⇒ لا داعي لـSTOP الطارئ. يُمكن المضيّ لتصميم Archive/Restore.**

## الفجوات المطلوب معالجتها في التصميم (Phase 3+)
1. **لا واجهة/سطح إداريّ للاستعراض والاستعادة** — الصفوف المحذوفة مخفيّة تمامًا بلا شاشة أرشيف ولا endpoint استعادة.
2. **تباين فهرس KPI** — فهرس فريد كامل (غير جزئيّ) ⇒ Phase 8: هجرة تحويله إلى جزئيّ `WHERE IsDeleted=false` (لتماثل سلوك Reports وتفادي حجب إنشاء تقييم جديد بعد الحذف).
3. **معالجة تعارض استعادة Reports** — `restore_active_conflict` (409) عند وجود نشط بنفس الفترة.
4. **لا Audit استعادة** — يلزم `archive_item_restored` مع BeforeJson/AfterJson (بلا email/notification في R1).

---

**التوقف هنا (بوابة Phase 2):** لم يُكتب أيّ كود، لم تُنشأ فروع/worktrees، لم يُمسّ الإنتاج. بانتظار الموافقة على المضيّ إلى Phase 3–13 (التصميم + البناء المعزول + الاختبار)، ثم Phase 14 (RC + UAT)، ثم التوقف قبل الإنتاج.
