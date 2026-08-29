# REAL-TEMPLATE-ROOT-CAUSE — لماذا نجح قالب UAT الصناعيّ وفشل القالب التشغيليّ الحقيقيّ

**التذكرة:** MASTER CORRECTIVE TICKET — REAL OPERATIONAL MULTI-WORK-ITEM ACTIVATION & FUNCTIONAL ACCEPTANCE R2A
**التاريخ:** 29 أغسطس 2026 · **الحالة:** تشخيص مكتمل بالقياس · **لم تُمَسّ أيّ بيئة حتّى هذه اللحظة** (قراءة فقط).

---

## 0) الحالة الحيّة المقيسة (لا مفترضة)

| البند | القيمة المقيسة | مصدر القياس |
|---|---|---|
| `origin/develop` | `16983d2d4f5a3116aeea630af5b7c5fbcf21ce10` | `git rev-parse origin/develop` |
| `develop` المحلّيّ | كان `736b5c5` (متأخّر 56) ⇒ قُدِّم سريعًا إلى `16983d2` بلا force | `git merge --ff-only` |
| خدمة TEST | `khubara-reporting-test.service` · `active running` · MainPID 1606410 | `systemctl` |
| مسار الخلفيّة | `/opt/reporting-test/publish/Reporting.Api.dll` (483,840 B · Aug 26 20:21) | `ls -la` |
| إصدار الخلفيّة المنشور | `1.0.0+f8c4ad298a06e13e2f8c793110f17aef0822910a` | `strings Reporting.Api.dll` |
| منفذ الخلفيّة | `127.0.0.1:5091` | `nginx` + `systemd` |
| قاعدة بيانات TEST | **`reporting_test_uat`** | `/etc/khubara-reporting-test.env` (`Database=…`) |
| جذر الواجهة | `/opt/reporting-test/frontend/dist` · `index.html` Aug 26 20:43 | `nginx` + `ls -la` |
| حزمة الواجهة | `index-BSGCZnf1.js` · `index-CmcRamKF.css` | `index.html` |

الخلفيّة `f8c4ad2` والواجهة (المبنيّة من `706c8fe`) كلتاهما **بعد** `d934fc9` (كوميت الميزة) ⇒ **البايتات المنشورة تحوي شيفرة بنود العمل المتعدّدة كاملة**.

---

## 1) الخلاصة القاطعة

> **الشيفرة المنشورة على TEST تدعم بنود العمل المتعدّدة دعمًا كاملًا. العطل ليس في الشيفرة إطلاقًا — بل في بيانات القالب التشغيليّ الحقيقيّ: إصدارُه المنشور ما زال مخطّط v1 بلا `workItems`.**
> والميزة كلّها **مقادة بالقالب (Template-Driven)** بتصميمها: غياب `workItems` من `ConfigJson` يعني — بحكم الشيفرة نفسها — **إخفاءً كاملًا** لزرّ «إضافة بند عمل» ولكامل تدفّق البنود.

ولهذا نجح قالب UAT الصناعيّ: لأنّه — وهو وحده — أُنشئ بمخطّط v2.

---

## 2) المقارنة المباشرة بين القالبين (القياس الحاسم)

### 2.1 القالب التشغيليّ الحقيقيّ
- **`ReportTemplateId`** = `9e375ad7-8a65-46f4-849f-886d5b795bfe`
- **العنوان** = `تقرير كاتب المحتوى الأسبوعي` · `Weekly` · `Status=Published` · `IsActive=true` · `Classification=Primary` · أُنشئ `2026-07-12`
- **الإصدار المنشور الفعّال** = **v4** `0ac84366-5a10-4e2c-8de0-bfa8010cd034` (5 حقول)
- **`ConfigJson` للحقل `ProjectRepeatableSection`** — مفاتيحه العليا المقيسة:

```
['fields', 'maxProjects', 'minProjects', 'projectRequired']
schemaVersion = (غير موجود)
workItems     = (غير موجود)
حقول مستوى المشروع = ['content_type', 'content_goal', 'work_status', 'count', 'notes']
```

⇒ **كلّ حقول العمل (نوع المحتوى، الهدف، الحالة، العدد، الملاحظات) ما زالت على مستوى بطاقة المشروع لا على مستوى بند العمل** — وهو بالضبط ما يجبر الموظّف على تكرار المشروع.

### 2.2 قالب UAT الصناعيّ (المؤقّت)
- **`ReportTemplateId`** = `aed0016c-398d-4e27-a901-43a2c9097fe8` · `UAT R2 — بنود العمل المتعدّدة (مؤقّت)` · `Classification=Supplementary` · أُنشئ `2026-08-26 20:26`

```
مفاتيح ConfigJson = ['fields', 'workItems', 'maxProjects', 'minProjects', 'schemaVersion', 'projectRequired']
schemaVersion = 2
حقول مستوى المشروع = ['project_goal', 'project_notes']
workItems = {key:'work_items', label:'بنود العمل', addLabel:'+ إضافة بند عمل',
             itemLabel:'بند عمل', minItems:1, maxItems:0, uniqueBy:[]}
حقول بند العمل = ['content_type', 'work_status', 'count', 'item_notes']
```

### 2.3 الفارق في جملة واحدة
| | القالب الحقيقيّ v4 | قالب UAT |
|---|---|---|
| `schemaVersion` | — | `2` |
| `workItems` | — | معرَّف بأربعة حقول |
| موضع حقول العمل | بطاقة المشروع | بند العمل |
| النتيجة في الواجهة | **لا زرّ ولا بنود** | زرّ + بنود متعدّدة |

---

## 3) موضع الانقطاع في سلسلة التفعيل (مقيس بالشيفرة)

سلسلة التفعيل: `تعريف القالب → الإصدار المنشور → لقطة التسليم → مصيّر الواجهة → تفاعل المستخدم → حمولة الحفظ → تحقّق الخادم → ValueJson المخزَّن → محوّل القراءة → الواجهة بعد التحميل → شريحة المشروع`

**الانقطاع يقع عند الحلقة الأولى (تعريف القالب/الإصدار المنشور).** كلّ الحلقات التالية سليمة ومنشورة:

| الحلقة | الشاهد | الحكم |
|---|---|---|
| نوع الإعداد في الواجهة | `reporting-frontend/src/types/api.ts:1236-1237` — `schemaVersion?` و`workItems?` **اختياريّان**، وغيابهما = «سلوك v1 حرفيًّا» | سليم |
| تطبيع الإعداد | `SubmissionsPage.tsx:732-733`, `742-745` — `normalizeWorkItems` تُعيد `undefined` إن خلت `fields` | سليم |
| بوّابة المصيّر | `SubmissionsPage.tsx:1665` — `const wi = config.workItems ?? null;` | **هنا يُخفى الزرّ** |
| صفّ جديد | `SubmissionsPage.tsx:1673` — `wi ? {…, workItems:[{answers:{}}]} : {…}` | يعتمد على `wi` |
| إضافة بند | `SubmissionsPage.tsx:1713-1714` — `addItem` موجودة وصحيحة | سليم لكنّه غير مبلوغ |
| العرض | `SubmissionsPage.tsx:1920-1923` — `if (!wi \|\| items.length === 0) return null;` | يعتمد على `wi` |
| تخطّي ميفولة العرض | `SubmissionsPage.tsx:1870` — `config.workItems ? null : resolvePresentationProfile(...)` | سليم |
| حفظ/تحقّق الخادم | `SubmissionService.cs:1467-1518` (`SchemaVersion`, `RepeatableWorkItemsConfig`, `RepeatableWorkItem`) و`1599-1611` (تحقّق حقول البنود) | سليم ومنشور |
| شريحة المشروع | `ProjectReportSliceModels.cs` + `ProjectService.cs` — `workItems` ضمن عقد الشريحة | سليم ومنشور |

**الاستنتاج:** لا سطر واحد يحتاج تغييرًا لتظهر الميزة؛ يحتاج القالب الحقيقيّ إصدارًا جديدًا بمخطّط v2 فقط.

---

## 4) العيب الثاني — الجُمود الذي أوقف التذكرة السابقة (409)

التقرير السابق ذكر تعذّر إنشاء إصدار جديد بسبب مسودات أعادت `409`. **السبب الجذريّ مقيس:**

`reporting-backend/src/Reporting.Infrastructure/Services/ReportTemplateService.cs:721-722`

```csharp
if (template.Versions.Any(v => !v.IsPublished))
    return Result<TemplateVersionDto>.Failure("يوجد إصدار مسودة مفتوح بالفعل.", "version.draft_exists.conflict");
```

الحارس يفحص **أيّ** إصدار غير منشور، بصرف النظر عن رقمه. وحالة القالب الحقيقيّة:

| الإصدار | `Id` | `IsPublished` | حقول | تسليمات |
|---|---|---|---|---|
| v1 | `cf29dadf-7b34-4d96-aff8-73bfbc9811f5` | **false** | 16 | 0 |
| v2 | `63706888-e178-45e9-bc72-107c83d85fb1` | **false** | 5 | 0 |
| v3 | `3595e9d5-ffb9-4004-ab62-55477368f79a` | **false** | 5 | 0 |
| v4 | `0ac84366-5a10-4e2c-8de0-bfa8010cd034` | **true** | 5 | 0 |

ثلاث مسودات **أرقامها أدنى من الإصدار المنشور** (بقايا بذر أوّليّ). ولأنّ اختيار الإصدار الفعّال هو «أعلى رقم منشور» (`ReportTemplateService.cs:359-360`)، فهذه المسودات:
1. **غير قابلة للبلوغ وظيفيًّا** — نشرها لا يغيّر الإصدار الفعّال (يبقى v4).
2. **تُجمِّد القالب إلى الأبد** — لا يمكن إنشاء إصدار جديد عبر الـAPI مطلقًا.

**الشذوذ ليس محصورًا بهذا القالب.** المقيس على TEST — أربعة قوالب بالحالة نفسها (3 مسودات + منشور واحد v4):
`تقرير كاتب المحتوى الأسبوعي` · `تقرير المديرشن الأسبوعي` · `تقرير فريق الفيديو` · `تقرير فريق التصميم`.

المخرج الوحيد عبر الـAPI اليوم هو `DeleteVersionAsync` (حذف المسودات) — **وهو ممنوع بنصّ التذكرة.**

---

## 5) حالة بيئة UAT على TEST (تؤثّر على خطّة التنفيذ)

| البند | المقيس | الأثر |
|---|---|---|
| إسنادات القالب الحقيقيّ | **0** | لا يستطيع أيّ موظّف إنشاء تقرير منه ⇒ يلزم إسناد للـUAT |
| تسليمات القالب الحقيقيّ | **0** (على كلّ إصداراته) | لا تقارير تاريخيّة تتأثّر بإصدار جديد |
| إجمالي التسليمات في القاعدة | 17 | كلّها على قوالب أخرى |
| حسابات `p360r2.%@r2uat.test` | موجودة (6) لكن **مقفلة ومعطّلة** عند إغلاق R22 | يلزم تفعيل أو بديل |
| عملاء/مشاريع الطُعم | **محذوفة** (`cleanup.sql`) | يلزم إنشاء مشروع للسيناريو |
| ملفّات الأسرار (`.pw`, `.adminpw`, `.basic`) | **حُذِفت عمدًا** عند الإغلاق | **يلزم تزويدها من المستخدم** |

---

## 6) نتائج التشخيص الأوّل المطلوبة في §5 من التذكرة

القيم أدناه مستنتَجة **قطعًا** من قياس البيانات + الشيفرة المنشورة (لا تخمين)، ولم تُثبَت بصريًّا بعد لتعذّر تسجيل الدخول (أسرار TEST محذوفة):

```
ACTUAL_TEMPLATE_HAS_SCHEMA_V2       = NO   (لا مفتاح schemaVersion إطلاقًا)
ACTUAL_TEMPLATE_HAS_WORK_ITEMS_CONFIG = NO (لا مفتاح workItems إطلاقًا)
ADD_WORK_ITEM_BUTTON_PRESENT        = NO   (محكوم بـSubmissionsPage.tsx:1665)
SECOND_WORK_ITEM_VISIBLE            = NO
SECOND_WORK_ITEM_SAVED              = NO
SECOND_WORK_ITEM_RELOADED           = NO
TEMPLATE_ASSIGNMENTS_FOR_REAL_TEMPLATE = 0 (لا يمكن أصلًا فتح تقرير منه اليوم)
```

⇒ **العيب الفعليّ = القالب التشغيليّ الحقيقيّ لم يُفعَّل على مخطّط v2 قطّ، والجُمود البرمجيّ (§4) هو ما منع تفعيله.**

---

## 7) ما لم يُمَسّ

- RC: لم يُمَسّ. الإنتاج: لم يُمَسّ. `origin/main`: لم يُمَسّ. TEST: **قراءة فقط** حتّى الآن (لا كتابة، لا نشر، لا هجرة).
- لا مسودة قالب حُذِفت ولا عُدِّلت. لا تقرير تاريخيّ تغيّر.
- نسخة احتياطيّة مؤكَّدة بـmd5 لكلّ الملفّات غير المتتبَّعة قبل التقديم السريع: `~/Documents/_repo-untracked-backup-20260829` (169 ملفًّا · 13MB · 0 اختلاف md5).
