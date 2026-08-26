namespace Reporting.Application.Security;

/// <summary>
/// تفسير تصنيف حسّاسيّة الملاحظة الإداريّة (P2-SEC-001) بلا Backfill.
/// السجلّات السابقة (<c>Sensitivity == null</c>) تُقرأ <see cref="FieldSensitivity.Internal"/>
/// **داخل التطبيق فقط** — لا يُكتب شيء على البيانات التاريخيّة.
/// </summary>
public static class NoteSensitivity
{
    /// <summary>التصنيف الافتراضيّ التاريخيّ الآمن: داخليّ (لا يراه الموظّف نفسه).</summary>
    public const FieldSensitivity LegacyDefault = FieldSensitivity.Internal;

    /// <summary>يحوّل القيمة المخزَّنة إلى تصنيف فعليّ؛ القيمة غير المعروفة تُعامَل بالأشدّ لا بالأضعف.</summary>
    public static FieldSensitivity Effective(int? stored)
    {
        if (stored is null) return LegacyDefault;
        return Enum.IsDefined(typeof(FieldSensitivity), stored.Value)
            ? (FieldSensitivity)stored.Value
            : FieldSensitivity.ManagementConfidential;
    }
}
