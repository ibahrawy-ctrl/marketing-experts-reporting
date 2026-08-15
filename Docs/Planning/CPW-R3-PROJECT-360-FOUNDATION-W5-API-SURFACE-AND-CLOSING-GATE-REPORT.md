# CPW-R3 — PROJECT 360 FOUNDATION — W5: سطح الـAPI + بذر الكتالوج + عقد التخويل — تقرير بوّابة الإغلاق

**الحالة:** `W5 = GO` (بانتظار اعتماد المالك — **لا يبدأ W6 تلقائيًّا**)
**التاريخ:** 2026-08-15
**الأساس (D-01):** `develop @ c157829f750ce98b7e7aad451a23183b58462cb4`
**المصدر التصميميّ:** `Docs/Planning/CPW-R3-PROJECT-360-FOUNDATION-R2-REVISED-DESIGN-REPORT.md` (R1 = `SUPERSEDED`)
**النطاق:** كود محلّيّ فقط. **صفر** Commit / Push / Merge / PR / Tag / TEST / RC / Production / Frontend / Migration جديدة.

---

## A. النتيجة التنفيذيّة

| البند | النتيجة |
|---|---|
| البناء | **0 Errors** (12 تحذير مهل قراءة NuGet — حميدة، نفس خطّ الأساس) |
| اختبارات الوحدة | **137 / 137** |
| اختبارات Project 360 المستهدَفة | **34 / 34** (20 `Project360FoundationTests` + 14 `Project360ApiSurfaceTests`) |
| الانحدار الكامل | **1461 إجماليًّا · 1438 ناجحًا · 23 فاشلًا** (مدّة 1 س 48 د) |
| **انحدارات مرشَّحة (Candidate Regression)** | **0** |
| **غير محسوم (Unresolved)** | **0** |
| عيوب خطّ أساس (Baseline Defect) | **5** |
| حالة مشتركة/تلوّث بيئة (Shared-State) | **18** |
| مزامنة النموذج | `No changes have been made to the model since the last migration.` |
| الهجرات | **33**، الرأس `20260811142239_AddProject360Foundation` — **لا Migration #34** |

---

## B. ملفّات W5 / CPW-R3 في شجرة العمل

### ب-1. ملفّات معدَّلة تخصّ CPW-R3 (9 فوق قائمة W0 §6 القذرة)

`Project.cs` · `Decision.cs` · `Enums.cs` · `AppDbContext.cs` · `ClientConfigurations.cs` · `GovernanceConfigurations.cs` · `AppDbContextModelSnapshot.cs` · `ExecutionTaxonomySeeder.cs` · `ExecutionTaxonomyService.cs`

ملفّ مشترك واحد: `DependencyInjection.cs` — كان معدَّلًا أصلًا في الحالة القذرة (W0 §6)، وأضافت CPW-R3 إليه **تسجيل خدمات Project 360 فقط** (+15 سطرًا).

**التحقّق العدديّ:** ملفّات الباك-إند المعدَّلة الآن = **23**؛ قائمة W0 القذرة = **14**؛ الفارق = **9** = بالضبط ملفّات CPW-R3 أعلاه. صفر انزلاق.

### ب-2. ملفّات جديدة (غير متتبَّعة) تخصّ CPW-R3 — 36 ملفًّا

| الطبقة | الملفّات |
|---|---|
| Api/Controllers (6) | `ProjectOverviewController` · `ProjectStrategyController` · `ProjectObjectivesController` · `ProjectKpisController` · `ProjectContractDeliverablesController` · `ProjectGovernanceReadController` |
| Application/Projects360 (5) | `IProject360Authorization` · `IProject360Services` · `Project360Codes` · `Project360Models` · `ProjectHealthPolicy` |
| Domain/Entities/Projects360 (6) | `ProjectStrategy` · `ProjectStrategyAttribute` · `ProjectObjective` · `ProjectKpi` · `ProjectKpiReading` · `ProjectDeliverable` |
| Domain/Projects360 (5) | `ProjectHealthReason` · `ProjectHealthReasonCodes` · `ProjectHealthSnapshot` · `ProjectKpiAchievement` · `ProjectObjectiveProgress` |
| Infrastructure/Services (8) | `Project360Authorization` · `Project360Guards` · `ProjectStrategyService` · `ProjectObjectiveService` · `ProjectKpiService` · `ProjectContractDeliverableService` · `ProjectOverviewService` · `ProjectGovernanceReadService` |
| Persistence (3) | `Projects360Configurations` · هجرة W3 (`.cs` + `.Designer.cs`) |
| Tests (3) | `Project360FoundationTests` · `Project360ApiSurfaceTests` · `ProjectHealthPolicyTests` |

---

## C. جرد مسارات الـAPI (Route Inventory)

### قبل W5 — `/api/projects` (خطّ الأساس W0 §4)

| # | المسار | الحماية |
|---|---|---|
| 1 | `GET /api/projects` | — |
| 2 | `GET /api/projects/{id}` | — |
| 3 | `GET /api/projects/{id}/reports` | — |
| 4 | `GET /api/projects/{id}/summary` | — |
| 5 | `POST /api/projects` | `ManagementOnly` |
| 6 | `PUT /api/projects/{id}` | `ManagementOnly` |
| 7 | `POST /api/projects/{id}/archive` | `ManagementOnly` |
| 8 | `POST /api/projects/{id}/reactivate` | `ManagementOnly` |
| 9 | `DELETE /api/projects/{id}` | `ManagementOnly` |

بالإضافة إلى متحكّمين قائمين تحت نفس البادئة خارج `ProjectsController`: `ProjectWorkstreamsController` · `WorkstreamDeliverablesController` · `ProjectFirstExecutionAggregationController`.

### بعد W5 — الإضافات (33 مسارًا، إضافيّة بحتة)

| المتحكّم | العدد | المسارات |
|---|---|---|
| `ProjectOverviewController` | 1 | `GET …/overview` |
| `ProjectStrategyController` | 3 | `GET …/strategy` · `GET …/strategy/schema` · `PUT …/strategy` |
| `ProjectObjectivesController` | 8 | `GET` · `GET {id}` · `POST` · `PUT {id}` · `PATCH {id}/status` · `PATCH {id}/activate` · `PATCH {id}/deactivate` · `DELETE {id}` |
| `ProjectKpisController` | 10 | `GET kpis` (قراءة مجمّعة) · `GET/POST objectives/{oid}/kpis` · `GET/PUT objectives/{oid}/kpis/{kid}` · `PATCH …/activate` · `PATCH …/deactivate` · `GET/POST …/readings` · `PUT …/readings/{rid}` |
| `ProjectContractDeliverablesController` | 8 | `GET` · `GET types` · `GET {id}` · `POST` · `PUT {id}` · `PATCH {id}/progress` · `PATCH {id}/activate` · `PATCH {id}/deactivate` |
| `ProjectGovernanceReadController` | 3 | `GET …/risks` · `GET …/decisions` · `GET …/notes` |

---

## D. توافق الـAPI القائم

```text
EXISTING PROJECT ROUTES REMOVED = 0
EXISTING PROJECT ROUTES CHANGED = 0
```

**الدليل:** `git status --porcelain` على `ProjectsController.cs` و`ProjectWorkstreamsController.cs` و`ProjectFirstExecutionAggregationController.cs` و`WorkstreamDeliverablesController.cs` = **صفر سطر** ⇒ الملفّات الحاملة للمسارات التسعة وما جاورها لم تُمَسّ إطلاقًا.

---

## E. بذر الكتالوج (DEC-W4-01)

ثلاثة مجالات في `ExecutionTaxonomySeeder` + `ExecutionTaxonomyService.KnownDomains` (19 ⟵ **22**):

| المجال | عدد الرموز | المصدر |
|---|---|---|
| `strategy_section` | 6 | ملحق W1-A بند 3 |
| `strategy_field` | 14 | R2 §9-7 + §5-3-ج |
| `contract_deliverable` | 18 | R2 §9-7 |
| **الإجماليّ** | **38** | — |

- `contract_deliverable` مجال **مستقلّ تمامًا** عن `deliverable` (مخرَج تيّار العمل) — D-03 محفوظ.
- `landing_page_delivery` استُعمل بدل `landing_page` تفاديًا للاصطدام برمز قائم.
- **بند مرفوع:** التسميات العربيّة/الإنجليزيّة ترجمة حرفيّة لرموزها لأنّ R2 لا يورد تسميات؛ وهي بيانات كتالوج قابلة للتحرير من شاشة الإدارة (صفر كود وصفر هجرة عند تعديلها).

---

## F. حتميّة البذر (Run 1 / Run 2)

الاختبار: `Project360ApiSurfaceTests.CatalogBootstrap_SecondRun_CreatesNothing_AndUpdatesNothing` — **PASS**.

| البُعد | Run 1 | Run 2 |
|---|---|---|
| القيم المُنشَأة | 38 (6 + 14 + 18) | **0** |
| القيم المحدَّثة | — | **0** (تطابق لقطة كاملة: المعرّف والاسمان والترتيب والحالة) |
| التكرارات | **0** | **0** |
| حتميّة الترتيب | `SortOrder` مستقلّ لكلّ مجال: 10, 20, 30, 40, 50, 60 | نفسه |
| سلامة مرجعيّة | كلّ رمز حقل يحمل بادئة قسم موجود فعلًا ⇒ صفر حقل يتيم | نفسه |

**آليّة الإثبات:** حذف المجالات الثلاثة ثمّ البذر مرّتين **داخل معاملة تُلغى بالكامل** ⇒ صفر كتابة باقية وصفر مساس بالمجالات الـ19 الأخرى. (البذر يجري أصلًا عند إقلاع التطبيق، فلا سبيل آخر لإثبات «التشغيلة الأولى» على قاعدة مشتركة دائمة.)

منطق الحتميّة في `SeedAsync`: `if (existing.Contains((Domain, Code))) continue;` — إدراج فقط، **بلا أيّ تحديث** لصفّ قائم ⇒ التعديل اليدويّ للتسميات لا يُدهَس عند إعادة التشغيل.

---

## G. عقد مخطَّط الاستراتيجيّة (DEC-W4-02)

`GET /api/projects/{projectId}/strategy/schema` يُرجِع:

- `CoreFields` — **11** حقلًا ثابتًا (`vision`, `strategy_summary`, `target_audience`, `customer_persona`, `positioning`, `value_proposition`, `competitors`, `tone_of_voice`, `messaging`, `marketing_approach`, `success_factors`).
- `Sections` — من كتالوج `strategy_section`: العامّة (`business`, `audience`, `brand`) لكلّ الخدمات + المحجوزة لخدمة (`seo`, `ads`, `social`) بمطابقة `ServiceType`.
- `DynamicFields` — من كتالوج `strategy_field`، ورمز الحقل = «رمز القسم . رمز الحقل»، والبادئة هي **مصدر قابليّة التطبيق**، مع `SectionCode` و`SectionNameAr` مرفقين لبناء المجموعات في الواجهة بلا سطر شرطيّ واحد.

**الإثبات (PASS):** `StrategySchema_IsServiceScoped_ByCatalogDataOnly` — مشروع SEO يرى `seo.keywords` ولا يرى `ads.budget`؛ ومشروع Social بالعكس. صفر `if (ServiceType == …)` في تحديد الحقول: المصدر بيانات الكتالوج حصرًا.
**الرفض الخادميّ (PASS):** `StrategyUpsert_FieldOutsideProjectService_IsRejectedWith400` ⇒ `project_strategy.field_not_applicable` بـ400.

---

## H. عقد التخويل

**صفر Policy جديدة، وصفر توسيع لنطاق الرؤية.** المصدر الوحيد للرؤية هو `IClientProjectAccess` القائم؛ طبقة `Project360Authorization` **تضيّق الكتابة فوقه** ولا تضيف مشروعًا واحدًا لما يراه المستخدم.

الترتيب المقصود داخل `LoadVisibleProjectAsync`: **مصادقة ⟵ رؤية ⟵ وجود** — `auth.forbidden` تُرجَع **قبل** الاستعلام عن الوجود، فلا يتسرّب وجود مشروع خارج النطاق عبر فارق الرسائل.

| الطبقة | من يملكها |
|---|---|
| قراءة | كلّ من يرى المشروع عبر `IClientProjectAccess` |
| كتابة **بنيويّة** (إنشاء/حذف هدف · مؤشّر · مخرَج تعاقديّ · استراتيجيّة) | `Roles.ProjectPlanManagers` = Admin / CEO / GeneralManager / Manager **داخل النطاق** |
| كتابة **تشغيليّة** (تقدّم · حالة · قراءات يدويّة) | الإدارة **أو** `project.TeamLeaderId == uid` **أو** `project.AccountManagerId == uid` — لهذا المشروع بعينه |

**مصفوفة عشر هويّات × ستّة موارد (PASS):**
- رؤية واسعة: Admin · CEO · GeneralManager ⇒ 2xx على كلّ الموارد.
- داخل النطاق بالانتماء: قائد الفريق المالك · مدير الحساب · موظّف الفريق ⇒ 2xx قراءةً.
- خارج النطاق: مدير إدارة أخرى · قائد فريق آخر · Viewer بلا انتماء ⇒ **403** على كلّ الموارد.
- بلا توكن ⇒ **401** قبل أيّ منطق.

*(ملاحظة عقد: `GET /strategy` على مشروع بلا استراتيجيّة بعدُ يُرجِع **204** — نجاح لا منع.)*

---

## I. مصفوفة قائد الفريق (D-07 / §23)

| العمليّة | مشروعه | مشروع غيره |
|---|---|---|
| قراءة كلّ موارد Project 360 | ✅ | ❌ 403 |
| تقدّم المشروع · حالة/تقدّم الهدف | ✅ | ❌ |
| قراءات KPI اليدويّة | ✅ | ❌ |
| تحديث المخرَج التعاقديّ ضمن حقوله القائمة | ✅ | ❌ |
| إنشاء/حذف هدف أو مؤشّر أو مخرَج (بنيويّ) | ❌ | ❌ |
| تعديل بيانات العميل الجوهريّة · Task Management · Workflow | ❌ | ❌ |

**الإثبات:** `TeamLeader_CanUpdateOperationalState_ButCannotManageStructure` · `AuthorizationMatrix_OperationalWrite_ReachesOwnersButNotPlainEmployee` — **PASS**. **صفر صلاحيّة عامّة على كلّ المشاريع.**

## J. مصفوفة مدير الحساب (§22)

مطابقة لمصفوفة قائد الفريق حرفًا بحرف عبر نفس المسار (`AccountManagerId == uid`)، **بلا أيّ توسعة لرؤية المشاريع**.
**الإثبات:** `AccountManager_CanUpdateOperationalState_ButCannotManageStructure` — **PASS**.

---

## K. واجهة الأهداف

8 مسارات (CRUD + status + activate/deactivate). **التقدّم مشتقّ لا مخزَّن** (`Objective_Crud_RoundTrips_AndProgressIsDerivedNotStored`). حذف هدف يحمل مؤشّرات ⇒ **409** `project_objective.has_kpis.conflict`.

## L. واجهة المؤشّرات + حارس المسار (D-02)

```text
PROJECT_LEVEL_KPI_CREATE_ROUTE = NONE
```

**الدليل:** عدد مسارات الكتابة (`POST`/`PUT`/`PATCH`/`DELETE`) الحاملة للقالب `"kpis…"` على مستوى المشروع = **0**. الموجود على مستوى المشروع هو `GET kpis` **قراءةً فقط**؛ وكلّ إنشاء يمرّ حصرًا عبر `POST …/objectives/{objectiveId}/kpis`.
**الإثبات:** `ProjectLevelKpiRoute_IsReadOnly_NoCreateRouteExists` · `Kpi_IsAlwaysCreatedUnderObjective_WithMatchingProjectId` — **PASS**.
**RM-01:** هدف موجود لكنّه تابع لمشروع آخر ⇒ **409** `project_kpi.objective_mismatch.conflict` (لا `not_found`) — `Kpi_ObjectiveFromAnotherProject_IsConflict` **PASS**.

## M. القراءات اليدويّة

`GET/POST …/readings` و`PUT …/readings/{id}`. القراءة تحدّث لقطة المؤشّر (`CurrentValue`/`LastReadingDate`)، وتكرار التاريخ ⇒ **409** `project_kpi_reading.duplicate_date.conflict`، والقراءة على مؤشّر غير يدويّ ⇒ **409** `project_kpi.source_not_manual.conflict` (تمهيد D-06: تحوّل `SourceType` لاحقًا على **نفس الصفّ** بلا هجرة).

---

## N. التجميع الموزون على مستويين (DEC-W4-03)

مثال المالك مُثبَت رقميًّا في مستويين من الاختبار:

- وحدة: `ProjectHealthPolicyTests.ComputeProjectKpiScore_TwoLevelWeighting_MatchesOwnerExample`
- تكامل من طرف إلى طرف: `Overview_TwoLevelWeightedAggregation_Returns65_NotFlatAverage`

```text
Objective A (Weight 70): KPI A1 (w50, ach100) + KPI A2 (w50, ach0)  ⇒ A = 50
Objective B (Weight 30): KPI B1 (w50, ach100)                        ⇒ B = 100
Project KPI Achievement = (50×70 + 100×30) / 100 = 65   ✅
المتوسّط المسطَّح المرفوض (66.67)                            ❌ مؤكَّد عدم مساواته
```

**رقم واحد للمفهوم الواحد عبر اللوحة كلّها:** `overview.Kpis.AverageAchievementPercent == overview.Objectives.AverageAchievementPercent == overview.Health.KpiScore == 65`. والنوع نفسه (`RollUpObjectiveScores` يستقبل نتائج أهداف لا مؤشّرات) يمنع تمرير قائمة مسطَّحة بالخطأ.

---

## O. المخرَجات التعاقديّة (D-03)

مسار مستقلّ `…/contract-deliverables` وكتالوج مستقلّ `contract_deliverable`، **بلا أيّ مساس** بـ`ProjectWorkstreamsController` أو `WorkstreamDeliverablesController` أو مجال `deliverable`.
`ContractDeliverables_HaveTheirOwnRouteAndCatalog_WithoutTouchingWorkstreamDeliverables` — **PASS**.
تعارض الهدف عبر مشروع آخر ⇒ **409** `project_deliverable.objective_mismatch.conflict`.
**DEC-W4-04 محفوظ:** صفر حقل جديد وصفر هجرة لأجل Progress/Weight/Tasks/Milestones — الاستعمال على مخطّط W3 كما هو.

## P. المخاطر / القرارات / الملاحظات

`GET …/risks` · `GET …/decisions` · `GET …/notes` — **قراءة فقط**، مرشَّحة بالمشروع (`Risk.ProjectId` · `Decision.ProjectId` · `ManagementNote(EntityType=Project, EntityId)`), وتخضع لنفس بوّابة الرؤية.
`GovernanceRead_IsProjectFiltered_AndWriteless` — **PASS** (صفر مسار كتابة أُضيف إلى الحوكمة).

## Q. تقدّم المشروع

`Project.ProgressPercent` يُحدَّث ضمن الطبقة التشغيليّة (الإدارة أو قائد الفريق/مدير الحساب المسؤول)، ويدخل مكوّنًا بوزن **0.30** في احتساب الصحّة (مقابل 0.50 للمؤشّرات و0.20 للجدول الزمنيّ). **Health.Reasons مشتقّة فقط** — صفر عمود JSON وصفر جدول أسباب وصفر تخزين تاريخيّ (W1-A).

## R. لوحة النظرة العامّة (D-05)

`GET /api/projects/{projectId}/overview` — **نداء API واحد** يجمع: Core · Progress · Health (+ أسباب مشتقّة) · Objectives (مع عناصرها) · KPIs · Contract Deliverables · Risks · Decisions · Notes · حالة الاستراتيجيّة.
`Overview_AggregatesEverySection_AndReflectsComputedHealth` · `Overview_ProjectWithoutAnyComponent_IsNotEvaluated_NotZero` — **PASS** (مشروع بلا أيّ مكوّن = «غير مُقيَّم» لا «صفر»).

## S. صفر N+1

```text
OVERVIEW_QUERY_COUNT_SMALL  = 12      (Fixture: 1 هدف · 1 مؤشّر)
OVERVIEW_QUERY_COUNT_LARGE  = 12      (Fixture: 20 هدفًا · 100 مؤشّر)
```

عدد ثابت تمامًا لا يتناسب خطّيًّا مع عدد الصفوف؛ والقيمة **12** مثبَّتة في الاختبار ضدّ الانحدار المستقبليّ.
`Overview_SqlQueryCount_IsConstant_RegardlessOfObjectiveCount` — **PASS** (بمعترِض يَعُدّ الاستعلامات فعليًّا، مع حارس `smallCount > 0` يمنع «نجاحًا كاذبًا» عند انفصال المعترِض).

## T. IDOR / BOLA

| السيناريو | النتيجة |
|---|---|
| مشروع قائم خارج النطاق مقابل مشروع غير موجود | **لا يمكن تمييزهما** من الاستجابة |
| هدف من مشروع A عبر مسار مشروع B | مرفوض خادميًّا |
| مؤشّر من مشروع A تحت هدف/مشروع B | مرفوض خادميًّا |
| مخرَج تعاقديّ من A عبر B | مرفوض خادميًّا |
| استراتيجيّة مشروع غير مصرَّح به | 403 قبل كشف الوجود |
| قائد فريق/مدير حساب على مشروع أجنبيّ | 403 |

`Idor_ExistingOutOfScopeAndNonExistent_AreIndistinguishable` · `Idor_ForeignChildResource_IsNotReachableThroughVisibleProject` · `ProjectOutsideVisibility_IsForbidden_BeforeExistenceIsRevealed` — **PASS**. **صفر ثقة في GUID الابن وحده.**

## U. عقد الأخطاء

كلّ الأكواد المطلوبة في §26 موجودة ومركزيّة في `Project360Codes` (لا يخترع المتحكّم كودًا ولا منطقًا):

`project_objective.has_kpis.conflict` · `project_kpi.objective_mismatch.conflict` · `project_kpi.source_not_manual.conflict` · `project_kpi_reading.duplicate_date.conflict` · `project_deliverable.objective_mismatch.conflict` · `project_strategy.field_code_invalid` · `project_strategy.section_code_invalid`
وإضافات متّسقة: `project_strategy.field_not_applicable` · `project_strategy.duplicate_field.conflict` · `project_kpi.objective_required` · `project_kpi.target_invalid` · `project_deliverable.type_immutable.conflict` · `project_deliverable.workstream_mismatch.conflict` · `project_360.{percent,weight,sort_order,date_range,name}_invalid`.
`ErrorCodes_MapToProvenHttpStatuses` — **PASS** (400 للتحقّق · 403 للتخويل · 404 للوجود · 409 للتعارض).

## V. سلامة الـDTO

كلّ الاستجابات عبر DTOs في `Project360Models`؛ صفر تسريب لكيان EF أو حقل داخليّ، والاستعلامات `AsNoTracking` بإسقاط `Select` على مستوى الخادم.

---

## W. الاختبارات المستهدَفة

| المجموعة | النتيجة |
|---|---|
| `Project360FoundationTests` | **20 / 20** |
| `Project360ApiSurfaceTests` | **14 / 14** |
| `Reporting.UnitTests` (شاملة `ProjectHealthPolicyTests`) | **137 / 137** |

## X. الانحدار الكامل

```text
Total: 1461   Passed: 1438   Failed: 23   Duration: 1 h 48 m
```

توزيع الفشل الـ23 على **6 أصناف** لا يمسّ أيّها Project 360.

---

## Y. تصنيف الـ23 فشلًا (بالدليل)

### ي-1. منهج الإثبات

1. **الانحدار الكامل** على `reporting_test` المشتركة.
2. **إعادة تشغيل كلّ صنف منفردًا** على نفس القاعدة ⇒ الـ23 نفسها تكرّرت بالضبط ⇒ **صفر Order-Dependence**.
3. **بوّابة القاعدة النظيفة المعزولة (§31):** نسخة معزولة من الشجرة إلى `/tmp` مع سلسلة اتّصال إلى قاعدة جديدة `reporting_test_w5iso` (لم تُمَسّ `reporting_test` ولا TEST ولا RC ولا الإنتاج)، بناء 0 أخطاء، ثمّ تشغيل الأصناف الستّة + صنفَي Project 360. **القاعدة المؤقّتة والنسخة حُذفتا بعد الاختبار وتُحقِّق من الحذف.**

### ي-2. جدول التصنيف

| الصنف / الاختبار | الانحدار الكامل | منفردًا (قاعدة مشتركة) | قاعدة نظيفة معزولة | مسّته W5؟ | التصنيف | الدليل |
|---|---|---|---|---|---|---|
| `ComplianceDueLateTests.DailySales_AllWorkingDays_FullCompliance` | Failed | Failed | **Failed** | لا | **Baseline Defect** | `Expected 5 / Actual 6` |
| `ComplianceDueLateTests.DailySales_PartialDays_LateAndMissingOverdue` | Failed | Failed | **Failed** | لا | **Baseline Defect** | `Expected 5 / Actual 6` |
| `ComplianceDueLateTests.DailySales_SubmittedAfterDay_IsLateSubmitted` | Failed | Failed | **Failed** | لا | **Baseline Defect** | `Expected 1 / Actual 2` |
| `ComplianceDueLateTests.DailySales_Draft_DoesNotCountAsSubmitted` | Failed | Failed | **Failed** | لا | **Baseline Defect** | `Expected 5 / Actual 6` |
| `ReportRemindersTests.Generate_DailyOverdue_PastWeek_CreatesRowPerWorkingDay` | Failed | Failed | **Failed** | لا | **Baseline Defect** | `Expected 5 / Actual 6` |
| `EmailNotificationsUiTests` (13 اختبارًا) | Failed ×13 | Failed ×13 | **Passed 21/21** | لا | **Shared-State** | `Npgsql TimeoutException` بعد 30 ث |
| `ReportReminderSchedulerTests.Tick_AfterRestartSameSlot_RunsAgainButCreatesNoNewRows` | Failed | Failed | **Passed 8/8** | لا | **Shared-State** | `Npgsql TimeoutException` |
| `ReportTemplateAssignmentTests` (2) | Failed ×2 | Failed ×2 | **Passed 15/15** | لا | **Shared-State** | `HttpClient.Timeout 100 s` |
| `V102TemplateAdminTests` (2) | Failed ×2 | Failed ×2 | **Passed 13/13** | لا | **Shared-State** | `HttpClient.Timeout 100 s` |

### ي-3. السبب الجذريّ — المجموعة الأولى (5 عيوب خطّ أساس)

الاختبارات الخمسة تتوقّع **5 أيّام عمل**، والكود يُنتج **6**. المصدر `ReportWorkingDaysPolicy.SaturdayApplicabilityFloor = 2026-07-25`: السبت يُحتسب يوم عمل متوقَّعًا لأدوار المبيعات من W31 فصاعدًا. توثيق `ComplianceDueLateTests` نفسه ما زال يقول «تُستبعَد الجمعة/السبت» ⇒ الاختبارات لم تُحدَّث بعد هوتفكس السبت.

**إثبات السبق الزمنيّ على CPW-R3:**

| الملفّ | آخر تعديل |
|---|---|
| `ReportWorkingDaysPolicy.cs` (غير متتبَّع — W0 §6) | **2026-07-25** |
| `ReportDueService.cs` (معدَّل — W0 §6) | **2026-07-25** |
| `ComplianceDueLateTests.cs` (متتبَّع، **غير معدَّل**) | 2026-06-27 |
| ملفّات W5 (`ProjectOverviewService.cs` مثالًا) | **2026-08-13** |

⇒ العطب أقدم من CPW-R3 بأسبوعين، وينتمي حرفيًّا إلى «الحالة القذرة القائمة مسبقًا» في W0 §6 التي حكمها المُلزِم: **لا يُصلَح داخل CPW-R3 ولا يُعَدّ انحدارًا**.
**مقترح تسجيل رسميّ (لقرار المالك):** `BASELINE-BE-DEFECT-03 — SATURDAY-EXPECTED-DAYS` — تُعالَج ضمن تذكرة هوتفكس السبت لا هنا.

### ي-4. السبب الجذريّ — المجموعة الثانية (18 حالة تلوّث بيئة)

كلّ الأخطاء الـ18 **مهل زمنيّة** لا تأكيدات منطقيّة، وكلّها اختفت تمامًا على قاعدة نظيفة. القياس المباشر لتضخّم `reporting_test` المشتركة الدائمة:

| الجدول | عدد الصفوف الحيّة | الأثر |
|---|---|---|
| `email_notifications` | **10,292,083** | استعلامات سجلّ البريد ⇒ `Npgsql TimeoutException` (15 حالة) |
| `notifications` | 846,817 | — |
| `refresh_tokens` | 172,408 | — |
| `AspNetUsers` | **136,323** | نقطة `assignments` تمسح المستخدمين ⇒ `HttpClient.Timeout` 100 ث (4 حالات) |
| `AspNetUserRoles` | 132,561 | — |

الفارق الزمنيّ حاسم: `EmailNotificationsUiTests` استغرقت **6 د 11 ث ⇒ 2 ثانية**، و`ReportTemplateAssignmentTests` **17 د 17 ث ⇒ 2 ثانية** على القاعدة النظيفة.
هذا يطابق قاعدة بيئة الاختبارات المعروفة: قاعدة دائمة مشتركة وحسابات `TestAuth` تتراكم. **لم يُجرَ أيّ تنظيف عالميّ للقاعدة المشتركة** التزامًا بـ§32.

### ي-5. الخلاصة العدديّة

```text
Candidate Regression         = 0
Baseline Defect              = 5
Shared-State / Order Dependent = 18
Unresolved                   = 0
المجموع                       = 23
```

---

## Z. البناء ومزامنة النموذج

```text
dotnet build Reporting.sln            ⇒ 0 Errors / 12 Warnings (مهل قراءة NuGet — نفس خطّ الأساس)
dotnet ef migrations has-pending-model-changes ⇒ No changes have been made to the model since the last migration.
عدد الهجرات                            ⇒ 33
رأس الهجرات                            ⇒ 20260811142239_AddProject360Foundation
Migration #34                          ⇒ NONE
```

*(تحذيرات `query filter` الأربعة قائمة مسبقًا منذ W0 وغير حاجبة.)*

---

## AA. تدقيق النطاق

```text
Frontend files changed by W5      = 0   (11 مسارًا أماميًّا = 10 معدَّلة + 1 غير متتبَّع، مطابقة حرفيًّا لقائمة W0 §6)
Workstream files changed          = 0
Project Document files changed    = 0
Task Management files changed     = 0
Milestone files changed           = 0
Workflow redesign                 = 0
CRM files changed                 = 0
Finance module changed            = 0
Employee KPI module changed       = 0
Migration files added after W3    = 0
Commit / Push / Merge / PR / Tag  = 0
TEST / RC / Production deploy     = 0
```

*(المطابقات الأربع لكلمات `Document/Finance` في `git status` كلّها **ملفّات توثيق** تحت `Docs/Planning/` من تذاكر CPW-R1B2/CPW-R2 السابقة — صفر ملفّ كود.)*

---

## AB. حدود معروفة وبنود مرفوعة

1. **تسميات الكتالوج**: الرموز الـ38 منقولة حرفيًّا من R2/W1-A، أمّا تسمياتها العربيّة/الإنجليزيّة فترجمة حرفيّة للرمز لأنّ R2 لا يورد تسميات — قابلة للتحرير من شاشة الإدارة بصفر كود.
2. **`BASELINE-BE-DEFECT-03` (سبت المبيعات)** — 5 اختبارات قائمة تناقض هوتفكس 25 يوليو غير المودَع؛ خارج نطاق CPW-R3 ويحتاج قرار مالك.
3. **تضخّم `reporting_test`** — 10.3 مليون صفّ في `email_notifications` و136 ألف مستخدم يجعلان الانحدار الكامل يستغرق ~1 س 48 د ويُسقِط 18 اختبارًا بمهل زمنيّة. تنظيفها/تدويرها تذكرة بيئة مستقلّة (**لم تُنفَّذ هنا التزامًا بـ§32**).
4. **مقياس N+1 = 12 استعلامًا** مثبَّت كقيمة صريحة؛ أيّ إضافة قسم جديد إلى اللوحة ستُسقِط الاختبار عمدًا حتّى تُراجَع الكلفة.
5. **Project Documents** مؤجَّلة كليًّا إلى `CPW-R3-DOCS-WIRING-R1` (D-01).

---

## AC. البوّابة النهائيّة

```text
CPW-R3 — W5 FINAL GATE

Catalog Bootstrap:                  GO
Catalog Idempotency:                GO
Strategy Schema:                    GO
API Compatibility:                  GO
Authorization:                      GO
Team Leader Contract:               GO
Account Manager Contract:           GO
Objectives API:                     GO
KPI API:                            GO
Manual KPI Readings:                GO
Two-Level Weighted Aggregation:     GO
Contract Deliverables API:          GO
Risks / Decisions / Notes:          GO
Project Progress:                   GO
Project Overview:                   GO
Zero N+1:                           GO
IDOR:                               GO
Existing Project API Regression:    GO
Candidate Regression:               0
Baseline Defects:                   5
Shared-State / Order Dependent:     18
Unresolved:                         0
Build:                              GO
Model Sync:                         GO
Migration #34:                      NONE
Scope Clean:                        GO
Ready for W6 Integration Gate:      GO
Ready for Frontend:                 NO-GO
Ready for Commit:                   NO-GO
Ready for Push:                     NO-GO
Ready for TEST:                     NO-GO
Ready for Production:               NO-GO
```

**التوقّف مُلزَم هنا.** لا يبدأ W6 إلّا بعد اعتماد المالك لهذا التقرير وتصريح صريح جديد.
