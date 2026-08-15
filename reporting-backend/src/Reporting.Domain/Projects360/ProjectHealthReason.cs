namespace Reporting.Domain.Projects360;

/// <summary>
/// سبب واحد من أسباب الصحّة (CPW-R3 · §9-8) — يُجيب عن سؤال «**لماذا** هذا اللون؟».
///
/// <para>
/// **لماذا وُجد أصلًا؟** لأنّ اللون وحده حكم بلا تفسير: مشروع «أحمر» بلا سبب يدفع المدير إلى
/// التخمين. السبب المُرمَّز يجعل الحكم **قابلًا للمراجعة والاختبار**: الاختبار يؤكّد الرمز لا النصّ.
/// </para>
///
/// <para>
/// **قيمة مشتقّة لا مُخزَّنة**: الأسباب تُشتقّ حتميًّا من نفس المكوّنات التي أنتجت
/// <see cref="ProjectHealthSnapshot.Score"/>، فلا يُضاف لها عمود ولا جدول — وهذا ما يُبقي
/// أثر §18-2 على **12 عمودًا** على <c>projects</c> بلا زيادة.
/// </para>
/// </summary>
/// <param name="Code">رمز مستقرّ من <see cref="ProjectHealthReasonCodes"/> — معرّف للآلة لا نصّ للعرض.</param>
/// <param name="Detail">قيمة رقميّة مساندة (النتيجة أو الفجوة) تُغني رسالة العرض. اختياريّة.</param>
public sealed record ProjectHealthReason(string Code, decimal? Detail = null);
