using System.Globalization;
using Reporting.Domain.Enums;

namespace Reporting.Application.Common;

/// <summary>
/// سياسة تقويم التقارير المُدرِكة للأدوار (ROLE-AWARE-REPORTING-CALENDAR).
/// دورة التقرير تبدأ يوم **السبت** وتنتهي يوم **الجمعة** (نافذة ثابتة Sat→Fri)، ورقم الدورة
/// يُشتقّ من **مرجع الثلاثاء** (بداية الدورة + 3 أيام) عبر ترقيم ISO — وهو ما يُعيد إنتاج
/// جدول الإدارة المعتمد (W27→السبت 27 يونيو 2026، W28→السبت 4 يوليو …) دون قاعدة مخترَعة.
///
/// المبدأ الجوهريّ: كل المستويات (موظّف/قائد فريق/مدير/مدير عام/رئيس تنفيذي/KPI) لنفس الفترة
/// تحمل **نفس مفتاح الدورة** (CycleKey)؛ ما يختلف هو **تاريخ الاستحقاق بحسب الدور** فقط:
///   الموظّف/فِرَق التنفيذ = الأربعاء (بداية+4)، قائد الفريق = الخميس (بداية+5)،
///   المدير = الأحد (بداية+8)، المدير العام/الرئيس التنفيذي = الاثنين (بداية+9).
/// اختلاف تاريخ الاستحقاق ≠ اختلاف الأسبوع.
///
/// دوال خالصة (Pure) بلا حالة؛ **الدور يُستخرَج خادميًّا** من دور المستخدم الأساسيّ ولا يُرسَل من الواجهة.
/// هذه السياسة هي **مصدر الحقيقة الوحيد** لحساب الدورة/الاستحقاق؛ يفوّض إليها التقويم التشغيليّ القديم.
/// </summary>
public static class ReportingCalendarPolicy
{
    // ===== المنطقة الزمنية المرجعية (آسيا/الرياض، UTC+3 ثابت بلا توقيت صيفي) =====

    /// <summary>إزاحة توقيت الرياض الثابتة عن UTC (+3 ساعات، بلا توقيت صيفي).</summary>
    public static readonly TimeSpan RiyadhOffset = TimeSpan.FromHours(3);

    /// <summary>التاريخ الحالي بتوقيت الرياض.</summary>
    public static DateOnly RiyadhToday() =>
        DateOnly.FromDateTime(DateTime.UtcNow.Add(RiyadhOffset));

    /// <summary>تاريخ لحظة UTC معيّنة بتوقيت الرياض (لتقييم تأخّر التسليم بيومه المحلّي).</summary>
    public static DateOnly RiyadhDate(DateTime utc) =>
        DateOnly.FromDateTime((utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc)).Add(RiyadhOffset));

    // ===== نافذة الدورة (السبت → الجمعة) =====

    /// <summary>يوم السبت الذي تبدأ به الدورة المحتوية على تاريخ معيّن.</summary>
    public static DateOnly CycleStart(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        return date.AddDays(-diff);
    }

    /// <summary>يوم الجمعة الذي تنتهي به الدورة (بداية + 6 أيام).</summary>
    public static DateOnly CycleEnd(DateOnly cycleStart) => cycleStart.AddDays(6);

    /// <summary>مرجع الثلاثاء للدورة (بداية + 3 أيام) — أساس الترقيم.</summary>
    public static DateOnly TuesdayReference(DateOnly cycleStart) => cycleStart.AddDays(3);

    /// <summary>مفتاح الدورة YYYY-Www للتاريخ (مرتكزًا على مرجع الثلاثاء).</summary>
    public static string CycleKeyFor(DateOnly date)
    {
        var start = CycleStart(date);
        var tuesday = TuesdayReference(start).ToDateTime(TimeOnly.MinValue);
        var week = ISOWeek.GetWeekOfYear(tuesday);
        var year = ISOWeek.GetYear(tuesday);
        return $"{year}-W{week:00}";
    }

    /// <summary>رقم الدورة (أسبوع ISO لمرجع الثلاثاء).</summary>
    public static int CycleNumber(DateOnly date) =>
        ISOWeek.GetWeekOfYear(TuesdayReference(CycleStart(date)).ToDateTime(TimeOnly.MinValue));

    /// <summary>سنة الدورة (سنة ISO لمرجع الثلاثاء).</summary>
    public static int CycleYear(DateOnly date) =>
        ISOWeek.GetYear(TuesdayReference(CycleStart(date)).ToDateTime(TimeOnly.MinValue));

    /// <summary>عكس العملية: (سبت البداية، جمعة النهاية) لمفتاح دورة.</summary>
    public static (DateOnly Start, DateOnly End) CycleRange(string cycleKey)
    {
        var (year, week) = ParseCycleKey(cycleKey);
        // مرجع الثلاثاء = ثلاثاء أسبوع ISO؛ بداية الدورة = مرجع الثلاثاء − 3 أيام (السبت السابق).
        var tuesday = DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Tuesday));
        var start = tuesday.AddDays(-3);
        return (start, start.AddDays(6));
    }

    /// <summary>تحليل مفتاح الدورة YYYY-Www إلى (سنة، رقم الأسبوع).</summary>
    public static (int Year, int Week) ParseCycleKey(string cycleKey)
    {
        var parts = cycleKey.Trim().Split("-W");
        return (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    /// <summary>هل المفتاح بصيغة الدورة YYYY-Www (مثل 2026-W27)؟</summary>
    public static bool IsCycleKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && System.Text.RegularExpressions.Regex.IsMatch(key.Trim(), @"^\d{4}-W\d{2}$");

    /// <summary>
    /// هل المفتاح صالح بنيويًّا **وقابل للعكس بلا خسارة**؟ يرفض أرقامًا خارج مدى ISO للسنة
    /// (مثل W53 لسنة ذات 52 أسبوعًا) إذ إنّ إعادة تشكيل المفتاح من نطاقه يجب أن تطابق الأصل.
    /// </summary>
    public static bool IsValidCycleKey(string? key)
    {
        if (!IsCycleKey(key)) return false;
        var (year, week) = ParseCycleKey(key!);
        if (week < 1 || week > ISOWeek.GetWeeksInYear(year)) return false;
        // تحقّق ذهاب-وإياب: مفتاح بداية الدورة يجب أن يساوي المفتاح المُدخَل.
        var (start, _) = CycleRange(key!);
        return CycleKeyFor(start) == key!.Trim();
    }

    // ===== تواريخ الاستحقاق بحسب الدور (نفس الدورة، إزاحات ثابتة من السبت) =====
    // الموظّف/التنفيذ: الأربعاء (بداية+4). قائد الفريق: الخميس (بداية+5).
    // المدير: الأحد (بداية+8). المدير العام/الرئيس التنفيذي: الاثنين (بداية+9).

    /// <summary>إزاحة يوم الاستحقاق (أيام من سبت البداية) لدور تقريريّ.</summary>
    public static int RoleDueOffset(string role) => role switch
    {
        Roles.TeamLeader => 5,                                     // الخميس
        Roles.Manager => 8,                                        // الأحد
        Roles.GeneralManager or Roles.Ceo or Roles.Admin => 9,    // الاثنين (مراجعة أعلى)
        _ => 4                                                     // الموظّف وغيره ⇒ الأربعاء
    };

    /// <summary>تاريخ الاستحقاق لدور معيّن عن دورة معيّنة (سبت البداية + إزاحة الدور).</summary>
    public static DateOnly RoleDueDate(string cycleKey, string role)
    {
        var start = CycleRange(cycleKey).Start;
        return start.AddDays(RoleDueOffset(role));
    }

    // ملاحظة: استخراج الدور الأساسيّ من أدوار المستخدم يتمّ عبر المصدر القانونيّ الوحيد
    // <see cref="RoleAccess.PrimaryRole"/> (لا تعريف ثانٍ هنا)، ثمّ يُمرَّر إلى <see cref="RoleDueOffset"/>.
    // الدور لا يُرسَل من الواجهة إطلاقًا — يُستخرَج خادميًّا من أدوار المستخدم الحاليّ.

    // ===== نطاق تغطية البيانات (Data Coverage) — نقطة توسّع موثّقة =====
    // في هذه الحزمة: تغطية البيانات الافتراضية = **نافذة الدورة نفسها** (السبت → الجمعة).
    // هذا مصمَّم كنقطة توسّع (Extension Point): مستقبلًا قد يُسنَد لقالب إعداد «مدى تغطية»
    // يمتدّ لعدّة دورات، فيُحسَب النطاق من هنا دون تعديل قاعدة البيانات أو الترقيم.
    // لا يوجد إعداد جديد في هذه الحزمة، ولا هجرة — القيمة الافتراضية فقط.

    /// <summary>
    /// نطاق تغطية البيانات الافتراضيّ لدورة (= نافذة الدورة السبت→الجمعة).
    /// نقطة توسّع: لا إعداد قالب جديد ولا هجرة في هذه الحزمة.
    /// </summary>
    public static (DateOnly Start, DateOnly End) DataCoverageWindow(string cycleKey) =>
        CycleRange(cycleKey);

    // ===== نافذة تاريخية محدودة (Bounded Historical Window) =====
    // عند عدم اختيار فترة محدّدة، يُحسَب «المتوقّع المفقود» عبر آخر عدد ثابت من الدورات
    // (شاملًا الدورة الحاليّة) — لا «كل الفترات» بلا حدّ. عدد استعلامات المُحلِّل ثابت
    // بنيويًّا مهما اتّسعت النافذة (دفعيّ عبر keys.Contains)، فالأداء محدود ومتوقَّع.

    /// <summary>
    /// مفاتيح آخر <paramref name="count"/> دورة (YYYY-Www) منتهيةً بالدورة المحتوية لـ<paramref name="today"/>،
    /// من الأحدث إلى الأقدم. تُرجِع قائمة فارغة إن كان العدد أقلّ من 1.
    /// </summary>
    public static IReadOnlyList<string> RecentCycleKeys(DateOnly today, int count)
    {
        if (count < 1) return Array.Empty<string>();
        var keys = new List<string>(count);
        var start = CycleStart(today);
        for (var i = 0; i < count; i++)
        {
            keys.Add(CycleKeyFor(start));
            start = start.AddDays(-7);
        }
        return keys;
    }

    // ===== انتماء الدورة لشهر/ربع/سنة (تجميع KPI) =====
    // تنتمي الدورة للشهر/الربع/السنة التي يقع فيها **مرجع الثلاثاء** — فلا تُحتسب دورة لفترتين.

    /// <summary>حدود فترة شهرية «YYYY-MM».</summary>
    public static (DateOnly Start, DateOnly End) MonthRange(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        return (start, start.AddMonths(1).AddDays(-1));
    }

    /// <summary>حدود فترة ربع سنوية (الربع 1..4).</summary>
    public static (DateOnly Start, DateOnly End) QuarterRange(int year, int quarter)
    {
        var firstMonth = (quarter - 1) * 3 + 1;
        var start = new DateOnly(year, firstMonth, 1);
        return (start, start.AddMonths(3).AddDays(-1));
    }

    /// <summary>حدود فترة سنوية.</summary>
    public static (DateOnly Start, DateOnly End) YearRange(int year) =>
        (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));

    /// <summary>هل تنتمي الدورة (بحسب مرجع الثلاثاء) إلى المدى [from, to]؟</summary>
    public static bool CycleInRange(string cycleKey, DateOnly from, DateOnly to)
    {
        if (!IsCycleKey(cycleKey)) return false;
        var anchor = TuesdayReference(CycleRange(cycleKey).Start); // مرجع الثلاثاء
        return anchor >= from && anchor <= to;
    }

    // ===== تسميات عربية مفهومة =====

    private static readonly string[] ArMonths =
    {
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
        "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
    };

    private static readonly string[] ArDays =
    {
        "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"
    };

    /// <summary>اسم اليوم العربيّ لتاريخ.</summary>
    public static string ArDayName(DateOnly date) => ArDays[(int)date.DayOfWeek];

    /// <summary>«8 يوليو».</summary>
    public static string ArDayMonth(DateOnly date) => $"{date.Day} {ArMonths[date.Month - 1]}";

    /// <summary>«الأسبوع 27 — 2026 (السبت 27 يونيو — الجمعة 3 يوليو)».</summary>
    public static string CycleLabel(string cycleKey)
    {
        if (!IsCycleKey(cycleKey)) return cycleKey;
        var (year, week) = ParseCycleKey(cycleKey);
        var (start, end) = CycleRange(cycleKey);
        return $"الأسبوع {week} — {year} (السبت {ArDayMonth(start)} — الجمعة {ArDayMonth(end)})";
    }

    /// <summary>تسمية مختصرة للدورة «الأسبوع 27 — 2026».</summary>
    public static string ShortCycleLabel(string cycleKey)
    {
        if (!IsCycleKey(cycleKey)) return cycleKey;
        var (year, week) = ParseCycleKey(cycleKey);
        return $"الأسبوع {week} — {year}";
    }

    // ===== الوضع اليوميّ (Daily) — تقارير المبيعات =====
    // مفتاح اليوم YYYY-MM-DD يُولَّد **خادميًّا** بتوقيت الرياض (لا حساب محليّ في الواجهة، ولا إدخال يدويّ).
    // أيام العمل اليومية المتوقَّعة: **الأحد→الخميس**. الجمعة **والسبت** عطلة أسبوعية لا تدخل التوقّع/الالتزام.
    // التقرير الفعليّ في يوم غير منطبق (جمعة/سبت) يبقى محفوظًا ومرئيًّا تاريخيًّا لكن لا يدخل المتوقّع/البسط/المقام.
    // هذه الدوال خالصة (Pure) وتشترك مع الأسبوعيّ في مصدر الحقيقة نفسه (RiyadhToday/التسميات العربية).

    /// <summary>مفتاح اليوم بصيغة YYYY-MM-DD (خادميّ، بلا انزياح منطقة زمنية).</summary>
    public static string DayKey(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>هل المفتاح بصيغة يوم YYYY-MM-DD صالحة بنيويًّا **وقابلة للعكس بلا خسارة**؟</summary>
    public static bool IsValidDayKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var s = key.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(s, @"^\d{4}-\d{2}-\d{2}$")) return false;
        return DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            && DayKey(d) == s; // تحقّق ذهاب-وإياب (يرفض مثل 2026-02-30 أو 2026-13-01).
    }

    /// <summary>تحليل مفتاح اليوم YYYY-MM-DD إلى تاريخ (يفترض صحّته البنيوية).</summary>
    public static DateOnly ParseDayKey(string key) =>
        DateOnly.ParseExact(key.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

    // الصيغ المقبولة لتطبيع مفتاح يوم إلى تاريخ منطقيّ واحد (CanonicalDay).
    // ISO الصارم أوّلًا (yyyy-MM-dd)، ثم صيغ ISO غير مبطّنة (yyyy-M-d)، ثم صيغ قديمة يوم-شهر-سنة (d-M-yyyy).
    // الترتيب مهمّ: القيم غير الصالحة (يوم>31 أو سنة موضعها خطأ) ترفض التفسير الخاطئ فيسقط لأوّل صيغة صحيحة.
    // موضع السنة رباعيّ الأرقام يفصل بنيويًّا بين عائلتَي yyyy-first و d-first فلا تداخل.
    private static readonly string[] CanonicalDayFormats =
    {
        "yyyy-MM-dd", "yyyy-M-d", "yyyy-MM-d", "yyyy-M-dd",
        "d-M-yyyy", "dd-MM-yyyy", "d-MM-yyyy", "dd-M-yyyy",
    };

    /// <summary>
    /// يُطبِّع مفتاح يوم — بما فيه الصيغ التاريخية غير القياسية (مثل <c>6-7-2026</c> أو <c>2026-07-9</c>) —
    /// إلى تاريخ منطقيّ واحد (CanonicalDay). لا يعدّل التخزين ولا يُعيد كتابة المفتاح الخام؛ يُستعمَل داخليًّا
    /// لاشتقاق موعد الاستحقاق ومنع الازدواج بين التسليم الفعليّ والصفّ المتوقّع المُولَّد (DAILY-…-R1 §2/§3).
    /// يُعيد <c>false</c> للمفاتيح غير القابلة للتفسير (تبقى مرئيّة بحالتها الخام دون توليد «متوقّع مفقود»).
    /// </summary>
    public static bool TryCanonicalDay(string? key, out DateOnly day)
    {
        day = default;
        if (string.IsNullOrWhiteSpace(key)) return false;
        return DateOnly.TryParseExact(
            key.Trim(), CanonicalDayFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
    }

    // ===== سريان السبت لموظّفي المبيعات (SALES-DAILY-SATURDAY-APPLICABILITY-HOTFIX-R1) =====
    // قرار تشغيليّ معتمَد: اعتبارًا من **2026-07-25 (سبت)** يصبح السبت يوم عمل متوقَّع/مُلتزَم به
    // لموظّفي **المبيعات فقط** (SALES_B2B/SALES_B2C) — أي الالتزام يمتدّ من السبت إلى الخميس، وتبقى
    // **الجمعة** محجوبة دائمًا وللجميع. **لا أثر رجعيّ**: أيّ سبت **قبل** الأرضية يبقى غير متوقَّع (لا
    // يُصنَّف Missing/Overdue) لكنه يظل محفوظًا ومرئيًّا كتقرير فعليّ. **غير المبيعات لا يتغيّر إطلاقًا**
    // (الأحد→الخميس، الجمعة+السبت عطلة). تفعيل السبت يُشتقّ من **مجموعة المسمّيات اليومية القانونيّة نفسها**
    // <see cref="ReportCadencePolicy.DailyJobRoleCodes"/> (مصدر واحد، لا قائمة موازية).

    /// <summary>أرضية سريان السبت للمبيعات: 2026-07-25 (سبت). لا التزام سبتٍ قبلها لأيّ دور.</summary>
    public static readonly DateOnly SalesSaturdayApplicabilityFloor = new(2026, 7, 25);

    /// <summary>
    /// هل السبت مُفعَّل كيوم عمل متوقَّع لهذا المسمّى الوظيفيّ؟ = المسمّيات اليومية القانونيّة
    /// (SALES_B2B/SALES_B2C) حصرًا؛ يفوّض إلى <see cref="ReportCadencePolicy.DailyJobRoleCodes"/> (لا قائمة ثانية).
    /// </summary>
    public static bool SaturdayEnabledForJobRole(string? jobRoleCode) =>
        jobRoleCode is not null && ReportCadencePolicy.DailyJobRoleCodes.Contains(jobRoleCode);

    /// <summary>
    /// **مصدر الحقيقة الوحيد** لعقد أيام العمل اليومية المتوقَّعة (DAILY-BUSINESS-DAY-COMPLIANCE-R1 §4):
    /// يوم منطبق للتوقّع اليوميّ ⟺ **الأحد→الخميس** (أيّ ليس جمعة ولا سبت).
    /// **الصيغة صفريّة الوسائط تحافظ حرفيًّا على سلوك غير-المبيعات** (السبت عطلة دائمًا) عبر التفويض
    /// بـ<paramref>saturdayEnabled=false</paramref>. كل السطوح تفوّض إلى هذه الدالّة بدل تكرار الاستبعاد.
    /// </summary>
    public static bool IsDailyExpectedBusinessDay(DateOnly date) =>
        IsDailyExpectedBusinessDay(date, saturdayEnabled: false);

    /// <summary>
    /// عقد أيام العمل اليومية المتوقَّعة **مُدرِكًا لسريان سبت المبيعات**:
    /// الجمعة محجوبة دائمًا؛ الأحد→الخميس متوقَّعة دائمًا؛ **السبت** متوقَّع ⟺
    /// (<paramref name="saturdayEnabled"/> = مبيعات) **و** التاريخ ≥ <see cref="SalesSaturdayApplicabilityFloor"/>.
    /// حين <paramref name="saturdayEnabled"/>=false ⇒ السبت عطلة دائمًا (سلوك غير-المبيعات الأصليّ حرفيًّا).
    /// </summary>
    public static bool IsDailyExpectedBusinessDay(DateOnly date, bool saturdayEnabled) => date.DayOfWeek switch
    {
        DayOfWeek.Friday => false,
        DayOfWeek.Saturday => saturdayEnabled && date >= SalesSaturdayApplicabilityFloor,
        _ => true, // الأحد→الخميس
    };

    /// <summary>
    /// هل اليوم عطلة أسبوعية بحسب سياسة **التوقّع/الالتزام** اليومي؟ معكوس <see cref="IsDailyExpectedBusinessDay(DateOnly)"/>.
    /// الصيغة صفريّة الوسائط = سلوك غير-المبيعات (الجمعة+السبت عطلة). **لا تُستخدَم في بوابة إنشاء التقرير**.
    /// </summary>
    public static bool IsDailyHoliday(DateOnly date) =>
        !IsDailyExpectedBusinessDay(date);

    /// <summary>
    /// هل اليوم عطلة أسبوعية مُدرِكًا لسريان سبت المبيعات؟ معكوس <see cref="IsDailyExpectedBusinessDay(DateOnly, bool)"/>:
    /// لمبيعات، السبت ≥ الأرضية **ليس** عطلة (يوم عمل)؛ الجمعة تبقى عطلة دائمًا.
    /// </summary>
    public static bool IsDailyHoliday(DateOnly date, bool saturdayEnabled) =>
        !IsDailyExpectedBusinessDay(date, saturdayEnabled);

    /// <summary>
    /// **بوابة إنشاء التقرير اليومي** (DAILY-BUSINESS-DAY-COMPLIANCE-R1 — قرار إنشاء يوم السبت):
    /// سياسة **مستقلّة** عن التوقّع/الالتزام تحافظ على سلوك الإنشاء السابق حرفيًّا:
    /// **الجمعة فقط** ممنوعة من إنشاء التقرير اليومي، و**السبت مسموح** (تقرير فعليّ طوعيّ).
    /// تقرير السبت الطوعيّ يظهر في التقارير المقدَّمة لكنه لا يدخل Expected/Compliance ولا يولّد Missing/Reminder.
    /// **لا تربط هذه الدالّة بـ <see cref="IsDailyHoliday"/> (الجمعة+السبت)** حتى لا يُحظَر إنشاء السبت.
    /// </summary>
    public static bool IsDailySubmissionBlockedDay(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Friday;

    /// <summary>
    /// **الأيام اليومية المتوقَّعة** لدورة (Sat→Fri) حتى «اليوم» (شامل الحدّ الأدنى المؤسّسي):
    /// نافذة الدورة = <see cref="CycleRange"/> (لا WeekRange الخميس→الأربعاء)، مقيَّدة بـ
    /// <see cref="ApplicabilityFloorPolicy.IsDailyDateApplicable"/> (أرضية الإطلاق) و
    /// <see cref="IsDailyExpectedBusinessDay(DateOnly)"/> (الأحد→الخميس). الصيغة صفريّة-السبت
    /// = سلوك غير-المبيعات حرفيًّا (لا سبت متوقَّع). مصدر مشترك لكل سطوح «المتوقّع».
    /// </summary>
    public static List<DateOnly> DailyExpectedDates(string cycleKey, DateOnly today) =>
        DailyExpectedDates(cycleKey, today, saturdayEnabled: false);

    /// <summary>
    /// **الأيام اليومية المتوقَّعة** لدورة مُدرِكًا لسريان سبت المبيعات: نفس المنطق مع تفويض تصنيف السبت
    /// إلى <see cref="IsDailyExpectedBusinessDay(DateOnly, bool)"/> (السبت متوقَّع لمبيعات ابتداءً من الأرضية).
    /// الجمعة تبقى محجوبة والأحد→الخميس متوقَّعة كما هي؛ الأرضية المؤسّسية للإطلاق تُطبَّق دائمًا.
    /// </summary>
    public static List<DateOnly> DailyExpectedDates(string cycleKey, DateOnly today, bool saturdayEnabled)
    {
        var (start, end) = CycleRange(cycleKey);
        var cap = today < end ? today : end;
        var floor = ApplicabilityFloorPolicy.OrganizationalReportingLaunchFloor;
        var dates = new List<DateOnly>();
        for (var d = start; d <= cap; d = d.AddDays(1))
            if (ApplicabilityFloorPolicy.IsDailyDateApplicable(d, floor) && IsDailyExpectedBusinessDay(d, saturdayEnabled))
                dates.Add(d);
        return dates;
    }

    /// <summary>
    /// صيغة مُريحة تشتقّ تفعيل السبت من المسمّى الوظيفيّ عبر <see cref="SaturdayEnabledForJobRole"/>
    /// (SALES_B2B/SALES_B2C ⇒ السبت متوقَّع من الأرضية؛ غيرهم ⇒ سلوك غير-المبيعات).
    /// </summary>
    public static List<DateOnly> DailyExpectedDates(string cycleKey, DateOnly today, string? jobRoleCode) =>
        DailyExpectedDates(cycleKey, today, SaturdayEnabledForJobRole(jobRoleCode));

    /// <summary>«الثلاثاء 14 يوليو 2026».</summary>
    public static string ArFullDateLabel(DateOnly date) =>
        $"{ArDayName(date)} {date.Day} {ArMonths[date.Month - 1]} {date.Year}";

    /// <summary>مفتاح اليوم السابق (تقويميًّا).</summary>
    public static string PreviousDayKey(DateOnly date) => DayKey(date.AddDays(-1));

    /// <summary>مفتاح اليوم التالي (تقويميًّا).</summary>
    public static string NextDayKey(DateOnly date) => DayKey(date.AddDays(1));
}
