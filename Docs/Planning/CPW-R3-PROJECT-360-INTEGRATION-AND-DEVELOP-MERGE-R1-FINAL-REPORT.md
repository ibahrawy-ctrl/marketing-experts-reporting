# CPW-R3 — تقرير التكامل والدمج على `develop` وبوّابة إصدار TEST (R1) — التقرير النهائيّ

**التاريخ:** 15 أغسطس 2026 · **النطاق:** المراحل 1–12 من التوجيه الحاكم.
**النتيجة المختصرة:** الدمج على `develop` **تمّ**؛ النشر على TEST **متوقّف بمانع نَسَب حقيقيّ** (تفصيله في `CPW-R3-TEST-LINEAGE-BLOCK-AND-CPW-R2-RECONCILIATION-R1-REPORT.md`).

---

## 1) سلامة المرشَّح (Candidate Integrity)

| البند | القيمة المثبَتة |
| --- | --- |
| الفرع البعيد | `refs/heads/feature/cpw-r3-project-360-candidate-r1` |
| الرأس البعيد | `78c8a2d6996e614b979f0e4ff479b7233fe43946` |
| الأساس المشترك مع `origin/develop` | `6859ee0d51bef574a4dc4623c015817af325e78c` |
| متقدّم/متأخّر عن `origin/develop` قبل الدمج | **13 متقدّمًا · 0 متأخّرًا** |
| ملفّات محذوفة أو معادة التسمية | **0** (`--diff-filter=DR` فارغ) |
| ملفّات أسرار/سجلّات/قواعد/مخلَّفات بناء | **0** (فحص `.env`/`secret`/`credential`/`.pem`/`.key`/`.log`/`.sql`/`.dump`/`bin`/`obj`/`node_modules`/`dist`/`appsettings.*.json` = لا تطابق) |
| سلاسل شبيهة بالأسرار في الأسطر المضافة | **0** |
| ملفّات خارج نطاق CPW-R3 | **0** — كلّ الملفّات ضمن `Docs/Planning/CPW-R3-*` أو `Projects360`/`project360` أو ملفّات ربط إلزاميّة |
| ملفّات تخصّ Documents/Tasks/CRM/Finance | **0** |

**الحكم:** المرشَّح سليم بنيويًّا. لم يُعَد بناؤه لأنّه **لم يظهر أيّ ملفّ فعليّ خارج النطاق**. ما ظهر هو **خطآن توثيقيّان** فقط، صُحّحا في القسم 2.

---

## 2) الالتزامات الفعليّة + **مُلحَق تصحيح (Addendum)** لأرقام التقرير السابق

### 2-أ) تصحيحان توثيقيّان (لا يعطّلان الدمج، طبقًا لـ§3)

| # | ما ورد في التقرير السابق | **الصواب المُثبَت** | كيف ثبت |
| --- | --- | --- | --- |
| **ADD-01** | «12 التزامًا» فوق `develop` | **13 التزامًا** فوق `origin/develop`: **12** لـCPW-R3 + **1** موروث هو `c157829` | `git rev-list --left-right --count origin/develop...<candidate>` = `0 13` |
| **ADD-02** | «68 ملفًّا · ~+22,300 سطر» | **82 ملفًّا · +22,714 · −1** لمدى CPW-R3 وحده (`c157829..78c8a2d`) | `git diff --shortstat`؛ ومجموع إحصاءات الالتزامات الاثني عشر يطابقه بالضبط (14+7+5+10+2+7+4+3+12+2+14+2 = 82) |

**تفسير ADD-01 (مهمّ تشغيليًّا):** الالتزام `c157829` («close ADMIN-GOVERNANCE-R1 as the official develop EF baseline») كان رأس `develop` **المحلّيّ** لكنّه **لم يُدفَع قطّ** إلى `origin/develop`. لذلك حمل المرشَّح — بحكم بنائه فوقه — نشرَه أيضًا. هذا **مقصود ومتّسق مع عنوان الالتزام نفسه**، لكنّه لم يكن موثَّقًا. أثره الملموس: الدمج نشر أيضًا الهجرة `20260713171040_AdminGovernanceReportKpiCorrection`.

### 2-ب) الالتزامات الثلاثة عشر بالتجزئة الكاملة

| # | Hash كامل | العنوان | ملفّات | إضافة/حذف | نطاق CPW-R3؟ |
| --- | --- | --- | --- | --- | --- |
| 0 | `c157829f750ce98b7e7aad451a23183b58462cb4` | feat(governance): close ADMIN-GOVERNANCE-R1 as the official develop EF baseline | 25 | +5455 / −47 | **لا — موروث (ADMIN-GOVERNANCE-R1)** |
| 1 | `6236234cc27812d8be520e379e4524b55db08b03` | feat(projects360): add Project 360 domain entities, enums and health value objects (W1) | 14 | +902 | نعم |
| 2 | `8e66c12132bb7478a16f1eba5a51e9203eef3faf` | feat(projects360): map Project 360 schema and add additive migration #33 (W3) | 7 | +5898 | نعم |
| 3 | `0d0305a48b1584db92820c407282bb4e5462c63b` | feat(projects360): add application contracts, DTOs and ProjectHealthPolicy (W4) | 5 | +1385 | نعم |
| 4 | `5e6da71c133483c8e03cb768b6d0fdd928b93e34` | feat(projects360): implement services, persisted health and anti-enumeration guards (W4/W6) | 10 | +1977 | نعم |
| 5 | `56c706e829038014d0b2925ca209dbfc55b825f8` | feat(projects360): seed the 38-value Project 360 catalog as data, not code (W5 · DEC-W4-01) | 2 | +72 / −1 | نعم |
| 6 | `1c7ab16b77f118e95a7985a84300b8e95384c55e` | feat(projects360): expose the 34-route Project 360 API surface (W5/W6) | 7 | +343 | نعم |
| 7 | `74d6ca269c4c2b487d498130f0932a97580cd624` | test(projects360): cover health policy, API surface and anti-enumeration (W6-IG) | 4 | +2466 | نعم |
| 8 | `7bd73da7dc08f0c3f1d39279f81a72f777bc4bb3` | feat(projects360): add typed Project 360 client models, hooks and formatters (R2-W12) | 3 | +1234 | نعم |
| 9 | `0494c2b1838e99e603c24d31c1562ebc164677e5` | feat(projects360): build the Project 360 workspace with lazy tabs and server-owned health (R2-W12) | 12 | +1767 | نعم |
| 10 | `fbf7983da5e8dcb49621656ad136e0969a2bcbd4` | test(projects360): assert single-call overview, lazy tabs and zero client-side health math (R2-W12) | 2 | +813 | نعم |
| 11 | `5dca2a303a3d10f1f721629b212d0936b6717fad` | docs(projects360): record the CPW-R3 design, gates, findings and candidate lineage | 14 | +5440 | نعم |
| 12 | `78c8a2d6996e614b979f0e4ff479b7233fe43946` | docs(projects360): close CPW-R3 with the commit/push manifest and final candidate verdict | 2 | +417 | نعم |

**لا التزام مفقودًا ولا غير موثَّق** بعد إضافة السطر 0.
**توزيع الـ82 ملفًّا:** `reporting-backend/src` 45 · `reporting-frontend/src` 17 · `Docs/Planning` 16 · `reporting-backend/tests` 4. (70 إضافة `A` + 12 تعديل `M`.)

---

## 3) أحدث `develop` كأساس جديد

`git fetch origin --prune` غير متلِف. القراءة عند بدء الجولة وعند لحظة الدمج **متطابقة**:

```
origin/develop = 6859ee0d51bef574a4dc4623c015817af325e78c   (لم يتحرّك)
origin/main    = 508509ad8474b321c80cbdd48eb84ecb54bee212   (لم يُمَسّ)
```

**`develop` لم يتحرّك منذ آخر قياس** ⟹ لا التزامات جديدة تحتاج تصنيف أثر، ولا حاجة إلى إعادة تشغيل بوّابة بسبب حركة.
ملاحظة نَسَب مهمّة: `c157829` **ليس** سلفًا لـ`origin/develop` (`merge-base --is-ancestor` = NO) — وهذا مصدر ADD-01.

---

## 4) متقدّم/متأخّر والأساس المشترك

| القياس | القيمة |
| --- | --- |
| `merge-base(origin/develop, candidate)` | `6859ee0` |
| المرشَّح متقدّم | 13 |
| المرشَّح متأخّر | **0** |
| نوع الدمج الممكن | **Fast-forward نظيف** |
| اصطلاح المستودع | خطّيّ بالكامل (0 التزام دمج في تاريخ `origin/develop`) |

---

## 5) تعارضات التكامل

شجرة تكامل معزولة: `/tmp/cpw-r3-integration-20260815` على فرع `integration/cpw-r3-develop-r1` من `origin/develop`.

| الفحص | النتيجة |
| --- | --- |
| دمج تجريبيّ `--no-commit --no-ff` | `Automatic merge went well` |
| ملفّات بحالة `U` (تعارض) | **0** |
| الدمج النهائيّ | `--ff-only` ⟹ نجح |
| رأس شجرة التكامل | `78c8a2d…` (مطابق للمرشَّح حرفيًّا) |
| نظافة شجرة العمل | 0 ملفّ معدَّل |

---

## 6) قرارات حلّ التعارض

**لا يوجد** — صفر تعارض. لم يُستعمل `ours`/`theirs`، ولم يُعَد أيّ فرع منشور بـrebase، ولم يُعمَل داخل الشجرة الأصليّة المتّسخة (التي بقيت على `develop` عند `c157829` بملفّاتها الـ190 تخصّ تذاكر أخرى، بلا مساس).

---

## 7) التزامات إصلاح التكامل

**0** — لم تظهر أيّ حاجة إلى إصلاح (لا تعارض، ولا انحدار ناتج عن الدمج).

---

## 8) نتائج الخلفيّة (Backend)

| البوّابة | النتيجة |
| --- | --- |
| `dotnet restore` | نجح |
| `dotnet build Reporting.sln -c Debug` | **Build succeeded · 0 Errors · 1 Warning** (تحذير أساس سابق في `ReportTemplateTests.cs:121`) |
| اختبارات الوحدة | **115 / 115 ناجحًا · 0 فاشل** (18 ms) |
| `Project360FoundationTests` | **20 / 20** |
| `Project360ApiSurfaceTests` | **14 / 14** |
| `Project360HealthAndAuthorizationTests` | **18 / 18** |
| **مجموع اختبارات Project 360 الجديدة** | **52 / 52** |
| Model Sync (`has-pending-model-changes`) | **«No changes have been made to the model since the last migration.»** |
| جرد الهجرات في الكود | **33** · الرأس `20260811142239_AddProject360Foundation` · **لا #34** |
| جرد المسارات | **34** = Objectives 8 · Kpis 10 · Strategy 3 · ContractDeliverables 8 · Overview 1 · GovernanceRead 3 · Health 1 |
| مسار إنشاء KPI على مستوى المشروع | **غير موجود** — المسار الوحيد على مستوى المشروع هو `[HttpGet("kpis")]`، وكلّ الإنشاء تحت `objectives/{objectiveId}/kpis` |

---

## 9) نتائج الواجهة (Frontend)

| البوّابة | النتيجة |
| --- | --- |
| `npm ci` | نجح |
| فحص الأنواع (`tsc -b` ضمن `npm run build`) | **نظيف — صفر خطأ** |
| `npm test` (vitest 4.1.8) | **271 / 271 ناجحًا · 30 ملفّ اختبار** (5.51 ث) |
| اختبارات مكوّنات Project 360 | **44 / 44** (`Project360Page.test.tsx` + `project360Tabs.test.tsx`) |
| `npm run build` (vite/rolldown) | **✓ built in 929ms** |
| `eslint .` | **31 مكتشفًا** (18 خطأ + 13 تحذيرًا) — كلّها أساس سابق في 14 ملفًّا |
| مكتشفات ESLint داخل أيّ ملفّ Project 360 | **0** |
| Playwright E2E | **1 / 1 ناجحًا** (8.9 ث) |

> `npm audit fix` **لم يُنفَّذ** — محظور صراحةً في §2.

---

## 10) الانحدار الكامل على قاعدة معزولة

القاعدة: `reporting_cpwr3_int` (أُنشئت بـ`createdb`، لم تُستعمل `reporting_test` الملوَّثة إطلاقًا).

| المجموعة | إجمالي | ناجح | فاشل | الزمن |
| --- | --- | --- | --- | --- |
| Backend Unit | 115 | 115 | 0 | 18 ms |
| Backend Integration | **1,462** | **1,460** | **2** | 4 د 46 ث |
| Frontend (vitest) | 271 | 271 | 0 | 5.5 ث |
| E2E (Playwright) | 1 | 1 | 0 | 8.9 ث |

---

## 11) تصنيف الفشل — **بالدليل التجريبيّ المباشر**

بدل الاعتماد على التصنيف القديم، شُغِّل **الانحدار الكامل نفسه على التزام الأب `c157829`** (أي الأساس المباشر للمرشَّح، بلا أيّ كود CPW-R3) على قاعدة معزولة ثالثة `reporting_cpwr3_parent`:

| | الأب `c157829` | المرشَّح `78c8a2d` | الفارق |
| --- | --- | --- | --- |
| إجمالي | **1,410** | **1,462** | **+52** (= 20+14+18 اختبارات Project 360 بالضبط) |
| ناجح | 1,408 | 1,460 | +52 |
| **فاشل** | **2** | **2** | **0** |
| الزمن | 4 د 17 ث | 4 د 46 ث | — |

**الفاشلان متطابقان حرفيًّا في الحالتَين:**
1. `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete` — `Assert.Equal() Failure: Values differ · Expected: OK`
2. `EmployeeProfileScopeTests.Profile_Summary_Reflects_Submitted_Kpi` — `Assert.NotNull() Failure: Nullable<decimal> does not have a value`

**التصنيف النهائيّ: عيبا أساس (Baseline Defects) موروثان من `c157829`، وليسا انحدار مرشَّح.**
- كلا ملفَّي الاختبار **لم يمسّهما CPW-R3 إطلاقًا** (`git diff --name-only c157829 78c8a2d` لا يذكرهما)، و`EmployeeProfileScopeTests.cs` **متطابق بايتًا ببايت** بين `origin/develop` والمرشَّح.
- `AdminGovernanceTests.cs` **أضافه** الالتزام `c157829` نفسه.

### تصحيح إلزاميّ لسجلّ سابق
`BASELINE-DEFECT-01` (اختبار AdminGovernance) كان قد صُنِّف في وقت سابق من اليوم بأنّه **«تلوّث بيئة»** لأنّه نجح في جولة واحدة على قاعدة نظيفة. **هذا التصنيف خاطئ ويُلغى:** الاختبار فشل الآن على **قاعدتَين نظيفتَين مستقلّتَين** (المرشَّح والأب). السلوك الصحيح: **عيب أساس ذو حساسيّة للترتيب/الحالة (order-dependent)** — ينجح منفردًا ويفشل ضمن المجموعة الكاملة.
كذلك `EmployeeProfileScopeTests` **نجح 6/6 عند تشغيله منفردًا** على `origin/develop` وقاعدة نظيفة، وفشل ضمن المجموعة الكاملة ⟹ الطبيعة نفسها.

**لم يُوسَّع نطاق CPW-R3 لإصلاحهما** التزامًا بـ§7. يستحقّان تذكرة مستقلّة: «عزل حالة الاختبارات المشتركة في AdminGovernance/EmployeeProfileScope».

---

## 12) حالة الهجرات

| البند | القيمة |
| --- | --- |
| عدد الهجرات في `origin/develop` قبل الدمج | **31** |
| عدد الهجرات في المرشَّح | **33** |
| المضافة بالدمج | **2**: `20260713171040_AdminGovernanceReportKpiCorrection` (من `c157829`) + `20260811142239_AddProject360Foundation` (CPW-R3) |
| هجرة #34 | **NONE** |
| تعديل على الهجرة 33 أو اللقطة يدويًّا | **لم يحدث** |
| رأس الهجرات المطبَّق على القاعدة المعزولة | `20260811142239_AddProject360Foundation` · العدد 33 |
| جداول Project 360 المُنشأة | 6: `project_objectives` · `project_kpis` · `project_kpi_readings` · `project_strategies` · `project_strategy_attributes` · `project_deliverables` |

---

## 13) المرشَّح مقابل أحدث `develop`

`develop` لم يتحرّك ⟹ الفارق كلّه هو الالتزامات الثلاثة عشر في القسم 2-ب. لا التزام على `develop` بعد `6859ee0` يحتاج تصنيف أثر على Projects / Authorization / Frontend Routing / API / EF-Migrations / Tests / الخدمات المشتركة.

**عقود §6 الحاسمة — كلّها مثبَتة:**

| العقد | المطلوب | المُثبَت |
| --- | --- | --- |
| Candidate Regression | 0 | **0** (1,462/1,460/2 مقابل 1,410/1,408/2 — فارق الفشل صفر) |
| Unresolved | 0 | **0** |
| Migration Count | 33 | **33** |
| Migration #34 | NONE | **NONE** |
| Catalog | 38 | **38** مبذورة (المصدر: 18 `contract_deliverable` + 14 `strategy_field` + 6 `strategy_section`؛ والقاعدة تُظهر 42 منها 4 أنشأتها الاختبارات بالبادئة `w4_test_`) |
| Duplicate Catalog | 0 | **0** (تجميع على `(Domain, Code)` بلا مجموعة >1) |
| Project-level KPI Create Route | NONE | **NONE** |
| Health Persistence | PASS | **PASS** — أعمدة `projects` الصحّيّة **ثلاثة بالضبط**: `HealthStatus varchar` · `HealthPercent numeric` · `HealthComputedAtUtc timestamptz` |
| Health Recompute | PASS | **PASS** (`POST /api/projects/{id}/health/recompute` مغطّى ضمن 18/18) |
| Health Reasons Stored | NO | **NO** — عدد أعمدة تحوي `reason` في جداول `project*` = **0** |
| Out-of-scope Project = 404 | PASS | **PASS** (مغطّى ضمن `Project360HealthAndAuthorizationTests` 18/18) |
| Missing Project = 404 | PASS | **PASS** (نفس الرمز والرسالة، بلا استعلام وجود عند فشل الرؤية) |
| Existing Project Routes Removed/Changed | 0 | **0** — `ProjectsController.cs` و`ClientsController.cs` **بلا أيّ تغيير** (`git diff --stat` فارغ) |
| Documents/Tasks/CRM/Finance Scope | untouched | **untouched** — صفر ملفّ مطابق للنمط |

---

## 14) نتيجة الدمج والدفع

```
الطريقة:      git push origin integration/cpw-r3-develop-r1:develop   (دفع عاديّ، بلا force، بلا rebase)
قبل:          origin/develop = 6859ee0d51bef574a4dc4623c015817af325e78c
بعد:          origin/develop = 78c8a2d6996e614b979f0e4ff479b7233fe43946
تحقّق ls-remote: 78c8a2d6996e614b979f0e4ff479b7233fe43946  refs/heads/develop   ⟹ MATCH ✓
النوع:        Fast-forward (6859ee0..78c8a2d)
حماية الفرع:  لم تُصادَف — الدفع قُبِل مباشرة، ولم يُلتَفّ على أيّ حماية
origin/main:  508509ad8474b321c80cbdd48eb84ecb54bee212  (بلا مساس)
```

`develop` **المحلّيّ لم يُحرَّك عمدًا** وبقي عند `c157829`: الشجرة الرئيسيّة عليها 190 مدخلًا متّسخًا تخصّ تذاكر أخرى، وتحريك المرجع كان سيغيّر حالتها — و§2 يمنع أيّ `Reset/Checkout` لتغييرات المستخدم. الدفع تمّ من الشجرة المعزولة بمرجع صريح، وهو مكافئ تمامًا ولا يمسّ عمل المستخدم.

---

## 15) تدقيق نَسَب TEST

**التفصيل الكامل في `CPW-R3-TEST-LINEAGE-BLOCK-AND-CPW-R2-RECONCILIATION-R1-REPORT.md`.** الخلاصة:

| البند | القيمة |
| --- | --- |
| الخدمة | `khubara-reporting-test` · `MainPID=812859` · `active` · `/opt/reporting-test/publish/Reporting.Api.dll` |
| البيئة | `ASPNETCORE_ENVIRONMENT=Staging` · `/etc/khubara-reporting-test.env` |
| القاعدة | `reporting_test_uat` · **34 هجرة** · 72 جدول `public` · 12 MB |
| رأس الهجرات | `20260809165617_ClientDocumentVisibility` |
| الالتزام المنشور | `3344f7800f223a97b2fd4429d92d8c3449f3cfd9` (فرع CPW-R2، **ليس سلفًا لـ`develop`**) |
| Project 360 على TEST | **غائب تمامًا** (0 رمز، و`/api/projects/{id}/overview` = 404) |
| `/health` | `HTTP 200` |
| **التصنيف** | **C + D** |

---

## 16) توافق CPW-R2

| السؤال | الجواب |
| --- | --- |
| مسارات CPW-R2 حاضرة على TEST؟ | **نعم** — `GET /api/clients/{id}/documents` = **401** (موجود، يتطلّب مصادقة) |
| واجهة مستندات العملاء حاضرة؟ | **نعم** — حزمة `index-QJsadsGt.js` تحوي `client-documents` و«المستندات» |
| صلاحيّات المستندات تعمل؟ | **نعم** — `DocumentAccessEvaluator` مبنيّ داخل `Reporting.Infrastructure.dll` (18 تطابق رمز) |
| هل سيستبدل نشر CPW-R3 ملفّات CPW-R2 أو يعطّلها؟ | **نعم — وهذا هو المانع.** المرشَّح لا يحوي أيًّا من 16 ملفّ CPW-R2؛ النشر يزيل المتحكّم والخدمة ومقيّم الوصول وواجهة المستندات، ويترك 7 جداول `client_document*` (بها صفّ فعليّ واحد) بلا تعيين في النموذج |
| البيانات ستُحذف؟ | **لا** — الهجرات إضافيّة والجداول تبقى؛ لكنّ الميزة تصبح غير قابلة للوصول |
| المصالحة ممكنة؟ | **نعم وصغيرة** — 5 ملفّات متقاطعة، **تعارضان نصّيّان فقط** (`AppDbContext.cs` ≈10 أسطر، `App.tsx` ≈8 أسطر)، كلاهما إضافيّ بحت. العقبة الحقيقيّة: إعادة توليد `AppDbContextModelSnapshot` + الحاجة إلى تصريح دمج CPW-R2 |

---

## 17) النسخ الاحتياطيّة على TEST

**لم تُؤخَذ — ولم يجز أخذها.** §11 يجعل النسخ الاحتياطيّ خطوةً تمهيديّة للنشر، والقرار في §10 كان **توقّفًا قبل النشر**؛ فأخذ نسخ احتياطيّة كان سيوحي ببدء مسار نشر لن يُستكمَل.
**جاهزيّة النسخ مثبَتة مسبقًا (قراءة-فقط):** `/dev/sda1` 96G منها 56G متاح؛ الأحجام المطلوبة صغيرة (القاعدة 12MB، `publish` 108MB، `dist` 1.5MB)؛ ومسار النسخ المعتمد `/root/db-backups/` يحوي فعلًا `reporting_test_uat-precpwr2def01-20260810-163737.dump`.

---

## 18) خطوات النشر

**لم تبدأ (NOT STARTED).** صفر أمر كتابة على TEST في هذه الجولة كلّها؛ التفاعل الوحيد مع الخادم كان قراءة (`systemctl show`، `ls`، `grep` على مفاتيح لا قيم أسرار، `psql` استعلامات `SELECT` فقط، `curl` على `/health` ومسارات غير مصادَقة، `strings`/`grep` على ثنائيّات).

## 19) تنفيذ الهجرة على TEST

**لم يُنفَّذ.** الهجرة `20260811142239_AddProject360Foundation` تبقى **معلَّقة** بالنسبة إلى `reporting_test_uat`. لم يُعدَّل `__EFMigrationsHistory` بأيّ صورة.

## 20) أدلّة الكتالوج

على القاعدة المعزولة (لا على TEST):

| الفحص | النتيجة |
| --- | --- |
| مصدر البذر `ExecutionTaxonomySeeder.cs` | 18 `contract_deliverable` + 14 `strategy_field` + 6 `strategy_section` = **38** |
| المبذور فعليًّا في القاعدة (باستثناء ما أنشأته الاختبارات) | **38** |
| الإجمالي الخام في نطاقات Project 360 | 42 (الفارق 4 قيم اختبار: `w4_test_section_a`, `w4_test_field_a`, `w4_test_field_b`, `w4_test_monthly_report`) |
| تكرارات `(Domain, Code)` | **0** |
| Catalog Bootstrap على TEST | **لم يُنفَّذ** (تابع لقرار النشر المتوقّف) · الوضع الحاليّ على TEST: 170 قيمة في 19 نطاقًا، بلا أيّ نطاق من نطاقات Project 360 |

## 21) اختبارات الدخان على TEST

**لم تُشغَّل (NOT RUN)** — لا يوجد ما يُدخَّن: Project 360 غير منشور. الفحوص القرائيّة التي أُجريت أثبتت فقط سلامة الوضع القائم: `/health` = 200، ومسارات Project 360 = 404 كما هو متوقَّع قبل النشر.

## 22) اختبارات الصلاحيّات على TEST

**لم تُشغَّل** — تابعة للنشر. غطّتها بالكامل على المستوى المنطقيّ 18 اختبار `Project360HealthAndAuthorizationTests` الناجحة محلّيًّا: 401 لغير المصادَق · 404 **غير مميَّز حرفيًّا** للمفقود وللخارج عن النطاق · 403 لمن يرى ولا يملك القدرة · `CanManage` محصورة في Admin/CEO/GeneralManager/Manager · `CanOperate` تشمل قائد الفريق/مدير الحساب **لهذا المشروع بعينه**. **لم يُستعمل أيّ حساب إنتاج.**

## 23) عدم انحدار CPW-R2

**0 انحدار** — بالبناء لا بالاختبار: لم تُنفَّذ أيّ عمليّة كتابة على TEST، فبقيت CPW-R2 كما هي بالضبط (401 على مسار المستندات، الواجهة المنشورة سليمة، صفّ `client_documents` الوحيد سليم، 34 هجرة بلا تغيير).

## 24) جاهزيّة التراجع (Rollback)

**لم يُحتَج إليها** — لا يوجد ما يُتراجَع عنه على TEST. أمّا على `develop` فالتراجع المفاهيميّ متاح ومحدَّد: `origin/develop` كان `6859ee0` وأصبح `78c8a2d`، والدمج fast-forward نظيف؛ أيّ تراجع مستقبليّ **يجب** أن يكون بـ`revert` لا بإعادة كتابة تاريخ (§2 يمنع Force Push وإعادة كتابة التاريخ وحذف الفروع البعيدة). فرع المرشَّح `feature/cpw-r3-project-360-candidate-r1` **باقٍ على `origin` بلا حذف** كنقطة مرجعيّة.

## 25) حزمة UAT

**لم تُجهَّز.** §16 يبني الحزمة على بيئة منشورة قابلة للاختبار، وهي غير متاحة بسبب مانع النَسَب. تجهيز الحزمة الآن كان سينتج وثيقة تصف بيئة غير موجودة.
**ما يلزم قبلها:** قرار المالك بين الخيارَين (أ) و(ب) في تقرير الحظر، ثمّ نشر ناجح، ثمّ بناء الحزمة على البيئة الفعليّة.
**لم يُنشَأ أيّ بيان اختبار `CPW-R3-UAT-*`** (§15 لم يُفعَّل).

## 26) المخاطر المتبقّية

| # | الخطر | الشدّة | الحالة |
| --- | --- | --- | --- |
| R-01 | تفرّع نَسَب TEST عن `develop` (CPW-R2 غير مدموج) | **عالية** | مفتوح — يحجب نشر TEST ويؤخّر UAT |
| R-02 | نشر `c157829` ضمنًا مع الدمج (هجرة `20260713171040`) | متوسّطة | **مقبول** — الالتزام نفسه معنون بأنّه أساس `develop` الرسميّ، وقد اجتاز الانحدار الكامل؛ موثَّق هنا في ADD-01 |
| R-03 | عيبا الأساس المزدوجان (AdminGovernance + EmployeeProfileScope) | متوسّطة | مفتوح — تذكرة مستقلّة؛ يفشلان معًا في المجموعة الكاملة وينجحان منفردَين |
| R-04 | `AppDbContextModelSnapshot` عند المصالحة | متوسّطة | مفتوح — يلزم إعادة توليد لا دمج نصّيّ |
| R-05 | تلوّث `reporting_test` (23 فشلًا مقابل 2، و~1س48د مقابل ~4.5د) | منخفضة | مُلتَفّ عليه بالقواعد المعزولة؛ يستحقّ تذكرة تنظيف |
| R-06 | 18 خطأ ESLint أساس سابق | منخفضة جدًّا | خارج النطاق — 0 منها في Project 360 |

## 27) البوّابة النهائيّة

| المعيار | النتيجة |
| --- | --- |
| سلامة المرشَّح | ✅ (تصحيحان توثيقيّان فقط، بلا ملفّ خارج النطاق) |
| صفر تعارض تكامل | ✅ |
| بوّابة الخلفيّة | ✅ |
| بوّابة الواجهة | ✅ |
| Candidate Regression = 0 | ✅ (مثبَت بمقارنة الأب) |
| Migration 33 · لا #34 | ✅ |
| Catalog 38 · تكرار 0 | ✅ |
| صحّة مخزَّنة 3 أعمدة · أسباب غير مخزَّنة | ✅ |
| مكافحة التعداد | ✅ |
| صفر مساس بالمسارات القائمة والنطاقات الأخرى | ✅ |
| الدمج على `develop` | ✅ |
| نشر TEST | ⛔ محجوب بمانع حقيقيّ (C + D) |

---

```
CPW-R3 Integrated into develop: YES
Develop Remote HEAD:            78c8a2d6996e614b979f0e4ff479b7233fe43946
Candidate Regression:           0
Unresolved:                     0

TEST Lineage:                   BLOCKED
TEST Deployment:                NOT STARTED
TEST Smoke:                     NOT RUN
CPW-R2 Regression:              0
Ready for UAT Authorization:    NO-GO
Ready for RC:                   NO-GO
Ready for Production:           NO-GO
```

**توقّف إلزاميّ.** لم تُبدأ UAT، ولم يُمَسّ RC أو الإنتاج، ولم يُربَط Project Documents، ولم تُبدأ أيّ تذكرة جديدة. الخطوة التالية بيد المالك: اختيار (أ) أو (ب) من تقرير الحظر.
