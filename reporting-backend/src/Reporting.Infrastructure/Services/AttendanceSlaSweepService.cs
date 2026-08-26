using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reporting.Application.Attendance;
using Reporting.Application.Security;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// P2-ATT-006 — مُشغِّل كنس SLA لوقائع الحضور.
///
/// <para>هذا الكنس **إجراء نظام لا إجراء مستخدم**: يُشعِر الموظّفين، ويُنهي نوافذ الردّ المنقضية،
/// ويُحيل إلى الموارد البشريّة. لا يُنتج خصمًا ولا حركة رصيد ولا أثرًا على الرواتب في أيّ مسار.</para>
///
/// <para>معطّل ما لم يُفعَّل <c>Phase2:AttendanceEnabled</c>؛ والعلم إخفاء للميزة لا تفويض.</para>
/// </summary>
public class AttendanceSlaSweepService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Phase2FeatureOptions _options;
    private readonly ILogger<AttendanceSlaSweepService> _logger;

    public AttendanceSlaSweepService(
        IServiceScopeFactory scopeFactory,
        IOptions<Phase2FeatureOptions> options,
        ILogger<AttendanceSlaSweepService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AttendanceEnabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                // دورة فاشلة لا تُسقِط الخدمة؛ الدورة التالية تُعيد المحاولة على نفس الصفوف.
                _logger.LogError(ex, "AttendanceSlaSweep cycle failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>دورة واحدة بنطاق خدمات مستقلّ — مكشوفة كي يبقى الاختبار حتميًّا بلا انتظار.</summary>
    public async Task<AttendanceSlaSweepResult?> RunOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAttendanceService>();

        var result = await service.RunSlaSweepAsync(ct);
        if (!result.Succeeded) return null;

        var value = result.Value!;
        if (value.NotifiedEmployees + value.TimedOut + value.SentToHr > 0)
            _logger.LogInformation(
                "AttendanceSlaSweep notified={Notified} timedOut={TimedOut} sentToHr={SentToHr}",
                value.NotifiedEmployees, value.TimedOut, value.SentToHr);

        return value;
    }
}
