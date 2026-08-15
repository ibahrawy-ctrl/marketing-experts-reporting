# LEAVE-WORKFLOW-RECONCILIATION-PUBLISHER-R1 — تقرير نشر الأداة على الإنتاج + المعاينة (Plan) فقط

> **الحالة النهائية:** `LEAVE-WORKFLOW-RECONCILIATION-PUBLISHER-R1 / PRODUCTION TOOL DEPLOYED — PLAN COMPLETE / ELIGIBLE=2 / ALREADY_APPLIED=0 / LEDGER=0 / BALANCE_CHANGE=0 / NO WRITES EXECUTED`
>
> **نوع العملية:** نشر أداة مستقلّة (standalone) إلى الإنتاج + تشغيل معاينة (Plan) قراءة-فقط على الطلبين الحقيقيّين العالقين. **لم يُنفَّذ أيّ `--execute`، ولا كتابة، ولا إعادة تشغيل، ولا هجرة، ولا نشر واجهة.**

---

## 1) الملخّص التنفيذي
تمّ نشر أداة `Reporting.LeaveWorkflowReconciliationPublisher` المستقلّة إلى مسار أدوات معزول على خادم الإنتاج (`/opt/reporting/tools/leave-workflow-reconciliation-publisher/`) دون أيّ مساس بخدمة الـAPI أو الواجهة أو المخطّط أو الإعدادات. ثمّ شُغِّلت **معاينة (Plan) قراءة-فقط** على الطلبين الحقيقيّين العالقين عند خطوة المدير بلا بديل تشغيليّ:
- الطلب الذي يبدأ بـ `9d445a3e…`
- الطلب الذي يبدأ بـ `2407739b…`

نتيجة المعاينة: **كلاهما `Eligible` بالسبب `structural_deadlock_foldable`**، **صفر كتابة**. لقطة الحالة قبل/بعد Plan متطابقة تمامًا.

## 2) النطاق والمحظورات المُلتزَم بها
- **النطاق:** نشر الأداة فقط + Plan قراءة-فقط + إثبات صفر كتابة + Manifest إنتاج آمن.
- **مُنِع بالكامل ولم يُنفَّذ:** `--execute`، أيّ Transition/HrApproval، إنشاء تدقيق، إنشاء Ledger، خصم رصيد، تعديل أيّ طلب، إعادة تشغيل الـAPI، هجرة، نشر واجهة، تغيير إعداد، أيّ `UPDATE/INSERT/DELETE`، معالجة محمد إبراهيم أو نور الدين، توسيع النطاق لأيّ طلب آخر، بدء أيّ تذكرة أخرى.
- **الأسرار:** لم تُقرأ ولم تُطبَع أيّ كلمة مرور/JWT/Token/ConnectionString/Gmail App Password، ولم يُنفَّذ `cat /etc/reporting-api.env`.

## 3) المرحلة 0 — إعادة تحقّق التجميد (محلّي)
- `HEAD = 976575672939396e40d86c926a676bbc6418e114`
- `Parent = 2d282cebf0a22f65b78cd751de17d6c927128d0d`
- `Tree = 6fc12b03823c8bffd9a089b15fa972e634db07fe`
- `git status --porcelain` = فارغ (لا untracked/WIP/merge/rebase).
- **بصمة الأثر (Artifact SHA256) = `5a5519e979d6e6f978ae8ec67781a161c5061f90ea933860f8200fbc25b75b35`** (لم تتغيّر؛ رسالة الخلفية لم تُعدِّل المرشّح).

## 4) المرحلة 1 — الفحص القبْليّ للإنتاج (قراءة-فقط)
- Environment = **Production**؛ خدمة `reporting-api` = **active/running**.
- `MainPID = 505567`، `NRestarts = 0`.
- Health: داخليّ `http://127.0.0.1:5090/health` = **200**، عامّ `https://reports.emarketingacademy.net/health` = **200**.
- SourceLink الحيّ = `2d282ceb…` (مطابق للأساس المتوقَّع).
- الهجرات = **30**، الرأس = `20260724224053_AddReportApproverAndKpiReviewerOverrides`.
- `email_outbox`: Pending/Processing/Failed = **0/0/0** (لا صفوف إطلاقًا).
- الطلبان قبل أيّ عمل: كلاهما `TeamLeaderApproved / Manager` (غير مُعدَّلَين).

## 5) المرحلة 2 — التحقّق من الأثر (Artifact)
- بصمة DLL الأداة (محلّيًّا: `publish/` و`bin/Release`) = `5a5519e9…` (مطابقة).
- لا Manifest إنتاج داخل Git؛ لا ملفّات نتائج/أسرار/هجرة/واجهة/endpoint/وحدة خدمة/cron/BackgroundService/منفذ ضمن حزمة الأداة.
- لا تعديل على حزمة نشر الـAPI.

## 6) المرحلة 3 — نشر الأداة فقط
- `DEPLOY_TS = 20260805-100655`.
- الوجهة: `/opt/reporting/tools/leave-workflow-reconciliation-publisher/` (حزمة `publish` مكتفية ذاتيًّا، اعتماد على runtime dotnet 8).
- **بصمة DLL على الخادم = `5a5519e979d6e6f978ae8ec67781a161c5061f90ea933860f8200fbc25b75b35`** (مطابقة للأثر).
- الملكيّة `root:root` + `chmod go-w`.
- **صفر أثر على الـAPI:** حزمة `/opt/reporting/publish` لم تُمَسّ (48 ملفًّا، Infra=`83c30928…`، Api=`8669bca2…`)؛ لا خدمة/cron/عمليّة/منفذ جديد؛ `MainPID=505567`/`NRestarts=0` بلا تغيير؛ health=200.

## 7) المرحلة 4 — Manifest الإنتاج
- المسار (**خارج Git**): `/root/secure/leave-workflow-reconciliation-r1-20260805.json`، الملكيّة `root:root`، الصلاحيات `600`.
- `schemaVersion=1`، `batchId=leave-deadlock-r1-20260805`، `maxItems=2`، **عدد العناصر = 2 بالضبط** (بلا تكرار RequestId).
- خالٍ من أيّ سرّ/بريد (grep للمفاتيح الحسّاسة = 0).

| # | requestId | expectedEmployeeUserId | expectedStatus | expectedCurrentStep | expectedLedgerCount |
|---|-----------|------------------------|----------------|---------------------|---------------------|
| 1 | `9d445a3e-3470-46c4-b884-7fa356eb05ce` | `1c7f0896…` | TeamLeaderApproved | Manager | 0 |
| 2 | `2407739b-0c53-4abd-ad88-2a0ee0bbbbe2` | `d352528f…` | TeamLeaderApproved | Manager | 0 |

## 8) المرحلة 5 — إعادة التحقّق قراءة-فقط: الطلب `9d445a3e…`
- موجود، غير محذوف، `Type=Leave`.
- `Status = TeamLeaderApproved`، `CurrentStep = Manager`.
- `TeamLeaderReviewerId = f6380cb1…` (حاضر)، `ManagerReviewerId` فارغ، `HrReviewerId` فارغ، `CancelledAtUtc` فارغ.
- `Requester.ManagerId = f6380cb1…` = `TeamLeaderReviewerId` (شرط P2 محقَّق).
- Ledger المرتبط بالطلب = **0**؛ لا حدث طيّ سابق `manager_step_auto_folded_no_operational_manager`؛ الأحداث = `submitted` + `team_leader_approved` فقط.
- لا تدقيق تسوية سابق (audit_logs: `leave_request.submitted` + `leave_request.team_leader_approved` فقط).
- ليس HrApproved/Rejected/Cancelled؛ `UpdatedAtUtc = 2026-07-21 15:22:49` (غير متغيّر منذ التقرير السابق).

## 9) المرحلة 5 — إعادة التحقّق قراءة-فقط: الطلب `2407739b…`
- موجود، غير محذوف، `Type=Leave`.
- `Status = TeamLeaderApproved`، `CurrentStep = Manager`.
- `TeamLeaderReviewerId = 8be4ba0c…` (حاضر)، `ManagerReviewerId` فارغ، `HrReviewerId` فارغ، `CancelledAtUtc` فارغ.
- `Requester.ManagerId = 8be4ba0c…` = `TeamLeaderReviewerId` (شرط P2 محقَّق).
- Ledger المرتبط بالطلب = **0**؛ لا حدث طيّ سابق؛ الأحداث = `submitted` + `team_leader_approved` فقط.
- لا تدقيق تسوية سابق (نفس النمط أعلاه).
- ليس HrApproved/Rejected/Cancelled؛ `UpdatedAtUtc = 2026-08-03 20:08:39` (غير متغيّر منذ التقرير السابق).

## 10) سلاسل المديرين وغياب البديل التشغيليّ (P2)
تسلُّق سلسلة `ManagerId` صعودًا من قائد الفريق (المعتمِد) بحثًا عن **نشط + دور `Manager` + ≠ المعتمِد** (Admin/CEO/GM/CeoSupport ليست بديلًا تشغيليًّا):
- **`9d445a3e…`:** TL `f6380cb1…` (TeamLeader، نشط) بلا أيّ سلف في السلسلة ⇒ **لا مدير تشغيليّ أعلى**.
- **`2407739b…`:** TL `8be4ba0c…` (TeamLeader، نشط) → `f4e25122…` (GeneralManager، نشط) → `7e2cb6ac…` (CEO، نشط). **لا دور `Manager` في السلسلة إطلاقًا** ⇒ لا بديل تشغيليّ.

⇒ الطلبان **جمود بنيويّ قابل للطيّ** (`structural_deadlock_foldable`).

## 11) المرحلة 6 — تشغيل Plan (مع حماية السرّ)
- الأمر: `dotnet …/Reporting.LeaveWorkflowReconciliationPublisher.dll --plan --manifest /root/secure/…json --batch-id leave-deadlock-r1-20260805` — **بلا `--execute` إطلاقًا**.
- سلسلة الاتّصال حُقِنت عبر **خدمة عابرة** `systemd-run --pipe --wait --collect -p EnvironmentFile=/etc/reporting-api.env` — فحقن systemd المتغيّرات دون أن تُقرأ أو تُطبَع (تكريمًا لحظر `cat /etc/reporting-api.env`؛ التحقّق من وجود المفتاح كان عدًّا فقط `grep -c` = 1).
- الخروج = **0**. الوحدة العابرة نُظِّفت (`--collect`؛ لا وحدة dotnet متبقّية).
- التحذيرات EF (Model.Validation 10622) حميدة (تحقّق نموذج، ليست أخطاء).

## 12) جدول نتائج Plan (معرّفات مُقنَّعة)
```
RequestId=9d445a3e… | القرار=Eligible | السبب=structural_deadlock_foldable | قبل=TeamLeaderApproved ⟶ بعد=TeamLeaderApproved | كتابة=لا
RequestId=2407739b… | القرار=Eligible | السبب=structural_deadlock_foldable | قبل=TeamLeaderApproved ⟶ بعد=TeamLeaderApproved | كتابة=لا
```
لا أسماء كاملة ولا بريد في السجلّ؛ المعرّفات مُقنَّعة بالبادئة.

## 13) تصنيف الدلاء (Buckets) والعدّ
| الدلو | القيمة |
|-------|--------|
| Eligible | **2** |
| AlreadyApplied | 0 |
| Natural | 0 |
| InvalidState | 0 |
| OperationalManagerExists | 0 |
| LedgerExists | 0 |
| ManualReview | 0 |
| Created | 0 |
| Updated | 0 |

## 14) المرحلة 7 — إثبات صفر كتابة (BEFORE = AFTER)
| العنصر | BEFORE | AFTER |
|--------|--------|-------|
| Status/CurrentStep (كلا الطلبين) | TeamLeaderApproved / Manager | مطابق |
| ManagerReviewerId / HrReviewerId | فارغ / فارغ | مطابق |
| UpdatedAtUtc `9d445a3e` / `2407739b` | 2026-07-21 15:22:49 / 2026-08-03 20:08:39 | مطابق |
| عدد `leave_request_events` (الطلبان) | 4 | 4 |
| عدد تدقيق `audit_logs` (الطلبان) | 4 | 4 |
| Ledger المرتبط بالطلبين | 0 | 0 |
| صفوف رصيد المُنشئَين (`1c7f0896`/`d352528f`) | 2 / 5 | 2 / 5 |
| الهجرات / الرأس | 30 / `20260724224053` | 30 / `20260724224053` |
| `email_outbox` (Pending/Processing/Failed) | 0/0/0 | 0/0/0 |

⇒ **SaveChanges=0، Audit=0، Ledger=0، Balance=0، HR=0، restart=0، Scheduler لم يُشغَّل، SMTP=0.**

## 15) حالة الخدمة والبيئة والصحّة بعد Plan
- `MainPID = 505567`، `NRestarts = 0`، active/running (لا إعادة تشغيل).
- `mtime` لـ `/etc/reporting-api.env` = `1785095398` (غير متغيّر).
- Health داخليّ = 200، عامّ = 200.

## 16) المرحلة 8 — الأمان والتنظيف
- **الـManifest مُحتفَظ به** عند `/root/secure/…json` بصلاحيات `600` (للتنفيذ اللاحق تحت تصريح مستقلّ) — **لم يُحذَف**.
- حُذفت فقط الملفّات المؤقّتة (`/tmp/recon_*.sql`, `/tmp/recon_before.txt`) ⇒ `/tmp/recon_*` = clean.
- لا سرّ/بريد/Token/ConnectionString/كلمة مرور في هذا التقرير ولا في الـManifest.

## 17) إثبات عدم مساس الإنتاج
- **API:** حزمة `/opt/reporting/publish` غير مُمَسّة (48 ملفًّا، بصمات ثابتة)؛ لا إعادة تشغيل.
- **Frontend:** لم يُنشَر شيء.
- **Schema/Migration:** 30 هجرة، الرأس ثابت؛ لا هجرة مُطبَّقة.
- **Email/Scheduler:** لم يُغيَّر إعداد؛ outbox=0؛ لا إرسال SMTP.
- **قاعدة البيانات:** لا `UPDATE/INSERT/DELETE`؛ كلّ الوصول كان `SELECT` قراءة-فقط.

## 18) المخاطر والضوابط وأكواد الخروج
- الأداة مُبوَّبة: التنفيذ الحقيقيّ يتطلّب `--execute` + `--confirm` + تطابق `--expected-count`/`--batch-id`؛ Plan لا يمرّ أيًّا منها.
- أكواد الخروج: 0 نجاح · 2 بوّابة · 3 Manifest · 4 عدم تطابق العدد/الدفعة · 5 بند غير مؤهَّل (صفر كتابة) · 6 فشل جزئيّ · 7/8 تشغيليّ · 9 أمان. **الناتج هنا = 0.**
- بوّابة الأمان `exit-5` تضمن أنّ أيّ بند غير مؤهَّل يوقف الكتابة كلّيًّا (مُثبَتة في قبول RC).

## 19) الخطوة التالية (خارج نطاق هذه المهمّة)
التنفيذ الفعليّ (`--execute`) لطيّ خطوة المدير للطلبين نحو `ManagerApproved/Hr` **يتطلّب تصريحًا صريحًا مستقلًّا**. الـManifest جاهز ومحفوظ لهذا الغرض. **لم يُبدأ أيّ تنفيذ، ولا أيّ تذكرة أخرى.**

## 20) الحالة النهائية
```
LEAVE-WORKFLOW-RECONCILIATION-PUBLISHER-R1 / PRODUCTION TOOL DEPLOYED — PLAN COMPLETE / ELIGIBLE=2 / ALREADY_APPLIED=0 / LEDGER=0 / BALANCE_CHANGE=0 / NO WRITES EXECUTED
```
