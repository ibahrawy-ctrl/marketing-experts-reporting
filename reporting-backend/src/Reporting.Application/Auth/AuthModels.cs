namespace Reporting.Application.Auth;

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ChangeEmailRequest(string NewEmail, string CurrentPassword);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresUtc,
    Guid UserId,
    string FullName,
    string Email,
    IReadOnlyCollection<string> Roles,
    // الدورية المتوقَّعة لتقارير هذا المستخدم (يومي لمندوبي المبيعات، أسبوعي لغيرهم).
    string ExpectedReportCadence,
    // رمز المسمّى الوظيفي (مثل SALES_B2C) — لتحديد لوحات المبيعات وعناصر التنقّل بالواجهة (null إن لم يُسنَد).
    string? JobRoleCode = null,
    // P3-NAV-001 — قدرات المستخدم نفسه (انظر MeResponse).
    IReadOnlyCollection<string>? Permissions = null,
    string? ScopeType = null,
    // P123-R1 — الميزات المفتوحة في هذه البيئة (انظر MeResponse).
    IReadOnlyCollection<string>? Features = null);

public record MeResponse(
    Guid UserId,
    string FullName,
    string Email,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    // الدورية المتوقَّعة لتقارير هذا المستخدم (يومي لمندوبي المبيعات، أسبوعي لغيرهم) — تُعرض كقيمة ثابتة بالواجهة.
    string ExpectedReportCadence,
    // رمز المسمّى الوظيفي (مثل SALES_B2C) — لتحديد لوحات المبيعات وعناصر التنقّل بالواجهة (null إن لم يُسنَد).
    string? JobRoleCode = null,
    // P3-NAV-001 — **قدرات المستخدم نفسه**: مفاتيح `perm` المُسنَدة إليه صراحةً في Identity.
    // انعكاس قراءة فقط لما يحمله المستخدم بالفعل داخل رمزه (JWT) — لا يمنح شيئًا ولا يوسّع وصولًا،
    // والخادم يبقى المُنفِّذ الوحيد للتخويل. الغرض: تجعل الواجهة تُخفي ما لا يملكه المستخدم بدل
    // أن تُظهر سطحًا يردّ عليه الخادم 403، وبدل أن تُخمّن الملكيّة من قائمة أدوار ثابتة تكذب.
    IReadOnlyCollection<string>? Permissions = null,
    // نوع نطاق رؤية المستخدم كما يحسبه الخادم (own/team/department/company/governance).
    // للعرض والتوجيه السياقيّ فقط (وضع الذات)؛ الفرز الفعليّ خادميّ عبر IScopeResolver.
    string? ScopeType = null,
    // P123-R1 — **الميزات المفتوحة في هذه البيئة** كما يقرّرها إعداد الخادم (AppFeatures).
    // مكمّلة لـPermissions لا بديلة عنها: الأولى تقول «هل السطح مفتوح أصلًا؟» والثانية «هل يملكه
    // هذا المستخدم؟». بغياب الأولى كانت الواجهة تعرض رابطًا يردّ عليه الخادم 404 حتمًا، فيقرأه
    // المستخدم «خطأ» بينما هو إغلاق متعمَّد. لا تمنح شيئًا: الخادم يبقى المُنفِّذ الوحيد للتخويل.
    IReadOnlyCollection<string>? Features = null);
