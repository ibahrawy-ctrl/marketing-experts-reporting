# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 20: الدمج في develop والنشر على TEST

**التذكرة:** `RECONCILE-PROD-DEVELOP-LINEAGE`
**المراحل:** K2 (Develop Integration) · L (TEST Backup · Deployment · Migration)
**التاريخ:** 16 أغسطس 2026
**الحكم:** **نجاح تامّ** · انحدار = 0 · فقدان بيانات = 0 · تصادم هجرات = 0
**RC والإنتاج: لم يُمَسّا إطلاقًا.**

> يجمع هذا التقرير موضوعات التقارير 20–23 المخطَّطة (بوّابة ما قبل الدمج النهائيّة،
> الدمج والدفع، النسخ والنشر، والتحقّق من جسر الهجرات) في وثيقة واحدة متّصلة لأنّ
> أدلّتها متسلسلة سببيًّا ولا تُقرأ منفصلة.

---

## 1) الدفع إلى `origin/develop` (K2)

الدفع تمّ **بلا `--force`** وبتقديم سريع محض (fast-forward)، لأنّ `develop` كان سلفًا
صارمًا لمرجع التكامل. الفرع `develop` كان مسحوبًا في شجرة العمل الرئيسيّة، فبدل
سحبه مجدّدًا دُفِع مرجع التكامل مباشرةً إلى الفرع البعيد:

```
git push origin integration/reconcile-prod-develop-lineage-r1:develop
To github.com:ibahrawy-ctrl/marketing-experts-reporting.git
   10c26f7..4fddc20  integration/reconcile-prod-develop-lineage-r1 -> develop
```

| البند | القيمة |
|---|---|
| `origin/develop` بعد الدفع | **`4fddc20ad23757636c54f3a5baa94fec08a84c61`** |
| سلف من `develop` القديم | `git merge-base --is-ancestor 10c26f7 4fddc20` ⟹ **نعم** |
| سلف من الإنتاج الحيّ | `git merge-base --is-ancestor ce166662 4fddc20` ⟹ **نعم** |
| `origin/main` | `508509ad…` — **لم يتحرّك** |
| نوع الدفع | تقديم سريع · **بلا `--force`** · بلا إعادة كتابة تاريخ |

**هذا هو جوهر التذكرة:** الرأس الجديد **خَلَفٌ حقيقيّ للأبوَين معًا**، لا نسخة ملفّات
ولا إعادة بناء. النَسَب المتشعّب منذ `6fd2253` صار موحّدًا.

### 1.1 الالتزامات المُضافة في هذه الجلسة

| الالتزام | الغرض |
|---|---|
| `a510c01` | إغلاق الواجهة المفتوحة في `types/api.ts` (TS1131) |
| `bc71b19` | استعادة المسارَين اليتيمَين + حارس `routeRegistry.test.ts` |
| `5b13bee` | التقرير 19 — بوّابة ما قبل الدمج |
| `4fddc20` | تصحيح ادّعاء قائم على الاسم في التقرير 18 |

---

## 2) النسخ الاحتياطيّة قبل النشر (L.1)

المسار: `/root/backups/20260816-recon-l/` — **56 ميغابايت** · تسعة عناصر · بصمات في
`SHA256SUMS.txt`.

| العنصر | الغرض |
|---|---|
| `reporting_test_uat.dump` (623 KB) | نسخة `pg_dump -Fc` قابلة للاستعادة |
| `reporting_test_uat.sql` (771 KB) | نسخة نصّيّة — **صارت مصدر خطّ الأساس العدديّ** |
| `migrations-before.txt` | سجلّ الهجرات قبل النشر (35) |
| `publish-before.tgz` (47 MB) | ثنائيّات الخلفيّة السابقة |
| `frontend-dist-before.tgz` (369 KB) | حزمة الواجهة السابقة |
| `documents-before.tgz` (9.2 MB) | مستندات العملاء |
| `storage-md5-before.txt` | بصمة تجميعيّة لملفّات التخزين |
| `appsettings-masked.txt` · `service-unit.txt` | التهيئة (بلا أسرار) ووحدة الخدمة |

**ملاحظة منهجيّة مهمّة:** استُخرِج خطّ الأساس العدديّ لاحقًا **من نسخة `pg_dump` نفسها**
عبر عدّ صفوف كتل `COPY … FROM stdin` (السكربت `/root/dump-counts.sh`)، لا من ثوابت
مكتوبة يدويًّا. هذا يجعل «حفظ البيانات» قابلًا لإعادة التحقّق من الأرشيف وحده.

---

## 3) النشر (L.2)

```
systemctl stop khubara-reporting-test                  → inactive
mv publish            → publish-backup-recon-20260816
mv publish-staging-recon → publish
mv frontend/dist      → frontend/dist-backup-recon-20260816
mv frontend/dist-staging-recon → frontend/dist
chown -R www-data:www-data publish frontend/dist
systemctl start khubara-reporting-test                 → active
```

نُقِلت الحزم المرحليّة إلى مواضعها بـ`mv` ذرّيّ بعد إيقاف الخدمة، والنسخة السابقة
بقيت في مكانها باسم `*-backup-recon-20260816` ⟹ **التراجع لحظيّ ولا يحتاج فكّ أرشيف**.

| الأثر المنشور | القيمة |
|---|---|
| `publish/` | 109 ميغابايت · 86 ملفًّا |
| `Reporting.Api.dll` | `6a4b6022cb73735877f971a07219ab69fbd615ba41c3ae9d4f32cefe8fd7f085` |
| `Reporting.Application.dll` | `73dd90ffd15e3c26e32f03c777aa76163084d93ee25bd51854ed6402bc860f00` |
| `Reporting.Infrastructure.dll` | `285927516bc582492a306f594fe0748756cf54f299a7fce580e2721c57b28de4` |
| `Reporting.Domain.dll` | `c6fe07cb53b88855ccaf5982088d63ab6b97e103202703853e9f77a868e8377b` |
| `frontend/dist/` | 1.6 ميغابايت · 7 ملفّات · بصمة تجميعيّة `f836bb9797b3457112cceeadfdfcd40954b765ae77faa0c42cb7931655c32150` |

---

## 4) الهجرات 35 → 38 (L.3)

الهجرات تُطبَّق عند الإقلاع (`db.Database.MigrateAsync()`). سجلّ الخدمة يوثّق التطبيق
الثلاثيّ حرفيًّا:

```
info: Microsoft.EntityFrameworkCore.Migrations[20402]
      Applying migration '20260715162851_AddBypassTeamLeaderApproval'.
      Applying migration '20260716015239_KpiEvaluationPartialUniqueIndex'.
      Applying migration '20260724224053_AddReportApproverAndKpiReviewerOverrides'.
```

| الفحص | النتيجة |
|---|---|
| عدد الهجرات | 35 → **38** |
| رأس الهجرات | `20260811142239_AddProject360Foundation` |
| الهجرات المطبَّقة | **الثلاث المتوقَّعة بالضبط، ولا رابعة** |
| `42P07 already exists` | **0** |
| أخطاء `journalctl -p 3` بعد الإقلاع | **0** |
| `GET /health` | `200 {"status":"ok","service":"reporting-api"}` |
| عدد الجداول | 78 = 78 |
| عدد الأعمدة | 928 = 928 |

### 4.1 تطابق المخطَّط مع المسار المحلّيّ المستقلّ

بصمة `md5` على `information_schema.columns` (اسم الجدول · العمود · النوع · قابليّة العدم):

| القاعدة | كيف بُنِيت | البصمة |
|---|---|---|
| `rr_k_fresh` (محلّيّة) | `dotnet ef database update` على قاعدة فارغة | `3dc2638fe72aadbdaa5450a9aa70c2c2` |
| `reporting_test_uat` (TEST) | ترقية 35→38 على قاعدة بها بيانات حيّة | `3dc2638fe72aadbdaa5450a9aa70c2c2` |

**مساران مختلفان تمامًا يبلغان نفس المخطَّط** ⟹ مسار «القاعدة الجديدة» (وهو مسار RC
والإنتاج المستقبليّ) يعطي نفس النتيجة التي أعطاها مسار الترقية التدريجيّة.

### 4.2 كائنات نَسَب الإنتاج المستعادة — إثبات على القاعدة الحيّة

```
col BypassTeamLeaderApproval     = present
col KpiReviewerOverrideUserId    = present
col ReportApproverOverrideUserId = present
positions_tables = position_permissions, position_scopes, positions
kpi_template_assignments = present
IX_kpi_evaluations_KpiTemplateVersionId_SubjectUserId_PeriodKey
  ON public.kpi_evaluations (…)  WHERE ("IsDeleted" = false)
```

الفهرس الفريد صار **جزئيًّا** بمرشّح `IsDeleted = false` — أي **أقلّ تقييدًا** من الفهرس
غير المرشَّح الذي كان قائمًا، فتطبيقه على قاعدة بها بيانات آمن بنيويًّا لا احتماليًّا،
وهو ما تنبّأ به الفحص القبليّ ثمّ تحقّق فعلًا.

---

## 5) حفظ البيانات — مقارنة بنسخة ما قبل النشر لا بثوابت

| الجدول | قبل النشر (من `pg_dump`) | بعد النشر | الحكم |
|---|---|---|---|
| `client_documents` | 16 | 16 | = |
| `client_document_versions` | 21 | 21 | = |
| `clients` | 6 | 6 | = |
| `projects` | 8 | 8 | = |
| `report_templates` | 34 | 34 | = |
| `AspNetUsers` | 17 | 17 | = |
| `report_submissions` | 13 | 13 | = |
| `execution_taxonomy_values` | 208 | 208 | = |
| `email_outbox` | 0 | 0 | = |
| `__EFMigrationsHistory` | 35 | **38** | **+3 بالتصميم** |
| بصمة ملفّات التخزين | `9694308428425295627aca90e778cfb6` | `9694308428425295627aca90e778cfb6` | = |

**`Unexpected Data Loss = 0`** — والاستثناء الوحيد معلَن ومقصود.

---

## 6) تمهيد الكتالوج — تشغيلان متعاقبان

`ExecutionTaxonomySeeder` يعمل عند كلّ إقلاع وهو idempotent بمطابقة (`Domain`, `Code`).
أُقلِعت الخدمة مرّتين وقيست النتيجة بعد كلّ إقلاع:

| البند | التشغيل 1 | التشغيل 2 |
|---|---|---|
| `strategy_section` | 6 | 6 |
| `strategy_field` | 14 | 14 |
| `contract_deliverable` | 18 | 18 |
| مجموع النطاقات الثلاثة | **38** | **38** |
| مجموع الكتالوج كلّه | 208 | 208 |
| تكرار (`Domain`,`Code`) | **0** | **0** |
| أحدث `CreatedAtUtc` | `2026-08-15 21:28:57` | `2026-08-15 21:28:57` |

ثبات أحدث طابع زمنيّ على قيمة **سابقة لهذا النشر** هو الإثبات الحاسم: التمهيد أنشأ
**صفر صفّ** في كلا التشغيلَين. **`Bootstrap Duplicates = 0`**.

---

## 7) التحقّق من جسر سجلّ الهجرات على TEST

`Ops/MigrationHistoryBridge/bridge.sh` أداة ملتزَمة، **تجربتها الجافّة هي الافتراضيّ**،
ولا تكتب شيئًا بلا `--apply`. شُغِّلت جافّة على `reporting_test_uat`:

```
[1/9] هويّة البيئة …………………… OK
[2/9] الاتصال ………………………… OK (PostgreSQL 16.14)
[4/9] لا معاملات معلّقة ………… OK
[5/9] سجلّ الهجرات ……………… OK (المطبَّق = 38)
REFUSED: الصفّ التاريخيّ '20260622144900_KpiTemplateAssignmentsPhaseT1' غير موجود
         — هذه البيئة ليست من نَسَب الإنتاج.
RESULT = REFUSED          bridge_exit=3
```

هذا **تحقّق سلبيّ ناجح**: الأداة ترفض العمل على قاعدة من نَسَب `develop`، وتُرجِع رمز
خروج غير صفريّ (3) يمكن لأتمتة النشر أن تحجب عليه. الجسر لازم لـRC والإنتاج **فقط**
لأنّهما وحدهما يحملان الصفوف التاريخيّة الإنتاجيّة.

> **مزلق موثَّق:** `bridge.sh … | tail -30` ثمّ `$?` يقرأ رمز خروج `tail` لا رمز الأداة،
> فيبدو الرفض نجاحًا. يجب إعادة التوجيه إلى ملفّ ثمّ قراءة `$?` مباشرةً.

---

## 8) كتلة بوّابة المرحلة L

```
origin/develop                      = 4fddc20ad23757636c54f3a5baa94fec08a84c61
Descendant of develop (10c26f7)     = YES
Descendant of production (ce166662) = YES
Force Push Used                     = NO
origin/main Moved                   = NO
TEST Backup                         = 9 artifacts · 56 MB · SHA256SUMS present
TEST Deployment                     = SUCCESS (rollback dirs retained in place)
Migration Count / Head              = 38 / 20260811142239_AddProject360Foundation
Migrations Applied This Deploy      = exactly 3
Migration Collision (42P07)         = 0
Schema Fingerprint TEST == fresh EF = 3dc2638fe72aadbdaa5450a9aa70c2c2
Unexpected Data Loss                = 0
Document Storage Fingerprint        = unchanged
Bootstrap Duplicates                = 0 (two consecutive runs, 0 rows created)
Startup Errors (journal -p 3)       = 0
Health                              = 200
Migration Bridge on TEST            = REFUSED by design (exit 3)
RC Touched                          = NO
Production Touched                  = NO
```
