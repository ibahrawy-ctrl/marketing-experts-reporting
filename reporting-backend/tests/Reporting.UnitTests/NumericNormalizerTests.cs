using System.Text.Json;
using Reporting.Application.Common;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// مواصفة الأداة المركزية لتطبيع الأرقام (RC-3 Task 2B): تحويل الخانات العربية-الهندية/الفارسية
/// إلى لاتينية، تنقية الأعمدة الرقمية، والتحويل الآمن — بصرف النظر عن لغة لوحة المفاتيح.
/// </summary>
public class NumericNormalizerTests
{
    [Theory]
    [InlineData("١٠", "10")]
    [InlineData("٣٠٠٠٠", "30000")]
    [InlineData("٠", "0")]
    [InlineData("٩٩٩", "999")]
    [InlineData("۱۲۳", "123")]      // فارسية U+06Fx
    [InlineData("١٢٣.٥٠", "123.50")]
    [InlineData("1234567890", "1234567890")] // لاتينية تبقى كما هي
    public void NormalizeDigits_MapsArabicAndPersianToAscii(string input, string expected)
        => Assert.Equal(expected, NumericNormalizer.NormalizeDigits(input));

    [Fact]
    public void NormalizeDigits_KeepsLettersAndSymbols_OnlyMapsDigits()
    {
        // نصّ حرّ فيه خانات عربية: الحروف/العلامات تبقى، الأرقام فقط تتحوّل.
        Assert.Equal("ملاحظة 12 و 30%", NumericNormalizer.NormalizeDigits("ملاحظة ١٢ و ٣٠%"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NormalizeDigits_NullOrEmpty_ReturnsInput(string? input)
        => Assert.Equal(input, NumericNormalizer.NormalizeDigits(input));

    [Theory]
    [InlineData("١٢٣abc", "123")]
    [InlineData("أبجد", "")]
    [InlineData("12+3", "123")]
    [InlineData("10/2", "102")]
    [InlineData("5*4", "54")]
    [InlineData("30%", "30")]
    [InlineData("(٥)", "5")]
    [InlineData("-١٢", "-12")]
    [InlineData("١٢-٣", "123")]       // سالب في غير المقدّمة يُزال (تبقى الخانات)
    [InlineData("١٢.٣.٤", "12.34")]   // فاصلة عشرية واحدة فقط
    [InlineData("١٢٣.٥", "123.5")]
    public void SanitizeNumeric_StripsNonNumeric(string input, string expected)
        => Assert.Equal(expected, NumericNormalizer.SanitizeNumeric(input));

    [Theory]
    [InlineData("١٠", 10)]
    [InlineData("٣٠٠٠٠", 30000)]
    [InlineData("٠", 0)]
    [InlineData("٩٩٩", 999)]
    [InlineData("۱۲۳", 123)]
    [InlineData("١٢٣.٥", 123.5)]
    [InlineData(" ٤٢ ", 42)]
    public void TryParseDecimal_ParsesArabicDigits(string input, double expected)
    {
        Assert.True(NumericNormalizer.TryParseDecimal(input, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("أبجد")]
    public void TryParseDecimal_NonNumeric_ReturnsFalse(string? input)
        => Assert.False(NumericNormalizer.TryParseDecimal(input, out _));

    // خانات عربية حرفية داخل JSON ⇒ تُطبَّع في كل قيمة نصّية، والحروف تبقى، والبنية تُصان.
    [Fact]
    public void NormalizeJsonDigits_LiteralArabic_NormalizesCellsKeepsText()
    {
        var json = "[[\"خدمة أ\",\"٤٠\",\"١٠٠\",\"٨٠٠٠\"]]";
        var rows = JsonSerializer.Deserialize<string[][]>(NumericNormalizer.NormalizeJsonDigits(json)!)!;
        Assert.Equal(new[] { "خدمة أ", "40", "100", "8000" }, rows[0]);
    }

    // خانات عربية مُرمَّزة يونيكود (\u0664\u0660) — كما تُنتِجها System.Text.Json/سكربت — تُطبَّع أيضًا.
    [Fact]
    public void NormalizeJsonDigits_UnicodeEscapedArabic_Normalizes()
    {
        var json = "[[\"\\u062E\\u062F\\u0645\\u0629\",\"\\u0664\\u0660\",\"\\u0668\\u0660\\u0660\\u0660\"]]";
        var rows = JsonSerializer.Deserialize<string[][]>(NumericNormalizer.NormalizeJsonDigits(json)!)!;
        Assert.Equal(new[] { "خدمة", "40", "8000" }, rows[0]);
    }

    // خانات فارسية (۹۹۹) داخل JSON ⇒ تُطبَّع.
    [Fact]
    public void NormalizeJsonDigits_PersianDigits_Normalizes()
    {
        var json = "[[\"a\",\"\u06F9\u06F9\u06F9\"]]";
        var rows = JsonSerializer.Deserialize<string[][]>(NumericNormalizer.NormalizeJsonDigits(json)!)!;
        Assert.Equal(new[] { "a", "999" }, rows[0]);
    }

    // قسم متكرّر (كائن مع مصفوفة إجابات) ⇒ الخانات داخل القيم النصّية تُطبَّع والمفاتيح تبقى.
    [Fact]
    public void NormalizeJsonDigits_ObjectStructure_NormalizesStringValues()
    {
        var json = "[{\"projectId\":\"p1\",\"answers\":[\"١٢٣\",\"ملاحظة ٥\"]}]";
        var normalized = NumericNormalizer.NormalizeJsonDigits(json)!;
        Assert.Contains("\"123\"", normalized);
        Assert.Contains("ملاحظة 5", normalized);
        Assert.Contains("projectId", normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeJsonDigits_NullOrEmpty_ReturnsInput(string? input)
        => Assert.Equal(input, NumericNormalizer.NormalizeJsonDigits(input));

    // نصّ ليس JSON صالحًا ⇒ يسقط للتطبيع النصّي المباشر (لا يرمي).
    [Fact]
    public void NormalizeJsonDigits_InvalidJson_FallsBackToDigitNormalization()
        => Assert.Equal("abc 12", NumericNormalizer.NormalizeJsonDigits("abc ١٢"));
}
