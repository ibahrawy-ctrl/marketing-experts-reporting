# تقرير تنفيذ المرحلة الثانية — Employee 360 & HR Operations

- **التاريخ:** 25 أغسطس 2026
- **المرشّح الأساس (Phase 1):** `545689bbf2bab3755e524cf6d89a23a92949b692`
- **الفرع:** `feature/p2-employee360-hr-ops-20260825` (worktree معزول `.claude/worktrees/p2-emp360-20260825`)
- **النطاق:** تنفيذ محلّيّ فقط. لا نشر TEST/RC/إنتاج، ولا Push/Merge/Tag، ولا كتابة على قاعدة مشتركة أو حيّة.
- **قاعدة بيانات المرحلة الثانية المعزولة:** `reporting_p2_20260825` (محلّيّة، أُنشئت لهذه المرحلة وحدها).

> **حالة التقرير: نهائيّ.** كلّ معرّفات الطابور الأحد عشر مغلقة بالتزامات محلّيّة. لا نشر ولا Push ولا Merge ولا Tag.

---

## 1) بوابة البيئة (§2) — مُثبَتة

| البند | القيمة |
|---|---|
| جذر المستودع | `/Users/ibrahimelbahrawi/Documents/Mrketing Experts syestem` |
| المرشّح | `545689b` رأس `feature/p1-kpi-truth-20260824`، الشجرة نظيفة |
| الفرع الجديد | `feature/p2-employee360-hr-ops-20260825` مقطوع من `545689b` |
| ملفّات المستخدم المحميّة | `M CLAUDE.md` و`?? Ops/R21/RC-CANDIDATE-BUILD-AND-REHEARSAL-REPORT-20260823.md` — **لم تُمسّ** (لا stash ولا reset ولا clean) |
| فرع الأدلّة | `evidence/p1-kpi-test-shadow-20260825` (`5b004db`) — لم يُدمَج ولم يُمسّ |
| `/tmp/p1-shadow-20260825/` | لم يُستعمل ولم يُحذف |

---

## 2) حالة طابور التنفيذ

| المعرّف | العنوان | الحالة |
|---|---|---|
| P2-SEC-001 | طبقة الرؤية على مستوى الحقل/القسم | **COMPLETED** |
| P2-EMP-002 | واجهة Employee 360 الخلفيّة | **COMPLETED** |
| P2-EMP-003 | واجهة Employee 360 + وضع الذات | **COMPLETED** |
| P2-ATT-004 | نواة Workflow للحضور | **COMPLETED** |
| P2-ATT-005 | دومين الحضور + هجرة إضافيّة | **COMPLETED** |
| P2-ATT-006 | آلة الحالات وواجهات API للحضور | **COMPLETED** |
| P2-ATT-007 | واجهات الحضور | **COMPLETED** |
| P2-HR-008 | محرّك الالتزامات المتوقَّعة | **COMPLETED** |
| P2-HR-009 | لوحة HR Operations وطوابير الإجراءات | **COMPLETED** |
| P2-HR-010 | قائمة خدمة الموظّف والالتزام | **COMPLETED** |
| P2-SEC-011 | بوابة الإغلاق الأمنيّ المتقاطع | **COMPLETED** |

---

## 3) P2-SEC-001 — طبقة الرؤية على مستوى الحقل (COMPLETED)

**الالتزام:** `6d93431`

### المكوّنات
| الملفّ | الدور |
|---|---|
| `Reporting.Application/Security/FieldSensitivity.cs` | سبع درجات حسّاسيّة + `SubjectRelation` + `Employee360Section` |
| `Reporting.Application/Security/AppPermissions.cs` | مفاتيح الأذونات الدقيقة، نوع الـclaim `perm` |
| `Reporting.Application/Security/FieldVisibilityRules.cs` | المصفوفة **النقيّة** (بلا قاعدة بيانات) — كلّ القرار هنا |
| `Reporting.Application/Security/IFieldVisibilityPolicy.cs` | العقد |
| `Reporting.Infrastructure/Services/FieldVisibilityPolicy.cs` | حلّ العلاقة + التدقيق |
| `Reporting.Application/Security/NoteSensitivity.cs` | تفسير التصنيف التاريخيّ بلا Backfill |
| `Reporting.Application/Security/Phase2FeatureOptions.cs` | أعلام المرحلة 2، كلّها `false` افتراضيًّا |

### المبادئ المُثبَتة باختبارات
- خارج النطاق ⟵ لا يرى أيّ درجة ولا أيّ قسم، مهما بلغت أدواره وأذوناته.
- الدرجات الحسّاسة (`HrOnly`, `ManagementConfidential`, `FinancialSensitive`, `MedicalSensitive`) **لا تُمنَح بدور إطلاقًا** بل بإذن صريح؛ فـ`Admin` لا يرى شيئًا حسّاسًا تلقائيًّا ولا يرى أيّ قسم عدا الهويّة.
- الموظّف على نفسه يرى `PublicOperational` و`SharedWithEmployee` ولا يرى `Internal` فما فوق.
- تعدّد الأدوار = **اتّحاد المُمنوح صراحةً** لا فتحًا شاملًا.
- الملاحظة التاريخيّة (`Sensitivity == null`) تُقرأ `Internal` **داخل التطبيق فقط**؛ القيمة المخزَّنة غير المعروفة تُعامَل بالأشدّ (`ManagementConfidential`) لا بالأضعف.
- قراءة أيّ حقل حسّاس تُكتَب في `AuditLog` بالفعل `sensitive_field.read` **بلا قيمة الحقل**.

### الهجرة
`20260824230015_AddManagementNoteSensitivity` — إضافيّة بحتة:
```
AddColumn<int>("Sensitivity", "management_notes", type: "integer", nullable: true)
```
لا Backfill، ولا حذف/إعادة تسمية/تغيير نوع لأيّ عمود قائم. طُبِّقت على `reporting_p2_20260825` المعزولة (42 هجرة) وعلى قاعدة التطوير المحلّيّة `reporting_dev`؛ العمود `integer / nullable` وكلّ السجلّات السابقة `NULL` (لا كتابة على بيانات تاريخيّة).

### قرار مُوثَّق: نطاق دور HR
`RoleAccess.ScopeTypeFor` القائم يُسقِط `HR` إلى نطاق `own`، بينما مصفوفة §7 تجعل الموارد البشريّة وظيفة مؤسّسيّة. طبقًا لأسبقيّة §4 (القواعد الصريحة في الرسالة > الكود القائم) وُسِّعت **العلاقة** داخل `FieldVisibilityPolicy` وحدها، و**لم يُمسّ `ScopeResolver`** كي لا يتغيّر سلوك أيّ شاشة قائمة. التوسيع لا يفتح شيئًا حسّاسًا لأنّ الدرجات الحسّاسة محكومة بالإذن الصريح لا بالدور.

---

## 4) P2-EMP-002 — Employee 360 (COMPLETED)

**الالتزام:** `72bb2e1`

### السطح
| المسار | الوصف |
|---|---|
| `GET /api/employees/{id}/profile-360` | العرض الكامل لموظّف، خارج النطاق = 404 |
| `GET /api/employees/me/profile-360` | اسم بديل ذاتيّ يُحَلّ خادميًّا، **لا يستبدل** المسار على المعرّف |
| المعاملات | `sections` (قائمة مفصولة بفواصل) · `period` (مفتاح الفترة الموحّد) |

### الأقسام الأحد عشر
`identity` · `operationalSummary` · `reports` · `kpi` · `leaveAndPermissions` · `requestsAndBalances` · `attendanceAndCompliance` · `notes` · `governance` · `developmentAndTraining` · `timeline`

كلّ قسم يحمل `key` و`titleAr` و`status` (`Ready|NoData|Partial|Error`) و`dataQuality` و`lastUpdatedAtUtc` و`summary`/`items`.

### الحدود المعماريّة المحفوظة
- **عرض قراءة/إسقاط فقط**: لا جدول `Employee360`، ولا تكرار بيان، ولا كتابة إطلاقًا. مالكو الحقيقة (التقارير/KPI/الإجازات/الحوكمة/التطوير) لم يُمسّوا.
- القسم غير المصرَّح به **لا يظهر مفتاحًا** في `sections`.
- الحقل غير المصرَّح به **لا يُسلسَل**: `Employee360LeaveDto.Reason` عليه `JsonIgnoreCondition.WhenWritingNull`، والاختبار يفحص **غياب المفتاح** لا كونه `null`.
- «غير موجود» و«خارج نطاقي» يعطيان استجابة متطابقة (404) فلا يُستدلّ على وجود موظّف.
- فشل قسم واحد محصور فيه (`Status=Error` + سبب عربيّ عامّ) ولا يُسقِط الصفحة، والاستثناء لا يُسرَّب للعميل.
- عدّاد الملاحظات المفتوحة يُحسب **بعد** ترشيح الحسّاسيّة، وإلّا سرّب العدّاد وجود ملاحظة محجوبة.
- قسم الحضور يُعلن `dataQuality = Unavailable` وسببًا عربيًّا صريحًا بدل اختلاق بيانات أو جدول موازٍ.
- نوافذ KPI تستعمل `IPeriodService` (مرحلة 1) مصدرًا وحيدًا للحدود؛ الأسبوعيّ والربعيّ منفصلان، ولكلّ نافذة `Coverage` و`ExpectedPeriods`.

### الأعلام (§9)
`Phase2:Employee360Enabled` · `Phase2:AttendanceEnabled` · `Phase2:HrOperationsEnabled` · `Phase2:EmployeeChecklistEnabled` — **كلّها `false` افتراضيًّا**. تُرفَع في `Phase2WebApplicationFactory` للاختبار المحلّيّ فقط. رفع العلم **ليس تفويضًا**: كلّ فحوص الصلاحيّة تعمل كاملة تحته، والمسار المُطفَأ يُرجِع 404.

---

## 5) P2-EMP-003 — واجهة Employee 360 + وضع الذات (COMPLETED)

### المكوّنات
| الملفّ | الدور |
|---|---|
| `reporting-frontend/src/types/employee360.ts` | عقد الواجهة + `EMPLOYEE_360_SECTION_ORDER` (ترتيب الأقسام الأحد عشر) |
| `reporting-frontend/src/components/Employee360Panel.tsx` | اللوحة: تنقّل الأقسام، مرشّح الفترة، حالات القسم، مرشّحات الخطّ الزمنيّ |
| `reporting-frontend/src/components/Employee360Panel.test.tsx` | 15 اختبار Vitest |
| `reporting-frontend/src/pages/EmployeeProfilePage.tsx` | **مُوسَّع لا مُستبدَل** (+20 سطرًا): وضع الذات + تركيب اللوحة في نهاية الصفحة |
| `reporting-frontend/src/App.tsx` | **إضافة** مسار `/app/employee/me` بجانب `/app/employee/:userId` (+2 سطر) |

### المبادئ المُثبَتة باختبارات
- **الأقسام تُرسَم من مفاتيح الخادم حصرًا**: لا شرط صلاحيّة محسوب في المتصفّح، ولا إخفاء بصريّ. القسم غير المصرَّح به لا عنوان له ولا رابط تنقّل ولا عقدة DOM.
- **الحقل المحجوب لا عمود له**: عمود «السبب» في الإجازات يُحذف كلّيًّا حين لا يصل الحقل في أيّ صفّ، ويظهر حين يرسله الخادم لصاحب الإذن — اختباران متقابلان.
- **«صفر» ≠ «لا بيانات»**: الملخّص التشغيليّ يعرض `0` رقمًا حقيقيًّا، بينما القسم بحالة `NoData` يعرض حالة فارغة بالسبب العربيّ القادم من الخادم.
- **فشل قسم واحد محصور فيه**: `Status=Error` يعرض بطاقة خطأ + زرّ إعادة محاولة داخل القسم، وبقيّة الأقسام تُرسَم سليمة.
- **الحضور** يُعلن «غير متاحة» + السبب الخادميّ بدل اختلاق بيانات.
- **وضع الذات** ينادي `/employees/me/profile-360`، ولا يشتقّ معرّف المستخدم في المتصفّح إطلاقًا؛ الصفحة في هذا الوضع لا تستدعي `/dashboard/employee-profile/{id}` (نقطة تفترض صلاحيّة إشرافيّة) لأنّ الاستعلام مُعطَّل بـ`enabled: false`.
- **مرشّح الفترة** لا يُرسَل إلّا بعد التطبيق الصريح، والمفتاح المحلول يُعرض كما أعاده الخادم لا كما حسبه المتصفّح.
- **مرشّحات الخطّ الزمنيّ** (النوع/المصدر/«يحتاج إجراءً منّي») تعمل محلّيًّا **بلا أيّ نداء شبكة إضافيّ** — مُقاس بعدّاد النداءات.

### الوصوليّة و RTL
`nav` بـ`aria-label` عربيّ، كلّ قسم `section` بـ`aria-labelledby` و`tabIndex={-1}` و`scroll-mt-24`، روابط التنقّل عناصر `<a>` قابلة للتنقّل بلوحة المفاتيح، الجداول بـ`<th scope="col">`، الهياكل العظميّة بـ`role="status"`، والشبكات متجاوبة (`sm`/`lg`).

### قرار مُوثَّق: لا تعديل على القائمة الرئيسيّة
§8 يُلزِم بالإبقاء على التنقّل الحاليّ، و§9 يجعل `Phase2:Employee360Enabled` **مطفأً افتراضيًّا** ⟹ إضافة عنصر «ملفّي» دائم إلى `navConfig` كانت ستنتج رابطًا يقود إلى 404 في كلّ بيئة غير مُفعَّلة. لذا اكتُفي بالمسار الصريح `/app/employee/me` (الصلاحيّة «يجوز» في §6 لا «يجب»)، وتُترَك إضافة عنصر القائمة إلى المرحلة الثالثة حيث تُعاد تنظيم التنقّل أصلًا.

---

## 6) الأذونات (§6/P2-HR-009) — تعريف بلا منح

عُرِّفت المفاتيح والسياسات فقط: `HrOperations.View` · `HrOperations.Export` · `Attendance.Report` · `Attendance.Review` · `Attendance.Export` · `Attendance.Escalate` + أذونات قراءة الدرجات الحسّاسة.

**لم يُمنَح أيّ إذن لأيّ دور مخزَّن ولا لأيّ مستخدم مخزَّن.** الاختبارات تمنح الإذن لمستخدم اختباريّ مؤقّت عبر claims فقط. إسناد الأدوار الفعليّ قرار نشر لاحق.

---

## 7) P2-ATT-004/005/006/007 — الحضور والالتزام (COMPLETED)

**الالتزامات:** `0034c0c` (نواة Workflow + الدومين + الهجرة) · `c67a711` (آلة الحالات وسطح الـAPI) · `87e96c1` (الواجهة وربط قسم Employee 360).

### آلة الحالات
الحالات الثلاث عشرة: `Draft(0)` · `Reported(1)` · `AwaitingEmployee(2)` · `Acknowledged(3)` · `Disputed(4)` · `EmployeeResponseTimedOut(5)` · `AwaitingHr(6)` · `Confirmed(7)` · `Rejected(8)` · `Corrected(9)` · `Reconciled(10)` · `Escalated(11)` · `Closed(12)`.

المُشغِّلات: `Submit` · `Cancel` · `NotifyEmployee` · `Withdraw` · `Acknowledge` · `Dispute` · `TimeOutEmployeeResponse` · `SendToHr` · `HrConfirm` · `HrReject` · `HrCorrect` · `HrReconcile` · `ReturnToEmployee` · `Escalate` · `Close` · `Void`.

القواعد غير القابلة للتفاوض ومكان إثباتها:
| القاعدة | كيف أُثبِتت |
|---|---|
| `Reported` بلاغ مبدئيّ لا واقعة مؤكَّدة | `AttendancePolicy.IsOfficialIncident` = `Confirmed \|\| ((Closed\|Escalated) && decision==Confirm)` — وحدويّ |
| لا `Confirmed` بلا مرور بحقّ ردّ الموظّف | جدول الانتقالات: المدخل الوحيد إلى `Confirmed` هو `HrConfirm` من `AwaitingHr`، ولا يُبلَغ `AwaitingHr` إلّا بعد `Acknowledge`/`Dispute`/`TimeOutEmployeeResponse` — وحدويّ |
| لا أثر ماليّ إطلاقًا | `AttendanceApiTests.Confirming_An_Incident_Creates_No_Balance_Movement_Whatsoever` يعدّ `EmployeeBalanceLedger` قبل/بعد التأكيد ⇒ متساويان. وبنيويًّا: صفر إشارة إلى `Payroll`/`Ledger`/`Balance`/خصم في `Reporting.Application/Attendance/**` و`AttendanceService.cs` |
| التصالح مع إجازة معتمدة ليس صامتًا | `HrReconcile` ينتج `Reconciled` **وحدثًا** في `attendance_incident_events` |
| لا حذف صامت بعد الإرسال | `Cancel` مقصور على `Draft`؛ بعد الإرسال `Withdraw`/`Void` فقط، وكلاهما يُسجَّل |
| كلّ انتقال في سجلّ الأحداث | `attendance_incident_events` صفّ لكلّ انتقال + مسار `GET /api/attendance/{id}/events` |
| Idempotency | مفتاح تكافؤ على الإنشاء — مُثبَت في e2e «تسجيل بلاغ يحمل مفتاح تكافؤ فيمنع الازدواج عند إعادة المحاولة» |
| Concurrency Token | `concurrencyStamp` إلزاميّ في كلّ تغيير حالة ⇒ 409 عند التعارض |
| المرفقات خارج جذر الويب والوصول غير المصرَّح 404 | `Phase2SecurityGateTests.Attachment_Outside_Scope_Is_404_While_The_Subject_Still_Downloads_It` |

**ترتيب مقصود داخل `RunTransitionAsync`:** تخويل الفاعل ← صلاحيّة الانتقال ← التزامن. بهذا لا يتعلّم فاعلٌ غير مخوَّل أنّ المورد تغيّر.

### الواجهة
`AttendancePage.tsx` + `useAttendance.ts` + `types/attendance.ts` + مسار في `App.tsx` وعنصر في `navConfig.ts`. الأزرار تُرسَم **من `allowedActions` التي يمنحها الخادم** لا من استنتاج العميل؛ والحقل الذي لم يرسله الخادم غائب من الشاشة لا معروضًا فارغًا.

---

## 8) P2-HR-008 — محرّك الالتزامات (COMPLETED)

**الالتزام:** `a7d4e17`

`ObligationPolicy` (نقيّة) + `ObligationsService` + `ObligationsController`. مصدر خادميّ وحيد لـ«المطلوب/الناقص/المتأخّر»: لا يحسب العميل التزامًا ولا يشتقّ تأخّرًا. `GET /api/obligations` تحت `HrOperations.View`، و`GET /api/obligations/me` مفتوحة لصاحبها (لا مورد لغيره كي يُخفى).

**الاختبارات:** `ObligationPolicyTests` (وحدويّ) + `ObligationsApiTests` **17/17** تكامليّ.

---

## 9) P2-HR-009 — لوحة عمليّات الموارد البشريّة وطوابير الإجراءات (COMPLETED)

**الالتزام:** `835efde`

`HrOperationsPolicy` (نقيّة) + `HrOperationsService` + `HrOperationsController` + `HrOperationsPage.tsx`/`useHrOperations.ts`/`types/hrOperations.ts`.

- **مصدر عدّ واحد:** الرقم على البطاقة والبنود داخل الطابور يخرجان من نفس الاستعلام ⇒ الـDrill-down يعيد إنتاج الرقم بالضبط (مُثبَت في e2e).
- **المرشِّح يذهب إلى الخادم** ولا يُطبَّق في المتصفّح (مُثبَت في e2e برصد الطلب).
- **المفتاحان منفصلان:** `HrOperations.View` للّوحة و`HrOperations.Export` للتصدير؛ منع التصدير بـ403 لا يُسقط اللوحة.
- **التصدير مُسجَّل في التدقيق** ويُخبِر المستخدم بذلك.
- الطوابير الأحد عشر لكلّ منها حالة فراغ مستقلّة لا «جدول بلا صفوف» ولا شاشة عطل.

**الاختبارات:** `HrOperationsPolicyTests` (وحدويّ) + `HrOperationsApiTests` **21/21** + `HrOperationsPage.test.tsx` + `hr-operations.spec.ts` (9 حالات e2e).

---

## 10) P2-HR-010 — قائمة خدمة الموظّف والالتزام (COMPLETED)

**الالتزام:** `80e0826`

- **لا نسخ للبيانات المشتقّة:** البند المحسوب يُحسَب من مصدره لحظة الطلب ولا يُخزَّن. الجدول الجديد `employee_checklist_items` **للبنود اليدويّة وحدها**.
- كلّ بند يحمل: الاسم · النوع (محسوب/يدويّ) · الحالة · الجهة المسؤولة · تاريخ الاستحقاق · آخر إجراء · الدليل · رابط المصدر · وهل على المستخدم الحاليّ إجراء.
- **البند المحسوب بلا محرّر**، واليدويّ وحده يحمل حقلًا وزرّ حفظ (مُثبَت في e2e).
- **«غير منطبق» لا يُعرَض عدّادًا صفرًا** (مُثبَت في e2e) — الغياب ليس صفرًا.
- الكتابة تحت `Policies.EmployeeChecklistManage` حصرًا؛ القراءة تحت النطاق والرؤية الحقليّة.

**الاختبارات:** `ChecklistPolicyTests` (وحدويّ) + `EmployeeChecklistApiTests` **20/20** + `EmployeeChecklistPanel.test.tsx` + 7 حالات e2e.

---

## 11) CS10 — اكتمال Employee 360 ووضع الذات وغياب N+1 (COMPLETED)

**الالتزام:** `3b1e71e`

### الأقسام الأحد عشر (`Employee360Section`)
`Identity(1)` · `OperationalSummary(2)` · `Reports(3)` · `Kpi(4)` · `LeaveAndPermissions(5)` · `RequestsAndBalances(6)` · `AttendanceAndCompliance(7)` · `Notes(8)` · `Governance(9)` · `DevelopmentAndTraining(10)` · `Timeline(11)`.

المسارات محفوظة: `/app/employee/:userId` و`/app/employee/me` كلاهما يعمل، و`EmployeeProfileSelfRoute.test.tsx` يحرس أسبقيّة المسار حتّى لا يبتلع `:userId` كلمة `me`.

### قياس غياب N+1 (عدّ أوامر SQL لا انطباع)
`Phase2WebApplicationFactory` يحمل `CountingCommandInterceptor` يعدّ أوامر قاعدة البيانات فعليًّا:

| السطح | قبل تكثير البيانات | بعد التكثير | القراءة |
|---|---|---|---|
| Employee 360 | **27** | **27** | ثابت تحت نموّ البيانات ×20 |
| Employee Checklist | **29** | **29** | ثابت |
| HR Operations | **28** (نطاق 2 مستخدمين) | **28** (نطاق 52 مستخدمًا) | ثابت تحت نموّ النطاق ×26 |

### الأزمنة (n=20 لكلّ قياس)
| السطح | P95 | الوسيط | السقف | النتيجة |
|---|---|---|---|---|
| Employee 360 | **26.5 ms** | 17.9 ms | ≤800 ms | ✅ |
| HR Operations على **500** مرؤوس مباشر | **12.9 ms** | 10.6 ms | ≤1500 ms | ✅ |

> **تحفّظ مُعلَن ولا يُعمَّم:** الـ500 مستخدمًا المزروعون بلا تقارير ولا صفوف KPI، فطوابير اللوحة شبه فارغة. الرقم يُثبت **غياب N+1 وثبات عدد الأوامر**، ولا يصلح تنبّؤًا بزمن الإنتاج على بيانات حقيقيّة.

---

## 12) P2-SEC-011 — بوابة الإغلاق الأمنيّ المتقاطع (COMPLETED)

**الالتزام:** `8a680a7` — الملفّات: `AttendanceActorRules.cs` · `Phase2SecurityGateTests.cs` (جديد، 591 سطرًا) · `Phase2TestAuth.cs` · `AttendanceWorkflowTests.cs`.

### مصفوفة الاستجابة المُلزِمة — مُطبَّقة ومقيسة
| الحالة | الرمز | إثباتها |
|---|---|---|
| مورد/موظّف خارج النطاق | **404** | `Employee_Reading_A_Colleagues_Profile_Gets_404_Indistinguishable_From_Nonexistent` · `Employee_Reading_A_Colleagues_Checklist_Gets_404_Not_403` · `TeamLeader_Can_Neither_Read_Nor_Report_An_Incident_Outside_Their_Team` |
| طلب يكشف وجود مورد غير مرئيّ | **404** | `Attachment_Outside_Scope_Is_404_While_The_Subject_Still_Downloads_It` |
| توجيه سطح الذات إلى موضوع آخر بحقن استعلام | **يُتجاهَل** | `Self_Surfaces_Cannot_Be_Steered_To_Another_Subject_By_Query_Injection` |
| إذن وظيفيّ عامّ ناقص **قبل تحديد أيّ مورد** | **403** | `HrOperations_Without_The_View_Key_Is_403_For_Employee_And_For_The_Hr_Role_Alike` · `Export_Key_Is_Independent_From_View_And_Personal_Obligations_Stay_Open` · `Checklist_Write_Needs_Its_Own_Key_Even_For_A_Supervisor_Who_Can_Read_It` |
| تعارض حالة مشروع على مورد مرئيّ | **409** | مقارنة المراجِع الآخر داخل `Self_Confirmation_Is_403_…` |
| خطأ تحقّق في الجسم | **400** | `ApiControllerBase.ToProblem` (افتراضيّ) |

### حسم الـ403 المسجَّلَين في CS5 — يبقيان 403 ولا يتحوّلان إلى 404
1. **تأكيد المُبلِّغ لبلاغه:** الاختبار يُثبت أوّلًا أنّ الفاعل **يقرأ المورد فعلًا** (`GET /api/attendance/{id}` = 200)، ثمّ يُرفَض تأكيده بـ403. وليعزل السبب في **الفاعل لا في حالة الواقعة**، يُجرِّب مراجِعًا آخر على الحالة نفسها فيحصل على **409 لا 403**. لو حُوِّل إلى 404 لكان النظام يكذب على من يرى المورد أمامه.
2. **تسجيل الموظّف بلاغًا على نفسه:** لا مورد سابق كي يُخفى، والموضوع هو الفاعل نفسه ⇒ 403 صحيح. و`Self_Report_Is_403_And_Provably_Creates_Nothing` يُثبت أنّ قائمة وقائع الموظّف تبقى فارغة بعده.

### الحجب غياب لا `null`
`HrNote_Is_Absent_From_Json_Without_The_Sensitivity_Key_And_Present_With_It` يبني **بنيتين متطابقتين لا تختلفان إلّا في المفتاح**، ثمّ:
- بلا `Hr.SensitiveRead`: المفتاح `hrNote` **غائب من JSON** (`TryGetProperty` = false) ونصّ الملاحظة **لا يرد في الجسم الخام** إطلاقًا.
- بالمفتاح: الحقل حاضر وقيمته مطابقة ⇒ الحجب حراسة لا تعطيل.

ويكمله: `HrOnly_Note_Is_Hidden_From_The_Subject_And_From_Their_Manager_Until_The_Explicit_Key` · `Internal_Note_Reaches_The_Supervisor_But_Not_The_Subject` · `Admin_Alone_Receives_Identity_Only_And_The_Other_Sections_Are_Absent` (المفاتيح المُعادة = `["identity"]` حصرًا) · `Multi_Role_Viewer_Gains_The_Union_Of_Grants_But_No_Sensitive_Field` (Admin+TeamLeader على مرؤوس مباشر: `notes` و`leaveAndPermissions` حاضران، ملاحظة `Internal` مرئيّة، وملاحظة `HrOnly` غائبة).

### حارس بنيويّ — لا نقطة نهاية بلا قرار تخويل صريح
`Every_Phase2_Endpoint_Declares_An_Explicit_Authorization_Decision` يفحص بالانعكاس (Reflection) كلّ أفعال `AttendanceController` · `EmployeesController` · `HrOperationsController` · `ObligationsController`:
- `[Authorize]` على مستوى الصنف موجود، ولا `[AllowAnonymous]` في أيّ موضع.
- كلّ فعل إمّا يحمل `Authorize(Policy=…)` غير فارغة، وإمّا مُدرَج صراحةً في قائمة «القرار داخل الخدمة» (`ServiceEnforcedEndpoints`) — وهي **قائمة إعلان لا قائمة استثناء**: نقطة نهاية جديدة بلا سياسة وبلا إدراج **تُسقِط الاختبار**، وإدراجٌ بائت يُسقِطه أيضًا.
- `Phase2_Endpoint_Surface_Is_The_One_That_Was_Reviewed` يثبّت العدد المُراجَع: 17 فعلًا في `AttendanceController` · 5 في `EmployeesController` · 3 في `HrOperationsController` · 2 في `ObligationsController` — أيّ توسّع لاحق للسطح يستوجب مراجعة أمنيّة جديدة.

> سطح الحضور كلّه «قرار داخل الخدمة» عن قصد: لو حُرِس بسياسة على مستوى الفعل لأنتج **403** عند الخروج من النطاق، وهو ما تمنعه القاعدة. القرار في `AttendanceService`/`AttendanceAccess`/`AttendanceActorRules` كي يظلّ **404**.

### عيب أمنيّ سابق اكتُشِف وعُولِج داخل هذه البوابة
**الوصف:** `AttendanceActorRules.Authorize` كانت تمنح `HrConfirm` بمجرّد `ctx.CanReview`. فمُقدِّم بلاغ يحمل `Attendance.Review` كان **يؤكّد بلاغه بنفسه** ويُنشئ «واقعة رسميّة» منفردًا — انهيار فصل واجبات، لا مجرّد ثغرة شكليّة.

**لماذا لم يظهر:** الاختبار القائم `Reporter_CannotConfirmOwnReport` كان يفحص مُبلِّغًا **بلا** مفتاح المراجعة، فينجح لسبب آخر تمامًا (غياب المفتاح) ويترك فصل الواجبات غير مُختبَر — **أخضر كاذب**.

**العلاج (أصغر تغيير آمن):** فصل ذراع `HrConfirm` وحدها عن بقيّة مُشغِّلات المراجعة، لأنّها المدخل الوحيد إلى `Confirmed`. الرفض **403 لا 404** لأنّ المُبلِّغ يرى واقعته أصلًا.

**اختبارا الانحدار:** `Reporter_CannotConfirmOwnReport_EvenWhenHoldingTheReviewKey` (الحالة الحاسمة + إثبات أنّ مراجِعًا آخر يؤكّد بلا عائق) و`DutySeparation_Binds_Confirm_Only_Not_The_Rest_Of_Hr_Review` (المنع مقصور على التأكيد؛ الرفض والتصحيح والتصالح والإبطال تبقى متاحة للمُبلِّغ الحامل للمفتاح).

**إثبات بالتزييف (Falsification):** باستبدال `ctx.IsReporter` بـ`false` مؤقّتًا سقط الاختبار الوحدويّ وعاد الاختبار التكامليّ بـ`Conflict` بدل `Forbidden`؛ وباستعادة الشرط عادا أخضرين. أي أنّ الاختبارين يقيسان الشرط نفسه لا شيئًا آخر.

**إصلاح تابع:** `Phase2TestAuth.CreateWithRolesAsync` كانت ستُسنِد الأدوار بفشل صامت، فيصير اختبار تعدّد الأدوار «ناجحًا» بدور واحد. صارت تُلقي استثناءً صريحًا عند فشل الإسناد.

---

## 13) قرار مُوثَّق: نطاق دور `Hr`

`RoleAccess.PrimaryRole` لا يُدرِج `Roles.Hr` في ترتيب الأدوار ⇒ مستخدم `Hr` يسقط إلى نطاق `own`. هذا **سلوك قائم لم يُغيَّر عمدًا** (تغييره توسيعٌ ضمنيّ لصلاحيّة دور قائم، وهو ممنوع). أثره العمليّ: عضو الموارد البشريّة يرى موظّفًا آخر عبر **مفاتيح الأذونات الصريحة** (`Attendance.Review` / `HrOperations.View` / `Hr.SensitiveRead`) لا عبر دوره وحده.

لهذا صُمِّمت اختبارات الحسّاسيّة المرهونة بالنطاق على **قائد فريق مشرف** بمفتاح وبلا مفتاح، لا على مستخدم `Hr`: بهذا يبقى المتغيّر الوحيد بين البنيتين هو المفتاح.

---

## 14) الأذونات والسياسات — الجرد النهائيّ

| المفتاح | يحرس |
|---|---|
| `Attendance.Report` | تسجيل بلاغ |
| `Attendance.Review` | مراجعة الموارد البشريّة (تأكيد/رفض/تصحيح/تصالح/إعادة/إبطال/إغلاق) |
| `Attendance.Escalate` | التصعيد |
| `Attendance.Export` | تصدير الحضور |
| `HrOperations.View` | لوحة العمليّات + الطوابير + `GET /api/obligations` |
| `HrOperations.Export` | تصدير الطوابير (منفصل عن العرض) |
| `EmployeeChecklist.Manage` | تحرير بند قائمة يدويّ |
| `Hr.SensitiveRead` | حقول/ملاحظات درجة `HrOnly` |

**لم يُمنَح أيّ إذن لأيّ دور مخزَّن ولا لأيّ مستخدم مخزَّن.** كلّ المنح في الاختبارات عبر `perm` claims لمستخدم اختباريّ مؤقّت. إسناد الأدوار الفعليّ قرار نشر لاحق خارج نطاق هذه المرحلة.

**الأعلام:** `Phase2:Employee360Enabled` · `Phase2:AttendanceEnabled` · `Phase2:HrOperationsEnabled` · `Phase2:EmployeeChecklistEnabled` — **كلّها `false` افتراضيًّا**. رفع العلم ليس تفويضًا: كلّ فحوص الصلاحيّة تعمل كاملة تحته.

---

## 15) الهجرات — إضافيّة بحتة ومُتحقَّق منها up/down

| الهجرة | العمليّات |
|---|---|
| `20260824230015_AddManagementNoteSensitivity` | `AddColumn` واحد (`management_notes.Sensitivity`) |
| `20260824233938_AddAttendanceIncidents` | 4 × `CreateTable` + 9 × `CreateIndex` |
| `20260825111521_P2_HR010_EmployeeChecklistItems` | 1 × `CreateTable` + 2 × `CreateIndex` |

**صفر** عمليّة `Drop`/`Rename`/`Alter`/`Update`/`Delete`/`Sql` خام في أيّ `Up`. كلّ `Down` يُسقِط ما أضافه هو فقط. **لا Backfill**.

### التحقّق الفعليّ (قاعدة تحقّق مؤقّتة مخصّصة `reporting_p2_migverify_20260825`)
| الخطوة | النتيجة المقيسة |
|---|---|
| `up` كامل على قاعدة فارغة | 44 هجرة · `Done.` |
| الكائنات بعد `up` | `attendance_incidents` · `attendance_incident_events` · `attendance_incident_attachments` · `attendance_incident_types` · `employee_checklist_items` · عمود `management_notes.Sensitivity` |
| `down` إلى ما قبل المرحلة (`20260824195457`) | 41 هجرة · تراجع الثلاث بالترتيب العكسيّ |
| بقايا بعد `down` | **0** جدول و**0** عمود — لا أثر |
| `re-up` | 44 هجرة · `Done.` |
| `has-pending-model-changes` | `No changes have been made to the model since the last migration.` |
| تنظيف | `dropdb reporting_p2_migverify_20260825` — نُفِّذ، صفر بقايا |

### حادثة تشغيليّة أثناء التحقّق — سُجِّلت وعُولِجت بالكامل
النداء الأوّل لـ`dotnet ef database update` مرّر سلسلة الاتّصال عبر متغيّر بيئة `ConnectionStrings__Default`، ولم تلتقطه العمليّة، فسقط الحلّ إلى الافتراضيّ المُشفَّر في `DependencyInjection.cs:39` وهو **`reporting_dev`** (قاعدة تطوير محلّيّة على الجهاز نفسه — ليست مشتركة ولا TEST/UAT/RC/إنتاج).

- **الأثر:** طُبِّقت هجرتان فقط (`AddAttendanceIncidents` و`P2_HR010_EmployeeChecklistItems`) — `AddManagementNoteSensitivity` كانت مطبَّقة سلفًا فيها. أُثبِت ذلك بختم إنشاء ملفّي الجدولين في `pg_stat_file` (`15:41:29` من اليوم) وبأنّ مخرَج الأمر لم يتضمّن سوى سطرَي تطبيق.
- **الاستعادة:** `dotnet ef database update 20260824230015_AddManagementNoteSensitivity --connection "…Database=reporting_dev…"` ⇒ عادت القاعدة إلى **42 هجرة** كما كانت، و**0** من جداول المرحلة الثانية باقية.
- **التصحيح المنهجيّ:** كلّ نداءات `dotnet ef` التالية استعملت `--connection` صراحةً لا متغيّر بيئة.
- **لم تُمسّ** `reporting_test` ولا أيّ قاعدة مشتركة أو حيّة في أيّ لحظة.

---

## 16) نتائج الاختبارات النهائيّة — أرقام مقيسة

### الخلفيّة
| المجموعة | النتيجة |
|---|---|
| بناء الحلّ (`dotnet build Reporting.sln`) | **Build succeeded** · 4 تحذيرات · **0 أخطاء** |
| وحدويّ (`Reporting.UnitTests`) | **548 / 548** ✅ |
| تكامل — الحزمة المعزولة للمرحلة الثانية مجتمعةً | **109 / 109** ✅ (19 ث) |
| ├─ `Phase2SecurityGateTests` (التخويل والرؤية الحقليّة) | **17 / 17** ✅ |
| ├─ `AttendanceApiTests` | **14 / 14** ✅ |
| ├─ Employee 360 | **21 / 21** ✅ |
| ├─ `EmployeeChecklistApiTests` | **20 / 20** ✅ |
| ├─ `HrOperationsApiTests` | **21 / 21** ✅ |
| ├─ `ObligationsApiTests` | **17 / 17** ✅ |
| └─ `Phase2PerformanceTests` | **5 / 5** ✅ |
| التحقّق من الهجرات | up/down/re-up نظيف · صفر بقايا · صفر تغييرات نموذج معلّقة |

> مجموع المجموعات المفردة (115) يفوق المجموع المُوحَّد (109) لأنّ مرشِّحات الأسماء تتقاطع (اختبارات قسم الحضور داخل Employee 360 تُلتقط بالمرشِّحين). الرقم المُعتمَد للإغلاق هو **109/109** من التشغيل الموحَّد.

قاعدة التنفيذ: `reporting_p2_20260825` المحلّيّة المعزولة حصرًا. **لم تُستعمل `reporting_test` المشتركة إطلاقًا.**

### الواجهة
| المجموعة | النتيجة |
|---|---|
| `npx tsc -b --force` | **0 أخطاء** · رمز خروج 0 · مخرَج فارغ |
| Vitest — الحزمة الكاملة | **672 / 672** في **56** ملفًّا ✅ (12.2 ث) |
| Playwright (chromium) | **34 / 34** ✅ (16.1 ث) · رمز خروج 0 |

### تغطية Playwright مقابل المطلوب
| المطلوب | الملفّ والحالة |
|---|---|
| Employee 360 | `employee360.spec.ts` — «وضع الذات يرسم الأقسام الأحد عشر…» + «القسم الذي لم يصل لا يُرسَم» + «دورتا تحميل مستقلّتان» |
| `/employee/me` | `employee360.spec.ts:158` — ينادي مسار `me` الخادميّ |
| تسجيل واقعة | `attendance.spec.ts:186` — مفتاح التكافؤ يمنع الازدواج |
| ردّ الموظّف | `attendance.spec.ts:144` — الاعتراض يستلزم رواية مكتوبة ثمّ ينقل الواقعة |
| مراجعة الموارد البشريّة | `attendance.spec.ts:162` — التأكيد عبر `hr-review` بلا أثر ماليّ |
| HR Operations | `hr-operations.spec.ts:120,149,168,176,187` |
| طوابير الإجراءات | `hr-operations.spec.ts:132` (Drill-down) و`:199` (طابور فارغ) |
| قائمة الالتزام | `employee360.spec.ts:200,214,228,237,257` |
| رابط مباشر بلا صلاحيّة | `employee360.spec.ts:182` — شاشة منع لا بيانات موظّف |
| RTL | `attendance.spec.ts:221` · `employee360.spec.ts:269` · `hr-operations.spec.ts:207` |
| مكتب/لوح/جوّال | `attendance.spec.ts:233` (ثلاث حالات) + قياسات داخل حالتَي RTL الأخريَين |

---

## 17) العيوب المكتشَفة أثناء التنفيذ ومعالجتها

| # | العيب | الأثر | المعالجة | الإثبات |
|---|---|---|---|---|
| 1 | `HrConfirm` يُمنَح بـ`CanReview` وحده ⇒ المُبلِّغ يؤكّد بلاغه بنفسه | انهيار فصل واجبات على المخرج الوحيد المُنتِج لـ«واقعة رسميّة» | فصل ذراع `HrConfirm` وحدها (`8a680a7`) | اختبارا انحدار + تزييف مزدوج (وحدويّ + تكامليّ) |
| 2 | `Reporter_CannotConfirmOwnReport` أخضر كاذب (الفاعل بلا مفتاح أصلًا) | الاختبار لم يكن يقيس ما يدّعي | استُبدِل بحالة حاسمة: مُبلِّغ **يحمل** المفتاح | الاختبار يسقط عند تزييف الشرط |
| 3 | `Phase2TestAuth` تُسنِد الأدوار بفشل صامت | اختبار تعدّد الأدوار كان سينجح بدور واحد | استثناء صريح عند فشل الإسناد | `CreateWithRolesAsync` |
| 4 | تأكيد فارغ في مسودّة اختبار البوّابة (`DoesNotContain("HrConfirm")` عند حالة `Reported`) | التأكيد لا يُثبت شيئًا لأنّ المُشغِّل غير متاح في تلك الحالة أصلًا | استُبدِل بمُميِّز: مراجِع آخر على الحالة نفسها ⇒ **409** لا 403 | يعزل السبب في الفاعل لا في الحالة |
| 5 | هجرتان طُبِّقتا سهوًا على `reporting_dev` المحلّيّة | صفر — قاعدة تطوير محلّيّة، لا مشتركة ولا حيّة | تراجُع فوريّ إلى الحالة الأصليّة (42 هجرة) + استعمال `--connection` صراحةً بعدها | §15 |

---

## 18) حدود وأدلّة ناقصة — مُعلَنة صراحةً

1. **قياس الأداء على بيانات غير واقعيّة:** الـ500 مستخدمًا بلا تقارير ولا KPI ⇒ الأرقام تُثبت ثبات عدد الأوامر لا زمن الإنتاج. لا تُعمَّم.
2. **لا قياس على TEST/RC/إنتاج إطلاقًا:** كلّ الأرقام محلّيّة على `reporting_p2_20260825`. لم تُنشَر المرحلة ولم تُشغَّل على أيّ بيئة مشتركة.
3. **الأذونات معرَّفة لا ممنوحة:** لم يُختبَر أثر إسنادها لأدوار حقيقيّة في بيانات حقيقيّة، لأنّ الإسناد قرار نشر لاحق.
4. **نطاق دور `Hr` كما هو:** لم يُوسَّع (§13). أيّ توقّع بأنّ «HR ترى الجميع بدورها وحدها» غير صحيح في هذا البناء.
5. **مهامّ SLA الخلفيّة** (`NotifyEmployee` / `TimeOutEmployeeResponse`) مُشغِّلات نظام مُختبَرة وحدويًّا وتكامليًّا بفاعل نظام؛ لم يُشغَّل جدولٌ زمنيّ حقيقيّ طويل الأمد.

---

## 19) مؤجَّل إلى المرحلة الثالثة حصرًا

1. إسناد المفاتيح الثمانية إلى أدوار مخزَّنة فعليّة (قرار حوكمة + سكربت منح مُراجَع).
2. رفع أعلام `Phase2:*` على أيّ بيئة، وما يستلزمه من تخطيط نشر ونسخ احتياطيّ.
3. جدولة مهامّ SLA للحضور في مُشغِّل خلفيّ حقيقيّ ومراقبتها.
4. إشعارات البريد/SignalR لأحداث الحضور والالتزامات (القناة موجودة، الربط لم يُنفَّذ في هذه المرحلة).
5. قياس أداء على لقطة بيانات واقعيّة، وضبط الفهارس إن لزم بعده.
6. مراجعة نطاق دور `Hr` كقرار منتج مستقلّ (§13) — أيّ توسيع هنا تغييرُ صلاحيّة دور قائم.
7. تصدير/تقارير موسَّعة لعمليّات الموارد البشريّة خارج الطوابير الأحد عشر.

---

## 20) حالة الإغلاق

| البند | القيمة |
|---|---|
| الفرع | `feature/p2-employee360-hr-ops-20260825` |
| SHA النهائيّ الكامل | `8a680a7` → انظر السطر أدناه بعد التزام التقرير |
| `git status --short` (الـworktree) | نظيف عدا هذا التقرير قبل التزامه |
| الشجرة الرئيسيّة `HEAD` | `736b5c567b0dde2511dd91ac8fcb1c9cd466b951` — **لم تتغيّر** |
| تعديلات المستخدم المحميّة | ` M CLAUDE.md` و`?? Ops/R21/RC-CANDIDATE-BUILD-AND-REHEARSAL-REPORT-20260823.md` — **باقيتان كما هما، لم تُمسّا** |
| worktrees أخرى | لم تُفتَح ولم تُمسّ |
| `/tmp/p1-shadow-20260825/` | لم يُستعمل ولم يُحذف |
| Push / Merge / Tag / Deploy | **لم يحدث أيّ منها** |
| قواعد مشتركة أو حيّة | **لم تُمسّ** — العمل كلّه على `reporting_p2_20260825` وقاعدة تحقّق مؤقّتة حُذِفت |

### شروط الإغلاق — كلّها مستوفاة
- [x] لا اختبار أحمر — 548 وحدويّ · 109 تكامل · 672 Vitest · 34 Playwright · كلّها خضراء
- [x] لا نقطة نهاية بلا قرار تخويل — مفروض بحارس بنيويّ لا بمراجعة بشريّة
- [x] لا تسريب على مستوى الحقل — الحقل المحجوب **غائب من JSON** لا `null`
- [x] لا مسار مكسور — كلّ المسارات القائمة محفوظة، ولا مسار جديد يحلّ محلّ قائم
- [x] كلّ هجرة مُراجَعة ومُتحقَّق منها up/down
- [x] لا فارق غير مُفسَّر — الفارق الوحيد (115 مقابل 109) مُفسَّر بتقاطع المرشِّحات
- [x] Employee 360 وHR Operations وطوابير الإجراءات وقائمة الالتزام — منفَّذة بالكامل
