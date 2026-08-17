using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Entities.Projects360;

namespace Reporting.Infrastructure.Persistence.Configurations;

/// <summary>
/// إعداد كيان الهدف (CPW-R3 · D-02 · §5-4).
///
/// <para>
/// **الحذف التعاقبيّ من المشروع**: حذف المشروع يحذف أهدافه. لا خاصّيّة تنقّل من الهدف إلى المشروع
/// عمدًا (الاتّجاه واحد)، فتُعرَّف العلاقة بصيغة <c>HasOne&lt;Project&gt;().WithMany()</c>.
/// </para>
///
/// <para>
/// **حارس مكافحة اليُتم** (<c>project_objective.has_kpis.conflict</c>) يبقى في **طبقة الخدمة** لا هنا:
/// القاعدة لا تعرف الفرق بين حذف هدف مباشرةً وحذفه ضمن حذف مشروع، والقيد على القاعدة كان سيمنع
/// حذف المشروع نفسه. الفصل مقصود.
/// </para>
/// </summary>
public class ProjectObjectiveConfiguration : IEntityTypeConfiguration<ProjectObjective>
{
    public void Configure(EntityTypeBuilder<ProjectObjective> b)
    {
        b.ToTable("project_objectives");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Notes).HasMaxLength(2000);

        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        b.Property(x => x.Weight).HasColumnType("numeric(9,2)");

        b.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.WorkstreamId);
        b.HasIndex(x => x.OwnerUserId);
    }
}

/// <summary>
/// إعداد كيان مؤشّر المشروع (CPW-R3 · D-02 + D-06 · §5-5).
///
/// <para>
/// **مساران تعاقبيّان مقصودان**: من <c>projects</c> ومن <c>project_objectives</c>. هذا مقبول في
/// PostgreSQL (خلافًا لـSQL Server) لأنّ حذف صفّ محذوف سلفًا لا يُخطئ. البديل <c>Restrict</c>
/// كان سيجعل حذف المشروع يفشل إن لم يُضمَن ترتيب التتالي — وهو ما لا يُضمَن.
/// </para>
///
/// <para>
/// **<c>ObjectiveId</c> إلزاميّ (NOT NULL)** بقرار D-02 — مؤشّر بلا هدف مرفوض بنيويًّا لا منطقيًّا.
/// </para>
/// </summary>
public class ProjectKpiConfiguration : IEntityTypeConfiguration<ProjectKpi>
{
    public void Configure(EntityTypeBuilder<ProjectKpi> b)
    {
        b.ToTable("project_kpis");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.CustomUnitLabel).HasMaxLength(100);

        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Unit).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(20);

        b.Property(x => x.BaselineValue).HasColumnType("numeric(18,2)");
        b.Property(x => x.TargetValue).HasColumnType("numeric(18,2)");
        b.Property(x => x.CurrentValue).HasColumnType("numeric(18,2)");
        b.Property(x => x.Weight).HasColumnType("numeric(9,2)");

        // D-06 مرحلة 3 — مُهيَّأة الآن وفارغة تمامًا (تمنع هجرة مستقبليّة).
        b.Property(x => x.ExternalSourceKey).HasMaxLength(100);
        b.Property(x => x.ExternalMetricCode).HasMaxLength(200);

        b.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Objective)
            .WithMany(o => o.Kpis)
            .HasForeignKey(x => x.ObjectiveId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => x.ObjectiveId);
        b.HasIndex(x => x.SourceType);
    }
}

/// <summary>
/// إعداد كيان قراءة المؤشّر (CPW-R3 · §5-6).
///
/// <para>
/// **الفهرس الفريد** <c>(ProjectKpiId, ReadingDate)</c> يفرض «قراءة واحدة لكلّ تاريخ لكلّ مؤشّر»
/// على مستوى القاعدة، وهو سند الحارس <c>project_kpi_reading.duplicate_date.conflict</c> (409).
/// </para>
/// </summary>
public class ProjectKpiReadingConfiguration : IEntityTypeConfiguration<ProjectKpiReading>
{
    public void Configure(EntityTypeBuilder<ProjectKpiReading> b)
    {
        b.ToTable("project_kpi_readings");
        b.HasKey(x => x.Id);

        b.Property(x => x.Notes).HasMaxLength(2000);

        b.Property(x => x.Value).HasColumnType("numeric(18,2)");
        b.Property(x => x.TargetSnapshot).HasColumnType("numeric(18,2)");
        b.Property(x => x.AchievementSnapshot).HasColumnType("numeric(9,2)");

        b.HasOne(x => x.ProjectKpi)
            .WithMany(k => k.Readings)
            .HasForeignKey(x => x.ProjectKpiId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.ProjectKpiId, x.ReadingDate })
            .IsUnique()
            .HasDatabaseName("IX_project_kpi_readings_ProjectKpiId_ReadingDate");

        b.HasIndex(x => x.ReadingDate);
    }
}

/// <summary>
/// إعداد كيان **المخرَج التعاقديّ** (CPW-R3 · D-03 · §5-7).
///
/// <para>
/// **<c>ObjectiveId</c> اختياريّ بحذف <c>SetNull</c>**: المخرَج التزام تعاقديّ قائم بذاته؛ حذف هدف
/// مرتبط يجب ألّا يمحو التزامًا أمام العميل — يفكّ الربط فقط.
/// </para>
///
/// <para>
/// **<c>WorkstreamId</c> مرجع <c>Guid?</c> بلا مفتاح أجنبيّ** — جسر عرضٍ فقط، صيانةً للحدّ الصارم
/// «لا مساس بأيّ كيان <c>Workstream*</c>».
/// </para>
/// </summary>
public class ProjectDeliverableConfiguration : IEntityTypeConfiguration<ProjectDeliverable>
{
    public void Configure(EntityTypeBuilder<ProjectDeliverable> b)
    {
        b.ToTable("project_deliverables");
        b.HasKey(x => x.Id);

        b.Property(x => x.DeliverableTypeCode).IsRequired().HasMaxLength(100);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Notes).HasMaxLength(2000);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);

        b.Property(x => x.ProgressPercent).HasColumnType("numeric(9,2)");

        // نفس دقّة كلّ نسب المنظومة (DN-05) — الوزن نسبة لا عدد صحيح.
        b.Property(x => x.WeightPercentage).HasColumnType("numeric(9,2)");

        b.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Objective)
            .WithMany()
            .HasForeignKey(x => x.ObjectiveId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => x.ObjectiveId);
        b.HasIndex(x => x.WorkstreamId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.OwnerUserId);
        b.HasIndex(x => x.DeliverableTypeCode);
    }
}

/// <summary>
/// إعداد النواة الثابتة للاستراتيجيّة (CPW-R3 · D-04 · §5-3-أ).
///
/// <para>
/// **القيد الحاكم — الفهرس الفريد الجزئيّ**: التفرّد على <c>ProjectId</c> **مشروطًا بـ<c>IsActive</c>**
/// لا مطلقًا. هذا هو ما يجعل الإصدارات (Versioning) ترقيةً **إضافيّة بحتة** لاحقًا: القيد المطلق
/// كان سيجعل النسخة التاريخيّة مستحيلةً فيُجبِر الترقية على **إسقاط قيد قائم** (تغيير كاسر)،
/// بينما القيد المشروط يسمح ببقاء نسخ غير نشطة **من اليوم** مع بقاء «استراتيجيّة نشطة واحدة
/// لكلّ مشروع» مفروضًا على القاعدة.
/// </para>
/// </summary>
public class ProjectStrategyConfiguration : IEntityTypeConfiguration<ProjectStrategy>
{
    public void Configure(EntityTypeBuilder<ProjectStrategy> b)
    {
        b.ToTable("project_strategies");
        b.HasKey(x => x.Id);

        b.Property(x => x.Vision).HasMaxLength(4000);
        b.Property(x => x.StrategySummary).HasMaxLength(4000);
        b.Property(x => x.TargetAudience).HasMaxLength(4000);
        b.Property(x => x.CustomerPersona).HasMaxLength(4000);
        b.Property(x => x.Positioning).HasMaxLength(4000);
        b.Property(x => x.ValueProposition).HasMaxLength(4000);
        b.Property(x => x.Competitors).HasMaxLength(4000);
        b.Property(x => x.ToneOfVoice).HasMaxLength(4000);
        b.Property(x => x.Messaging).HasMaxLength(4000);
        b.Property(x => x.MarketingApproach).HasMaxLength(4000);
        b.Property(x => x.SuccessFactors).HasMaxLength(4000);

        b.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ProjectId)
            .HasFilter("\"IsActive\" = true")
            .IsUnique()
            .HasDatabaseName("IX_project_strategies_ProjectId_Active");
    }
}

/// <summary>
/// إعداد السمة الاستراتيجيّة المشروطة بنوع الخدمة (CPW-R3 · D-04 · §5-3-ب).
///
/// <para>
/// **الفهرس الفريد** <c>(ProjectStrategyId, FieldCode)</c>: سمة واحدة لكلّ رمز حقل لكلّ استراتيجيّة.
/// </para>
///
/// <para>
/// <c>FieldCode</c> و<c>SectionCode</c> **نصّان بلا مفتاح أجنبيّ** إلى كتالوج التصنيفات — التحقّق
/// خادميّ (<c>project_strategy.field_code_invalid</c> / <c>section_code_invalid</c> · 400)
/// كي يبقى «قسم جديد» صفَّ بيانات لا هجرةَ بنية.
/// </para>
/// </summary>
public class ProjectStrategyAttributeConfiguration : IEntityTypeConfiguration<ProjectStrategyAttribute>
{
    public void Configure(EntityTypeBuilder<ProjectStrategyAttribute> b)
    {
        b.ToTable("project_strategy_attributes");
        b.HasKey(x => x.Id);

        b.Property(x => x.FieldCode).IsRequired().HasMaxLength(100);
        b.Property(x => x.SectionCode).HasMaxLength(100);
        b.Property(x => x.ValueText).HasMaxLength(4000);

        b.HasOne(x => x.ProjectStrategy)
            .WithMany(s => s.Attributes)
            .HasForeignKey(x => x.ProjectStrategyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.ProjectStrategyId, x.FieldCode })
            .IsUnique()
            .HasDatabaseName("IX_project_strategy_attributes_StrategyId_FieldCode");

        b.HasIndex(x => x.SectionCode);
    }
}

/// <summary>
/// إعداد **جسر التنفيذ الهجين** (P360-WF-R2 · §10).
///
/// <para>
/// **حذف المشروع يجرف مقترحاته (<c>Cascade</c>)** — المقترح ادّعاء على مخرَجٍ داخل مشروع، ولا معنى
/// لبقائه بعد زوال المشروع. وكذلك الحذف عبر المخرَج: مقترحٌ على مخرَجٍ محذوف ادّعاءٌ بلا هدف.
/// </para>
///
/// <para>
/// **<c>SubmissionId</c> بلا مفتاح أجنبيّ** — كما في <c>WorkstreamId</c> أعلاه: إشارة استدلاليّة إلى
/// دورة التقارير لا ارتباط بنيويّ بها. ربطه بمفتاح أجنبيّ كان سيجعل دورة اعتماد التقارير قادرة على
/// إسقاط سجلّ تنفيذٍ مُقَرّ، وهو ما تمنعه القاعدة «الجسر لا يملك دورة التقارير ولا تملكه».
/// </para>
///
/// <para>
/// **الفهرس <c>(DeliverableId, Status)</c>** يخدم السؤال التشغيليّ الوحيد المتكرّر: «ما المقترحات
/// المعلَّقة على هذا المخرَج؟» — وهو نفسه حارس التعادليّة عند القبول.
/// </para>
/// </summary>
public class ProjectExecutionUpdateProposalConfiguration : IEntityTypeConfiguration<ProjectExecutionUpdateProposal>
{
    public void Configure(EntityTypeBuilder<ProjectExecutionUpdateProposal> b)
    {
        b.ToTable("project_execution_update_proposals");
        b.HasKey(x => x.Id);

        b.Property(x => x.ExecutionNote).HasMaxLength(2000);
        b.Property(x => x.ReviewNote).HasMaxLength(2000);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ProposedStatus).HasConversion<string>().HasMaxLength(20);

        b.Property(x => x.ProposedProgressPercent).HasColumnType("numeric(9,2)");
        b.Property(x => x.PreviousProgressPercent).HasColumnType("numeric(9,2)");

        b.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Deliverable)
            .WithMany()
            .HasForeignKey(x => x.DeliverableId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => new { x.DeliverableId, x.Status })
            .HasDatabaseName("IX_project_execution_update_proposals_DeliverableId_Status");
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.ProposedById);
        b.HasIndex(x => x.SubmissionId);
    }
}
