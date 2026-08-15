# LEAVE-WORKFLOW-RECONCILIATION-PUBLISHER-R1 — تقرير القبول النهائيّ للتنفيذ المحكوم على الإنتاج (PRODUCTION CONTROLLED EXECUTE)

> **القرار النهائيّ:** `PRODUCTION CONTROLLED EXECUTE — FOLDED=2 / CURRENT_STEP=HR / FAILED=0 / AUDIT=1 PER REQUEST / LEDGER=0 / BALANCE_CHANGE=0 / HR APPROVAL NOT EXECUTED / IDEMPOTENCY PASS / CLOSED`
> **التاريخ:** 2026-08-05 (نافذة التنفيذ UTC ≈ 10:23:35 / الرياض ≈ 13:23:35).
> **النطاق:** طيّ خطوة المدير المحكوم لطلبَي جمود تاريخيَّين معتمَدَين حصرًا: أحمد نصار (بادئة `9d445a3e`) وسمر مجدي (بادئة `2407739b`)، من `TeamLeaderApproved/Manager` ⟶ `ManagerApproved/Hr`، بحدث تدقيق واحد لكلٍّ، **بلا اعتماد HR، بلا Ledger، بلا خصم رصيد، بلا مساس بأيّ طلب آخر**.

---

## 1) التمهيد الإنتاجيّ (Production Preflight)
- الوقت: UTC `2026-08-05 10:22:02` / الرياض `13:22:02` عند بدء المرحلة 0.
- البيئة: `Environment=Production`، الخدمة `reporting-api` = `active (running)`.
- `MainPID=505567`، `NRestarts=0` (لا إعادة تشغيل خلال العمليّة كلها).
- health داخليّ `http://127.0.0.1:5090/health` = 200، وعامّ `https://reports.emarketingacademy.net/health` = 200.
- SourceLink الحيّ في Api DLL = `2d282cebf0a22f65b78cd751de17d6c927128d0d` (عدد التطابق=1).
- الهجرات = 30، الرأس = `20260724224053_AddReportApproverAndKpiReviewerOverrides` (بلا تطبيق أيّ هجرة).

## 2) إثبات الأداة والـArtifact
- المسار المنشور: `/opt/reporting/tools/leave-workflow-reconciliation-publisher/Reporting.LeaveWorkflowReconciliationPublisher.dll` (owner `root:root`).
- بصمة SHA256 = `5a5519e979d6e6f978ae8ec67781a161c5061f90ea933860f8200fbc25b75b35` (مطابقة لبصمة النشر المعتمدة).
- الأداة framework-dependent، تُشغَّل عبر dotnet 8 مع حقن سلسلة الاتصال بلا طباعة/قراءة للسرّ من خلال:
  `systemd-run --pipe --wait --quiet --collect -p EnvironmentFile=/etc/reporting-api.env /usr/bin/dotnet …` (خدمة transient تُنظَّف تلقائيًّا بـ`--collect`).

## 3) إثبات الـManifest
- المسار: `/root/secure/leave-workflow-reconciliation-r1-20260805.json`، خارج مستودع Git تمامًا.
- الأذونات: `600 root:root`، الحجم 624 بايتًا.
- `schemaVersion=1`، `batchId=leave-deadlock-r1-20260805`، `maxItems=2`، عدد البنود = 2 بلا تكرار.
- مسح الأسرار على الملفّ = 0 (لا password/secret/token/connectionstring/smtp/@).
- البندان: `9d445a3e-3470-46c4-b884-7fa356eb05ce` (موظّف `1c7f0896…`) و`2407739b-0c53-4abd-ad88-2a0ee0bbbbe2` (موظّف `d352528f…`)، كلاهما expectedStatus=`TeamLeaderApproved`، expectedCurrentStep=`Manager`، expectedLedgerCount=0.

## 4) إعادة التحقّق قبل التنفيذ (المرحلة 0 — قراءة فقط)
- Eligible = **2** (لو خالف ⇒ توقّف؛ لم يخالف).
- لكلّ طلب: موجود/غير محذوف، `Status=TeamLeaderApproved`، `CurrentStep=Manager`، `TeamLeaderReviewerId` حاضر، `ManagerReviewerId` فارغ، `Employee.ManagerId == TL` (شرط P2)، لا مدير تشغيليّ بديل أعلى، لا حدث طيّ/تسوية سابق، Ledger=0، لا خصم رصيد، ليس HrApproved/Rejected/Cancelled.
- سلاسل المدير المُعاد تأكيدها: `9d445a3e` قائد فريقه `f6380cb1` بلا سلف؛ `2407739b` قائد فريقه `8be4ba0c` ⟶ GM `f4e25122…` ⟶ CEO `7e2cb6ac…` (لا دور Manager تشغيليّ) ⟶ لا بديل تشغيليّ.

## 5) لقطة ما قبل التنفيذ (Before Snapshot)
- لكلّ طلب: `TeamLeaderApproved/Manager`، `ManagerReviewerId`/`HrReviewerId` فارغان، عدد الأحداث=2 (submitted, team_leader_approved)، أحداث الطيّ=0، Ledger للطلب=0.
  - `9d445a3e` UpdatedAtUtc = `2026-07-21 15:22:49`.
  - `2407739b` UpdatedAtUtc = `2026-08-03 20:08:39`.
- النظام: `MainPID=505567`, `NRestarts=0`, migrations=30/head=`20260724224053`, `outbox=0`, Email Pending/Processing/Failed=0, env mtime=`1785095398`, health=200.
- ملاحظة: للموظّف `d352528f` حركة Debit سابقة (1.00 ApprovedLeave) تخصّ **طلبًا معتمَدًا آخر** لا علاقة له بـ`2407739b` (Ledger المرتبط بـ`2407739b` = 0).

## 6) أمر التنفيذ والبوّابات (المرحلة 2)
```
--execute
--manifest /root/secure/leave-workflow-reconciliation-r1-20260805.json
--expected-count 2
--batch-id leave-deadlock-r1-20260805
--confirm LEAVE-WORKFLOW-RECONCILIATION-PUBLISHER-R1
```
- تنفيذ تسلسليّ حصرًا، إعادة تحقّق قبل كلّ طلب، `SaveChanges` مستقلّ لكلّ طلب (لا معاملة دفعة واحدة)، بلا Retry، بلا Parallel.
- النتيجة: `ExitCode=0`، **Folded=2, AlreadyApplied=0, Failed=0, ManualReview=0**.
- سطر الملخّص: «طُوِيت الآن: 2 — مطبَّق سلفًا (idempotent): 0 — من أصل 2 بندًا. لا اعتماد نهائيّ (HR)، لا حركة رصيد، لا خصم رصيد، لا مساس بأيّ طلب آخر.»

## 7) نتيجة أحمد نصار (`9d445a3e`)
- `القرار=Eligible | السبب=folded | قبل=TeamLeaderApproved ⟶ بعد=ManagerApproved | كتابة=نعم | 343ms`.
- بعد: `Status=ManagerApproved`, `CurrentStep=Hr`, `ManagerReviewerId=f6380cb1` (=قائد الفريق، دلالة P2)، `HrReviewerId=NULL`.
- UpdatedAtUtc = `2026-08-05 10:23:35.464643+00`.

## 8) نتيجة سمر مجدي (`2407739b`)
- `القرار=Eligible | السبب=folded | قبل=TeamLeaderApproved ⟶ بعد=ManagerApproved | كتابة=نعم | 20ms`.
- بعد: `Status=ManagerApproved`, `CurrentStep=Hr`, `ManagerReviewerId=8be4ba0c` (=قائد الفريق، دلالة P2)، `HrReviewerId=NULL`.
- UpdatedAtUtc = `2026-08-05 10:23:35.659174+00`.

## 9) التحقّق من الانتقال
- كلاهما `TeamLeaderApproved/Manager` ⟶ `ManagerApproved/Hr` كما هو مأذون حصرًا.
- `ManagerReviewerId` مضبوط = قائد الفريق الفعليّ لكلٍّ (طيّ تشغيليّ لا قرار مدير يدويّ).
- المُعتمِد الفعليّ الحاليّ (المشتقّ من `CurrentStep=Hr`) = خطوة اعتماد HR (لا عمود `CurrentApproverId` مخزَّن في السكيمة؛ المُعتمِد يُشتقّ من الخطوة والحقول).

## 10) التحقّق من التدقيق (Audit)
- حدث واحد فقط لكلّ طلب في `leave_request_events`: `manager_step_auto_folded_no_operational_manager` (المجموع=2، لا تكرار).
- تفاصيل الحدث: `Step=Manager`, `FromStatus=TeamLeaderApproved`, `ToStatus=ManagerApproved`, `ActorUserId`=قائد الفريق.
- التعليق يحمل بيانات التسوية المعتمدة حرفيًّا: `batchId=leave-deadlock-r1-20260805؛ tool=leave-workflow-reconciliation-publisher-r1/1.0.0؛ reason=historical_deadlock_resumed_fold`.
- معرّفا الحدثين: `bed9f455…` (9d445a3e) و`add37a6d…` (2407739b).

## 11) إثبات عدم اعتماد HR
- `HrReviewerId=NULL` لكلا الطلبين، وعدد الطلبات بحالة `HrApproved` ضمن البندين = **0**.
- لا حدث اعتماد HR في سجلّ الأحداث (الأحداث لكلّ طلب = submitted / team_leader_approved / fold فقط).

## 12) إثبات عدم وجود Ledger
- Ledger المرتبط بأيّ من الطلبين (`RelatedRequestId`) = **0**.
- لا صفّ Ledger أُنشئ في نافذة التنفيذ (`CreatedAtUtc >= 10:22`) = 0.

## 13) إثبات عدم تغيّر الرصيد
- لا حركة Credit/Debit جديدة على أيّ موظّف من البندين مرتبطة بهذين الطلبين.
- حركة `d352528f` السابقة (Debit 1.00) تخصّ طلبًا آخر ولم تتأثّر.

## 14) الـIdempotency (المرحلة 4)
- إعادة نفس أمر Execute بنفس Manifest/BatchId/expected-count=2 ⇒ `ExitCode=0`.
- النتيجة: `AlreadyApplied=2` (السبب `fold_event_present`، كتابة=لا لكلٍّ)، `Folded=0`.
- تحقّق القاعدة بعد الإعادة: `UpdatedAtUtc` بلا تغيير (10:23:35 لكلٍّ)، أحداث الطيّ=2 (1 لكلٍّ)، مجموع الأحداث=6، Ledger=0، HrApproved=0 ⇒ **صفر كتابة جديدة**. لم تُتجاوَز بوّابة العدد لفرض تشغيل ثالث.

## 15) لقطة ما بعد التنفيذ (After Snapshot)
- الوقت: UTC `2026-08-05 10:28:36` / الرياض `13:28:36`.
- التغييرات المسموحة فقط تحقّقت: `Status TeamLeaderApproved→ManagerApproved`, `CurrentStep Manager→Hr`, `ManagerReviewerId`=قائد الفريق، حدث تدقيق واحد لكلٍّ.
- الثوابت: `MainPID=505567`, `NRestarts=0`, migrations=30/head=`20260724224053`, `outbox=0`, Email states = DryRun 139 / Sent 166 (لا Pending/Processing/Failed)، env mtime=`1785095398`, health داخليّ+عامّ=200.

## 16) عدم التأثير (Non-Impact)
- طلبات أخرى تغيّرت في نافذة التنفيذ = **0**.
- أحداث طيّ على أيّ طلب آخر في النافذة = **0**.
- صفوف Ledger في النافذة = **0**.
- لم تُمَسّ ريم/عائشة/بسنت، ولم يُعالَج محمد إبراهيم أو نور الدين رجب (خارج النطاق).

## 17) الأمان
- لا سرّ/كلمة مرور/توكن/سلسلة اتصال/بريد كامل طُبِع في أيّ خطوة (الحقن عبر `EnvironmentFile` بلا قراءة).
- الـManifest خارج Git، أذوناته `600 root:root`، بلا أسرار.
- لا وحدة systemd transient متبقّية (نظّفها `--collect`)، لا عمليّة تسوية قائمة، لا cron/port/service unit للتسوية.

## 18) سلوك الفشل الجزئيّ (Partial Failure)
- لم يقع فشل جزئيّ: كلا الطلبين نجحا تسلسليًّا (الأوّل ثمّ الثاني) بـ`ExitCode=0`.
- لو فشل الأوّل: توقّف قبل بدء الثاني. لو نجح الأوّل وفشل الثاني: كان سيُصدَر `PARTIAL FAILURE — STOPPED SAFELY` بلا عكس تلقائيّ للأوّل. لم يتحقّق أيّ من ذلك.

## 19) جاهزية التراجع (Rollback Readiness)
- الطيّ حركة أمامية محكومة (Manager فقط)، بلا Ledger/خصم ⇒ التراجع (إن لزم بقرار مستقلّ) = إعادة `Status=TeamLeaderApproved`/`CurrentStep=Manager`/تفريغ `ManagerReviewerId` وحذف حدث الطيّ للطلبين حصرًا — **لم يُنفَّذ ولا يُنفَّذ إلا بتصريح جديد**.
- النسخة الاحتياطيّة للقاعدة قبل النشر الإنتاجيّ للأداة قائمة (سلسلة LEAVE-WORKFLOW-DEADLOCK-HOTFIX). الـManifest مُبقًى 600 لإثبات الـIdempotency.

## 20) القرار النهائيّ
- الطلبان الآن عند خطوة HR جاهزان للاعتماد النهائيّ اليدويّ من HR عبر الواجهة (خطوة لاحقة خارج نطاق هذه المهمّة).
- **توقّف مُلزَم:** لا بدء اعتماد HR، لا تسوية Ledger، لا خصم رصيد، لا معالجة محمد إبراهيم/نور الدين، لا أيّ تذكرة أخرى دون تصريح مستقلّ.

```
LEAVE-WORKFLOW-RECONCILIATION-PUBLISHER-R1
PRODUCTION CONTROLLED EXECUTE
FOLDED=2
CURRENT_STEP=HR
FAILED=0
AUDIT=1 PER REQUEST
LEDGER=0
BALANCE_CHANGE=0
HR APPROVAL NOT EXECUTED
IDEMPOTENCY PASS
CLOSED
```
