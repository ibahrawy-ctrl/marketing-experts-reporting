# PRE-STABILIZATION WORKTREE SNAPSHOT (Read-Only)

> لقطة مرجعية للحالة قبل أي تعديل. أُنشئت للقراءة فقط قبل تنفيذ خطوات الاستقرار.
> لا تحوي أي أسرار أو محتوى تهيئة حسّاس.

## 0. معلومات الالتقاط

| العنصر | القيمة |
|---|---|
| الفرع (Branch) | `develop` |
| HEAD SHA (كامل) | `6fd2253b9d720a1584c04b81f40cc5440c8f3500` |
| HEAD SHA (مختصر) | `6fd2253` |
| آخر Commit | `6fd2253 RC-4 Sales Module baseline` |
| Remote | `origin  git@github.com:ibahrawy-ctrl/marketing-experts-reporting.git` |
| origin/develop متتبَّع محليًّا؟ | لا |
| تاريخ الالتقاط | 2026-07-11 |

## 1. ملخّص العدّ

| الفئة | العدد |
|---|---|
| مُعدَّل متتبَّع ( M) | 25 |
| محذوف متتبَّع ( D) | 3 |
| غير متتبَّع (??) | 68 |
| **الإجمالي** | **96 إدخال** |

> ملاحظة تسوية: العدّ السابق في تدقيق `DEVELOP_WORKTREE_STABILIZATION_AUDIT.md` كان 91 إدخالًا (63 غير متتبَّع). الفرق (+5 غير متتبَّع) سببه إنشاء 5 وثائق تخطيط/تدقيق جذرية إضافية خلال جلسات التخطيط (`MASTER_EXECUTION_PLAN.md`, `DEVELOP_WORKTREE_STABILIZATION_AUDIT.md` وغيرها). لا تغيير في الكود المصدري.

## 2. Diffstat (الملفات المتتبَّعة المُعدَّلة/المحذوفة)

```
28 files changed, 2516 insertions(+), 2670 deletions(-)
```

الملفات المتتبَّعة (25 M + 3 D):

| الملف | الحالة | Δ |
|---|---|---|
| reporting-backend/src/Reporting.Api/Program.cs | M | +2 |
| reporting-backend/src/Reporting.Application/Common/ReportCalendarPolicy.cs | M | +57 |
| reporting-backend/src/Reporting.Application/Common/Roles.cs | M | +11 |
| reporting-backend/src/Reporting.Domain/Enums/Enums.cs | M | +18 |
| reporting-backend/src/Reporting.Infrastructure/DependencyInjection.cs | M | +4 |
| reporting-backend/src/Reporting.Infrastructure/Persistence/AppDbContext.cs | M | +6 |
| reporting-backend/src/Reporting.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs | M | +190 |
| reporting-backend/src/Reporting.Infrastructure/Persistence/TemplateSeeder.cs | M | +510 |
| reporting-backend/src/Reporting.Infrastructure/Services/SubmissionService.cs | M | +78 |
| reporting-backend/tests/Reporting.IntegrationTests/ErdsPhase3RolloutTests.cs | M | ±60 |
| reporting-backend/tests/Reporting.IntegrationTests/ErdsPhase55WorkUnitTests.cs | D | −441 |
| reporting-backend/tests/Reporting.IntegrationTests/ErdsPhase5PodExecutionTests.cs | D | −384 |
| reporting-backend/tests/Reporting.IntegrationTests/ErdsPhase6ExecutiveDashboardTests.cs | D | −372 |
| reporting-backend/tests/Reporting.IntegrationTests/ReportsTests.cs | M | −918 |
| reporting-frontend/src/App.tsx | M | +15 |
| reporting-frontend/src/components/DashboardShell.nav.test.tsx | M | ±62 |
| reporting-frontend/src/components/DashboardShell.tsx | M | ±378 |
| reporting-frontend/src/lib/auth.tsx | M | ±14 |
| reporting-frontend/src/lib/format.ts | M | +26 |
| reporting-frontend/src/pages/AccountPortfolioPage.tsx | M | ±4 |
| reporting-frontend/src/pages/ProjectDetailPage.tsx | M | +802 |
| reporting-frontend/src/pages/SalesAggregationPage.tsx | M | ±425 |
| reporting-frontend/src/pages/SalesRepDashboardPage.test.tsx | M | ±58 |
| reporting-frontend/src/pages/SalesRepDashboardPage.tsx | M | ±68 |
| reporting-frontend/src/pages/SubmissionsPage.tsx | M | ±52 |
| reporting-frontend/src/pages/TeamLeaderSalesDashboardPage.test.tsx | M | ±39 |
| reporting-frontend/src/pages/TeamLeaderSalesDashboardPage.tsx | M | +7 |
| reporting-frontend/src/types/api.ts | M | +185 |

### الملفات المحذوفة المتتبَّعة (3)
- `reporting-backend/tests/Reporting.IntegrationTests/ErdsPhase55WorkUnitTests.cs` (كان 441 سطرًا)
- `reporting-backend/tests/Reporting.IntegrationTests/ErdsPhase5PodExecutionTests.cs` (كان 384 سطرًا)
- `reporting-backend/tests/Reporting.IntegrationTests/ErdsPhase6ExecutiveDashboardTests.cs` (كان 372 سطرًا)

## 3. جرد الملفات غير المتتبَّعة (68)

### 3.1 وثائق تخطيط/تدقيق جذرية (7 — قبل إعادة التنظيم)
- ARCHITECTURAL_DECISION_REGISTER.md
- BRD_CLARIFICATION_REGISTER.md
- CURRENT_STATE_INVENTORY.md
- DEVELOP_WORKTREE_STABILIZATION_AUDIT.md
- ENTERPRISE_ARCHITECTURE_REVIEW_VALIDATION.md
- MASTER_EXECUTION_PLAN.md
- TRACEABILITY_MATRIX.md

### 3.2 مخرجات بناء (Build Output) — يجب استبعادها
- `reporting-backend/publish-p0p1/` — 162 ملفًا، ~107MB. لا يحوي أي ملف مصدري `.cs` (تحقّق=0). يحوي DLLs/PDBs/deps.json/runtimeconfig + خط LatoFont + أشجار نشر متداخلة (`publish/`, `publish-test/`, `publish-rc3/`) + نسخ `appsettings.json`/`appsettings.Development.json` مولَّدة. **غير مُتجاهَل حاليًّا** (`git check-ignore` = خروج 1).

### 3.3 مصدر Backend غير متتبَّع (Domain)
- reporting-backend/src/Reporting.Domain/Entities/Clients/ProjectWorkstream.cs
- reporting-backend/src/Reporting.Domain/Entities/Clients/WorkstreamDeliverable.cs
- reporting-backend/src/Reporting.Domain/Entities/ExecutionTaxonomy/ (مجلّد)

### 3.4 مصدر Backend غير متتبَّع (Application)
- reporting-backend/src/Reporting.Application/Clients/DeliverableModels.cs
- reporting-backend/src/Reporting.Application/Clients/IProjectWorkstreamService.cs
- reporting-backend/src/Reporting.Application/Clients/IWorkstreamDeliverableService.cs
- reporting-backend/src/Reporting.Application/Clients/WorkstreamModels.cs
- reporting-backend/src/Reporting.Application/Common/ProjectFirstExecutionSchema.cs
- reporting-backend/src/Reporting.Application/ExecutionTaxonomy/ (مجلّد)
- reporting-backend/src/Reporting.Application/Reports/IProjectFirstExecutionAggregationService.cs
- reporting-backend/src/Reporting.Application/Reports/ProjectFirstExecutionModels.cs

### 3.5 مصدر Backend غير متتبَّع (Infrastructure)
- Persistence/Configurations/ExecutionTaxonomyConfigurations.cs
- Persistence/Configurations/ProjectWorkstreamConfiguration.cs
- Persistence/Configurations/WorkstreamDeliverableConfiguration.cs
- Persistence/ExecutionTaxonomySeeder.cs
- Services/ExecutionTaxonomyService.cs
- Services/ProjectFirstExecutionAggregationService.cs
- Services/ProjectWorkstreamService.cs
- Services/WorkstreamDeliverableService.cs

### 3.6 مصدر Backend غير متتبَّع (Controllers)
- Controllers/ExecutionTaxonomyController.cs
- Controllers/ExecutionTaxonomyOptionsController.cs
- Controllers/ProjectFirstExecutionAggregationController.cs
- Controllers/ProjectWorkstreamsController.cs
- Controllers/WorkstreamDeliverablesController.cs

### 3.7 هجرات غير متتبَّعة (3 أزواج .cs + .Designer.cs)
- 20260708232456_AddExecutionTaxonomyCatalog (.cs + .Designer.cs)
- 20260709222126_AddProjectWorkstreams (.cs + .Designer.cs)
- 20260709231845_AddWorkstreamDeliverables (.cs + .Designer.cs)

### 3.8 اختبارات Backend غير متتبَّعة (6)
- ExecutionTaxonomyAdminTests.cs
- ExecutionTaxonomyOptionsTests.cs
- ProjectFirstExecutionAggregationTests.cs
- ProjectWorkstreamsTests.cs
- TemplateTaxonomyV4Tests.cs
- WorkstreamDeliverablesTests.cs

### 3.9 مصدر Frontend غير متتبَّع (25)
Components: Collapsible.tsx/.test, HeaderActions.tsx, ShowMore.tsx/.test, StickyBar.tsx/.test, Tabs.tsx/.test, DashboardShell.execution.nav.test.tsx, DashboardShell.portfolio.nav.test.tsx
Lib: navConfig.ts, useExecutionTaxonomy.ts, useProjectExecution.ts, useProjectWorkstreams.ts, useWorkstreamDeliverables.ts
Pages: ExecutionTaxonomyManagementPage.tsx/.test, ProjectDeliverables.test.tsx, ProjectWorkstreams.test.tsx, TaxonomySelect.test.tsx, TeamLeaderExecutionPage.tsx, TeamLeaderProjectExecutionPage.tsx/.test

## 4. قيود اللقطة
- التقاط للقراءة فقط عبر `git status/log/diff/check-ignore` و`ls/find/du`.
- لم يُعدَّل/يُرحَّل/يُلتزَم أي شيء أثناء الالتقاط.
- لم تُطبَّق أي هجرة ولم تُكتب أي قاعدة بيانات.
- سلسلة هجرات الإنتاج تبقى `[UNV]` (غير قابلة للتحقّق من المستودع وحده).

*نهاية اللقطة المرجعية.*
