using Microsoft.AspNetCore.Hosting;

namespace Reporting.IntegrationTests;

/// <summary>
/// PROJECT-REPEATABLE-NUMERIC-VALIDATION-R1 — مصنع اختبارات معزول لتحقّق الحقول الرقميّة داخل
/// قسم المشاريع المتكرّر. قاعدة PostgreSQL منفصلة مؤقّتة (reporting_pfe_num_iso) لا تمسّ قاعدة
/// الاختبارات المشتركة reporting_test إطلاقًا (شرط العزل في التذكرة). يرث كامل الإعداد من المصنع
/// القياسيّ ثم يستبدل سلسلة الاتصال فقط (آخر UseSetting يفوز). الهجرة والبذور تعمل عند الإقلاع.
///
/// <para><b>TEST-ISO-01</b>: كان هذا المصنع يقرأ <c>TEST_DB_CONNECTION_PFE</c> ويؤول إلى
/// <c>reporting_pfe_iso</c> — وهما بعينهما متغيّر <see cref="ProjectFirstIsolatedFactory"/> وقاعدته.
/// فمصنعان مستقلّان (مجموعتان تعملان بالتوازي) كانا يتقاسمان قاعدةً واحدة رغم أنّ كليهما يوثّق
/// أنّه «معزول». على قاعدة مهاجَرة سلفًا يمرّ الأمر صامتًا؛ وعلى قاعدة نظيفة تتصادم
/// <c>MigrateAsync</c> المتزامنة فتسقط عشرات الاختبارات بـ<c>23505 pg_type_typname_nsp_index</c>
/// و<c>42701 column … already exists</c> — أي أنّ قياس الحزمة على قاعدة نظيفة كان متعذّرًا أصلًا.
/// المفتاح والقاعدة صارا خاصَّين بهذه التذكرة، والقاعدة تُنشَأ تلقائيًّا عند أوّل هجرة.</para>
/// </summary>
public class PfeNumericIsolatedFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // قابلة للتجاوز عبر TEST_DB_CONNECTION_PFE_NUM؛ الافتراضي reporting_pfe_num_iso.
        builder.UseSetting("ConnectionStrings:Default",
            System.Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_PFE_NUM")
                ?? "Host=localhost;Database=reporting_pfe_num_iso;Username=ibrahimelbahrawi");
    }
}

[CollectionDefinition("PfeNumericIsolated")]
public class PfeNumericIsolatedCollection : ICollectionFixture<PfeNumericIsolatedFactory> { }
