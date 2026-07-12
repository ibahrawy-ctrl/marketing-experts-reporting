namespace Reporting.Application.Clients;

/// <summary>
/// حُرّاس تحقّق مشتركة لحقول ملف العميل (Client 360 — CPW-R1B):
/// (1) التحقّق من صحّة الروابط (http/https فقط، well-formed).
/// (2) منع تخزين أيّ أسرار/كلمات مرور/رموز وصول في الحقول النصّية الحرّة.
/// </summary>
public static class ClientFieldGuards
{
    /// <summary>مصطلحات محظورة تدلّ على سرّ محتمَل — تُرفَض في الحقول الحرّة.</summary>
    private static readonly string[] SecretTerms =
    {
        "password", "passwd", "pwd", "كلمة المرور", "كلمة السر",
        "secret", "apikey", "api key", "api_key", "access token", "accesstoken",
        "access_token", "bearer ", "refresh token", "refresh_token",
        "client secret", "client_secret", "recovery code", "recoverycode",
        "recovery_code", "cookie", "otp code", "2fa code"
    };

    /// <summary>
    /// رابط صالح: فارغ (مقبول) أو http/https well-formed. يُرجِع false للروابط المشوَّهة أو غير http(s).
    /// </summary>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        var value = url.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>يحوي النصّ مصطلحًا يدلّ على سرّ محتمَل؟ (فحص غير حسّاس لحالة الأحرف).</summary>
    public static bool ContainsSecret(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        foreach (var term in SecretTerms)
        {
            if (lower.Contains(term)) return true;
        }
        return false;
    }

    /// <summary>يحوي أيٌّ من النصوص سرًّا محتمَلًا؟</summary>
    public static bool AnyContainsSecret(params string?[] texts)
    {
        foreach (var t in texts)
        {
            if (ContainsSecret(t)) return true;
        }
        return false;
    }
}
