using Reporting.Application.Common;

namespace Reporting.Application.Directory;

public interface IDirectoryService
{
    Task<IReadOnlyList<DirectoryUserDto>> ListUsersAsync(bool includeInactive, CancellationToken ct = default);
    Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TeamDto>> ListTeamsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JobRoleDto>> ListJobRolesAsync(bool activeOnly = false, CancellationToken ct = default);

    // ===== ملخّصات الهيكل التنظيمي (ORG-STRUCTURE-ADMIN-R1) — قراءة فقط مع عدّادات؛ تخضع لفلترة النطاق مثل القوائم العادية =====

    /// <summary>قائمة الفرق مع عدّاداتها (أعضاء/مشاريع/مشاريع نشطة/عدم تطابق إدارة الأعضاء) واسم الإدارة والقائد — لشاشة الفرق.</summary>
    Task<IReadOnlyList<TeamSummaryDto>> ListTeamSummariesAsync(CancellationToken ct = default);

    /// <summary>قائمة الإدارات مع فرقها وعدّاداتها (فرق/أعضاء/مشاريع) وعلم وجود مدير — لشاشة الإدارات وتفاصيلها.</summary>
    Task<IReadOnlyList<DepartmentSummaryDto>> ListDepartmentSummariesAsync(CancellationToken ct = default);

    /// <summary>ملخّص أثر نقل فريق إلى إدارة مستهدفة — قراءة فقط، يُعرَض قبل الحفظ. لا يغيّر شيئًا.</summary>
    Task<Result<TeamMoveImpactDto>> GetTeamMoveImpactAsync(Guid teamId, Guid targetDepartmentId, CancellationToken ct = default);

    // ===== دليل الموارد البشرية المخصّص (قراءة فقط لحزمة A) — على مستوى الشركة، بلا فلترة نطاق، محكوم بسياسة HrDirectoryRead =====
    // منفصل تمامًا عن الدليل العام أعلاه؛ لا يغيّر سلوكه ولا يستدعي ScopeResolver. لا يُكشف أيّ محتوى تقارير.

    /// <summary>قائمة الموظفين (على مستوى الشركة) لقوائم اختيار حزمة HR — مع علمَي IsSensitive/CanEdit. لا فلترة نطاق.
    /// <paramref name="actingIsAdmin"/> يجعل CanEdit=true حتى للحسابات الحسّاسة (Admin يعدّل الاسم/التنظيم؛ الأدوار/التعطيل/كلمة المرور تبقى في إدارة المستخدمين).</summary>
    Task<IReadOnlyList<HrDirectoryUserDto>> ListHrDirectoryUsersAsync(bool includeInactive, bool actingIsAdmin, CancellationToken ct = default);

    /// <summary>قائمة كل الإدارات لقوائم اختيار حزمة HR — بلا فلترة نطاق.</summary>
    Task<IReadOnlyList<DepartmentDto>> ListHrDirectoryDepartmentsAsync(CancellationToken ct = default);

    /// <summary>قائمة كل الفرق لقوائم اختيار حزمة HR — بلا فلترة نطاق.</summary>
    Task<IReadOnlyList<TeamDto>> ListHrDirectoryTeamsAsync(CancellationToken ct = default);

    /// <summary>قائمة المديرين المتاحين (المستخدمون النشطون فقط) لمنتقي المدير في النقل التنظيمي — بلا فلترة نطاق.
    /// استبعاد المستخدم نفسه ومنع العلاقة الدائرية يُفرضان نهائيًّا في طبقة الخدمة عند الحفظ.</summary>
    Task<IReadOnlyList<HrDirectoryUserDto>> ListHrDirectoryManagersAsync(bool actingIsAdmin, CancellationToken ct = default);

    /// <summary>قائمة المسمّيات الوظيفية مع عدّاد الموظفين وعدّاد القوالب المرتبطة واسم الإدارة — لشاشة الإدارة.</summary>
    Task<IReadOnlyList<JobRoleDetailDto>> ListJobRolesWithCountsAsync(CancellationToken ct = default);

    /// <summary>إنشاء مسمّى وظيفي جديد (يمنع تكرار الاسم العربي) — يسجّل Audit jobrole.created.</summary>
    Task<Result<JobRoleDetailDto>> CreateJobRoleAsync(CreateJobRoleRequest req, Guid actingUserId, CancellationToken ct = default);

    /// <summary>تعديل بيانات مسمّى وظيفي قائم (يمنع تكرار الاسم العربي) — يسجّل Audit jobrole.updated.</summary>
    Task<Result<JobRoleDetailDto>> UpdateJobRoleAsync(Guid jobRoleId, UpdateJobRoleRequest req, Guid actingUserId, CancellationToken ct = default);

    /// <summary>أرشفة/إعادة تفعيل مسمّى وظيفي (بلا حذف صلب) — يسجّل Audit jobrole.archived / jobrole.reactivated.</summary>
    Task<Result<JobRoleDetailDto>> SetJobRoleActiveAsync(Guid jobRoleId, bool isActive, Guid actingUserId, CancellationToken ct = default);

    /// <summary>مصفوفة الأدوار: نطاق الرؤية والصلاحيات لكل دور في النظام.</summary>
    IReadOnlyList<RoleAccessDto> GetRoleMatrix();

    /// <summary>تحديث أدوار مستخدم (إضافة/إزالة) مع حواجز أمان ضد قفل النظام.</summary>
    Task<Result> UpdateUserRolesAsync(Guid userId, IReadOnlyList<string> roles, Guid actingUserId, CancellationToken ct = default);

    /// <summary>إنشاء مستخدم جديد مع أدواره وانتمائه التنظيمي — للأدمن فقط.</summary>
    Task<Result<DirectoryUserDto>> CreateUserAsync(CreateUserRequest req, CancellationToken ct = default);

    /// <summary>تعديل بيانات مستخدم قائم — للأدمن فقط.</summary>
    Task<Result<DirectoryUserDto>> UpdateUserAsync(Guid userId, UpdateUserRequest req, Guid actingUserId, CancellationToken ct = default);

    /// <summary>
    /// تعديل المسمّى الوظيفي للموظف فقط (السطح المخصّص) — Admin/CeoSupport/HR/GM/CEO.
    /// لا يمسّ أي حقل آخر؛ يتحقّق من وجود المسمّى، ويسجّل Audit (القديم/الجديد/المنفّذ/الوقت).
    /// </summary>
    Task<Result<DirectoryUserDto>> UpdateUserJobRoleAsync(Guid userId, UpdateUserJobRoleRequest req, Guid actingUserId, CancellationToken ct = default);

    /// <summary>
    /// تعديل البيانات الأساسية غير الحسّاسة للموظف (الاسم الكامل فقط) — Admin/CeoSupport/HR.
    /// لا يمسّ البريد/الأدوار/التفعيل/كلمة المرور/الانتماء التنظيمي؛ يسجّل Audit user.basic.updated.
    /// حاجز الحساب الحسّاس يُطبَّق على غير الأدمن فقط: HR/CeoSupport ممنوعون من تعديل Admin/CEO/GM/CeoSupport (403)،
    /// بينما <paramref name="actingIsAdmin"/>=true يسمح للأدمن بتعديل الاسم لأيّ حساب (الأدوار/التعطيل/كلمة المرور تبقى خارج هذا السطح).
    /// </summary>
    Task<Result<DirectoryUserDto>> UpdateUserBasicAsync(Guid userId, UpdateUserBasicRequest req, Guid actingUserId, bool actingIsAdmin, CancellationToken ct = default);

    /// <summary>
    /// تعديل الانتماء التنظيمي للموظف (الإدارة/الفريق/المدير) عند نقل تنظيمي — Admin/CeoSupport/HR/GM/CEO.
    /// قيود أمان صارمة: لا تعطيل، لا تغيير ذاتي، لا مدير غير نشط، لا علاقة دائرية.
    /// حاجز الحساب الحسّاس يُطبَّق على غير الأدمن فقط: HR/CeoSupport/GM/CEO ممنوعون من تعديل Admin/CEO/GM/CeoSupport (403)،
    /// بينما <paramref name="actingIsAdmin"/>=true يسمح للأدمن بالنقل التنظيمي لأيّ حساب (الأدوار/التعطيل/كلمة المرور تبقى خارج هذا السطح).
    /// لا يمسّ الاسم/البريد/الأدوار/التفعيل/كلمة المرور؛ يسجّل Audit user.org.changed (القديم/الجديد/المنفّذ/الوقت/الملاحظة).
    /// </summary>
    Task<Result<DirectoryUserDto>> UpdateUserOrgAssignmentAsync(Guid userId, UpdateUserOrgAssignmentRequest req, Guid actingUserId, bool actingIsAdmin, CancellationToken ct = default);

    /// <summary>حذف مستخدم من النظام مع حواجز أمان (لا حذف ذاتي ولا آخر أدمن).</summary>
    Task<Result> DeleteUserAsync(Guid userId, Guid actingUserId, CancellationToken ct = default);

    /// <summary>
    /// إعادة تعيين كلمة مرور مستخدم بواسطة جهة مخوّلة (Admin أو CeoSupport) — تستخدم Identity حصرًا،
    /// لا تُرجع/تطبع كلمة المرور، وتُبطل التوكنات النشطة وتُسجّل العملية في Audit.
    /// لا يُعاد تعيين حساب Admin إلا بواسطة Admin، ولا يُعاد تعيين آخر أدمن نشط.
    /// </summary>
    Task<Result> ResetUserPasswordAsync(Guid userId, string newPassword, Guid actingUserId, bool actorIsAdmin, CancellationToken ct = default);

    /// <summary>إضافة عضو إلى فريق (يضبط الفريق والإدارة تلقائيًا).</summary>
    Task<Result> AddTeamMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default);

    /// <summary>إزالة عضو من فريق (يفرّغ ربط الفريق).</summary>
    Task<Result> RemoveTeamMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default);

    // ===== عضويات الفريق الإضافية (MULTI-TEAM-MEMBERSHIP-MVP-R1) — منفصلة عن AddTeamMemberAsync تمامًا =====
    // لا تغيّر أيّ حقل تنظيمي على المستخدم (TeamId/DepartmentId/ManagerId/JobRoleId)؛ لا تدخل ScopeResolver/KPI/التقارير.

    /// <summary>أعضاء الفريق مفصولين: الأساسيون (TeamId) والإضافيون (جدول العضويات النشطة) — للأدمن فقط.</summary>
    Task<Result<TeamMembershipsDto>> ListTeamMembershipsAsync(Guid teamId, CancellationToken ct = default);

    /// <summary>
    /// إضافة عضو إضافي إلى فريق (ثانوي) دون نقله من فريقه الأساسي ودون تغيير أيّ حقل تنظيمي — للأدمن فقط.
    /// يرفض: المستخدم/الفريق غير موجود، العضو أساسي في نفس الفريق، عضوية إضافية نشطة قائمة، الحسابات الحسّاسة.
    /// إن وُجدت عضوية غير نشطة لنفس الثنائية تُعاد تفعيلها بدل التكرار.
    /// </summary>
    Task<Result<TeamMemberDto>> AddAdditionalTeamMemberAsync(Guid teamId, AddAdditionalMemberRequest req, Guid actingUserId, CancellationToken ct = default);

    /// <summary>إزالة/إلغاء تفعيل عضوية إضافية لمستخدم في فريق — للأدمن فقط. لا يمسّ TeamId الأساسي.</summary>
    Task<Result> RemoveAdditionalTeamMemberAsync(Guid teamId, Guid userId, Guid actingUserId, CancellationToken ct = default);

    /// <summary>عضويات المستخدم: فريقه الأساسي وفرقه الإضافية النشطة — للأدمن فقط.</summary>
    Task<Result<UserTeamMembershipsDto>> ListUserTeamMembershipsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>إنشاء فريق جديد — للأدمن فقط.</summary>
    Task<Result<TeamDto>> CreateTeamAsync(CreateTeamRequest req, CancellationToken ct = default);

    /// <summary>
    /// تعديل بيانات فريق قائم (الاسم/القائد/التفعيل/نقل الإدارة) — Admin/CEO/GM ضمن النطاق.
    /// عند تغيّر الإدارة و<c>SyncMemberDepartments</c>=true تُزامَن <c>DepartmentId</c> لأعضاء الفريق الحاليين؛
    /// يسجّل Audit team.updated / team.moved (مع عدد الأعضاء المُزامَنين).
    /// </summary>
    Task<Result<TeamDto>> UpdateTeamAsync(Guid teamId, UpdateTeamRequest req, Guid actingUserId, CancellationToken ct = default);

    /// <summary>
    /// حذف فريق — محروس: يُمنع الحذف إن كان للفريق أعضاء أو مشاريع يملكها (OwnerTeamId) → 409.
    /// يسجّل Audit team.deleted. الأرشفة (IsActive=false) هي البديل الموصى به للفريق المستخدَم.
    /// </summary>
    Task<Result> DeleteTeamAsync(Guid teamId, Guid actingUserId, CancellationToken ct = default);

    /// <summary>إنشاء إدارة جديدة — للأدمن فقط.</summary>
    Task<Result<DepartmentDto>> CreateDepartmentAsync(CreateDepartmentRequest req, CancellationToken ct = default);

    /// <summary>تعديل بيانات إدارة قائمة — للأدمن فقط.</summary>
    Task<Result<DepartmentDto>> UpdateDepartmentAsync(Guid departmentId, UpdateDepartmentRequest req, CancellationToken ct = default);

    /// <summary>حذف إدارة (يمنع الحذف إذا كانت بها فرق) — للأدمن فقط.</summary>
    Task<Result> DeleteDepartmentAsync(Guid departmentId, CancellationToken ct = default);
}
