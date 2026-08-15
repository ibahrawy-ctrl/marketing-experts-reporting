# MODERATION-CONTENT-PERFORMANCE-R1B — PHASE 0: Baseline / Lineage / Safety

> التاريخ: 2026-07-20 · قراءة فقط · لا كود/لا نشر/لا DB تغيّر · الحكم في نهاية التقرير.

---

## 0) الحكم (Gate)
**BASELINE/LINEAGE BLOCKED — DO NOT IMPLEMENT.**

الأداة (artifact) المُجهَّزة لـR1B تعيش على سلالة **مُتباعدة عن الإنتاج ومتأخّرة عنه جزئيًّا**؛ نشرها كما هي سيُسقِط ميزة Unified Status التي أُغلقت للتوّ على الإنتاج، ويُدخِل ميزات غير منشورة (Client 360 + Project-First Taxonomy)، ويحمل تغيير TemplateSeeder (V6) ذا خطر معروف على مُجمِّع الإنتاج. **يلزم قرار صريح على (أ) أساس النشر و(ب) نطاق R1B قبل أي تنفيذ.**

---

## 1) حالة المستودع (مُثبَتة)
- الفرع: `develop` · HEAD = `6859ee0` (`test(clients): add Client 360 foundation integration tests (CPW-R1B)`).
- `origin/develop` = `6859ee0` (متطابق).
- **شجرة العمل الأساسية ملوّثة**: 82 تغييرًا (KPI/Email/Submissions/TemplateSeeder/… + ملفات مودريشن) ⇒ ممنوع العمل داخلها (قيد إلزاميّ).
- **worktree R1B قائم** `/private/tmp/release-mod-content-r1b-20260719-014808` (detached @ `6859ee0`)، دلتاه:
  - `TemplateSeeder.cs` (+129) — **بذر قالب V6**.
  - `Reporting.Infrastructure.csproj` (+5).
  - `TemplateTaxonomyV4Tests.cs` (تعديل).
  - `SubmissionsPage.tsx` (+220/−…).
  - جديد: `ModerationContentPerformanceV6Tests.cs` + `ModerationContentPerformanceV6.test.tsx`.
  - ⇒ **R1B ليس Frontend-only**؛ يمسّ Backend Seeder + قالب V6 + اختبارات تكامل.

## 2) سلالة الإنتاج مقابل سلالة المودريشن (الحقيقة الحاسمة)
- **الإنتاج الحاليّ = `3eee204`** (سلالة `bd84115` = لقطة الإنتاج + Unified Status، أُغلقت اليوم).
- **قاعدة التباعد** بين `develop` و`3eee204` = `6fd2253` (RC-4 baseline). `develop` +11 commit، `3eee204` +8 commit، **ولا أحدهما سلف للآخر**.
- **`develop@6859ee0` ينقصه Unified Status frontend**: `reporting-frontend/src/lib/unifiedBanner.ts` و`components/WeeklyCycleCalendarPicker.tsx` **غائبان تمامًا** (حاضران في `3eee204` فقط) ⇒ بناء الواجهة من develop ونشره على الإنتاج **يُسقِط ميزة Unified Status المنشورة**.
- **`develop@6859ee0` يضيف ميزات غير منشورة على الإنتاج**: Client 360 (CPW-R1B، 6 commits) + Project-First Execution/Execution Taxonomy (RC-4) ⇒ نشره يُدخِل شيفرة لم تُراجَع/تُعتمَد إنتاجيًّا.
- **8 commits سلالة الإنتاج** (غير موجودة في develop): Unified Status، bd84115 parity (Restore/Archive Governance R1 + Fatma-Direct + Admin Governance + BypassTeamLeader)، Approval UX، Navigation Hotfix، Role-Aware Calendar Phase 2، Admin Governance R1، Production lineage reconstruction.

## 3) قابلية إعادة التوطين على `3eee204` (تقييم أوّليّ)
- التبعيات الجوهرية للواجهة موجودة في `3eee204`: `ProjectRepeatableEntry`/`parseGrid`/`GridDisplay` حاضرة في `SubmissionsPage.tsx`. `projectStatusLabel` مُصدَّر من `format.ts` (يلزم استيراده فقط).
- **لكن** `SubmissionsPage.tsx` يتباعد ~**272 سطرًا** بين `3eee204` و`6859ee0` (101 إدراج/171 حذف) ⇒ دلتا R1B المُجهَّزة على 6859ee0 **لن تنطبق نظيفة** على الإنتاج؛ تلزم **إعادة اشتقاق** الدلتا مقابل نسخة `3eee204`.
- **`UpgradeModerationToPerformanceV5Async` غير مُلتزَم في أيّ من الفرعين** (0 في 6859ee0 و3eee204) — موجود في شجرة العمل الملوّثة غير المُلتزَمة فقط، مطابقًا للتشخيص السابق «سيدر V5 taxonomy غير منشور، خطر إعادة كتابة V5 الإنتاجيّ».

## 4) خطر TemplateSeeder V6 (مُوثَّق سابقًا)
تشخيص R1 المؤكَّد: القالب الحيّ على الإنتاج `db8c764d` V5 (Vocabulary 1) مع تسليمَين فقط (شيماء صالح). أيّ سيدر مودريشن من سلالة develop يعيد كتابة V5 لمفاتيح taxonomy ⇒ **يكسر `ModerationRollupAsync` على بيانات الإنتاج القائمة**. جاهزية البيانات = **LOW** (تسليمان، مستخدم واحد، حقول معطوبة: converted_opportunities سالبة، cases_grid الأعمدة الحرجة فارغة، لا platform/violation_type/severity). ⇒ V6/Backfill يتطلّب **موافقة Migration/Seeder مستقلّة + إثبات بيانات** (قيد إلزاميّ).

## 5) تصنيف الحالة
| البند | الحالة |
|---|---|
| شجرة العمل الأساسية | ملوّثة (82 تغييرًا) — ممنوع العمل داخلها |
| worktree R1B الحاليّ (6859ee0) | **Stale/Divergent** — متأخّر عن الإنتاج (ينقصه Unified Status) ومتقدّم بميزات غير منشورة |
| نطاق R1B المُجهَّز | Backend Seeder V6 + قالب + اختبارات + Frontend — **ليس Frontend-only** |
| أساس النشر الصحيح | `3eee204` (الإنتاج الحاليّ) — **لا `6859ee0`** |
| قابلية التطبيق المباشر | لا (SubmissionsPage يتباعد 272 سطرًا) — تلزم إعادة اشتقاق |
| جاهزية البيانات | LOW (2 تسليم، مستخدم واحد، حقول معطوبة) |

## 6) القرارات المطلوبة قبل التنفيذ (تتطلّب موافقة صريحة)
1. **أساس النشر**: إعادة توطين دلتا المودريشن المعتمَدة فوق `3eee204` (نمط unified-status re-lineage) في worktree معزول جديد — لا العمل من 6859ee0. **موافقة على الأساس مطلوبة.**
2. **نطاق R1B**: تحديد أيّ المسارين:
   - **(أ) R1A Frontend-only** (مواءمة Vocabulary 1 فوق قالب V5 الحيّ — بلا Backend/Seeder/Migration/قالب جديد)، أو
   - **(ب) R1B V6 Structured Extension** (قالب V6 + Seeder + احتمال Migration + Backfill) — يتطلّب **موافقة Migration/Seeder مستقلّة + إثبات بيانات (PHASE 4)** ويصطدم بجاهزية بيانات LOW.
3. لا نشر RC/Production، لا Account Manager، لا تغيير بوّابات Email/Reminders/Scheduler، لا تغيير Unified Status/Workflow — تبقى كما هي.

## 7) لم يُمَسّ شيء
قراءة فقط بالكامل. لا كود، لا بناء، لا نشر، لا DB. الإنتاج `3eee204` + Unified Status سليم. RC سليم. البوّابات دون تغيير.

---

## الحكم النهائي
**BASELINE/LINEAGE BLOCKED — DO NOT IMPLEMENT.** بانتظار موافقة صريحة على (أساس النشر = `3eee204`) و(نطاق R1B = A أم B). لا انتقال إلى Account Manager.
