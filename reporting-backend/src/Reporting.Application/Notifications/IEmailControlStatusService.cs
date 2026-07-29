namespace Reporting.Application.Notifications;

/// <summary>
/// EMAIL-CONTROL-CENTER-LIVE-MODE-STATUS-R1 — قراءة الحالة التشغيليّة الحيّة لقناة البريد.
///
/// عقد قراءة فقط: لا كتابة، لا اتّصال SMTP، لا استدعاء لأيّ مهمّة مجدوَلة، ولا كشف لأيّ سرّ.
/// منفصل عن <see cref="IEmailControlService"/> عمدًا كي لا يمسّ مسارات القوالب/القواعد/التذكير اليدويّ.
/// </summary>
public interface IEmailControlStatusService
{
    Task<EmailControlCenterStatusDto> GetStatusAsync(CancellationToken ct = default);
}
