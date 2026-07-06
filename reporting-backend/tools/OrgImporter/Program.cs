using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Org;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using OrgImporter;

// =====================================================================================
// OrgImporter — أداة استيراد لمرة واحدة لنقل الهيكل التنظيمي للديمو إلى بيئة الإنتاج/Pilot.
//
// تستورد فقط: المستخدمين + الأدوار (Identity) + الإدارات + المسميات الوظيفية + الفرق
// + علاقات الرفع (ManagerId) وعضوية الفِرق (TeamId) — وهي أعمدة على ApplicationUser.
// لا تنقل أي بيانات تشغيلية (تقارير، KPI، إجازات، حوكمة، تدقيق، إشعارات، عملاء/مشاريع).
//
// أمان Admin (إلزامي): تتجاهل تمامًا أي مستخدم قائم بدور Admin — لا تعديل ولا حذف،
// ولا تُنشئ أي مستخدم بدور Admin. الأدوار المُسندة من الديمو لا تشمل Admin إطلاقًا.
//
// الوضع الافتراضي: --dry-run (محاكاة داخل معاملة تُلغى بالكامل، لا تكتب شيئًا).
// التنفيذ الفعلي: --apply (يتطلب ORG_IMPORT_PASSWORD، ويُثبِّت المعاملة فقط عند نجاح كل الإنشاءات).
//
// الإعداد عبر متغيرات البيئة فقط:
//   ConnectionStrings__Default   سلسلة اتصال PostgreSQL (إلزامية).
//   ORG_IMPORT_PASSWORD          كلمة مرور مؤقتة موحّدة للمستخدمين الجدد (إلزامية مع --apply).
// لا تُطبع كلمة المرور ولا أي أسرار في أي مخرجات.
// =====================================================================================

Console.OutputEncoding = Encoding.UTF8;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var dryRun = !apply;

var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
if (string.IsNullOrWhiteSpace(conn))
{
    Console.Error.WriteLine("خطأ: متغيّر البيئة ConnectionStrings__Default غير مضبوط.");
    return 2;
}

var tempPassword = Environment.GetEnvironmentVariable("ORG_IMPORT_PASSWORD");
if (apply && string.IsNullOrWhiteSpace(tempPassword))
{
    Console.Error.WriteLine("خطأ: --apply يتطلب ضبط ORG_IMPORT_PASSWORD (كلمة مرور مؤقتة موحّدة).");
    return 2;
}

// كلمة مرور بديلة للمحاكاة فقط — تُستخدم داخل معاملة تُلغى دائمًا في --dry-run، فلا تُحفظ أبدًا.
var creationPassword = tempPassword ?? "DryRunPlaceholder1A";

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddDataProtection(); // يلزم لمزوّدات رموز Identity الافتراضية
services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));
services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

await using var sp = services.BuildServiceProvider();
using var scope = sp.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

var report = new Report();
var hadCreateFailure = false;

await using var tx = await db.Database.BeginTransactionAsync();

// ---- 0) تحميل المستخدمين القائمين + تحديد حسابات Admin (تُستثنى كليًّا) ----
var existingUsers = await db.Users.ToListAsync();
var byEmail = existingUsers
    .Where(u => u.Email != null)
    .ToDictionary(u => u.Email!, u => u, StringComparer.OrdinalIgnoreCase);

var adminUserIds = new HashSet<Guid>();
var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == Roles.Admin);
if (adminRole != null)
    adminUserIds = (await db.UserRoles.Where(ur => ur.RoleId == adminRole.Id)
        .Select(ur => ur.UserId).ToListAsync()).ToHashSet();

var resolved = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

// ---- 1) ضمان وجود المستخدمين (إنشاء الناقص بكلمة المرور المؤقتة + إسناد الدور) ----
foreach (var p in OrgData.People)
{
    if (byEmail.TryGetValue(p.Email, out var existing))
    {
        if (adminUserIds.Contains(existing.Id))
        {
            report.AdminsExcluded.Add(p.Email); // حساب Admin قائم — لا يُلمَس إطلاقًا
            continue;
        }
        resolved[p.Email] = existing.Id;
        report.UsersExisting.Add(p.Email);
        continue;
    }

    var user = new ApplicationUser
    {
        UserName = p.Email,
        Email = p.Email,
        EmailConfirmed = true,
        FullName = p.FullName,
        IsActive = true
    };
    var created = await userMgr.CreateAsync(user, creationPassword);
    if (!created.Succeeded)
    {
        hadCreateFailure = true;
        report.Warnings.Add($"فشل إنشاء {p.Email}: {string.Join("; ", created.Errors.Select(e => e.Code))}");
        continue;
    }
    var roled = await userMgr.AddToRoleAsync(user, p.Role);
    if (!roled.Succeeded)
    {
        hadCreateFailure = true;
        report.Warnings.Add($"فشل إسناد الدور {p.Role} لـ {p.Email}: {string.Join("; ", roled.Errors.Select(e => e.Code))}");
    }
    resolved[p.Email] = user.Id;
    report.UsersCreated.Add(p.Email);
}

// ---- 2) الإدارات (upsert بالرمز Code) ----
var deptIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
var existingDepts = await db.Departments.ToListAsync();
foreach (var d in OrgData.Departments)
{
    Guid? mgrId = resolved.TryGetValue(d.ManagerEmail, out var mid) ? mid : null;
    if (mgrId is null) report.Warnings.Add($"الإدارة {d.Code}: لم يُحَلّ المدير {d.ManagerEmail}");

    var ex = existingDepts.FirstOrDefault(x => string.Equals(x.Code, d.Code, StringComparison.OrdinalIgnoreCase));
    if (ex is null)
    {
        var nd = new Department { NameAr = d.NameAr, Code = d.Code, ManagerId = mgrId, IsActive = true };
        db.Departments.Add(nd);
        await db.SaveChangesAsync();
        deptIdByCode[d.Code] = nd.Id;
        report.DeptsCreated.Add(d.Code);
    }
    else
    {
        if (ex.ManagerId is null && mgrId is not null)
        {
            ex.ManagerId = mgrId;
            report.DeptsUpdated.Add(d.Code);
        }
        deptIdByCode[d.Code] = ex.Id;
    }
}
await db.SaveChangesAsync();

// ---- 3) المسميات الوظيفية (upsert بالرمز Code) ----
var jobIdByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
var existingJobs = await db.JobRoles.ToListAsync();
foreach (var j in OrgData.JobRoles)
{
    Guid? deptId = j.DeptCode is not null && deptIdByCode.TryGetValue(j.DeptCode, out var did) ? did : null;
    var ex = existingJobs.FirstOrDefault(x => string.Equals(x.Code, j.Code, StringComparison.OrdinalIgnoreCase));
    if (ex is null)
    {
        var nj = new JobRole { NameAr = j.NameAr, Code = j.Code, DepartmentId = deptId, IsActive = true };
        db.JobRoles.Add(nj);
        await db.SaveChangesAsync();
        jobIdByCode[j.Code] = nj.Id;
        report.JobsCreated.Add(j.Code);
    }
    else
    {
        if (ex.DepartmentId is null && deptId is not null)
        {
            ex.DepartmentId = deptId;
            report.JobsUpdated.Add(j.Code);
        }
        jobIdByCode[j.Code] = ex.Id;
    }
}
await db.SaveChangesAsync();

// ---- 4) الفِرق (upsert بـ Department + NameAr) ----
var teamIdByName = new Dictionary<string, Guid>(StringComparer.Ordinal);
var existingTeams = await db.Teams.ToListAsync();
foreach (var t in OrgData.Teams)
{
    if (!deptIdByCode.TryGetValue(t.DeptCode, out var deptId))
    {
        report.Warnings.Add($"الفريق {t.NameAr}: لم تُحَلّ إدارته {t.DeptCode}");
        continue;
    }
    Guid? leaderId = t.LeaderEmail is not null && resolved.TryGetValue(t.LeaderEmail, out var lid) ? lid : null;
    if (t.LeaderEmail is not null && leaderId is null)
        report.Warnings.Add($"الفريق {t.NameAr}: لم يُحَلّ القائد {t.LeaderEmail}");

    var ex = existingTeams.FirstOrDefault(x => x.DepartmentId == deptId && x.NameAr == t.NameAr);
    if (ex is null)
    {
        var nt = new Team { NameAr = t.NameAr, DepartmentId = deptId, TeamLeaderId = leaderId, IsActive = true };
        db.Teams.Add(nt);
        await db.SaveChangesAsync();
        teamIdByName[t.NameAr] = nt.Id;
        report.TeamsCreated.Add(t.NameAr);
    }
    else
    {
        if (ex.TeamLeaderId is null && leaderId is not null)
        {
            ex.TeamLeaderId = leaderId;
            report.TeamsUpdated.Add(t.NameAr);
        }
        teamIdByName[t.NameAr] = ex.Id;
    }
}
await db.SaveChangesAsync();

// ---- 5) ضبط روابط المستخدم التنظيمية (مدير/إدارة/فريق/مسمى وظيفي) ----
foreach (var p in OrgData.People)
{
    if (!resolved.TryGetValue(p.Email, out var uid)) continue; // مستثنى (Admin) أو فشل إنشاؤه

    var u = await db.Users.FirstAsync(x => x.Id == uid);

    Guid? mgr = null;
    if (p.ManagerEmail is not null)
    {
        if (resolved.TryGetValue(p.ManagerEmail, out var m)) mgr = m;
        else report.Warnings.Add($"المستخدم {p.Email}: لم يُحَلّ المدير {p.ManagerEmail}");
    }
    Guid? deptId = p.DeptCode is not null && deptIdByCode.TryGetValue(p.DeptCode, out var d2) ? d2 : null;
    Guid? teamId = p.TeamName is not null && teamIdByName.TryGetValue(p.TeamName, out var t2) ? t2 : null;
    Guid? jobId = p.JobCode is not null && jobIdByCode.TryGetValue(p.JobCode, out var j2) ? j2 : null;

    u.ManagerId = mgr;
    u.DepartmentId = deptId;
    u.TeamId = teamId;
    u.JobRoleId = jobId;
    report.RelationshipsSet++;
}
await db.SaveChangesAsync();

// ---- إنهاء المعاملة: --apply يُثبّت فقط عند نجاح كل الإنشاءات؛ --dry-run يُلغي دائمًا ----
var committed = false;
if (apply && !hadCreateFailure)
{
    await tx.CommitAsync();
    committed = true;
}
else
{
    await tx.RollbackAsync();
}

// ---- طباعة التقرير ----
Console.WriteLine();
Console.WriteLine("==================================================================");
Console.WriteLine($" الوضع: {(apply ? "APPLY (تنفيذ فعلي)" : "DRY-RUN (محاكاة — لا كتابة)")}");
Console.WriteLine($" الحالة: {(committed ? "تم تثبيت التغييرات" : (apply ? "أُلغيت بسبب أخطاء إنشاء — لا تغييرات" : "محاكاة مُلغاة — لا تغييرات"))}");
Console.WriteLine("==================================================================");
report.Print();

if (apply && hadCreateFailure)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("تنبيه: حدثت أخطاء إنشاء فأُلغيت المعاملة كاملةً. عالِج الأخطاء أعلاه ثم أعد المحاولة.");
    return 1;
}

return 0;
