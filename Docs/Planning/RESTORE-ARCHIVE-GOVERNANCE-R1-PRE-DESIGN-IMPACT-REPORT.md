# RESTORE / ARCHIVE GOVERNANCE R1 — PRE-DESIGN IMPACT REPORT

**التاريخ:** 2026-07-16 · **النوع:** تقرير تأثير قبل التصميم — قراءة-فقط بالكامل · **الحالة:** لم يُكتب كود، لا Branch/Worktree، لا Migration، لا Backup، لا تغيير Index، لا Restore، لا نشر. توقف بعد التقرير.

---

## 1. Source Provenance (إعادة تأكيد Phase 0 — بلا drift)
- المصدر المطابق للإنتاج = `92b8c01` (Approval UX، 27 migration) **+ دلتا Fatma Direct Reporting Override (Backend فقط)** ⇒ 28 migration، head `20260715162851_AddBypassTeamLeaderApproval`.
- **تحقّق حيّ (قراءة-فقط):** count = **28**، head = **`20260715162851`** — مطابق.
- النَّسَب: Admin Governance ✓ · Role-Aware Calendar ✓ · Nav Hotfix ✓ · Fatma DR ✓ · Approval UX ✓. العمل الاستراتيجي (Workstreams/Deliverables/Client360/Execution Taxonomy/Flexible Positions) **غائب**.

## 2. Production Baseline (بلا drift مقابل التشخيص السابق)
| البند | القيمة |
|---|---|
| Internal / Public health | 200 / 200 |
| Migrations count / head | 28 / `20260715162851` |
| report_submissions محذوف ناعمًا / إجمالي | 4 / 67 |
| kpi_evaluations محذوف ناعمًا / إجمالي | 0 / 23 |
| أعمدة الحذف الناعم (الجدولان) | `IsDeleted`(bool NOT NULL)، `DeletedAtUtc`، `DeletedByUserId`، `DeletionReason` — متطابقة |
| Global Query Filter | `!IsDeleted` على الكيانين |

`STATE = NO BASELINE DRIFT` (كل القيم مطابقة للتشخيص القرائي السابق).

## 3. KPI Index Definition
```
IX_kpi_evaluations_KpiTemplateVersionId_SubjectUserId_PeriodKey
  UNIQUE btree ("KpiTemplateVersionId","SubjectUserId","PeriodKey")   -- بلا WHERE، بلا Expression
```
- أعمدة: KpiTemplateVersionId, SubjectUserId, PeriodKey. لا شروط إضافية، لا تعبيرات.
- مقابل Reports: `... ("ReportTemplateVersionId","SubmitterId","PeriodKey") WHERE ("IsDeleted"=false)` (جزئيّ).

## 4. Duplicate Scan (KPI — قراءة-فقط)
| الفحص | العدد |
|---|---|
| مجموعات Active مكرّرة (نفس المفتاح، >1 نشط) | **0** |
| مجموعات فيها Active + Soft-Deleted معًا | **0** |
| مجموعات فيها >1 Soft-Deleted لنفس المفتاح | **0** |
| إجمالي KPI محذوف ناعمًا | **0** |

## 5. Partial Index Safety
تحويل الفهرس إلى `WHERE "IsDeleted"=false` يُضيّق نطاق فرض التفرّد على **الصفوف النشطة فقط** — وهو **تخفيف** لقيد يستوفيه مجموع الصفوف النشطة الحالي أصلًا (0 تكرار من أي نوع، 0 محذوف). لا صفّ قائم يخالف الفهرس الجزئيّ الجديد ⇒ عملية `CREATE UNIQUE INDEX ... WHERE` لا يمكن أن تفشل على بيانات الإنتاج الحالية.

```
KPI PARTIAL INDEX IMPACT = SAFE
```
السبب: 0 تكرار نشط + 0 محذوف ⇒ الفهرس الجزئيّ ضِمنيًّا مُستوفًى، والتحويل استرخاء قيد لا تشديده.

## 6. Status Preservation Evidence (من الكود)
**Report `AdminDeleteAsync` (SubmissionService.cs:628–667):**
- `Status`: **لا يُعدَّل** (يبقى Submitted). — لا حاجة لعمود منفصل.
- `CurrentApproverId` → `null`؛ خطوات الاعتماد المعلّقة → `CancelledByAdministrativeDeletion` (تجميد Workflow، ليس تغيير قيمة Status).
- Field values / results: **لا تُمَسّ**.

**KPI `AdminDeleteAsync` (KpiEvaluationService.cs:450–479):**
- `Status`: **لا يُعدَّل** (`fromStatus=e.Status` للأثر فقط).
- `Results`: مُضمَّنة لكن **لا تُعدَّل**.
- Workflow/approver: لا يُمَسّ؛ يُضاف فقط `ReviewEvent "AdminDeleted"` (سجلّ إلحاقيّ) + snapshot.

في الحالتين قيمة `Status` الأصلية محفوظة **داخل الصفّ نفسه** في عمود `Status` (لا يُكتب فوقها الحذف) ⇒ الاستعادة تقرؤها مباشرة.

```
STATUS BEFORE DELETE COLUMN = NOT REQUIRED
```

## 7. Audit Capability
`audit_logs`: `Id, ActorId, Action, EntityType, EntityId, DataJson(jsonb), IpAddress, CreatedAtUtc, UpdatedAtUtc`.
`IAuditService.LogAsync(actorId, action, entityType, entityId, dataJson?, ipAddress?)`.
الحذف الحاليّ يُسجَّل بـ`DataJson` مُنظَّم (مثال حيّ: `{"reason":"...","periodKey":"2026-W27","submitterId":"..."}`).

`archive_item_restored` يُغطَّى بالكامل: Action/EntityType/EntityId/ActorId/Timestamp أعمدة أصلية؛ **Reason + BeforeJson + AfterJson** تُدرَج داخل `DataJson` الـjsonb الحرّ. لا حاجة لجدول جديد ولا أعمدة جديدة.

```
RESTORE AUDIT USING EXISTING AUDIT = SUPPORTED
```

## 8. Restore Metadata Decision
- **Option A (Audit Only):** الاستعادة تمسح حقول الحذف؛ تاريخ/فاعل/سبب الاستعادة كاملة في `audit_logs` (استعلام بـ`EntityId + Action=archive_item_restored`). يحقّق: التتبّع ✓ · العرض في التفاصيل ✓ · المراجعة المستقبلية ✓ · عدم فقد السبب ✓.
- **Option B (أعمدة على الكيان):** `RestoredAt/RestoredByUserId/RestoreReason` — سطح هجرة أوسع بلا فائدة إضافية.

بما أن Option A يحقّق كل المتطلبات:

```
RESTORE METADATA COLUMNS = NOT REQUIRED
```

## 9. الأربعة تقارير المحذوفة — حالة كل واحد (قراءة-فقط)
موظف الجميع = **خالد مجدي** · القالب = **📞 تقرير قائد فريق مبيعات B2C** · Status = **Submitted** · محذوف بواسطة **مدير النظام** · السبب **«التقرير كان تجربة»** · بتاريخ 2026-07-14 · user/version/template **موجودون جميعًا** · **لا تعارض نشط لأيٍّ منها**.

| ID (مختصر / كامل) | Period | FieldValues | ApprovalSteps | ActiveConflict | الحكم |
|---|---|---|---|---|---|
| e840df55 / e840df55-8628-4af0-b2a0-5129a1ab42a9 | 2026-W23 | 0 | 1 | 0 | RESTORABLE NOW |
| 2ada052d / 2ada052d-a2eb-4e17-928b-37e08e7f5063 | 2026-W25 | 28 | 1 | 0 | RESTORABLE NOW |
| 4c883e36 / 4c883e36-2949-4db7-8705-86b5f741fef0 | 2026-W26 | 28 | 1 | 0 | RESTORABLE NOW |
| 416de42c / 416de42c-628b-4fa6-ade8-e9097d2f2a33 | 2026-W27 | 0 | 1 | 0 | RESTORABLE NOW |

> W23/W27: 0 قيم حقول = تقارير Submitted فارغة صالحة (ليست فشل تبعية).

```
Deleted Reports:
Restorable = 4
Active Conflict = 0
Missing Dependency = 0
```

## 10. Migration Decision
- **مطلوبة؟ نعم — واحدة فقط.**
- محتواها الحصريّ: `DROP` فهرس KPI الفريد الكامل + `CREATE UNIQUE INDEX ... WHERE "IsDeleted"=false` (مطابقة لسلوك Reports). إضافية/آمنة (أُثبِت SAFE في §5). لا AddColumn، لا تغيير بيانات.
- لا هجرة لأعمدة StatusBeforeDelete/Restore (كلاهما NOT REQUIRED).

## 11. Minimal Design Recommendation
```
MINIMAL DESIGN:
- No archive table                       (الصفوف المحذوفة ناعمًا هي الأرشيف)
- No StatusBeforeDelete column           (Status محفوظ في الصفّ)
- No Restore metadata columns            (Audit-Only كافٍ)
- Existing soft-delete rows are the archive
- Existing audit_logs store restore history (archive_item_restored + Before/After في DataJson)
- One KPI index migration only           (full unique → partial WHERE IsDeleted=false)
- API + frontend archive page + tests
```
**أقل عدد ملفات متوقَّع (تقديريّ، للتصميم اللاحق فقط):**
- Backend ≈ 6–9: Controller أرشيف (list/detail/restore) + ArchiveService (يقرأ عبر IgnoreQueryFilters ويستعيد) + Models/DTOs + سياسة RBAC (Admin/CEO/GM) + دالّتا Restore على خدمتَي Submission/Kpi + هجرة KPI واحدة.
- Frontend ≈ 4–5: صفحة `/app/admin/archive` (Tabs Reports/KPI) + hooks + types + عنصر تنقّل + إعادة استخدام Toast الحاليّ (Approval UX).
- اختبارات: Backend ≥ 14 · Frontend ≥ 10.
- **R1 بلا جدول Archive جديد = ممكن ومُثبَت.**

## 12. المخاطر والقيود
- **تعارض استعادة Reports مستقبلًا:** الفهرس الجزئيّ يعني أن استعادة تقرير بينما يوجد نشط بنفس (النسخة، المُرسِل، الفترة) ⇒ يجب أن تُعيد الخدمة 409 `archive.restore_active_conflict`. حاليًّا لا تعارض لأيٍّ من الأربعة، لكن التصميم يجب أن يحرس ذلك زمن الاستعادة.
- **استعادة Workflow للتقرير:** الحذف صفّر `CurrentApproverId` وألغى الخطوات المعلّقة. قرار التصميم لاحقًا: هل الاستعادة تُعيد اشتقاق المعتمِد الحاليّ / تُعيد تفعيل الخطوة، أم تستعيد الصفّ بحالته المجمَّدة؟ (لا يؤثّر على قرارات هذا التقرير الخمسة.)
- **KPI Global Query Filter + الفهرس:** الاستعادة/الإنشاء يجب أن يمرّا عبر `IgnoreQueryFilters` للقراءة؛ والفهرس الجزئيّ الجديد يمنع تكرار النشط فقط.
- **بلا Email/Notification في R1** (كما هو مطلوب).
- القيد الحاليّ: كل ما سبق قراءة-فقط؛ لم يُنفَّذ أي تغيير.

---

## الأحكام الخمسة
```text
KPI PARTIAL INDEX IMPACT = SAFE
STATUS BEFORE DELETE COLUMN = NOT REQUIRED
RESTORE AUDIT USING EXISTING AUDIT = SUPPORTED
RESTORE METADATA COLUMNS = NOT REQUIRED
MINIMAL RESTORE/ARCHIVE DESIGN = GO
```

---

**STOP.** انتهى تقرير التأثير. لم يبدأ Phase 3/التنفيذ، لا Migration، لا Worktree، لا تعديل كود/بيانات، لا نشر. بانتظار موافقة صريحة جديدة على التصميم الأدنى.
