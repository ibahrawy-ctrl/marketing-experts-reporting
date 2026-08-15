# KPI-REVIEWER-OVERRIDE-R1 — تقرير نشر المرشّح على RC وقبول UAT

- التاريخ: 26 يوليو 2026
- النطاق المنفَّذ: **RC فقط**. **لم يُنشر أيّ شيء على Production، ولم تُطبَّق أيّ تسوية على Production.**
- الأساس: `f601bd1` (نفس الكود العامل على Production)، فرع المرشّح `candidate/kpi-reviewer-override-r1-20260726`.
- شجرة العمل المعزولة: `/private/tmp/cand-kpi-reviewer-override-r1-20260726/`.

---

## 1) سطح التغيير

| الملف | طبيعة التغيير |
|---|---|
| `Reporting.Api/Controllers/KpiEvaluationsController.cs` | مسار قراءة جديد `GET /api/kpi-evaluations/lookup` |
| `Reporting.Application/Kpi/IKpiEvaluationService.cs` | تصريح `LookupAsync` |
| `Reporting.Application/Kpi/KpiModels.cs` | `KpiEvaluationLookupQuery` + `KpiEvaluationLookupDto` |
| `Reporting.Infrastructure/Services/KpiEvaluationService.cs` | توسيع النطاق بالـOverride، حسم المُراجِع، الاعتماد المباشر، `LookupAsync` |
| `reporting-frontend/src/pages/KpiPage.tsx` | فلترة القائمة بالموظّف المختار + تحميل التقييم القائم بلا أثر جانبيّ |
| `reporting-frontend/src/types/api.ts` | نوع `KpiEvaluationLookupDto` |
| `tests/.../KpiReviewerOverrideR1Tests.cs` (جديد) | 11 اختبار تكامل |
| `tests/.../RoleAwarePersonalReportOverrideTests.cs` | تحديث توقُّع حالة «المُدخِل هو المُراجِع» |
| `tools/FatmaPeriodReconciler/` (جديد) | أداة تسوية فترات، خارج `Reporting.sln` |

**عدد ملفات الهجرة في المرشّح = عدد ملفات الهجرة في `f601bd1` (61 ملفًا، فرق صفر).** لا هجرة جديدة.

---

## 2) النشر على RC

| البند | القيمة |
|---|---|
| الطابع الزمنيّ | `20260726-212816` |
| الخدمة | `khubara-reporting-rc.service` (`active`) |
| البيئة | `Hosting environment: ReleaseCandidate` |
| `health` | 200 |
| رأس الهجرات قبل وبعد | `20260724224053_AddReportApproverAndKpiReviewerOverrides` (بلا تغيير) |
| سطور `Applying migration` في سجلّ RC | **0** |
| حزمة الواجهة الجديدة | `index-D8FXC2lW.js` |
| نسخة احتياطيّة للخلفيّة | `/opt/reporting-rc/publish-backup-kpirevoverride-20260726-212816` |
| نسخة احتياطيّة للواجهة | `/opt/reporting-rc/frontend/dist-backup-kpirevoverride-20260726-212816` |
| نسخة احتياطيّة لقاعدة البيانات | `/root/db-backups/reporting_rc-prekpirevoverride-20260726-212816.dump` |
| نسخة احتياطيّة قبل التسوية | `/root/db-backups/reporting_rc-prerecon-20260726-214002.dump` |

---

## 3) تهيئة بيانات UAT على RC (RC حصرًا)

RC ليس مرآةً لبيانات الإنتاج، فبُذرت البيانات الدنيا اللازمة لتمرين السيناريو، مع **الحفاظ على معرّفات الإنتاج نفسها** كي تكون تهيئة الأداة مطابقة تمامًا لما ستستعمله على Production لاحقًا:

- تقريران إضافيّان لفاطمة (`232f5c72…` على `2026-W28`، و`e729b926…` على `2026-W30`) مستنسخان من تقريرها القائم، بحُرّاس `NOT EXISTS`.
- تقييم KPI تاريخيّ لفاطمة على `2026-W28`: `Approved`، `TotalScore = 94.25`، `ReviewerId = NULL` (مطابقة الوضع الإنتاجيّ)، مع 12 نتيجة مؤشّر على إصدار القالب `6f6c1d88-46e0-4e07-8f91-1476b20fdefc` (12 مؤشّرًا، كلّها `Auto`، الهدف 100، مجموع الأوزان 100).
- بيانات دخول مؤقّتة لإبراهيم وأحمد عبر `UatCredentialTool --grant` مع لقطة استرجاع `/root/kpirev-uat-snapshot.json`. **استُرجعت بالكامل بعد UAT** (تحقُّق: `PasswordHash مطابق=True`، `SecurityStamp مطابق=True`، تنظيميّ/أدوار بلا تغيير=True)، ومُحي ملفّ كلمة المرور المؤقّتة نهائيًّا.

---

## 4) نتائج الاختبارات الخمسة عشر — **15/15 ناجحة**

| # | الاختبار | النتيجة | الدليل |
|---|---|---|---|
| 1 | إبراهيم يرى الأربعة فقط ضمن النطاق الإضافيّ | ✅ | `GET /kpi-evaluations/evaluatable-subjects` = 200، `count=4`، `isAdminOverride=false`، المجموعة تطابق {فاطمة، أحمد عبدالرؤوف، محمد عبدالله، محسن مجدي} |
| 2 | محمد عبدالله ومحسن مجدي أصبحا ظاهرَين | ✅ | كلاهما ضمن القائمة |
| 3 | اختيار فاطمة يفلتر الجدول عليها وحدها | ✅ | `GET /kpi-evaluations?subjectUserId=03b725e4…` = 200، كلّ الصفوف بموظّف واحد فقط |
| 4 | اختيار W28 يحمّل 94.25 معتمَد | ✅ | `GET /kpi-evaluations/lookup` = 200، `found=true`، `totalScore=94.25`، `status=Approved` |
| 5 | لا يُنشأ تقييم W28 ثانٍ | ✅ | عدد التقييمات قبل القراءة = بعدها = 2 |
| 6 | إبراهيم يستطيع إكمال وإرسال W29 | ✅ | إنشاء=200، حفظ 12 نتيجة=200، إرسال=200 |
| 7 | W29 يُعتمَد مباشرةً بسبب الـExplicit Override | ✅ | `status=Approved`، `reviewerId=7e2cb6ac…`، `reviewedAtUtc` مضبوط، حدث مراجعة `ApprovedByExplicitReviewerOverride`، تدقيق `kpi.approved_direct_by_reviewer_override` |
| 8 | لا يُوجَّه إلى أحمد عبدالرؤوف | ✅ | `reviewerId ≠ f4e25122…` |
| 9 | من لا يملك Override لا يحصل على اعتماد مباشر | ✅ | أحمد قيّم محمد عبدالله ⟶ `UnderReview` ووُجّه للمُراجِع الصريح (إبراهيم) لا اعتماد مباشر |
| 10 | لا تغيير في `ManagerId` ولا أدوار Identity | ✅ | بصمة تنظيميّة `98fbaf96ab0f171992ab4dc1a8a7ca8f` (35 مستخدمًا) وبصمة أدوار `09f0e66818f8406692b7b7e654c7d865` (40 صفًّا) متطابقتان قبل/بعد |
| 11 | تسوية تقارير فاطمة تُنتج W28/W29/W30 | ✅ | `b127a8f9…`⟶`2026-W28`، `232f5c72…`⟶`2026-W29`، `e729b926…`⟶`2026-W30` بلا تغيير؛ `W27` النشِط = 0 |
| 12 | تقارير بقيّة المستخدمين بلا تغيير | ✅ | بصمة `56ff1af17d46493eed2ad8ba6a12671b` (34 صفًّا) متطابقة قبل/بعد |
| 13 | إعادة تشغيل الأداة = `AlreadyApplied` | ✅ | التشغيل الثاني: الخطوتان `AlreadyApplied`، «عدد التغييرات المُطبَّقة: 0» |
| 14 | لا Migration | ✅ | رأس الهجرات ثابت `20260724224053`، فرق ملفات الهجرات = 0، سطور `Applying migration` = 0 |
| 15 | لا تغيير للبريد ولا للمجدول ولا لإعداداتهما | ✅ | مفاتيح `Email__*` و`EmailNotifications__*` و`ReportReminderScheduler__*` لم تُمَسّ، `email_outbox` = 0 صفّ |

---

## 5) تفصيل المسار A

### أ) توسيع نطاق التقييم
`EvaluatableSubjectScopeAsync` صار يشمل الموظّف إذا كان `u.ManagerId == uid` **أو** `u.KpiReviewerOverrideUserId == uid`، بشرط `u.IsActive && u.Id != uid`.
**لم يُمَسّ**: `ManagerId` لأيّ مستخدم، `ScopeResolver`، أدوار Identity؛ ولم يُمنح إبراهيم دور `Admin` (`isAdminOverride=false` في الاستجابة الحيّة).

### ب) فلترة القائمة
الواجهة صارت تمرّر `subjectUserId` المختار في النموذج إلى `GET /kpi-evaluations`، وأُسقط الاعتماد على `subjectFilter` غير المربوط بالنموذج.

### ج) مسار قراءة بلا أثر جانبيّ
`GET /api/kpi-evaluations/lookup?subjectUserId=&periodKey=&kpiTemplateId=|kpiTemplateVersionId=` — قراءة محضة، **بلا حارس الدورة المستقبليّة وبلا حارس الانطباق**، فلا يُخفى التقييم التاريخيّ بسبب الانطباق الحاليّ، ولا يُنشئ أيّ صفّ. صلاحيّة الوصول: الذات أو نطاق التقييم أو `ScopeResolver`. عند عدم المطابقة يُرجِع `Found=false` بلا خطأ.

### د) الاعتماد المباشر عند `evaluator == explicit reviewer`
عند الإرسال وبعد نجاح كلّ عمليّات التحقّق، إذا كان المستخدم الحاليّ هو **مُدخِل التقييم** و**المُراجِع الصريح** للموظّف معًا:
- الحالة ⟵ `Approved` مباشرةً، بلا سقوط إلى `ManagerId`، وبلا رسالة «مُراجِع غير صالح».
- تُضبط `ReviewerId` و`ReviewedAtUtc`، ويُسجَّل حدث مراجعة `ApprovedByExplicitReviewerOverride`.
- يُكتب تدقيق `kpi.approved_direct_by_reviewer_override` بنصّ صريح:
  > «الاعتماد المباشر تمّ لأنّ المُدخِل هو المُراجِع الصريح المعيَّن للموظّف (KpiReviewerOverrideUserId).»
- **الاستثناء لا يعمل بلا Override صريح** — مُثبَت باختبار 9 حيًّا.

### هـ) تقييم فاطمة التاريخيّ لـW28
لم يُعدَّل صفّه، ولم تُملأ `ReviewerId` بأثر رجعيّ (بقيت `NULL`)، وبقي `Approved` بدرجة 94.25 ويُعرَض للقراءة فقط.

---

## 6) تفصيل المسار B — أداة `FatmaPeriodReconciler`

- خارج `Reporting.sln`، تُقرأ تهيئتها من متغيّرات البيئة حصرًا، مع **حارس نطاق إلزاميّ** بالبريد المالك للتقارير.
- الوضع الافتراضيّ **DryRun**؛ التطبيق يحتاج `--apply` صريحًا.
- **معاملة واحدة** تغلّف الخطوتين بالترتيب الإلزاميّ: (1) `2026-W28 → 2026-W29`، ثمّ (2) `2026-W27 → 2026-W28`.
- النتائج الممكنة لكلّ خطوة: `Applied | AlreadyApplied | CollisionSkipped | SourceMismatchSkipped | NotFound`. أيّ خطوة محجوبة ⟶ تراجُع كامل.
- الحرّاس: الوجود، `!IsDeleted`، تطابق المالك، تطابق مفتاح المصدر، وفحص تصادم مسبق على الفهرس الفريد الجزئيّ `("ReportTemplateVersionId","SubmitterId","PeriodKey") WHERE "IsDeleted"=false`.
- التعديل مقصور على `PeriodKey` و`UpdatedAtUtc` فقط — **المعرّفات والمحتوى والحالة والاعتمادات والقيم المرتبطة كلّها محفوظة**.
- تدقيق `submission.period_reconciled` لكلّ `SubmissionId` مع الحمولة الكاملة.
- ملفّ تراجُع SQL يُكتب **قبل** الـCommit؛ فشل كتابته يُلغي المعاملة.

نتيجة التنفيذ على RC:

| المعرّف | قبل | بعد | الحالة |
|---|---|---|---|
| `b127a8f9-107e-41e1-9dee-2f9b957b7782` | `2026-W27` | `2026-W28` | `Submitted` (بلا تغيير) |
| `232f5c72-10cf-4c5a-9539-e296365fc7d5` | `2026-W28` | `2026-W29` | `Closed` (بلا تغيير) |
| `e729b926-eff3-4f81-8415-0db8568edced` | `2026-W30` | `2026-W30` | `Closed` (لم يُمَسّ) |

ملفّات التراجُع: `/root/recon-backups/fatma-period-reconciler-rollback-20260726-214011.sql` (التطبيق) و`…-214014.sql` (التشغيل الثاني، بلا تغييرات).

**لم تُمَسّ**: نسخة خالد مجدي المحذوفة، ولا أيّ مستخدم آخر، ولا أيّ تقرير آخر في W28/W29، ولا جدول `kpi_evaluations`.

---

## 7) المسار C — تقييمات KPI على `2026-W27`

سُلِّم تقرير تشخيصيّ مستقلّ **قراءة فقط بلا أيّ كتابة**: `Docs/Planning/KPI-W27-COLLISION-DIAGNOSTIC-REPORT-R1.md`.
خلاصته: خمسة تقييمات نشِطة على `2026-W27`، ثلاثة منها (أحمد صبحي، أميرة محمد، بسنت محمد) لها تقييم نشِط على `2026-W28` **بنفس `KpiTemplateVersionId`** ⇒ أيّ نقل مباشر يخرق فهرس التفرّد؛ واثنان فقط (أمير عادل، عائشة كمال) بلا تصادم وكلاهما `Draft` بلا درجة. **لم يُنقل أيّ تقييم، والمسار متوقّف بانتظار قرار مستقلّ.**

---

## 8) التنظيف

- أُزيلت سكربتات UAT وملفّات SQL المؤقّتة من الخادم.
- مُحي ملفّ كلمة المرور المؤقّتة نهائيًّا؛ لم تُطبع أيّ كلمة مرور أو توكن أو سلسلة اتّصال في أيّ مخرَج.
- لم يُنشأ أيّ Git tag.

---

## 9) مسارات التراجُع على RC

1. **الكود**: استعادة `publish-backup-kpirevoverride-20260726-212816` و`dist-backup-kpirevoverride-20260726-212816` ثمّ إعادة تشغيل الخدمة. لا هجرة لعكسها.
2. **التسوية**: تنفيذ `/root/recon-backups/fatma-period-reconciler-rollback-20260726-214011.sql`، أو استعادة `/root/db-backups/reporting_rc-prerecon-20260726-214002.dump`.

---

## 10) الحكم والتوقّف

المرشّح **مقبول على RC** بنتيجة **15/15**، بلا Migration، وبلا مساس بالبريد أو المجدول أو الأدوار أو العلاقات التنظيميّة.

**التوقّف هنا وفق التوجيه.** لن يُنفَّذ نشر Production ولا تسوية Production قبل تصريح مستقلّ صريح.
