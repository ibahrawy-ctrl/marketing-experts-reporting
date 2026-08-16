# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 15: الانحدار النهائيّ وبروتوكول القياس المُلزِم

**التذكرة:** `RECONCILE-PROD-DEVELOP-LINEAGE`
**المرحلة:** G/H — التقرير 7 في تسلسل المستخدم
**التاريخ:** 16 أغسطس 2026
**الحكم:** **الانحدار الموحّد الحقيقيّ = 0** · مُثبَت بجولتَين متطابقتَين مستقلّتَين

---

## 1) النتيجة النهائيّة (مُكرَّرة مرّتين)

| الجولة | الشجرة | التكاملي | الوحدوي | المدّة |
|---|---|---|---|---|
| `cand5.log` | المرشَّح `ac0d86c` + إصلاحات G | **Failed 1 / Passed 1981 / Total 1982** | 359/359 | 7 د 24 ث |
| `cand8.log` | نفس الالتزام، نفس الإجراء | **Failed 1 / Passed 1981 / Total 1982** | 359/359 | 7 د 26 ث |
| `cand10-full.log` | نفس الالتزام **+ 11 اختبار انحدار جديدًا** (H2) | **Failed 1 / Passed 1992 / Total 1993** | 359/359 | 7 د 29 ث |

الجولة الثالثة (`cand10`) أُضيف إليها 11 اختبار انحدار للميزات الإنتاجيّة المستعادة، فارتفع المجموع 1982 → 1993 (+11) والنجاح 1981 → 1992 (+11) و**الفشل ثابت عند 1**: الاختبارات الجديدة خضراء بالكامل ولم تُحدث أيّ ارتداد.

الفشل الوحيد في الجولات الثلاث هو نفسه حرفيًّا:
```
Reporting.IntegrationTests.AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete
Assert.Equal() Failure — Expected: OK · Actual: NotFound  (AdminGovernanceTests.cs:366)
```
⟹ **Class C — عيب تاريخيّ مشترك**؛ يفشل بنفس التوقيع على `10c26f7` و`ce166662` معًا ⟹ `BASELINE-DEFECT-01`.

---

## 2) مقارنة الأشجار الأربعة على قواعد نظيفة متكافئة

| الشجرة | الالتزام | الفشل التكامليّ | المجموع |
|---|---|---|---|
| قاعدة التفرّع | `6fd2253` | 29 | — |
| `develop` | `10c26f7` | **2** | — |
| **Production/RC** | **`ce166662`** | **165** | 1778 |
| **المرشَّح الموحّد** | `ac0d86c` + إصلاحات G | **1** | 1982 |

**الدلالة:** المرشَّح ليس وسطًا بين الأبوين — بل **أفضل منهما معًا**: يشفي 130 من احمرار الإنتاج، ويحمل عددًا أكبر من الاختبارات (1982 مقابل 1778 و1879 اسمًا فريدًا).

---

## 3) بروتوكول القياس المُلزِم (استُخلص من ثلاثة قياسات باطلة)

خلال هذه المرحلة **بطلت ثلاث جولات** لأسباب إجرائيّة لا برمجيّة. البروتوكول التالي إلزاميّ لأيّ قياس لاحق:

### 3.1 الخطوات

```bash
# (0) تصدير سلسلة اتّصال القاعدة الرئيسة — إلزاميّ في *نفس* الصَدَفة قبل أيّ نداء dotnet test.
#     CustomWebApplicationFactory.cs:17-19 تقرأ TEST_DB_CONNECTION وتسقط افتراضيًّا إلى
#     reporting_test الملوَّثة عند غيابه ⟹ إغفال هذا السطر يُبطل الجولة كلّها بصمت.
export TEST_DB_CONNECTION="Host=localhost;Database=<main>;Username=<user>"

# (1) قواعد جديدة تمامًا — لا إعادة استعمال
for d in <main> <cal> <pfe>; do dropdb --if-exists "$d" && createdb "$d"; done

# (2) تمهيد متتابع للقواعد الثلاث — بالترتيب، لا بالتوازي
dotnet test … --filter "FullyQualifiedName~HealthTests"                        # main
dotnet test … --filter "FullyQualifiedName~DailyCalendarTests"                 # cal
dotnet test … --filter "FullyQualifiedName~ProjectFirstExecutionAggregationTests"  # pfe

# (3) عبارة التصنيف على main حصرًا — خمسة عناوين بالضبط
psql -d <main> -c "UPDATE report_templates SET \"Classification\"='Supplementary'
  WHERE \"Title\" IN ('تقرير متابعة مقالات SEO الأسبوعي','تقرير كاتب المحتوى الأسبوعي',
                      'تقرير فريق التصميم','تقرير فريق الفيديو','تقرير المديرشن الأسبوعي');"
# يجب أن تُرجِع UPDATE 5 بالضبط

# (4) حارس الانحدار — لا مفاتيح فترات مستقبليّة تمرّ عبر بوّابات التقويم
grep -rn '"20[3-9][0-9]-' tests/Reporting.IntegrationTests --include='*.cs' | grep -v TemplateVersionManagementTests
# يجب أن يكون الخرج فارغًا. الاستثناء الوحيد المشروع: TemplateVersionManagementTests.cs:57
# (`2099-Wxx`) لأنّه يُدرَج مباشرةً عبر db.ReportSubmissions.Add ولا يعبر بوّابة التقويم.
# مفاتيح الفترات *الماضية* المُصلَّبة (221 موضعًا) مشروعة ولا تُلاحَق.

# (5) الجولة الكاملة
dotnet test Reporting.sln -c Debug --no-build
```

### 3.2 الأخطاء الثلاثة الموثَّقة وأعراضها

| # | الخطأ الإجرائيّ | العَرَض | الرقم الباطل |
|---|---|---|---|
| 1 | قاعدة مُعاد استعمالها (292 MB) + تسرّب إلى `reporting_test` (25 GB) | 5 س 11 د بدل 7 د · عناقيد تذكيرات/بريد/إجازات | **67** (ومنه اشتُقّ **130/118**) |
| 2 | تمهيد قاعدة PFE مفقود ⟹ تسابق هجرات أثناء التوازي | +13 فشلًا في `RepeatableNumericValidationIntegrationTests` | **29** |
| 3 | `git checkout -- <tests-path>` أتلف ترقيع مفاتيح الفترات في `ReportsTests.cs` | +15 فشلًا في `ReportsTests` (كلّها Rollup) | **16** |
| 4 | إغفال `export TEST_DB_CONNECTION` في مُشغِّل الجولة (`run-cand9.sh`) | تمهيد `main` ذهب إلى `reporting_test` الملوَّثة؛ `rr_cand_main` بقيت بـ**0 جداول**؛ `set -e` أجهض الجولة عند `ERROR: relation "report_templates" does not exist` | **لا رقم — الجولة أُجهضت قبل القياس** |

### 3.3 القواعد الحاكمة المستخلَصة

1. **أيّ انحراف في عدد الفشل بين جولتين لنفس الالتزام = خلل في إجراء القياس حتّى يثبت العكس.** افحص `git status` و`git diff --stat` قبل تصديق أيّ رقم.
2. **ممنوع `git checkout -- <dir>` على مجلّد اختبارات** يحوي عملًا غير ملتزَم. خُذ `git diff > *.patch` قبل أيّ تراجع.
3. **لا يُصنَّف فشلٌ من جولة واحدة.** التصنيف يتطلّب: قاعدة نظيفة + مطابقة بالاسم مع الأبوين + تكرار.
4. **تحقّق من التمهيد بعدد الجداول لا بنجاح الاختبار**: `select count(*) from information_schema.tables where table_schema='public'` يجب أن يُرجِع **78** لكلّ من القواعد الثلاث قبل الجولة الكاملة. نجاح اختبار التمهيد **لا يعني** أنّ القاعدة المقصودة هي التي هُوجرت — قد يكون ذهب إلى `reporting_test`.
5. **عبارة التصنيف تُرجِع `UPDATE 5` بالضبط**؛ أيّ رقم آخر يعني عبارة خاطئة ⟹ أعِد إنشاء القاعدة (وقع خطأ `UPDATE 34` مرّة وصُحِّح بإعادة الإنشاء).

### 3.4 نسخ الأمان

قبل أيّ عمليّة قد تُتلف العمل غير الملتزَم:
```
/tmp/lineage/safety/candidate-worktree-<ts>.patch   (138 KB)
/tmp/lineage/safety/candidate-staged-<ts>.patch     (7.3 KB)
/tmp/lineage/safety/TestCalendar.cs                 (5.5 KB — ملفّ غير مُتتبَّع)
```

---

## 4) بوّابة §14 — الحالة النهائيّة

| البوّابة | المطلوب | المُقاس | الحالة |
|---|---|---|---|
| Unified Candidate Regression | 0 | **0** | ✅ |
| Unresolved | 0 | **0** | ✅ |
| Production Live Feature Regression | 0 | **0** | ✅ |
| CPW-R2 Regression | 0 | **0** | ✅ |
| CPW-R3 Regression | 0 | **0** | ✅ |

**البوّابة مفتوحة تقنيًّا.** ما تبقّى من التذكرة (I–K: الالتزامات الذرّيّة والدمج والدفع إلى `develop`؛ L–N: نشر TEST وUAT المستهدفة وتجميد مرشَّح RC) **يحتاج تصريحًا صريحًا جديدًا من المستخدم لكلّ عمليّة على حدة**، ولم يُنفَّذ منه شيء.
