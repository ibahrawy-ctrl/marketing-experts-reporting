# تقرير تنفيذ الإصلاح — DEF-P123-RC-001

| الحقل | القيمة |
|---|---|
| معرّف العيب | `DEF-P123-RC-001` |
| الخطورة | P2 — إفشاء وجود بلاغ سابق للإرسال لموظّفه الموضوع |
| القاعدة الأساس | `8479d374238b71731996ad73d20d1485701d2053` (وسم develop المدموج الحاكم) |
| فرع الإصلاح | `feature/p123-rc001-attendance-list-privacy` |
| شجرة عمل معزولة | `.claude/worktrees/p123-rc001-20260826` |
| التاريخ | 26 أغسطس 2026 |

---

## 1) السلوك المقيس قبل الإصلاح

| السطح | المتوقَّع | المقيس قبل الإصلاح | الحكم |
|---|---|---|---|
| `GET /api/attendance/{id}` بحالة `Draft` والمستخدم هو الموضوع | `404 attendance.not_found` | `404` | سليم |
| `GET /api/attendance` والمستخدم هو الموضوع | لا تظهر الواقعة في `items` | `200` وتظهر الواقعة في `items` | **فاشل — إفشاء** |
| `totalCount` في القائمة نفسها | لا يعدّها | يعدّها | **فاشل — إفشاء عبر العدّاد** |

نصّ الانحراف الحرفيّ الذي أطلقه اختبار الاتّساق قبل الإصلاح:

```
انحراف رؤية: القائمة أظهرت 30e3103a-0d45-4e49-9f83-4935f6ed7fc4 للصفة «الموضوع» بينما التفاصيل ردّت 404.
```

نتيجة الحزمة قبل الإصلاح على قاعدة نظيفة معزولة `reporting_rc001_pre`:
`Failed: 8, Passed: 5, Skipped: 0, Total: 13, Duration: 2 s`.

---

## 2) السبب الجذريّ

سطحا القراءة كانا يحملان **قاعدتَي رؤية منفصلتين** كتبتا مرّتين، فانحرفتا:

| السطح | القاعدة المستعملة | الموضع |
|---|---|---|
| العنصر المفرد (التفاصيل والأحداث والمرفقات والكتابة) | `AttendanceAccess.CanViewIncident` | `AttendanceService.LoadVisibleAsync` / `LoadForWriteAsync` |
| القائمة (`ListAsync`) | شرط `Where` مكتوب يدويًّا داخل الخدمة | `AttendanceService.BuildScopedQueryAsync` |

`CanViewIncident` كانت تحمل حدّ ما قبل الإرسال (`if (ctx.IsSelf) return !IsPreSubmission(status);`)، بينما بنى `BuildScopedQueryAsync` نطاقه من **الملكيّة والنطاق التنظيميّ وحدهما** بلا أيّ اعتبار للحالة:

```csharp
return query.Where(i =>
    i.SubjectUserId == me || i.ReportedByUserId == me || visibleUsers.Contains(i.SubjectUserId));
```

فالموضوع كان يدخل من الفرع الأوّل مهما كانت الحالة ⇒ تظهر له المسودّة.

---

## 3) شكل الإصلاح

**لم يُعالَج في React، ولا بإخفاء بعد الجلب.** العلاج نُقل إلى **مصدر حقيقة واحد** في طبقة التطبيق، ودخل **داخل الاستعلام** فسبق `Count` و`Skip/Take` والإسقاط والتسلسل معًا.

### 3.1 `Reporting.Application/Attendance/AttendanceAccess.cs`

1. **رُفِعت** حالات ما قبل الإرسال إلى مصفوفة عامّة واحدة تستهلكها القاعدتان معًا:

```csharp
public static readonly AttendanceIncidentStatus[] PreSubmissionStatuses =
{
    AttendanceIncidentStatus.Draft,
    AttendanceIncidentStatus.Cancelled
};

public static bool IsPreSubmission(AttendanceIncidentStatus status) =>
    Array.IndexOf(PreSubmissionStatuses, status) >= 0;
```

فلا يمكن أن ينحرف السطحان بتعديل أحدهما وحده بعد اليوم.

2. **أُضيفت** نظيرة `CanViewIncident` على مستوى الاستعلام، **بترتيب الفروع نفسه وبالمصفوفة نفسها**:

```csharp
public static Expression<Func<AttendanceIncident, bool>> VisibleIncidentPredicate(
    Guid viewerUserId, bool canReviewOrEscalate, bool isOperationalSupervisor,
    bool seesAllSubjects, IReadOnlyCollection<Guid> scopedSubjectUserIds)
{
    var preSubmission = PreSubmissionStatuses;   // متغيّر محلّيّ ⇒ يُقوَّم كمعامل استعلام (= ANY)
    var scoped = scopedSubjectUserIds as IList<Guid> ?? scopedSubjectUserIds.ToList();

    return i => i.SubjectUserId == viewerUserId
        ? !preSubmission.Contains(i.Status)
        : i.ReportedByUserId == viewerUserId
          || canReviewOrEscalate
          || (isOperationalSupervisor && (seesAllSubjects || scoped.Contains(i.SubjectUserId)));
}
```

### 3.2 `Reporting.Infrastructure/Services/AttendanceService.cs`

أُعيد توصيل `BuildScopedQueryAsync` بالمُسنِدة المشتركة بدل الشرط المكتوب يدويًّا، مع الحفاظ على تحسين الأداء الأصليّ (حامل مفتاح المراجعة/التصعيد لا يُحلّ نطاقه إطلاقًا).

---

## 4) القواعد المطلوبة — إثبات التحقّق

| # | القاعدة | الحالة | الدليل |
|---|---|---|---|
| 1 | الموضوع لا يرى `Draft` | محقّقة | `Subject_List_DoesNotContain_DraftIncident` |
| 2 | الموضوع لا يرى `Cancelled` السابقة للإرسال | محقّقة | `Subject_CannotSee_CancelledPreSubmissionIncident` |
| 3 | الموضوع يراها بعد أوّل حالة إرسال رسميّة | محقّقة | `Subject_CanSee_Incident_AfterSubmission` |
| 4 | المُبلِّغ يرى مسودّته داخل نطاقه | محقّقة | `Reporter_CanSee_OwnDraftIncident` |
| 5 | المراجع المخوّل يرى حسب الصلاحية والنطاق | محقّقة | `AuthorizedReviewer_CanSee_DraftWithinScope` |
| 6 | غير المرتبط لا يكتشفها | محقّقة | `UnrelatedActor_CannotDiscoverDraft` |
| 7 | خارج النطاق يُرَدّ بـ404 | محقّقة | `OutOfScopeActor_Gets404` |
| 8 | لا يتسرّب الوجود من `totalCount`/الترقيم | محقّقة | `Subject_List_TotalCount_DoesNotReveal_DraftIncident` · `Pagination_DoesNotLeakHiddenDraft` |
| 9 | لا تقييم على العميل | محقّقة | `VisibleIncidentPredicate_IsTranslatedToSql_NotEvaluatedOnClient` — يقرأ `ToQueryString()` ويُثبِت وجود `Status`/`SubjectUserId`/`ReportedByUserId` في نصّ SQL |
| 10 | لا تحميل كامل للجدول في الذاكرة | محقّقة | نفس الاختبار: الشرط كلّه داخل `WHERE` |
| 11 | لا انحراف مستقبليّ بين السطحين | محقّقة | `Attendance_List_And_Detail_UseEquivalentVisibilityRules` + مصفوفة `PreSubmissionStatuses` الواحدة |

### حدّ الحالة — مقيس لا مفترَض

جدول `AttendanceTransitions.Map` يعطي `Draft --Submit--> Reported`، **لكنّ القياس الفعليّ** أظهر أنّ النظام يُشعِر الموظّف تلقائيًّا فتستقرّ الواقعة على `AwaitingEmployee` لا `Reported`. لذلك لم يُثبَّت الاختبار على اسم حالة بعينها، بل على **مغادرة ما قبل الإرسال** — وهو الحدّ الحاكم فعلًا.

---

## 5) مسح كامل لأسطح قراءة الحضور (Phase 2)

| السطح | القاعدة الفعليّة | الحكم |
|---|---|---|
| `AttendanceService.ListAsync` → `BuildScopedQueryAsync` | كانت قاعدة منفصلة بلا حدّ حالة | **موضع العيب الوحيد — عولج** |
| `GetAsync` / `ListEventsAsync` / `SuggestReconciliationAsync` / `DownloadAttachmentAsync` → `LoadVisibleAsync` | `CanViewIncident` | سليم — فُحص ولم يُسجَّل عيبًا |
| `UpdateDraftAsync` / `UploadAttachmentAsync` / الانتقالات → `LoadForWriteAsync` | `CanViewIncident` | سليم |
| `Employee360Service` (`:495-497`, `:687-688`) | يستبعد `Draft` **و**`Reported` للذات — أضيق | سليم |
| `HrOperationsService` (`:385-434`) | حالات ما بعد الإرسال المفتوحة فقط، خلف `HrOperations.View` | سليم بالتصميم |
| `EmployeeChecklistService` (`:306-311`) | `AwaitingEmployee`/`AwaitingHr` للذات فقط | سليم |
| بحث/ملخّص/تصدير/عدّاد لوحة مستقلّ للحضور | لا وجود لنقطة نهاية منفصلة؛ `totalCount` هو العدّاد الوحيد | لا سطح إضافيّ |

---

## 6) نطاق التغيير — إثبات الانضباط

```
 .../Attendance/AttendanceAccess.cs                 | 58 +++++++++++++++++++++-
 .../Services/AttendanceService.cs                  | 38 ++++++++------
 2 files changed, 80 insertions(+), 16 deletions(-)
```

بالإضافة إلى ملفّ اختبار جديد واحد: `tests/Reporting.IntegrationTests/AttendanceListVisibilityTests.cs`.

- **صفر ملفّات خارج النطاق.**
- **صفر هجرات جديدة**: 45 هجرة على القاعدة `8479d374` = 45 هجرة بعد الإصلاح (مطابقة تامّة).
- `git diff --check`: نظيف.
- مسح الأسرار على الفرق كلّه وعلى ملفّ الاختبار الجديد: **لا مطابقة حقيقيّة واحدة** (المطابقة الوحيدة كانت كلمة `CancellationToken` في توقيع دالّة).

---

## 7) ملاحظة تصميميّة مسجَّلة — تضييق لا توسيع

الإصلاح **يضيّق** القائمة ولا يوسّعها أبدًا؛ الثابت المحروس هو **القائمة ⊆ التفاصيل**.

يترتّب على توحيد القاعدة أنّ من كان نطاقه `SeesAll` بلا دور قيادة تشغيليّة وبلا مفتاح مراجعة صريح (مثل `Admin`/`Ceo`/`GeneralManager` بلا `Attendance.Review`) لم يعد يستعرض وقائع غيره في القائمة — وهو **بالضبط** ما كان سطح التفاصيل يفرضه عليه أصلًا بـ404. أي إنّ التوحيد أزال توسيعًا غير مقصود كان في القائمة وحدها، ولم يمسّ أيّ صلاحية مصرَّح بها. التكامل الكامل (2188/2188) يثبت أنّ هذا التضييق لم يكسر أيّ سلوك متعاقَد عليه.

يبقى فرق تضييق **سابق للإصلاح وغير مُسرِّب** في تركيبة `[Hr, TeamLeader]`: التفاصيل أوسع من القائمة لأنّ `FieldVisibilityPolicy` يوسّع دور `Hr` إلى علاقة مؤسّسيّة بينما `ScopeResolver` لا يفعل. هذا **ليس إفشاءً** (اتّجاهه معاكس للعيب)، وهو خارج نطاق هذه التذكرة، ومسجَّل هنا للمتابعة.
