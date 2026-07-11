# CURRENT_STATE_INVENTORY

### جرد فِعليّ قائم على الأدلّة للمستودع الحالي — للتحكّم في مراحل التنفيذ اللاحقة

> **المنهج:** تحقيق **للقراءة فقط** على شجرة العمل الفعلية لفرع `develop`. تاريخ الجرد: **2026-07-11**. كل رقم/عنصر مصحوب بمساره أو أمره.
>
> **تحذير حاكم (يجب قراءته):** كل ما في هذا المستند = **حقيقة مستودع (`develop`)**، وليس حالة الإنتاج. `develop` **ليس** مطابقًا للإنتاج (دليل مثبت: يحتوي هجرة `20260620001156_FlexiblePositionsPhase1A` التي تنصّ سجلّات النشر أنها **لم تُنشَر**). **لا يجوز الاستدلال بأي بند هنا على أنه «منشور على الإنتاج» دون تحقّق خادميّ مستقلّ.** حقل «حالة النشر» أدناه يظلّ **UNVERIFIED من المستودع** لكل بند.

---

## 0. لقطة عليا (Evidence Snapshot)

| البند | القيمة المُتحقَّقة | الأمر |
|------|-------------------|-------|
| الفرع | `develop` | `git rev-parse --abbrev-ref HEAD` |
| إجمالي تغيّرات git | 90 (25 معدّل · 3 محذوف · 62 غير مُتتبَّع) | `git status --porcelain \| wc -l` + grep `^ M`/`^ D`/`^??` |
| آخر commit | `6fd2253 RC-4 Sales Module baseline` | `git log --oneline -1` |
| طبقات الـ backend | 4 (Api / Application / Domain / Infrastructure) | `ls src` |
| Controllers | 42 ملفًّا (41 فعليّ + `ApiControllerBase`) | `ls Controllers/*.cs \| wc -l` |
| خدمات Infrastructure | 48 | `ls Services/ \| wc -l` |
| هجرات EF | 30 (27 مُتتبَّعة + 3 غير مُتتبَّعة) | `ls Migrations/*.cs` بلا Designer/Snapshot |
| كيانات Domain | 54 ملفًّا في 15 مجلدًا | `find Domain/Entities -name '*.cs' \| wc -l` |
| صفحات الواجهة (غير اختبار) | 57 | `ls pages/*.tsx \| grep -v .test.tsx \| wc -l` |
| ملفات اختبار تكامل | 102 | `ls IntegrationTests/*.cs \| wc -l` |
| ملفات اختبار وحدة | 6 | `ls UnitTests/*.cs \| wc -l` |
| ملفات اختبار واجهة | 28 | `find src -name '*.test.ts*' \| wc -l` |
| سمات `[Fact]/[Theory]` (ساكنة) | 1227 تكامل · 37 وحدة | `grep -rho ... --include='*.cs' \| wc -l` |
| سياسات التخويل | 28 | `grep -c 'AddPolicy(' Program.cs` |
| محرّك سير عمل / CRM / Task / CSP | **غائبة كلها (grep=0)** | grep على الأسماء |
| SignalR / ScopeResolver | **موجودان** | فحص الملفات |

**مفتاح «النطاق»:** `[حالي]` = نطاق المنصّة الحالية المعتمَدة (تقارير/موظف/حوكمة أساسية/عملاء/مشاريع/مبيعات) · `[قيد تطوير]` = عمل غير مُودَع على `develop` · `[رؤية]` = قدرة مؤسسية مستقبلية في BRD.

---

## 1. الفرع وحالة Git والعمل غير المُودَع

| البند | الحالة | الدليل | الاكتمال | حدود معروفة | النطاق |
|------|-------|--------|:---:|-------------|:---:|
| الفرع الحالي | `develop` | `git rev-parse` | — | ليس فرع الإنتاج | — |
| عمل مُعدَّل غير مُودَع | 25 ملفًّا | `git status \| grep '^ M'` | — | خطر فقدان عمل | [حالي]+[قيد تطوير] |
| ملفات محذوفة غير مُودَعة | 3 (ErdsPhase55/Phase5/Phase6 tests) | `git status \| grep '^ D'` | — | حذف اختبارات دون commit | [حالي] |
| ملفات غير مُتتبَّعة | 62 | `git status \| grep '^??'` | — | تشمل وحدتَي التقارير الجديدة أدناه | [قيد تطوير] |

**العمل غير المُودَع الجوهريّ (وحدات جديدة على `develop`):**
- **Execution Taxonomy** — Controllers `ExecutionTaxonomyController.cs` + `ExecutionTaxonomyOptionsController.cs`، كيان `ExecutionTaxonomy/ExecutionTaxonomyValue.cs`، خدمة `ExecutionTaxonomyService.cs`، هجرة `20260708232456_AddExecutionTaxonomyCatalog`.
- **Project Workstreams** — Controller `ProjectWorkstreamsController.cs`، كيان `Clients/ProjectWorkstream.cs`، خدمة `ProjectWorkstreamService.cs`، هجرة `20260709222126_AddProjectWorkstreams`.
- **Workstream Deliverables** — Controller `WorkstreamDeliverablesController.cs`، كيان `Clients/WorkstreamDeliverable.cs`، خدمة `WorkstreamDeliverableService.cs`، هجرة `20260709231845_AddWorkstreamDeliverables`.
- **Project-First Execution Aggregation** — Controller `ProjectFirstExecutionAggregationController.cs` + خدمة + نماذج.
- **صفحات واجهة تنفيذ** — `TeamLeaderExecutionPage`, `TeamLeaderProjectExecutionPage`, `ExecutionTaxonomyManagementPage`, مكوّنات `Collapsible/Tabs/StickyBar/ShowMore/HeaderActions` + `navConfig.ts` + hooks تنفيذ.

> **مخاطرة تشغيلية R2:** 62 ملفًّا غير مُتتبَّع + 3 هجرات غير مُودَعة = خطر فقدان عمل عالٍ. **يُوصى بإيداعها في فرع مراجعة فورًا** (القرار AD-10). *(توصية — ليست أمرًا تنفيذيًّا الآن.)*

---

## 2. مشاريع الـ Backend وطبقاته

| الطبقة | المسار | الدور | ملاحظات | النطاق |
|-------|-------|------|---------|:---:|
| `Reporting.Api` | `reporting-backend/src/Reporting.Api` | Controllers + Program + Realtime (SignalR) + Middleware | 41 Controller فعليّ + `ApiControllerBase` | [حالي] |
| `Reporting.Application` | `.../Reporting.Application` | العقود، DTOs، الأدوار/السياسات، النماذج | لا منطق بنية تحتية | [حالي] |
| `Reporting.Domain` | `.../Reporting.Domain` | الكيانات + Enums | 54 كيانًا، 15 مجلدًا | [حالي] |
| `Reporting.Infrastructure` | `.../Reporting.Infrastructure` | EF Core + Persistence + Services + Migrations | 48 خدمة، 30 هجرة | [حالي] |

**الاكتمال:** بنية Clean Architecture سليمة ومطبّقة (فصل الطبقات واضح). **حد معروف:** لا مشروع اختبارات معماري (ArchUnitNET) يفرض حدود الطبقات — تحسين اختياري (F).

---

## 3. بنية الـ Frontend

| البند | القيمة | الدليل | النطاق |
|------|-------|--------|:---:|
| الإطار | React 18 + TS + Vite + TanStack Query + Tailwind, RTL | `package.json`/بنية `src` | [حالي] |
| الصفحات | 57 صفحة (غير اختبار) | `ls pages/*.tsx` | [حالي] |
| التوجيه | `App.tsx` (4 `<Route>` جذرية + توجيه فرعيّ عبر `navConfig.ts`) | grep | [حالي] |
| مكوّنات مشتركة | `Collapsible, Tabs, StickyBar, ShowMore, HeaderActions, DashboardShell` (بعضها غير مُودَع) | `ls components` | [حالي]+[قيد تطوير] |
| الطبقة المساعدة | `lib/` (auth, format, use*.ts hooks لكل مجال) | `ls lib` | [حالي] |

**حد معروف:** التوجيه ليس ملفًّا مركزيًّا واحدًا (يعتمد `navConfig.ts` غير المُودَع بعد) — يجب إيداعه لتثبيت خريطة المسارات.

---

## 4. الوحدات الحالية (Modules)

| الوحدة | الحالة | الدليل (Controller/Service/Entity) | الاكتمال | حدود معروفة | النطاق |
|-------|-------|-----------------------------------|:---:|-------------|:---:|
| المصادقة/الهوية | مكتملة | `AuthController`, `AuthService`, `TokenService` | عالٍ | — | [حالي] |
| الدليل التنظيمي | مكتملة | `DirectoryController`, `DirectoryService`, `Org/*` | عالٍ | — | [حالي] |
| قوالب التقارير | مكتملة | `ReportTemplatesController`, `ReportTemplateService`, `Templates/*` | عالٍ | — | [حالي] |
| التسليمات + الاعتماد | مكتملة | `SubmissionsController`, `SubmissionService`, `Submissions/*` | عالٍ | سلاسل اعتماد صلبة (لا محرّك) | [حالي] |
| التقارير/التجميع | مكتملة | `ReportsController`, `ReportingAggregationController`, `ReportingService` | عالٍ | تعمل على OLTP مباشرةً | [حالي] |
| KPI (قوالب/تقييم/إسناد/تصدير مالي) | مكتملة | `KpiTemplatesController`, `KpiEvaluationsController`, `Kpi/*` | عالٍ | — | [حالي] |
| لوحات المعلومات | مكتملة | `DashboardController`, `ExecutiveDashboardController` | متوسط–عالٍ | لا Read Models | [حالي] |
| الحوكمة (Items/Actions/Escalations/Notes/Risk/Decision) | مُنفَّذة جزئيًّا | `Governance*Controller`, `Governance/*` (10 كيانات) | متوسط | روابط الدورة (Risk→Decision→CAPA) ناقصة | [حالي] |
| الإجازات/خدمات الموظف/الأرصدة | مكتملة | `LeaveRequestsController`, `BalancesController`, `EmployeeServiceRequestsController` | عالٍ | — | [حالي] |
| المالية (عرض تأثير الرواتب) | عرض فقط | `PayrollController`, `PayrollImpactService`, `Payroll/PayrollImpactReview` | متوسط | لا حساب/صرف مستحقات (مقصود) | [حالي] |
| المناصب المرنة | مُنفَّذة | `PositionsController`, `PositionService`, `Positions/*` | متوسط | حالتها الإنتاجية غير مؤكّدة | [حالي]/UNVERIFIED نشرًا |
| العملاء/المشاريع | مُنفَّذة | `ClientsController`, `ProjectsController`, `Clients/{Client,Project}` | متوسط–عالٍ | إتمام دورة المشروع لاحقًا | [حالي] |
| بوابة العميل/محفظة الحسابات | مُنفَّذة | `AccountPortfolioController`, `AccountPortfolioService` | متوسط | — | [حالي] |
| الدورات/الخدمات (كتالوج) | مُنفَّذة | `CoursesController`, `ServicesController`, `Course.cs`, `Service.cs` | متوسط | — | [حالي] |
| الإشعارات (SignalR) | مكتملة | `NotificationsController`, `NotificationHub`, `SignalRNotificationPusher` | عالٍ | — | [حالي] |
| البريد (Outbox/Control/Notifications) | مكتملة | `EmailControlController`, `EmailNotificationsController`, `EmailOutboxDispatcher`, `MailKitEmailSender` | عالٍ | مُعطَّل/allow-list إنتاجيًّا (UNVERIFIED) | [حالي] |
| منح رؤية التقارير | مُنفَّذة | `ReportViewGrantsController`, `ReportViewGrantService` | متوسط | — | [حالي] |
| تذكيرات/تقويم التقارير | مُنفَّذة | `ReportRemindersController`, `ReportCalendarController`, `ReportDueService`, `SubmissionReminderService` | متوسط | — | [حالي] |
| التدقيق | مكتملة | `AuditController`, `AuditService`, `System/AuditLog` | عالٍ | — | [حالي] |
| التطوير (خطط/احتياجات تدريب) | مُنفَّذة | `DevelopmentController`, `DevelopmentService`, `Development/*` | متوسط | — | [حالي] |
| **Execution Taxonomy / Workstreams / Deliverables / Project-First Aggregation** | **قيد تطوير (غير مُودَعة)** | Controllers + Services + Entities + 3 هجرات غير مُتتبَّعة | جزئيّ | لم تُودَع/تُراجَع بعد | **[قيد تطوير]** |
| **محرّك سير عمل قابل للتهيئة** | **غائبة** | grep=0 | — | لا محرّك؛ سلاسل صلبة | **[رؤية]** |
| **CRM B2B (Lead→Contract)** | **غائبة** | grep=0 | — | — | **[رؤية]** |
| **Task/Execution (Task/WorkLog/Revision)** | **غائبة** | grep=0 | — | WorkstreamDeliverable ≠ Task | **[رؤية]** |
| **Metrics Catalog / Decision Center / Alerts / Insight** | **غائبة** | grep=0 | — | — | **[رؤية]** |
| **File Service مُصنَّف/مُصدَّر** | **غائبة** | لا كيان File مُصنَّف | — | تخزين ملفات محليّ فقط | **[رؤية]** |
| **AI** | **غائبة** | — | — | مؤجّل صراحةً (Ch14.22) | **[مؤجّل]** |

---

## 5. كيانات Domain الحالية (54 كيانًا / 15 مجلدًا)

| المجلد | الكيانات | النطاق |
|-------|---------|:---:|
| Clients | Client, Project, **ProjectWorkstream\***, **WorkstreamDeliverable\*** | [حالي]+[قيد تطوير] |
| Courses | Course | [حالي] |
| Development | ImprovementPlan, TrainingNeed | [حالي] |
| EmployeeServices | BalancePolicy, EmployeeBalanceLedger, EmployeeServiceRequest, EmployeeServiceRequestEvent | [حالي] |
| ExecutionTaxonomy | **ExecutionTaxonomyValue\*** | [قيد تطوير] |
| Governance | Decision, Escalation, GovernanceActionItem(+Update), GovernanceEscalation(+Update), GovernanceItem(+Update), ManagementNote, Risk | [حالي] |
| Kpi | KpiEvaluation, KpiMetric, KpiResult, KpiTemplate, KpiTemplateAssignment, KpiTemplateVersion | [حالي] |
| Leave | LeaveRequest, LeaveRequestEvent | [حالي] |
| Org | Department, JobRole, Team, UserTeamMembership | [حالي] |
| Payroll | PayrollImpactReview | [حالي] |
| Positions | Position, PositionPermission, PositionScope, UserPosition | [حالي] |
| Services | Service | [حالي] |
| Submissions | ApprovalStep, ReportSubmission, ReportViewGrant, SubmissionFieldValue | [حالي] |
| System | AuditLog, EmailNotification, EmailOutbox, EmailRule, EmailTemplate, Notification | [حالي] |
| Templates | ReportTemplate, ReportTemplateAssignment, ReportTemplateVersion, TemplateField | [حالي] |

`*` = غير مُودَع بعد (قيد تطوير). **حد معروف:** لا كيانات `Lead/Opportunity/Proposal/Contract/Company` (CRM)، ولا `Task/WorkLog/Revision/PlanVersion` (تنفيذ)، ولا `Metric/Dashboard/Widget/Dataset/Alert` ككيانات كتالوج — كلها [رؤية].

---

## 6. الخدمات الحالية (48 خدمة)

مصنّفة وظيفيًّا (كلها في `Reporting.Infrastructure/Services`):
- **هوية/تخويل/نطاق:** `AuthService`, `TokenService`, `CurrentUser`, `ScopeResolver`, `ClientProjectAccess`.
- **دليل/تنظيم:** `DirectoryService`, `GovernanceDirectoryService`.
- **قوالب/تسليم/تقارير:** `ReportTemplateService`, `SubmissionService`, `ReportingService`, `ReportingAggregationService`, `PodExecutionAggregationService`, `ProjectFirstExecutionAggregationService*`, `PdfReportBuilder`, `ReportViewGrantService`, `ReportCalendarService`, `ReportDueService`, `ReportReminderService`, `SubmissionReminderService`.
- **KPI:** `KpiTemplateService`, `KpiEvaluationService`.
- **لوحات:** `DashboardService`, `ExecutiveDashboardService`.
- **حوكمة:** `GovernanceService`, `GovernanceItemService`, `GovernanceActionItemService`, `GovernanceEscalationService`, `ManagementNoteService`.
- **موظف/إجازات/مالية:** `LeaveRequestService`, `BalanceService`, `EmployeeServiceRequestService`, `PayrollImpactService`.
- **عملاء/مشاريع/تنفيذ:** `ClientService`, `ProjectService`, `AccountPortfolioService`, `ProjectWorkstreamService*`, `WorkstreamDeliverableService*`, `ExecutionTaxonomyService*`.
- **كتالوج:** `CourseService`, `ServiceCatalogService`.
- **إشعارات/بريد:** `NotificationService`, `EmailOutboxDispatcher`, `MailKitEmailSender`, `EmailControlService`, `EmailNotificationService`.
- **تدقيق/تطوير:** `AuditService`, `DevelopmentService`.

`*` = غير مُودَع. **حد معروف:** منطق القرار/الاعتماد مكتوب داخل خدمات المجال (لا خدمة سير عمل موحّدة) — دَين مُسمّى في BRD Ch14.14 (تُعالَج في P3 كنواة خفيفة).

---

## 7. الـ Controllers ونقاط النهاية (42 ملفًّا)

41 Controller فعليّ + `ApiControllerBase` (صنف أساس). القائمة الكاملة:

`AccountPortfolio, AdminCourses, AdminServices, Audit, Auth, Balances, Clients, Courses, Dashboard, Development, EmailControl, EmailNotifications, EmployeeServiceRequests, ExecutionTaxonomy*, ExecutionTaxonomyOptions*, ExecutiveDashboard, GovernanceActionItems, Governance, GovernanceEscalations, GovernanceItems, KpiEvaluations, KpiTemplates, LeaveRequests, ManagementNotes, Notifications, Payroll, PodExecutionAggregation, Positions, ProjectFirstExecutionAggregation*, ProjectWorkstreams*, Projects, ReportCalendar, ReportReminders, ReportTemplates, ReportViewGrants, ReportingAggregation, Reports, Services, Submissions, WorkstreamDeliverables*`.

`*` = غير مُودَع (قيد تطوير). **الاكتمال:** كل Controller محميّ بـ `[Authorize]` + سياسة (28 سياسة). **حد معروف:** لا API versioning (كل النقاط تحت `/api/*` بلا `/v1`) — تحسين موصى (AD-11).

---

## 8. صفحات/مسارات الواجهة (57 صفحة)

مجموعات وظيفية بارزة (عيّنة تمثيلية، ليست حصرًا):
- **مبيعات/تنفيذ (RC-4):** `SalesAggregationPage`, `SalesRepDashboardPage`, `TeamLeaderSalesDashboardPage`, `AccountPortfolio{,Client,Project}Page`, `TeamLeaderExecutionPage*`, `TeamLeaderProjectExecutionPage*`, `ExecutionTaxonomyManagementPage*`.
- **تقارير/قوالب/تسليم:** صفحات القوالب، التسليمات، التجميع، التقويم، التذكيرات، منح الرؤية.
- **موظف/إجازات/مالية:** الأرصدة، إدارة الأرصدة، طلبات HR، الإجازات، تأثير الرواتب، تصدير KPI للمالية.
- **حوكمة/إدارة:** عناصر الحوكمة، الإجراءات، التصعيدات، الملاحظات، المسمّيات الوظيفية، المناصب، المستخدمون.

`*` = صفحات غير مُودَعة (قيد تطوير). **حد معروف:** بعض الصفحات تعتمد `navConfig.ts` غير المُودَع.

---

## 9. الهجرات الحالية (30 هجرة)

**27 مُتتبَّعة** (من `20260609142107_InitialIdentity` إلى `20260706230935_AddServiceCatalog`) + **الهجرة الـ«Snapshot»** `20260622180127_LeaveBalanceGuardSnapshot` (سبب تصحيح العدّ من 29 إلى 30) + **3 غير مُتتبَّعة**:
- `20260708232456_AddExecutionTaxonomyCatalog`
- `20260709222126_AddProjectWorkstreams`
- `20260709231845_AddWorkstreamDeliverables`

**الاكتمال:** كل الهجرات المفحوصة **إضافية** (CREATE/ADD)، متوافقة مع قاعدة «لا DROP DATABASE». **حد معروف:** آخر هجرة إنتاجية مؤكّدة **غير معروفة من المستودع** — `develop` متقدّم على الإنتاج بعدّة هجرات (منها FlexiblePositions غير المنشورة). **أيّ نشر يتطلّب تحقّق سلسلة الهجرات على الخادم أولًا.**

---

## 10. سياسات التخويل ومنطق النطاق

| البند | الحالة | الدليل | الاكتمال | حدود معروفة | النطاق |
|------|-------|--------|:---:|-------------|:---:|
| سياسات RBAC | 28 سياسة | `grep 'AddPolicy(' Program.cs` | عالٍ | موزّعة على الخدمات | [حالي] |
| حلّ النطاق الموحّد | `ScopeResolver.cs` | فحص ملف | متوسط–عالٍ | مصدر واحد للنطاق لكن الحرّاس موزّعون | [حالي] |
| Resource-Based Auth | مُطبَّق (منع IDOR) | `ClientProjectAccess`, حرّاس الخدمات | عالٍ | لا خدمة صلاحيات موحّدة (دَين Ch14.14) | [حالي]/[رؤية للتوحيد] |
| الأدوار | Admin, CEO, GM, Manager, TeamLeader, Employee, CeoSupport, Viewer, HR + Granular | `Roles.cs` | عالٍ | — | [حالي] |

**حد معروف (R6):** انتشار حرّاس التخويل عبر الخدمات يرفع سطح الخطأ الأمني ⇒ يجب **تعزيز تغطية اختبار النطاق الآن**، وتأجيل توحيد Permission Service (AD-09).

---

## 11. سير العمل الحالي (Workflows)

| المسار | الحالة | الدليل | الاكتمال | حد معروف |
|-------|-------|--------|:---:|---------|
| اعتماد التقارير | صلب مكتوب يدويًّا | `SubmissionService` (Draft→Submitted→Returned→Approved→Escalated→Closed) | عالٍ للحالة الحالية | لا تهيئة ديناميكية |
| اعتماد الإجازات/الاستئذان | صلب مكتوب يدويًّا | `LeaveRequestService` (TeamLeader→Manager→HR + skip/routing) | عالٍ | منطق مكرَّر عبر الخدمات |
| تصعيد الحوكمة | جزئيّ | `GovernanceEscalationService` | متوسط | روابط الدورة ناقصة |

**الحقيقة الحاسمة:** **لا محرّك سير عمل قابل للتهيئة** (grep على WorkflowDefinition/Instance/Engine/Stage/Transition = 0). كل المسارات **سلاسل حالة صلبة كافية للحالات الحالية**. الترقية إلى **نواة خفيفة مشتركة (Option B)** مقرّرة في **P3** (BRD ADR-004) — ليست عيبًا حاليًّا.

---

## 12. التقارير ولوحات المعلومات

| البند | الحالة | الدليل | الاكتمال | حد معروف | النطاق |
|------|-------|--------|:---:|---------|:---:|
| تجميع التقارير | مكتمل | `ReportingAggregationService` (Approved + Same Period) | عالٍ | على OLTP مباشرةً | [حالي] |
| لوحة تنفيذية | مكتملة | `ExecutiveDashboardService` | متوسط–عالٍ | لا Read Models | [حالي] |
| تجميع مبيعات/تنفيذ (RC-4) | مُنفَّذ + [قيد تطوير] | `PodExecutionAggregationService`, `ProjectFirstExecutionAggregationService*` | متوسط | جزء منه غير مُودَع | [حالي]+[قيد تطوير] |
| بناء PDF | مكتمل | `PdfReportBuilder` | عالٍ | — | [حالي] |
| **Metrics Catalog / Decision Center / Alerts / Insight** | **غائبة** | grep=0 | — | — | **[رؤية] (P8)** |

---

## 13. قدرات الإشعارات والبريد

| البند | الحالة | الدليل | الاكتمال | حد معروف | النطاق |
|------|-------|--------|:---:|---------|:---:|
| إشعارات In-App (SignalR) | مكتملة | `NotificationHub`, `SignalRNotificationPusher`, `/hubs/notifications` | عالٍ | — | [حالي] |
| صندوق بريد صادر (Outbox) | مكتمل | `EmailOutboxDispatcher`, هجرة `EmailOutbox` | عالٍ | مُعطَّل/allow-list إنتاجيًّا (UNVERIFIED) | [حالي] |
| مرسِل SMTP | مكتمل | `MailKitEmailSender` | عالٍ | — | [حالي] |
| مركز تحكّم البريد | مكتمل | `EmailControlService`, هجرة `AddEmailControlCenter` | عالٍ | — | [حالي] |
| قوالب/قواعد البريد | مُنفَّذة | `EmailTemplate`, `EmailRule`, `EmailNotification` | متوسط | — | [حالي] |

متوافق مع **ADR-005 (In-App أساسيّ)** و**ADR-006 (Email عبر Outbox)**.

---

## 14. الاختبارات الموجودة

| البند | القيمة | الدليل | ملاحظة |
|------|-------|--------|-------|
| ملفات اختبار تكامل | 102 | `ls IntegrationTests/*.cs` | xUnit + WebApplicationFactory |
| ملفات اختبار وحدة | 6 | `ls UnitTests/*.cs` | — |
| ملفات اختبار واجهة | 28 | Vitest + RTL | — |
| سمات `[Fact]/[Theory]` (ساكنة) | 1227 تكامل · 37 وحدة | grep | **عدّ ساكن فقط** |
| **نتيجة تشغيل الاختبارات** | **غير مؤكّدة** | لم يُشغَّل `dotnet test` (يكتب لقاعدة `reporting_test` = محظور) | **UNVERIFIED** |

**حد معروف حرج (R8):** اختبارات التكامل تستخدم قاعدة PostgreSQL **دائمة مشتركة** (`reporting_test`) ⇒ تراكم بيانات + تصادم هجرات محتمل. **يُوصى بعزلها (قاعدة عابرة لكل تشغيل)** لاحقًا.

---

## 15. ملفات النشر والتهيئة

| البند | الحالة | الدليل | حد معروف | النطاق |
|------|-------|--------|---------|:---:|
| إعدادات التطبيق | `appsettings.json` + `appsettings.Development.json` | `ls Api/*.json` | لا ملف Production مُودَع (يُحقن عبر env) | [حالي] |
| مجلدات نشر محلية | `publish/`, `publish-p0p1/`, وربما `publish-rc*` | `ls -d publish*` | **مصنوعات بناء لا يجب أن تُتتبَّع** (خطر تلوّث الريبو) | — |
| النشر | يدويّ (RC-based) + `pg_dump` backups | سجلّ commits (RC-2/RC-4) | لا CI/CD، لا RC مطابق مؤتمت | [حالي]/[رؤية للأتمتة] |
| الهجرات وقت الإقلاع | `db.Database.MigrateAsync()` | نمط موثّق | كلها إضافية | [حالي] |

**حد معروف:** وجود `publish-*` كمصنوعات غير مُتتبَّعة داخل الشجرة ⇒ يُوصى بإضافتها إلى `.gitignore` عند الإيداع.

---

## خلاصة الجرد

- **النظام ناضج ومكتمل عمليًّا لنطاقه الحالي** (تقارير + موظف + حوكمة أساسية + عملاء/مشاريع + مبيعات/تنفيذ RC-4).
- **العائق الوحيد من فئة «مطلوب حاليًّا ومفقود» = CSP** (أمنيّ بسيط).
- **أعلى خطر تشغيليّ = العمل غير المُودَع** (62 ملفًّا + 3 هجرات) — لا يعطّل الإنتاج لكنه خطر فقدان عمل.
- **الفجوات الكبرى (Workflow/CRM/Task/BI/Files/AI) كلها [رؤية] مخطّطة في BRD** — ليست عيوبًا في النطاق الحالي، ولا تُصنَّف نواقصَ حاليّة.
- **كل ادّعاءات «حالة الإنتاج» تبقى UNVERIFIED من المستودع** وتتطلّب تحقّقًا خادميًّا مستقلًّا.

*— انتهى الجرد. لم تُعدَّل شيفرة/هجرة/تهيئة، ولم يُكتب إلى قاعدة، ولم يُشغَّل نشر أو اختبار.*
