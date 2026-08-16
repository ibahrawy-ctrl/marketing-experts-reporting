# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 05: دفتر حلّ التعارضات

**التاريخ:** 16 أغسطس 2026 · **المرحلة:** E · **الدمج:** `ac0d86c` (200 ملفًّا · +42223 / −4148)

الدمج **حقيقيّ حافظ للتاريخ** (`git merge` بأبوَين)، لا rebase ولا squash ولا cherry-pick.
الرمز `o` = جانب develop، `t` = جانب الإنتاج، `b` = الاتّحاد (develop ثمّ الإنتاج).

## 1. الملفّات الأربعة والعشرون المتعارضة

| # | الملفّ | القِطَع | القرار | المبرّر |
|---|---|---|---|---|
| 1 | `Reporting.Api/Program.cs` | 1 | `t` | يضيف سياسة `ArchiveGovernanceAccess` الحيّة |
| 2 | `Infrastructure/DependencyInjection.cs` | 1 | `o` | يُبقي `IProjectWorkstreamService`/`IWorkstreamDeliverableService`/`IPositionService` |
| 3 | `Persistence/AppDbContext.cs` | 1 | `o` | يُبقي `using` كيانات المناصب ومشاريع 360 |
| 4 | `Configurations/KpiConfigurations.cs` | 1 | `t` | الفهرس الفريد **الجزئيّ** الحيّ `.HasFilter("\"IsDeleted\" = false")` |
| 5 | `Application/Common/Roles.cs` | 2 | `t,t` | يضيف `ArchiveGovernanceAccessors` و`ArchiveGovernanceAccess` |
| 6 | `Application/Common/ReportCalendarPolicy.cs` | 1 | `t` | نسخة الإنتاج الكاملة من `PreviousPeriodKey` (انظر §3) |
| 7 | `Submissions/ISubmissionService.cs` | 1 | `t` | يضيف `GetOverviewAsync` |
| 8 | `Services/DashboardService.cs` | 1 | `t` | عدّادات الحالة المتوقَّعة Users-first تُعرّف المتغيّرات التي كان بلوك develop يُعيد تعريفها |
| 9–14 | `ProjectFirstExecution*` (Controller/Schema/IService/Models/Service/Tests) | add/add | `--theirs` | إصدار الإنتاج R1-V2 يَخلُف أساس develop RC-4 (Schema 129 سطرًا مقابل 31) |
| 15 | `Migrations/20260713171040_*.Designer.cs` | 12 | `--ours` | ملفّ الهجرة `.cs` متطابق md5 على الخطَّين؛ الـDesigner يعكس النموذج الأغنى |
| 16 | `Migrations/AppDbContextModelSnapshot.cs` | 3 | `--ours` ثمّ إعادة توليد | انظر التقرير 02 §4 |
| 17 | `Services/KpiEvaluationService.cs` | 6 | `t` | `ResolveReviewerWithOverrideAsync`، تعداد `ReviewerResolution`، اعتماد SelfOverride المباشر، توجيه سلسلة الموضوع بـ`BypassTeamLeaderApproval` |
| 18 | `Services/SubmissionService.cs` | 3 | `t` | لقطة سير الأرشفة قبل الحذف + `RepeatableNumericValidation` المدفوعة بالقالب |
| 19 | `tests/AdminGovernanceTests.cs` | 3 | `t` | مجموعة الإنتاج **فوقيّة**: تضيف حالات منع HR/CeoSupport/AccountPortfolioReader + 3 اختبارات توجيه مراجِع KPI |
| 20 | `frontend/src/App.tsx` | 1 | `o` | يُبقي `PROJECT_360_ROLES`/`CLIENT_360_ROLES`/`GOVERNANCE_WORKSPACE_ROLES` |
| 21 | `frontend/src/types/api.ts` | 2 | `b,b` | **اتّحاد**: `catalogDomain` من develop + `min/max/integerOnly/step` من الإنتاج؛ وDTOs المشاريع من develop + DTOs التقويم/الموحّد/الأرشيف من الإنتاج |
| 22 | `frontend/src/lib/navConfig.ts` | 13 | `o×4,t,o×8` + ترقيع يدويّ | بنية UI-NAV-RESTRUCTURE-R2 من develop + فصل تقاريري/تقارير الفريق من الإنتاج + إضافة `ARCHIVE_GOVERNANCE` يدويًّا |
| 23 | `frontend/src/components/DashboardShell.tsx` | 5 | `o` | `handleLogout` في نسخة الإنتاج يشير إلى `logout`/`navigate` غير مُعرَّفَين في البنية الجديدة؛ الوظيفة محفوظة في `ProfileMenu` |
| 24 | `frontend/src/components/DashboardShell.nav.test.tsx` | 3 | `o,o,t` | القطعة الثالثة تطابق تبويبات التنقّل الجديدة |

## 2. الحذف الصامت المُبطَل

20 ملفًّا من develop كانت ستُحذف بلا علامة تعارض (تفاصيلها في التقرير 01 §4)، أُعيدت حرفيًّا من
`origin/develop`، وأُتبِعت بخمس إصلاحات بناء:

| الإصلاح | الملفّ |
|---|---|
| إعادة تعداد `PositionScopeKind` | `Domain/Enums/Enums.cs` |
| إعادة `DbSet` الأربعة للمناصب | `Persistence/AppDbContext.cs` |
| إعادة `using Reporting.Application.Positions;` | `Infrastructure/DependencyInjection.cs` |
| إعادة ثابت `Policies.PositionManagement` | `Application/Common/Roles.cs` |
| إعادة تسجيل السياسة (Admin فقط) | `Reporting.Api/Program.cs` |

## 3. أخطاء البناء التي واجهناها وعلاجها

| الخطأ | السبب الجذريّ | العلاج |
|---|---|---|
| `CS0246: PositionScopeKind` | نَسَب الإنتاج حذف التعداد والدمج احترم الحذف | إعادته حرفيًّا من develop |
| `CS0106: modifier 'public' is not valid` في `ReportCalendarPolicy` | اختيار `o` أدرج دالّة `PreviousPeriodKey` كاملة **داخل** نسخة الإنتاج المدموجة تلقائيًّا من الدالّة نفسها | `git checkout -m --` لإعادة علامات التعارض ثمّ إعادة الحلّ بـ`t` |
| 37 خطأ `Positions`/`IPositionService`/`KeyPlanned` | (أ) الحذف الصامت (ب) بلوك تحقّق المفاتيح العامّة في develop يشير إلى ثوابت لم تعد في مخطَّط الإنتاج الخالِف | استعادة DbSets وthe using + حذف البلوك المُتجاوَز |
| `CS0117: Policies.PositionManagement` | الثابت وتسجيل السياسة محذوفان | إعادتهما |

**إثبات تجاوز البلوك المحذوف** (أسطر 1544–1568 من `SubmissionService.cs`): نسخة الإنتاج لا تحوي
أيّ إشارة إلى `ProjectFirstExecutionSchema.Key*`، وتستعمل مفاتيح v5 الحقيقيّة
(`required_pieces`, `delivered_pieces`, `approved_first_time`, `incoming_messages`, `answered_messages`,
`complaints`, `escalations`) عبر `MetricKeyMap`/`MapFor`؛ والبلوك كان مغلقًا على نفسه
(`isExec`, `isProductionExec`, `isModerationExec`, `canonicalNumericKeys`, `LabelOf`, `fieldKeys`
بلا أيّ استعمال لاحق).

## 4. إصلاح الانحدار الوحيد في الواجهة

`reporting-frontend/src/pages/ProjectRepeatableGrid.test.tsx` — بعد التوحيد صار محرّر الحقول
المنسدلة يقرأ خيارات الكتالوج عبر TanStack Query (`useTaxonomyOptions`)، فسقطت 7 اختبارات بـ
`No QueryClient set`. العلاج: غلاف تصيير واحد بـ`QueryClientProvider` **بلا أيّ تغيير في سلوك
المكوّنات**. النتيجة: 51/51 في الملفّ و**548/548** في الواجهة كلّها (45 ملفّ اختبار).

## 5. بوّابات البناء الخضراء

| البوّابة | النتيجة |
|---|---|
| `dotnet build Reporting.sln -c Debug` | `0 Error(s)` |
| `npx tsc --noEmit` | exit 0 |
| `npx vitest run` | 45 ملفًّا · **548/548** |
| `npx vite build` | ✓ built |
| اختبارات الوحدة الخلفيّة | **359/359** (مقابل 313 على الأب الإنتاجيّ — مجموعة فوقيّة) |
