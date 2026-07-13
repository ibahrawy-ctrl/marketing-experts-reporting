namespace Reporting.Application.Common;

/// <summary>أدوار النظام — تُدار عبر جداول ASP.NET Identity (لا عمود Role في المستخدمين).</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Ceo = "CEO";
    public const string GeneralManager = "GeneralManager";
    public const string Manager = "Manager";
    public const string TeamLeader = "TeamLeader";
    public const string Employee = "Employee";
    public const string CeoSupport = "CeoSupport";
    public const string Viewer = "Viewer";
    // دور الموارد البشرية الرسمي (V1.0.1-A) — المعتمِد النهائي الطبيعي لطلبات الإجازة/الاستئذان.
    // ليس دورًا إداريًّا (ليس ضمن Management) ولا يملك صلاحيات الأدمن؛ سلطته مقصورة على الاعتماد النهائي للإجازات.
    public const string Hr = "HR";
    // الأدوار المالية (FIN-R1) — صلاحيات قراءة/إدارة قراءةً ماليّةً فقط على FIN-L1 وتصدير KPI للمالية.
    // ليست أدوارًا إدارية (ليست ضمن Management) ولا تملك أيّ قدرة صرف/خصم/تعديل راتب أو workflow؛
    // نطاقها الافتراضي "own" (RoleAccess) ولا ترى تجميعات أعلى إلا عبر السياسات المالية الصريحة أدناه.
    public const string FinanceManager = "FinanceManager"; // المدير المالي
    public const string Accountant = "Accountant";         // الحسابات
    // صلاحية «محفظة مدير الحساب» (ACCOUNT-MANAGER-PORTFOLIO) — رؤية فقط لمشاريع/عملاء مدير الحساب نفسه.
    // مستقلّة تمامًا عن المسمّى الوظيفي (JobRole) وعن شجرة الإدارة؛ لا تمنح إدارة/اعتماد/إنشاء/حذف،
    // ولا ترى تقارير المسودّة/المُعادة ولا KPI ولا تقييمات الموظفين. تُسنَد لمن تقرّره الإدارة (مثل مديري الحسابات).
    public const string AccountPortfolioReader = "AccountPortfolioReader";

    public static readonly string[] All =
    {
        Admin, Ceo, GeneralManager, Manager, TeamLeader, Employee, CeoSupport, Viewer, Hr,
        FinanceManager, Accountant, AccountPortfolioReader
    };

    /// <summary>الأدوار الإدارية التي ترى تجميعات أعلى من المستخدم نفسه.</summary>
    public static readonly string[] Management =
    {
        Admin, Ceo, GeneralManager, Manager, TeamLeader
    };

    /// <summary>
    /// الأدوار المخوّلة بإدارة عضوية الفرق (تعديل الاسم/القائد، إضافة/إزالة عضو):
    /// المستوى الإداري الأعلى فقط — Admin / CEO / GM. عمدًا لا تشمل Manager أو TeamLeader.
    /// TODO: عند تعريف أدوار HR / HR Manager / HR Officer / المساعد الإداري (Administrative Assistant)
    /// تُضاف هنا — ليست معرّفة في النظام حاليًا (لا تفتح Phase 4 لإضافتها).
    /// </summary>
    public static readonly string[] TeamManagement =
    {
        Admin, Ceo, GeneralManager
    };

    /// <summary>
    /// الأدوار المخوّلة بحوكمة القوالب ومؤشرات الأداء (إنشاء/تعديل قوالب التقارير وKPI،
    /// إدارة الإصدارات والأوزان والربط بالأدوار، وإعدادات الأداء):
    /// المستوى الإداري الأعلى فقط — Admin / CEO / GM. عمدًا لا تشمل Manager أو TeamLeader أو Employee.
    /// إدارة القوالب التفصيلية تعيش في منطقة Admin/Governance؛ CEO/GM يَطّلعون على النتائج أساسًا.
    /// TODO: عند تعريف أدوار HR / المساعد الإداري (Administrative Assistant) تُضاف هنا — غير معرّفة بالنظام حاليًا.
    /// </summary>
    public static readonly string[] TemplateGovernance =
    {
        Admin, Ceo, GeneralManager
    };

    /// <summary>
    /// الأدوار المخوّلة بالاعتماد النهائي لطلبات الإجازة/الاستئذان (V1.0.1-A — قدرة LeaveFinalApproval).
    /// HR هو المعتمِد النهائي الطبيعي؛ يبقى Admin / CEO / GM كصلاحية تدخّل/override حسب السياسة.
    /// ملاحظة: الاعتماد النهائي لطلب HR الشخصي نفسه لا يتم عبر هذه المجموعة بل عبر مسار خاص
    /// (المدير العام يراجع ثم CEO/Admin يعتمد) — يُفرَض في طبقة الخدمة.
    /// </summary>
    public static readonly string[] LeaveFinalApprovers =
    {
        Hr, Admin, Ceo, GeneralManager
    };

    /// <summary>
    /// الأدوار التي ترى طابور «بانتظار قراري» وتراجع طلبات الإجازة (اتحاد الإدارة + الموارد البشرية).
    /// الفرض النهائي للنطاق والخطوة في طبقة الخدمة؛ هذه طبقة دفاع أولى عند نقطة النهاية.
    /// </summary>
    public static readonly string[] LeaveReviewers =
    {
        Admin, Ceo, GeneralManager, Manager, TeamLeader, Hr
    };

    /// <summary>
    /// الأدوار المخوّلة بتعديل المسمّى الوظيفي للموظف (JobRoleId) عبر السطح المخصّص فقط:
    /// Admin / CeoSupport / HR / GM / CEO. لا تشمل Manager أو TeamLeader أو Employee.
    /// مستقلة تمامًا عن صلاحية إعادة تعيين كلمة المرور (UserPasswordReset) وعن AdminOnly لبقية إدارة المستخدم.
    /// </summary>
    public static readonly string[] UserJobRoleManagers =
    {
        Admin, CeoSupport, Hr, GeneralManager, Ceo
    };

    /// <summary>
    /// الأدوار المخوّلة برؤية/إدارة أرصدة الإجازات والأذونات (V1.1 — خدمات الموظف):
    /// Admin / CEO / GM / CeoSupport / HR. لا تشمل Manager أو TeamLeader أو Employee.
    /// COO (غير معرّف بعد) يُضاف هنا عند تعريفه؛ مؤقتًا CEO/GM يغطّيان سلطته.
    /// </summary>
    public static readonly string[] BalanceManagers =
    {
        Admin, Ceo, GeneralManager, CeoSupport, Hr
    };

    /// <summary>
    /// الأدوار المخوّلة بمعالجة طلبات الموارد البشرية العامة (V1.1 — خدمات الموظف):
    /// HR / Admin / CEO / GM / CeoSupport. لا تشمل Manager أو TeamLeader أو Employee.
    /// </summary>
    public static readonly string[] HrRequestManagers =
    {
        Hr, Admin, Ceo, GeneralManager, CeoSupport
    };

    /// <summary>
    /// الأدوار المخوّلة بتعديل البيانات الأساسية غير الحسّاسة للموظف (الاسم الكامل فقط حاليًا) عبر سطح HR المخصّص:
    /// Admin / CeoSupport / HR. لا تشمل تعديل الأدوار/الصلاحيات/كلمة المرور/التفعيل/البريد/إنشاء/حذف المستخدم.
    /// مستقلة تمامًا عن AdminOnly (إدارة المستخدم الكاملة) وعن UserPasswordReset.
    /// </summary>
    public static readonly string[] UserBasicManagers =
    {
        Admin, CeoSupport, Hr
    };

    /// <summary>
    /// الأدوار المخوّلة بتعديل الانتماء التنظيمي للموظف (الإدارة/الفريق/المدير) عند الضرورة لنقل تنظيمي:
    /// Admin / CeoSupport / HR / GM / CEO. لا تشمل Manager أو TeamLeader أو Employee.
    /// محكومة بقيود أمان صارمة في طبقة الخدمة (لا تمسّ حسابات Admin/CEO/GM/CeoSupport، لا تعطيل، لا تغيير ذاتي،
    /// لا مدير غير نشط، لا علاقة دائرية)، ولا تمنح أيّ قدرة أخرى على إدارة المستخدم.
    /// </summary>
    public static readonly string[] UserOrgAssigners =
    {
        Admin, CeoSupport, Hr, GeneralManager, Ceo
    };

    /// <summary>
    /// الأدوار المخوّلة برؤية شاشة «متابعة التزام التسليم» (per-person، عرض فقط بلا أيّ محتوى للتقرير):
    /// الأدوار الإدارية + دعم الرئيس + مُطّلِع + الموارد البشرية (HR). HR يرى الالتزام على مستوى الشركة
    /// (من سلّم/من تأخّر/الحالة) بلا قدرة فتح/قراءة/اعتماد أيّ تقرير. توسعة رؤية الالتزام فقط لا التفويض.
    /// </summary>
    public static readonly string[] CompletionMonitors =
    {
        Admin, Ceo, GeneralManager, Manager, TeamLeader, CeoSupport, Viewer, Hr
    };

    /// <summary>
    /// الأدوار المخوّلة بقراءة «دليل الموارد البشرية» المخصّص (قوائم اختيار الموظفين/الإدارات/الفرق/المديرين لحزمة HR):
    /// Admin / CeoSupport / HR / GM / CEO — اتحاد مَن يستطيع تعديل البيانات الأساسية أو الانتماء التنظيمي.
    /// قراءة فقط على مستوى الشركة لأغراض حزمة A (تعديل الاسم/النقل التنظيمي/متابعة الالتزام)؛ منفصلة تمامًا
    /// عن الدليل العام (/directory/users وأخواته) ولا تغيّر سلوكه ولا تمنح أيّ قدرة كتابة أو رؤية محتوى تقارير.
    /// الحسابات الحسّاسة (Admin/CEO/GM/CeoSupport) تظهر كـ«غير قابلة للتعديل» ويمنعها الخادم فعليًّا.
    /// </summary>
    public static readonly string[] HrDirectoryReaders =
    {
        Admin, CeoSupport, Hr, GeneralManager, Ceo
    };

    /// <summary>
    /// الأدوار المخوّلة بقراءة «عرض التأثير على الرواتب» (FIN-L1 — طلبات الإجازة/الاستئذان المؤثّرة على الراتب):
    /// Admin / CEO / GM / HR / CeoSupport. عرض على مستوى الشركة (بلا ScopeResolver)، إعلامي بحت بلا أيّ تعديل
    /// على الطلب الأصلي ولا خصم آليّ. لا تشمل Manager / TeamLeader / Employee / Viewer.
    /// </summary>
    public static readonly string[] PayrollImpactReaders =
    {
        Admin, Ceo, GeneralManager, Hr, CeoSupport, FinanceManager, Accountant
    };

    /// <summary>
    /// الأدوار المخوّلة بتحديث حالة المراجعة المالية لطلب مؤثّر على الراتب (FIN-L1 — قرار المراجعة فقط):
    /// Admin / HR فقط. يغيّر حالة المراجعة المالية والملاحظة لا غير؛ لا يمسّ الطلب الأصلي ولا حالته ولا الراتب.
    /// CEO / GM / CeoSupport يقرؤون فقط (ليسوا هنا).
    /// </summary>
    public static readonly string[] PayrollImpactManagers =
    {
        Admin, Hr, FinanceManager, Accountant
    };

    /// <summary>
    /// الأدوار المخوّلة بتصدير تقييمات KPI المعتمدة للمالية (KPI-FIN1 — عرض/تصدير قراءة فقط):
    /// Admin / CEO / GM / HR / CeoSupport. تصدير على مستوى الشركة (بلا ScopeResolver)، إعلامي بحت لا يحسب
    /// أو يصرف أيّ مستحقات ولا يغيّر أيّ تقييم. لا تشمل Manager / TeamLeader / Employee / Viewer.
    /// </summary>
    public static readonly string[] KpiFinanceExporters =
    {
        Admin, Ceo, GeneralManager, Hr, CeoSupport, FinanceManager, Accountant
    };

    /// <summary>
    /// الأدوار المخوّلة بإدارة المستخدمين الكاملة (إنشاء/تعديل/تعطيل/حذف المستخدم وتعديل أدواره) عبر صفحة «إدارة فريق العمل» (GOV-R1):
    /// Admin / CEO فقط. عمدًا لا تشمل GM / HR / Manager / FinanceManager / Accountant / Employee / CeoSupport.
    /// منفصلة عن AdminOnly العامة (التي تبقى Admin-only لبقية الموارد) وعن UserPasswordReset. الحُرّاس الأمنية في الخدمة
    /// (self-lockout / آخر مدير نظام / حساب Admin لا يُعاد ضبط كلمته إلا بفاعل Admin) لم تتغيّر وتنطبق على CEO أيضًا.
    /// </summary>
    public static readonly string[] UserManagers =
    {
        Admin, Ceo
    };

    /// <summary>
    /// الأدوار المخوّلة برؤية «محفظة مدير الحساب» (مشاريعي/عملائي — عرض فقط):
    /// AccountPortfolioReader (الصلاحية المخصّصة) + Admin (لأغراض التشغيل/التحقّق فقط).
    /// لا تمنح أيّ قدرة إدارة/اعتماد/إنشاء/حذف؛ الرؤية مقصورة خادمًا على مشاريع المستخدم نفسه
    /// (Project.AccountManagerId == المستخدم الحالي) وعملائها المشتقّين منها — لا توسعة عبر
    /// منح الرؤية أو عضوية الفرق أو المسمّى الوظيفي.
    /// </summary>
    public static readonly string[] AccountPortfolioReaders =
    {
        AccountPortfolioReader, Admin
    };

    /// <summary>
    /// الأدوار المخوّلة بالوصول إلى «ورشة الحوكمة» (GOV-GOVERNANCE-UX1 — تسجيل/متابعة بنود الحوكمة العامة):
    /// Admin / CEO / GM / CeoSupport (رؤية واسعة) + Manager / TeamLeader (نطاقهم فقط) + HR (محدود: المُسنَد إليه/المُنشأ
    /// بواسطته/ما يخصّه أو إدارته). Employee / Viewer لا وصول لهم. الفرز التفصيلي (واسع/نطاق/HR محدود) يُفرَض في الخدمة.
    /// </summary>
    public static readonly string[] GovernanceWorkspaceUsers =
    {
        Admin, Ceo, GeneralManager, CeoSupport, Manager, TeamLeader, Hr
    };

    /// <summary>
    /// الأدوار ذات الرؤية الواسعة على كل بنود الحوكمة العامة (بلا تقييد نطاق): Admin / CEO / GM / CeoSupport.
    /// Manager / TeamLeader يُقيَّدون بنطاقهم عبر ScopeResolver، وHR يُقيَّد بالبنود المتعلّقة به/بإدارته.
    /// </summary>
    public static readonly string[] GovernanceWorkspaceWideViewers =
    {
        Admin, Ceo, GeneralManager, CeoSupport
    };

    /// <summary>
    /// الأدوار المخوّلة بتغيير الحالة/الإغلاق لأيّ بند حوكمة بغضّ النظر عن مالكه: Admin / CEO / GM.
    /// عداهم (CeoSupport/Manager/TeamLeader/HR) يستطيع التغيير فقط إن كان منشئ البند أو المُسنَد إليه (يُفرَض في الخدمة).
    /// </summary>
    public static readonly string[] GovernanceWorkspaceManagers =
    {
        Admin, Ceo, GeneralManager
    };

    /// <summary>
    /// الأدوار المخوّلة بالوصول إلى «التصعيد الفردي» (GOV-INDIVIDUAL-ESCALATION1 — كيان مستقلّ):
    /// Admin / CEO / GM / CeoSupport (رؤية واسعة) + Manager / TeamLeader (نطاقهم فقط) + HR (نطاق HR/إدارته)
    /// + Employee (إنشاء + رؤية ما رفعه/استُهدف به/أُسنِد إليه فقط). Viewer لا وصول له.
    /// الفرز التفصيلي (واسع/نطاق/HR/موظف) وقيود الإسناد/الإغلاق/إعادة الفتح تُفرَض في طبقة الخدمة.
    /// </summary>
    public static readonly string[] GovernanceEscalationUsers =
    {
        Admin, Ceo, GeneralManager, CeoSupport, Manager, TeamLeader, Hr, Employee
    };

    /// <summary>
    /// الأدوار ذات الرؤية الواسعة على كل التصعيدات الفردية (بلا تقييد نطاق): Admin / CEO / GM / CeoSupport.
    /// Manager / TeamLeader يُقيَّدون بنطاقهم عبر ScopeResolver، وHR بنطاق إدارته، والموظف بما يخصّه فقط.
    /// </summary>
    public static readonly string[] GovernanceEscalationWideViewers =
    {
        Admin, Ceo, GeneralManager, CeoSupport
    };

    /// <summary>
    /// الأدوار المخوّلة بالإسناد/تغيير الحالة/الإغلاق/إعادة الفتح لأيّ تصعيد بغضّ النظر عن رافعه: Admin / CEO / GM.
    /// عداهم: المدير ضمن نطاقه يعالج/يُسنِد؛ قائد الفريق يتابع/يعلّق (لا يغلق إلا إن كان مُسنَدًا إليه)؛
    /// الموظف لا يُسنِد ولا يغلق (يُفرَض في الخدمة).
    /// </summary>
    public static readonly string[] GovernanceEscalationWideManagers =
    {
        Admin, Ceo, GeneralManager
    };

    // ===== إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1) =====

    /// <summary>
    /// الأدوار التي يحقّ لها الوصول لمنظومة إجراءات الحوكمة: Admin / CEO / GM / CeoSupport + Manager / TeamLeader / HR
    /// + Employee (يرى ما أُسنِد إليه أو أنشأه فقط). Viewer لا وصول له. الفرز التفصيلي (إنشاء/رؤية/تحديث) يُفرَض في الخدمة.
    /// </summary>
    public static readonly string[] GovernanceActionItemUsers =
    {
        Admin, Ceo, GeneralManager, CeoSupport, Manager, TeamLeader, Hr, Employee
    };

    /// <summary>
    /// الأدوار ذات الرؤية الواسعة على كل إجراءات الحوكمة (بلا تقييد نطاق): Admin / CEO / GM / CeoSupport.
    /// Manager / TeamLeader يُقيَّدون بنطاقهم، وHR بنطاق إدارته، والموظف بما أُسنِد إليه/أنشأه فقط.
    /// </summary>
    public static readonly string[] GovernanceActionItemWideViewers =
    {
        Admin, Ceo, GeneralManager, CeoSupport
    };

    /// <summary>
    /// الأدوار المخوّلة بإنشاء/إسناد/تغيير تاريخ الاستحقاق/إلغاء/إعادة فتح أيّ إجراء حوكمة: Admin / CEO / GM.
    /// عداهم: المدير ضمن نطاقه يُنشئ/يُسنِد؛ المُسنَد إليه يحدّث حالة التنفيذ فقط؛ يُفرَض في الخدمة.
    /// </summary>
    public static readonly string[] GovernanceActionItemWideManagers =
    {
        Admin, Ceo, GeneralManager
    };

    /// <summary>
    /// الأدوار المخوّلة بقراءة سجلّ إشعارات البريد (EMAIL-NOTIFICATIONS-R1): Admin / CEO / GM / CeoSupport.
    /// عرض إداريّ قراءة-فقط بلا متن الرسالة. عداهم ممنوعون.
    /// </summary>
    public static readonly string[] EmailNotificationLogViewers =
    {
        Admin, Ceo, GeneralManager, CeoSupport
    };

    /// <summary>
    /// الأدوار المخوّلة بإدارة مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1): Admin حصرًا.
    /// كتابة قوالب/قواعد + تشغيل تذكير يدويّ DryRun. عداه ممنوعون.
    /// </summary>
    public static readonly string[] EmailControlManagers =
    {
        Admin
    };

    public const string DisplayAr_Admin = "مدير النظام";
    public const string DisplayAr_Ceo = "الرئيس التنفيذي";
    public const string DisplayAr_GeneralManager = "المدير العام";
    public const string DisplayAr_Manager = "مدير";
    public const string DisplayAr_TeamLeader = "قائد فريق";
    public const string DisplayAr_Employee = "موظف";
    public const string DisplayAr_CeoSupport = "دعم الرئيس التنفيذي";
    public const string DisplayAr_Viewer = "مُطّلِع";
    public const string DisplayAr_Hr = "الموارد البشرية";
    public const string DisplayAr_FinanceManager = "المدير المالي";
    public const string DisplayAr_Accountant = "الحسابات";
    public const string DisplayAr_AccountPortfolioReader = "محفظة مدير الحساب";

    public static string DisplayAr(string role) => role switch
    {
        Admin => DisplayAr_Admin,
        Ceo => DisplayAr_Ceo,
        GeneralManager => DisplayAr_GeneralManager,
        Manager => DisplayAr_Manager,
        TeamLeader => DisplayAr_TeamLeader,
        Employee => DisplayAr_Employee,
        CeoSupport => DisplayAr_CeoSupport,
        Viewer => DisplayAr_Viewer,
        Hr => DisplayAr_Hr,
        FinanceManager => DisplayAr_FinanceManager,
        Accountant => DisplayAr_Accountant,
        AccountPortfolioReader => DisplayAr_AccountPortfolioReader,
        _ => role
    };
}

/// <summary>أسماء سياسات التفويض.</summary>
public static class Policies
{
    public const string ManagementOnly = "ManagementOnly";
    public const string AdminOnly = "AdminOnly";
    public const string ExecutiveOnly = "ExecutiveOnly"; // Admin + CEO + GM
    public const string TeamManagement = "TeamManagement"; // إدارة عضوية الفرق — Admin/CEO/GM (+ HR لاحقًا)
    // حوكمة القوالب وKPI والأوزان والربط والإصدارات وإعدادات الأداء — Admin/CEO/GM (+ HR لاحقًا).
    // تغطّي القدرات المنطقية: ManageReportTemplates / ManageKpiTemplates / AssignTemplatesToRoles /
    // ManageTemplateVersions / ManagePerformanceSettings (سياسة واحدة مجمّعة بدل خمس سياسات منفصلة، اتباعًا للنمط القائم).
    public const string TemplateGovernance = "TemplateGovernance";

    // الاعتماد النهائي لطلبات الإجازة/الاستئذان (V1.0.1-A) — قدرة الموارد البشرية HR (+ تدخّل Admin/CEO/GM).
    public const string LeaveFinalApproval = "LeaveFinalApproval";

    // رؤية طابور المراجعة «بانتظار قراري» للإجازات — اتحاد الإدارة + الموارد البشرية (Roles.LeaveReviewers).
    public const string LeaveReview = "LeaveReview";

    // إعادة تعيين كلمة مرور المستخدم بواسطة جهة مخوّلة — Admin + CEO + CeoSupport (GOV-R1).
    // عمدًا لا تشمل HR/GM/Manager/TeamLeader. حساب Admin لا يُعاد ضبط كلمته إلا بفاعل Admin (حارس الخدمة لم يتغيّر).
    public const string UserPasswordReset = "UserPasswordReset";

    // إدارة المستخدمين الكاملة (إنشاء/تعديل/تعطيل/حذف + الأدوار) عبر «إدارة فريق العمل» — Admin + CEO فقط (GOV-R1).
    // منفصلة عن AdminOnly العامة كي لا تتسرّب الصلاحية لبقية موارد AdminOnly (قوالب/إشعارات/فِرق/إدارات).
    public const string UserManagement = "UserManagement";

    // تعديل المسمّى الوظيفي للموظف فقط (السطح المخصّص) — Admin/CeoSupport/HR/GM/CEO (Roles.UserJobRoleManagers).
    // مستقلة عن UserPasswordReset وعن AdminOnly؛ لا تمنح أيّ قدرة أخرى على إدارة المستخدم.
    public const string UserJobRoleManagement = "UserJobRoleManagement";

    // رؤية/إدارة أرصدة الإجازات والأذونات (V1.1) — Admin/CEO/GM/CeoSupport/HR (Roles.BalanceManagers).
    public const string BalanceManagement = "BalanceManagement";

    // معالجة طلبات الموارد البشرية العامة (V1.1) — HR/Admin/CEO/GM/CeoSupport (Roles.HrRequestManagers).
    public const string HrRequestManagement = "HrRequestManagement";

    // تعديل البيانات الأساسية غير الحسّاسة للموظف (الاسم فقط) — Admin/CeoSupport/HR (Roles.UserBasicManagers).
    // لا تمسّ الأدوار/الصلاحيات/كلمة المرور/التفعيل/البريد/الإنشاء/الحذف؛ منفصلة عن AdminOnly وUserPasswordReset.
    public const string UserBasicManagement = "UserBasicManagement";

    // تعديل الانتماء التنظيمي للموظف (الإدارة/الفريق/المدير) — Admin/CeoSupport/HR/GM/CEO (Roles.UserOrgAssigners).
    // القيود الأمنية تُفرَض في طبقة الخدمة؛ لا تمنح أيّ قدرة أخرى على إدارة المستخدم.
    public const string UserOrgAssignment = "UserOrgAssignment";

    // رؤية شاشة متابعة التزام التسليم (per-person، عرض فقط بلا محتوى تقرير) — Roles.CompletionMonitors (+HR).
    public const string ReportCompletionView = "ReportCompletionView";

    // قراءة دليل الموارد البشرية المخصّص (موظفون/إدارات/فرق/مديرون لقوائم الاختيار) — Roles.HrDirectoryReaders.
    // قراءة فقط، منفصلة عن الدليل العام، لا تمنح كتابة ولا رؤية محتوى تقارير. الحسابات الحسّاسة تظهر مقفولة.
    public const string HrDirectoryRead = "HrDirectoryRead";

    // قراءة «عرض التأثير على الرواتب» (FIN-L1) — Admin/CEO/GM/HR/CeoSupport (Roles.PayrollImpactReaders).
    // عرض على مستوى الشركة (بلا ScopeResolver)، إعلامي بحت؛ لا تعديل على الطلب الأصلي ولا خصم آليّ.
    public const string PayrollImpactRead = "PayrollImpactRead";

    // تحديث حالة المراجعة المالية لطلب مؤثّر على الراتب (FIN-L1) — Admin/HR فقط (Roles.PayrollImpactManagers).
    // يغيّر حالة المراجعة والملاحظة فقط؛ لا يمسّ حالة الطلب الأصلي ولا الراتب. CEO/GM/CeoSupport قراءة فقط.
    public const string PayrollImpactManage = "PayrollImpactManage";

    // تصدير تقييمات KPI المعتمدة للمالية (KPI-FIN1) — Admin/CEO/GM/HR/CeoSupport (Roles.KpiFinanceExporters).
    // عرض/تصدير على مستوى الشركة (بلا ScopeResolver)، قراءة فقط؛ لا يحسب/يصرف مستحقات ولا يغيّر أيّ تقييم.
    public const string KpiFinanceExport = "KpiFinanceExport";

    // رؤية «محفظة مدير الحساب» (مشاريعي/عملائي — عرض فقط) — AccountPortfolioReader (+Admin تشغيليًّا)
    // عبر Roles.AccountPortfolioReaders. الرؤية مقصورة خادمًا على مشاريع المستخدم نفسه (AccountManagerId)
    // وعملائها المشتقّين؛ لا إدارة/اعتماد/إنشاء/حذف، لا مسودّات/معادة، لا KPI، لا تقييمات.
    public const string AccountPortfolioRead = "AccountPortfolioRead";

    // الوصول إلى «ورشة الحوكمة» (GOV-GOVERNANCE-UX1) — Roles.GovernanceWorkspaceUsers
    // (Admin/CEO/GM/CeoSupport واسع + Manager/TeamLeader نطاق + HR محدود). Employee/Viewer ممنوعون.
    // الفرز التفصيلي للرؤية (واسع/نطاق/HR) وقيود تغيير الحالة/الإغلاق تُفرَض في طبقة الخدمة.
    public const string GovernanceWorkspaceAccess = "GovernanceWorkspaceAccess";

    // الوصول إلى «التصعيد الفردي» (GOV-INDIVIDUAL-ESCALATION1 — كيان مستقلّ عن بنود الحوكمة العامة) —
    // Roles.GovernanceEscalationUsers (Admin/CEO/GM/CeoSupport واسع + Manager/TeamLeader نطاق + HR محدود + Employee
    // ما يخصّه). Viewer ممنوع. الفرز التفصيلي للرؤية وقيود الإسناد/الإغلاق/إعادة الفتح تُفرَض في طبقة الخدمة.
    // مستقلّ تمامًا عن GovernanceWorkspaceAccess ولا يمسّ سير اعتماد التقارير.
    public const string GovernanceEscalationAccess = "GovernanceEscalationAccess";

    // إجراءات الحوكمة والمتابعة (GOV-ACTION-ITEMS-R1): Roles.GovernanceActionItemUsers (Admin/CEO/GM/CeoSupport واسع
    // + Manager/TeamLeader نطاق + HR محدود + Employee ما أُسنِد إليه/أنشأه). Viewer ممنوع. الفرز التفصيلي للرؤية وقيود
    // الإنشاء/الإسناد/تغيير الحالة تُفرَض في طبقة الخدمة. مستقلّ تمامًا ولا يمسّ سير اعتماد التقارير.
    public const string GovernanceActionItemAccess = "GovernanceActionItemAccess";

    // سجلّ إشعارات البريد (EMAIL-NOTIFICATIONS-R1): Roles.EmailNotificationLogViewers (Admin/CEO/GM/CeoSupport).
    // عرض إداريّ قراءة-فقط (بلا متن الرسالة). مستقلّ تمامًا ولا يمسّ أي سير عمل.
    public const string EmailNotificationLog = "EmailNotificationLog";

    // مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1): Roles.EmailControlManagers (Admin حصرًا).
    // كتابة قوالب/قواعد + تذكير يدويّ DryRun. مستقلّ تمامًا ولا يمسّ أي سير عمل. R1: DryRun فقط.
    public const string EmailControlManage = "EmailControlManage";
}
