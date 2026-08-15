using System.Text.RegularExpressions;

namespace Reporting.Application.Documents;

/// <summary>نتيجة التحقّق من رابط خارجيّ.</summary>
public record ExternalLinkValidation(bool IsValid, string? ErrorCode, string? Error, string NormalizedUrl)
{
    public static ExternalLinkValidation Reject(string code, string message)
        => new(false, code, message, string.Empty);

    public static ExternalLinkValidation Accept(string url) => new(true, null, null, url);
}

/// <summary>
/// سياسة الروابط الخارجيّة (CPW-R1B2 — «الروابط المهمّة»).
/// الرابط مرجع عنوان فقط: يُرفَض إن حمل ما يشبه سرًّا (رمز/مفتاح/كلمة مرور/جلسة)،
/// أو إن حمل اعتمادات مضمَّنة (<c>user:pass@host</c>)، أو إن لم يكن http/https مطلقًا.
/// </summary>
public static class ExternalLinkPolicy
{
    public const int MaxUrlLength = 1000;

    /// <summary>أسماء بارامترات محظورة لأنّها تحمل أسرارًا عادةً.</summary>
    private static readonly string[] ForbiddenParameterNames =
    {
        "password", "passwd", "pwd", "token", "access_token", "refresh_token", "id_token",
        "apikey", "api_key", "secret", "client_secret", "session", "sessionid", "sid",
        "auth", "authorization", "credential", "credentials", "recovery", "recovery_code",
        "otp", "cookie", "signature", "sig", "key"
    };

    /// <summary>مقاطع مسار محظورة (مسارات إعادة تعيين/دعوة تحمل رموزًا).</summary>
    private static readonly string[] ForbiddenPathFragments =
    {
        "reset-password", "resetpassword", "recovery-code", "oauth/token", "/token/", "access_token"
    };

    /// <summary>
    /// يتحقّق من الرابط ويُعيده مُطبَّعًا. الرفض يُرجِع رمز خطأ ورسالة عربيّة.
    /// </summary>
    public static ExternalLinkValidation Validate(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return ExternalLinkValidation.Reject("external_link.url_required", "الرابط مطلوب.");

        var trimmed = url.Trim();
        if (trimmed.Length > MaxUrlLength)
            return ExternalLinkValidation.Reject("external_link.url_too_long",
                $"الرابط يتجاوز {MaxUrlLength} محرفًا.");

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return ExternalLinkValidation.Reject("external_link.url_invalid", "صيغة الرابط غير صحيحة.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return ExternalLinkValidation.Reject("external_link.scheme_not_allowed",
                "يُسمح بروابط http أو https فقط.");

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return ExternalLinkValidation.Reject("external_link.embedded_credentials",
                "لا يُسمح بحفظ رابط يحتوي على اسم مستخدم أو كلمة مرور مضمَّنة.");

        if (ContainsSecret(uri))
            return ExternalLinkValidation.Reject("external_link.secret_detected",
                "لا يُسمح بحفظ رابط يحتوي على رمز وصول أو مفتاح أو كلمة مرور. احفظ الرابط الأساسيّ فقط.");

        return ExternalLinkValidation.Accept(trimmed);
    }

    /// <summary>هل يحمل الرابط ما يشبه سرًّا في بارامتراته أو مساره أو جزئه اللاحق.</summary>
    public static bool ContainsSecret(Uri uri)
    {
        var query = uri.Query.TrimStart('?');
        var fragment = uri.Fragment.TrimStart('#');

        foreach (var part in new[] { query, fragment })
        {
            if (string.IsNullOrEmpty(part)) continue;
            foreach (var pair in part.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var name = pair.Split('=')[0].Trim().ToLowerInvariant();
                // مطابقة اسم البارامتر بدقّة بعد تجاهل الفواصل الشائعة.
                var normalized = Regex.Replace(name, "[^a-z_]", string.Empty);
                if (ForbiddenParameterNames.Contains(normalized)) return true;
            }
        }

        var path = uri.AbsolutePath.ToLowerInvariant();
        return ForbiddenPathFragments.Any(f => path.Contains(f, StringComparison.Ordinal));
    }
}
