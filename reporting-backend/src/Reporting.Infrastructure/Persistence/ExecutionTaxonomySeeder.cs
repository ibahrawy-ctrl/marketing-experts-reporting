using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Domain.Entities.ExecutionTaxonomy;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// بذر كتالوج تصنيفات التنفيذ الأولي (قابل للتعديل لاحقًا من شاشة الإدارة Task 4D2).
/// idempotent بمطابقة (Domain, Code): لا يحذف/يعدّل القائم، ولا يمسّ أي تقرير/قالب.
/// يعمل في كل البيئات عند الإقلاع — يجب أن يعمل قبل TemplateSeeder كي تتوفّر القيم عند بناء قوالب v3.
/// </summary>
public static class ExecutionTaxonomySeeder
{
    private record TaxDef(string Domain, string Code, string NameAr, string? NameEn = null);

    // القيم مرتّبة حسب Domain ثم بترتيب الظهور. SortOrder يُشتقّ من ترتيب العنصر داخل الـ Domain (×10).
    private static readonly TaxDef[] Defs =
    {
        // ===== كاتب المحتوى (Content Writer) =====
        // نوع المحتوى
        new("content_type", "carousel", "Carousel"),
        new("content_type", "static_post", "Static Post"),
        new("content_type", "reel_script", "Reel Script"),
        new("content_type", "story", "Story"),
        new("content_type", "blog", "Blog"),
        new("content_type", "landing_page_copy", "Landing Page Copy"),
        new("content_type", "email", "Email"),
        new("content_type", "whatsapp_copy", "WhatsApp Copy"),
        new("content_type", "product_description", "Product Description"),
        new("content_type", "case_study", "Case Study"),
        new("content_type", "company_profile", "Company Profile"),
        new("content_type", "other", "أخرى", "Other"),
        // هدف المحتوى
        new("content_goal", "sales", "Sales"),
        new("content_goal", "educational", "Educational"),
        new("content_goal", "awareness", "Awareness"),
        new("content_goal", "engagement", "Engagement"),
        new("content_goal", "branding", "Branding"),
        new("content_goal", "offer", "Offer"),
        new("content_goal", "recruitment", "Recruitment"),
        new("content_goal", "event", "Event"),
        new("content_goal", "announcement", "Announcement"),
        // حالة العمل
        new("work_status", "draft", "Draft"),
        new("work_status", "revision", "Revision"),
        new("work_status", "approved", "Approved"),
        new("work_status", "published", "Published"),

        // ===== المصمّم (Designer) =====
        // نوع التصميم
        new("design_type", "carousel", "Carousel"),
        new("design_type", "static", "Static"),
        new("design_type", "story", "Story"),
        new("design_type", "infographic", "Infographic"),
        new("design_type", "banner", "Banner"),
        new("design_type", "thumbnail", "Thumbnail"),
        new("design_type", "presentation", "Presentation"),
        new("design_type", "website_section", "Website Section"),
        new("design_type", "landing_page", "Landing Page"),
        new("design_type", "logo", "Logo"),
        new("design_type", "brand_identity", "Brand Identity"),
        new("design_type", "print", "Print"),
        new("design_type", "motion_assets", "Motion Assets"),
        // حالة التصميم
        new("design_status", "new", "New"),
        new("design_status", "revision", "Revision"),
        new("design_status", "final", "Final"),
        new("design_status", "delivered", "Delivered"),
        // أداة التنفيذ
        new("design_tool", "photoshop", "Photoshop"),
        new("design_tool", "illustrator", "Illustrator"),
        new("design_tool", "canva", "Canva"),
        new("design_tool", "figma", "Figma"),
        new("design_tool", "ai_assisted", "AI Assisted"),

        // ===== محرّر الفيديو (Video Editor) =====
        // نوع الفيديو
        new("video_type", "reel", "Reel"),
        new("video_type", "tiktok", "TikTok"),
        new("video_type", "short", "Short"),
        new("video_type", "long_video", "Long Video"),
        new("video_type", "podcast", "Podcast"),
        new("video_type", "motion_graphic", "Motion Graphic"),
        new("video_type", "animation", "Animation"),
        new("video_type", "interview", "Interview"),
        new("video_type", "testimonial", "Testimonial"),
        new("video_type", "ad", "Ad"),
        // نوع التنفيذ
        new("edit_type", "full_editing", "Full Editing"),
        new("edit_type", "ai_generated", "AI Generated"),
        new("edit_type", "ai_assisted", "AI Assisted"),
        new("edit_type", "light_edit", "Light Edit"),
        new("edit_type", "subtitle_only", "Subtitle Only"),
        new("edit_type", "motion_only", "Motion Only"),
        new("edit_type", "color_correction", "Color Correction"),
        // مدة الفيديو
        new("video_duration", "under_1min", "أقل من دقيقة", "Under 1 min"),
        new("video_duration", "1_3min", "1–3 دقائق", "1–3 min"),
        new("video_duration", "3_10min", "3–10 دقائق", "3–10 min"),
        new("video_duration", "over_10min", "أكثر من 10 دقائق", "Over 10 min"),
        // حالة الفيديو
        new("video_status", "draft", "Draft"),
        new("video_status", "revision", "Revision"),
        new("video_status", "final", "Final"),
        new("video_status", "published", "Published"),

        // ===== المديرشن (Moderation) =====
        // نوع النشاط
        new("activity_type", "comments", "Comments"),
        new("activity_type", "inbox", "Inbox"),
        new("activity_type", "messenger", "Messenger"),
        new("activity_type", "whatsapp", "WhatsApp"),
        new("activity_type", "instagram_dm", "Instagram DM"),
        new("activity_type", "tiktok_messages", "TikTok Messages"),
        // نتيجة التفاعل
        new("interaction_result", "inquiry", "Inquiry"),
        new("interaction_result", "lead", "Lead"),
        new("interaction_result", "complaint", "Complaint"),
        new("interaction_result", "escalated", "Escalated"),
        new("interaction_result", "closed", "Closed"),
        new("interaction_result", "follow_up", "Follow-up"),
        // زمن الاستجابة
        new("response_time", "under_1h", "أقل من ساعة", "Under 1 hour"),
        new("response_time", "under_4h", "أقل من 4 ساعات", "Under 4 hours"),
        new("response_time", "under_1d", "أقل من يوم", "Under 1 day"),
        new("response_time", "over_1d", "أكثر من يوم", "Over 1 day"),

        // ========================================================================
        // ===== P0 — منصّة التنفيذ العامة (Execution Platform Catalog Freeze) =====
        // ستة مجالات جديدة تُجهِّز المنصّة العامة. إضافة بحتة — لا تمسّ المجالات/القيم أعلاه.
        // ========================================================================

        // ===== نوع تيار العمل (Workstream Type) =====
        new("workstream_type", "social_media", "سوشيال ميديا", "Social Media"),
        new("workstream_type", "content", "المحتوى", "Content"),
        new("workstream_type", "design", "التصميم", "Design"),
        new("workstream_type", "video", "الفيديو", "Video"),
        new("workstream_type", "moderation", "المديرشن", "Moderation"),
        new("workstream_type", "web_development", "تطوير الويب", "Web Development"),
        new("workstream_type", "ui_design", "تصميم واجهات المستخدم", "UI Design"),
        new("workstream_type", "seo", "تحسين محرّكات البحث", "SEO"),
        new("workstream_type", "ads", "الإعلانات", "Ads"),
        new("workstream_type", "email_marketing", "التسويق بالبريد", "Email Marketing"),
        new("workstream_type", "whatsapp", "واتساب", "WhatsApp"),
        new("workstream_type", "branding", "الهوية والعلامة التجارية", "Branding"),

        // ===== المُخرَج (Deliverable) =====
        new("deliverable", "post", "منشور", "Post"),
        new("deliverable", "carousel", "كاروسيل", "Carousel"),
        new("deliverable", "story", "ستوري", "Story"),
        new("deliverable", "reel", "ريل", "Reel"),
        new("deliverable", "ad_creative", "تصميم إعلان", "Ad Creative"),
        new("deliverable", "landing_page", "صفحة هبوط", "Landing Page"),
        new("deliverable", "website_page", "صفحة موقع", "Website Page"),
        new("deliverable", "homepage", "الصفحة الرئيسية", "Homepage"),
        new("deliverable", "service_page", "صفحة خدمة", "Service Page"),
        new("deliverable", "blog_article", "مقال مدوّنة", "Blog Article"),
        new("deliverable", "seo_article", "مقال SEO", "SEO Article"),
        new("deliverable", "keyword_research", "بحث الكلمات المفتاحية", "Keyword Research"),
        new("deliverable", "technical_fix", "إصلاح تقني", "Technical Fix"),
        new("deliverable", "internal_linking", "روابط داخلية", "Internal Linking"),
        new("deliverable", "backlink", "رابط خارجي", "Backlink"),
        new("deliverable", "video_short", "فيديو قصير", "Short Video"),
        new("deliverable", "long_video", "فيديو طويل", "Long Video"),
        new("deliverable", "motion_graphic", "موشن جرافيك", "Motion Graphic"),
        new("deliverable", "moderation_response", "ردّ مديرشن", "Moderation Response"),
        new("deliverable", "lead_handling", "معالجة عميل محتمل", "Lead Handling"),
        new("deliverable", "complaint_handling", "معالجة شكوى", "Complaint Handling"),

        // ===== سياق الاستخدام (Usage Context) =====
        new("usage_context", "organic_social", "سوشيال أورجانيك", "Organic Social"),
        new("usage_context", "paid_ads", "إعلانات مدفوعة", "Paid Ads"),
        new("usage_context", "website", "موقع إلكتروني", "Website"),
        new("usage_context", "landing_page", "صفحة هبوط", "Landing Page"),
        new("usage_context", "seo", "تحسين محرّكات البحث", "SEO"),
        new("usage_context", "email", "بريد إلكتروني", "Email"),
        new("usage_context", "whatsapp", "واتساب", "WhatsApp"),
        new("usage_context", "branding", "الهوية والعلامة التجارية", "Branding"),
        new("usage_context", "event", "فعالية", "Event"),
        new("usage_context", "internal", "داخلي", "Internal"),
        new("usage_context", "course", "دورة تدريبية", "Course"),
        new("usage_context", "print", "مطبوعات", "Print"),

        // ===== خطوة سير العمل (Workflow Step) =====
        new("workflow_step", "planned", "مخطّط", "Planned"),
        new("workflow_step", "assigned", "مُسنَد", "Assigned"),
        new("workflow_step", "in_progress", "قيد التنفيذ", "In Progress"),
        new("workflow_step", "draft", "مسودّة", "Draft"),
        new("workflow_step", "internal_review", "مراجعة داخلية", "Internal Review"),
        new("workflow_step", "client_review", "مراجعة العميل", "Client Review"),
        new("workflow_step", "revision", "تعديل", "Revision"),
        new("workflow_step", "approved", "معتمَد", "Approved"),
        new("workflow_step", "delivered", "مُسلَّم", "Delivered"),
        new("workflow_step", "published", "منشور", "Published"),
        new("workflow_step", "live", "مباشر", "Live"),
        new("workflow_step", "verified", "مُتحقَّق منه", "Verified"),
        new("workflow_step", "reported", "مُبلَّغ عنه", "Reported"),
        new("workflow_step", "blocked", "متوقّف", "Blocked"),
        new("workflow_step", "cancelled", "مُلغى", "Cancelled"),

        // ===== سبب التأخير (Delay Reason) =====
        new("delay_reason", "client_delay", "تأخّر العميل", "Client Delay"),
        new("delay_reason", "dependency_delay", "تأخّر اعتمادية", "Dependency Delay"),
        new("delay_reason", "approval_delay", "تأخّر الاعتماد", "Approval Delay"),
        new("delay_reason", "content_missing", "نقص المحتوى", "Content Missing"),
        new("delay_reason", "design_missing", "نقص التصميم", "Design Missing"),
        new("delay_reason", "technical_issue", "مشكلة تقنية", "Technical Issue"),
        new("delay_reason", "capacity_overload", "ضغط الطاقة الاستيعابية", "Capacity Overload"),
        new("delay_reason", "unclear_brief", "بريف غير واضح", "Unclear Brief"),
        new("delay_reason", "scope_change", "تغيّر النطاق", "Scope Change"),
        new("delay_reason", "priority_change", "تغيّر الأولوية", "Priority Change"),
        new("delay_reason", "other", "أخرى", "Other"),

        // ===== المنصّة/القناة (Platform / Channel) =====
        new("platform_channel", "instagram", "إنستغرام", "Instagram"),
        new("platform_channel", "facebook", "فيسبوك", "Facebook"),
        new("platform_channel", "tiktok", "تيك توك", "TikTok"),
        new("platform_channel", "snapchat", "سناب شات", "Snapchat"),
        new("platform_channel", "x", "إكس (تويتر)", "X"),
        new("platform_channel", "linkedin", "لينكدإن", "LinkedIn"),
        new("platform_channel", "youtube", "يوتيوب", "YouTube"),
        new("platform_channel", "whatsapp", "واتساب", "WhatsApp"),
        new("platform_channel", "messenger", "ماسنجر", "Messenger"),
        new("platform_channel", "website", "موقع إلكتروني", "Website"),
        new("platform_channel", "email", "بريد إلكتروني", "Email"),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();

        var existing = (await db.ExecutionTaxonomyValues
                .Select(v => new { v.Domain, v.Code })
                .ToListAsync())
            .Select(x => (x.Domain, x.Code))
            .ToHashSet();

        // عدّاد ترتيب مستقلّ لكل Domain (×10) مطابق لترتيب التعريف أعلاه.
        var sortByDomain = new Dictionary<string, int>();

        foreach (var def in Defs)
        {
            var next = sortByDomain.TryGetValue(def.Domain, out var cur) ? cur + 10 : 10;
            sortByDomain[def.Domain] = next;

            if (existing.Contains((def.Domain, def.Code))) continue;

            db.ExecutionTaxonomyValues.Add(new ExecutionTaxonomyValue
            {
                Domain = def.Domain,
                Code = def.Code,
                NameAr = def.NameAr,
                NameEn = def.NameEn ?? def.NameAr,
                SortOrder = next,
                IsActive = true,
            });
        }

        await db.SaveChangesAsync();
    }
}
