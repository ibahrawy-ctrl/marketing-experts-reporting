namespace Reporting.Application.Notifications;

/// <summary>إعدادات قناة البريد — تُقرأ من قسم "Email" (يدعم متغيرات البيئة Email__*).</summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>تفعيل إرسال البريد للأحداث. معطّل افتراضيًا (جاهزية) ويُفعَّل عبر الإعدادات.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    /// <summary>اسم بديل لمضيف SMTP (يطابق مفتاح بيئة الإنتاج Email__SmtpHost).</summary>
    public string SmtpHost { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    /// <summary>منفذ بديل (يطابق Email__SmtpPort). إن ضُبط فهو الأولى.</summary>
    public int? SmtpPort { get; set; }

    /// <summary>STARTTLS على 587 (الأنسب لـ Google Workspace). اضبطه false لاستخدام SSL مباشر على 465.</summary>
    public bool UseStartTls { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    /// <summary>كلمة مرور التطبيق (App Password). لا تُطبع أبدًا في السجلّات.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>عنوان المُرسِل. يقع على Username إن تُرك فارغًا.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>عنوان بديل للمُرسِل (يطابق Email__FromEmail).</summary>
    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "نظام تقارير خبراء التسويق";

    /// <summary>مزوّد البريد (إعلامي فقط، مثل GoogleWorkspace) — لا يؤثّر في السلوك.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// قائمة سماح اختيارية: إن لم تكن فارغة فلن يُرسَل بريد إلا لأنواع الإشعارات المذكورة فيها
    /// (تظل بقية الأنواع إشعارات داخل التطبيق فقط). تُستخدم للتفعيل المحدود أثناء التجربة (Pilot).
    /// </summary>
    public string[] IncludedTypes { get; set; } = System.Array.Empty<string>();

    /// <summary>أنواع الإشعارات المستثناة من البريد (تظل إشعارات داخل التطبيق فقط). تُطبَّق بعد IncludedTypes.</summary>
    public string[] ExcludedTypes { get; set; } = System.Array.Empty<string>();

    /// <summary>أقصى عدد محاولات إرسال قبل اعتبار الرسالة فاشلة نهائيًا.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>فترة فحص صندوق الصادر بالثواني.</summary>
    public int PollSeconds { get; set; } = 15;

    /// <summary>أساس التراجع الأُسّي بالثواني بين المحاولات (يُضرب في رقم المحاولة).</summary>
    public int BackoffBaseSeconds { get; set; } = 60;

    /// <summary>عدد الرسائل المعالَجة في كل دورة فحص.</summary>
    public int BatchSize { get; set; } = 20;

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    /// <summary>مضيف SMTP الفعّال — يفضّل Host ثم SmtpHost (مفتاح بيئة الإنتاج).</summary>
    public string EffectiveHost => FirstNonEmpty(Host, SmtpHost);

    /// <summary>المنفذ الفعّال — SmtpPort إن ضُبط، وإلا Port (افتراضي 587).</summary>
    public int EffectivePort => SmtpPort ?? Port;

    /// <summary>عنوان المُرسِل الفعّال — FromAddress ثم FromEmail ثم Username.</summary>
    public string EffectiveFromAddress => FirstNonEmpty(FromAddress, FromEmail, Username);

    /// <summary>اسم المستخدم الفعّال للمصادقة — Username ثم FromEmail ثم FromAddress (Gmail يستخدم عنوان المُرسِل).</summary>
    public string EffectiveUsername => FirstNonEmpty(Username, FromEmail, FromAddress);
}

/// <summary>نتيجة محاولة إرسال بريد واحدة — بلا أي أسرار.</summary>
public record EmailSendResult(bool Success, string? Error = null)
{
    public static EmailSendResult Ok() => new(true);
    public static EmailSendResult Fail(string error) => new(false, error);
}

/// <summary>مُرسِل البريد عبر SMTP — تجريد في طبقة التطبيق، يُنفَّذ بـ MailKit في البنية التحتية.</summary>
public interface IEmailSender
{
    /// <summary>هل القناة مُهيّأة بحد أدنى (المضيف والمُرسِل)؟ مستقلّة عن Enabled.</summary>
    bool IsConfigured { get; }

    Task<EmailSendResult> SendAsync(string toEmail, string? toName, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>قالب بريد عربي RTL بسيط ومتوافق مع عملاء البريد (مشترك بين الإشعارات واختبار البريد).</summary>
public static class EmailHtml
{
    /// <summary>
    /// R22B/MULTILINE-EMAIL: يحفظ أسطر النصّ في بريد HTML **بعد** الترميز الآمن حصرًا.
    /// الترتيب مُلزِم: (1) ترميز HTML كامل ⟹ أيّ وسم في مُدخَل المستخدم يصير نصًّا حرفيًّا،
    /// (2) توحيد نهايات الأسطر CRLF/CR إلى LF، (3) استبدال LF بـ<br /> — وهو الوسم الوحيد
    /// الذي نحقنه نحن ولا يأتي من المستخدم. عملاء البريد لا يعتمدون على white-space:pre-line
    /// اعتمادًا موثوقًا، لذلك <br /> بدل CSS. **ممنوع** عكس الترتيب أو تمرير HTML خام.
    /// </summary>
    internal static string EncodeWithLineBreaks(string value)
        => System.Net.WebUtility.HtmlEncode(value)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Replace("\n", "<br />");

    public static string Build(string title, string? body, string? link)
    {
        var safeTitle = System.Net.WebUtility.HtmlEncode(title);
        var safeBody = string.IsNullOrWhiteSpace(body) ? "" :
            $"<p style=\"margin:0 0 16px;font-size:15px;line-height:1.8;color:#333\">{EncodeWithLineBreaks(body)}</p>";
        var safeLink = string.IsNullOrWhiteSpace(link) ? "" :
            $"<p style=\"margin:0\"><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\" style=\"display:inline-block;background:#E8772E;color:#fff;text-decoration:none;padding:10px 22px;border-radius:6px;font-size:14px\">فتح في النظام</a></p>";

        return $@"<!DOCTYPE html>
<html dir=""rtl"" lang=""ar""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;background:#f4f6f9;font-family:Tahoma,Arial,sans-serif"">
  <div style=""max-width:560px;margin:24px auto;background:#fff;border-radius:10px;overflow:hidden;border:1px solid #e6e9ef"">
    <div style=""background:#1F2A44;padding:18px 24px""><span style=""color:#fff;font-size:16px;font-weight:bold"">خبراء التسويق — نظام تقارير الأداء</span></div>
    <div style=""padding:24px"">
      <h1 style=""margin:0 0 14px;font-size:19px;color:#1F2A44"">{safeTitle}</h1>
      {safeBody}
      {safeLink}
    </div>
    <div style=""padding:14px 24px;background:#fafbfc;border-top:1px solid #eef1f5;color:#8a94a6;font-size:12px"">هذه رسالة آلية من نظام التقارير الداخلي. الرجاء عدم الرد عليها.</div>
  </div>
</body></html>";
    }
}
