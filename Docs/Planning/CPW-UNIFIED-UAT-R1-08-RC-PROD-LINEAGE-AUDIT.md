# CPW-UNIFIED-UAT-R1 — تدقيق نَسَب RC/Production (قراءة فقط)

- **التذكرة:** CPW-R2 + CPW-R3 — UNIFIED UAT, SECURITY CLOSURE & RC READINESS
- **المرحلة:** K — تدقيق النَسَب والهجرات على RC والإنتاج، **قراءةً فقط بلا أيّ كتابة**
- **التاريخ:** 16 أغسطس 2026
- **النتيجة:** **K_GATE = PASS (التدقيق نفسه)** · **RC/PROD DEPLOY = NO-GO (توقّف إلزاميّ)**

> لم تُنفَّذ أيّ عمليّة كتابة على RC أو الإنتاج: لا هجرة، لا إعادة تشغيل خدمة، لا تعديل ملفّ، لا `UPDATE`/`INSERT`. كلّ الاستعلامات `SELECT` وكلّ قراءات النظام `systemctl show` و`ls`/قراءة بايتات DLL.

---

## 1) الحالة الفعليّة للبيئات الثلاث

| البيئة | الخدمة | القاعدة | عدد الهجرات المطبَّقة | رأس الهجرات | SHA المنشور (SourceLink) |
|---|---|---|---|---|---|
| Production | `reporting-api` | `reporting_prod` | **30** | `20260724224053_AddReportApproverAndKpiReviewerOverrides` | `ce166662f46598ed3593beed0105ba67059fc3bc` |
| RC | `khubara-reporting-rc` | `reporting_rc` | **30** | نفس الرأس | `ce166662f46598ed3593beed0105ba67059fc3bc` |
| TEST/UAT | `khubara-reporting-test` | `reporting_test_uat` | **35** | `20260811142239_AddProject360Foundation` | `81ee1455eb995b4cd823d0308e83186ee1e7ef9a` |

- **RC و Production متطابقان تمامًا**: `diff` قائمتَي `__EFMigrationsHistory` = **IDENTICAL**، ونفس الـSHA المنشور. أي RC ليس تدرّجًا وسيطًا بل مرآة للإنتاج.
- **الاتّساق الداخليّ لكلّ بيئة سليم**: مجموعة ملفّات الهجرات في شجرة الـcommit المنشور تطابق حرفيًّا صفوف `__EFMigrationsHistory` في قاعدتها (30 = 30 للإنتاج/RC، 35 = 35 لـTEST). **لا يوجد أيّ صفّ يتيم بلا ملفّ ولا ملفّ غير مطبَّق في أيّ من البيئات الثلاث.**

---

## 2) الاكتشاف الحاجب: `ce166662` **ليس سلفًا** لـ`develop`

```
git merge-base --is-ancestor ce166662 HEAD   ->  NO
merge-base(ce166662, 81ee145)                ->  6fd2253  "RC-4 Sales Module baseline"
commits موجودة في ce166662 وغائبة عن develop ->  32
commits موجودة في develop وغائبة عن ce166662 ->  42
الفرع الحاوي لـce166662                       ->  candidate/leave-deduction-tl-approval-r1-20260806
```

أي أنّ الإنتاج **لا يقع على خطّ `develop` إطلاقًا**، بل على فرع مرشَّح منفصل تشعّب عند `6fd2253`. النَسَبان يسيران متوازيَين منذ ذلك الحين، وكلٌّ منهما راكم عملًا لا يعرفه الآخر.

---

## 3) تشعّب مجموعات الهجرات

### 3.1 موجودة في `develop` (وفي TEST) وغائبة عن الإنتاج/RC — 10

```
20260620001156_FlexiblePositionsPhase1A
20260622140138_KpiTemplateAssignmentsPhaseT1
20260626124527_AddReportViewGrants
20260708232456_AddExecutionTaxonomyCatalog
20260709222126_AddProjectWorkstreams
20260709231845_AddWorkstreamDeliverables
20260712211952_AddClient360Foundation
20260807033602_ClientDocumentsAndExternalLinks
20260809165617_ClientDocumentVisibility
20260811142239_AddProject360Foundation
```

### 3.2 موجودة في الإنتاج/RC وغائبة تمامًا عن شجرة `develop` — 5

```
20260622144900_KpiTemplateAssignmentsPhaseT1              (تصادم منطقيّ)
20260626135944_AddReportViewGrants                        (تصادم منطقيّ)
20260715162851_AddBypassTeamLeaderApproval                (وظيفيّة حقيقيّة)
20260716015239_KpiEvaluationPartialUniqueIndex            (وظيفيّة حقيقيّة)
20260724224053_AddReportApproverAndKpiReviewerOverrides    (وظيفيّة حقيقيّة)
```

---

## 4) خطران مثبَتان بالفحص لا بالاستنتاج

### 4.1 تصادم `42P07` مؤكَّد — هجرتان بمعرّفَين مختلفَين تُنشئان الكائنات نفسها

| الهجرة على `develop` | الهجرة على الإنتاج | الكائنات المُنشأة |
|---|---|---|
| `20260622140138_KpiTemplateAssignmentsPhaseT1` | `20260622144900_KpiTemplateAssignmentsPhaseT1` | جدول `kpi_template_assignments` + فهرسان بنفس الأسماء |
| `20260626124527_AddReportViewGrants` | `20260626135944_AddReportViewGrants` | جدول `report_view_grants` + 6 فهارس بنفس الأسماء |

وقد تحقّقنا من قاعدة الإنتاج مباشرةً:

```
select tablename from pg_tables where tablename in ('kpi_template_assignments','report_view_grants');
-> kpi_template_assignments
-> report_view_grants
```

**النتيجة الحتميّة:** أيّ إقلاع لبناء `develop` على `reporting_prod` سيستدعي `MigrateAsync()`، الذي سيرى المعرّفَين `20260622140138` و`20260626124527` غير مسجَّلَين في `__EFMigrationsHistory` فيحاول تنفيذهما، فينفّذ `CREATE TABLE kpi_template_assignments` على جدول **موجود فعلًا** ⇒ **`42P07 relation already exists`** وتعطّل الإقلاع. الخطأ يقع **بعد** بدء المعاملة على قاعدة الإنتاج الحيّة.

### 4.2 انحدار وظيفيّ صامت — أعمدة إنتاج لا يعرفها كود `develop`

| العمود/الفهرس في الإنتاج | القابليّة للإفراغ | الافتراضيّ | موجود في كود `develop`؟ |
|---|---|---|---|
| `AspNetUsers.BypassTeamLeaderApproval` | `NOT NULL` | `false` | **لا** |
| `AspNetUsers.KpiReviewerOverrideUserId` | `NULL` | — | **لا** |
| `AspNetUsers.ReportApproverOverrideUserId` | `NULL` | — | **لا** |
| `IX_kpi_evaluations_…_PeriodKey` (فهرس فريد جزئيّ) | — | — | **لا** |

بحث نصّيّ شامل في `reporting-backend/src` على `develop`: **0 مطابقة** للأسماء الثلاثة.

- لا يقع فشل `INSERT` (العمود الإلزاميّ له `DEFAULT false`، والآخران يقبلان `NULL`)، ولذلك **الخطر أخبث**: لا يظهر كخطأ.
- الأثر الحقيقيّ: **اختفاء ثلاث ميزات حيّة في الإنتاج** — تجاوز اعتماد قائد الفريق، وتجاوزات معتمِد التقرير/مراجِع الـKPI، والفهرس الفريد الجزئيّ الحارس لتفرّد تقييم الـKPI. الأخير حارس **سلامة بيانات**: بغيابه من نموذج الكود قد تُكتَب صفوف تقييم مكرَّرة… ثمّ يرفضها الفهرس القائم في القاعدة بخطأ تعارض غير مُتوقَّع في الكود.

---

## 5) لماذا هذا توقُّف إلزاميّ

يقع الاكتشاف على **بندَين** من بنود التوقّف الإلزاميّ في التوجيه:

1. **«تعارض Business/Security غير قابل للحلّ داخل النطاق»** — التوفيق بين النَسَبَين يستلزم قرار مالك المنتج: أيّ الميزتَين تُحفَظ وكيف تُدمَج 32 commit إنتاجيّة في `develop`.
2. **«الحاجة إلى تغيير RC أو Production»** — أيّ نشر يستلزم مسبقًا كتابة على النَسَب (دمج/إعادة كتابة هجرات)، وهو خارج التصريح.

كما يصطدم بحظر صريح: **«ممنوع إنشاء هجرة جديدة بعد 35»** و**«ممنوع تعديل هجرة مطبَّقة»** — وأيّ حلّ تقنيّ للتصادم في §4.1 يتطلّب أحدهما حتمًا.

---

## 6) الحكم

| البند | النتيجة |
|---|---|
| تنفيذ التدقيق قراءةً فقط بلا كتابة | **PASS** |
| اتّساق الملفّات/الصفوف داخل كلّ بيئة | **PASS** (30/30 · 30/30 · 35/35) |
| تطابق RC مع الإنتاج | **PASS** (متطابقان حرفيًّا) |
| `ce166662` سلف لـ`develop` | **FAIL — ليس سلفًا** |
| توافق مجموعات الهجرات | **FAIL — تشعّب 10 مقابل 5** |
| سلامة النشر على الإنتاج/RC من `develop` | **FAIL — `42P07` مؤكَّد + انحدار 3 ميزات** |

**K_GATE = PASS (التدقيق) · RC_DEPLOY = NO-GO · PROD_DEPLOY = NO-GO**

التوصية: تُفتَح **تذكرة توفيق نَسَب مستقلّة** (RECONCILE-PROD-DEVELOP-LINEAGE) قبل أيّ حديث عن إصدار، ولا يُعتبر مرشَّح RC الجاهز في المرحلة J صالحًا للنشر على RC الحاليّة قبل إغلاقها.
