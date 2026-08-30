# بيان نطاق إصدار R22A — الإصدار المحدود من قاعدة الإنتاج

> **القرار المُنفَّذ:** الخيار (ب) — `RELEASE_SCOPE = R22A_ONLY`.
> لم يُنشر `ff1e337` ولا أيّ جزء من التزاماته الـ187. الفرع بُني محلّيًّا من الالتزام
> المنشور على الإنتاج فعلًا، ولم يُمسّ RC ولا الإنتاج بأيّ عمليّة أثناء هذه المرحلة.

| مفتاح | قيمة |
|---|---|
| `PRODUCTION_BASE_SHA` | `897c9b187ab4216213b4f453ec65948cd06dff27` |
| `RELEASE_SCOPE` | `R22A_ONLY` |
| اسم الفرع | `release/r22a-from-897c9b18` |
| رأس الفرع | `4b8902eec9b67513d115dbc49c20af0dd62de8b8` |
| نسخة العمل المعزولة | `/Users/ibrahimelbahrawi/r22a-release` (worktree مستقلّ، خارج شجرة العمل الحاليّة) |
| `GIN_INDEX_MIGRATION_IN_R22A` | `EXCLUDED` |
| `R22A_REQUIRES_DATABASE_MIGRATION` | `NO` |
| `MINIMAL_RELEASE_MIGRATION_DELTA` | `0` |
| `R22A_MINIMAL_RELEASE_FEASIBLE` | `YES` |

---

## 1) إثبات إغلاق الاعتماديّات — قِيس ولم يُفترَض

### 1-1 موضع الالتزامَين المعتمدَين من قاعدة الإنتاج

```
git merge-base --is-ancestor 897c9b18 d934fc9  →  0  (897c9b18 سلفٌ لـd934fc9)
git merge-base --is-ancestor 897c9b18 36a6a5b  →  0  (897c9b18 سلفٌ لـ36a6a5b)
git rev-parse d934fc9^                          →  897c9b187ab4216213b4f453ec65948cd06dff27
```

**النتيجة الحاكمة:** الأب المباشر للالتزام `d934fc9` هو قاعدة الإنتاج `897c9b18` نفسها.
أي أنّ ميزة بنود العمل المتعدّدة **بُنيت أصلًا فوق ما يعمل على الإنتاج الآن** ⟹ صفر
التزامات وسيطة لازمة لها، والانتقاء ليس افتراضًا بل تطابق شجريّ.

### 1-2 المسار من قاعدة الإنتاج إلى إصلاح الحارس

بين `897c9b18` و`36a6a5b` ستّة التزامات. صُنِّفت بفحص ملفّاتها فعليًّا لا بعناوينها:

| # | الالتزام | التصنيف | ملفّات المنتج | القرار | السبب |
|---|---|---|---|---|---|
| 1 | `d934fc9` | كود ميزة | 10 (بعد التصفية) | **أُدخِل** | R22A مباشر — بنود العمل المتعدّدة |
| 2 | `f8c4ad2` | توثيق فقط | 0 | استُبعِد | 172 ملفًّا كلّها تحت `Ops/` (أدلّة P123/P360-NAVFIX) — لا يلزم للبناء ولا للتشغيل |
| 3 | `706c8fe` | كود إصلاح | 2 | **أُدخِل** | اعتماديّة حقيقيّة: يمنع انهيار محرّر التسليم على قيمة إجابة غير نصّيّة، وهو عيب داخل الميزة نفسها ويمسّ `SubmissionsPage.tsx` الذي غيّره `d934fc9` |
| 4 | `ac487de` | توثيق فقط | 0 | استُبعِد | 58 ملفًّا كلّها `Ops/R22/` (أدلّة إغلاق) |
| 5 | `16983d2` | توثيق فقط | 0 | استُبعِد | 6 ملفّات كلّها `Ops/R22/` |
| 6 | `36a6a5b` | كود إصلاح | 2 (+4 توثيق R22A) | **أُدخِل** | R22A مباشر — إصلاح حارس إنشاء إصدارات القوالب |

**أقلّ مجموعة اعتماديّات إضافيّة = التزام واحد (`706c8fe`)، وهو ملفّان يخصّان R22A ذاتها.**
لا يوجد أيّ التزام غير ذي صلة مطلوب للبناء أو التشغيل ⟹ `R22A_MINIMAL_RELEASE_FEASIBLE = YES`.

---

## 2) الالتزامات المُدخَلة على فرع الإصدار

| الالتزام على الفرع | مأخوذ من | التصنيف | الملفّات | السطور |
|---|---|---|---|---|
| `e6d4ad14560dafd1122f8b3dd52b15b3774b72c6` | `d934fc9` **مصفّى** | R22A مباشر | 10 | +1155 / −115 |
| `e510f5a95a04fbc66d53504613a7ae6871f42115` | `706c8fe` | اعتماديّة R22A | 2 | +56 / −2 |
| `4b8902eec9b67513d115dbc49c20af0dd62de8b8` | `36a6a5b` | R22A مباشر | 6 | +761 / −3 |

### 2-1 ملفّات الالتزام الأوّل بعد التصفية (10)

```
reporting-backend/src/Reporting.Application/Clients/ProjectReportSliceModels.cs             15 ±
reporting-backend/src/Reporting.Infrastructure/Services/ProjectService.cs                   97 ±
reporting-backend/src/Reporting.Infrastructure/Services/SubmissionService.cs               109 ±
reporting-backend/tests/Reporting.IntegrationTests/ProjectMultiWorkItemDiscoveryTests.cs   308 +
reporting-backend/tests/Reporting.IntegrationTests/ProjectScopedReportSliceTests.cs           6 ±
reporting-frontend/src/pages/ProjectMultiWorkItems.test.tsx                                328 +
reporting-frontend/src/pages/ProjectReportSlicePage.test.tsx                                  2 ±
reporting-frontend/src/pages/ProjectReportSlicePage.tsx                                      14 ±
reporting-frontend/src/pages/SubmissionsPage.tsx                                            357 ±
reporting-frontend/src/types/api.ts                                                          34 ±
```

### 2-2 ملفّات الالتزام الثاني (2)

```
reporting-frontend/src/pages/SubmissionsPage.tsx                16 ±
reporting-frontend/src/pages/ProjectMultiWorkItems.test.tsx     42 +
```

### 2-3 ملفّات الالتزام الثالث (6)

```
reporting-backend/src/Reporting.Infrastructure/Services/ReportTemplateService.cs        9 ±   [كود]
reporting-backend/tests/Reporting.IntegrationTests/TemplateVersionManagementTests.cs   80 +   [اختبار]
Ops/R22A/BUILD-AND-TEST-GATES.json                                                    161 +   [توثيق]
Ops/R22A/DUPLICATE-FILES-MANIFEST.json                                                 76 +   [توثيق]
Ops/R22A/REAL-TEMPLATE-ROOT-CAUSE.md                                                  162 +   [توثيق]
Ops/R22A/TEMPLATE-VERSION-IMPACT.json                                                 276 +   [توثيق]
```

---

## 3) استبعاد فهرس GIN — أثر واحد متماسك أُزيل كاملًا

القرار 2 يوجب استبعاد الهجرة وتعديل Model Snapshot المرتبط بها. الفحص أثبت أنّ أثر
الفهرس **ثلاثة مواضع لا موضعان**، وأنّ إزالتها الثلاثة معًا هي وحدها التي تُبقي النموذج
متّسقًا:

| # | الموضع | ماذا كان يحوي | الإجراء |
|---|---|---|---|
| 1 | `Persistence/Migrations/20260826185232_AddSubmissionFieldValueJsonGinIndex.cs` | 29 سطرًا: `CreateIndex` + `DropIndex` بصفر DML | حُذِف |
| 2 | `Persistence/Migrations/20260826185232_AddSubmissionFieldValueJsonGinIndex.Designer.cs` | 5359 سطرًا (لقطة مولَّدة) | حُذِف |
| 3 | `Persistence/Migrations/AppDbContextModelSnapshot.cs` | 6 أسطر تسجّل الفهرس في اللقطة | أُعيد إلى نصّ `897c9b18` |
| 4 | `Persistence/Configurations/SubmissionConfigurations.cs` | 7 أسطر: `HasIndex(ValueJson).HasMethod("gin").HasOperators("jsonb_path_ops")` | أُعيد إلى نصّ `897c9b18` |

الموضع الرابع لم يذكره القرار صراحةً، لكنّ إبقاءه مع حذف اللقطة كان سيُحدِث **تعارضًا
بين النموذج واللقطة** ويجعل EF يطالب بهجرة جديدة. حذفه هو التنفيذ الأمين لروح القرار
ونصّه معًا (`MINIMAL_RELEASE_MIGRATION_DELTA = 0`).

### 3-1 أدلّة عدم الاعتماد — أربعة مستقلّة

1. **تطابق نصّيّ كامل لطبقة الاستمرار مقابل الإنتاج:**
   ```
   git diff 897c9b18..HEAD -- .../Persistence/Migrations         → 0 أسطر
   git diff 897c9b18..HEAD -- .../Migrations/AppDbContextModelSnapshot.cs → 0 أسطر
   git diff 897c9b18..HEAD -- .../Persistence/Configurations     → 0 أسطر
   ```
2. **مجموعة ملفّات الهجرات متطابقة حرفيًّا:** 44 ملفًّا على `897c9b18` و44 على رأس الفرع،
   و`diff` لقائمتَي الملفّات فارغ (`IDENTICAL_MIGRATION_SET`).
3. **شهادة EF نفسها:**
   `dotnet ef migrations has-pending-model-changes` ⟹
   **`No changes have been made to the model since the last migration.`**
4. **شهادة سلوكيّة:** 34 اختبار تكامل حاكم لـR22A
   (`ProjectMultiWorkItemDiscoveryTests` · `ProjectScopedReportSliceTests` ·
   `TemplateVersionManagementTests`) **نجحت 34/34 على قاعدة نظيفة والفهرس غائب تمامًا**
   ⟹ شرط الاحتواء البنيويّ `jsonb` يعمل بلا الفهرس؛ الفهرس تسريعٌ لا شرط صحّة.

نصّ الالتزام الأصليّ نفسه يقرّ بذلك: «والهجرة المرفقة فهرس GIN فقط بصفر DML».
الفهرس مُرحَّل إلى تذكرة أداء مستقلّة (حجم الجدول والفهرس، زمن البناء، نوع القفل،
`CREATE INDEX CONCURRENTLY`، نافذة الصيانة، مراقبة الحمل، خطّة التراجع).

---

## 4) إثبات استبعاد ما سواه

| مقياس | الإصدار المحدود | نشر `ff1e337` كاملًا | النسبة |
|---|---|---|---|
| ملفّات مغيَّرة إجمالًا | **16** | 281 | 5.7% |
| أسطر مضافة | **1,970** | 19,769 | 10.0% |
| أسطر محذوفة | 118 | 118 | 100% |
| ملفّات كود المنتج | **12** (+1,295 / −118) | — | — |
| ملفّات توثيق `Ops/` | 4 (+675) | — | — |
| هجرات قاعدة بيانات جديدة | **0** | 1 (فهرس GIN) | — |

**ما استُبعِد صراحةً ولم يدخل الفرع:** أعمال الإجازات، والبريد، وKPI، ومدير الحساب،
وكلّ ما عدا ما ذُكر في §2 — لأنّ الفرع لم يُبنَ بدمج `develop` أصلًا، بل بانتقاء ثلاثة
التزامات فوق `897c9b18` مباشرةً. الدليل هو جدول الفرق أعلاه: 16 ملفًّا لا 281.

**الملفّات المحلّيّة (القرار 3):** تعديل `CLAUDE.md` لم يُدرَج؛ وملفّا `… 2.md` لم يُحذفا
ولم يُدرَجا؛ ولم يُدرَج أيّ ملفّ غير متتبَّع. الفرع بُني من مصدر معزول لا من شجرة العمل الحاليّة.

---

## 5) بوابات المرحلة 1 — القياس المحلّيّ

| البوّابة | النتيجة | الدليل |
|---|---|---|
| نظافة شجرة نسخة الإصدار | **PASS** | `git status --short` فارغ |
| `MINIMAL_RELEASE_BUILD` | **PASS** | `dotnet build Reporting.sln -c Release` — 0 أخطاء، 4 تحذيرات `CS8604` سابقة الوجود في ملفّات لم تمسّها R22A |
| اختبارات الوحدة | **PASS** | 556/556 |
| `TemplateVersionManagementTests` + `ProjectMultiWorkItemDiscoveryTests` + `ProjectScopedReportSliceTests` | **PASS** | 34/34 على قاعدة معزولة نظيفة `reporting_r22a_min` |
| حوكمة القوالب (`Phase4TemplateGovernance` · `TemplateRoleGuard` · `TemplateTaxonomyV4` · `ReportTemplate`) | **PASS** | 48/48 |
| كامل مجموعة اختبارات التكامل | **PASS** | **2212/2212** بصفر إخفاق في 7د 51ث — بما فيها الإخفاقات الثمانية المرصودة سابقًا في `UnifiedReportStatusTests` (تأكيد أنّها حسّاسة لليوم لا للكود) |
| `MINIMAL_RELEASE_FRONTEND` — TypeScript | **PASS** | `npx tsc --noEmit` بلا مخرجات |
| اختبارات الواجهة | **PASS** | 767/767 في 65 ملفًّا |
| بناء إنتاج الواجهة | **PASS** | `vite build` نجح |
| `MINIMAL_RELEASE_MIGRATION_DELTA` | **0** | §3-1 |
| `MINIMAL_RELEASE_SECRET_SCAN` | **PASS** | مسح كامل الفرق: الإصابات الوحيدة كلمة `SECRET_SCAN` داخل ملفّ بوّابات R22A و`CancellationToken` في تواقيع الدوالّ — لا أسرار |

قاعدة الاختبار المعزولة `reporting_r22a_min` أُنشئت بـ`createdb` ولم تُمسّ `reporting_test`
المشتركة الملوَّثة، ولا أيّ قاعدة حيّة.
