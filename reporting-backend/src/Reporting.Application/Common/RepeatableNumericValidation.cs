using System.Text.Json;

namespace Reporting.Application.Common;

/// <summary>
/// PROJECT-REPEATABLE-NUMERIC-VALIDATION-R1 — عقد تحقّق رقميّ قابل لإعادة الاستخدام للحقول الرقميّة
/// داخل قسم المشاريع المتكرّر (ProjectRepeatableSection). دوال ثابتة صرفة (بلا حالة/بلا قاعدة بيانات)
/// كي تكون مصدر الحقيقة الوحيد للقواعد الرقميّة ولتكون قابلة للاختبار وحدةً مباشرةً.
///
/// المبدأ الحاكم — توافق خلفيّ تامّ: الحقل لا يخضع لأيّ فرض رقميّ إلا إذا كان نوعه رقميًّا
/// وحمل قيدًا واحدًا على الأقل (min/max/integerOnly/step). القوالب القديمة بلا قيود تبقى بلا تغيير.
/// </summary>
public static class RepeatableNumericValidation
{
    // أكواد أخطاء مستقرّة قابلة للقراءة آليًّا (تُرجَع كما هي وتُغلَّف برسالة عربية + موضع الخطأ في المستدعي).
    public const string NumberInvalid = "report.repeatable_number_invalid";
    public const string BelowMin = "report.repeatable_number_below_min";
    public const string AboveMax = "report.repeatable_number_above_max";
    public const string IntegerRequired = "report.repeatable_number_integer_required";
    public const string StepInvalid = "report.repeatable_number_step_invalid";
    public const string ConfigInvalid = "report.repeatable_config_invalid";

    // الأنواع الرقميّة المسموح لها بقيود داخل القسم المتكرّر.
    public static readonly IReadOnlySet<string> NumericTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Number", "Currency", "Decimal", "Percentage" };

    public static bool IsNumericType(string? type) => type is not null && NumericTypes.Contains(type);

    /// <summary>هل يخضع الحقل للتحقّق الرقميّ؟ (نوع رقميّ + قيد واحد على الأقل).</summary>
    public static bool HasConstraint(string? type, decimal? min, decimal? max, bool integerOnly, decimal? step)
        => IsNumericType(type) && (min is not null || max is not null || integerOnly || step is not null);

    /// <summary>تعريف القيود صالح ما لم يكن Min>Max أو Step&lt;=0.</summary>
    public static bool IsConstraintDefinitionValid(decimal? min, decimal? max, decimal? step)
    {
        if (min is decimal mn && max is decimal mx && mn > mx) return false;
        if (step is decimal st && st <= 0m) return false;
        return true;
    }

    /// <summary>
    /// يستخرج قيمة رقميّة من إجابة PRS سواء كانت رمز JSON رقميًّا أو نصًّا رقميًّا
    /// (يدعم الخانات العربية/الفارسية عبر NumericNormalizer، وبثقافة ثابتة).
    /// </summary>
    public static bool TryGetNumber(JsonElement e, out decimal value)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Number:
                return e.TryGetDecimal(out value);
            case JsonValueKind.String:
                return NumericNormalizer.TryParseDecimal(e.GetString() ?? string.Empty, out value);
            default:
                value = 0m;
                return false;
        }
    }

    /// <summary>
    /// يتحقّق من قيمة رقميّة مُحلَّلة مقابل القيود؛ يُرجِع كود الخطأ المناسب أو null إن كانت صالحة.
    /// الترتيب: عدد صحيح ⇒ حدّ أدنى ⇒ حدّ أقصى ⇒ خطوة الإدخال.
    /// </summary>
    public static string? ValidateParsed(decimal num, decimal? min, decimal? max, bool integerOnly, decimal? step)
    {
        if (integerOnly && num != Math.Truncate(num)) return IntegerRequired;
        if (min is decimal mn && num < mn) return BelowMin;
        if (max is decimal mx && num > mx) return AboveMax;
        if (step is decimal st && st > 0m)
        {
            var baseline = min ?? 0m;
            var quotient = (num - baseline) / st;
            if (Math.Abs(quotient - Math.Round(quotient)) > 0.0000001m) return StepInvalid;
        }
        return null;
    }
}
