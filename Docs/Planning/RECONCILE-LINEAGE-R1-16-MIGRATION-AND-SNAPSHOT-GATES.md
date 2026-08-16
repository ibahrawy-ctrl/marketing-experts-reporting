# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 16: بوّابات الهجرة ولقطة النموذج

**التذكرة:** `RECONCILE-PROD-DEVELOP-LINEAGE`
**المرحلة:** H — التقرير 8 في تسلسل المستخدم
**التاريخ:** 16 أغسطس 2026
**الحكم:** **كلّ بوّابات الهجرة خضراء** · المخطَّط الناتج من نَسَب الإنتاج **مطابق بصمةً** للمخطَّط الناتج من قاعدة جديدة · **صفر فقدان بيانات**

> **تصريح حوكمة:** كلّ ما في هذا التقرير نُفِّذ على **نسخ محلّيّة** (`recon_clone_prod` · `recon_clone_rc` · `recon_prod_nobridge` · `recon_fresh`). **لم تُمَسّ قاعدة الإنتاج ولا RC ولا TEST** بأيّ قراءة كتابيّة أو هجرة أو إعادة تشغيل.

---

## 1) مجموعات الهجرات في الأشجار الثلاث

| الشجرة | الالتزام | عدد ملفّات الهجرة |
|---|---|---|
| `origin/develop` | `10c26f7` | 35 |
| Production/RC | `ce166662` | 30 |
| **المرشَّح الموحّد** | `ac0d86c` + إصلاحات G | **38** |

### 1.1 تقاطعات المجموعات (مُقاسة بالاسم)

```
cand ∩ prod        = 28
cand \ prod        = 10
prod \ cand        =  2
dev  \ cand        =  0      ← develop مُحتواة بالكامل في المرشَّح
```

**المعادلة المُغلقة:**
`38 = 28 مشتركة + 2 توأمان مُعاد ترقيمهما + 8 هجرات جديدة`
`30 = 28 مشتركة + 2 توأمان بترقيم الإنتاج`
`35 = 38 − 3` (الهجرات الإنتاجيّة الثلاث التي استُقدِمت إلى المرشَّح)

### 1.2 الهجرات الثلاث الإنتاجيّة المستقدَمة (في المرشَّح وليست في `develop`)

```
20260715162851_AddBypassTeamLeaderApproval
20260716015239_KpiEvaluationPartialUniqueIndex
20260724224053_AddReportApproverAndKpiReviewerOverrides
```

### 1.3 «في `develop` وليست في المرشَّح» = **لا شيء**

⟹ لا هجرة واحدة من `develop` سقطت أثناء الدمج. **بوّابة عدم الفقدان: ✅**

---

## 2) التوأمان المتصادمان — إثبات التطابق البايتيّ

الهجرتان الوحيدتان في الإنتاج وليستا في المرشَّح هما **نفس الهجرتين بمعرّفَي زمن مختلفَين** (وُلِّدتا مرّتين على فرعين متوازيين):

| الاسم المنطقيّ | معرّف الإنتاج | معرّف المرشَّح | الحجم | MD5 للجسم |
|---|---|---|---|---|
| `KpiTemplateAssignmentsPhaseT1` | `20260622144900_…` | `20260622140138_…` | 2 808 B / 60 سطرًا | `13e7356322b59afff664213540bf093f` (**متطابق**) |
| `AddReportViewGrants` | `20260626135944_…` | `20260626124527_…` | 3 499 B / 79 سطرًا | `edbad105dfa9c270c0a36cdac13f89b9` (**متطابق**) |

`diff` جسم الهجرتين في كلّ زوج = **0 سطر**. الفرق الوحيد هو سِمة `[Migration("…")]` في ملفّ `*.Designer.cs`:

```
prod : [Migration("20260622144900_KpiTemplateAssignmentsPhaseT1")]
cand : [Migration("20260622140138_KpiTemplateAssignmentsPhaseT1")]
prod : [Migration("20260626135944_AddReportViewGrants")]
cand : [Migration("20260626124527_AddReportViewGrants")]
```

⟹ **DDL متطابق حرفيًّا، ومعرّف مختلف** — وهذا بالضبط شرط خطأ `42P07`.

---

## 3) البوّابة G1 — إثبات تجريبيّ للفشل بلا جسر

نُفِّذت هجرات المرشَّح على نسخة إنتاج **بلا** جسر (`recon_prod_nobridge`، بدايتها 30 صفًّا / 57 جدولًا):

```
dotnet ef database update --connection "…Database=recon_prod_nobridge…"
```

**النتيجة:**
```
SqlState:    42P07
MessageText: relation "kpi_template_assignments" already exists
File: heap.c   Line: 1166   Routine: heap_create_with_catalog
```

**الحالة بعد الفشل — قاعدة معطوبة نصف‑مهاجَرة:**

| المؤشّر | قبل | بعد الفشل |
|---|---|---|
| صفوف `__EFMigrationsHistory` | 30 | **31** |
| جداول `public` | 57 | **61** |

الصفّ الزائد: `20260620001156_FlexiblePositionsPhase1A` — طُبِّق ونجح (كلّ هجرة معاملة مستقلّة) وأنشأ أربعة جداول:
```
positions · position_scopes · position_permissions · user_positions
```
ثمّ فشلت الهجرة التالية وتراجعت وحدها. النتيجة قاعدة **لا تتقدّم ولا ترجع**، والتطبيق لا يُقلِع.

⟹ **النشر المباشر لـ`develop`/المرشَّح على الإنتاج بلا جسر = عطل مؤكَّد لا محتمل.** هذا الإثبات التجريبيّ يرفع الحاجب من «متوقَّع» إلى «مُقاس».

---

## 4) البوّابة G2 — سلوك حزمة الجسر (`Migration History Bridge`)

الجسر يُدرِج **صفَّي اسم مستعار فقط** في `__EFMigrationsHistory` بنفس `ProductVersion` المأخوذ من الصفّ التاريخيّ المقابل — **بلا أيّ DDL وبلا أيّ لمس لجدول أعمال**.

### 4.1 التنفيذ على نسخة الإنتاج (`recon_clone_prod`, APPLY)

```
[1/9] هويّة البيئة … OK      [5/9] سجلّ الهجرات … OK (المطبَّق = 30)
[6/9] الصفّان التاريخيّان … OK
[7/9] وجود الجدولَين … OK
[8/9] الصفّان المعتمدان … غائبان (كما هو متوقَّع)
[9/9] بصمة المخطَّط … OK (متطابقة سطرًا بسطر)
قبل: صفوف السجلّ = 30 | جداول = 57
BEGIN / INSERT 0 1 / INSERT 0 1 / COMMIT
بعد: صفوف السجلّ = 32 | جداول = 57
بصمة المخطَّط بعد التنفيذ … بلا تغيير OK
RESULT = APPLIED (2 alias rows)
```

**جداول 57 → 57**: الجسر لم يُنشئ ولم يحذف شيئًا.

### 4.2 حارس النَسَب — الرفض على بيئة ليست من نَسَب الإنتاج

```
db=recon_clone_test env=test mode=DRY-RUN
[5/9] سجلّ الهجرات … OK (المطبَّق = 38)
REFUSED: الصفّ التاريخيّ '20260622144900_KpiTemplateAssignmentsPhaseT1' غير موجود (وُجد 0)
         — هذه البيئة ليست من نَسَب الإنتاج.
RESULT = REFUSED
```
⟹ الأداة **لا يمكن تشغيلها بالخطأ** على TEST أو على قاعدة نظيفة.

### 4.3 التراجع الذرّيّ

`/tmp/lineage/bridge-out/bridge-rollback-recon_clone_prod-*.sql`:
```sql
BEGIN;
DELETE FROM "__EFMigrationsHistory"
 WHERE "MigrationId" IN ('20260622140138_KpiTemplateAssignmentsPhaseT1',
                         '20260626124527_AddReportViewGrants');
SELECT count(*) AS history_rows FROM "__EFMigrationsHistory";  -- يجب أن يعود 30
COMMIT;
```
يحذف **حصريًّا** الصفَّين اللذين أضافهما الجسر؛ لا يمسّ أيّ جدول أعمال ولا أيّ بيانات.

### 4.4 وضع التجربة الجافّة قبل التنفيذ

نُفِّذ `DRY-RUN` على `recon_clone_rc` أوّلًا وأعطى `RESULT = DRY-RUN OK (ready to apply)` مع سرد العبارتين المزمعتين والعدد المتوقَّع (32 صفًّا / 57 جدولًا) — أي **لا تنفيذ بلا معاينة**.

---

## 5) البوّابة G3 — تطابق البصمة بعد الهجرة الكاملة

بعد الجسر ثمّ `dotnet ef database update` بهجرات المرشَّح:

| القاعدة | المصدر | صفوف السجلّ | الجداول | رأس الهجرات |
|---|---|---|---|---|
| `recon_fresh` | **قاعدة جديدة** من المرشَّح | 38 | **78** | `20260811142239_AddProject360Foundation` |
| `recon_clone_prod` | نسخة الإنتاج + جسر + هجرات | 40 | **78** | نفسه |
| `recon_clone_rc` | نسخة RC + جسر + هجرات | 40 | **78** | نفسه |

`40 = 38 + 2` (صفّا الاسم المستعار التاريخيّان الباقيان).

### بصمة المخطَّط الكاملة (جداول + أعمدة بأنواعها وأطوالها وقابليّة العدم + كلّ الفهارس بتعريفاتها)، باستثناء `__EFMigrationsHistory`:

```
recon_fresh        1261 سطرًا   md5 = e86691079fe2c2140e8b61a3ac35b92f
recon_clone_prod   1261 سطرًا   md5 = e86691079fe2c2140e8b61a3ac35b92f
recon_clone_rc     1261 سطرًا   md5 = e86691079fe2c2140e8b61a3ac35b92f

diff(fresh, clone_prod) = 0 سطر
```

⟹ **مسار الترقية من نَسَب الإنتاج يُنتج مخطَّطًا مطابقًا بايتًا‑ببايت لمسار القاعدة الجديدة.** لا انحراف مخطَّط، ولا جدول مفقود، ولا عمود بنوع مختلف، ولا فهرس ضائع.

---

## 6) البوّابة G4 — لقطة النموذج (`ModelSnapshot`)

```
$ dotnet ef migrations has-pending-model-changes \
    --project src/Reporting.Infrastructure --startup-project src/Reporting.Api
Build succeeded.
No changes have been made to the model since the last migration.
```

⟹ `ApplicationDbContextModelSnapshot` **متّسق تمامًا** مع مجموعة الهجرات الـ38 بعد الدمج. لا هجرة معلّقة مطلوبة، ولا لقطة بائتة نتجت عن حلّ التعارض.

*(التحذيرات الأربعة الظاهرة `global query filter … required end of a relationship` سابقة للتذكرة وموجودة على الأبوين معًا — ليست ناتجًا للدمج.)*

---

## 7) البوّابة G5 — الحفاظ على البيانات

مقارنة عدّ الصفوف لكلّ جدول قبل الهجرة وبعدها على نسختَي الإنتاج وRC (57 جدولًا قبل → 78 بعد):

| الفرق المرصود | العدد | الحكم |
|---|---|---|
| جداول **نقص** فيها عدد الصفوف | **0** | ✅ |
| جداول **تغيّر** فيها عدد الصفوف | **0** | ✅ |
| `__EFMigrationsHistory` | 30 → 40 | متوقَّع (8 هجرات + صفّا الاسم المستعار) |
| جداول جديدة فارغة (`=0`) | **21** | إضافة بحتة |

الجداول الجديدة الـ21:
```
client_documents · client_document_versions · client_document_allowed_roles ·
client_document_allowed_users · client_external_links · client_contacts ·
client_brand_profiles · client_digital_channels · execution_taxonomy_values ·
positions · position_scopes · position_permissions · user_positions ·
project_objectives · project_strategies · project_strategy_attributes ·
project_workstreams · project_deliverables · workstream_deliverables ·
project_kpis · project_kpi_readings
```

عيّنة من الجداول الحيّة المحفوظة بلا تغيير: `AspNetUsers=34` · `AspNetUserRoles=39` · `AspNetRoles=12` · `approval_steps=293` · `audit_logs=1272` · `clients=10` · `balance_policies=1`.

⟹ **الهجرات إضافيّة بحتة (Additive-only).** لا `DROP`، لا `ALTER … TYPE` متلِف، لا Backfill، ولا فقدان صفّ واحد. نفس النتيجة حرفيًّا على نسخة RC.

---

## 8) خلاصة البوّابات

| البوّابة | المطلوب | المُقاس | الحالة |
|---|---|---|---|
| G0 — لا هجرة من `develop` مفقودة | 0 | **0** | ✅ |
| G1 — إثبات `42P07` بلا جسر | مُثبَت | **مُثبَت تجريبيًّا** | ✅ |
| G2 — الجسر بلا DDL وبلا مسّ بيانات | 57→57 جدولًا | **57→57** | ✅ |
| G2ب — حارس النَسَب يرفض بيئة أجنبيّة | يرفض | **REFUSED على TEST** | ✅ |
| G2ج — تراجع ذرّيّ متاح | متاح | **سكربت مولَّد لكلّ تنفيذ** | ✅ |
| G3 — تطابق بصمة المخطَّط (نسخة إنتاج ⇔ قاعدة جديدة) | 0 فرق | **0 فرق · md5 واحد** | ✅ |
| G3ب — تطابق نسخة RC | 0 فرق | **0 فرق · نفس md5** | ✅ |
| G4 — `ModelSnapshot` بلا تغييرات معلّقة | لا تغييرات | **No changes** | ✅ |
| G5 — صفر فقدان بيانات | 0 | **0** | ✅ |

---

## 9) الشروط المُلزِمة لأيّ نشر لاحق (مستخلَصة من هذه البوّابات)

1. **الجسر إلزاميّ وسابق للهجرة** على أيّ قاعدة من نَسَب الإنتاج (`reporting_prod`, `reporting_rc`). ترتيب العمليّة: نسخة احتياطيّة ⟵ `DRY-RUN` ⟵ `APPLY` ⟵ التحقّق من 32 صفًّا / 57 جدولًا ⟵ الهجرة الكاملة.
2. **لا يُشغَّل الجسر على TEST** ولا على أيّ قاعدة نظيفة — الأداة ترفض تلقائيًّا، ولا تُعطَّل هذه الحماية.
3. **بوّابة تحقّق بعديّة إلزاميّة:** بصمة المخطَّط بعد الهجرة يجب أن تُطابِق `e86691079fe2c2140e8b61a3ac35b92f` (1261 سطرًا) وعدد الجداول 78 وصفوف السجلّ 40.
4. **بوّابة بيانات إلزاميّة:** عدّ صفوف كلّ جدول قائم قبل/بعد يجب أن يكون **متطابقًا تمامًا**؛ أيّ نقص = إيقاف فوريّ وتراجع.
5. لا شيء من ذلك يُنفَّذ على بيئة حيّة **بلا تصريح صريح جديد من المستخدم لكلّ عمليّة على حدة**.

---

## 10) الأدلّة على القرص

```
/tmp/lineage/gates/twin-20260622144900_KpiTemplateAssignmentsPhaseT1.diff   (0 سطر)
/tmp/lineage/gates/twin-20260626135944_AddReportViewGrants.diff             (0 سطر)
/tmp/lineage/gates/nobridge-migrate.log                                     (42P07)
/tmp/lineage/gates/fp-recon_fresh.txt · fp-recon_clone_prod.txt · fp-recon_clone_rc.txt
/tmp/lineage/gates/m-cand.txt · m-prod.txt · m-dev.txt
/tmp/lineage/rows_prod_before.txt · rows_prod_after.txt · rows_rc_before.txt · rows_rc_after.txt
/tmp/lineage/bridge-out/bridge-recon_clone_prod-*.log   (DRY-RUN + APPLY)
/tmp/lineage/bridge-out/bridge-recon_clone_test-*.log   (REFUSED)
/tmp/lineage/bridge-out/bridge-rollback-*.sql
```
