# LEAVE-WORKFLOW-DEADLOCK-HOTFIX — تسوية محكومة للطلبات القائمة (تقرير التنفيذ)

> **الحالة النهائيّة:** `EXECUTION HALTED AT STOP CONDITION — NO SAFE IN-SCOPE MECHANISM — ZERO WRITES EXECUTED`
>
> **صفر كتابة نُفِّذت على الإنتاج.** توقّفتُ عند بوّابة توقّف مشروعة (STOP CONDITION من التذكرة نفسها) **قبل** أيّ اعتماد أو طيّ أو Ledger أو خصم رصيد. Phase 0 (إعادة التحقّق قراءة-فقط) اكتمل أخضر؛ لكنّ Phase 1/2 لا يمكن تنفيذهما بآليّة آمنة وضمن النطاق المصرَّح به.
>
> **التاريخ:** 2026-08-05 — **البيئة:** Production (`reporting_prod`).

---

## 1. Preflight (Phase 0 — إعادة التحقّق قراءة-فقط، أخضر بالكامل)

| البند | القيمة المُثبَتة الآن | مطابقة الخطة المعتمدة؟ |
|---|---|---|
| MainPID | `505567` | نعم |
| NRestarts | `0` | نعم |
| Health داخليّ | `200` | نعم |
| Health عامّ (HTTPS) | `200` | نعم |
| SourceLink الحيّ | `1.0.0+2d282cebf0a22f65b78cd751de17d6c927128d0d` | نعم (المرشّح P2) |
| Migration Head | `20260724224053_AddReportApproverAndKpiReviewerOverrides` (30 هجرة) | نعم |
| Environment | `Production` | نعم |

**إعادة التحقّق من الطلبات التسعة (الحالة + Ledger):** كلّها **بلا تغيير** عن الخطة المعتمدة، وكلّها **Ledger = 0**:

| RequestId | الموظّف | الحالة | الخطوة | Ledger |
|---|---|---|---|---|
| `9d445a3e…` | أحمد نصار | TeamLeaderApproved | Manager | 0 |
| `2407739b…` | سمر مجدي | TeamLeaderApproved | Manager | 0 |
| `60e7ddf3…` | ريم جاب الله | TeamLeaderApproved | Manager | 0 |
| `0cbf9aa3…` | عائشة كمال | TeamLeaderApproved | Manager | 0 |
| `e8da41e3…` | بسنت محمد | TeamLeaderApproved | Manager | 0 |
| `f40ee93c…` | محمد إبراهيم | ManagerApproved | Hr | 0 |
| `5ed1efa3…` | نور الدين رجب | ManagerApproved | Hr | 0 |

**لا mismatch.** Preflight يسمح بالمتابعة تقنيًّا — لكنّ الفحص العميق للكود كشف عائقين جوهريّين يوجبان التوقّف (أدناه).

---

## 2. Executed Requests

**لا يوجد.** لم يُنفَّذ أيّ اعتماد أو طيّ أو انتقال على أيّ طلب. صفر كتابة.

---

## 3. Controlled Transition (Phase 1) — **متوقّف: لا آليّة آمنة منشورة**

المطلوب: طيّ خطوة Manager العالقة لطلبَي أحمد نصار (`9d445a3e…`) وسمر مجدي (`2407739b…`) بدلالة P2 (`ManagerApproved/Hr` + حدث `manager_step_auto_folded_no_operational_manager`).

**العائق المُثبَت من الكود (`LeaveRequestService.cs`):**

1. **طيّ P2 لا يعمل إلّا لحظة اعتماد قائد الفريق** داخل `DecideAsync`: الطيّ يقع فقط عند اعتماد TL حين `Requester.ManagerId == approver` وغياب بديل مدير تشغيليّ. الطلبان **تجاوزا خطوة TL بالفعل** (`TeamLeaderApproved`) ⇒ **لا مسار تشغيليّ يعيد إطلاق الطيّ عليهما**.

2. **أداة المعالجة الوحيدة `RemediateTeamLeaderStuckRequestsAsync` لا تنطبق:** تعالج حصرًا الطلبات العالقة عند `Status=Submitted / CurrentStep=TeamLeader` (تخطّي TL الذاتيّ)، **لا** الطلبات العالقة عند `TeamLeaderApproved / CurrentStep=Manager` (نمط الجمود عندنا). شرطها `r.CurrentStep == LeaveRequestStep.TeamLeader && r.TeamLeaderReviewerId == null` لا يطابق طلبَينا.

3. **الاعتماد الطبيعيّ لخطوة Manager (`ManagerApproveAsync`) محكوم بالنطاق** (`scope.Contains(requester)`): لا يوجد **مدير تشغيليّ** ضمن سلسلة الموظّف (`op_mgr_above = 0` مقيسة فعليًّا) — **وهذا هو الجمود نفسه**. أيّ اعتماد من Admin/GM لن يُنتج دلالة الطيّ ولا حدث `manager_step_auto_folded_no_operational_manager` المطلوب.

**الخيارات المتبقّية لتنفيذ الطيّ كلّها خارج النطاق المصرَّح به:**
- (أ) **SQL يدويّ** (`UPDATE leave_requests` + `INSERT leave_request_events`) — **محظور صراحةً** في التذكرة والخطة («No SQL Manual Update, direct INSERT, manual Ledger edit»)، ويتجاوز ضمانات التطبيق.
- (ب) **بناء ونشر أداة/endpoint محكومة جديدة** تُنفّذ الطيّ بدلالة P2 — يتطلّب **بناء + نشر + إعادة تشغيل الخدمة**، وهو **توسيع للنطاق** ممنوع في هذه التذكرة («No widening of scope»، ومنع «restart service/Migration/Frontend/Config»).

⇒ **لا يمكن تنفيذ Controlled Transition بأمان وضمن النطاق. توقّف إلزاميّ.**

---

## 4. HR Approval (Phase 2) — **متوقّف: لا مسار HR طبيعيّ متاح**

المطلوب: اعتماد HR طبيعيّ لطلبَي محمد إبراهيم (`f40ee93c…`) ونور الدين رجب (`5ed1efa3…`) — «Normal HR approval only. No bypass. No special handling. Normal production path.»

**العائق المُثبَت:**

1. **`HrApproveAsync` يتطلّب فاعلًا ضمن `Roles.LeaveFinalApprovers`** (HR/Admin/CEO/GM) عبر `AuthorizeDecisionAsync` (خطوة Hr، سطر 665-668) + حارس عدم تصرّف نفس الشخص في خطوتين (سطر 543).

2. **لا أملك بيانات اعتماد مستخدم HR الإنتاجيّ.** مستخدم HR الحقيقيّ الوحيد على الإنتاج `Mmagdy2828@gmail.com` وكلمته غير متاحة لي — لا يمكنني تنفيذ «المسار الطبيعيّ» لاعتماد HR.

3. **استخدام break-glass admin للاعتماد = «معالجة خاصّة» يمنعها Phase 2 صراحةً**، وهو يُنفّذ **خصمًا فعليًّا لرصيد إجازة موظّفين حقيقيّين** (`ApplyApprovalDeductionAsync`) — إجراء حقيقيّ صعب التراجع لا يجوز اتخاذه خارج المسار المصرَّح.

⇒ **لا يمكن تنفيذ HR Approval بالمسار الطبيعيّ المطلوب. توقّف إلزاميّ.**

---

## 5. Ledger Validation

**لا تغيير.** كلّ الطلبات المستهدفة Ledger = 0 قبل وبعد (لم تُنفَّذ أيّ عمليّة). لا Ledger مُنشأ، لا Ledger مكرّر.

## 6. Balance Validation

**لا تغيير.** لا خصم رصيد وقع على أيّ موظّف. الأرصدة كما كانت تمامًا.

## 7. Audit Validation

**لا أحداث جديدة.** لم يُضَف أيّ `leave_request_event` ولا أيّ سجلّ تدقيق. لا أحداث مكرّرة (لأنّه لا أحداث أصلًا).

## 8. Production Integrity

الإنتاج **لم يُمَسّ إطلاقًا خارج القراءة**: MainPID 505567 / NRestarts 0 / health 200/200 / SourceLink بلا تغيير / Migration head ثابت (30) / Email مستقرّ / Scheduler مستقرّ / Frontend بلا تغيير / بقيّة طلبات الإجازة/KPI/التقارير/الصلاحيات بلا مساس. **صفر تغيير غير مقصود** لأنّه **صفر تغيير**.

## 9. Idempotency Proof

غير منطبق فعليًّا (لم تُنفَّذ عمليّة). ملاحظة تصميميّة: عند توفّر مسار آمن مستقبلًا، `ApplyApprovalDeductionAsync` idempotent عبر `(RelatedRequestId, Source)` ⇒ الخصم لا يتكرّر؛ والطيّ يجب أن يُحرَس بشرط الحالة الحاليّة (`TeamLeaderApproved/Manager`) لضمان عدم التكرار.

## 10. Final Decision

**تنفيذ متوقّف عند شرط توقّف مشروع، بلا أيّ كتابة.** السبب ليس عيبًا في البيانات (Preflight أخضر تمامًا)، بل **غياب آليّة تنفيذ آمنة ضمن النطاق المصرَّح**:

- **Phase 1 (Controlled Transition):** لا مسار تشغيليّ منشور يطوي خطوة Manager لطلب عالق؛ والبدائل (SQL يدويّ / نشر أداة جديدة + إعادة تشغيل) **محظورة صراحةً أو خارج النطاق**.
- **Phase 2 (HR Approval):** لا أملك بيانات اعتماد HR الإنتاجيّ، واستخدام admin = «معالجة خاصّة» ممنوعة تخصم رصيدًا حقيقيًّا لا يمكن التراجع عنه بسهولة.

**قرار مسؤول:** الالتزام الحرفيّ بالتذكرة يوجب `STOP` عند «Unexpected workflow state / Any exception» ويمنع «SQL Manual Update / direct INSERT / bypass / special handling / HR skip». لذا أوقفتُ العمليّة **قبل** أيّ كتابة.

### ما يلزم لاستئناف التنفيذ (قرار المستخدم)
1. **لـ Controlled Transition:** تصريح صريح ببناء ونشر **أداة/endpoint محكومة idempotent** تُنفّذ الطيّ بدلالة P2 (مع Backup + ملفّ تراجع + قبول إعادة تشغيل الخدمة)، **أو** تصريح بمعاملة SQL واحدة مُراجَعة محدّدة الطلبين مع Backup وRollback مُعدَّين مسبقًا.
2. **لـ HR Approval:** تنفيذ اعتماد HR عبر **مستخدم HR إنتاجيّ حقيقيّ** (بياناته لدى الإدارة)، **أو** تصريح صريح باستخدام معتمِد نهائيّ محدّد (Admin/CEO/GM) مع الإقرار بأنّه سيخصم رصيدًا فعليًّا.

**لن أُنفّذ أيّ كتابة قبل تصريح مستقلّ يحلّ العائقين أعلاه.**

---

**الحالة النهائيّة:** `EXECUTION HALTED AT STOP CONDITION — NO SAFE IN-SCOPE MECHANISM — ZERO WRITES EXECUTED`
