# تقرير جاهزيّة الإصدار المحلّيّ النهائيّ — المرحلة 3: الملاحة وبنية المعلومات والأمن والانحدار

**المعرّف:** `P3-REL-007` · **التاريخ:** 25 أغسطس 2026 · **الطابع الزمنيّ UTC:** `2026-08-25T15:18:32Z`
**الحالة:** `LOCAL_ONLY` — لا نشر، لا دفع، لا دمج، لا وسم، لا مسّ لأيّ قاعدة بيانات مشتركة أو حيّة.
**المرشّح النهائيّ:** `41954ccff768b56e198af23b51648dbdb1da05c9` على الفرع `feature/p3-navigation-final-local-candidate-20260825`.

> هذا التقرير **بيان حالة مقيسة**، لا إعلان جاهزيّة إنتاج ولا نتيجة UAT. كلّ رقم فيه مأخوذ من تشغيل فعليّ محلّيّ في شجرة عمل معزولة، وكلّ ادّعاء غير مقيس مُعلَن صراحةً في §12.

---

## 1. سلسلة النَسَب: الأساس ← المرحلة 1 ← المرحلة 2 ← المرحلة 3

النَسَب مُثبَت بـ`git merge-base --is-ancestor` لا بالمقارنة البصريّة للتواريخ، لأنّ ترتيب التواريخ لا يُثبِت الاشتقاق.

| الحلقة | SHA الكامل | العلاقة بالتالي | نتيجة الإثبات |
|---|---|---|---|
| الأساس (`develop` المنشور) | `736b5c567b0dde2511dd91ac8fcb1c9cd466b951` | سلف `545689bb…` | **نعم** |
| المرحلة 1 — KPI Truth | `545689bb…` | سلف `fa563d8c…` | **نعم** |
| المرحلة 2 — Employee 360 & HR Ops | `fa563d8c8fb85d36f9cf4147a42a939e196c3801` | سلف `HEAD` | **نعم** |
| المرحلة 3 — الملاحة (هذا المرشّح) | `41954ccff768b56e198af23b51648dbdb1da05c9` | — | رأس الفرع |

**الاستنتاج:** السلسلة **خطّيّة ومتّصلة**. المرشّح النهائيّ يحتوي المراحل الثلاث مجتمعةً بلا إعادة كتابة تاريخ ولا `force` ولا `cherry-pick` انتقائيّ.

**نقطة البدء المُلزَمة احتُرِمت حرفيًّا:** الفرع أُنشئ من `fa563d8c8fb85d36f9cf4147a42a939e196c3801` بالضبط، داخل شجرة عمل معزولة `.claude/worktrees/p3-nav-20260825`.

---

## 2. كلّ Commit وChange Set في المرحلة 3

سبع دفعات، كلّ واحدة أُنشئت **بعد** خضرة اختباراتها لا قبلها.

| # | SHA | النوع | المعرّف | الوصف |
|---|---|---|---|---|
| 1 | `6309913` | feat | P3-NAV-001/002 | سجلّ ملاحة واحد واعٍ بالدور والقدرة والنطاق + انعكاس `permissions`/`scopeType` من الخادم |
| 2 | `00d6e77` | feat | P3-NAV-003/004 | أسطح الملاحة: فائض «المزيد ⋯»، فتات خبز، aliases حافظة للاستعلام والمِرساة |
| 3 | `e2c7ef0` | test | P3-NAV-002/004 | تثبيت عقد الملاحة: ترتيب ثابت، ظهور بالقدرة، فائض، فتات خبز |
| 4 | `64a329a` | fix | P3-SEC-005 | القائمة لا تُعلن بابًا مُقفَلًا — المسمّى الوظيفيّ شرط إضافيّ لا بديل |
| 5 | `7e324d2` | test | P3-SEC-005 | انعكاس القدرات مرآة لا مِنحة، والرابط المباشر يصطدم بالحارس |
| 6 | `41954cc` | test | P3-REL-006 | الملاحة تُقاس على البناء المحزَّم وعلى المقاسات الثلاثة |
| 7 | (هذا التقرير) | docs | P3-REL-007 | حزمة جاهزيّة الإصدار المحلّيّ النهائيّ |

**الملفّات المتأثّرة: 23 ملفًّا · +1929 / −261 سطرًا.**

| الملفّ | الأثر |
|---|---|
| `reporting-backend/src/Reporting.Application/Auth/AuthModels.cs` | +15/−? — حقلا `Permissions` و`ScopeType` في `AuthResponse` و`MeResponse` |
| `reporting-backend/src/Reporting.Infrastructure/Services/AuthService.cs` | انعكاس المطالبات والنطاق داخل `IssueAsync` (يخدم `Login` و`Refresh` معًا) |
| `reporting-backend/tests/Reporting.IntegrationTests/Phase3CapabilityReflectionTests.cs` | **جديد** — 11 اختبار تكامل لانعكاس القدرات |
| `reporting-frontend/src/lib/navConfig.ts` | +502/−? — السجلّ الموحَّد: 7 وحدات، 48 عنصرًا، 18 alias |
| `reporting-frontend/src/lib/useNavCtx.ts` | **جديد** — بناء سياق الملاحة من الجلسة |
| `reporting-frontend/src/components/NavOverflow.tsx` | **جديد** — شريط الأقسام + طيّ الفائض إلى «المزيد ⋯» |
| `reporting-frontend/src/components/Breadcrumbs.tsx` | **جديد** — الوحدة › المجموعة › القسم |
| `reporting-frontend/src/components/AliasRedirect.tsx` | **جديد** — تحويل حافظ للاستعلام والمِرساة |
| `reporting-frontend/src/components/DashboardShell.tsx` | +190/−? — الشريط الجانبيّ والدرج والمعالم المعنونة |
| `reporting-frontend/src/components/HeaderActions.tsx` | مواءمة مع السجلّ |
| `reporting-frontend/src/App.tsx` | +15 — تصيير الـaliases داخل الغلاف نفسه |
| `reporting-frontend/src/lib/auth.tsx` · `src/types/api.ts` | استقبال `permissions`/`scopeType` |
| 6 ملفّات اختبار واجهة + `e2e/navigation.spec.ts` | تثبيت العقد وقياسه على البناء المحزَّم |

---

## 3. خريطة بنية المعلومات النهائيّة (Final IA Sitemap)

**سبع وحدات ثابتة، بترتيب ثابت، لكلّ الأدوار.** ما يتغيّر بالدور هو **محتوى** الوحدة لا **وجودها ولا موضعها** — وهذا هو الفرق بين قائمة متوقَّعة وقائمة تتراقص تحت المستخدم كلّما تغيّر دوره.

| # | الوحدة | المعرّف | عدد الأقسام |
|---|---|---|---|
| 1 | الرئيسية | `home` | 1 |
| 2 | الموظفون | `people` | 11 |
| 3 | التقارير | `reports` | 14 |
| 4 | الأداء وKPI | `performance` | 3 |
| 5 | الحوكمة | `governance` | 6 |
| 6 | العملاء والمشروعات | `portfolio` | 3 |
| 7 | الإعدادات | `settings` | 10 |
| | **الإجمالي** | | **48 قسمًا** |

### 3.1 الشجرة الكاملة

```
① الرئيسية
   └── الرئيسية ................................ /app

② الموظفون
   ├── ملفي ................................... /app/employee/me
   ├── دليل الموظفين ........................... /app/hr-employees
   ├── فرق العمل ............................... /app/teams
   ├── الحضور والالتزام ......................... /app/attendance
   ├── الإجازات والاستئذانات ..................... /app/leave-requests
   ├── الطلبات ................................. /app/hr-requests
   ├── الأرصدة ................................. /app/balances
   ├── إدارة الأرصدة ............................ /app/balance-management
   ├── الطلبات المؤثّرة على الراتب ................ /app/payroll/leave-impacts
   ├── التطوير والمتابعة ........................ /app/development
   └── عمليّات الموارد البشريّة .................... /app/hr-operations   [قدرة: HrOperations.View]

③ التقارير
   ├── تقاريري ................................. /app/my-reports
   ├── تقارير النطاق ............................ /app/submissions
   ├── التقويم والاستحقاقات ..................... /app/report-calendar
   ├── متابعة الالتزام .......................... /app/compliance
   ├── التقارير التنفيذية ....................... /app/reports
   ├── التحليلات ............................... /app/analytics
   ├── تجميع المبيعات ........................... /app/sales-aggregation
   ├── لوحة مبيعات الفريق ....................... /app/sales/team-dashboard
   ├── لوحة مبيعاتي ............................. /app/sales/my-dashboard
   ├── تقارير التنفيذ ........................... /app/execution-reports
   ├── لوحة تنفيذ الفريق ........................ /app/execution/team-dashboard
   ├── القوالب والإصدارات ....................... /app/report-templates
   ├── مسارات الاعتماد .......................... /app/workflows
   └── منح الرؤية .............................. /app/report-view-grants

④ الأداء وKPI
   ├── لوحة الأداء ............................. /app/performance
   ├── قوالب KPI ............................... /app/kpi-templates
   └── التصدير المالي ........................... /app/kpi-finance-export

⑤ الحوكمة
   ├── نظرة عامة ............................... /app/governance
   ├── ورشة الحوكمة ............................ /app/governance-workspace
   ├── التصعيدات ............................... /app/governance/escalations
   ├── الإجراءات ............................... /app/governance/action-items
   ├── سجل التدقيق ............................. /app/audit
   └── الأرشيف الإداري .......................... /app/admin/archive

⑥ العملاء والمشروعات
   ├── العملاء ................................. /app/clients
   ├── المشروعات ............................... /app/projects
   └── مشاريع عملائي ........................... /app/account-portfolio

⑦ الإعدادات
   ├── المستخدمون والأدوار والصلاحيات ............. /app/users
   ├── الإدارات ................................ /app/departments
   ├── المسميات ................................ /app/job-roles
   ├── المناصب ................................. /app/positions
   ├── الخدمات ................................. /app/services
   ├── الدورات ................................. /app/courses
   ├── تصنيفات التنفيذ .......................... /app/execution-taxonomy
   ├── الإشعارات والبريد ........................ /app/email-control
   ├── سجل البريد .............................. /app/email-notifications
   └── الإعدادات العامة ......................... /app/settings
```

### 3.2 المسارات التفصيليّة غير المُدرَجة في القائمة (`matchPaths`)

سبعة مسارات تفصيليّة لا تظهر كأقسام مستقلّة، لكنّها **تُعلِّم وحدتها وقسمها** فلا يفقد المستخدم موضعه عند الدخول إليها:
`/app/teams/:teamId` · `/app/clients/:clientId` · `/app/projects/:projectId` · `/app/projects/:projectId/360` · `/app/employee/:userId` · `/app/employees/:userId/kpi` · `/app/job-roles/manage`.

---

## 4. مصفوفة الملاحة الفعليّة حسب الدور والنطاق

**مقيسة لا موصوفة:** وُلِّدت بتشغيل `isItemVisible` على **كلّ** عنصر × **كلّ** دور × **كلّ** نطاق، في حالتين: «بكلّ القدرات» و«بلا أيّ قدرة». الاختلاف بين العمودين هو أثر القدرات الصريح.

| القسم | يراه (بكلّ القدرات) | يراه (بلا قدرة) |
|---|---|---|
| الرئيسية | الجميع (12 دورًا) | الجميع |
| ملفي | الجميع | الجميع |
| دليل الموظفين | HR, CEO, GM, CeoSupport, Admin | نفسه |
| فرق العمل | TeamLeader, Manager, CEO, GM, CeoSupport, Admin, Viewer | نفسه |
| الحضور والالتزام | الجميع | نفسه |
| الإجازات والاستئذانات | الجميع | نفسه |
| الطلبات | الجميع | نفسه |
| الأرصدة | الجميع | نفسه |
| إدارة الأرصدة | HR, CEO, GM, CeoSupport, Admin | نفسه |
| الطلبات المؤثّرة على الراتب | HR, CEO, GM, CeoSupport, Admin, FinanceManager, Accountant | نفسه |
| التطوير والمتابعة | الجميع | نفسه |
| **عمليّات الموارد البشريّة** | الجميع **بشرط `HrOperations.View`** | **لا أحد** |
| تقاريري | الجميع | نفسه |
| تقارير النطاق | TeamLeader, Manager, CEO, GM, CeoSupport, Admin, Viewer | نفسه |
| التقويم والاستحقاقات | الجميع | نفسه |
| متابعة الالتزام | TeamLeader, Manager, HR, CEO, GM, CeoSupport, Admin, Viewer | نفسه |
| التقارير التنفيذية | TeamLeader, Manager, CEO, GM, CeoSupport, Admin, Viewer | نفسه |
| التحليلات | TeamLeader, Manager, CEO, GM, CeoSupport, Admin, Viewer | نفسه |
| تجميع المبيعات | Manager, CEO, GM, Admin | نفسه |
| لوحة مبيعات الفريق | TeamLeader, Manager, CEO, GM, Admin | نفسه |
| لوحة مبيعاتي | الجميع | نفسه |
| تقارير التنفيذ | TeamLeader, Manager, CEO, GM, Admin, AccountPortfolioReader | نفسه |
| لوحة تنفيذ الفريق | TeamLeader, Manager, CEO, GM, Admin | نفسه |
| القوالب والإصدارات | CEO, GM, Admin | نفسه |
| مسارات الاعتماد | TeamLeader, Manager, CEO, GM, CeoSupport, Admin, Viewer | نفسه |
| منح الرؤية | Admin | نفسه |
| لوحة الأداء | الجميع | نفسه |
| قوالب KPI | CEO, GM, Admin | نفسه |
| التصدير المالي | HR, CEO, GM, CeoSupport, Admin, FinanceManager, Accountant | نفسه |
| نظرة عامة (حوكمة) | CEO, GM, CeoSupport, Admin | نفسه |
| ورشة الحوكمة | TeamLeader, Manager, HR, CEO, GM, CeoSupport, Admin | نفسه |
| التصعيدات | Employee, TeamLeader, Manager, HR, CEO, GM, CeoSupport, Admin | نفسه |
| الإجراءات | Employee, TeamLeader, Manager, HR, CEO, GM, CeoSupport, Admin | نفسه |
| سجل التدقيق | CEO, GM, Admin | نفسه |
| الأرشيف الإداري | CEO, GM, Admin | نفسه |
| العملاء | TeamLeader, Manager, CEO, GM, CeoSupport, Admin, Viewer, AccountPortfolioReader | نفسه |
| المشروعات | TeamLeader, Manager, CEO, GM, CeoSupport, Admin, Viewer | نفسه |
| مشاريع عملائي | Admin, AccountPortfolioReader | نفسه |
| المستخدمون والأدوار | CEO, CeoSupport, Admin | نفسه |
| الإدارات · المناصب · الإشعارات والبريد · الإعدادات العامة | Admin | نفسه |
| المسميات | HR, CEO, GM, CeoSupport, Admin | نفسه |
| الخدمات · الدورات · تصنيفات التنفيذ | CEO, GM, Admin | نفسه |
| سجل البريد | CEO, GM, CeoSupport, Admin | نفسه |

### 4.1 دور النطاق (`scopeType`)

النطاق **يُحسَب على الخادم** من الدور الأساسيّ ولا يُرسَل من العميل: `Admin`/`CeoSupport` → `governance` · `CEO`/`GeneralManager` → `company` · `Manager` → `department` · `TeamLeader` → `team` · غير ذلك → `own`. وهو يُستعمل في الملاحة **لتشكيل المحتوى المعروض** (نطاق البيانات) لا لمنح صلاحيّة. المُثبَت اختباريًّا: نطاق واسع **لا يستلزم أيّ قدرة** (Admin بنطاق `governance` وقائمة قدرات فارغة يُرفَض على `/api/hr-operations/dashboard` بـ403).

### 4.2 القدرات (`permissions`)

عنصر واحد فقط في السجلّ مشروط بقدرة صريحة: **عمليّات الموارد البشريّة** (`HrOperations.View`). وقد ثبت اختباريًّا أنّه **لا يُرى بأيّ دور مهما اتّسع نطاقه** ما لم تحضر القدرة نفسها. القدرات تُنقَل كمطالبات `perm` في الرمز وتنعكس على `/auth/me` و`/auth/login` معًا.

---

## 5. مصفوفة الحفاظ على الوظائف النهائيّة (Feature Preservation Matrix)

**القاعدة المفروضة:** لا حذف ضمنيّ. أيّ مسار كان قبل المرحلة 3 يجب أن يبقى بعدها — إمّا في السجلّ أو خلفه عبر alias.

| المقياس | قبل المرحلة 3 | بعد المرحلة 3 | الفرق |
|---|---|---|---|
| عدد المسارات التطبيقيّة (`APP_ROUTES`) | 58 | **59** | +1 |
| مسارات محذوفة | — | **0** | — |
| مسارات مضافة | — | 1 (`/app/performance`) | +1 |
| **UnexplainedDeltaCount** | — | **0** | — |

### 5.1 الجدول التفصيليّ — 59 مسارًا، جميعها `PRESERVED`

الأعمدة: المسار · الحارس الفعليّ في `App.tsx` · نتيجة الدخول المباشر للمصرَّح · نتيجة الدخول المباشر لغير المصرَّح · معرّف اختبار الانحدار.

**الحراسة العرضيّة موحَّدة بنيويًّا:** كلّ عنصر في `APP_ROUTES` يمرّ عبر `Protected` في حلقة `.map()` واحدة، والمسارات الحرفيّة الوحيدة خارجها هي `/` و`/login` (العامّتان). هذا مفروض باختبار بنيويّ لا بالمراجعة البصريّة: `navSecurity.test.ts › لا مسار تطبيقيّ يُصيَّر خارج الغلاف المحميّ`.

**لذلك تنطبق النتيجتان التاليتان على الـ59 كلّها بلا استثناء:**
- **دخول مباشر لمصرَّح:** المحتوى يُعرَض داخل `DashboardShell` مع تعليم الوحدة والقسم. (`ProtectedRoute.test.tsx › بالدور المسموح: المحتوى يُعرَض`)
- **دخول مباشر لغير مصرَّح:** تحويل إلى `/app` **بلا تسريب أيّ جزء من المحتوى**. وبلا جلسة: تحويل إلى `/login`. وأثناء تحميل الجلسة: لا محتوى ولا تحويل. (`ProtectedRoute.test.tsx` — الاختبارات الأربعة)

| # | المسار | الحارس | الحالة |
|---|---|---|---|
| 1 | `/app` | أيّ مصادَق عليه | PRESERVED |
| 2 | `/app/teams` | EXEC_ROLES | PRESERVED |
| 3 | `/app/teams/:teamId` | EXEC_ROLES | PRESERVED |
| 4 | `/app/clients` | CLIENT_360_ROLES | PRESERVED |
| 5 | `/app/clients/:clientId` | CLIENT_360_ROLES | PRESERVED |
| 6 | `/app/projects` | EXEC_ROLES | PRESERVED |
| 7 | `/app/projects/:projectId` | PROJECT_360_ROLES | PRESERVED |
| 8 | `/app/projects/:projectId/360` | PROJECT_360_ROLES | PRESERVED |
| 9 | `/app/account-portfolio` | ACCOUNT_PORTFOLIO_ROLES | PRESERVED |
| 10 | `/app/account-portfolio/projects/:id` | ACCOUNT_PORTFOLIO_ROLES | PRESERVED |
| 11 | `/app/account-portfolio/clients/:id` | ACCOUNT_PORTFOLIO_ROLES | PRESERVED |
| 12 | `/app/employee/me` | أيّ مصادَق عليه | PRESERVED |
| 13 | `/app/employee/:userId` | أيّ مصادَق عليه | PRESERVED |
| 14 | `/app/my-kpi` | أيّ مصادَق عليه | PRESERVED |
| 15 | `/app/employees/:userId/kpi` | أيّ مصادَق عليه | PRESERVED |
| 16 | `/app/submissions` | أيّ مصادَق عليه | PRESERVED |
| 17 | `/app/my-reports` | أيّ مصادَق عليه | PRESERVED |
| 18 | `/app/leave-requests` | أيّ مصادَق عليه | PRESERVED |
| 19 | `/app/attendance` | أيّ مصادَق عليه | PRESERVED |
| 20 | `/app/hr-operations` | أيّ مصادَق عليه + قدرة `HrOperations.View` خادميًّا | PRESERVED |
| 21 | `/app/balances` | أيّ مصادَق عليه | PRESERVED |
| 22 | `/app/hr-requests` | أيّ مصادَق عليه | PRESERVED |
| 23 | `/app/balance-management` | BALANCE_MANAGEMENT_ROLES | PRESERVED |
| 24 | `/app/payroll/leave-impacts` | PAYROLL_ROLES | PRESERVED |
| 25 | `/app/kpi-finance-export` | KPI_FINANCE_EXPORT_ROLES | PRESERVED |
| 26 | `/app/report-templates` | TEMPLATE_GOVERNANCE_ROLES | PRESERVED |
| 27 | `/app/kpi` | أيّ مصادَق عليه | PRESERVED |
| 28 | `/app/performance` | أيّ مصادَق عليه | **ADDED** (هدف موحَّد للوحة الأداء) |
| 29 | `/app/report-calendar` | أيّ مصادَق عليه | PRESERVED |
| 30 | `/app/kpi-templates` | TEMPLATE_GOVERNANCE_ROLES | PRESERVED |
| 31 | `/app/workflows` | EXEC_ROLES | PRESERVED |
| 32 | `/app/governance` | GOVERNANCE_ROLES | PRESERVED |
| 33 | `/app/governance-workspace` | GOVERNANCE_WORKSPACE_ROLES | PRESERVED |
| 34 | `/app/governance/escalations` | GOVERNANCE_ESCALATION_ROLES | PRESERVED |
| 35 | `/app/governance/action-items` | GOVERNANCE_ACTION_ITEM_ROLES | PRESERVED |
| 36 | `/app/analytics` | EXEC_ROLES | PRESERVED |
| 37 | `/app/sales-aggregation` | EXEC_ROLES | PRESERVED |
| 38 | `/app/sales/team-dashboard` | TEAM_SALES_DASHBOARD_ROLES | PRESERVED |
| 39 | `/app/execution-reports` | EXECUTION_REPORTS_ROLES | PRESERVED |
| 40 | `/app/execution/team-dashboard` | TEAM_EXECUTION_DASHBOARD_ROLES | PRESERVED |
| 41 | `/app/sales/my-dashboard` | أيّ مصادَق عليه | PRESERVED |
| 42 | `/app/compliance` | COMPLETION_ROLES | PRESERVED |
| 43 | `/app/hr-employees` | HR_EMPLOYEE_ROLES | PRESERVED |
| 44 | `/app/users` | USERS_PAGE_ROLES | PRESERVED |
| 45 | `/app/job-roles` | JOB_ROLE_MANAGEMENT_ROLES | PRESERVED |
| 46 | `/app/job-roles/manage` | JOB_ROLE_MANAGEMENT_ROLES | PRESERVED |
| 47 | `/app/positions` | ADMIN | PRESERVED |
| 48 | `/app/report-view-grants` | ADMIN | PRESERVED |
| 49 | `/app/email-notifications` | GOVERNANCE_ROLES | PRESERVED |
| 50 | `/app/email-control` | ADMIN | PRESERVED |
| 51 | `/app/departments` | ADMIN | PRESERVED |
| 52 | `/app/courses` | TEMPLATE_GOVERNANCE_ROLES | PRESERVED |
| 53 | `/app/services` | TEMPLATE_GOVERNANCE_ROLES | PRESERVED |
| 54 | `/app/execution-taxonomy` | TEMPLATE_GOVERNANCE_ROLES | PRESERVED |
| 55 | `/app/settings` | ADMIN | PRESERVED |
| 56 | `/app/audit` | أيّ مصادَق عليه | PRESERVED |
| 57 | `/app/admin/archive` | ARCHIVE_GOVERNANCE_ROLES | PRESERVED |
| 58 | `/app/development` | أيّ مصادَق عليه | PRESERVED |
| 59 | `/app/reports` | EXEC_ROLES | PRESERVED |

**لا مسار بحالة `Unknown`، ولا مسار بلا اختبار.** الحفاظ العدديّ مفروض آليًّا في `routeRegistry.test.ts`، والحفاظ الأمنيّ في `navSecurity.test.ts`، والحفاظ السلوكيّ في `ProtectedRoute.test.tsx` + `navigation.spec.ts`.

**تعريف الأدوار المختصرة أعلاه:**
`EXEC_ROLES` = Admin, CEO, GM, Manager, TeamLeader, CeoSupport, Viewer ·
`CLIENT_360_ROLES` = EXEC_ROLES + AccountPortfolioReader ·
`PROJECT_360_ROLES` = CLIENT_360_ROLES + Employee ·
`ACCOUNT_PORTFOLIO_ROLES` = AccountPortfolioReader, Admin ·
`BALANCE_MANAGEMENT_ROLES` = Admin, CEO, GM, CeoSupport, HR ·
`PAYROLL_ROLES` = `KPI_FINANCE_EXPORT_ROLES` = Admin, CEO, GM, HR, CeoSupport, FinanceManager, Accountant ·
`TEMPLATE_GOVERNANCE_ROLES` = `ARCHIVE_GOVERNANCE_ROLES` = Admin, CEO, GM ·
`GOVERNANCE_ROLES` = Admin, CeoSupport, CEO, GM ·
`GOVERNANCE_WORKSPACE_ROLES` = GOVERNANCE_ROLES + Manager, TeamLeader, HR ·
`GOVERNANCE_ESCALATION_ROLES` = `GOVERNANCE_ACTION_ITEM_ROLES` = GOVERNANCE_WORKSPACE_ROLES + Employee ·
`TEAM_SALES_DASHBOARD_ROLES` = `TEAM_EXECUTION_DASHBOARD_ROLES` = Admin, CEO, GM, Manager, TeamLeader ·
`EXECUTION_REPORTS_ROLES` = TEAM_EXECUTION_DASHBOARD_ROLES + AccountPortfolioReader ·
`COMPLETION_ROLES` = EXEC_ROLES + HR ·
`HR_EMPLOYEE_ROLES` = `JOB_ROLE_MANAGEMENT_ROLES` = Admin, CeoSupport, HR, GM, CEO ·
`USERS_PAGE_ROLES` = Admin, CEO, CeoSupport ·
`ADMIN` = Admin.

---

## 6. بيان الـAliases والتحويلات (Alias/Redirect Manifest)

**18 alias عبر 14 عنصرًا.** كلّ alias يُصيَّر كتحويل خالص إلى وجهته، والوجهة وحدها تحمل `ProtectedRoute` — فلا التفاف على الحراسة. **الاستعلام والمِرساة يُحفظان**، لأنّ التحويل تنقّل داخليّ لا إعادة تحميل.

| # | من (الرابط القديم) | إلى (الوجهة الحاليّة) |
|---|---|---|
| 1 | `/app/directory` | `/app/hr-employees` |
| 2 | `/app/leaves` | `/app/leave-requests` |
| 3 | `/app/permissions` | `/app/leave-requests` |
| 4 | `/app/employee-requests` | `/app/hr-requests` |
| 5 | `/app/calendar` | `/app/report-calendar` |
| 6 | `/app/kpi-compliance` | `/app/compliance` |
| 7 | `/app/executive` | `/app/reports` |
| 8 | `/app/sales-aggregate` | `/app/sales-aggregation` |
| 9 | `/app/approval-flows` | `/app/workflows` |
| 10 | `/app/kpi-evaluations` | `/app/performance` |
| 11 | `/app/kpi-aggregate` | `/app/performance` |
| 12 | `/app/kpi-quarterly` | `/app/performance` |
| 13 | `/app/risks` | `/app/governance` |
| 14 | `/app/decisions` | `/app/governance` |
| 15 | `/app/escalations` | `/app/governance/escalations` |
| 16 | `/app/actions` | `/app/governance/action-items` |
| 17 | `/app/notifications` | `/app/email-control` |
| 18 | `/app/email-log` | `/app/email-notifications` |

**الثابت المفروض اختباريًّا** (`navSecurity.test.ts › كل alias يرث حارس وجهته المرجعيّة`): لكلّ alias، الوجهة مسجَّلة بحارسها **و**الـalias نفسه **ليس** مسارًا مستقلًّا له حارس آخر. أي أنّ إضافة alias لا تفتح بابًا جديدًا أبدًا.

**دليل الحفظ المقيس** (`navigation.spec.ts`): الدخول على `/app/escalations?status=Open#top` ينتهي بالضبط على `/app/governance/escalations?status=Open#top`.

---

## 7. نتائج الأمن والدخول المباشر (P3-SEC-005)

### 7.1 الثابت الأمنيّ المفروض

```
{ الأدوار التي ترى الرابط }  ⊆  { الأدوار التي يسمح لها حارس المسار بفتحه }
```

اتّجاه واحد عمدًا: **التضييق مسموح** (القائمة قد تُخفي ما يُسمح بفتحه مباشرةً)، **والتوسيع ممنوع**. والقياس على **أسوأ حالة**: يُمنح المستخدم كلّ القدرات وكلّ المسمّيات الوظيفيّة وكلّ النطاقات، فإن ظهر الرابط عندئذٍ لدور لا يسمح به الحارس فهو تسريب عرض حقيقيّ.

| الفحص | النتيجة |
|---|---|
| تسريبات القائمة (`leaks`) عبر 48 عنصرًا × 12 دورًا × 6 نطاقات | **0** |
| مسارات مسجَّلة بلا حارس معروف | **0** |
| مسارات تطبيقيّة خارج الغلاف المحميّ | **0** (الحرفيّ الوحيد: `/` و`/login`) |
| aliases لها حارس مستقلّ | **0** |
| عناصر مشروطة بقدرة تظهر بلا قدرتها | **0** |
| عناصر تظهر بلا جلسة مصادَق عليها | **0** |

**عطب واحد اكتُشف وأُصلح أثناء التنفيذ** (`64a329a`): كانت القائمة تُعلن بابًا مُقفَلًا لأنّ المسمّى الوظيفيّ كان يُعامَل كبديل عن شرط الدور بدل أن يكون شرطًا إضافيًّا فوقه. أُصلِح، وثُبِّت بالاختبار حتّى لا يعود.

### 7.2 الدخول المباشر بالرابط (تجاوز القائمة كلّها)

| السيناريو | السلوك المقيس |
|---|---|
| مسار عميق بلا جلسة | `/login` — ولا ذرّة محتوى |
| مسار عميق بدور غير مسموح | `/app` — ولا ذرّة محتوى |
| مسار عميق بالدور المسموح | المحتوى يُعرَض |
| مسار عميق أثناء تحميل الجلسة | **لا محتوى ولا تحويل** (التحويل المبكّر كان سيطرد صاحب الجلسة الصحيحة عند كلّ إعادة تحميل) |

### 7.3 انعكاس القدرات على الخادم — مرآة لا مِنحة

11 اختبار تكامل على قاعدة معزولة، تُثبِت أنّ حقلَي `permissions` و`scopeType` في `/auth/me` و`/auth/login`:

| الثابت | النتيجة |
|---|---|
| يعكسان مطالبات **المتّصل نفسه** بمساواة مجموعيّة تامّة (لا احتواء) | ✅ |
| بلا مفاتيح ⇒ **قائمة فارغة حاضرة** لا حقل غائب | ✅ |
| `/auth/login` و`/auth/me` يُبلِغان **نفس** القدرات والنطاق | ✅ |
| كلّ مفتاح مُعلَن يفتح مساره فعلًا (`HrOperations.View` → 200 على `/api/hr-operations/dashboard`) | ✅ |
| كلّ مفتاح غائب يُرفَض مساره فعلًا (بلا `HrOperations.Export` → 403 على مسار التصدير) | ✅ |
| نطاق واسع **لا يستلزم** أيّ قدرة (Admin: `governance` + قائمة فارغة + 403) | ✅ |
| النطاق **يحسبه الخادم** من الدور (5 حالات: own/team/department/company/governance) | ✅ |
| نطاق مُملى من العميل (ترويسة `X-Scope-Type` أو استعلام `?scopeType=`) **يُتجاهَل** | ✅ |

**الموقف المبدئيّ المحفوظ:** الخادم يبقى **المُخوِّل الوحيد**. إخفاء عنصر من القائمة تحسين تجربة لا إجراء أمنيّ، وإظهار عنصر لا يمنح شيئًا. ما تغيّر في المرحلة 3 هو أنّ القائمة صارت **صادقة** عن حدود المستخدم بدل أن تعرض أبوابًا تُصفَع في وجهه.

---

## 8. نتائج الاختبارات بالأرقام الفعليّة

كلّها مُشغَّلة محلّيًّا على المرشّح النهائيّ. قاعدة التكامل معزولة (`reporting_p2_20260825`) — **لا مساس بأيّ قاعدة مشتركة أو حيّة**.

| البوّابة | الأمر | الأساس (قبل المرحلة 3) | النتيجة | الحالة |
|---|---|---|---|---|
| بناء الحلّ الخلفيّ | `dotnet build Reporting.sln` | — | نجح · 4 تحذيرات · **0 أخطاء** | ✅ |
| اختبارات وحدويّة خلفيّة | `dotnet test` (Unit) | 548 | **548 نجحت / 0 فشلت** | ✅ |
| اختبارات تكامل | فلتر `Phase2` + `Phase3` | ≥109 | **124 نجحت / 0 فشلت** (+15) | ✅ |
| فحص الأنواع | `npx tsc -b --force` | نظيف | **نظيف** | ✅ |
| اختبارات الواجهة | `npx vitest run` | 672 | **734 نجحت / 61 ملفًّا** (+62) | ✅ |
| بناء الواجهة | `npm run build` | نجح | **نجح** (≈2.73 ث) | ✅ |
| E2E على البناء المحزَّم | `npx playwright test` | 34 | **42 نجحت / 0 فشلت** (+8) | ✅ |
| التدقيق الأسلوبيّ | `npm run lint` | 42 مشكلة / 25 خطأ | **42 / 25 — بلا تغيير** | ⚠️ أساس قائم |

**ملاحظة على `lint`:** الرقم مطابق للأساس تمامًا، **وصفر** من الملاحظات يقع على ملفّ من ملفّات المرحلة 3. أي أنّ المرحلة 3 لم تُضِف ولا واحدة ولم تُصلِح ولا واحدة (الإصلاح خارج نطاق التصريح).

### 8.1 ما تُثبِته اختبارات المرحلة 3 الجديدة تحديدًا

| المجموعة | العدد | ما تُثبِته |
|---|---|---|
| `Phase3CapabilityReflectionTests.cs` | 11 | انعكاس القدرات مرآة دقيقة لا تزيد ولا تنقص ولا تمنح |
| `navConfig.test.ts` | — | عقد السجلّ: الترتيب الثابت، الظهور بالقدرة، اشتقاق الـaliases |
| `navSecurity.test.ts` | 6 | القائمة ⊆ الحارس، والحراسة بنيويّة لا اتّفاقيّة |
| `ProtectedRoute.test.tsx` | 4 | الدخول المباشر يصطدم بالحارس في الحالات الأربع |
| `NavOverflow.test.tsx` | — | الفائض يُطوى ولا يُبتلع الموضع الحاليّ |
| `DashboardShell.p3.test.tsx` | — | الوحدات السبع، فتات الخبز، الحالة النشطة أب+ابن |
| `navigation.spec.ts` (E2E) | 8 | السلوك على **البناء المحزَّم** وعلى المقاسات الثلاثة |

---

## 9. فحص الأداء السريع (Performance Smoke)

| الجانب | الأثر | الدليل |
|---|---|---|
| نداءات شبكة إضافيّة عند الإقلاع | **صفر** — `/auth/me` كان يُنادى أصلًا، وحمل حقلين إضافيّين لا ينشئ رحلة جديدة | مسار الإقلاع في `auth.tsx` |
| حجم الحمولة الإضافيّة على `/auth/me` | قائمتان قصيرتان (قدرات المستخدم + سلسلة نطاق واحدة) | `AuthModels.cs` |
| كلفة السجلّ في زمن التشغيل | ثابت وحدة (module constant) يُقيَّم **مرّة واحدة** عند التحميل؛ لا جلب ولا حساب متكرّر | `navConfig.ts` |
| زمن بناء الواجهة | ≈2.73 ثانية — ضمن المدى المعتاد للمستودع | مخرجات `npm run build` |
| تمرير أفقيّ / قصّ التخطيط | **صفر** على المقاسات الثلاثة (1440×900 · 820×1180 · 390×844)؛ و`scrollWidth ≤ clientWidth+1` لشريط الأقسام | `navigation.spec.ts` |
| زمن استجابة الملاحة | كلّ انتقال داخليّ (`Link` + `Navigate`) بلا إعادة تحميل الصفحة | `navigation.spec.ts` (حفظ المِرساة يُثبِت أنّه تنقّل داخليّ) |

**لم تُقَس ميزانيّة حزم رسميّة ولا Lighthouse** — لا توجد أداة ميزانيّة حزم مُهيَّأة في المستودع، ولم يُصرَّح بإدخال واحدة. مسجَّل في §12.

---

## 10. سلسلة الهجرات وحالتها **المحلّيّة فقط**

| المقياس | القيمة |
|---|---|
| عدد الهجرات في السلسلة | **44** |
| رأس السلسلة (Migration Head) | `20260825111521_P2_HR010_EmployeeChecklistItems` |
| الهجرات المُضافة في المرحلة 3 | **صفر** |
| قاعدة الاختبار المعزولة | `reporting_p2_20260825` (محلّيّة، تُنشأ وتُسقَط داخل الدورة) |
| قاعدة اختبار الهجرات المعزولة | `reporting_p3_mig_20260825` — أُنشئت، اختُبرت، ثمّ **أُسقِطت** |

**اختبار الانعكاسيّة (محلّيّ معزول):** صعود كامل (44) ← نزول إلى `20260824233938_AddAttendanceIncidents` ← إعادة صعود كامل. **الثلاثة نجحت.** ثمّ أُسقِطت القاعدة.

**تأكيد مطلق:** لم تُطبَّق أيّ هجرة على TEST أو RC أو الإنتاج. لم يُنفَّذ أيّ Backfill ولا Seed ولا كتابة على أيّ قاعدة مشتركة. لم تُغيَّر أيّ صلاحيّة مستخدم ولا دور ولا `ManagerId`.

---

## 11. أعلام الميزات وحالتها الافتراضيّة

أربعة أعلام تحت `Phase2:` مرتبطة بـ`Phase2FeatureOptions`. **لا يوجد قسم `Phase2` في `appsettings.json`** ⇒ الافتراضيّ لكلّ منها `false` (مطفأ) في كلّ بيئة ما لم يُضبَط صراحةً.

| العلم | الافتراضيّ | أثر الإطفاء |
|---|---|---|
| `Phase2:Employee360Enabled` | `false` | `EmployeesController` يردّ 404 — إخفاء السطح لا رفض تخويل |
| `Phase2:AttendanceEnabled` | `false` | قسم الحضور يُعلن عدم توفّره صراحةً؛ ومهمّة `AttendanceSlaSweepService` لا تعمل |
| `Phase2:HrOperationsEnabled` | `false` | `HrOperationsController` يُخفي السطح كلّه (404) |
| `Phase2:EmployeeChecklistEnabled` | `false` | نقاط قوائم التحقّق تردّ 404 |

**العَلَم إخفاء ميزة لا تفويض** — هذا مكتوب في الكود نفسه وليس اتّفاقًا شفويًّا. تفعيلها في هذه الدورة كان **داخل مصنع الاختبار المعزول حصرًا** (`Phase2WebApplicationFactory`)، ولم يُفعَّل أيّ علم في أيّ بيئة مشتركة.

**أعلام المرحلة 3:** لا شيء. الملاحة ليست خلف علم — لأنّها لا تُضيف قدرة، بل تُنظّم عرض ما هو موجود ومحروس أصلًا.

---

## 12. القيود المعروفة والأدلّة الناقصة

هذا القسم مقصود أن يكون **غير مريح**. الاعتراف بحدود القياس أنفع من ادّعاء تغطية غير موجودة.

1. **لا دليل UAT ولا دليل بيئة مشتركة.** كلّ ما ورد أعلاه مقيس **محلّيًّا** في شجرة عمل معزولة على جهاز واحد. لم يُلمس TEST ولا RC ولا الإنتاج.
2. **اختبارات E2E تعمل بواجهة برمجيّة مُعترَضة (stub).** نداءات `/api/**` غير `auth/me` تُجاب بـ403 عمدًا: المقصود قياس **هيكل الملاحة** لا صحّة البيانات. أيّ عطب يظهر فقط مع بيانات حقيقيّة **لن تلتقطه** هذه المجموعة.
3. **دور واحد في E2E.** جلسة الـE2E تُحاكي `Admin` بقائمة قدرات فارغة. مصفوفة الأدوار الكاملة مقيسة على مستوى الوحدة (12 دورًا × 6 نطاقات) لا على البناء المحزَّم.
4. **لا قياس أداء رسميّ.** لا Lighthouse ولا ميزانيّة حزم ولا قياس زمن أوّل رسم. §9 يقيس **غياب الانحدار البنيويّ** لا **جودة الأداء المطلقة**.
5. **`lint` عند أساس قائم به 25 خطأ.** لم يُصلَح شيء منها ولم يُضَف شيء إليها. تنظيفها خارج نطاق هذا التصريح ويحتاج تذكرة مستقلّة.
6. **لا اختبار قارئ شاشة حقيقيّ.** الإتاحة مقيسة عبر الأدوار والمعالم المعنونة و`aria-current` — وهذا **شرط لازم غير كافٍ**. التجربة الفعليّة على VoiceOver/NVDA لم تُجرَ.
7. **لا قياس على متصفّحات متعدّدة.** Playwright شُغِّل على المتصفّح المُهيَّأ في المستودع فقط.
8. **`/app/performance` مسار جديد.** هو الإضافة الوحيدة، وقد أُضيف ليكون هدفًا موحَّدًا لثلاثة روابط قديمة (`kpi-evaluations`, `kpi-aggregate`, `kpi-quarterly`). صحّة **محتواه** موروثة من المرحلة 1، وهذه المرحلة تضمن **وصوله** لا **دقّة أرقامه**.
9. **لا اختبار حِمل ولا تزامن.** غير مطلوب في هذه المرحلة وغير مُدَّعى.

---

## 13. نتائج المقارنة الظلّيّة للمرحلة 1 — وضعها الصحيح

**تُذكَر هنا لاكتمال الصورة، ولا يُعاد ادّعاء أنّها UAT.**

بوّابة المقارنة الظلّيّة لـ`P1-KPI-TRUTH` نُفِّذت وأُغلِقت في 25 أغسطس 2026 بنتيجة **`CONDITIONAL_PASS`** — لا `PASS`.

| الحقيقة | القيمة |
|---|---|
| `UNEXPLAINED_DELTA` | **0** |
| التطابق المستقلّ | 100% ضمن هامش `0.01` |
| **سبب الشرطيّة** | لقطة TEST المستعملة فيها **صفر تقييمات KPI** (`NoTestData`) |
| المرشّح المقاس | `545689b` — **لم يُمسّ** |
| موضع الأدلّة | فرع محلّيّ `evidence/p1-kpi-test-shadow-20260825` (`5b004db`) |

**ما يعنيه هذا بدقّة:** المقارنة أثبتت أنّ الحسابين **لا يختلفان على البيانات المتاحة**، لكنّ البيانات المتاحة كانت خالية من الحالة التي يُفترض أن تُختبَر. غياب الفرق على مجموعة فارغة **ليس دليل تكافؤ**. لذا:

- **لا** تُعتبر هذه النتيجة قبولًا وظيفيًّا.
- **لا** تُعتبر بديلًا عن UAT.
- **يجب** إعادة المقارنة على لقطة تحتوي تقييمات KPI فعليّة قبل أيّ ترقية.

هذا الشرط **مفتوح ومنقول كما هو** إلى بوّابة TEST التالية (§14).

---

## 14. خطّة بوّابة TEST التالية — **مُصاغة لا مُنفَّذة**

> ⛔ **لم يُنفَّذ أيّ بند من هذا القسم، ولا يُنفَّذ إلّا بتصريح صريح جديد ومنفصل لكلّ عمليّة على حدة.**

### 14.1 الشروط المسبقة قبل أيّ نشر على TEST

1. تصريح صريح مكتوب من المستخدم يذكر **النشر على TEST** بالاسم.
2. نسخة احتياطيّة ثلاثيّة لقاعدة `reporting_test` قبل أيّ هجرة.
3. تأكيد `EmailNotifications__Mode=Disabled` أو `DryRun` على TEST قبل الإقلاع — العلم القديم `Email__Enabled` **لا يتحكّم** بالقناة الجديدة.
4. بناء الواجهة بـ`VITE_API_BASE_URL=https://test.emarketingacademy.net/api` وإلّا سقط إلى احتياطيّ `localhost:5090` = Network Error.
5. **حلّ الشرط المفتوح من §13:** توفير لقطة تحتوي تقييمات KPI فعليّة، وإعادة المقارنة الظلّيّة عليها.

### 14.2 التسلسل المقترح

| الخطوة | المحتوى | معيار النجاح |
|---|---|---|
| 1 | نسخ احتياطيّ ثلاثيّ | ثلاث نسخ مُتحقَّق من أحجامها |
| 2 | إعادة إنشاء `reporting_test` **نظيفة** | قاعدة `reporting_test` الحاليّة ملوَّثة (23 فشلًا و~1س48د مقابل 2 و~4.5د على نظيفة) ⇒ **لا يُقاس عليها مرشَّح** |
| 3 | تطبيق سلسلة الهجرات الـ44 | صعود نظيف بلا خطأ |
| 4 | نشر الخلفيّة ثمّ الواجهة | `/health` أخضر · بصمة البناء مطابقة |
| 5 | إعادة المقارنة الظلّيّة للمرحلة 1 على بيانات غير فارغة | `UNEXPLAINED_DELTA = 0` مع **عدد تقييمات > 0** |
| 6 | UAT متعدّد الأدوار على الملاحة | الوحدات السبع لكلّ دور · لا رابط يقود إلى 403 |
| 7 | فحص الروابط القديمة الـ18 | كلّها تصل لوجهاتها بالاستعلام والمِرساة |
| 8 | فحص المقاسات الثلاثة على أجهزة حقيقيّة | لا قصّ ولا تمرير أفقيّ |

### 14.3 معايير الرفض الصريحة (أيّ واحد ⇒ إيقاف فوريّ)

- ظهور رابط في القائمة يقود إلى 403 لأيّ دور.
- فقدان أيّ مسار من الـ59.
- alias يفقد استعلامه أو مِرساته.
- أيّ هجرة تفشل أو تحتاج تدخّلًا يدويًّا.
- بقاء `UNEXPLAINED_DELTA` غير صفريّ، **أو** بقاء عدد التقييمات صفرًا مرّة أخرى.

---

## 15. خطّة التراجع (Rollback)

**الوضع الحاليّ يجعل التراجع تافهًا:** كلّ العمل محبوس في فرع محلّيّ داخل شجرة عمل معزولة، ولم يُدفَع ولم يُدمَج ولم يُوسَم.

### 15.1 التراجع المحلّيّ (متاح فورًا · بلا أثر)

| الهدف | الإجراء |
|---|---|
| تجاهل المرحلة 3 كلّها | لا تفعل شيئًا. `origin/develop` و`develop` المحلّيّ على `736b5c5` بلا مساس. |
| إزالة شجرة العمل | `git worktree remove .claude/worktrees/p3-nav-20260825` (بعد تصريح — قد تحتوي عملًا) |
| العودة إلى المرحلة 2 وحدها | الفرع `fa563d8c…` سليم ومستقلّ |
| العودة إلى المرحلة 1 وحدها | الفرع `545689bb…` سليم ومستقلّ |

### 15.2 التراجع بعد نشر مستقبليّ على TEST (لم يحدث بعد)

1. إعادة نشر بصمة البناء السابقة من النسخة المعزولة المحفوظة.
2. **الهجرات إضافيّة بحتة** ⇒ لا حاجة لنزول قسريّ؛ الكود الأقدم يتجاهل الأعمدة الجديدة.
3. إن لزم النزول فعلًا: انعكاسيّة السلسلة مُختبَرة محلّيًّا (§10)، لكنّها **غير مُختبَرة على بيانات حقيقيّة** ⇒ النسخة الاحتياطيّة هي خطّ الدفاع الأوّل لا الهجرة العكسيّة.
4. استعادة النسخة الاحتياطيّة الثلاثيّة عند أيّ شكّ في سلامة البيانات.

### 15.3 نقطة اللاعودة

**لا توجد نقطة لاعودة في هذا المرشّح.** أوّل نقطة لاعودة محتملة هي **الهجرة على قاعدة مشتركة**، وهي لم تحدث ولا يجوز أن تحدث بلا تصريح صريح منفصل.

---

## 16. البصمة الكاملة النهائيّة (Final Full SHA)

```
41954ccff768b56e198af23b51648dbdb1da05c9
```

| الحقل | القيمة |
|---|---|
| الفرع | `feature/p3-navigation-final-local-candidate-20260825` |
| بصمة الشجرة (Tree) | `cd27989840774b1e54b7cc1e483de600c34cda2a` |
| البصمة الأصل (Parent) | `7e324d2` (P3-SEC-005) |
| نقطة البدء المُلزَمة | `fa563d8c8fb85d36f9cf4147a42a939e196c3801` |
| موضع شجرة العمل | `.claude/worktrees/p3-nav-20260825` |

---

## 17. `git status --short` النهائيّ

**في شجرة العمل المعزولة** (بعد إضافة هذا التقرير ستُسجَّل دفعة `docs` نهائيّة):

```
$ git status --short
(فارغ — لا شيء غير مُتتبَّع ولا شيء غير مُسجَّل)
```

**في الشجرة الرئيسيّة:**

```
$ git status --short
 M CLAUDE.md
?? Ops/R21/RC-CANDIDATE-BUILD-AND-REHEARSAL-REPORT-20260823.md
```

---

## 18. إثبات عدم لمس الشجرة الرئيسيّة وتغييرات المستخدم

| البند | الحالة المطلوبة | الحالة المقيسة | النتيجة |
|---|---|---|---|
| فرع الشجرة الرئيسيّة | `develop` بلا تبديل | `develop` | ✅ |
| رأس الشجرة الرئيسيّة | `736b5c5…` بلا تحريك | `736b5c567b0dde2511dd91ac8fcb1c9cd466b951` | ✅ |
| `M CLAUDE.md` | يبقى **معدَّلًا وغير مُسجَّل** كما تركه المستخدم | ` M CLAUDE.md` | ✅ **لم يُلمَس** |
| `?? Ops/R21/RC-CANDIDATE-BUILD-AND-REHEARSAL-REPORT-20260823.md` | يبقى **غير مُتتبَّع** كما تركه المستخدم | `?? …` | ✅ **لم يُلمَس** |
| ملفّات أخرى في الشجرة الرئيسيّة | بلا تغيير | لا شيء إضافيّ في `git status --short` | ✅ |

**كيف تحقّق ذلك بنيويًّا لا بالحذر:** كلّ العمل جرى داخل `git worktree` منفصلة لها فهرسها ورأسها المستقلّان. الشجرة الرئيسيّة لم تُستدعَ فيها أيّ عمليّة كتابة أصلًا — فالحماية **معماريّة** لا سلوكيّة.

---

## 19. إثبات عدم وجود Push / Merge / Tag / Deploy

| العمليّة | الحالة | الدليل |
|---|---|---|
| `git push` | **لم تُنفَّذ** | الفرع محلّيّ بحت بلا فرع تتبّع بعيد؛ `origin/develop` باقٍ على `736b5c5` |
| `git merge` | **لم تُنفَّذ** | `develop` المحلّيّ لم يتحرّك عن `736b5c5` |
| `git tag` | **لم يُنشأ** | آخر الوسوم يبقى `rc-p360-wf-r2-20260817` — لا وسم بتاريخ 25 أغسطس |
| نشر على TEST | **لم يُنفَّذ** | لا اتّصال `ssh` تنفيذيّ ولا `rsync` ولا إعادة تشغيل خدمة |
| نشر على RC | **لم يُنفَّذ** | `khubara-reporting-rc` لم تُلمَس |
| نشر على الإنتاج | **لم يُنفَّذ** | `reporting-api` لم تُلمَس؛ الإنتاج باقٍ على `7e063b4` |
| هجرة على قاعدة مشتركة/حيّة | **لم تُنفَّذ** | الهجرات جرت على `reporting_p3_mig_20260825` المعزولة ثمّ أُسقِطت |
| Backfill / Seed | **لم يُنفَّذ** | لا سكربت بيانات شُغِّل خارج مصانع الاختبار |
| تغيير مستخدمين/أدوار/`ManagerId` | **لم يُنفَّذ** | مستخدمو الاختبار أُنشئوا داخل القاعدة المعزولة حصرًا |
| تفعيل أعلام ميزات خارج الاختبار | **لم يُنفَّذ** | الأعلام الأربعة تبقى `false` افتراضيًّا (§11) |

---

## البيان الرسميّ (Manifest)

```yaml
manifest_version: 1
report_id: P3-REL-007
generated_at_utc: "2026-08-25T15:18:32Z"
status: LOCAL_ONLY

candidate:
  final_sha: "41954ccff768b56e198af23b51648dbdb1da05c9"
  parent_sha: "7e324d2"
  tree_sha: "cd27989840774b1e54b7cc1e483de600c34cda2a"
  branch: "feature/p3-navigation-final-local-candidate-20260825"
  worktree: ".claude/worktrees/p3-nav-20260825"

ancestry:
  baseline: "736b5c567b0dde2511dd91ac8fcb1c9cd466b951"
  phase1_kpi_truth: "545689bb"
  phase2_employee360_hrops: "fa563d8c8fb85d36f9cf4147a42a939e196c3801"
  phase3_navigation: "41954ccff768b56e198af23b51648dbdb1da05c9"
  linear_verified: true

migrations:
  head: "20260825111521_P2_HR010_EmployeeChecklistItems"
  total_count: 44
  added_in_phase3: 0
  reversibility_tested_locally: true
  applied_to_shared_or_live_db: false

tests:
  backend_build: { errors: 0, warnings: 4 }
  backend_unit: { passed: 548, failed: 0 }
  backend_integration: { passed: 124, failed: 0, baseline: 109 }
  frontend_typecheck: clean
  frontend_unit_vitest: { passed: 734, failed: 0, files: 61, baseline: 672 }
  frontend_build: success
  e2e_playwright: { passed: 42, failed: 0, baseline: 34 }
  lint: { problems: 42, errors: 25, delta_from_baseline: 0 }

navigation:
  modules: 7
  items: 48
  match_paths: 7
  aliases: 18
  alias_bearing_items: 14

routes:
  count_before: 58
  count_after: 59
  preserved: 58
  removed: 0
  added: 1
  unexplained_delta_count: 0

security:
  invariant: "visible_roles SUBSET_OF guard_admitted_roles"
  menu_leaks: 0
  unguarded_app_routes: 0
  aliases_with_independent_guard: 0
  capability_gated_items_visible_without_capability: 0
  items_visible_without_session: 0
  defects_found_and_fixed: 1

feature_flags:
  Phase2__Employee360Enabled: false
  Phase2__AttendanceEnabled: false
  Phase2__HrOperationsEnabled: false
  Phase2__EmployeeChecklistEnabled: false
  phase3_flags: none
  enabled_outside_isolated_tests: false

governance:
  pushed: false
  merged: false
  tagged: false
  deployed_test: false
  deployed_rc: false
  deployed_production: false
  shared_db_written: false
  main_worktree_touched: false
  user_changes_preserved: ["M CLAUDE.md", "?? Ops/R21/RC-CANDIDATE-BUILD-AND-REHEARSAL-REPORT-20260823.md"]

open_conditions:
  - id: P1-KPI-SHADOW-NO-TEST-DATA
    status: OPEN
    detail: "المقارنة الظلّيّة للمرحلة 1 أُغلِقت بـCONDITIONAL_PASS لأنّ لقطة TEST خالية من تقييمات KPI. يجب إعادتها على بيانات غير فارغة قبل أيّ ترقية."
```

### بيان الملفّات المتغيّرة (بصمات Git للكائنات)

```
9a39dfd3603b  reporting-backend/src/Reporting.Application/Auth/AuthModels.cs
979c3999508f  reporting-backend/src/Reporting.Infrastructure/Services/AuthService.cs
5890378e68cb  reporting-backend/tests/Reporting.IntegrationTests/Phase3CapabilityReflectionTests.cs
5649b084f4d5  reporting-frontend/e2e/navigation.spec.ts
b74ef8e67572  reporting-frontend/src/App.tsx
5ebd2cc87b7b  reporting-frontend/src/components/AliasRedirect.tsx
b208a45f47b9  reporting-frontend/src/components/Breadcrumbs.tsx
65cadca847df  reporting-frontend/src/components/DashboardShell.execution.nav.test.tsx
15271f801c7c  reporting-frontend/src/components/DashboardShell.nav.test.tsx
33e2d65c9fcf  reporting-frontend/src/components/DashboardShell.p3.test.tsx
927b3da87a4f  reporting-frontend/src/components/DashboardShell.portfolio.nav.test.tsx
6168ba0c1c22  reporting-frontend/src/components/DashboardShell.tsx
5d1f0ed3a2a4  reporting-frontend/src/components/HeaderActions.tsx
19ee24d10709  reporting-frontend/src/components/NavOverflow.test.tsx
5fd3658b563c  reporting-frontend/src/components/NavOverflow.tsx
e4a88dff997f  reporting-frontend/src/components/ProtectedRoute.test.tsx
4f852f39e304  reporting-frontend/src/lib/auth.tsx
6d2d8d32359f  reporting-frontend/src/lib/navConfig.test.ts
c0d87c188b08  reporting-frontend/src/lib/navConfig.ts
4164d84158a8  reporting-frontend/src/lib/useNavCtx.ts
9f255159892e  reporting-frontend/src/navSecurity.test.ts
bd7426f8df61  reporting-frontend/src/routeRegistry.test.ts
5de77be2b097  reporting-frontend/src/types/api.ts
```

---

## الخلاصة

المرحلة 3 **مكتملة محلّيًّا** على مرشّح واحد غير قابل للتغيير يحتوي المراحل الثلاث. سبع وحدات ثابتة، 48 قسمًا، 18 رابطًا قديمًا محفوظًا، 59 مسارًا بلا فقدان واحد، وصفر تسريب عرض عبر 48×12×6 تركيبة. الاختبارات خضراء بالكامل عند 548 + 124 + 734 + 42، والانحدار الأسلوبيّ الوحيد هو **بقاء** أساس قائم لا زيادة عليه.

**ما لم يحدث ولا يجوز أن يحدث بلا تصريح صريح منفصل:** أيّ دفع، دمج، وسم، أو نشر على أيّ بيئة؛ وأيّ هجرة أو كتابة على أيّ قاعدة مشتركة أو حيّة.

**الشرط المفتوح الوحيد المنقول إلى البوّابة التالية:** إعادة المقارنة الظلّيّة للمرحلة 1 على لقطة تحتوي تقييمات KPI فعليّة (§13).
