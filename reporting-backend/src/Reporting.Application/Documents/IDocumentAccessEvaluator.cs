using System.Linq.Expressions;
using Reporting.Domain.Entities.Clients;

namespace Reporting.Application.Documents;

/// <summary>
/// نتيجة تقييم صلاحية مستند واحد (CPW-R2).
/// في v1 <c>CanViewMetadata == CanDownload</c> — لا فصل بين رؤية البيانات الوصفيّة والتنزيل.
/// </summary>
public readonly record struct DocumentAccess(bool CanViewMetadata, bool CanDownload)
{
    public static readonly DocumentAccess Denied = new(false, false);
    public static readonly DocumentAccess Granted = new(true, true);
}

/// <summary>
/// سياق تقييم صلاحيّات مستندات عميل واحد. يُبنى مرّة واحدة لكلّ طلب ثمّ يُعاد استخدامه
/// لكلّ مستند (أو يُترجَم إلى شرط SQL) — كي لا يتكرّر أيّ استعلام لكلّ صفّ.
/// </summary>
/// <param name="UserId">معرّف المستخدم الحالي (null للمجهول — يُرفَض دائمًا خارج الأدوار).</param>
/// <param name="RoleNames">أسماء أدوار المستخدم الحالي كما هي في Identity.</param>
/// <param name="IsAdmin">مدير النظام — يتجاوز كلّ السياسات.</param>
/// <param name="IsManagement">الإدارة العليا/المديرون (<c>Roles.ClientCoreManagers</c>).</param>
/// <param name="IsFinance">المالية (<c>FinanceManager</c>/<c>Accountant</c>) — منفصلة تمامًا عن الإدارة.</param>
/// <param name="IsHrManagement">الموارد البشريّة + القيادة التنفيذيّة.</param>
/// <param name="IsProjectTeam">فريق تنفيذ العميل: مدير العميل، أو مدير حساب أحد مشاريعه، أو عضو في الفريق المسؤول.</param>
/// <param name="HasClientScopeAccess">
/// صلاحيّة عضويّة على العميل نفسه (مدير العميل أو نطاق تنظيميّ يراه). المالية تصل إلى مستندات أيّ عميل
/// بحكم وظيفتها على مستوى الشركة، لكنّها بلا هذه الصلاحيّة ⇒ لا ترى <c>ClientScoped</c> بل الماليّ فقط.
/// </param>
public sealed record DocumentAccessContext(
    Guid? UserId,
    IReadOnlyList<string> RoleNames,
    bool IsAdmin,
    bool IsManagement,
    bool IsFinance,
    bool IsHrManagement,
    bool IsProjectTeam,
    bool HasClientScopeAccess);

/// <summary>
/// المقيّم المركزيّ الوحيد لصلاحيّة المستندات (CPW-R2).
/// <para>
/// <b>قاعدة مُلزِمة:</b> لا يُوزَّع منطق الصلاحيّة داخل List/Get/Download — كلّها تمرّ من هنا.
/// الترتيب دائمًا: <b>صلاحيّة العميل أوّلًا</b> (تبقى في طبقة الخدمة) <b>ثمّ سياسة المستند</b> هنا.
/// المنع يعني «غير موجود» (<c>client_document.not_found</c>) لا «ممنوع» — منعًا للتعداد.
/// </para>
/// </summary>
public interface IDocumentAccessEvaluator
{
    /// <summary>يبني سياق التقييم لعميل محدّد (استعلامات الفريق/المشاريع تُنفَّذ مرّة واحدة).</summary>
    Task<DocumentAccessContext> BuildContextAsync(Guid clientId, CancellationToken ct = default);

    /// <summary>يقيّم مستندًا محمَّلًا (يجب تضمين <c>AllowedRoles</c>/<c>AllowedUsers</c> للسياسات المخصّصة).</summary>
    DocumentAccess Evaluate(ClientDocument document, DocumentAccessContext context);

    /// <summary>شرط فلترة خادميّ قابل للترجمة إلى SQL — يُطبَّق داخل الاستعلام لا بعده.</summary>
    Expression<Func<ClientDocument, bool>> VisibleFilter(DocumentAccessContext context);
}
