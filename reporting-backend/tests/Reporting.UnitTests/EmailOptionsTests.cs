using Reporting.Application.Notifications;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// تتحقّق من مواءمة EmailOptions مع مفاتيح بيئة الإنتاج (SmtpHost/SmtpPort/FromEmail)
/// واشتقاق اسم المستخدم تلقائيًا من عنوان المُرسِل لـ Google Workspace.
/// </summary>
public class EmailOptionsTests
{
    [Fact]
    public void Effective_Prefers_ExplicitCanonicalKeys()
    {
        var o = new EmailOptions
        {
            Host = "smtp.canonical", SmtpHost = "smtp.alt",
            Port = 25, SmtpPort = 2525,
            FromAddress = "canon@x.com", FromEmail = "alt@x.com",
            Username = "user@x.com"
        };

        Assert.Equal("smtp.canonical", o.EffectiveHost);
        Assert.Equal(2525, o.EffectivePort);
        Assert.Equal("canon@x.com", o.EffectiveFromAddress);
        Assert.Equal("user@x.com", o.EffectiveUsername);
    }

    [Fact]
    public void Effective_FallsBack_ToProductionEnvKeys()
    {
        // يحاكي بيئة الإنتاج: SmtpHost/SmtpPort/FromEmail فقط، بلا Host/Port/Username.
        var o = new EmailOptions
        {
            SmtpHost = "smtp.gmail.com",
            SmtpPort = 587,
            FromEmail = "info@marketingexperts.com.sa",
            Provider = "GoogleWorkspace"
        };

        Assert.Equal("smtp.gmail.com", o.EffectiveHost);
        Assert.Equal(587, o.EffectivePort);
        Assert.Equal("info@marketingexperts.com.sa", o.EffectiveFromAddress);
        // اسم المستخدم يقع على عنوان المُرسِل عند غياب Username.
        Assert.Equal("info@marketingexperts.com.sa", o.EffectiveUsername);
    }

    [Fact]
    public void EffectivePort_DefaultsTo587_WhenNeitherSet()
    {
        var o = new EmailOptions { SmtpHost = "smtp.gmail.com", FromEmail = "a@b.com" };
        Assert.Equal(587, o.EffectivePort);
    }
}
