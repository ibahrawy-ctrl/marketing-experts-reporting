using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;

// R22B — أداة اعتماد مؤقّتة لبيئات الاختبار فقط (تستعمل ASP.NET Core Identity الرسميّة، لا SQL خام).
// تضبط كلمة مرور حساب موجود، وتفكّ الإيقاف، وتؤكّد البريد — لأداء رحلة القبول البصريّ.
// حارس إلزاميّ: ترفض العمل على أيّ قاعدة غير المصرّح بها في --expect-db.
//
//   dotnet R22BAccountTool.dll --conn <cs> --expect-db reporting_test_uat \
//       --email <mail> --password <pwd> [--role Employee] [--disable]

string? conn = null, expectDb = null, email = null, password = null, role = null;
var disable = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--conn": conn = args[++i]; break;
        case "--expect-db": expectDb = args[++i]; break;
        case "--email": email = args[++i]; break;
        case "--password": password = args[++i]; break;
        case "--role": role = args[++i]; break;
        case "--disable": disable = true; break;
        default: Console.Error.WriteLine($"وسيط غير معروف: {args[i]}"); return 2;
    }
}

if (conn is null || expectDb is null || email is null)
{
    Console.Error.WriteLine("الاستعمال: --conn <cs> --expect-db <db> --email <mail> [--password <pwd>] [--role <r>] [--disable]");
    return 2;
}

// حارس البيئة: الإنتاج ممنوع قطعيًّا على هذه الأداة.
if (expectDb == "reporting_prod")
{
    Console.Error.WriteLine("حارس البيئة: هذه الأداة محظورة على قاعدة الإنتاج.");
    return 3;
}

var services = new ServiceCollection();
services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));
services.AddIdentityCore<ApplicationUser>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = true;
        o.Password.RequireUppercase = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireNonAlphanumeric = false;
        o.User.RequireUniqueEmail = true;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<AppDbContext>();

await using var sp = services.BuildServiceProvider();
using var scope = sp.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

var actualDb = (await db.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"").ToListAsync())[0];
if (actualDb != expectDb)
{
    Console.Error.WriteLine($"حارس الهويّة: متوقَّع «{expectDb}» والاتّصال على «{actualDb}» — أُوقِف التنفيذ.");
    return 3;
}

var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var user = await users.FindByEmailAsync(email);
if (user is null)
{
    Console.Error.WriteLine($"لا يوجد مستخدم بالبريد {email} — الأداة لا تنشئ حسابات جديدة.");
    return 4;
}

if (disable)
{
    user.IsActive = false;
    await users.UpdateAsync(user);
    await users.SetLockoutEnabledAsync(user, true);
    await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
    await users.RemovePasswordAsync(user);
    Console.WriteLine($"عُطِّل الحساب {email} (قفل دائم + إزالة كلمة المرور).");
    return 0;
}

if (password is null) { Console.Error.WriteLine("مطلوب --password."); return 2; }

await users.SetLockoutEndDateAsync(user, null);
await users.ResetAccessFailedCountAsync(user);
user.EmailConfirmed = true;
user.IsActive = true;
var upd = await users.UpdateAsync(user);
if (!upd.Succeeded) { Console.Error.WriteLine(string.Join("; ", upd.Errors.Select(e => e.Description))); return 5; }

if (await users.HasPasswordAsync(user)) await users.RemovePasswordAsync(user);
var add = await users.AddPasswordAsync(user, password);
if (!add.Succeeded) { Console.Error.WriteLine(string.Join("; ", add.Errors.Select(e => e.Description))); return 5; }

if (role is not null && !await users.IsInRoleAsync(user, role))
{
    var r = await users.AddToRoleAsync(user, role);
    if (!r.Succeeded) { Console.Error.WriteLine(string.Join("; ", r.Errors.Select(e => e.Description))); return 5; }
}

var roles = await users.GetRolesAsync(user);
Console.WriteLine($"جاهز: {email} · Id={user.Id} · نشط={user.IsActive} · أدوار=[{string.Join(",", roles)}] · القاعدة={actualDb}");
return 0;
