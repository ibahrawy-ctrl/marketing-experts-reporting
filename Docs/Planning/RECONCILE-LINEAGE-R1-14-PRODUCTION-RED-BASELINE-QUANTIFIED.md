# RECONCILE-PROD-DEVELOP-LINEAGE — التقرير 14: التفكيك الكمّيّ الكامل لاحمرار نَسَب الإنتاج

**التذكرة:** `RECONCILE-PROD-DEVELOP-LINEAGE`
**المرحلة:** G — استكمال التقرير 1 (يُكمِّل ولا يُلغي التقرير 09)
**التاريخ:** 16 أغسطس 2026
**الحكم:** **الإنتاج الحيّ ليس معطوبًا. 164 من 165 دَين اختبارات، والباقي عيب تاريخيّ مشترك.**

---

## 1) القياس الفاصل — كلا الشجرتين على قواعد **مُعاد إنشاؤها نظيفة**

| الشجرة | القاعدة | تكاملي | وحدوي | المدّة |
|---|---|---|---|---|
| **Production/RC `ce166662`** | `rr_prod_{main,cal,pfe}` نظيفة | **Failed 165 / Passed 1613 / Total 1778** | 313/313 | 6 د 39 ث |
| **المرشَّح `ac0d86c` + إصلاحات G** | `rr_cand_{main,cal,pfe}` نظيفة | **Failed 1 / Passed 1981 / Total 1982** | 359/359 | 7 د 24 ث |

> تصحيح للرقم السابق: القياس القديم أعطى **159** على قاعدة `rr_prod_main` بحجم 102 MB.
> على قاعدة نظيفة العدد **165** — أي أنّ التلوّث كان **يُخفي** ستّة إخفاقات لا يضخّمها.
> **الاستنتاج لا يتغيّر**، بل يقوى: الاحمرار سلوك حتميّ للشجرة لا أثر بيئة.

---

## 2) التفكيك بالاسم الكامل (لا بالعدد)

مجموعة اختبارات المرشَّح التكامليّة = **1879** اسمًا فريدًا (`--list-tests`).

| الفئة | العدد | الحالة على المرشَّح |
|---|---|---|
| **موجود بالاسم في المرشَّح** | **131** | 130 **PASS** · 1 FAIL |
| **غائب بالاسم عن المرشَّح** | **34** | — |
| **المجموع** | **165** | |

الفشل الوحيد المشترك: `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_ButNot_ApproveRejectReopenDelete`
⟹ يفشل على **الأبوين والمرشَّح** ⟹ **Class C — عيب تاريخيّ مشترك** (`BASELINE-DEFECT-01`).

### 2.1 الـ131 الحاضرة — كلّها خضراء على المرشَّح عدا واحدًا

| الصنف | العدد | | الصنف | العدد |
|---|---|---|---|---|
| `ProjectRepeatableGridTests` | 15 | | `SalesAggregationCourseGroupedTests` | 3 |
| `ReportsTests` | 13 | | `ProjectFirstExecutionAggregationTests` | 3 |
| `RepeatableNumericValidationIntegrationTests` | 13 | | `NotificationLinkTests` | 3 |
| `B2bByServiceTemplateTests` | 11 | | `TemplateRoleGuardTests` | 2 |
| `ErdsPhase6ExecutiveDashboardTests` | 10 | | `TeamLeaderSalesScopeTests` | 2 |
| `B2cByCourseValidationTests` | 9 | | `ReportCadenceTests` | 2 |
| `SubmissionTests` | 6 | | `OrgHierarchyTests` | 2 |
| `ReportViewGrantTests` | 6 | | `NotificationTests` | 2 |
| `MultiProjectSectionTests` | 5 | | `KpiOverviewScopeTests` | 2 |
| `ScopeEnforcementTests` | 4 | | `SubmissionReminderTests` · `SalesContextTests` · `Phase6ClientProjectTests` · `ErdsPhase55WorkUnitTests` · `EmployeeProfileScopeTests` | 1 لكلٍّ |
| `SalesAggregationTests` | 4 | | **`AdminGovernanceTests`** | **1 (Class C)** |
| `ComplianceDueLateTests` | 4 | | | |
| `B2cNewOldTests` | 4 | | | |

**الدلالة:** 130 اختبارًا يفشل على الإنتاج و**ينجح على المرشَّح** ⟹ النَسَب الموحّد **يشفي** احمرار الإنتاج، لا يرثه.

### 2.2 الـ34 الغائبة — تفكيك مسبَّب

#### أ) `ErdsPhase55WorkUnitTests` — 14 اختبارًا: **إعادة تسمية مقصودة لا حذف**

| على الإنتاج | على المرشَّح |
|---|---|
| `ContentPerHour_Computed` | `ContentPerHour_HistoricalRead_Computed` |
| `Design_WorkHoursColumn_PresentAndAggregated` | `Design_HistoricalRead_WorkHoursColumn_PresentAndAggregated` |
| `Projects_StillHasWorkHours` | `Projects_HistoricalRead_StillHasWorkHours` |
| … (14 اسمًا بنفس النمط) | … |

- المرشَّح يحوي **16** اختبارًا في هذا الصنف (14 مُعاد تسميتها + اختباران جديدان):
  `WorkUnitTemplate_IsArchived_AndRejectsNewSubmission` و`Phase4_B2cB2b_Unaffected`.
- **السبب:** قوالب وحدة العمل صارت **مؤرشَفة** وترفض تسليمًا جديدًا؛ فتحوّلت الاختبارات إلى قراءة تاريخيّة (`HistoricalRead`) وأُضيف اختبار يحرس الأرشفة نفسها.
- **سبب احمرارها على الإنتاج:** 14/14 منها فشلت برمز `calendar.cycle_not_open` — تحاول إنشاء تسليم جديد على دورة لم تبدأ.
- **الحكم:** **تغطية مُحسَّنة لا منقوصة** (16 > 14). لا فقد.

#### ب) `ReportsTests` — 20 اختبار تجميع: **حذف مُبرَّر — نموذج التسليم الذي تختبره أُلغي**

الأسماء المحذوفة (5 عائلات × 4 أدوار):
`ContentWriterRollup_{Employee,TeamLeader,GeneralManager,Ceo}` ·
`DesignerRollup_{…}` · `ModerationRollup_{…}` · `SocialOpsRollup_{…}` · `VideoRollup_{…}`

| البند | الإثبات |
|---|---|
| موجودة عند قاعدة التفرّع `6fd2253` | **نعم** (25 موضعًا) |
| موجودة على الإنتاج `ce16666` | **نعم** (25 موضعًا) |
| موجودة على `develop 10c26f7` | **لا** (0) |
| الالتزام الحاذف | **`d922e59`** — *«RC-4 Baseline: Project-First execution…»* — 11 يوليو 2026 |
| حجم الحذف من الملفّ | **918 سطرًا محذوفًا، 0 مُضاف** |
| تبرير صريح في رسالة الالتزام | **لا يوجد** — لكنّه **ثبت بالفحص** (§ب-2 أدناه) |

##### ب-2) التبرير المُثبَت بالفحص (تصحيح لفرضيّة أوّليّة)

فُرِض ابتداءً أنّ الحذف بلا مبرّر، فجُرِّبت **استعادة الـ25 اختبارًا فعليًّا** في المرشَّح مع مراسي
فترات صالحة (`TestCalendar.Cycle(18…42)` بدل `2026-W81…W205`). النتيجة: **بناء ناجح، ثمّ فشل 25/25**
عند مُحلِّلات القوالب نفسها لا عند التأكيدات:

```
System.InvalidOperationException : Sequence contains no matching element
  at ResolveDesignerTemplateAsync  → version.Fields.Single(f => f.Label == DesignerReportSchema.RequestedDesigns)
  at ResolveVideoTemplateAsync     → version.Fields.Single(f => f.Label == VideoReportSchema.RequestedVideos)
  at ResolveModerationTemplateAsync, ResolveContentWriterTemplateAsync — بنفس التوقيع
```

الفحص المباشر لقاعدة المرشَّح يكشف السبب:

| القالب | الإصدارات | الإصدار المنشور |
|---|---|---|
| `تقرير فريق التصميم` | 1, 2, 3, **4** | **4** |
| `تقرير فريق الفيديو` | 1, 2, 3, **4** | **4** |

هذه القوالب رُقِّيت إلى **تصنيف التنفيذ v3 ثمّ v4**
(`TemplateSeeder.UpgradeExecutionTemplatesToTaxonomyV3Async` / `…V4Async`)، فاستُبدلت الحقول
الرقميّة المسطّحة (`RequestedDesigns`, `DeliveredDesigns`, `ApprovedFirstTime`…) بقسم مشروع متكرّر
(`FieldType.ProjectRepeatableSection`) ضمن نموذج **Project-First** الذي أدخله `d922e59` نفسه.

⟹ الاختبارات العشرون كانت تبني تسليماتها على **بنية حقول لم تعد موجودة**؛ فحذفها لم يكن إسقاط
تغطية بل **إزالة اختبارات لنموذج مُلغى**. تغطيتها انتقلت إلى:
`ProjectRepeatableGridTests` · `MultiProjectSectionTests` · `ProjectFirstExecutionAggregationTests`
· `ErdsPhase55WorkUnitTests.*_HistoricalRead_*`.

⟹ **التصنيف الصحيح: Class G — Superseded Test** (لا «حذف تغطية»). **تراجعتُ عن الاستعادة**
وأُعيد `ReportsTests.cs` مطابقًا لـ`HEAD` بايتًا (829 سطرًا، غير مُعدَّل في `git status`).

**هل ضاعت الميزة الإنتاجيّة نفسها؟ لا.** الأسطح المنتِجة سليمة ومتطابقة في الشجرتين:

| الملفّ | المواضع |
|---|---|
| `src/Reporting.Api/Controllers/ReportsController.cs` | 10 |
| `src/Reporting.Application/Reports/ReportModels.cs` | 21 |
| `src/Reporting.Application/Reports/IReportingService.cs` | 5 |
| `src/Reporting.Infrastructure/Services/ReportingService.cs` | 51 |
| **المجموع** | **87** — متطابق حرفيًّا بين `/tmp/recon-parent-dev` و`/tmp/recon-int` |

⟹ **Production Live Feature Regression = 0** لهذه العائلة (نقاط النهاية الخمس قائمة وتخدم القراءة
التاريخيّة للتسليمات المُنشأة قبل الترقية).
⟹ و**Coverage Regression = 0** أيضًا: التغطية انتقلت إلى اختبارات النموذج الجديد كما في §ب-2.

**سبب احمرارها على الإنتاج:** مفاتيح أسابيع **اصطناعيّة خارج المدى** كانت تُستعمل قديمًا لضمان التفرّد:
`"2026-W60"`, `"2026-W71"…"2026-W74"`, `"2026-W81"…"2026-W85"`, `"2026-W91"…"2026-W99"` (و`"2026-W53"` في `NotificationTests`).
البوّابة الحيّة ترفضها ⟹ لا يُنشأ تسليم ⟹ التجميع يُرجِع `Rows = 0` فيفشل التأكيد بـ`Expected: 2 / Actual: 0`.
أي أنّها **Class H — Invalid Fixture Anchor** بعينها، لا عطبًا في محرّك التجميع.

---

## 3) الجدول النهائيّ لاحمرار الإنتاج (165)

| السبب الجذريّ | العدد | يدلّ على عطب في الإنتاج الحيّ؟ |
|---|---|---|
| مفاتيح فترات اصطناعيّة/مستقبليّة ترفضها بوّابات التقويم الحيّة | **~150** | **لا** |
| دلالات مُتجاوَزة (users-first · أرضيّة الانطباق · أرشفة قوالب وحدة العمل · سبت المبيعات) | **~14** | **لا** |
| عيب تاريخيّ مشترك (`BASELINE-DEFECT-01`) | **1** | **لا** (قائم على الأبوين معًا) |
| عطب فعليّ في منتج الإنتاج | **0** | — |

**الحكم النهائيّ:** الاحمرار البالغ 165 هو **دَين اختبارات إنتاجيّ** بالكامل. لا يوجد ولا مؤشّر واحد على أنّ التطبيق المنشور على `reports.emarketingacademy.net` معطوب. وهذا يطابق الواقع التشغيليّ: النظام يخدم مستخدمين فعليّين، والبوّابات التي «تُفشِل» الاختبارات هي بالضبط ما يحمي الإنتاج من إنشاء تقارير عن فترات لم تبدأ.

---

## 4) بنود مغلقة — لا بند مفتوح

| المعرّف | الحالة |
|---|---|
| `COVERAGE-GAP-01` (فجوة تغطية مزعومة في اختبارات التجميع الخمسة) | **باطل ومغلق** — الحذف مُبرَّر بترقية القوالب إلى v4/Project-First (§2.2-ب-2)، والتغطية منتقلة لا مفقودة. جُرِّبت الاستعادة عمليًّا فأثبتت الإبطال، ثمّ تُراجِع عنها بالكامل |

**لا بند يحجب البوّابة §14.** الميزة نفسها سليمة بمطابقة مصدر حرفيّة (87 موضعًا في 4 ملفّات، متطابقة بين الشجرتين).
