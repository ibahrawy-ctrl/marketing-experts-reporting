namespace Reporting.Application.Workflow;

/// <summary>
/// P2-ATT-004 — نواة Workflow **صغيرة ومحايدة** تُبنى عليها آلة حالات الحضور، وتصلح لإعادة
/// الاستعمال لاحقًا بلا إعادة هيكلة.
///
/// **ما ليست عليه عمدًا:** ليست محرّك Workflow عامًّا مُعرَّفًا بالبيانات. دورات التقارير
/// والإجازات القائمة **لم تُمسّ ولم تُهاجَر إليها** (§5/§8)؛ المحرّك العامّ تذكرة مستقبليّة
/// موثَّقة لا عمل في هذه المرحلة.
/// </summary>
public sealed record WorkflowTransitionResult(bool Allowed, string? ErrorCode, string? ReasonAr)
{
    public static WorkflowTransitionResult Ok() => new(true, null, null);

    public static WorkflowTransitionResult Deny(string errorCode, string reasonAr) =>
        new(false, errorCode, reasonAr);
}

/// <summary>
/// مُدقّق الانتقالات: يجيب «هل يجوز هذا المُشغِّل من هذه الحالة؟» ولا يعرف شيئًا عن المستخدم
/// ولا عن قاعدة البيانات — دالّة نقيّة قابلة للاختبار وحدويًّا بالكامل.
/// </summary>
public interface IWorkflowTransitionValidator<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    /// <summary>هل الانتقال مسموح شكليًّا من <paramref name="from"/> بهذا المُشغِّل.</summary>
    WorkflowTransitionResult Validate(TState from, TTrigger trigger);

    /// <summary>الحالة الهدف. يُنادى بعد <see cref="Validate"/> ناجحًا فقط.</summary>
    TState Target(TState from, TTrigger trigger);

    /// <summary>كلّ المُشغِّلات المسموحة شكليًّا من حالة ما — لتغذية الواجهة بأزرار حقيقيّة.</summary>
    IReadOnlyCollection<TTrigger> AllowedTriggers(TState from);
}

/// <summary>
/// مُخوِّل الفاعل: يجيب «هل يحقّ لهذا الفاعل تشغيل هذا المُشغِّل؟». منفصل عن مُدقّق الانتقالات
/// عمدًا لأنّ «الانتقال جائز» و«أنت من يملك تشغيله» سؤالان مختلفان، وخلطهما مصدر ثغرات.
/// </summary>
public interface IWorkflowActorAuthorizer<TTrigger, TContext>
    where TTrigger : struct, Enum
{
    WorkflowTransitionResult Authorize(TTrigger trigger, TContext context);
}

/// <summary>
/// كاتب أحداث الخطّ الزمنيّ: يُلحِق سجلًّا غير قابل للتعديل بكلّ انتقال، ويكتب الأثر التدقيقيّ.
/// الإلحاق جزء من الانتقال لا خطوة اختياريّة بعده.
/// </summary>
public interface IWorkflowEventWriter<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    Task AppendAsync(
        Guid entityId,
        Guid actorUserId,
        TTrigger trigger,
        TState fromState,
        TState toState,
        string? comment = null,
        string? changesJson = null,
        CancellationToken ct = default);
}
