# ROOT CAUSE & ARCHITECTURE DECISION — PROJECT360-MULTI-WORK-ITEMS-AND-REPORT-DISCOVERY-CLOSURE-R2

> وثيقة بوّابة: كُتِبت **قبل** أيّ تعديل كود، بناءً على قياس فعليّ للمصدر ولقاعدة الإنتاج (قراءة فقط).
> كلّ رقم أدناه مقيس لا مفترض، وكلّ ادّعاء مرفق بملفّ ورقم سطر أو باستعلام.

---

## 0) بوّابة النسَب (§5.1–§5.4)

| المقياس | القيمة المقيسة |
|---|---|
| `HEAD` المحلّيّ (فرع `develop`) | `736b5c567b0dde2511dd91ac8fcb1c9cd466b951` |
| هل `HEAD` المحلّيّ يحتوي `63b7d42`؟ | **NO** — الفرع المحلّيّ متأخّر عن الأصل |
| `origin/develop` (بعد `fetch` ناجح بالمفتاح المخصَّص) | `897c9b187ab4216213b4f453ec65948cd06dff27` |
| هل `origin/develop` يحتوي `PREVIOUS_FIX_SHA=63b7d42`؟ | **YES** (`63b7d42` → `59f483e` → `897c9b1`) |
| `origin/main` | `508509ad8474b321c80cbdd48eb84ecb54bee212` |
| TEST المنشور | `1.0.0+63b7d42f2d0cc54899b22cc919045389c23b2ec7` · md5 `06d09ba2e39f9468c509be8f97927df2` · `/health` = 200 · MainPID 1589037 |
| RC المنشور | `1.0.0+897c9b187ab4216213b4f453ec65948cd06dff27` · MainPID 1592241 |
| الإنتاج المنشور | `1.0.0+7e063b493b50ad90ba6131e47042c7cd035fb65b` · md5 `ddf9598c0bf00f821a0aefe0c6cc1975` · MainPID 1556574 |

**الحكم:** لا تعارض. TEST متأخّر بـcommit واحد عن `origin/develop` (`897c9b18` أضاف `DEF-P123-RC-001` المنشور على RC)، وهذا **مفسَّر** ولا يستدعي التوقّف.

**خطّ الأساس المعتمد لهذه التذكرة:** `BASE_SHA = 897c9b187ab4216213b4f453ec65948cd06dff27` — أحدث خطّ آمن يحتوي `PREVIOUS_FIX_SHA`.
**Worktree نظيف:** `.claude/worktrees/p360-r2-20260826` · فرع `feature/p360-multi-workitem-r2-20260826` · `git status` = 0 ملفّ عند الإنشاء.

**أدلّة التذكرة السابقة غير الملتزَمة (§5.4 — لم تُمسّ ولن تُحذف):**
`.claude/worktrees/p360-navfix-20260826` على فرع `fix/project360-project-scoped-report-nav-r1` تحوي
`Ops/R21/PROJECT360-PROJECT-SCOPED-REPORT-NAVIGATION-FIX-{REPORT.md,EVIDENCE.docx,EVIDENCE.pdf}` و`Ops/UAT/P360-NAVFIX-20260826/`.

---

## 1) نموذج التخزين الفعليّ (§5.5)

| العنصر | الحقيقة المقيسة |
|---|---|
| نوع الحقل | `FieldType.ProjectRepeatableSection` — `Reporting.Domain/Enums/Enums.cs:71` (القيمة الرمزيّة 22) |
| **تخزين `FieldType` في PostgreSQL** | **نصّ (`varchar`) لا رقم** — `WHERE "FieldType"='ProjectRepeatableSection'` |
| كيان الحقل | `Reporting.Domain/Entities/Templates/TemplateField.cs` — `ReportTemplateVersionId, Label, Key, FieldType, Order, IsRequired, HelpText, ConfigJson` |
| كيان القيمة | `Reporting.Domain/Entities/Submissions/SubmissionFieldValue.cs` — `ValueText/ValueNumber/ValueDate/ValueBool/ValueJson` |
| **نوع عمود `ValueJson`** | **`jsonb`** — `Persistence/Configurations/SubmissionConfigurations.cs:41` |
| نوع عمود `ConfigJson` | **`jsonb`** (ثبت بفشل `length(jsonb)` في psql) |
| فهارس `submission_field_values` | اثنان فقط: `PK` و`IX_..._ReportSubmissionId_TemplateFieldId`. **لا فهرس GIN على `ValueJson`** |
| استعمالات JSON في الاستعلامات | **صفر** — لا `EF.Functions.JsonContains` ولا `JsonExists` في المستودع كلّه |

### مخطّط `ConfigJson` الحاليّ (v1) — مأخوذ حرفيًّا من الإنتاج

```json
{
  "projectRequired": true,
  "minProjects": 1,
  "maxProjects": 0,
  "fields": [
    { "key": "content_type", "type": "Select", "label": "نوع المحتوى",
      "options": ["Carousel","Static Post","Reel","..."], "columns": null,
      "required": true, "catalogDomain": "content_type" },
    { "key": "content_goal", "type": "Select", "label": "هدف المحتوى", "required": true, "catalogDomain": "content_goal" },
    { "key": "work_status",  "type": "Select", "label": "حالة العمل",  "required": true, "catalogDomain": "work_status" },
    { "key": "count",        "type": "Number", "label": "العدد",       "required": true },
    { "key": "notes",        "type": "LongText", "label": "ملاحظات",   "required": false }
  ]
}
```

نظير C#: `SubmissionService.RepeatableConfig` (س1461-1467) و`RepeatableField` (س1469-1481).

### مخطّط `ValueJson` الحاليّ (v1)

```json
[ { "projectId": "<guid>", "answers": { "<key>": <scalar|string> } } ]
```

نظير C#: `SubmissionService.RepeatableEntry` (س1488-1492).
**تحقّق مباشر على الإنتاج:** `jsonb_object_keys` على كلّ عناصر كلّ الصفوف = **`projectId` و`answers` فقط** (لا أثر لأيّ مفتاح آخر).

### Snapshot القالب (§5.11) — الحقيقة

**لا يوجد عمود Snapshot ولا نسخة JSON مجمَّدة.** التسليم يحمل `ReportSubmission.ReportTemplateVersionId`، و**إصدار القالب نفسه هو النسخة المحفوظة**: تعديل القالب يُنشئ إصدارًا جديدًا، والتقارير القديمة تبقى مربوطة بإصدارها.

> **الأثر التصميميّ الحاسم:** أيّ تعديل على تعريف القالب يجب أن يكون **بإصدار جديد**؛ ولو عُدِّل `ConfigJson` لإصدار مستعمَل فعلًا لتغيّر عرض تقارير تاريخيّة ⟹ ممنوع (§7).

---

## 2) القوالب المستعمِلة للقسم (§5.6–§5.7)

| المقياس | القيمة |
|---|---|
| قوالب مميّزة تستعمل القسم | **11** |
| إصدارات مميّزة | **49** |
| حقول قسم | **49** ⟹ **حقل واحد بالضبط لكلّ إصدار** (لا إصدار فيه حقلان — استعلام `HAVING count(*)>1` أعاد 0 صفًّا) |

القوالب: كاتب المحتوى الأسبوعي · متابعة مقالات SEO الأسبوعي · فريق التصميم · فريق الفيديو · فريق SEO · المديرشن الأسبوعي · فريق الويب · تشغيل الأكاديمية · النمو والأداء (Media Buyer) · إدارة الحسابات العملاء · مدير الحسابات.

عناوين حقل القسم مختلفة فعليًّا بين القوالب («تفاصيل المشروع» · «مقالات SEO حسب المشروع» · «حالة مشاريع الويب» · «أداء الحملات حسب العميل / المشروع» · «تفاصيل العميل»…) ⟹ **التسمية Template-Driven بالفعل**، ولا يجوز تثبيت أيّ اسم في الكود.

الإصدارات المستعمَلة فعليًّا من تسليمات: **16 زوج (قالب، إصدار)**.

---

## 3) السبب الجذريّ الأوّل — تعذّر تسجيل عدّة أنواع عمل داخل المشروع نفسه

**المصدر:** `Reporting.Infrastructure/Services/SubmissionService.cs:1571-1595`
دالّة `ValidateRepeatableSectionsAsync`، الحارس:

- س1573: `var seenProjects = new HashSet<Guid>();`
- س1591-1595: `if (!seenProjects.Add(pid)) errors.Add($"قسم «{sec.Label}»: لا يمكن تكرار نفس المشروع أكثر من مرة في التقرير الواحد.");`

**السبب الجذريّ ليس هذا الحارس** — الحارس **صحيح ومطلوب** (§4.2 يُبقيه). السبب الجذريّ هو:

> **بنية `ValueJson` مسطَّحة بمستوى واحد: `Project → answers` (إجابة واحدة لكلّ حقل لكلّ مشروع).**
> لا يوجد أيّ موضع في المخطّط يستوعب «عدّة بنود عمل داخل المشروع الواحد»، فاضطُرّ المستخدم إلى تكرار المشروع كحيلة، فاصطدم بحارس تفرّد صحيح.

**تأكيد بالكود:** `RepeatableEntry` (س1488-1492) فيه `ProjectId` + `Answers` فقط — لا مجموعة متداخلة. `RepeatableConfig.Fields` قائمة مسطّحة، والنوع الوحيد شبه المركّب `Grid` يُخزَّن `string[][]` داخل إجابة واحدة ⟹ **جدول نصّيّ بلا هويّة عناصر ولا تحقّق لكلّ بند ولا قابليّة للاستعلام**، فلا يصلح بديلًا عن بنود العمل.

**فحص التحقّق في الواجهة:** **لا يوجد أيّ تحقّق ضدّ التكرار في الواجهة إطلاقًا.**
`reporting-frontend/src/pages/SubmissionsPage.tsx` — `ProjectRepeatableEditor` (س1524-1656)؛ `addEntry` (س1536) يضيف `{projectId:null, answers:{}}` بلا فحص؛ زرّ `+ إضافة مشروع` (س1652) معطَّل فقط عند `maxProjects`.

**⟹ جذر تكرار الـToasts:** التكرار لا يُكتشف إلّا عند الحفظ في الخادم، والخادم يُعيد **قائمة أخطاء** (`List<string>`) لا خطأً واحدًا — والحلقة في س1575-1632 تضيف رسالة **لكلّ صفّ مكرَّر**، ولكلّ حقل مطلوب في كلّ صفّ. مستخدم كرّر المشروع 3 مرّات بـ4 حقول مطلوبة يولّد رسائل متعدّدة تُعرض متتابعة.

**قيود قاعدة البيانات:** لا يوجد قيد تفرّد على `ProjectId` داخل القيمة. القيد الوحيد على مستوى التسليم: `(ReportTemplateVersionId, SubmitterId, PeriodKey)` فريد بمرشِّح `IsDeleted=false` — `SubmissionConfigurations.cs:19-20`.

**اختبارات قائمة تحرس السلوك:** `tests/Reporting.IntegrationTests/MultiProjectSectionTests.cs` — `Submit_DuplicateProject_Returns400` (س153-169) و`Submit_TwoDistinctProjects_Succeeds` (س171-190). **يجب أن يبقيا خضراوين.**

---

## 4) السبب الجذريّ الثاني — فشل اكتشاف تقارير المشروع

**المصدر:** `Reporting.Infrastructure/Services/ProjectService.cs:256`
```
var rows = await LinkedReportsAsync(s => s.ProjectId == id, ct);
```

التذكرة وصفت هذا بأنّه «قد لا يظهر تقرير». **القياس على قاعدة الإنتاج يُظهر أنّ الأمر أسوأ بكثير ومنهجيّ:**

| القياس (إنتاج، قراءة فقط) | القيمة |
|---|---|
| إجماليّ التسليمات | 311 |
| تسليمات لها `ProjectId` علويّ **غير فارغ** | **2** |
| تسليمات `ProjectId = NULL` | **309** |
| تسليمات تحمل قيمة للقسم المتكرّر | **76** (Closed 66 · Submitted 8 · Draft 2) |
| منها ما له `ProjectId` علويّ | **2** |
| بطاقات مشروع لا يطابق `projectId` فيها `Submission.ProjectId` | **261** |
| تسليمات متأثّرة | **75** |
| **مشاريع مميّزة محجوبة عن قوائم تقاريرها** | **32** من أصل **34** مشروعًا (**94%**) |

**الاستنتاج المقيس:** `Submission.ProjectId` حقل شبه مهجور (2/311 = 0.6%). الشرط `s.ProjectId == id` يجعل قائمة «تقارير المشروع المرتبطة» **فارغة فعليًّا لـ32 من 34 مشروعًا**، و**74 من 76 تقريرًا حاملًا لبيانات مشاريع لا يظهر في قائمة أيّ مشروع**.

**لماذا ظهر تقرير أحمد في لقطة المستخدم إذن؟** لأنّ مشروع التذكرة `1f23cea4-682e-4dc4-a72c-ac7be39d2356` («حملات إعلانية») هو **أحد الاستثنائين الوحيدين**: التسليم `1caffdb6-0a94-41db-831e-765bc025bfda` (Submitted، `2026-W35`) يحمل `ProjectId` علويًّا يساوي هذا المشروع. وفي الوقت نفسه توجد **بطاقتان** أخريان تشيران إلى المشروع نفسه من داخل `ValueJson` **ولا تظهران**.

توزيع المشاريع داخل التسليم الواحد: 6 تسليمات بمشروع واحد · **70 تسليمًا بمشروعين فأكثر** · الأقصى **11 مشروعًا**.

**نقاط النهاية المعنيّة:**
| المسار | الموضع |
|---|---|
| `GET /api/projects/{id}/reports` | `ProjectsController.Reports` س29-31 → `ProjectService.GetReportsAsync` س247-258 |
| `GET /api/projects/{id}/reports/{submissionId}` | `ProjectsController.ReportSlice` س37-39 → `GetReportSliceAsync` س294-350 |
| `GET /api/clients/{id}/reports` | `ClientsController.Reports` س31-33 |

**الشريحة (`GetReportSliceAsync`) سليمة ولا تعاني هذه الفجوة**: تصفّي على `FieldType.ProjectRepeatableSection` وتستخرج بـ`ExtractProjectEntries` (س359-…) بمطابقة `projectId` داخل العنصر. أي **الفتح المباشر يعمل والاكتشاف يفشل** — وهو بالضبط ما وصفته التذكرة.

**ملاحظة أثر جانبيّ مقيسة:** `GetSummaryAsync` (س269) يستعمل الشرط المهجور نفسه `s.ProjectId == id` لحساب عدّادات المشروع (إجماليّ/مغلق/معلّق/آخر إرسال) ⟹ **عدّادات مساحة المشروع 360 صفريّة بالخطأ لـ32 مشروعًا**. هذا يفسّر — بدليل جديد — لماذا بدت مساحة المشروع «كأنّها لم تُحمَّل» في بلاغ التذكرة السابقة: لم تكن فارغة لأنّ المشروع بلا أهداف فحسب، بل لأنّ **عدّاد التقارير نفسه كان يقرأ حقلًا مهجورًا**.

---

## 5) القرار المعماريّ (ADR-R2-001)

### السياق
مطلوب: `Project Entry 1 → N Work Items` مع (أ) توافق تامّ مع 76 تسليمًا تاريخيًّا و49 إصدار قالب، (ب) حلّ عامّ لا استثناء لقالب بعينه، (ج) بلا كسر لصيغة التقارير التاريخيّة.

### الخيارات المدروسة

| # | الخيار | الحكم |
|---|---|---|
| A | تطبيع كامل: جداول `report_project_entries` + `report_work_items` | **مرفوض الآن** — يتطلّب Backfill لـ261 بطاقة عبر 49 إصدارًا، ويُنشئ مصدر حقيقة ثانيًا بجوار `ValueJson` (ممنوع بـ§5 من التذكرة السابقة). قد يكون مسارًا لاحقًا مستقلًّا. |
| B | استعمال `Grid` القائم كبنود عمل | **مرفوض** — `string[][]` بلا مفاتيح ولا تحقّق لكلّ بند ولا `required` لكلّ عمود؛ يخالف «حقول بنود العمل Template-Driven». |
| C | تخفيف حارس التفرّد والسماح بتكرار المشروع | **مرفوض صراحةً** — §4.2 يوجب `PROJECT_ENTRY_UNIQUE_PER_REPORT = YES`. |
| **D** | **امتداد عامّ داخل المخطّط القائم: مجموعة متكرّرة متداخلة اختياريّة (`workItems`) بـ`schemaVersion`** | **معتمد** |

### القرار (D) — تفصيلًا

**1) امتداد `ConfigJson` (اختياريّ بالكامل، إضافيّ بحت):**
```json
{
  "schemaVersion": 2,
  "projectRequired": true, "minProjects": 1, "maxProjects": 0,
  "fields": [ ... حقول على مستوى المشروع، تبقى كما هي ... ],
  "workItems": {
    "key": "work_items",
    "label": "بنود العمل",
    "itemLabel": "بند عمل",
    "addLabel": "+ إضافة بند عمل",
    "minItems": 1,
    "maxItems": 0,
    "uniqueBy": [],
    "fields": [ ... نفس نحو RepeatableField حرفيًّا ... ]
  }
}
```
- `workItems` **غائب ⟹ سلوك v1 حرفيًّا** (كلّ الإصدارات الـ49 الحاليّة).
- حقول بند العمل تعيد استعمال **نفس** `RepeatableField` (`key,label,type,required,options,columns,catalogDomain,min,max,integerOnly,step`) ⟹ صفر نحو جديد، وكلّ التسميات من القالب (`Content Type` / `Design Type` / `SEO Activity` … بلا أيّ ترميز صلب).
- `uniqueBy` **فارغة افتراضًا** ⟹ تكرار نوع العمل **مسموح** ما لم ينصّ القالب صراحةً (§4.2).
- `maxItems = 0` ⟹ بلا حدّ.

**2) امتداد `ValueJson` (إضافيّ بحت):**
```json
[ { "projectId": "<guid>", "answers": { ... }, "workItems": [ { "answers": { ... } } ] } ]
```
- `workItems` غائب/`null` ⟹ عنصر قديم (legacy) يُقرأ كما هو تمامًا.
- **قاعدة التوافق عند القراءة (Read Adapter):** إذا عرّف القالب `workItems` وكان العنصر المخزَّن بلا `workItems` ذات عناصر، يُعرَض على أنّه **بند عمل واحد ضمنيّ مشتقّ من `answers`** — **بلا أيّ كتابة على المخزَّن**.

**3) قواعد التحقّق:**
- تفرّد `projectId` **يبقى كما هو** (`seenProjects`) — لا تخفيف.
- عدد بنود العمل يخضع لـ`minItems`/`maxItems`.
- `required` والقيود الرقميّة تُطبَّق **لكلّ بند عمل على حدة** بإعادة استعمال `RepeatableNumericValidation` (مصدر الحقيقة الوحيد القائم).
- رسائل الخطأ تُجمَّع وتُرقَّم بالمشروع والبند بدل تكرار رسالة عامّة.

**4) لماذا هذا هو «أقلّ امتداد عامّ ممكن»:** لا كيان جديد · لا جدول جديد · لا مصدر حقيقة جديد · لا هجرة بيانات · لا تغيير على `FieldType` · لا لمس لـ`ScopeResolver` ولا للهيكل التنظيميّ · وكلّ الإصدارات القائمة تعمل حرفيًّا كما هي لأنّ المفتاحين الجديدين اختياريّان.

### إصلاح الاكتشاف (ADR-R2-002)

استبدال `s.ProjectId == id` بشرط **OR** يُفرَض **خادميًّا**:

```
s.ProjectId == id
  OR EXISTS (
       submission_field_values v JOIN template_fields f ON f.Id = v.TemplateFieldId
       WHERE v.ReportSubmissionId = s.Id
         AND f.FieldType = 'ProjectRepeatableSection'
         AND v.ValueJson @> '[{"projectId":"<id>"}]'
     )
```

- **تقييد النوع إلزاميّ**: بلا `f.FieldType = ProjectRepeatableSection` قد يطابق `@>` أيّ `jsonb` آخر يصادف احتواء المفتاح ⟹ إرجاع تقرير لا يخصّ المشروع (ممنوع بـ§8).
- `EXISTS` يمنع تكرار صفوف التقرير حين يظهر المشروع في أكثر من موضع ⟹ يحقّق شرط «لا تكرار» بلا حاجة إلى `Distinct` لاحق.
- التصفية **كلّها في الخادم**؛ لا جلب-ثمّ-تصفية في الواجهة.
- **الفهرس:** يُضاف فهرس `GIN (ValueJson jsonb_path_ops)` بهجرة **إضافيّة بحتة (إنشاء فهرس فقط، بلا أيّ DML)**. الحجم الحاليّ صغير فلا ضرورة أداء آنيّة، لكنّه يمنع تدهورًا مستقبليًّا ويُقاس بـ`EXPLAIN` قبل/بعد.
- **`GetSummaryAsync` يُصحَّح بالمنطق نفسه** وإلّا بقيت عدّادات 360 كاذبة (فجوة مقيسة أعلاه).

### التوافق التاريخيّ (§7)

| الفئة | القرار |
|---|---|
| تقارير Submitted/Closed (74) | **لا تُلمَس البتّة** — لا كتابة، لا دمج، لا تغيير إصدار. تُقرأ عبر Read Adapter بلا تعديل مخزَّن. |
| إصدارات القوالب الـ49 القائمة | **لا تُعدَّل** — أيّ تفعيل لبنود العمل يكون **بإصدار قالب جديد** حصرًا. |
| المسوّدات (**2 فقط**) | لا تحتاج تحويلًا: المخطّط الجديد إضافيّ والقراءة متوافقة ⟹ `DRAFTS_CONVERTED = 0` و`DRAFT_CONVERSION_DATA_LOSS = 0` بلا أيّ Backfill. |
| هجرة بيانات | **لا توجد** — الهجرة الوحيدة المقترحة هي **فهرس** لا يمسّ صفًّا واحدًا. |

**⟹ `SUBMITTED_REPORTS_MUTATED = NO` مضمون بنيويًّا لا بالوعد: لا يوجد في هذه التذكرة أيّ مسار كتابة على تسليمات قائمة.**

---

## 6) عقد العرض Project-Scoped (§9) — الأثر

`GetReportSliceAsync` يبقى كما هو أمنيًّا (رفض موحّد `project.not_found`، تصفية على نوع الحقل وحده)، ويُضاف إليه إخراج `workItems` الخاصّة بالمشروع المطلوب فقط ضمن `ProjectReportSliceFieldDto`. الفلترة تبقى **قبل** مغادرة الخادم ⟹ `FRONTEND_ONLY_FILTERING = NO`.

---

## 7) قواعد التوقّف — الحالة

| قاعدة (§19) | الحالة |
|---|---|
| الإصلاح السابق غير موجود في النسَب | **غير محقّقة** — موجود في `origin/develop` |
| TEST على SHA غير مفسَّر | **غير محقّقة** — `63b7d42` متأخّر commit واحد بسبب مفسَّر (`DEF-P123-RC-001`) |
| البيانات القديمة لا تُحوَّل بلا فقد | **غير محقّقة** — لا تحويل أصلًا (امتداد إضافيّ) |
| كسر صيغة التقارير التاريخيّة | **غير محقّقة** — المفتاحان الجديدان اختياريّان |
| تعذّر فرض Project Scope خادميًّا | **غير محقّقة** — مفروض ومُختبَر |
| الاستعلام الجديد قد يعيد تقارير خارج النطاق | **مُعالَجة بالتصميم** — تقييد على نوع الحقل + بوّابة `CanViewProject` قبل الاستعلام |
| تشعّب نسَب الهجرات | **غير محقّقة** — TEST 45=45 عند آخر قياس |
| تغييرات منتج غير مرتبطة في الـworktree | **غير محقّقة** — الـworktree أُنشئ نظيفًا (0 ملفّ) |

**لا قاعدة توقّف مُفعَّلة ⟹ المضيّ في التنفيذ مأذون ضمن حدود التذكرة (TEST كحدّ أقصى).**

---

## 8) ملخّص المخرجات المتوقَّعة من التنفيذ

- خادم: امتداد `RepeatableConfig`/`RepeatableEntry` + تحقّق بنود العمل + إصلاح `GetReportsAsync` و`GetSummaryAsync` + إخراج `workItems` في الشريحة + هجرة فهرس GIN.
- واجهة: محرّر متداخل (مشروع → بنود عمل)، منع تكرار المشروع **قبل** الحفظ برسالة واحدة وتوجيه إلى البطاقة القائمة، عرض بنود العمل في القراءة والشريحة، حالات Loading/Empty/Denied/Error مميّزة.
- اختبارات: وحدويّة + تكامليّة (سيناريو A/B) + واجهة، مع إبقاء `MultiProjectSectionTests` خضراء.
