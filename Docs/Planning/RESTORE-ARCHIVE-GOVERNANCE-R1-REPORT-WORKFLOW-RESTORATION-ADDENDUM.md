# RESTORE / ARCHIVE GOVERNANCE R1 — REPORT WORKFLOW RESTORATION ADDENDUM

**التاريخ:** 2026-07-16 · **النوع:** ملحق تصميميّ قراءة-فقط بالكامل — يعالج استعادة Workflow التقرير (لا مجرّد `IsDeleted=false`) · **الحالة:** لم يُكتب كود، لا Branch/Worktree، لا Migration، لا تعديل `AdminDeleteAsync`، لا Restore، لا نشر. توقف بعد التقرير بانتظار موافقة صريحة.

---

## Phase 0 — Baseline Reconfirmation (بلا drift)

| البند | القيمة | مطابقة |
|---|---|---|
| Internal / Public health | 200 / 200 | ✓ |
| Migrations count / head | 28 / `20260715162851_AddBypassTeamLeaderApproval` | ✓ |
| report_submissions محذوف ناعمًا / إجمالي | 4 / 67 | ✓ |
| kpi_evaluations محذوف ناعمًا / إجمالي | 0 / 23 | ✓ |
| `archive_item_restored` في audit_logs | 0 | ✓ |

```
REPORT WORKFLOW RESTORE BASELINE = NO DRIFT
```
كل القيم مطابقة للتشخيص القرائيّ السابق. لا انحراف ⇒ نُكمل الملحق.

---

## Phase 1 — Administrative Delete Snapshot Audit

**سؤال Phase 1:** هل `DataJson` للحذف الإداريّ (Before/After) كافٍ لعكس الحذف حرفيًّا واستعادة Workflow قابل للإتمام؟

**الدليل الحيّ (قراءة-فقط) — نموذج DataJson لأحد الأربعة:**
```json
{"reason":"التقرير كان تجربة","periodKey":"2026-W27","submitterId":"8284241a-be8c-42f9-92cf-e6442ea8db61"}
```

**ما يحتويه سجلّ الحذف:** `reason` + `periodKey` + `submitterId` فقط (ملخّص).

**ما لا يحتويه (حاسم لعكس Workflow):**
- `CurrentApproverId` قبل الحذف (صُفِّر إلى null أثناء الحذف).
- قائمة `ApprovalSteps` وحالاتها قبل الحذف (Level/ApproverId/Status/DecidedAtUtc).
- `Status` قبل الحذف.

```
REPORT DELETE AUDIT SNAPSHOT = INCOMPLETE
```

**لكن — مصدر حقيقة بديل ومكتمل موجود في القاعدة نفسها:** الحذف الإداريّ **لا يحذف صفوف `approval_steps`**؛ يحوّل المعلّقة منها إلى `CancelledByAdministrativeDeletion` مع `DecidedAtUtc=وقت الحذف`، ويُبقيها في الجدول. كذلك `Status` على صفّ التقرير **لا يُكتب فوقه** (يبقى Submitted). لذا رغم أن سجلّ الـAudit ناقص، فإن **الحالة الكاملة قبل الحذف قابلة لإعادة البناء من جداول القاعدة مباشرة** (`report_submissions.Status` + صفوف `approval_steps` المحفوظة). النقص في الـAudit ≠ نقص في البيانات.

---

## Phase 2 — Four Deleted Reports Workflow State (قراءة-فقط)

موظّف الأربعة = **خالد مجدي** (`8284241a-be8c-42f9-92cf-e6442ea8db61`) · IsActive=true · ManagerId=`9141ee82` · TeamId=`87611efc`.

| ID (مختصر) | Period | Status (على الصفّ) | CurrentApproverId | عدد ApprovalSteps | الخطوة المحفوظة |
|---|---|---|---|---|---|
| e840df55 | 2026-W23 | Submitted | NULL | 1 | Level=1 · ApproverId=`9141ee82` · CancelledByAdministrativeDeletion |
| 2ada052d | 2026-W25 | Submitted | NULL | 1 | Level=1 · ApproverId=`9141ee82` · CancelledByAdministrativeDeletion |
| 4c883e36 | 2026-W26 | Submitted | NULL | 1 | Level=1 · ApproverId=`9141ee82` · CancelledByAdministrativeDeletion |
| 416de42c | 2026-W27 | Submitted | NULL | 1 | Level=1 · ApproverId=`9141ee82` · CancelledByAdministrativeDeletion |

**صلاحية المعتمِد التاريخيّ:** `9141ee82` = **محمد عبدالقوي** · IsActive=**true** · له مدير أعلى (سلسلة سليمة).

**تحقّق تنظيميّ حاسم:** المعتمِد التاريخيّ `9141ee82` = **نفسه ManagerId الحاليّ** لخالد مجدي (`9141ee82`). ⇒ الاستعادة التاريخيّة (الخطوة المحفوظة) وإعادة الاشتقاق من التنظيم الحاليّ **تُوجِّهان لنفس الشخص النشط**. لا تغيير تنظيميّ يُبطِل المعتمِد. لا تعارض. لا خطوة متعدّدة المستويات (كلها Level=1 مباشِرة للمدير).

---

## Phase 3 — Restoration Strategies (مقارنة)

| البُعد | Option A — Historical Exact Restore | Option B — Current Org Re-Resolution | Option C — Hybrid Safe Restore |
|---|---|---|---|
| المصدر | صفوف `approval_steps` المحفوظة كما هي | إعادة اشتقاق المعتمِد من تنظيم اليوم | التاريخيّ أولًا + تحقّق صلاحية حاضِر + حراسة |
| السلوك | يُعيد `CancelledByAdministrativeDeletion` → Pending، `CurrentApproverId`=ApproverId التاريخيّ | يتجاهل الخطوة المحفوظة، يبني خطوة جديدة بمدير اليوم | يُحيي الخطوة التاريخيّة، لكن يتحقّق أن ApproverId ما زال نشطًا/صالحًا؛ وإلا يحجب أو يطلب حسمًا |
| «يعكس الحذف» | نعم (حرفيًّا) | لا (يُعيد تصميم Workflow صامتًا) | نعم (يعكس، ثم يحرس فقط عند الخطر) |
| خطر المعتمِد المُعطَّل | يُحيي خطوة لمعتمِد قد يكون غير نشط | يتجنّبه بإعادة الاشتقاق | يكتشفه ويحجب برمز واضح |
| التوافق مع تفضيل المستخدم | جزئيّ (يعكس لكن لا يحرس) | يخالف («لا يُعيد تصميم صامتًا») | **مطابق** |
| على الأربعة الحاليين | ينجح (المعتمِد نشط) | ينجح (نفس الشخص) | ينجح (نشط + مطابق) |

**الخلاصة:** الثلاثة تنجح على الأربعة الحاليين لأن المعتمِد التاريخيّ = مدير اليوم = نشط. الفرق يظهر في الحالات المستقبلية (معتمِد مُعطَّل/محذوف/تغيّر تنظيم). Option A تُحيي خطوة ميّتة عمياء؛ Option B تُعيد تصميم Workflow بصمت (يخالف التفضيل)؛ **Option C يعكس الحذف حرفيًّا ويحرس فقط عند الخطر**.

---

## Phase 4 — Semantic Decision

التفضيل الصريح: **«الاستعادة يجب أن تعكس الحذف، لا أن تُعيد تصميم Workflow بصمت.»**

- Option B (Current Org) تُعيد تصميم Workflow بصمت ⇒ **مرفوضة كافتراضيّ**.
- Option A (Historical) تعكس لكنها قد تُحيي معتمِدًا مُعطَّلًا بلا حراسة ⇒ ناقصة.
- Option C (Hybrid) تعكس الحذف بإحياء الخطوة التاريخيّة، ثم تتحقّق من صلاحية المعتمِد التاريخيّ زمن الاستعادة؛ إن صالح ⇒ استعادة تاريخيّة نظيفة؛ إن غير صالح ⇒ **حجب برمز واضح** (لا إعادة تصميم صامتة، لا إحياء أعمى).

```
RESTORE SEMANTICS = HYBRID
```
الأساس التاريخيّ محفوظ (يعكس الحذف)، والحراسة تمنع الإحياء الأعمى دون فرض تصميم جديد صامت. القرار للإدارة عند الحجب (إعادة تقديم / حسم يدويّ)، لا اشتقاق تلقائيّ خفيّ.

---

## Phase 5 — Data Model Impact

- **الأربعة الحاليّون:** قابلون للاستعادة الآن من الجداول (Status محفوظ + صفوف approval_steps محفوظة + المعتمِد نشط) ⇒ **لا حاجة لأيّ عمود جديد ولا جدول جديد لاستعادتهم**.
- **سجلّ الحذف الحاليّ ناقص** (Phase 1) ⇒ للحالات **المستقبلية** يُستحسَن أن يُخزِّن الحذف الإداريّ لقطة Workflow كاملة داخل `DataJson` الـjsonb الحرّ (CurrentApproverId + Status + قائمة الخطوات) — تحسينٌ للـAudit الموجود، **بلا عمود كيان جديد ولا migration بيانات**. يبقى الاعتماد الأساس في الاستعادة على صفوف approval_steps المحفوظة؛ لقطة الـAudit = طبقة تحقّق/تدقيق إضافية.

```
WORKFLOW RESTORE METADATA = AUDIT SNAPSHOT ENHANCEMENT REQUIRED
```
(ليس ENTITY MODEL REQUIRED — لا أعمدة جديدة على الكيان؛ وليس NOT REQUIRED — سجلّ الحذف الحاليّ ناقص ويجب إثراؤه مُستقبلًا داخل الـjsonb القائم.)

---

## Phase 6 — Restore Rules for Reports (+ رموز الحجب)

منطق الاستعادة المقترَح (تصميم فقط، لا كود):

1. قراءة الصفّ المحذوف عبر `IgnoreQueryFilters()`.
2. **تعارض نشط (الفهرس الجزئيّ):** إن وُجد تسليم نشط بنفس (ReportTemplateVersionId, SubmitterId, PeriodKey) ⇒ حجب.
3. **إعادة بناء Workflow:** جمع صفوف `approval_steps` المحفوظة. إن لم توجد أيّ خطوة قابلة لإعادة الإحياء ⇒ حجب لقطة مفقودة.
4. **تحقّق المعتمِد التاريخيّ:** ApproverId للخطوة المُحياة يجب أن يكون موجودًا ونشطًا ⇒ وإلا حجب.
5. عند النجاح: `IsDeleted=false` + مسح حقول الحذف؛ الخطوة `CancelledByAdministrativeDeletion → Pending` (مسح DecidedAtUtc)؛ `CurrentApproverId = ApproverId` للخطوة المُحياة؛ Status يبقى كما هو (Submitted محفوظ)؛ تدقيق `archive_item_restored` مع Before/After في DataJson.

| الرمز | HTTP | الرسالة العربية | متى |
|---|---|---|---|
| `archive.restore_workflow_snapshot_missing` | 409 | «تعذّرت استعادة سير الاعتماد: لا توجد خطوات اعتماد محفوظة قابلة للإحياء لهذا التقرير.» | لا approval_steps قابلة للإحياء |
| `archive.restore_historical_approver_missing` | 409 | «تعذّرت الاستعادة: المعتمِد التاريخيّ لهذا التقرير لم يعد موجودًا في النظام.» | ApproverId التاريخيّ غير موجود |
| `archive.restore_historical_approver_inactive` | 409 | «تعذّرت الاستعادة: المعتمِد التاريخيّ لهذا التقرير موقوف حاليًّا. يلزم حسم إداريّ قبل الاستعادة.» | ApproverId موجود لكن IsActive=false |
| `archive.restore_workflow_conflict` | 409 | «تعذّرت الاستعادة: يوجد تقرير نشط بالفعل لنفس الموظّف والقالب والفترة.» | تعارض الفهرس الجزئيّ النشط |
| `archive.restore_workflow_resolution_required` | 409 | «تعذّرت الاستعادة التلقائيّة لسير الاعتماد: يلزم حسم إداريّ لتحديد المعتمِد قبل الاستعادة.» | حالة غامضة تتطلّب قرار إدارة (لا اشتقاق صامت) |

كل الرموز 409 (تعارض حالة، متّسقة مع نمط `*.conflict` القائم؛ الرسائل في حقل ProblemDetails `type` كما هو معتمَد في المشروع).

---

## Phase 7 — Four Reports Final Verdict

المعتمِد التاريخيّ للأربعة = `9141ee82` (محمد عبدالقوي) · نشط · = مدير الموظّف الحاليّ · لا تعارض نشط لأيٍّ منها.

| ID (مختصر) | Period | الحكم | يعود لـ«بانتظار قراري»؟ |
|---|---|---|---|
| e840df55 | 2026-W23 | RESTORABLE WITH HISTORICAL WORKFLOW | **YES** — لدى محمد عبدالقوي |
| 2ada052d | 2026-W25 | RESTORABLE WITH HISTORICAL WORKFLOW | **YES** — لدى محمد عبدالقوي |
| 4c883e36 | 2026-W26 | RESTORABLE WITH HISTORICAL WORKFLOW | **YES** — لدى محمد عبدالقوي |
| 416de42c | 2026-W27 | RESTORABLE WITH HISTORICAL WORKFLOW | **YES** — لدى محمد عبدالقوي |

```
FOUR DELETED REPORTS = RESTORABLE (ALL WITH HISTORICAL WORKFLOW)
```
كلها تعود Pending عند نفس المعتمِد النشط (لا يُعاد تصميم شيء)، وتظهر ثانيةً في «بانتظار قراري» لمحمد عبدالقوي فور الاستعادة.

---

## Phase 8 — Minimal Design Update (تحديث فوق التقرير السابق)

```
MINIMAL DESIGN (updated):
- No archive table                          (الصفوف المحذوفة ناعمًا هي الأرشيف)
- No StatusBeforeDelete column              (Status محفوظ في الصفّ)
- No Restore metadata columns on entity     (Audit-Only + صفوف approval_steps المحفوظة كافية)
- Existing soft-delete rows = الأرشيف
- Existing approval_steps rows = مصدر إعادة بناء Workflow (يعكس الحذف)
- One KPI index migration only              (full unique → partial WHERE IsDeleted=false)
- Restore reverses delete: revive step (Cancelled→Pending) + set CurrentApproverId + verify approver + guard codes
- Delete audit ENHANCEMENT (prospective): store full workflow snapshot inside existing jsonb DataJson (بلا عمود/migration)
- API + frontend archive page + tests
```
**الفارق الوحيد عن التقرير السابق:** إضافة منطق إحياء Workflow + 5 رموز حجب + إثراء لقطة الحذف داخل الـjsonb القائم مستقبلًا. **لا أعمدة كيان جديدة، لا جدول جديد، لا migration بيانات إضافيّة** (تبقى migration الفهرس الجزئيّ للـKPI الوحيدة).

---

## أحكام الملحق (Phase 9)

```text
REPORT DELETE AUDIT SNAPSHOT = INCOMPLETE
RESTORE SEMANTICS = HYBRID
WORKFLOW RESTORE METADATA = AUDIT SNAPSHOT ENHANCEMENT REQUIRED
FOUR DELETED REPORTS = RESTORABLE
MINIMAL RESTORE/ARCHIVE DESIGN = CONDITIONAL GO
```

**شرط الـCONDITIONAL GO:** المضيّ مشروط بأن يتضمّن التصميم منطق إحياء Workflow (Hybrid) + رموز الحجب الخمسة + إثراء لقطة حذف مستقبليّة داخل الـjsonb القائم — بلا تعديل بنية الكيان. الأربعة الحاليّون قابلون للاستعادة الكاملة فورًا (المعتمِد نشط، لا تعارض).

---

**STOP.** انتهى الملحق. لم يُعدَّل `AdminDeleteAsync`، لا Migration، لا Restore، لا Branch/Worktree، لا RC/نشر، لا تعديل كود/بيانات. بانتظار موافقة صريحة على التصميم الأدنى المُحدَّث قبل بدء أيّ تنفيذ.
