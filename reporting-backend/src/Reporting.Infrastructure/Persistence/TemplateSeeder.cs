using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;

namespace Reporting.Infrastructure.Persistence;

/// <summary>تهيئة قوالب التقارير (≈24) وقوالب مؤشرات الأداء — إصدارات منشورة جاهزة. إدراج مرة واحدة (idempotent).</summary>
public static class TemplateSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
        var ownerId = admins.FirstOrDefault()?.Id;
        if (ownerId is null) return;

        await SeedReportTemplatesAsync(db, ownerId.Value);
        await SeedKpiTemplatesAsync(db, ownerId.Value);
    }

    private static async Task SeedReportTemplatesAsync(AppDbContext db, Guid ownerId)
    {
        var existingTitles = await db.ReportTemplates.Select(t => t.Title).ToListAsync();
        var existing = existingTitles.ToHashSet();

        foreach (var def in ReportDefs)
        {
            if (existing.Contains(def.Title)) continue;
            var template = new ReportTemplate
            {
                Title = def.Title,
                Description = def.Description,
                DefaultPeriodType = def.Period,
                Status = TemplateStatus.Published,
                OwnerId = ownerId,
                IsActive = true
            };
            var version = new ReportTemplateVersion
            {
                VersionNumber = 1,
                IsPublished = true,
                PublishedAtUtc = DateTime.UtcNow,
                PublishedById = ownerId
            };
            var order = 0;
            foreach (var f in def.Fields)
            {
                version.Fields.Add(new TemplateField
                {
                    Label = f.Label,
                    FieldType = f.Type,
                    IsRequired = f.Required,
                    HelpText = f.Help,
                    ConfigJson = BuildConfigJson(f),
                    Order = order++
                });
            }
            template.Versions.Add(version);
            db.ReportTemplates.Add(template);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedKpiTemplatesAsync(AppDbContext db, Guid ownerId)
    {
        var existingTitles = await db.KpiTemplates.Select(t => t.Title).ToListAsync();
        var existing = existingTitles.ToHashSet();

        foreach (var def in KpiDefs)
        {
            if (existing.Contains(def.Title)) continue;
            var template = new KpiTemplate
            {
                Title = def.Title,
                Description = def.Description,
                Cadence = def.Cadence,
                Status = TemplateStatus.Published,
                OwnerId = ownerId,
                IsActive = true
            };
            var version = new KpiTemplateVersion
            {
                VersionNumber = 1,
                IsPublished = true,
                PublishedAtUtc = DateTime.UtcNow,
                PublishedById = ownerId
            };
            var order = 0;
            foreach (var m in def.Metrics)
            {
                version.Metrics.Add(new KpiMetric
                {
                    Name = m.Name,
                    Weight = m.Weight,
                    TargetValue = m.Target,
                    Unit = m.Unit,
                    CalcMethod = m.Calc,
                    Order = order++
                });
            }
            template.Versions.Add(version);
            db.KpiTemplates.Add(template);
        }

        await db.SaveChangesAsync();
    }

    private record FieldDef(
        string Label,
        FieldType Type,
        bool Required = false,
        string[]? Options = null,
        string[]? Columns = null,
        string? Help = null);

    private record ReportDef(string Title, string? Description, PeriodType Period, FieldDef[] Fields);
    private record MetricDef(string Name, decimal Weight, decimal? Target = null, string? Unit = null, KpiCalcMethod Calc = KpiCalcMethod.Manual);
    private record KpiDef(string Title, string? Description, KpiCadence Cadence, MetricDef[] Metrics);

    // يحوّل خيارات SingleSelect/MultiSelect أو أعمدة TableGrid إلى JSON يُخزَّن في ConfigJson.
    private static string? BuildConfigJson(FieldDef f)
    {
        if (f.Options is { Length: > 0 })
            return JsonSerializer.Serialize(new { options = f.Options });
        if (f.Columns is { Length: > 0 })
            return JsonSerializer.Serialize(new { columns = f.Columns });
        return null;
    }

    // ===== مختصرات بناء الحقول =====
    private static FieldDef Sec(string label) => new(label, FieldType.SectionHeader);
    private static FieldDef Pick(string label, params string[] options) => new(label, FieldType.SingleSelect, false, options);
    private static FieldDef Note(string label) => new(label, FieldType.LongText, false, Help: "اكتب هنا…");
    private static FieldDef Long(string label, bool req = false) => new(label, FieldType.LongText, req);
    private static FieldDef Num(string label, bool req = false) => new(label, FieldType.Number, req);
    private static FieldDef Cur(string label, bool req = false) => new(label, FieldType.Currency, req);
    private static FieldDef Pct(string label, bool req = false) => new(label, FieldType.Percentage, req);
    private static FieldDef YesNo(string label) => new(label, FieldType.Boolean);
    private static FieldDef Grid(string label, params string[] columns) => new(label, FieldType.TableGrid, false, null, columns);

    // مجموعات خيارات متكرّرة
    private static readonly string[] StatusGYR = { "🟢 ممتازة", "🟡 مستقرة", "🔴 تحتاج تدخل" };
    private static readonly string[] WorkflowStatus = { "🟢 منتظمة", "🟡 تحتاج متابعة", "🔴 مشكلة" };
    private static readonly string[] AchievementBand = { "أقل من 60%", "من 60% إلى 84%", "من 85% إلى 100%" };
    private static readonly string[] DecisionPriority = { "عاجل", "هذا الأسبوع", "الأسبوع القادم" };

    private static readonly ReportDef[] ReportDefs =
    {
        // 1) تقرير قائد فريق SEO
        new("تقرير قائد فريق SEO", "تقرير أسبوعي لقائد فريق تحسين محركات البحث", PeriodType.Weekly, new[]
        {
            Sec("🚦 الحالة العامة"),
            Pick("حالة العمل العامة", StatusGYR),
            YesNo("هل توجد مشكلة تحتاج تصعيد؟"),
            Note("ملاحظة مختصرة"),
            Sec("📊 لوحة المؤشرات"),
            Grid("لوحة مؤشرات الأداء", "المؤشر", "المخطط", "المنفذ", "المتأخر", "نسبة الإنجاز"),
            Sec("🗂️ حالة المشاريع"),
            Grid("حالة كل مشروع", "المشروع", "الحالة", "أهم ما تم", "أهم مشكلة", "الخطوة القادمة"),
            Grid("تقدم الكلمات المفتاحية", "الكلمة", "الترتيب السابق", "الترتيب الحالي", "الحالة"),
            Grid("جودة المقالات", "المقال", "الحالة", "ملاحظة"),
            Grid("المشاكل الفنية", "المشكلة", "التأثير", "الحالة"),
            Sec("🤝 المطلوب من الفرق"),
            Grid("المطلوب من الفرق الأخرى", "المطلوب", "المسؤول", "الفريق", "المشروع", "الأولوية"),
            Sec("👥 أداء الفريق"),
            Grid("أداء أعضاء الفريق", "الاسم", "الدور", "الحالة", "ملاحظة"),
            Note("المتأخرات"),
            Note("خطة الأسبوع القادم"),
            Note("قرارات مطلوبة"),
        }),

        // 2) التقرير المالي
        new("التقرير المالي", "تقرير مالي شهري تنفيذي", PeriodType.Monthly, new[]
        {
            Sec("🚦 الحالة العامة"),
            Pick("الكاش فلو", StatusGYR),
            Pick("التحصيلات", StatusGYR),
            Pick("المصروفات", StatusGYR),
            Pick("تسجيل الحسابات", StatusGYR),
            YesNo("هل توجد مشكلة تحتاج تصعيد؟"),
            Note("ملاحظة مختصرة"),
            Sec("💰 الملخص المالي التنفيذي"),
            Cur("إجمالي الإيرادات", true),
            Cur("إجمالي المصروفات", true),
            Cur("صافي التدفق النقدي"),
            Cur("الالتزامات المستحقة"),
            Sec("🧾 مراجعة أعمال يوسف (المحاسب)"),
            Pick("تسجيل الفواتير", WorkflowStatus),
            Pick("التسويات البنكية", WorkflowStatus),
            Pick("إقفال الحسابات", WorkflowStatus),
            Sec("📋 جداول المتابعة"),
            Grid("العملاء المتأخرون في السداد", "العميل", "المبلغ", "عدد أيام التأخير", "ملاحظة"),
            Grid("المصروفات غير المعتادة", "البند", "المبلغ", "السبب"),
            Grid("موقف الالتزامات", "الالتزام", "المبلغ", "تاريخ الاستحقاق", "الحالة"),
            Pick("ملاحظات Odoo", WorkflowStatus),
            Note("مخاطر مالية"),
            Note("قرارات مطلوبة"),
        }),

        // 3) تقرير المدير العام
        new("تقرير المدير العام", "الملخص التنفيذي الشهري للمدير العام", PeriodType.Monthly, new[]
        {
            Sec("🏢 حالة الشركة"),
            Pick("الأداء العام", StatusGYR),
            Pick("المبيعات", StatusGYR),
            Pick("التشغيل والتنفيذ", StatusGYR),
            Pick("الوضع المالي", StatusGYR),
            Pick("موقف الشهر", "بداية الشهر", "منتصف الشهر", "نهاية الشهر"),
            Sec("🎯 ملخص التارجت"),
            Grid("ملخص التارجت", "البند", "المستهدف", "المحقق", "نسبة الإنجاز"),
            Sec("🧭 قراءة المدير العام"),
            Long("قراءة الأداء العام"),
            Long("قراءة المبيعات"),
            Long("قراءة التشغيل"),
            Long("قراءة الوضع المالي"),
            Long("قراءة الفريق"),
            Sec("📌 الملخص التنفيذي"),
            Note("أبرز 4 نقاط تنفيذية"),
            Note("مقترحات النمو"),
            Note("Red Flags - إشارات حمراء"),
            Sec("📊 لوحات التفصيل"),
            Grid("حالة المبيعات", "القناة", "المستهدف", "المحقق", "الحالة"),
            Grid("أهم 3 مشاكل", "المشكلة", "الأثر", "المقترح"),
            Grid("أهم 3 فرص", "الفرصة", "العائد المتوقع", "المطلوب"),
            Pick("أولوية القرارات", DecisionPriority),
            Note("قرارات مطلوبة"),
        }),

        // 4) تقرير قائد فريق السوشيال ميديا
        new("تقرير التيم ليدر للسوشيال ميديا", "تقرير أسبوعي لقائد فريق السوشيال ميديا", PeriodType.Weekly, new[]
        {
            Sec("🚦 الحالة العامة"),
            Pick("حالة المشروع", StatusGYR),
            Pct("نسبة تحقيق المخرجات"),
            Pct("نسبة إشغال الفريق"),
            Sec("🔄 سير العمل بين الأقسام"),
            Pick("التنسيق مع المحتوى", WorkflowStatus),
            Pick("التنسيق مع التصميم", WorkflowStatus),
            Pick("التنسيق مع الأداء", WorkflowStatus),
            Sec("📈 جودة المخرجات"),
            Num("عدد المنشورات المنفّذة"),
            Num("عدد المنشورات المعتمدة من أول مرة"),
            Num("عدد المنشورات المعادة"),
            Sec("👥 إدارة وتقييم الفريق"),
            Grid("تقييم أعضاء الفريق", "الاسم", "الدور", "الحالة", "ملاحظة"),
            Grid("المتأخرات", "المهمة", "المسؤول", "سبب التأخير", "الموعد الجديد"),
            Sec("🗣️ ملاحظات العميل"),
            YesNo("هل توجد ملاحظات على المحتوى؟"),
            YesNo("هل توجد ملاحظات على التصميم؟"),
            YesNo("هل توجد ملاحظات على مواعيد النشر؟"),
            YesNo("هل توجد ملاحظات على التفاعل؟"),
            YesNo("هل يوجد طلب تصعيد من العميل؟"),
            Grid("خطة الأسبوع القادم", "المهمة", "المسؤول", "الموعد"),
            Note("قرارات مطلوبة"),
        }),

        // 5) تقرير الحسابات
        new("تقرير الحسابات", "تقرير الحسابات الأسبوعي", PeriodType.Weekly, new[]
        {
            Sec("🚦 الحالة العامة"),
            Pick("حالة التحصيلات", StatusGYR),
            Pick("حالة الفوترة", StatusGYR),
            YesNo("هل توجد مشكلة تحتاج تصعيد؟"),
            Note("ملاحظة مختصرة"),
            Sec("🔢 ملخص الأرقام الأساسية"),
            Cur("إجمالي التحصيلات"),
            Cur("إجمالي الفواتير الصادرة"),
            Cur("إجمالي المصروفات"),
            Cur("إجمالي المستحقات"),
            Sec("📋 جداول المتابعة"),
            Grid("التحصيلات", "العميل", "المبلغ", "التاريخ", "ملاحظة"),
            Grid("الفواتير الصادرة", "العميل", "المبلغ", "التاريخ", "الحالة"),
            Grid("المصروفات", "البند", "المبلغ", "التاريخ"),
            Grid("المستحقات", "العميل", "المبلغ", "أيام التأخير"),
            Pick("ملاحظات Odoo", WorkflowStatus),
            Grid("المطلوب من الإدارات", "المطلوب", "الإدارة", "الأولوية"),
            Note("قرارات مطلوبة"),
        }),

        // 6) تقرير المديرشن الأسبوعي
        new("تقرير المديرشن الأسبوعي", "تقرير أسبوعي لإدارة التعليقات والرسائل", PeriodType.Weekly, new[]
        {
            Sec("🔢 ملخص الأرقام"),
            Num("عدد الرسائل الواردة"),
            Num("عدد الرسائل المُجاب عليها"),
            Num("متوسط زمن الرد (دقيقة)"),
            Num("عدد التعليقات الإشكالية"),
            // Business-1D-4: حقول مُضافة تراكميًا فقط (لا تعديل/حذف لحقول قائمة) لدعم تجميع المودريشن.
            Num("عدد الحالات المصعّدة"),
            Num("عدد الشكاوى"),
            Num("عدد الفرص البيعية المحوَّلة"),
            Sec("📋 جداول المتابعة"),
            Grid("متابعة النشر", "المنصّة", "عدد المنشورات", "الحالة"),
            Grid("استلام المحتوى", "المصدر", "تم الاستلام؟", "ملاحظة"),
            Grid("التعليقات الإشكالية", "التعليق", "المنصّة", "الإجراء المتخذ"),
            Sec("📝 ملاحظات تحليلية"),
            Long("أفضل منشور"),
            Long("أكثر منشور سلبي"),
            Long("الأسئلة المتكررة (FAQ)"),
            Long("توصيات"),
            Grid("المتأخر", "المهمة", "السبب", "الموعد الجديد"),
            Grid("خطة النشر القادمة", "اليوم", "المنصّة", "نوع المحتوى"),
            Note("ملاحظات للإدارة"),
        }),

        // 7) تقرير فريق التصميم
        new("تقرير فريق التصميم", "تقرير أسبوعي لفريق التصميم", PeriodType.Weekly, new[]
        {
            Sec("🚦 الحالة العامة"),
            Pct("نسبة تحقيق المخرجات"),
            Pct("نسبة العمل على المشروع"),
            Sec("🔢 الأرقام"),
            Num("عدد الطلبات المستلمة"),
            Num("عدد التصاميم المسلَّمة"),
            Num("معتمدة من أول مرة"),
            Num("متأخرة"),
            Num("بانتظار المراجعة"),
            Num("أعيدت للتعديل"),
            Sec("📝 التحليل"),
            Long("أسباب التأخير"),
            Long("أفضل التصاميم"),
            Long("التحديات"),
            Grid("خطة الأسبوع القادم", "المهمة", "المسؤول", "الموعد"),
            Note("طلبات الإدارة"),
        }),

        // 8) تقرير فريق الفيديو
        new("تقرير فريق الفيديو", "تقرير أسبوعي لفريق الفيديو", PeriodType.Weekly, new[]
        {
            Sec("🚦 الحالة العامة"),
            Pct("نسبة تحقيق المخرجات"),
            Pct("نسبة العمل على المشروع"),
            Sec("🔢 الأرقام"),
            Num("عدد الطلبات المستلمة"),
            Num("عدد الفيديوهات المسلَّمة"),
            Num("معتمدة من أول مرة"),
            Num("متأخرة"),
            Num("بانتظار المراجعة"),
            Num("أعيدت للتعديل"),
            Sec("📝 التحليل"),
            Long("أسباب التأخير"),
            Long("أفضل الفيديوهات"),
            Long("التحديات"),
            Grid("خطة الأسبوع القادم", "المهمة", "المسؤول", "الموعد"),
            Note("طلبات الإدارة"),
        }),

        // 9) تقرير فريق مبيعات الأكاديمية
        new("تقرير فريق مبيعات الأكاديمية", "تقرير مبيعات الأكاديمية", PeriodType.Weekly, new[]
        {
            Sec("🔢 الأرقام اليومية"),
            Grid("الأرقام اليومية", "اليوم", "ليدز", "مكالمات", "تسجيلات", "قيمة المبيعات"),
            Sec("📝 التحليل"),
            Long("أبرز الإنجازات"),
            Long("التحديات"),
            Long("أسباب عدم الشراء"),
            Grid("الاعتراضات", "الاعتراض", "التكرار", "طريقة المعالجة"),
            Long("خطوات التحسين"),
            Grid("خطة الأسبوع القادم", "المهمة", "المسؤول", "الموعد"),
        }),

        // 10) تقرير كاتب المحتوى الأسبوعي
        new("تقرير كاتب المحتوى الأسبوعي", "تقرير أسبوعي لكاتب المحتوى", PeriodType.Weekly, new[]
        {
            Sec("🚦 الحالة العامة"),
            Pct("نسبة تحقيق المخرجات"),
            Pct("نسبة العمل على المشروع"),
            Sec("🔢 الأرقام"),
            Num("عدد القطع المطلوبة"),
            Num("عدد القطع المسلَّمة"),
            Num("معتمدة من أول مرة"),
            Num("متأخرة"),
            Sec("📝 التحليل"),
            Long("أسباب التأخير"),
            Long("أفضل الأفكار"),
            Long("التحديات"),
            Long("أبرز إنجاز"),
            Grid("خطة الأسبوع القادم", "المهمة", "المسؤول", "الموعد"),
        }),

        // 11) تقرير مبيعات B2B
        new("تقرير مبيعات B2B", "تقرير مبيعات الشركات", PeriodType.Weekly, new[]
        {
            Sec("🔢 الأرقام"),
            Num("عملاء محتملون جدد"),
            Num("عدد الشركات"),
            Num("عدد العروض المقدّمة"),
            Cur("قيمة العروض"),
            Num("عقود مغلقة"),
            Cur("قيمة متوقعة (Pipeline)"),
            Sec("📝 التحليل"),
            Grid("الاعتراضات", "الاعتراض", "التكرار", "طريقة المعالجة"),
            Grid("خطة الأسبوع القادم", "المهمة", "المسؤول", "الموعد"),
        }),

        // 12) تقرير متابعة مقالات SEO
        new("تقرير متابعة مقالات SEO الأسبوعي", "متابعة أسبوعية لمقالات SEO", PeriodType.Weekly, new[]
        {
            Sec("🔢 ملخص الأسبوع"),
            Num("عدد المقالات المخطط لها"),
            Num("عدد المقالات المنشورة"),
            Num("عدد المقالات المتأخرة"),
            Sec("📋 الجداول"),
            Grid("جدول المقالات", "المقال", "الكلمة المفتاحية", "الحالة", "تاريخ النشر"),
            Grid("تفاصيل المنشورة", "المقال", "الرابط", "عدد الكلمات"),
            Grid("المتأخرة", "المقال", "سبب التأخير", "الموعد الجديد"),
            Grid("خطة الأسبوع القادم", "المقال", "الكلمة المفتاحية", "الموعد"),
            Sec("📝 ملاحظات"),
            Long("أبرز إنجاز"),
            Long("أبرز تحدٍّ"),
            Long("اقتراحات"),
            Note("ملاحظات للإدارة"),
        }),

        // 13) تقرير مدير المبيعات
        new("تقرير مدير المبيعات", "تقرير أسبوعي لمدير المبيعات", PeriodType.Weekly, new[]
        {
            Sec("🚦 حالة المبيعات"),
            Pick("حالة B2C", StatusGYR),
            Pick("حالة B2B", StatusGYR),
            Pick("حالة الفريق", StatusGYR),
            YesNo("هل توجد مشكلة تحتاج تصعيد؟"),
            Note("ملاحظة مختصرة"),
            Sec("🎯 ملخص التارجت"),
            Grid("ملخص التارجت", "القناة", "المستهدف", "المحقق", "نسبة الإنجاز"),
            Sec("🧭 قراءة مدير المبيعات"),
            Long("قراءة B2C"),
            Long("قراءة B2B"),
            Long("قراءة الفريق"),
            Long("قراءة التحصيلات"),
            Note("أهم 3 ملاحظات B2C"),
            Note("أهم 3 ملاحظات B2B"),
            Sec("📊 لوحات التفصيل"),
            Grid("Pipeline B2B", "العميل", "المرحلة", "القيمة المتوقعة", "الاحتمالية"),
            Grid("خطة B2B (التارجت)", "البند", "المستهدف", "المسؤول"),
            Grid("الفرص", "الفرصة", "العائد المتوقع", "المطلوب"),
            Grid("أسباب التعطيل", "السبب", "الأثر", "المقترح"),
            Long("خطة B2C"),
            Long("خطة B2B"),
            Pick("أولوية القرارات", DecisionPriority),
            Note("قرارات مطلوبة"),
        }),

        // 14) تقرير تشغيل الأكاديمية — شيري
        new("🏫 تقرير تشغيل الأكاديمية", "تقرير أسبوعي لتشغيل الأكاديمية", PeriodType.Weekly, new[]
        {
            Sec("🚦 Scorecard"),
            YesNo("هل سارت العمليات وفق الخطة؟"),
            YesNo("هل توجد مشكلة تحتاج تصعيد؟"),
            Pick("الحالة العامة", StatusGYR),
            Sec("📚 حالة الدورات"),
            Pick("انتظام الدورات", WorkflowStatus),
            Pick("جاهزية المدربين", WorkflowStatus),
            Pick("جاهزية المواد", WorkflowStatus),
            Sec("😊 رضا المتدربين"),
            Num("متوسط رضا المتدربين (1-10)"),
            Num("عدد الشكاوى"),
            Grid("متابعة المدربين", "المدرب", "الحالة", "ملاحظة"),
            Note("ملاحظات"),
            Pick("أولوية القرارات", DecisionPriority),
            Note("قرارات مطلوبة"),
        }),

        // 15) تقرير HR — محسن
        new("👤 تقرير الموارد البشرية", "تقرير أسبوعي للموارد البشرية", PeriodType.Weekly, new[]
        {
            Sec("🚦 Scorecard"),
            YesNo("هل الحضور والانضباط منتظم؟"),
            YesNo("هل توجد مشكلة تحتاج تصعيد؟"),
            Pick("الحالة العامة", StatusGYR),
            Sec("🗓️ الحضور"),
            Num("عدد أيام الغياب"),
            Num("عدد حالات التأخير"),
            Num("عدد الإجازات"),
            Grid("الموظفون تحت الملاحظة", "الموظف", "النوع", "السبب"),
            Long("توصيات HR"),
            Pick("أولوية القرارات", DecisionPriority),
            Note("قرارات مطلوبة"),
        }),

        // 16) تقرير Web Team
        new("💻 تقرير فريق الويب", "تقرير أسبوعي لفريق الويب", PeriodType.Weekly, new[]
        {
            Sec("🚦 Scorecard"),
            YesNo("هل المشاريع تسير وفق الخطة؟"),
            YesNo("هل توجد مشكلة تحتاج تصعيد؟"),
            Pick("الحالة العامة", StatusGYR),
            Sec("🗂️ حالة المشاريع"),
            Grid("حالة المشاريع", "المشروع", "الحالة", "نسبة الإنجاز", "ملاحظة"),
            Sec("🐞 المشاكل التقنية"),
            Num("عدد المشاكل التقنية"),
            Num("عدد المشاكل المُحلّة"),
            Grid("تفاصيل الدعم الفني", "المشكلة", "الجهة", "الحالة"),
            YesNo("هل أكواد التتبع جاهزة؟"),
            Pick("أولوية القرارات", DecisionPriority),
            Note("قرارات مطلوبة"),
        }),

        // 17) تقرير النمو والأداء — أحمد عبدالفتاح (Media Buyer)
        new("📈 تقرير النمو والأداء — Media Buyer", "تقرير أسبوعي لمشتري الإعلانات", PeriodType.Weekly, new[]
        {
            Sec("🚦 الحالة العامة"),
            Pick("حالة الحملات", StatusGYR),
            Pick("حالة الصرف", StatusGYR),
            Pick("جودة النتائج", StatusGYR),
            YesNo("هل توجد مشكلة تحتاج تصعيد؟"),
            Note("ملاحظة مختصرة"),
            Sec("🔢 ملخص الأرقام"),
            Cur("إجمالي الإنفاق"),
            Num("عدد العملاء المحتملين (Leads)"),
            Cur("تكلفة العميل المحتمل (CPL)"),
            Pct("معدل النقر (CTR)"),
            Pct("معدل التحويل"),
            Sec("📝 التحليل"),
            Long("الرسائل والمحتوى الأفضل أداءً"),
            Pick("سبب المشكلة الأساسي", "الميزانية", "الاستهداف", "الإبداع (Creative)", "اللاندنج بيج", "المنتج/العرض", "المنافسة", "الموسمية", "تتبع البيانات", "أخرى"),
            Long("الاختبارات الجارية (A/B)"),
            Grid("المطلوب من فريق المحتوى", "المطلوب", "الأولوية"),
            Grid("المطلوب من فريق التصميم", "المطلوب", "الأولوية"),
            Grid("المطلوب من فريق الويب", "المطلوب", "الأولوية"),
            Grid("المطلوب من فريق المبيعات", "المطلوب", "الأولوية"),
            Note("قرارات مطلوبة"),
        }),

        // 18) تقرير النمو والأداء — محمود القوصي (مدير الأداء)
        new("📈 تقرير النمو والأداء — مدير الأداء", "تقرير أسبوعي لمدير النمو والأداء", PeriodType.Weekly, new[]
        {
            Sec("🚦 Scorecard"),
            YesNo("هل النتائج وفق المستهدف؟"),
            Pick("الحالة العامة", StatusGYR),
            Pick("حالة الصرف", StatusGYR),
            Pick("جودة النتائج", StatusGYR),
            YesNo("هل توجد مشكلة تحتاج تصعيد؟"),
            Sec("📊 لوحة أداء المشاريع"),
            Grid("أداء المشاريع", "المشروع", "الإنفاق", "النتائج", "CPL", "الحالة"),
            Grid("أفضل 3 حملات", "الحملة", "النتيجة", "السبب"),
            Grid("أكثر 3 حملات تحتاج تدخل", "الحملة", "المشكلة", "المقترح"),
            Grid("ملاحظات مشتركة", "الملاحظة", "الجهة", "الإجراء"),
            Grid("المطلوب من الفرق", "المطلوب", "الفريق", "الأولوية"),
            Sec("👤 متابعة أحمد عبدالفتاح (Media Buyer)"),
            YesNo("هل أنجز المطلوب؟"),
            Pick("تقييم الأداء", StatusGYR),
            Grid("القرارات", "القرار", "المسؤول", "الموعد"),
        }),

        // 19) تقرير خالد — B2C Sales Team Leader
        new("📞 تقرير قائد فريق مبيعات B2C", "تقرير أسبوعي لقائد فريق مبيعات الأفراد", PeriodType.Weekly, new[]
        {
            Sec("👥 أداء الفريق الفردي"),
            Grid("أداء كل عضو", "الاسم", "ليدز", "مكالمات", "متابعات", "تسجيلات", "نسبة التحويل", "جودة المحادثة", "الالتزام بالسكربت", "الحالة"),
            Sec("🔢 مؤشرات الفريق"),
            Num("إجمالي الليدز"),
            Num("إجمالي المكالمات"),
            Num("إجمالي التسجيلات"),
            Pct("نسبة التحويل العامة"),
            Sec("📝 التحليل"),
            Long("أسباب عدم الشراء"),
            Grid("الاعتراضات", "الاعتراض", "التكرار", "طريقة المعالجة"),
            Long("جودة المحادثات"),
            Long("تقييم الفريق"),
            Num("تارجت الأسبوع القادم"),
            Long("خطوات التحسين"),
            Note("ملاحظات للإدارة"),
        }),

        // 19-أ) تقرير مندوب مبيعات B2C الفردي — أساس التجميع (Rollup) لـ Business-1A
        new(B2cReportSchema.TemplateTitle, "تقرير أسبوعي فردي لمندوب مبيعات الأفراد (B2C) — أرقام قابلة للتجميع في لوحات المتابعة", PeriodType.Weekly, new[]
        {
            Sec("🔢 أرقامي هذا الأسبوع"),
            Num(B2cReportSchema.Leads),
            Num(B2cReportSchema.Calls),
            Num(B2cReportSchema.FollowUps),
            Num(B2cReportSchema.Registrations),
            Num(B2cReportSchema.ClosedDeals),
            Num(B2cReportSchema.TargetRegistrations),
            Sec("📝 التحليل والجودة"),
            Long(B2cReportSchema.LostReasons),
            Pick(B2cReportSchema.DataQuality, "🟢 محدّث بالكامل", "🟡 يحتاج استكمال", "🔴 غير محدّث"),
            Note(B2cReportSchema.Notes),
            Note(B2cReportSchema.NextActions),
        }),

        // 20) تقرير SEO Team (الفريق)
        new("🔍 تقرير فريق SEO", "تقرير أسبوعي لفريق تحسين محركات البحث", PeriodType.Weekly, new[]
        {
            Sec("🚦 Scorecard"),
            YesNo("هل تسير الخطة بانتظام؟"),
            Pick("الحالة العامة", StatusGYR),
            Note("ملاحظة مختصرة"),
            Sec("🔑 أداء الكلمات المفتاحية"),
            Num("كلمات تحسّنت"),
            Num("كلمات تراجعت"),
            Num("Organic Traffic"),
            Num("الصفحات المفهرسة"),
            Num("المهام المنفّذة"),
            Num("المشاكل التقنية"),
            Sec("📝 التحليل"),
            Long("تحليل التحسينات"),
            Grid("احتياجات من فريق الويب", "المطلوب", "الأولوية"),
            Grid("احتياجات من فريق المحتوى", "المطلوب", "الأولوية"),
            Pick("أولوية القرارات", DecisionPriority),
            Note("قرارات مطلوبة"),
        }),

        // 21) تقرير نرمين — Planning & Quality Governance
        new("🔍 تقرير التخطيط والجودة", "تقرير أسبوعي لحوكمة التخطيط والجودة", PeriodType.Weekly, new[]
        {
            Sec("📐 مؤشرات الجودة"),
            Pct("جودة المخرجات"),
            Pct("الالتزام بالمواعيد"),
            Pct("نسبة الاعتماد من أول مرة"),
            Pct("نسبة تحقيق الخطة"),
            Pct("رضا العملاء"),
            Pick("حالة المشروع", StatusGYR),
            Sec("🗺️ التخطيط"),
            Long("ملخص التخطيط"),
            Long("أولويات الأسبوع"),
            Long("المخاطر التخطيطية"),
            Sec("✅ الجودة"),
            Long("ملاحظات الجودة"),
            Long("حالات إعادة العمل"),
            Long("تحسينات مقترحة"),
            Sec("⚠️ المخاطر والقرارات"),
            Grid("المخاطر", "مستوى الخطورة", "الجهة", "وصف الخطر"),
            Note("القرارات المطلوبة"),
            Note("ملاحظات ختامية"),
        }),

        // 22) تقرير Account Management
        new("🤝 تقرير إدارة الحسابات", "تقرير أسبوعي لإدارة حسابات العملاء", PeriodType.Weekly, new[]
        {
            Sec("🚦 الحالة العامة"),
            Pick("حالة العميل", StatusGYR),
            Pick("مستوى التواصل", "ممتاز", "جيد", "ضعيف"),
            Pick("مستوى التجاوب", "سريع", "متوسط", "بطيء"),
            YesNo("هل توجد مشكلة تحتاج تصعيد؟"),
            Note("ملخص الحالة"),
            Sec("🔄 حالة التنفيذ"),
            Pick("السوشيال ميديا", WorkflowStatus),
            Pick("المحتوى", WorkflowStatus),
            Pick("التصميم", WorkflowStatus),
            Pick("الأداء", WorkflowStatus),
            Sec("📋 الجداول"),
            Grid("موقف التسليمات", "التسليم", "الموعد", "الحالة"),
            Grid("ملاحظات العميل", "الملاحظة", "الجهة المعنية", "الإجراء"),
            Grid("حالة التواصل", "التاريخ", "نوع التواصل", "الخلاصة"),
            Note("قرارات مطلوبة"),
        }),

        // 23) تقرير Account Manager
        new("🤝 تقرير مدير الحسابات", "تقرير أسبوعي لمدير الحسابات", PeriodType.Weekly, new[]
        {
            Sec("🚦 الحالة العامة"),
            Pick("حالة المحفظة", StatusGYR),
            Pick("مستوى رضا العملاء", "مرتفع", "متوسط", "منخفض"),
            YesNo("هل توجد مشكلة تحتاج تصعيد؟"),
            Note("ملخص عام"),
            Sec("👥 حالة العملاء"),
            Pick("العملاء الكبار (Key Accounts)", StatusGYR),
            Note("ملاحظة على العملاء الكبار"),
            Pick("العملاء الجدد (Onboarding)", StatusGYR),
            Note("ملاحظة على العملاء الجدد"),
            Pick("العملاء المعرّضون للخطر (At Risk)", StatusGYR),
            Note("ملاحظة على العملاء المعرّضين للخطر"),
            Sec("📋 الجداول"),
            Grid("موقف الحسابات", "العميل", "الحالة", "آخر تواصل", "الخطوة القادمة"),
            Grid("الفرص (Upsell)", "العميل", "الفرصة", "القيمة المتوقعة"),
            Grid("المطلوب من الفرق", "المطلوب", "الفريق", "الأولوية"),
            Note("قرارات مطلوبة"),
        }),
    };

    private static readonly KpiDef[] KpiDefs =
    {
        // Business-1A — مؤشرات مندوب مبيعات B2C (أوزان واقعية + ربط ما يُحسب آليًا من التقرير)
        new(B2cReportSchema.KpiTitle, "تقييم أسبوعي لمندوب مبيعات الأفراد (B2C) — مبني على أرقام التقرير الفردي", KpiCadence.WeeklyPulse, new[]
        {
            new MetricDef("تحقيق التارجت", 35m, 100m, "٪", KpiCalcMethod.Hybrid),
            new MetricDef("معدل التحويل", 25m, 25m, "٪", KpiCalcMethod.Auto),
            new MetricDef("جودة المتابعة", 15m, 5m, "تقييم", KpiCalcMethod.Manual),
            new MetricDef("الالتزام بالمواعيد والتقارير", 15m, 100m, "٪", KpiCalcMethod.Hybrid),
            new MetricDef("جودة البيانات / تحديث CRM", 10m, 5m, "تقييم", KpiCalcMethod.Manual),
        }),
        new("مؤشرات مندوب المبيعات", "تقييم ربع سنوي لمندوب المبيعات", KpiCadence.Quarterly, new[]
        {
            new MetricDef("تحقيق المستهدف البيعي", 40m, 100m, "٪"),
            new MetricDef("عدد الصفقات المغلقة", 20m, 30m, "صفقة"),
            new MetricDef("معدل التحويل", 20m, 25m, "٪"),
            new MetricDef("رضا العملاء", 20m, 5m, "تقييم"),
        }),
        new("النبض الأسبوعي العام", "تقييم أسبوعي موجز لجميع الموظفين", KpiCadence.WeeklyPulse, new[]
        {
            new MetricDef("إنجاز المهام", 50m, 100m, "٪"),
            new MetricDef("الالتزام بالمواعيد", 25m, 100m, "٪"),
            new MetricDef("جودة التسليم", 25m, 5m, "تقييم"),
        }),
        new("مؤشرات مشتري الإعلانات", "تقييم ربع سنوي لأداء الحملات المدفوعة", KpiCadence.Quarterly, new[]
        {
            new MetricDef("العائد على الإنفاق الإعلاني (ROAS)", 35m, 4m, "x"),
            new MetricDef("تكلفة العميل المحتمل (CPL)", 25m, 50m, "ج.م"),
            new MetricDef("معدل النقر (CTR)", 20m, 3m, "٪"),
            new MetricDef("الالتزام بالميزانية", 20m, 100m, "٪"),
        }),
        // Business-1C — مؤشرات أداء SEO (أسبوعي). الأوزان مبنية على مزيج آلي/هجين/يدوي من تقارير SEO الحالية.
        // Auto: إنجاز خطة المحتوى (منشورة/مخطّط) وتحسّن الكلمات (صافي تحسّن من حقول يدوية).
        // Hybrid: جودة التنفيذ الفني (عدد مشاكل تقنية + تقدير) والالتزام بالمواعيد (مقالات متأخرة + تقدير).
        // Manual: جودة التقرير والتحليل (تقدير القائد). Organic Traffic لا يُستخدم كمصدر آلي دقيق (يحتاج GSC/GA).
        new(SeoReportSchema.KpiTitle, "تقييم أسبوعي لأداء فريق/أخصائي SEO — مبني على أرقام تقارير SEO الحالية بدون تكامل خارجي", KpiCadence.WeeklyPulse, new[]
        {
            new MetricDef("إنجاز خطة SEO الأسبوعية", 30m, 100m, "٪", KpiCalcMethod.Auto),
            new MetricDef("جودة التنفيذ الفني", 20m, 5m, "تقييم", KpiCalcMethod.Hybrid),
            new MetricDef("تحسّن الكلمات/المؤشرات", 20m, 100m, "٪", KpiCalcMethod.Auto),
            new MetricDef("الالتزام بالمواعيد", 15m, 100m, "٪", KpiCalcMethod.Hybrid),
            new MetricDef("جودة التقرير والتحليل", 15m, 5m, "تقييم", KpiCalcMethod.Manual),
        }),

        // Business-1D-1: مؤشرات كاتب المحتوى (نبض أسبوعي) — مبنية على أرقام «تقرير كاتب المحتوى الأسبوعي».
        // Auto: إنجاز المحتوى المطلوب (المسلَّمة/المطلوبة) وجودة المحتوى (الاعتماد من أول مرة) — كلاهما من حقول رقمية متاحة.
        // Hybrid: الالتزام بالمواعيد (المتأخرة + تقدير) وتقليل التعديلات (نسبة التعديلات + تقدير القائد).
        // Manual: جودة التقرير والتوثيق (تقدير بشري).
        new(ContentWriterReportSchema.KpiTitle, "تقييم أسبوعي لكاتب المحتوى — مبني على أرقام تقرير كاتب المحتوى الحالي بدون تكامل خارجي", KpiCadence.WeeklyPulse, new[]
        {
            new MetricDef("إنجاز المحتوى المطلوب", 30m, 100m, "٪", KpiCalcMethod.Auto),
            new MetricDef("جودة المحتوى / الاعتماد من أول مرة", 25m, 100m, "٪", KpiCalcMethod.Auto),
            new MetricDef("الالتزام بالمواعيد", 20m, 100m, "٪", KpiCalcMethod.Hybrid),
            new MetricDef("تقليل التعديلات", 15m, 5m, "تقييم", KpiCalcMethod.Hybrid),
            new MetricDef("جودة التقرير والتوثيق", 10m, 5m, "تقييم", KpiCalcMethod.Manual),
        }),

        // Business-1D-2: مؤشرات المصمّم (نبض أسبوعي) — مبنية على أرقام «تقرير فريق التصميم».
        // Auto: إنجاز التصميمات المطلوبة (المسلَّمة/المطلوبة) والالتزام بالمواعيد (المسلَّمة−المتأخرة/المسلَّمة) — كلاهما من حقول رقمية متاحة.
        // Hybrid: تقليل طلبات التعديل (نسبة «أعيدت للتعديل» + تقدير القائد) وجودة التصميم والالتزام بالهوية (الاعتماد من أول مرة + تقدير).
        // Manual: التعاون مع المحتوى والفيديو (تقدير بشري — لا يوجد حقل رقمي).
        new(DesignerReportSchema.KpiTitle, "تقييم أسبوعي للمصمّم — مبني على أرقام تقرير فريق التصميم الحالي بدون تكامل خارجي", KpiCadence.WeeklyPulse, new[]
        {
            new MetricDef("إنجاز التصميمات المطلوبة", 30m, 100m, "٪", KpiCalcMethod.Auto),
            new MetricDef("جودة التصميم والالتزام بالهوية", 25m, 5m, "تقييم", KpiCalcMethod.Hybrid),
            new MetricDef("الالتزام بالمواعيد", 20m, 100m, "٪", KpiCalcMethod.Auto),
            new MetricDef("تقليل طلبات التعديل", 15m, 5m, "تقييم", KpiCalcMethod.Hybrid),
            new MetricDef("التعاون مع المحتوى والفيديو", 10m, 5m, "تقييم", KpiCalcMethod.Manual),
        }),

        // Business-1D-3: مؤشرات الفيديو (نبض أسبوعي) — مبنية على أرقام «تقرير فريق الفيديو» (مطابق بنيويًا لقالب التصميم).
        // Auto: إنجاز الفيديوهات المطلوبة (المسلَّمة/المطلوبة) والالتزام بالمواعيد (المسلَّمة−المتأخرة/المسلَّمة) — كلاهما من حقول رقمية متاحة.
        // Hybrid: جودة الإخراج والمونتاج (الاعتماد من أول مرة + تقدير) وتقليل التعديلات (نسبة «أعيدت للتعديل» + تقدير القائد).
        // Manual: جودة التنسيق مع المحتوى والتصميم (تقدير بشري — لا يوجد حقل رقمي).
        new(VideoReportSchema.KpiTitle, "تقييم أسبوعي لفريق الفيديو — مبني على أرقام تقرير فريق الفيديو الحالي بدون تكامل خارجي", KpiCadence.WeeklyPulse, new[]
        {
            new MetricDef("إنجاز الفيديوهات المطلوبة", 30m, 100m, "٪", KpiCalcMethod.Auto),
            new MetricDef("جودة الإخراج والمونتاج", 25m, 5m, "تقييم", KpiCalcMethod.Hybrid),
            new MetricDef("الالتزام بالمواعيد", 20m, 100m, "٪", KpiCalcMethod.Auto),
            new MetricDef("تقليل التعديلات", 15m, 5m, "تقييم", KpiCalcMethod.Hybrid),
            new MetricDef("جودة التنسيق مع المحتوى والتصميم", 10m, 5m, "تقييم", KpiCalcMethod.Manual),
        }),

        // Business-1D-4: مؤشرات المودريشن (نبض أسبوعي) — مبنية على أرقام «تقرير المديرشن الأسبوعي».
        // المودريشن يقيس الاستجابة لا الإنتاج، لذا المقاييس مختلفة عن المحتوى/التصميم/الفيديو.
        // Auto: سرعة الاستجابة (متوسط زمن الرد رقمي متاح) وتحويل الفرص (عدد الفرص المحوَّلة رقمي مُضاف).
        // Hybrid: جودة الردود (نسبة الرد + تقدير القائد) ودقة التصعيد (عدد المصعّدة + تقدير).
        // Manual: الالتزام بالمتابعة اليومية (تقدير بشري — لا يوجد حقل رقمي مباشر).
        new(ModerationReportSchema.KpiTitle, "تقييم أسبوعي للمودريشن — مبني على أرقام تقرير المديرشن الأسبوعي الحالي بدون تكامل خارجي", KpiCadence.WeeklyPulse, new[]
        {
            new MetricDef("سرعة الاستجابة", 25m, 100m, "٪", KpiCalcMethod.Auto),
            new MetricDef("جودة الردود", 25m, 5m, "تقييم", KpiCalcMethod.Hybrid),
            new MetricDef("الالتزام بالمتابعة اليومية", 20m, 5m, "تقييم", KpiCalcMethod.Manual),
            new MetricDef("دقة التصعيد", 15m, 5m, "تقييم", KpiCalcMethod.Hybrid),
            new MetricDef("تحويل الفرص أو دعم المبيعات", 15m, 100m, "٪", KpiCalcMethod.Auto),
        }),
    };
}
