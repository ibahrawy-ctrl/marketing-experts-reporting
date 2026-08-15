# REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — تقرير نشر الإنتاج والإغلاق

> التاريخ: 2026-07-20 · البيئة: **Production فقط** · النطاق: Backend + Frontend، **بلا Migration/Seeder** · المصدر: artifact `3eee204` حصرًا · الحكم النهائي في نهاية التقرير.

---

## 0) الحكم النهائي
**PRODUCTION DEPLOYED AND VALIDATED — TICKET CLOSED.**

نُشِر الكود (Backend + Frontend) من نفس artifact `3eee204` المعتمَد على RC، بلا أي هجرة، مع تطابق hash بايت-ببايت، وصحّة إقلاع نظيفة، وتحقّق أعمال حيّ على بيانات إنتاجية حقيقية. البوّابات التشغيلية (Email/Reminders/Scheduler) بقيت **دون أي تغيير** (انظر §6). لا Moderation في هذه الجولة. **التذكرة مُغلقة.**

---

## 1) المصدر المنشور (Provenance)
- **Commit:** `3eee204` (فرع `relineage/unified-status-on-bd84115`، parent `bd84115` = لقطة حالة الإنتاج: Restore/Archive Governance R1 + Fatma-Direct + Admin Governance + BypassTeamLeader + KpiPartialIndex).
- **الهجرات:** التذكرة تضيف **0 هجرة**. رأس هجرات `3eee204` = `20260716015239_KpiEvaluationPartialUniqueIndex` (مطابق لحالة الإنتاج قبل النشر).
- **الاعتماد:** نُشِر بعد «RC DEPLOYED AND UAT PASSED — READY FOR PRODUCTION APPROVAL» وموافقة صريحة «APPROVED — PROCEED WITH PRODUCTION DEPLOYMENT».

---

## 2) PHASE P0 — Pre-Flight (النتيجة: PRODUCTION PRE-FLIGHT PASSED — SAFE TO DEPLOY)
- تطابق كل فحوص ما قبل النشر المحلية + الإنتاجية.
- **الانحراف الوحيد المرصود (سابق-الوجود، ليس تغيير نشر):** الإنتاج يشغّل `Reminders__Enabled=true` و`Scheduler__Enabled=true` (بينما افتراض pre-flight الأصلي كان false). طُرِح على صاحب القرار، والموافقة كانت: «تابِع مع ترك التهيئة كما هي، لا تلمس `/etc/reporting-api.env`». وُثِّق كحالة إنتاجية قائمة وانحراف عن افتراض pre-flight، لا كتغيير نشر.

## 3) PHASE P1 — النسخ الاحتياطية (شرط إلزامي — تمّ)
- TS = `20260720-064341`.
- **DB:** `/root/db-backups/reporting_prod-preunifiedstatus-20260720-064341.dump` (813001 bytes، `pg_dump -Fc`).
- **Backend:** `/opt/reporting/publish-backup-unifiedstatus-20260720-064341` (107M).
- **Frontend:** `/opt/reporting/reporting-frontend/dist-backup-unifiedstatus-20260720-064341` (1.5M، bundle القديم `index-Bpd--Clz.js`).
- علامة TS: `/root/unified-status-prod-deploy-ts.txt`.

## 4) PHASE P2 — نشر Backend (النتيجة: نجاح)
- rsync مع استبعاد `appsettings.Development.json`/tests/logs، `chown www-data`، restart. **بلا هجرة يدويّة.**
- **تطابق hashes بايت-ببايت مع artifact `3eee204`** (مؤكَّد ما بعد النشر في §6.4).

| DLL | SHA-256 (منشور على الإنتاج) |
|---|---|
| Reporting.Api | `abe565f212fee117e73ebbc8c53525203bdd21ff1c2d794ae31719637a52ffcb` |
| Reporting.Application | `eff4d5d84b0a758614488133b0e2f90aed15dd24d866f77568d446a573db7853` |
| Reporting.Domain | `006d4509ca7b457fbaae42a49c4ada1ec468b3b0ad369b9c5026e04903a40379` |
| Reporting.Infrastructure | `1fdf5d4c48721b20e16803185c081134ab86c36c3fe6846d14e91be799dd48ae` |

- سجلّ الإقلاع: **«No migrations were applied. The database is already up to date.»** + «Now listening on: http://127.0.0.1:5090» + «Hosting environment: Production».
- الخدمة `reporting-api.service` = **active + enabled**؛ health داخلي = **200**.

## 5) PHASE P3 + P4 — نشر Frontend + Smoke Test (النتيجة: نجاح)
- **Frontend bundle:** `index-Bpd--Clz.js` → **`index-6J6SJ-kI.js`** (يُخدَم عبر HTTPS، `index.html` يشير إليه، SPA deep-link=200، 0 تسريب localhost، `/api` same-origin).
- **Smoke (قراءة فقط، بلا كتابة):** health عام=200؛ دخول break-glass admin=200؛ `GET /api/reporting-calendar/my-cycles`=200 يُعيد الحقل الموحّد `unified` مع الحقول القديمة (legacy) متعايشَين؛ نقاط الانحدار (dashboard/kpi-templates/report-templates)=200.

## 6) PHASE P5 — التحقّق الأعمال الحيّ (على بيانات إنتاجية حقيقية، بلا إنشاء)
- **السيناريو الأساسي (أحمد نصار، id `1c7f0896-…`، مطوّر ويب):**
  - **W28 = Closed** (سُلِّم وأُغلق 2026-07-08) ⇒ الحالة الموحّدة **«مُغلَق»**، ولا يظهر «تجاوز الموعد» (حارس H1 `OVERDUE_ACTIONABLE_STATUSES`). ✓
  - **W29 = بلا تسليم + بعد الموعد** ⇒ **OverdueNotSubmitted** (isLate=true)، تقود اللافتة كـ`isCurrentPriority`، ولا تظهر «تم اعتماد تقريرك بالكامل» زورًا. ✓
  - القالب الأسبوعي المُسنَد «💻 تقرير فريق الويب» (`839ae6cf`، Primary/Published/active)، تسليم W28 ضمن سلالته ⇒ IsAssigned=true.
- **توزيع الحالات الإنتاجية الطبيعية المتاحة (قراءة فقط):** Closed=21، Submitted=12، ApprovedByDirectManager=6، Returned=2، Draft=1.
- **بقيّة الحالات الطرفية** (OverdueDraft/OverdueReturned/SubmittedLate/PendingApproval/DueNow/NotDue/no-assignment) اختُبِرت حيًّا بالكامل على RC UAT (انظر تقرير RC)؛ لم تُنشأ أي بيانات اختبار على الإنتاج التزامًا بالنطاق.

## 7) PHASE P6 — فحص السلامة والانحدار (كلها خضراء)
1. الخدمة active + enabled بعد عدّة دقائق من الاستقرار.
2. health داخلي=200 · عام=200.
3. **بوّابات env دون تغيير (إثبات):** `Email__Enabled=false` · `Reminders__Enabled=true` · `Scheduler__Enabled=true`؛ **mtime لملف env = `2026-07-17 12:29:37`** (يسبق النشر ⇒ لم يُلمَس أثناء النشر).
4. hashes الأربعة منشورة تطابق `3eee204`.
5. bundle الواجهة الجديد `index-6J6SJ-kI.js` مُقدَّم.
6. **عدد الهجرات = 29 · الرأس = `20260716015239_KpiEvaluationPartialUniqueIndex`** (لم يتغيّر).
7. **علامات حفظ الميزات الثلاث حاضرة:** `20260713171040_AdminGovernanceReportKpiCorrection` + `20260715162851_AddBypassTeamLeaderApproval` (Fatma-Direct) + `20260716015239_KpiEvaluationPartialUniqueIndex`.
8. **email_outbox = 0** إجمالًا · **0 رسائل أُنشئت في نافذة النشر** (منذ 06:00 UTC) ⇒ لا بريد غير متوقّع، لا زيادة غير طبيعيّة في الطابور.
9. **RC غير متأثّر:** `khubara-reporting-rc.service` = active · health 5092 = 200.

## 8) عدم المساس (No-Impact)
لم يُمَسّ أيّ من: الهجرات (0 مطبَّقة، الرأس ثابت)، Seeder، بيانات إنتاجية (لا إنشاء/تعديل/حذف)، بوّابات Email/Reminders/Scheduler (env بلا تغيير — mtime سابق للنشر)، Moderation، بيئة RC. الكود الجديد **لا يغيّر** أي إعداد تشغيليّ ولا يُطلق إشعارات ولا يُحدث أخطاء تشغيلية. النشر من artifact `3eee204` حصرًا مع تطابق hash مثبت.

## 9) Rollback (إن لزم مستقبلًا)
استعادة `/opt/reporting/publish-backup-unifiedstatus-20260720-064341` + `dist-backup-unifiedstatus-20260720-064341` وإعادة تشغيل `reporting-api.service` (لا هجرة لعكسها). DB dump متاح للطوارئ فقط.

---

## 10) الحكم النهائي
**PRODUCTION DEPLOYED AND VALIDATED — TICKET CLOSED.**

كل مراحل P0–P6 مرّت؛ النشر من `3eee204` بتطابق hash؛ بلا هجرة؛ الميزات محفوظة؛ لا بريد غير متوقّع؛ البوّابات دون تغيير؛ التحقّق الأعمال الأساسي (أحمد نصار W28/W29) مُثبَت حيًّا على بيانات إنتاجية حقيقية. **التذكرة مُغلقة.**

**التالي (توصية فقط — بانتظار موافقة صريحة، لا بدء تلقائيّ):** `NEXT: MODERATION-CONTENT-PERFORMANCE-R1B OPERATIONAL CLOSURE`.
