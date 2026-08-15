# PROJECT-REPEATABLE-NUMERIC-VALIDATION-CONTRACT-R1 — عقد التحقّق الرقميّ للحقول داخل قسم المشاريع المتكرّر

> الحالة: مُنفَّذ ومختبَر (RC). فرع `feature/project-repeatable-numeric-validation-r1` (أصل `046f0e0`).
> بلا Migration، بلا تعديل نموذج بيانات، بلا كسر لأيّ قالب قائم.

## 1) السياق والدافع
كشف تشخيص `CONTENT-CREATOR-EXECUTIVE-REPORT-R1-V6-DATA-VALIDATION-BLOCKER-DIAGNOSIS` أنّه لا يوجد أيّ مِفصَل (hinge) — لا في الواجهة ولا في الخادم — للتعبير عن قيود رقميّة (`min`/`integerOnly`) على الحقول الرقميّة الداخليّة لقسم المشاريع المتكرّر (ProjectRepeatableSection = «PRS»). هذا هو الجذر التاريخيّ لظهور قيم مثل `approved_first_time = -1`. يضيف هذا العقد آليّة عامّة قابلة لإعادة الاستخدام تسمح لكلّ حقل رقميّ داخليّ بأن يُعرِّف اختياريًّا `min`/`max`/`integerOnly`/`step`، وتُفرَض باتّساق في مُحرِّر الواجهة، والتحقّق العميليّ، والتحقّق الخادميّ عند التسليم.

## 2) المبدأ الحاكم (توافق خلفيّ تامّ)
لا يخضع الحقل لأيّ فرض رقميّ إلّا حين يكون **نوعه رقميًّا** ويحمل **قيدًا واحدًا على الأقلّ** (min أو max أو integerOnly=true أو step). حقل بلا قيود ⇒ يسلك تمامًا كما اليوم ⇒ القيمة التاريخيّة `-1` تبقى صالحة. لا فرض ضمنيّ لـ`min=0` عالميًّا. لا إبطال رجعيّ لأيّ تسليم تاريخيّ.

## 3) الأنواع الرقميّة
`Number`, `Currency`, `Decimal`, `Percentage` (مقارنة غير حسّاسة لحالة الأحرف عبر `StringComparer.OrdinalIgnoreCase`). أيّ نوع آخر (`ShortText`/`LongText`/`Date`/…) لا يخضع للتحقّق الرقميّ حتى لو حمل قيودًا.

## 4) شكل القيود في ConfigJson
كلّ حقل فرعيّ داخل `fields[]` قد يحمل اختياريًّا:
```jsonc
{
  "key": "pieces", "label": "عدد القطع", "type": "Number", "required": true,
  "min": 0,            // decimal? — حدّ أدنى
  "max": 100,          // decimal? — حدّ أقصى
  "integerOnly": true, // bool — يرفض العشريّ
  "step": 1            // decimal? — خطوة الإدخال (>0)
}
```
غياب المفاتيح ⇒ لا قيود ⇒ توافق خلفيّ. `step<=0` يُهمَل عند التحليل (يُعامَل كغياب).

## 5) مصدر الحقيقة الوحيد — `RepeatableNumericValidation`
`reporting-backend/src/Reporting.Application/Common/RepeatableNumericValidation.cs` — صنف ثابت صرف (بلا حالة/بلا قاعدة بيانات) قابل للاختبار الوحدويّ المباشر. الدوال:
- `IsNumericType(type)` — هل النوع رقميّ؟
- `HasConstraint(type,min,max,integerOnly,step)` = `IsNumericType && (min!=null || max!=null || integerOnly || step!=null)`.
- `IsConstraintDefinitionValid(min,max,step)` — false إذا `min>max` أو `step<=0`.
- `TryGetNumber(JsonElement,out decimal)` — Number⇒TryGetDecimal، String⇒`NumericNormalizer` (يطبّع الخانات العربية/الفارسية ثم `decimal.TryParse` بثقافة ثابتة)، غيرهما⇒false.
- `ValidateParsed(num,min,max,integerOnly,step)` — يُرجِع كود الخطأ أو null.

## 6) أكواد الأخطاء (مستقرّة، قابلة للقراءة آليًّا)
| الكود | المعنى |
|---|---|
| `report.repeatable_number_invalid` | القيمة ليست رقمًا قابلًا للتحليل |
| `report.repeatable_number_below_min` | أقلّ من الحدّ الأدنى |
| `report.repeatable_number_above_max` | أكبر من الحدّ الأقصى |
| `report.repeatable_number_integer_required` | مطلوب عدد صحيح |
| `report.repeatable_number_step_invalid` | لا يطابق خطوة الإدخال |
| `report.repeatable_config_invalid` | تعريف قيود غير صالح (min>max أو step<=0) |

## 7) ترتيب التحقّق (حاسم)
داخل `ValidateParsed`: **عدد صحيح ⇒ حدّ أدنى ⇒ حدّ أقصى ⇒ خطوة**. مثال: قيمة عشريّة سالبة مع `integerOnly+min=0` تُرجِع `IntegerRequired` أوّلًا (لا `BelowMin`). تفاوت الخطوة يُحتسَب نسبةً إلى `min ?? 0` بتسامح `0.0000001m`.

## 8) الفرض الخادميّ (السلطة النهائيّة)
`SubmissionService.ValidateRepeatableSectionsAsync` (تُستدعى من `SubmitAsync`). تخطوات: فحص عدد المشاريع (min/max) ⇒ فحص صحّة تعريف القيود (`IsConstraintDefinitionValid` وإلّا `ConfigInvalid` + متابعة) ⇒ `numericFields = Fields.Where(HasNumericConstraint)` ⇒ منع تكرار المشروع (HashSet) ⇒ الحقول المطلوبة ⇒ حلقة الرقميّات: تخطّي الفارغ، `TryGetNumber` وإلّا `NumberInvalid`، ثم `ValidateParsed`. صيغة الخطأ: `"{code} | قسم «{section}» الحقل «{field}» الصف {row}: {detail}"`. الأخطاء تُجمَّع وتُرجَع عبر `Result.Failure(string.Join("، ", errors), "submission.repeatable_section_invalid")` ⇒ HTTP 400، والجسم الخام يتضمّن كود الخطأ.

## 9) الفرض العميليّ (الواجهة)
`reporting-frontend/src/pages/SubmissionsPage.tsx`:
- `parseRepeatableConfig` ⇒ `normalizeSubField` يقرأ القيود الأربعة (ويُهمِل `step<=0`).
- `validateRepeatableNumber(sf, raw)` يعيد null لغير الرقميّ/بلا قيود/الفارغ، وإلّا رسالة عربية مطابقة لكود الخادم.
- `ProjectRepeatableEditor` يُسقِط `min`/`max`/`step` على `<input type="number">` (`integerOnly ⇒ step = step ?? 1`) ويعرض رسالة الخطأ العميليّة.

## 10) نوع الواجهة
`reporting-frontend/src/types/api.ts`: `RepeatableSubField` مُوسَّع بـ `min?: number; max?: number; integerOnly?: boolean; step?: number;`.

## 11) التوافق الخلفيّ — أدلّة
- قراءة RC (قراءة فقط) لكلّ قوالب PRS القائمة: 40 حقلًا داخليًّا عبر 11 قالبًا، **صفر منها يحمل قيدًا** ⇒ لا تغيير سلوك.
- اختبارات صريحة: حقل بلا قيود لا يُنتِج خطأً لأيّ قيمة (بما فيها `-1` و`12.5`).

## 12) التخزين وتمثيل القيمة
PRS ValueJson = `List<{ projectId: Guid?, answers: Dictionary<string,JsonElement> }>`. الإجابات الرقميّة تُخزَّن كقيم نصّيّة؛ لذا `TryGetNumber` يدعم String وNumber. لا تغيير في مخطّط التخزين.

## 13) لا تغيير في النموذج
`dotnet ef migrations has-pending-model-changes` ⇒ «No changes». لا Migration جديدة، لا تعديل ModelSnapshot، لا حقول قاعدة بيانات.

## 14) التغطية الاختباريّة
- وحدة الخادم: `RepeatableNumericValidationTests.cs` (44).
- وحدة الواجهة: `RepeatableNumericValidation.test.tsx` (N1–N17).
- تكامل معزول: `RepeatableNumericValidationIntegrationTests.cs` (I1–I13) على قاعدة معزولة `reporting_pfe_iso` عبر `PfeNumericIsolatedFactory`، بمفاتيح فترات ديناميكيّة (`System.Globalization.ISOWeek`) بلا أيّ مستقبل مثبَّت.

## 15) الحدود (خارج النطاق)
لا نشر إنتاجيّ، لا Migration، لا تصحيح بيانات تاريخيّة، لا نشر قوالب، لا إنشاء Content Creator v6، لا تغيير مفاتيح قوالب قائمة، لا تغيير معادلات التجميع، لا فرض خاصّ بـ Content Creator داخل الكود المشترك.
