using Reporting.Application.Notifications;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// R22B/MULTILINE-EMAIL — عقد بريد الإشعارات: يحفظ أسطر التعليق **ولا يفتح ثغرة حقن**.
///
/// <para>
/// خلفيّة العيب: قبل هذا الإصلاح كان الجسم يُرمَّز بـ<c>HtmlEncode</c> ثمّ يُدرَج داخل
/// <c>&lt;p&gt;</c> بلا أيّ معالجة للأسطر، وHTML يطوي <c>\n</c> إلى مسافة واحدة ⟹ تعليق
/// اعتماد من ثلاثة أسطر يصل إلى الموظّف سطرًا واحدًا ملتصقًا. ولا يوجد <c>TextBody</c>
/// بديل (<c>NotificationService</c> يضبط <c>HtmlBody</c> وحده)، فالفقد كامل لا جزئيّ.
/// </para>
///
/// <para>
/// **الترتيب هو العقد الأمنيّ:** الترميز أوّلًا ثمّ الاستبدال. لو عُكس (استبدال <c>\n</c>
/// بـ<c>&lt;br /&gt;</c> قبل الترميز) لصار الوسم نفسه مُرمَّزًا ونصًّا مرئيًّا؛ ولو مُرِّر
/// HTML خام لانفتح سطح XSS. لذلك كلّ اختبار هنا يتحقّق من **الأمرين معًا**:
/// الأسطر محفوظة، والوسوم القادمة من المستخدم مُرمَّزة حرفيًّا.
/// </para>
/// </summary>
public class EmailHtmlMultilineTests
{
    private const string Title = "أُعيد تقريرك للتعديل";
    private const string Link = "/app/submissions?open=1";

    [Fact]
    public void Body_With_Three_Lines_Renders_Two_LineBreaks()
    {
        // ثلاثة أسطر مميّزة (لا نصّ مكرَّر) ⟹ فاصلان اثنان بالضبط، لا أقلّ ولا أكثر.
        var body = "السطر الأوّل: نقص في الأدلّة\nالسطر الثاني: عدّل عدد المخرجات\nالسطر الثالث: أعد الإرسال اليوم";

        var html = EmailHtml.Build(Title, body, Link);

        Assert.Contains("السطر الأوّل: نقص في الأدلّة<br />السطر الثاني", html);
        Assert.Contains("السطر الثاني: عدّل عدد المخرجات<br />السطر الثالث", html);
        Assert.Equal(2, CountOccurrences(html, "<br />"));
    }

    [Fact]
    public void Crlf_And_Cr_Are_Normalized_To_Single_Break_Each()
    {
        // عملاء ويندوز يرسلون CRLF، وبعض المحرّرات القديمة CR وحده. الثلاثة تُعامَل سواء،
        // وإلّا ظهر CRLF فاصلين مزدوجين ⟹ فراغ مضاعف في البريد.
        var html = EmailHtml.Build(Title, "أ\r\nب\rج\nد", Link);

        Assert.Contains("أ<br />ب<br />ج<br />د", html);
        Assert.Equal(3, CountOccurrences(html, "<br />"));
        Assert.DoesNotContain("\r", html.Substring(html.IndexOf("أ<br />", System.StringComparison.Ordinal), 30));
    }

    [Fact]
    public void Script_Tag_In_Body_Is_Encoded_Not_Executable()
    {
        // الحالة الحرجة: مُدخَل يحاول الحقن **مع** أسطر متعدّدة في آنٍ واحد.
        var body = "قبل\n<script>alert('xss')</script>\nبعد";

        var html = EmailHtml.Build(Title, body, Link);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&lt;/script&gt;", html);
        // والأسطر محفوظة رغم الترميز — العقدان لا يتنازعان.
        Assert.Contains("قبل<br />&lt;script&gt;", html);
    }

    [Fact]
    public void Ampersand_And_Quotes_Are_Encoded()
    {
        var html = EmailHtml.Build(Title, "شركة أ & ب\nقال: \"تمّ\" و'انتهى'", Link);

        Assert.Contains("&amp;", html);
        Assert.Contains("&quot;", html);
        Assert.Contains("<br />", html);
        // لا يوجد & خام غير مُرمَّز داخل جسم الرسالة.
        Assert.DoesNotContain("أ & ب", html);
    }

    [Fact]
    public void Title_Is_Encoded_And_Never_Line_Broken()
    {
        // العنوان يُرمَّز فقط بلا فواصل أسطر: هو سطر واحد في <h1> بحكم التصميم.
        var html = EmailHtml.Build("عنوان <b>خطر</b>", "جسم عاديّ", Link);

        Assert.Contains("&lt;b&gt;خطر&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>خطر</b>", html);
        Assert.Equal(0, CountOccurrences(html, "<br />"));
    }

    [Fact]
    public void Single_Line_Body_Emits_No_Break()
    {
        var html = EmailHtml.Build(Title, "سطر واحد فقط", Link);

        Assert.Contains("سطر واحد فقط", html);
        Assert.Equal(0, CountOccurrences(html, "<br />"));
    }

    [Fact]
    public void Empty_Body_Emits_No_Paragraph()
    {
        var html = EmailHtml.Build(Title, null, Link);

        Assert.DoesNotContain("<br />", html);
        Assert.Contains(Title, html);
    }

    [Fact]
    public void Long_Body_Is_Preserved_Whole_With_All_Breaks()
    {
        // نصّ طويل بلا اقتطاع: عشرون سطرًا كلٌّ منها 300 محرف. الغرض إثبات أنّ المعالجة
        // خطّيّة على كامل الجسم ولا تتوقّف عند حدّ ضمنيّ، وأنّ الطول لا يُسقط فاصلًا.
        var lines = new string[20];
        for (var i = 0; i < lines.Length; i++)
            lines[i] = $"س{i:D2}:" + new string('ن', 300);
        var body = string.Join("\n", lines);

        var html = EmailHtml.Build(Title, body, Link);

        Assert.Equal(19, CountOccurrences(html, "<br />"));
        Assert.Contains(lines[0], html);
        Assert.Contains(lines[19], html);
        Assert.Contains($"{lines[0]}<br />{lines[1]}", html);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var i = 0;
        while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }
        return count;
    }
}
