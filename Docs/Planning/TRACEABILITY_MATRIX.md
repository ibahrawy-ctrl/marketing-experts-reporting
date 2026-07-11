# TRACEABILITY_MATRIX

### مصفوفة التتبّع الكاملة — من متطلّب BRD إلى الدليل البرمجيّ والمرحلة

> **السلسلة:** متطلّب BRD → المجال → Epic → Feature → حالة التنفيذ → دليل الكود → العمل المستقبليّ المطلوب → معايير القبول → تغطية الاختبار → المرحلة المستهدفة.
>
> **الحالات المسموحة فقط:** `Implemented` · `Implemented with gaps` · `Under development` · `Approved next` · `Planned future` · `Deferred` · `Out of scope` · `Requires clarification` · `Recommended only`.
>
> **قاعدة حاكمة:** **لا تُصنَّف قدرات BRD المستقبلية كعيوب في النطاق الحاليّ.** «تغطية الاختبار» تشير إلى وجود ملفات/سمات اختبار ساكنة (`[Fact]/[Theory]`) — **لا نتائج تشغيل مؤكّدة** (لم يُشغَّل `dotnet test`). مصدر الأدلّة: `develop`، 2026-07-11.

---

## المفتاح
- **الحالة**: كما أعلاه.
- **دليل الكود**: مسار/كيان/خدمة مُتحقَّق منه في `develop` (أو grep=0 للغائب).
- **الاختبار**: `موجود (ملفات/سمات)` = يوجد ملف اختبار مرتبط · `غير مؤكّد النتيجة` دائمًا (لم يُشغَّل) · `لا يوجد` = لم يُعثر على اختبار مباشر.

---

## المجال 1 — الهوية والتنظيم والتخويل

| Req (BRD) | Epic | Feature | الحالة | دليل الكود | عمل مستقبليّ | معايير قبول | اختبار | المرحلة |
|-----------|------|---------|:---:|-----------|--------------|-------------|:---:|:---:|
| مصادقة JWT + Refresh | Identity | تسجيل دخول/تدوير | **Implemented** | `AuthService`, `TokenService`, هجرة `RefreshTokens` | — | دخول صحيح يُصدر Access+Refresh؛ تدوير يُبطل القديم | موجود | حاليّ |
| RBAC + سياسات | AuthZ | 28 سياسة | **Implemented** | `Program.cs` (28 `AddPolicy`), `Roles.cs` | — | كل نقطة محميّة بسياسة؛ غير المصرّح 403 | موجود | حاليّ |
| Resource-Based Auth (منع IDOR) | AuthZ | حرّاس الموارد | **Implemented** | `ClientProjectAccess`, حرّاس الخدمات | — | لا وصول لمورد خارج النطاق | موجود | حاليّ |
| نطاق موحّد | AuthZ | ScopeResolver | **Implemented with gaps** | `ScopeResolver.cs` | توحيد Permission Service (AD-103) | النطاق يُحسَب من مصدر واحد | موجود | حاليّ / توحيد مستقبليّ |
| JobRole ≠ Identity Role (ADR-008) | Org | مسمّيات وظيفية | **Implemented** | `Org/JobRole`, `DirectoryService` | — | تعديل المسمّى لا يمسّ الدور | موجود | حاليّ |
| Admin ≠ Business Approver (ADR-010) | AuthZ | فصل الواجبات | **Implemented** | `CurrentApproverId` + سياسات | — | الأدمن لا يعتمد أعمالًا | موجود | حاليّ |
| عضويات فريق متعددة | Org | UserTeamMembership | **Implemented** | `Org/UserTeamMembership`, هجرة `AddUserTeamMemberships` | — | إضافة عضوية لا تمسّ TeamId الأساسي | موجود | حاليّ |
| **Permission Service موحّد** | AuthZ | توحيد الصلاحيات | **Recommended only** | لا خدمة موحّدة (Ch14.14) | AD-103 | — | — | رؤية |

---

## المجال 2 — قوالب التقارير والتسليم والاعتماد

| Req (BRD) | Epic | Feature | الحالة | دليل الكود | عمل مستقبليّ | معايير قبول | اختبار | المرحلة |
|-----------|------|---------|:---:|-----------|--------------|-------------|:---:|:---:|
| قوالب مرتبطة بالوظيفة + إصدار | Templates | Versioning + Assignment | **Implemented** | `Templates/*`, `ReportTemplateService`, هجرة `ReportTemplateAssignments` | — | تعديل القالب لا يغيّر تقريرًا قديمًا | موجود | حاليّ |
| حارس إسناد القالب خادميًّا | Templates | Template Role Guard | **Implemented** | `SubmissionService` (`report.template_not_assigned`) | — | قالب غير مُسنَد ⇒ 403 | موجود | حاليّ |
| دورة حياة التقرير (Draft→…→Closed) | Submissions | آلة الحالة | **Implemented with gaps** | `SubmissionService`, `Submissions/ApprovalStep` | نواة سير عمل خفيفة (AD-102) | كل انتقال حالة صحيح ومُدقَّق | موجود | حاليّ / P3 |
| تفرّد التسليم (User,Template,Period) | Submissions | Uniqueness | **Implemented** | `SubmissionService` + PeriodKey | — | تسليم Primary واحد لكل فترة | موجود | حاليّ |
| منح رؤية التقارير | Submissions | ReportViewGrant | **Implemented** | `ReportViewGrantService`, هجرة `AddReportViewGrants` | — | المنح يوسّع الرؤية بلا اعتماد | موجود | حاليّ |
| تذكيرات/تقويم | Reports | Reminders/Calendar | **Implemented with gaps** | `ReportReminderService`, `ReportCalendarService`, `SubmissionReminderService` | — | تذكير لغير المُسلِّمين ضمن النافذة | موجود | حاليّ |
| آلة حالة المراجعة (6 طبقات) | Submissions | Review layers | **Requires clarification** | حالة واحدة + سجلّ | CL-09 | تعريف صريح للطبقات | جزئيّ | P3 |

---

## المجال 3 — التجميع والتقارير ولوحات المعلومات

| Req (BRD) | Epic | Feature | الحالة | دليل الكود | عمل مستقبليّ | معايير قبول | اختبار | المرحلة |
|-----------|------|---------|:---:|-----------|--------------|-------------|:---:|:---:|
| تجميع (Approved + Same Period) | Reports | Aggregation | **Implemented** | `ReportingAggregationService`, `ReportingService` | — | يجمّع المعتمَد لنفس الفترة فقط | موجود | حاليّ |
| لوحة تنفيذية | Dashboards | Executive Dashboard | **Implemented with gaps** | `ExecutiveDashboardService`, `DashboardService` | Read Models (AD-104) | يعرض حسب النطاق | موجود | حاليّ / P8 |
| تصدير PDF | Reports | PdfReportBuilder | **Implemented** | `PdfReportBuilder` | — | ملف PDF صحيح | جزئيّ | حاليّ |
| **Metrics Catalog + إصدار** | BI | كتالوج المقاييس | **Planned future** | grep=0 | AD-105, CL-13 | — | — | P8 |
| **Decision Center / Insight / Alerts** | BI | ذكاء التقارير | **Planned future** | grep=0 | Ch10 | — | — | P8 |
| Drill-down مُخوَّل بالحبيبة | BI | تنقّل مُخوَّل | **Requires clarification** | جزئيّ عبر النطاق | CL-14 | — | — | P8 |
| تقارير على OLTP → Read Models | BI | أداء التقارير | **Recommended only** | لا Read Model (Ch14.14) | AD-104 | — | — | P8 |

---

## المجال 4 — KPI

| Req (BRD) | Epic | Feature | الحالة | دليل الكود | عمل مستقبليّ | معايير قبول | اختبار | المرحلة |
|-----------|------|---------|:---:|-----------|--------------|-------------|:---:|:---:|
| قوالب KPI + إصدار + مقاييس | KPI | Templates/Versions/Metrics | **Implemented** | `Kpi/{KpiTemplate,KpiTemplateVersion,KpiMetric}` | — | تعديل القالب لا يمسّ تقييمًا قائمًا | موجود | حاليّ |
| تقييم (Weekly/Quarterly) + احتساب | KPI | Evaluation/ComputeScore | **Implemented** | `KpiEvaluationService`, `Kpi/{KpiEvaluation,KpiResult}` | — | الاحتساب عند الإرسال؛ أوزان=100 | موجود | حاليّ |
| إسناد قوالب KPI (Phase T1) | KPI | KpiTemplateAssignment | **Implemented** | `Kpi/KpiTemplateAssignment`, هجرة `KpiTemplateAssignmentsPhaseT1` | — | «الأخصّ يطغى» | موجود | حاليّ |
| تصدير KPI للمالية (قراءة) | KPI | Finance Export | **Implemented** | `KpiEvaluationsController` (finance-export) | — | CSV BOM + تدقيق بلا PII | موجود | حاليّ |

---

## المجال 5 — خدمات الموظف والإجازات والمالية

| Req (BRD) | Epic | Feature | الحالة | دليل الكود | عمل مستقبليّ | معايير قبول | اختبار | المرحلة |
|-----------|------|---------|:---:|-----------|--------------|-------------|:---:|:---:|
| الإجازات + التوجيه (TL→Mgr→HR) | Employee | Leave routing | **Implemented** | `LeaveRequestService`, `Leave/*`, هجرة `HrLeaveRequestRouting` | نواة سير عمل (AD-102) | التوجيه للقائد الفعليّ حصرًا | موجود | حاليّ / P3 |
| أرصدة + Ledger مشتقّ | Employee | Balances | **Implemented** | `BalanceService`, `EmployeeServices/{BalancePolicy,EmployeeBalanceLedger}` | — | الرصيد = Σcredit−Σdebit | موجود | حاليّ |
| طلبات HR + خطاب نهائي PDF | Employee | HR requests + doc | **Implemented** | `EmployeeServiceRequestService`, هجرة `EmployeeServiceFinalDocumentMetadata` | — | PDF خارج wwwroot؛ تنزيل مُخوَّل | موجود | حاليّ |
| حارس كفاية الرصيد + إقرار | Employee | Balance guard | **Implemented** | `LeaveRequestService`, `PermissionShortfallResolution` | — | تجاوز بلا إقرار ⇒ 400 | موجود | حاليّ |
| عرض تأثير الرواتب (FIN-L1) | Finance | Payroll impact view | **Implemented (view only)** | `PayrollImpactService`, `Payroll/PayrollImpactReview` | — | عرض فقط، لا صرف | موجود | حاليّ |
| **ERP ماليّ كامل / صرف مستحقات** | Finance | كشوف رواتب | **Deferred** | خارج النطاق (Ch14.22) | — | — | — | مؤجّل |

---

## المجال 6 — الحوكمة

| Req (BRD) | Epic | Feature | الحالة | دليل الكود | عمل مستقبليّ | معايير قبول | اختبار | المرحلة |
|-----------|------|---------|:---:|-----------|--------------|-------------|:---:|:---:|
| عناصر الحوكمة + تحديثات | Governance | Items/Updates | **Implemented** | `Governance/{GovernanceItem,GovernanceItemUpdate}`, هجرة `GovernanceWorkspaceItems` | — | إنشاء/تحديث مُدقَّق | موجود | حاليّ |
| إجراءات + تصعيدات فردية | Governance | ActionItems/Escalations | **Implemented** | `Governance{ActionItem,Escalation}Service`, هجرات مرتبطة | — | مسار تصعيد يعمل | موجود | حاليّ |
| ملاحظات إدارية + مخاطر + قرارات | Governance | Notes/Risk/Decision | **Implemented with gaps** | `Governance/{ManagementNote,Risk,Decision}` | روابط الدورة (CL-05/06/07) | — | موجود | حاليّ / P7 |
| **Risk→Decision→ActionItem→CAPA closure** | Governance | دورة الحوكمة الكاملة | **Requires clarification** | كيانات موجودة، روابط ناقصة | CL-05, CL-06, CL-07, AD-05..07 | FK Risk→Decision مفروض | جزئيّ | P7 |

---

## المجال 7 — العملاء والمشاريع والتنفيذ

| Req (BRD) | Epic | Feature | الحالة | دليل الكود | عمل مستقبليّ | معايير قبول | اختبار | المرحلة |
|-----------|------|---------|:---:|-----------|--------------|-------------|:---:|:---:|
| العملاء (أرشفة/تفعيل/حذف محروس) | Clients | Client lifecycle | **Implemented** | `ClientService`, `Clients/Client` | — | حذف محروس بالاستخدام | موجود | حاليّ |
| المشاريع (دورة أساسية) | Clients | Project lifecycle | **Implemented with gaps** | `ProjectService`, `Clients/Project` | إتمام دورة التسليم (CL-08) | — | موجود | حاليّ / P2.5 |
| محفظة الحسابات | Clients | Account Portfolio | **Implemented** | `AccountPortfolioService` | — | عرض المحفظة حسب النطاق | جزئيّ | حاليّ |
| **Project→Goal→Workstream→Deliverable** | Execution | تفكيك التنفيذ | **Under development** | `ProjectWorkstream*`, `WorkstreamDeliverable*`, `ExecutionTaxonomyValue*` (غير مُودَعة) + 3 هجرات | إيداع + مراجعة + نشر مُتحقَّق (AD-110) | — | موجود (test files غير مُودَعة) | P2/P2.5 |
| تجميع تنفيذ (Pod / Project-First) | Execution | Execution aggregation | **Under development** | `PodExecutionAggregationService`, `ProjectFirstExecutionAggregationService*` | إتمام + نشر | — | جزئيّ | P2.5 |
| القوالب التنفيذية القديمة الستة (أرشفة/قراءة تاريخية) | Execution | Legacy execution templates | **Implemented (archived)** | `TemplateSeeder.ArchiveLegacyProductionTemplatesAsync`, حارس `report.template_not_assigned`, `LegacyExecutionFixture` | — | مؤرشفة + رفض الإنشاء + بقاء القراءة/التجميع التاريخيّ | موجود (ErdsPhase5/5.5/6 + Legacy fixture) | حاليّ |
| **تقرير مشتري الإعلانات (Media Buyer) Project-First** | Execution | Media Buyer Project-First model | **Requires clarification** | القديم `MediaBuyerByClient` مؤرشف؛ **لا قالب Project-First بديل** | **CL-16 (تعريف ناقص — يتطلّب قرار المالك)** | قرار مالك موثَّق (نموذج/تجميع/تجنّب double-counting) قبل أيّ قالب جديد | رفض الإنشاء + قراءة تاريخية مختبَران؛ لا قالب جديد | مرحلة توسّعة لاحقة |
| **Task / WorkLog / Revision / Quality** | Execution | منصّة التنفيذ | **Planned future** | grep=0 | ADR-003 (P5) | — | — | P5 |
| **الساعات الفعلية (مصدر التقارير)** | Execution | Actual hours | **Requires clarification** | لا WorkLog | **CL-01 (تناقض مؤكّد)**, AD-02 | حسم المصدر قبل التقرير | — | P5 |

---

## المجال 8 — CRM B2B

| Req (BRD) | Epic | Feature | الحالة | دليل الكود | عمل مستقبليّ | معايير قبول | اختبار | المرحلة |
|-----------|------|---------|:---:|-----------|--------------|-------------|:---:|:---:|
| Lead→Opportunity→Proposal→Won | CRM | خطّ الأنابيب | **Planned future** | grep=0 | BRD P6 | — | — | P6 |
| Client ↔ Company (النموذج) | CRM | نموذج العميل | **Requires clarification** | Client فقط (لا Company) | **CL-02**, AD-01 | حسم 1:1/1:N | — | P6 |
| Contract / Renewal | CRM | العقود/التجديد | **Planned future** | grep=0 | BRD P6 | — | — | P6 |

---

## المجال 9 — سير العمل والمنصّة المشتركة

| Req (BRD) | Epic | Feature | الحالة | دليل الكود | عمل مستقبليّ | معايير قبول | اختبار | المرحلة |
|-----------|------|---------|:---:|-----------|--------------|-------------|:---:|:---:|
| Workflow Engine مركزيّ (ADR-004) | Platform | نواة سير عمل | **Planned future** | grep=0 (سلاسل صلبة) | **AD-102 (Option B في P3)** | تعريف/إصدار/مراحل/انتقالات/فاعل/تدقيق/SLA/idempotent | — | P3 |
| Event Bus داخليّ | Platform | أحداث بين الوحدات | **Recommended only** | Outbox للبريد فقط | AD-107 | — | — | P3 |
| **File Service مُصنَّف/مُصدَّر (ADR-011)** | Platform | خدمة الملفات | **Planned future** | تخزين محليّ بسيط | AD-106, CL-04 | — | — | P4 |
| Integration Platform | Platform | تكاملات | **Planned future** | نقاط تكامل مفردة (Ch14.14) | — | — | — | P9 |
| Screen Catalog (ADR-009) | Platform | كتالوج الشاشات | **Deferred** | لا كتالوج | ADR-009 | — | — | رؤية |

---

## المجال 10 — الإشعارات والبريد والتدقيق والعمليات

| Req (BRD) | Epic | Feature | الحالة | دليل الكود | عمل مستقبليّ | معايير قبول | اختبار | المرحلة |
|-----------|------|---------|:---:|-----------|--------------|-------------|:---:|:---:|
| إشعارات In-App (ADR-005) | Notifications | SignalR | **Implemented** | `NotificationHub`, `SignalRNotificationPusher` | — | إشعار فوريّ للمستخدم | موجود | حاليّ |
| بريد عبر Outbox (ADR-006) | Email | Outbox + Control | **Implemented** | `EmailOutboxDispatcher`, `EmailControlService`, هجرات Email | — | إرسال موثوق + allow-list | موجود | حاليّ |
| تدقيق شامل | Audit | AuditLog | **Implemented** | `AuditService`, `System/AuditLog` | — | كل إجراء حسّاس مُدقَّق | موجود | حاليّ |
| **CSP header** | Security | تحصين الرأس | **Implemented with gaps** (مفقود) | grep=0 | **إضافة CSP (R1, فئة A)** | رأس CSP مُطبَّق | لا يوجد | **P0** |
| RC مطابق + CI/CD | Ops | أتمتة النشر | **Recommended only** | نشر يدويّ RC | AD-110/112 | — | — | تشغيليّ |
| سياسة البيئات (TEST=Staging توسّعة · RC=Hotfix) | Ops | فصل مسارَي النشر | **Implemented (policy)** | `MASTER_EXECUTION_PLAN.md` §تاسعًا · `RC-Environment-Full-Clone-Execution-Plan.md` | Release Manifest + Hotfix Forward-Integration Gate | لا خلط للمسارين؛ Forward-Merge لكل Hotfix؛ إطلاق التوسّعة من TEST المعتمَدة | **AD-114** · `HOTFIX_FORWARD_INTEGRATION_REGISTER.md` | تشغيليّ |
| DR (RTO/RPO) | Ops | استعادة الكوارث | **Recommended only** | Backups فقط | AD-113 | — | — | تشغيليّ |
| عزل قاعدة الاختبار | Ops | Test isolation | **Recommended only** | قاعدة مشتركة دائمة | AD-109 (R8) | — | — | P0 |

---

## المجال 11 — AI والقدرات المؤجّلة صراحةً (Ch14.22)

| Req (BRD) | الحالة | دليل | المرحلة |
|-----------|:---:|------|:---:|
| AI (بشريّ-في-الحلقة، ADR-012) | **Deferred** | grep=0 + Ch14.22 | مؤجّل |
| Customer Portal | **Out of scope** (مؤجّل صراحةً) | Ch14.22 | مؤجّل |
| Mobile App | **Out of scope** | Ch14.22 | مؤجّل |
| Public API Marketplace | **Out of scope** | Ch14.22 | مؤجّل |
| Multi-Company SaaS | **Out of scope** | Ch14.22 | مؤجّل |
| Data Warehouse | **Out of scope** | Ch14.22 | مؤجّل |
| Advanced Auto-Assignment | **Out of scope** | Ch14.22 | مؤجّل |

---

## خلاصة المصفوفة

| الحالة | العدد التقريبيّ للبنود | ملاحظة |
|--------|:---:|-------|
| Implemented | ~24 | نواة النظام الحالي ناضجة |
| Implemented with gaps | ~8 | فجوات مرتبطة بمراحل قادمة أو توحيد |
| Under development | ~3 | Workstreams/Deliverables/Taxonomy (غير مُودَعة) |
| Requires clarification | ~7 | مرتبطة بـ CL-01..16 وقراراتها (CL-16 = نموذج مشتري الإعلانات Project-First) |
| Planned future | ~10 | Workflow/CRM/Task/BI/Files (رؤية BRD) |
| Recommended only | ~7 | تحسينات معمارية/تشغيلية |
| Deferred / Out of scope | ~8 | Ch14.22 (AI/Portal/Mobile/…) |

**البند الوحيد المصنّف «مطلوب حاليًّا ومفقود» بوضوح = CSP (P0).** كل «Planned future / Deferred / Out of scope» **ليست عيوبًا في النطاق الحاليّ** بل رؤية مخطّطة أو مؤجّلة صراحةً في BRD.

*— انتهت المصفوفة. تحقيق قراءة فقط؛ نتائج الاختبارات غير مؤكّدة (لم يُشغَّل dotnet test).*
