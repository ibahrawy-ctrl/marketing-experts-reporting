using System.Text.Json;
using Reporting.Application.Common;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// PROJECT-REPEATABLE-NUMERIC-VALIDATION-R1 — مواصفة عقد التحقّق الرقميّ القابل لإعادة الاستخدام
/// للحقول الرقميّة داخل قسم المشاريع المتكرّر. تُبرهن الوحدةُ المبدأَ الحاكم: لا فرض رقميّ إلا حين
/// يكون النوع رقميًّا وحمل قيدًا واحدًا على الأقل — فالقوالب القديمة بلا قيود تبقى بلا تغيير.
/// </summary>
public class RepeatableNumericValidationTests
{
    // ── IsNumericType ──────────────────────────────────────────────────────
    [Theory]
    [InlineData("Number", true)]
    [InlineData("Currency", true)]
    [InlineData("Decimal", true)]
    [InlineData("Percentage", true)]
    [InlineData("number", true)]        // غير حسّاس لحالة الأحرف
    [InlineData("PERCENTAGE", true)]
    [InlineData("ShortText", false)]
    [InlineData("LongText", false)]
    [InlineData("Date", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsNumericType_RecognizesNumericTypesOnly(string? type, bool expected)
        => Assert.Equal(expected, RepeatableNumericValidation.IsNumericType(type));

    // ── HasConstraint: توافق خلفيّ — بلا قيود ⇒ لا فرض ───────────────────────
    [Fact]
    public void HasConstraint_NumericTypeNoConstraints_False()
        => Assert.False(RepeatableNumericValidation.HasConstraint("Number", null, null, false, null));

    [Fact]
    public void HasConstraint_NonNumericTypeWithConstraints_False()
        => Assert.False(RepeatableNumericValidation.HasConstraint("ShortText", 0m, 100m, true, 1m));

    [Theory]
    [InlineData("Number", 0.0, null, false, null)]   // min فقط
    [InlineData("Number", null, 100.0, false, null)]  // max فقط
    [InlineData("Number", 0.0, 100.0, false, null)]   // min+max
    [InlineData("Number", null, null, true, null)]    // integerOnly فقط
    [InlineData("Number", null, null, false, 0.5)]    // step فقط
    public void HasConstraint_NumericTypeWithAnyConstraint_True(
        string type, double? min, double? max, bool integerOnly, double? step)
        => Assert.True(RepeatableNumericValidation.HasConstraint(
            type, (decimal?)min, (decimal?)max, integerOnly, (decimal?)step));

    // ── IsConstraintDefinitionValid ────────────────────────────────────────
    [Fact]
    public void IsConstraintDefinitionValid_MinGreaterThanMax_False()
        => Assert.False(RepeatableNumericValidation.IsConstraintDefinitionValid(100m, 0m, null));

    [Fact]
    public void IsConstraintDefinitionValid_StepZeroOrNegative_False()
    {
        Assert.False(RepeatableNumericValidation.IsConstraintDefinitionValid(null, null, 0m));
        Assert.False(RepeatableNumericValidation.IsConstraintDefinitionValid(null, null, -1m));
    }

    [Theory]
    [InlineData(0.0, 100.0, 1.0)]
    [InlineData(null, null, null)]
    [InlineData(5.0, 5.0, null)]     // min==max مسموح
    [InlineData(null, null, 0.1)]
    public void IsConstraintDefinitionValid_ValidDefinitions_True(double? min, double? max, double? step)
        => Assert.True(RepeatableNumericValidation.IsConstraintDefinitionValid(
            (decimal?)min, (decimal?)max, (decimal?)step));

    // ── TryGetNumber: رمز رقميّ / نصّ رقميّ / خانات عربية / غير صالح ───────────
    [Fact]
    public void TryGetNumber_JsonNumberToken_Parses()
    {
        using var doc = JsonDocument.Parse("42");
        Assert.True(RepeatableNumericValidation.TryGetNumber(doc.RootElement, out var v));
        Assert.Equal(42m, v);
    }

    [Fact]
    public void TryGetNumber_JsonStringNumber_Parses()
    {
        using var doc = JsonDocument.Parse("\"12.5\"");
        Assert.True(RepeatableNumericValidation.TryGetNumber(doc.RootElement, out var v));
        Assert.Equal(12.5m, v);
    }

    [Fact]
    public void TryGetNumber_ArabicDigitsString_Parses()
    {
        using var doc = JsonDocument.Parse("\"١٢٣\"");
        Assert.True(RepeatableNumericValidation.TryGetNumber(doc.RootElement, out var v));
        Assert.Equal(123m, v);
    }

    [Fact]
    public void TryGetNumber_NegativeString_Parses()
    {
        using var doc = JsonDocument.Parse("\"-1\"");
        Assert.True(RepeatableNumericValidation.TryGetNumber(doc.RootElement, out var v));
        Assert.Equal(-1m, v);
    }

    [Theory]
    [InlineData("\"abc\"")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("true")]
    [InlineData("null")]
    public void TryGetNumber_Malformed_ReturnsFalse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        Assert.False(RepeatableNumericValidation.TryGetNumber(doc.RootElement, out _));
    }

    // ── ValidateParsed: القيم الصالحة ⇒ null ───────────────────────────────
    [Fact]
    public void ValidateParsed_NoConstraints_ValidEvenNegative()
        => Assert.Null(RepeatableNumericValidation.ValidateParsed(-1m, null, null, false, null));

    [Fact]
    public void ValidateParsed_ZeroWithMinZero_Valid()
        => Assert.Null(RepeatableNumericValidation.ValidateParsed(0m, 0m, null, true, null));

    [Fact]
    public void ValidateParsed_PositiveWithinRange_Valid()
        => Assert.Null(RepeatableNumericValidation.ValidateParsed(50m, 0m, 100m, true, null));

    [Fact]
    public void ValidateParsed_DecimalWhenIntegerOnlyFalse_Valid()
        => Assert.Null(RepeatableNumericValidation.ValidateParsed(12.5m, 0m, null, false, null));

    [Fact]
    public void ValidateParsed_VeryLargeWithinNoMax_Valid()
        => Assert.Null(RepeatableNumericValidation.ValidateParsed(999999999m, 0m, null, true, null));

    // ── ValidateParsed: القيم غير الصالحة ⇒ كود الخطأ المناسب ────────────────
    [Fact]
    public void ValidateParsed_NegativeBelowMinZero_BelowMin()
        => Assert.Equal(RepeatableNumericValidation.BelowMin,
            RepeatableNumericValidation.ValidateParsed(-1m, 0m, null, true, null));

    [Fact]
    public void ValidateParsed_AboveMax_AboveMax()
        => Assert.Equal(RepeatableNumericValidation.AboveMax,
            RepeatableNumericValidation.ValidateParsed(101m, 0m, 100m, false, null));

    [Fact]
    public void ValidateParsed_DecimalWhenIntegerOnly_IntegerRequired()
        => Assert.Equal(RepeatableNumericValidation.IntegerRequired,
            RepeatableNumericValidation.ValidateParsed(12.5m, 0m, null, true, null));

    [Fact]
    public void ValidateParsed_OffStep_StepInvalid()
        => Assert.Equal(RepeatableNumericValidation.StepInvalid,
            RepeatableNumericValidation.ValidateParsed(0.15m, 0m, null, false, 0.1m));

    [Fact]
    public void ValidateParsed_OnStep_Valid()
        => Assert.Null(RepeatableNumericValidation.ValidateParsed(0.2m, 0m, null, false, 0.1m));

    // ترتيب الفحص: integer قبل min ⇒ عشريّ سالب مع integerOnly+min=0 ⇒ IntegerRequired أولًا.
    [Fact]
    public void ValidateParsed_CheckOrder_IntegerBeforeMin()
        => Assert.Equal(RepeatableNumericValidation.IntegerRequired,
            RepeatableNumericValidation.ValidateParsed(-1.5m, 0m, null, true, null));
}
