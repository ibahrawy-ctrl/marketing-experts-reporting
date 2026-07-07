using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Reporting.Application.Common;

/// <summary>
/// أداة مركزية موحّدة لتطبيع الأرقام (RC-3 Task 2B).
/// المشكلة: بعض المستخدمين يُدخلون الأرقام بالخانات العربية-الهندية (١٢٣) أو الفارسية (۱۲۳)
/// بينما التجميع/التحقّق يقرأ الخانات اللاتينية (123) فقط ⇒ قيم تُهمَل أو تُحتسَب صفرًا.
///
/// مصدر الحقيقة الوحيد للتطبيع في كامل النظام (لا تكرار). طبقتان:
/// (1) تطبيع الخانات فقط (NormalizeDigits) — آمن لأي نصّ لأنه يحوّل رموز الأرقام فقط ولا يمسّ الحروف/العلامات
///     ⇒ يُستخدم عند الحفظ (لضمان عدم تخزين خانات عربية في القاعدة) وعند القراءة/التجميع (دفاع للبيانات القديمة).
/// (2) تنقية رقمية صارمة (SanitizeNumeric) — تُبقي الخانات + فاصلة عشرية واحدة + إشارة سالب اختيارية،
///     تُطبَّق فقط على الأعمدة الرقمية (لا على الأعمدة النصّية مثل ملاحظات/العميل).
/// </summary>
public static class NumericNormalizer
{
    /// <summary>
    /// يحوّل الخانات العربية-الهندية (U+0660–U+0669) والفارسية/العربية الموسّعة (U+06F0–U+06F9)
    /// إلى الخانات اللاتينية (0–9). لا يمسّ أيّ محرف آخر (حروف، علامات، فراغات) ⇒ آمن للنصوص الحرّة.
    /// </summary>
    public static string? NormalizeDigits(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        char[]? chars = null;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            char mapped;
            if (c >= '\u0660' && c <= '\u0669') mapped = (char)('0' + (c - '\u0660'));
            else if (c >= '\u06F0' && c <= '\u06F9') mapped = (char)('0' + (c - '\u06F0'));
            else continue;
            chars ??= s.ToCharArray();
            chars[i] = mapped;
        }
        return chars is null ? s : new string(chars);
    }

    /// <summary>
    /// تنقية رقمية صارمة لخلية عمود رقمي: تطبّع الخانات ثم تُبقي (0–9) وفاصلة عشرية واحدة
    /// وإشارة سالب في المقدّمة فقط. تُزيل الحروف والرموز ومعاملات الحساب. سلسلة فارغة تبقى فارغة.
    /// </summary>
    public static string SanitizeNumeric(string? s)
    {
        var normalized = NormalizeDigits(s);
        if (string.IsNullOrEmpty(normalized)) return string.Empty;
        var sb = new System.Text.StringBuilder(normalized.Length);
        var hasDot = false;
        for (var i = 0; i < normalized.Length; i++)
        {
            var c = normalized[i];
            if (c >= '0' && c <= '9') sb.Append(c);
            else if (c == '.' && !hasDot) { sb.Append('.'); hasDot = true; }
            else if (c == '-' && sb.Length == 0) sb.Append('-');
        }
        return sb.ToString();
    }

    /// <summary>
    /// يطبّع الخانات ثم يحاول التحويل إلى decimal بثقافة ثابتة (InvariantCulture).
    /// يُرجِع false للقيم الفارغة/غير الرقمية. مصدر الحقيقة لكل مواضع القراءة/التجميع.
    /// </summary>
    public static bool TryParseDecimal(string? s, out decimal value)
    {
        value = 0m;
        var normalized = NormalizeDigits(s);
        if (string.IsNullOrWhiteSpace(normalized)) return false;
        return decimal.TryParse(normalized.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static readonly JsonWriterOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// يطبّع الخانات العربية/الفارسية داخل قيمة JSON (شبكة جدول أو قسم متكرّر) على مستوى القيم المُفكَّكة لا النصّ الخام:
    /// يفكّ الـJSON ويطبّع الخانات في كلّ قيمة نصّية (والحروف تبقى سليمة) ثم يعيد التسلسل. هذا يعالج الحالتين معًا —
    /// النصّ الحرفي (١٢٣) والمُرمَّز يونيكود (\u0661\u0662\u0663) الذي تُنتِجه بعض العملاء (System.Text.Json/سكربت/استيراد) —
    /// لأنّ التطبيع على النصّ الخام يفشل مع الترميز اليونيكودي. عند فشل التحليل (ليس JSON صالحًا) يسقط للتطبيع النصّي المباشر.
    /// </summary>
    public static string? NormalizeJsonDigits(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        try
        {
            using var doc = JsonDocument.Parse(json);
            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, JsonOptions))
                WriteNormalized(doc.RootElement, writer);
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return NormalizeDigits(json);
        }
    }

    private static void WriteNormalized(JsonElement el, Utf8JsonWriter w)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject();
                foreach (var p in el.EnumerateObject())
                {
                    w.WritePropertyName(p.Name);
                    WriteNormalized(p.Value, w);
                }
                w.WriteEndObject();
                break;
            case JsonValueKind.Array:
                w.WriteStartArray();
                foreach (var item in el.EnumerateArray()) WriteNormalized(item, w);
                w.WriteEndArray();
                break;
            case JsonValueKind.String:
                w.WriteStringValue(NormalizeDigits(el.GetString()));
                break;
            case JsonValueKind.Number:
                w.WriteRawValue(el.GetRawText());
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                w.WriteBooleanValue(el.GetBoolean());
                break;
            default:
                w.WriteNullValue();
                break;
        }
    }
}
