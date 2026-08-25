# تقرير تنفيذ المرحلة الثانية — Employee 360 & HR Operations

- **التاريخ:** 25 أغسطس 2026
- **المرشّح الأساس (Phase 1):** `545689bbf2bab3755e524cf6d89a23a92949b692`
- **الفرع:** `feature/p2-employee360-hr-ops-20260825` (worktree معزول `.claude/worktrees/p2-emp360-20260825`)
- **النطاق:** تنفيذ محلّيّ فقط. لا نشر TEST/RC/إنتاج، ولا Push/Merge/Tag، ولا كتابة على قاعدة مشتركة أو حيّة.
- **قاعدة بيانات المرحلة الثانية المعزولة:** `reporting_p2_20260825` (محلّيّة، أُنشئت لهذه المرحلة وحدها).

> هذا التقرير يُحدَّث تدريجيًّا بعد كلّ Change Set أخضر. الأقسام غير المكتملة موسومة بـ«قيد التنفيذ».

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
| P2-SEC-011 | بوابة الإغلاق الأمنيّ المتقاطع | قيد التنفيذ |

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

## 7) الاختبارات (حتّى هذه النقطة)

| المجموعة | النتيجة |
|---|---|
| وحدويّ (`Reporting.UnitTests`) | **438/438** أخضر (منها 30 اختبارًا جديدًا لمصفوفة الرؤية) |
| تكامل Employee 360 | **15/15** أخضر على `reporting_p2_20260825` |
| بناء الحلّ (خلفيّة) | `Build succeeded` · 0 أخطاء |
| فحص أنواع الواجهة (`tsc -b`) | 0 أخطاء |
| بناء الواجهة (`vite build`) | `built` · نجح |
| Vitest — الحزمة الكاملة | **614/614** أخضر في 52 ملفًّا (منها 15 جديدًا لـEmployee360Panel) · لا ارتداد |
| `git diff --check` | نظيف |

> `node_modules` نُصِّبت في الـworktree بـ`npm ci` (بلا تعديل `package-lock.json`) وهي مُستبعَدة بـ`.gitignore` فلا تدخل أيّ التزام.

---

## 8) نقطة الحفظ (Checkpoint)

آخر معرّف مكتمل في الطابور: **P2-EMP-003**. الاستئناف من **P2-ATT-004**.
