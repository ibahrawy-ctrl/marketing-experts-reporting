using Reporting.Application.Common;

namespace Reporting.Application.Directory;

public record DirectoryUserDto(
    Guid Id,
    string FullName,
    string Email,
    bool IsActive,
    IReadOnlyList<string> Roles,
    Guid? DepartmentId,
    Guid? TeamId,
    Guid? ManagerId,
    Guid? JobRoleId = null);

/// <summary>
/// صف في «دليل الموارد البشرية» المخصّص (قراءة فقط لقوائم اختيار حزمة A) — منفصل عن DirectoryUserDto العام.
/// يحمل فقط البيانات اللازمة للعرض والاختيار + علمين أمنيين:
/// <c>IsSensitive</c> = حساب إداري/تنفيذي حسّاس (Admin/CEO/GM/CeoSupport)،
/// <c>CanEdit</c> = هل يجوز تعديله عبر سطح HR (=ليس حسّاسًا). لا يُكشف أيّ محتوى تقارير.
/// </summary>
public record HrDirectoryUserDto(
    Guid Id,
    string FullName,
    string Email,
    bool IsActive,
    Guid? DepartmentId,
    Guid? TeamId,
    Guid? ManagerId,
    Guid? JobRoleId,
    bool IsSensitive,
    bool CanEdit);

public record DepartmentDto(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? Code,
    Guid? ManagerId,
    bool IsActive);

public record TeamDto(
    Guid Id,
    string NameAr,
    string? NameEn,
    Guid DepartmentId,
    Guid? TeamLeaderId,
    bool IsActive);

public record JobRoleDto(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? Code,
    Guid? DepartmentId,
    bool IsActive);

/// <summary>
/// المسمّى الوظيفي مع عدّادات الاستخدام (عدد الموظفين المرتبطين + عدد القوالب المرتبطة) واسم الإدارة الافتراضية —
/// لشاشة «إدارة المسمّيات الوظيفية».
/// </summary>
public record JobRoleDetailDto(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? Code,
    Guid? DepartmentId,
    string? DepartmentName,
    bool IsActive,
    int EmployeeCount,
    int TemplateCount);

/// <summary>إنشاء مسمّى وظيفي جديد — Admin/CeoSupport/HR/GM/CEO.</summary>
public record CreateJobRoleRequest(string NameAr, string? NameEn, string? Code, Guid? DepartmentId);

/// <summary>تعديل بيانات مسمّى وظيفي قائم — Admin/CeoSupport/HR/GM/CEO. (التفعيل/الأرشفة عبر مسار منفصل.)</summary>
public record UpdateJobRoleRequest(string NameAr, string? NameEn, string? Code, Guid? DepartmentId);

/// <summary>صف في مصفوفة الأدوار: يصف نطاق الرؤية والصلاحيات لكل دور.</summary>
/// <remarks>
/// <c>Permissions</c>/<c>PermissionLabelsAr</c> = نموذج العرض القديم (9 أعلام) مُبقًى للتوافق.
/// <c>CapabilityGroups</c> = مصفوفة القدرات المجمّعة الجديدة (Phase A) التي تعكس الحقيقة الكاملة بحالات.
/// </remarks>
public record RoleAccessDto(
    string Role,
    string RoleLabelAr,
    string ScopeType,
    string ScopeDescriptionAr,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> PermissionLabelsAr,
    IReadOnlyList<RoleCapabilityGroup> CapabilityGroups);

public record UpdateUserRolesRequest(IReadOnlyList<string> Roles);

/// <summary>إنشاء مستخدم جديد في النظام — للأدمن فقط.</summary>
public record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    IReadOnlyList<string> Roles,
    Guid? DepartmentId,
    Guid? TeamId,
    Guid? ManagerId);

/// <summary>تعديل بيانات مستخدم قائم (الاسم/البريد/التفعيل/الانتماء التنظيمي) — للأدمن فقط.</summary>
public record UpdateUserRequest(
    string FullName,
    string Email,
    bool IsActive,
    Guid? DepartmentId,
    Guid? TeamId,
    Guid? ManagerId);

/// <summary>
/// تعديل المسمّى الوظيفي للموظف فقط (السطح المخصّص) — Admin/CeoSupport/HR/GM/CEO.
/// لا يمسّ الاسم/البريد/الأدوار/التفعيل/كلمة المرور. JobRoleId=null لإزالة المسمّى.
/// </summary>
public record UpdateUserJobRoleRequest(Guid? JobRoleId, string? Notes = null);

/// <summary>
/// تعديل البيانات الأساسية غير الحسّاسة للموظف (الاسم الكامل فقط) عبر سطح HR المخصّص — Admin/CeoSupport/HR.
/// لا يمسّ البريد/الأدوار/الصلاحيات/التفعيل/كلمة المرور/الانتماء التنظيمي.
/// </summary>
public record UpdateUserBasicRequest(string FullName, string? Notes = null);

/// <summary>
/// تعديل الانتماء التنظيمي للموظف (الإدارة/الفريق/المدير) عند نقل تنظيمي — Admin/CeoSupport/HR/GM/CEO.
/// لا يمسّ الاسم/البريد/الأدوار/التفعيل/كلمة المرور/المسمّى الوظيفي. القيم null لإزالة الانتماء المعني.
/// </summary>
public record UpdateUserOrgAssignmentRequest(Guid? DepartmentId, Guid? TeamId, Guid? ManagerId, string? Notes = null);

/// <summary>إعادة تعيين كلمة مرور مستخدم — Admin/CeoSupport فقط. لا تُخزَّن ولا تُسجَّل.</summary>
public record ResetPasswordRequest(string NewPassword);

/// <summary>إضافة/إزالة عضو من فريق.</summary>
public record TeamMemberRequest(Guid UserId);

// ===== عضويات الفريق الإضافية (MULTI-TEAM-MEMBERSHIP-MVP-R1) — منفصلة تمامًا عن TeamId الأساسي =====

/// <summary>عضو في فريق مع تمييز نوع العضوية (أساسي من TeamId أو إضافي من جدول العضويات).</summary>
/// <remarks><c>MembershipRowId</c> = معرّف صفّ العضوية الإضافية (للإزالة) — null للأعضاء الأساسيين.</remarks>
public record TeamMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    bool IsActive,
    Guid? DepartmentId,
    Guid? JobRoleId,
    bool IsPrimary,
    Guid? MembershipRowId,
    bool MembershipIsActive,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    string? Notes);

/// <summary>أعضاء فريق مفصولين: الأساسيون (TeamId) والإضافيون (جدول العضويات).</summary>
public record TeamMembershipsDto(
    Guid TeamId,
    string TeamNameAr,
    Guid DepartmentId,
    IReadOnlyList<TeamMemberDto> PrimaryMembers,
    IReadOnlyList<TeamMemberDto> AdditionalMembers);

/// <summary>عضوية فريق إضافية واحدة من منظور المستخدم (لشاشة المستخدم).</summary>
public record UserTeamMembershipDto(
    Guid MembershipRowId,
    Guid TeamId,
    string TeamNameAr,
    Guid DepartmentId,
    string? DepartmentName,
    bool IsActive,
    string MembershipType,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    string? Notes);

/// <summary>عضويات المستخدم: فريقه الأساسي (TeamId) وفرقه الإضافية.</summary>
public record UserTeamMembershipsDto(
    Guid UserId,
    string FullName,
    Guid? PrimaryTeamId,
    string? PrimaryTeamNameAr,
    IReadOnlyList<UserTeamMembershipDto> AdditionalMemberships);

/// <summary>إضافة عضو إضافي إلى فريق — لا يغيّر أي حقل تنظيمي على المستخدم.</summary>
public record AddAdditionalMemberRequest(Guid UserId, string? Notes = null, DateTime? StartDateUtc = null, DateTime? EndDateUtc = null);

/// <summary>إنشاء فريق جديد — للأدمن فقط.</summary>
public record CreateTeamRequest(
    string NameAr,
    string? NameEn,
    Guid DepartmentId,
    Guid? TeamLeaderId);

/// <summary>
/// تعديل بيانات فريق قائم — للأدمن/CEO/GM ضمن النطاق.
/// <c>SyncMemberDepartments</c> (افتراضي true): عند نقل الفريق إلى إدارة جديدة، يُحدَّث <c>User.DepartmentId</c>
/// لأعضاء الفريق الحاليين (<c>TeamId == team.Id</c>) إلى الإدارة الجديدة فقط — لا يمسّ المسمّى/المدير/الفريق
/// ولا أيّ مستخدم خارج الفريق. الغرض: منع تكوّن عدم تطابق الإدارة (كما حدث على الإنتاج: 11 حالة).
/// </summary>
public record UpdateTeamRequest(
    string NameAr,
    string? NameEn,
    Guid DepartmentId,
    Guid? TeamLeaderId,
    bool IsActive,
    bool SyncMemberDepartments = true);

/// <summary>
/// ملخّص فريق مع عدّاداته — لشاشة «الفرق» وتفاصيل الإدارة. قراءة فقط.
/// <c>MemberDepartmentMismatchCount</c> = عدد الأعضاء الذين تختلف <c>DepartmentId</c> لديهم عن إدارة الفريق
/// (إشارة لوجوب المزامنة). <c>ActiveProjectsCount</c> ⊆ <c>ProjectsCount</c>.
/// </summary>
public record TeamSummaryDto(
    Guid Id,
    string NameAr,
    string? NameEn,
    Guid DepartmentId,
    string? DepartmentName,
    Guid? TeamLeaderId,
    string? TeamLeaderName,
    bool IsActive,
    int MemberCount,
    int ProjectsCount,
    int ActiveProjectsCount,
    int MemberDepartmentMismatchCount,
    int PrimaryMemberCount,
    int AdditionalMemberCount);

/// <summary>
/// ملخّص إدارة مع فرقها وعدّاداتها — لشاشة «الإدارات» وتفاصيلها. قراءة فقط.
/// <c>HasManager</c> = هل للإدارة مدير معيَّن (لإظهار تحذير «بلا مدير»).
/// </summary>
public record DepartmentSummaryDto(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? Code,
    Guid? ManagerId,
    string? ManagerName,
    bool HasManager,
    bool IsActive,
    int TeamCount,
    int MemberCount,
    int ProjectsCount,
    IReadOnlyList<TeamSummaryDto> Teams);

/// <summary>
/// ملخّص أثر نقل فريق إلى إدارة جديدة — يُعرَض للأدمن قبل الحفظ. قراءة فقط، لا يغيّر شيئًا.
/// <c>WillSyncMembers</c> = هل ستُحدَّث <c>DepartmentId</c> لأعضاء الفريق (نقل فعليّ + مزامنة مطلوبة).
/// <c>Warnings</c> = رسائل تنبيه عربية (بلا قائد فريق، أعضاء غير متطابقين، مشاريع نشطة… إلخ).
/// </summary>
public record TeamMoveImpactDto(
    Guid TeamId,
    string TeamName,
    Guid CurrentDepartmentId,
    string? CurrentDepartmentName,
    Guid TargetDepartmentId,
    string? TargetDepartmentName,
    bool IsDepartmentChange,
    Guid? TeamLeaderId,
    string? TeamLeaderName,
    int MemberCount,
    int ProjectsCount,
    int ActiveProjectsCount,
    int SubmissionsCount,
    int MemberDepartmentMismatchCount,
    bool WillSyncMembers,
    IReadOnlyList<string> Warnings);

/// <summary>إنشاء إدارة جديدة — للأدمن فقط.</summary>
public record CreateDepartmentRequest(
    string NameAr,
    string? NameEn,
    string? Code,
    Guid? ManagerId);

/// <summary>تعديل بيانات إدارة قائمة — للأدمن فقط.</summary>
public record UpdateDepartmentRequest(
    string NameAr,
    string? NameEn,
    string? Code,
    Guid? ManagerId,
    bool IsActive);
