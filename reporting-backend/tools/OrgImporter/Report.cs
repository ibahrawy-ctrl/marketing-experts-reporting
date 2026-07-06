namespace OrgImporter;

/// <summary>تقرير تشخيصي لما سيُنفَّذ (أو نُفِّذ) — يُطبَع في نهاية كل تشغيل.</summary>
internal sealed class Report
{
    public List<string> UsersCreated { get; } = new();
    public List<string> UsersExisting { get; } = new();
    public List<string> AdminsExcluded { get; } = new();
    public List<string> DeptsCreated { get; } = new();
    public List<string> DeptsUpdated { get; } = new();
    public List<string> JobsCreated { get; } = new();
    public List<string> JobsUpdated { get; } = new();
    public List<string> TeamsCreated { get; } = new();
    public List<string> TeamsUpdated { get; } = new();
    public int RelationshipsSet { get; set; }
    public List<string> Warnings { get; } = new();

    public void Print()
    {
        Section("المستخدمون — سيُنشَأون", UsersCreated);
        Section("المستخدمون — قائمون (تُحدَّث روابطهم فقط)", UsersExisting);
        Section("حسابات Admin — مستثناة كليًّا (لا تُلمَس)", AdminsExcluded);
        Section("الإدارات — تُنشَأ", DeptsCreated);
        Section("الإدارات — تُحدَّث", DeptsUpdated);
        Section("المسميات الوظيفية — تُنشَأ", JobsCreated);
        Section("المسميات الوظيفية — تُحدَّث", JobsUpdated);
        Section("الفِرق — تُنشَأ", TeamsCreated);
        Section("الفِرق — تُحدَّث", TeamsUpdated);

        Console.WriteLine();
        Console.WriteLine($"روابط المستخدم المضبوطة (مدير/إدارة/فريق/مسمى): {RelationshipsSet}");

        Console.WriteLine();
        Console.WriteLine("ملخّص الأعداد:");
        Console.WriteLine($"  مستخدمون يُنشَأون     : {UsersCreated.Count}");
        Console.WriteLine($"  مستخدمون قائمون      : {UsersExisting.Count}");
        Console.WriteLine($"  Admin مستثنى         : {AdminsExcluded.Count}");
        Console.WriteLine($"  إدارات (إنشاء/تحديث) : {DeptsCreated.Count}/{DeptsUpdated.Count}");
        Console.WriteLine($"  مسميات (إنشاء/تحديث) : {JobsCreated.Count}/{JobsUpdated.Count}");
        Console.WriteLine($"  فِرق (إنشاء/تحديث)    : {TeamsCreated.Count}/{TeamsUpdated.Count}");
        Console.WriteLine($"  تحذيرات              : {Warnings.Count}");

        if (Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("التحذيرات:");
            foreach (var w in Warnings) Console.WriteLine($"  ⚠ {w}");
        }
    }

    private static void Section(string title, IReadOnlyList<string> items)
    {
        Console.WriteLine();
        Console.WriteLine($"{title} ({items.Count}):");
        if (items.Count == 0) { Console.WriteLine("  — لا شيء —"); return; }
        foreach (var i in items) Console.WriteLine($"  • {i}");
    }
}
