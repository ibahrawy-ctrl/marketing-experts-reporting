# ADMIN-GOVERNANCE-R1 — تقرير قبول RC وقرار GO/NO-GO

**التاريخ:** 2026-07-14 · **البيئة:** `reporting_rc` (الحقيقية، VPS) · **الخدمة:** `khubara-reporting-rc.service` (`http://127.0.0.1:5092`)
**النطاق:** حوكمة الأدمن للتقارير و KPI (حذف بسبب + أثر تدقيق، مسار مراجعة KPI، فصل الواجبات، استبعاد المحذوف من تصدير المالية).
**القرار:** ✅ **GO** — مقبول للترقية إلى الإنتاج، مشروطًا بتنفيذ شرط الـrunbook الإلزامي أدناه.

---

## 1. حالة المراحل (0–7)
| المرحلة | الوصف | الحالة |
|---|---|---|
| 0 | Pre-Flight قراءة فقط على RC (تطابق سلسلة الهجرات) | ✅ |
| 1 | تحقّق المرشّح النهائي (1290/1290 على `reporting_test`) | ✅ |
| 2 | نسخ احتياطية `/root/rc-backups/agov-20260713-215304` | ✅ |
| 3 | تطبيق الهجرة الوحيدة `20260713171040_AdminGovernanceReportKpiCorrection` | ✅ |
| 4 | نشر Backend + Frontend على RC والتحقّق | ✅ |
| 5 | القبول التقني RC | ✅ **31/31** |
| 6 | UAT (12 بندًا) | ✅ **12/12** |
| 7 | تقرير القبول + GO/NO-GO ثم التوقّف قبل الإنتاج | ✅ (هذا المستند) |

## 2. Phase 5 — القبول التقني (31/31)
- حوكمة التقارير R1–R5، حوكمة KPI + استبعاد المالية KA1–KA9، مسار مراجعة KPI مع فصل الواجبات KB1–KB9، فحوص RBAC RB1–RB4.
- **PASSED=31 · FAILED=0.** التفاصيل في `/tmp/AGOV-Phase5-Acceptance-Summary.md`.

## 3. Phase 6 — UAT (12/12، نتائج مرئية للمستخدم)
| البند | المعيار | النتيجة |
|---|---|---|
| U1 | الأدمن يحذف تقريرًا بسبب إلزامي | ✅ 200 |
| U2 | التقرير المحذوف يختفي من القائمة | ✅ مخفيّ |
| U3 | منع حذف تقرير بلا سبب | ✅ 400 `submission.delete_reason_required` |
| U4 | أثر التدقيق يسجّل الحذف (فاعل + سبب) | ✅ مؤكَّد عبر psql (`audit_logs`) بالأسباب العربية |
| U5 | إعادة فتح KPI للتصحيح بسبب | ✅ 200 → UnderReview |
| U6 | الأدمن يحذف تقييم KPI بسبب | ✅ 200 |
| U7 | منع حذف/تصحيح KPI بلا سبب | ✅ 400 `kpi_eval.reason_required` |
| U8 | التقييم المحذوف يُستبعَد من تصدير المالية | ✅ before=1 → after=0 |
| U9 | مسار مراجعة KPI الكامل (submit→approve→reopen→re-approve) | ✅ نهائي Approved |
| U10 | فصل الواجبات: مُدخِل التقييم لا يعتمده | ✅ 403 `auth.forbidden` |
| U11 | RBAC: موظف 403 (تقرير+KPI)، مجهول 401 | ✅ |
| U12 | الجدول الزمني الكامل بالترتيب مع الفاعل | ✅ Submitted→Approved→Reopen→Approved |

**دليل U4 (عيّنة من `audit_logs`):** `submission.admin_deleted` بسبب «UAT: حذف تقرير تجريبي للقبول»؛ `kpi.admin_deleted` بسبب «UAT: حذف تقييم KPI تجريبي» + previousStatus؛ `kpi.reopened` بسبب «UAT: إعادة فتح للتصحيح»؛ و`kpi.approved` مسجَّل بفاعل المراجع المميّز (لا المُدخِل) — يثبت فصل الواجبات في الأثر.

## 4. ⚠️ شرط النشر الإنتاجي الإلزامي (Runbook)
اكتُشِف في Phase 5 أن الجدول الجديد `kpi_evaluation_review_events` — حين تُطبَّق هجرته يدويًّا كـ `postgres` — يُنشأ بلا أي GRANT لدور التطبيق، فيفشل التطبيق وقت التشغيل بـ **`42501: permission denied`**. لذا على الإنتاج **يجب أحد الأمرين**:
1. تشغيل `MigrateAsync()` وقت الإقلاع بدور قادر على الملكية (owner-capable)، **أو**
2. بعد الهجرة مباشرةً:
   ```sql
   ALTER TABLE kpi_evaluation_review_events OWNER TO <prod_owner_role>;
   GRANT SELECT, INSERT, UPDATE, DELETE ON kpi_evaluation_review_events TO <prod_app_role>;
   ```
التحقّق: `\dp kpi_evaluation_review_events` يُظهِر `<app_role>=arwd/<owner_role>`. (يطابق أعطال إقلاع 12 يوليو «permission denied for schema public».)

## 5. سلوكيات مؤكَّدة (ليست عيوبًا)
- **تباعد إعادة الحذف:** التقرير soft-deleted عند إعادة حذفه ⇒ 404 `submission.not_found`؛ بينما KPI ⇒ 409 `already_deleted`.
- **فصل الواجبات:** `EnsureCanReview` يمنع المُدخِل/الـSubject من المراجعة حتى مع صلاحية Admin/CEO/GM.

## 6. حالة RC بعد القبول
- الأساس مُستعاد ومؤكَّد: `review_events=0 · submissions=35 · kpi_evaluations=1 · migrations=27`.
- المُختبِر break-glass `agov-rc-tester@test.local` (باقٍ حتى تنفيذ التنظيف النهائي في §7).

## 7. التنظيف النهائي لـ RC (يُنفَّذ فور اعتماد هذا التقرير)
1. تفريغ مفاتيح `Seed:*` في `/etc/khubara-reporting-rc.env` (النسخة الاحتياطية: `/root/khubara-reporting-rc.env.bak-agovtest-20260713-221015`).
2. إعادة تشغيل `khubara-reporting-rc.service` والتأكّد `/health`=200.
3. إزالة المُختبِر break-glass `agov-rc-tester@test.local`.
4. تأكيد الأساس النهائي: **27 | 0 | 35 | 1 | 35**.

## 8. القرار النهائي
**GO للإنتاج** — بعد تنفيذ شرط §4 ضمن runbook الإنتاج. **التوقّف الآن قبل الإنتاج بانتظار موافقة صريحة** (لا نشر إنتاجي في هذه المرحلة).
