# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 04: محاكاة مسارات قواعد البيانات الأربعة

**التاريخ:** 16 أغسطس 2026 · **المرحلة:** G · **البيئات الحيّة لم تُمَسّ إطلاقًا (قراءة فقط + نسخ محلّيّة).**

## 0. مصدر النسخ

`pg_dump -Fc` بقراءة فقط من الخادم لثلاث قواعد، ثمّ نُقِلت محلّيًّا وحُذفت من الخادم فورًا:

| المصدر | الحجم | الهجرات | الجداول | بصمة المخطَّط |
|---|---|---|---|---|
| `reporting_prod` | 1.2M | 30 | 57 | `e137d40dcd1ad8d088fa6c4ad9a8eebb` |
| `reporting_rc` | 392K | 30 | 57 | `e137d40dcd1ad8d088fa6c4ad9a8eebb` |
| `reporting_test_uat` | 412K | 35 | 78 | `b9f2d377b7f5aa2efb79530c99da0e1a` |

**RC والإنتاج لهما بصمة مخطَّط متطابقة حرفيًّا** ⟹ تأكيد تجريبيّ أنّ RC مرآة الإنتاج.

## 1. المسار Fresh — قاعدة فارغة من الصفر

```
dropdb/createdb recon_fresh ⟶ dotnet ef database update
Applying '20260609142107_InitialIdentity' … '20260811142239_AddProject360Foundation'   (38 هجرة)
Done.  __EFMigrationsHistory = 38
```

| الفحص | النتيجة |
|---|---|
| خطأ `42P07` أو Duplicate Table | **لا شيء** |
| `kpi_template_assignments` / `report_view_grants` | موجودان **مرّة واحدة** لكلّ منهما |
| `BypassTeamLeaderApproval` | عمود واحد |
| `ReportApproverOverrideUserId` + `KpiReviewerOverrideUserId` | عمودان |
| الفهرس الفريد الجزئيّ على `kpi_evaluations` | موجود بفلتر `WHERE ("IsDeleted" = false)` |
| الجداول | 78 · **بصمة مرجعيّة `3b3eb6b04fc0e6b1898468bd2cfed546`** (1309 سطرًا) |

## 2. المسار TEST — نسخة `reporting_test_uat`

35 ⟶ 38 هجرة بتطبيق الهجرات الإنتاجيّة الثلاث فقط. **لا حاجة لجسر** (نَسَب TEST هو نَسَب develop).

- بصمة المخطَّط بعد الترقية = **مطابقة تمامًا لبصمة Fresh**.
- فرق TEST عن Fresh قبل الترقية كان **حصريًّا**: 3 أعمدة + 2 فهرس + 2 مفتاح أجنبيّ + فلتر الفهرس الجزئيّ.
- **صفر تغيير في عدد صفوف الـ78 جدولًا** عدا `__EFMigrationsHistory` (35→38).
- `duplicate_active_kpi_evaluations = 0`.

## 3. إثبات الحاجب: نسخة إنتاج **بلا** جسر

```
Applying '20260620001156_FlexiblePositionsPhase1A'.
Applying '20260622140138_KpiTemplateAssignmentsPhaseT1'.
Npgsql.PostgresException 42P07: relation "kpi_template_assignments" already exists
```
القاعدة تُترك في حالة **نصف-مهاجَرة** (31 هجرة، 61 جدولًا) والخدمة لا تقلع. هذا هو الحاجب الفعليّ.

## 4. المسار RC — نسخة `reporting_rc` مع الجسر

| الخطوة | النتيجة |
|---|---|
| Dry Run | 9/9 فحوصًا خضراء · `RESULT = DRY-RUN OK` |
| Apply | 30 ⟶ 32 صفّ سجلّ · الجداول 57 بلا تغيير · البصمة بلا تغيير |
| إعادة التشغيل (idempotency) | `RESULT = ALREADY-BRIDGED (no-op)` |
| الترقية بعد الجسر | 8 هجرات معلّقة طُبِّقت · **بلا `42P07`** · السجلّ = 40 |
| البصمة النهائيّة | **= بصمة Fresh حرفيًّا** |
| صفوف الجداول الأصليّة الـ56 | **بلا أيّ تغيير** |

## 5. المسار Production — نسخة `reporting_prod` مع الجسر وبروفة التراجع

| الخطوة | النتيجة |
|---|---|
| Dry Run | `RESULT = DRY-RUN OK` |
| رفض بلا `--allow-production` | `REFUSED` (exit 3) |
| Apply بالعلم الصريح | 30 ⟶ 32 · الجداول 57 · البصمة بلا تغيير |
| **بروفة تراجع الجسر** (سكربت مولَّد) | عاد السجلّ إلى 30 · البصمة = الأصل تمامًا |
| إعادة الجسر ثمّ الترقية | 8 هجرات · بلا `42P07` · السجلّ = 40 |
| البصمة النهائيّة | **= بصمة Fresh حرفيًّا** |
| **مقارنة قيميّة** (md5 لمحتوى كلّ جدول) | **56/56 جدول أعمال متطابق قيمةً بقيمة مع الأصل** |
| **بروفة التراجع الكامل** من الحالة نصف-المهاجَرة (31 هجرة/61 جدولًا) بإعادة النسخة الاحتياطيّة | عاد إلى 30 هجرة/57 جدولًا · المخطَّط = الأصل · البيانات = الأصل قيمةً بقيمة |

## 6. بوّابات سلامة البيانات (§13 من التذكرة)

| البوّابة | TEST | RC | Production |
|---|---|---|---|
| Business rows deleted | 0 | 0 | 0 |
| Unexpected rows changed | 0 | 0 | 0 |
| Documents lost | 0 | 0 | 0 |
| Assignments lost | 0 | 0 | 0 |
| Overrides lost | 0 | 0 | 0 |
| Duplicate active KPI evaluations | 0 | 0 | 0 |

جميعها مُثبَتة بمقارنة عدد الصفوف **وبمقارنة md5 لمحتوى كلّ جدول** قبل/بعد.
