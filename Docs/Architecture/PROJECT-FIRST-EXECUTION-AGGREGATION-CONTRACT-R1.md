# PROJECT-FIRST-EXECUTION-AGGREGATION-CONTRACT-R1

> عقد تجميع «المشروع أوّلًا» (Project-First) لتقارير التنفيذ التنفيذية — **قراءة فقط، حتميّ، ثابت**.
> محرّك واحد صريح يلخّص تسليمات التقارير التنفيذية القائمة على المشاريع حسب: المشروع، الموظّف، الفريق/Pod،
> الدور، فترة الرفع، وقالب التقرير. لا يتحوّل إلى محرّك مهام أو Workflow. يدعم التقارير الحالية والمستقبلية
> القائمة على المشاريع دون إعادة تصميم دورة الاعتماد.

الإصدار: R1-V2 · الحالة: RC · النوع: عقد معماري (Architecture Contract) · لا هجرة قاعدة بيانات · لا واجهة أمامية.

---

## 1. الغرض والنطاق (Purpose & Scope)

يوفّر هذا العقد **مصدر حقيقة واحدًا** لتجميع تسليمات التقارير التنفيذية التي تُبنى بنمط «المشروع أوّلًا»،
حيث تُدخَل كل الأرقام **داخل** قسم المشاريع المتكرّر (Project Repeatable Section) لكل مشروع على حدة.
المحرّك:

- **قراءة فقط (read-only)**: لا يُنشئ/يُعدّل/يحذف أيّ تسليم أو قالب أو خطوة اعتماد.
- **حتميّ (deterministic)**: نفس المدخلات ⇒ نفس المخرجات دائمًا (دوال جمع وقسمة آمنة).
- **ثابت (stable)**: عقد مفاتيح صريح (v5) لا يتغيّر بتغيّر ترتيب الحقول أو تسمياتها المعروضة.
- **مستقلّ**: منفصل تمامًا عن مسار المبيعات (B2C/B2B) وعن المحرّك الموحّد المسطّح (Family B).

**خارج النطاق (Out of scope):** إنشاء قوالب، نشر إصدارات، إعادة كتابة بيانات تاريخية، Workflow، جدولة،
بريد، لوحة أمامية، أيّ كيان/هجرة جديدة.

## 2. الأدوار المدعومة (Supported Roles)

يدعم العقد أدوار التنفيذ القائمة على المشاريع:

| الدور | القالب التنفيذي | عائلة المقاييس الأساسية |
|---|---|---|
| Content Creator (كاتب المحتوى) | `تقرير كاتب المحتوى الأسبوعي` | الإنتاج (Production) |
| Graphic Designer (التصميم) | `تقرير فريق التصميم` | الإنتاج |
| Video Editor / Motion (الفيديو) | `تقرير فريق الفيديو` | الإنتاج |
| Moderation (المديرشن) | `تقرير المديرشن الأسبوعي` | التفاعل (Moderation family) |
| Account Manager, Media Buyer, SEO/Article Writer | (يُستوعَبون مستقبلًا عبر نفس العقد) | — |

القوالب الأربعة أعلاه هي `ExecutionTemplateTitles` — المحرّك يقرأ **حصريًّا** التسليمات التي تنتمي لأيّ إصدار
من هذه العناوين الأربعة.

## 3. تصنيف المقاييس (Metric Classification)

يميّز العقد صراحةً بين أربع فئات:

1. **مقاييس تنفيذ مشتركة (Shared execution metrics):** Planned، Completed، Approved، Revisions، Delayed —
   مشتركة بين قوالب الإنتاج (محتوى/تصميم/فيديو).
2. **مقاييس خاصّة بالدور (Role-specific metrics):** عائلة المديرشن (MessagesIn، Responses، IssueComments،
   Escalations) — تُملأ فقط لقالب المديرشن، وعائلة الإنتاج فارغة له قصدًا لمنع الازدواج.
3. **حالة المشروع المطبَّعة (Normalized project status):** RAG ثلاثيّ (Healthy/Stable/NeedsIntervention/Unspecified).
4. **محتوى تفصيليّ غير قابل للتجميع (Detailed non-aggregatable content):** الملاحظات النصية الحرّة وأسباب
   التأخير — تُقرأ في التقرير التفصيلي لا في التجميع.

## 4. عقد حالة المشروع (Status Contract — RAG)

`ProjectFirstExecutionSchema.NormalizeStatus(raw)` يطبّع أيّ نصّ حالة إلى قيمة آليّة ثابتة:

| المدخل (أمثلة) | الناتج الآليّ |
|---|---|
| `🟢 ممتاز` / `ممتازة` | `healthy` |
| `🟡 مستقر` / `مستقر` | `stable` |
| `🔴 يحتاج تدخل` / `يحتاج تدخل عاجل` | `needs_intervention` |
| فارغ / مسافات / null / قيمة قديمة غير معروفة | `unspecified` |

المطابقة **متسامحة** مع الرمز اللونيّ والمسافات، لكنها **حرفيّة** (لا تطبيع تشكيل — تُطابَق «تدخل» بلا شدّة).

## 5. صيغ التجميع (Aggregation Formulas)

الجمع لكل مفتاح عبر خريطة المفاتيح `MapFor(title)`، ثم النِسب عبر قسمة آمنة `Pct(num,den) = den>0 ? Round(num/den*100,1) : 0`:

- **CompletionRate** = `Pct(Completed, Planned)`
- **ApprovalRate** = `Pct(Approved, Completed)`
- **PublishRate** = `Pct(Published, Approved)`
- **ResponseRate** = `Pct(Responses, MessagesIn)`

مقياس العنوان (Headline) للمقارنة الدوريّة = `Completed + Responses` (يجمع مخرجات الإنتاج والمديرشن بلا ازدواج
لأن كلًّا منهما مصدره منفصل). كل الجمع بأنواع `decimal` لتفادي أخطاء الفاصلة العائمة.

## 6. أهليّة التسليم (Submission Eligibility)

يُحتسب التسليم إذا وفقط إذا:

- ينتمي `ReportTemplateVersionId` إلى أحد `ExecutionTemplateTitles` الأربعة، و
- `Status != Draft` (تُقرأ Submitted/Returned/Approved*/Escalated/Closed/Visible)، و
- يجتاز مرشّحات الفلتر (الفترة/الفريق/الموظّف) إن وُجدت، و
- المُدخِل (submitter) ضمن نطاق المستخدم **أو** المشروع مرئيّ عبر IClientProjectAccess.

المسودّات (Draft) تُستبعَد على مستوى SQL فلا تدخل عدّاد `SubmissionsConsidered`.

## 7. معالجة تكرار المشروع (Duplicate Project Handling) — استراتيجية A

**Strategy A = التراكم/الجمع (SUM/accumulate).** إن ظهر نفس `projectId` عدّة مرّات — في نفس التسليم أو عبر
تسليمات/موظّفين مختلفين ضمن نفس نطاق التجميع — **تُجمَع** كل المقاييس، ويُحسَب `Contributors` كعدد الموظّفين
المميّزين، وتُراكَم حصيلة الحالة (`Status.Total` = عدد المدخلات). هذا صريح وحتميّ ومُثبَت باختبار
`DuplicateProject_AccumulatesAcrossEntriesAndSubmissions_StrategyA`.

## 8. الطبيعة المعمارية (Backend-only, Read-only)

- خدمة واحدة `ProjectFirstExecutionAggregationService` (Infrastructure) خلف واجهة
  `IProjectFirstExecutionAggregationService` (Application).
- **لا كيانات، لا جداول، لا هجرات، لا تعديل ModelSnapshot، لا واجهة أمامية، لا مهام خلفية، لا بريد، لا مجدول.**
- التغييرات على الملفّات القائمة **إضافيّة بحتة**: دالّة `ReportCalendarPolicy.PreviousPeriodKey` (جديدة) وسطر
  تسجيل DI واحد.

## 9. الأمان والنطاق (Security & Scope)

نطاق مزدوج المحور مفروض عند القراءة:

- **IScopeResolver** (شجرة الأدوار/الإدارة): governance/company ⇒ `SeesAll`؛ own ⇒ الذات؛ team ⇒ الذات + المرؤوسون
  المباشرون؛ department ⇒ الشجرة الفرعية (BFS).
- **IClientProjectAccess** (رؤية المشاريع): إن لم يكن `SeesAll`، تُحمَّل `ProjectIds` المرئيّة؛ ويُقبَل المدخل إن
  كان المُدخِل ضمن النطاق **أو** كان مشروعه ضمن `ProjectIds`.
- المدخلات غير المرئيّة تُتخطّى **بصمت** (لا تظهر في التشخيص لمنع تسريب وجود بيانات خارج النطاق — منع IDOR/BOLA).
- كل منفذ يتحقّق `IsAuthenticated` ويعيد `auth.unauthenticated` عند الفشل ⇒ 401.

مُثبَت باختبار `Scope_UnrelatedEmployeeSeesNothing_AndAnonymous401`: موظّف غير مرتبط (own) ⇒ صفوف فارغة +
`ViewLevel="self"`؛ غير مصادَق ⇒ 401 على المنافذ الأربعة.

## 10. عقد الفلاتر (Filter Contract)

`ProjectFirstExecutionFilter(PeriodType?, PeriodKey?, TeamId?, EmployeeId?, ClientId?, ProjectId?)`:

- **PeriodType/PeriodKey:** إن غاب النوع واستُنتِج مفتاح أسبوعيّ صالح ⇒ Weekly ضمنًا (`EffectivePeriodType`).
- **TeamId/EmployeeId:** يُصفّى على `TeamId`/`SubmitterId` في SQL (لا مرشّح submitter عام على محور المحفظة).
- **ClientId:** يُطبَّق بعد حلّ المشاريع (مدخل خارجه ⇒ `outside_client_filter`).
- **ProjectId:** مدخل خارجه ⇒ `outside_project_filter`.

## 11. التشخيص (Diagnostics)

كل تقرير يحمل عدّادات شفّافة: `SubmissionsConsidered`، `SubmissionsIgnored`، `EntriesIgnored`،
`RowsConsidered`، `RowsIgnored`، و`IgnoredReasons` (قاموس سبب→عدد). الأسباب المعرّفة:
`empty_project_entry`، `outside_project_filter`، `outside_client_filter`. المدخلات خارج النطاق الأمنيّ
لا تُدرَج في التشخيص (سياسة عدم التسريب).

## 12. المقارنة الدوريّة (Period Comparison)

`PreviousPeriodKey(PeriodType, key)` يشتقّ الفترة السابقة (Weekly عبر إزاحة −7 يومًا على تقويم السبت→الجمعة،
Monthly/Quarterly مع التفاف السنة، Daily −1 يوم؛ غير ذلك ⇒ null). تُبنى `PeriodComparison(Current, Previous,
Change, ChangePercent, Trend, HasPrevious)` من مقاييس العنوان لكل صفّ. لا فترة سابقة ⇒ `HasPrevious=false`،
`Trend="none"`.

## 13. الاختبارات (Testing)

- **Unit (Phase 12):** `ProjectFirstExecutionSchemaTests` — 37 اختبارًا حتميًّا بلا قاعدة بيانات (تطبيع الحالة،
  خرائط v5 لكل قالب، اشتقاق مفتاح الفترة السابقة).
- **Integration (Phase 13):** `ProjectFirstExecutionAggregationTests` — 8 اختبارات على **قاعدة معزولة نظيفة**
  `reporting_pfe_iso` (مصنع `ProjectFirstIsolatedFactory`)، تبذر التسليمات مباشرةً عبر AppDbContext بمفاتيح v5
  الحقيقية (لأن مفاتيح القالب المبذور الفرعيّة تختلف، والمحرّك لا يعيد التحقّق من المفاتيح وقت القراءة). تغطّي:
  التجميع لكل مشروع/موظّف/Pod/عميل، الصيغ والنِسب، حالة RAG، المقارنة الدوريّة، تكرار المشروع (Strategy A)،
  الأمان/IDOR، واستبعاد المسودّات.

## 14. الانحدار (Regression)

الفرق ضد الأصل `a86ad3b` **إضافيّ بحت**: ملفّان معدّلان (ReportCalendarPolicy +PreviousPeriodKey،
DependencyInjection +سطر DI) + 8 ملفّات جديدة. **لا هجرة، لا تغيير ModelSnapshot، لا تعديل كيان، لا تعديل اختبار
قائم.** `has-pending-model-changes` = «No changes». الوحدات: 232/232 خضراء.

## 15. مفاتيح v5 لكل قالب (Real Production Keys)

| القالب | Planned | Completed | Approved | Revisions | Delayed | Moderation family |
|---|---|---|---|---|---|---|
| Content | `required_pieces` | `delivered_pieces` | `approved_first_time` | `returned_once`,`returned_more` | `late_pieces` | (فارغة) |
| Design | `requested_designs` | `delivered_designs` | `approved_first_time` | `revised_designs` | `late_designs` | (فارغة) |
| Video | `requested_videos` | `delivered_videos` | `approved_first_time` | `revised_videos` | `late_videos` | (فارغة) |
| Moderation | (فارغة) | (فارغة) | (فارغة) | (فارغة) | (فارغة) | `incoming_messages`,`answered_messages`,`complaints`,`escalations` |

مفتاح الحالة الموحّد: `project_status`.

## 16. بنية ValueJson (Data Shape)

قسم المشاريع المتكرّر يُخزَّن كـ `List<{ projectId: Guid?, answers: Dictionary<string, JsonElement> }>`.
الأرقام مخزّنة **داخل** `answers` لكل مشروع، ومتسامحة مع الأرقام العربية-الهندية عبر NumericNormalizer. المحرّك
لا يعيد التحقّق من المفاتيح مقابل تعريفات الحقول وقت القراءة — يقرأ مفاتيح v5 كما هي.

## 17. المنافذ (API Surface)

`[Authorize] Route("api/reporting/project-execution")` — أربع نقاط GET قراءة فقط:

- `GET /projects` ⇒ `ProjectFirstByProjectRow`
- `GET /employees` ⇒ `ProjectFirstByEmployeeRow`
- `GET /pods` ⇒ `ProjectFirstByPodRow`
- `GET /clients` ⇒ `ProjectFirstByClientRow`

جميعها تقبل `periodType, periodKey, teamId, employeeId, clientId, projectId` وتعيد `ProjectFirstExecutionReport<TRow>`.

## 18. الاستقلال عن المحرّكات الأخرى (Isolation)

- مستقلّ عن محرّك Phase 4 (B2C/B2B) — لا يقرأ قوالبه.
- مستقلّ عن محرّك ERDS Phase 5 (Pod Execution، جداول TableGrid) — عائلة مختلفة، مفاتيح مختلفة.
- لا يستهلك `IKpiTemplateService` ولا يمسّ ComputeScore/الاعتماد/Workflow/CurrentApproverId.

## 19. شروط التوقّف الإلزاميّة (Mandatory Stop Conditions)

يتوقّف التنفيذ فورًا إن: لزمت هجرة؛ تغيّر ModelSnapshot؛ لزمت كيانات RC-4؛ لزمت إعادة كتابة تاريخية؛ تعذّر فرض
الصلاحيات؛ كشفت الخدمة بيانات غير مصرّح بها؛ صارت الصيغ غير حتميّة؛ تعذّرت صراحة معالجة التكرار؛ انحدرت تقارير
قائمة ماديًّا؛ لزم تعديل الإنتاج؛ تغيّر عدّ هجرات RC؛ لزم نشر واجهة أمامية؛ تعذّر نجاح الاختبارات على القاعدة
المعزولة. **لم يُطلَق أيّ شرط** في R1-V2.

## 20. حدود العقد وتسليم المستقبل (Contract Boundaries & Future Handoff)

هذا العقد **تجميعيّ قراءة فقط** ولا يُصبح محرّك مهام/Workflow. القوالب المستقبلية القائمة على المشاريع
(Content Creator v6، تقارير التصميم/الفيديو الموسّعة، Account Manager) تُستوعَب بإضافة عناوينها إلى
`ExecutionTemplateTitles` وخرائط `MapFor` بمفاتيح v5 الخاصّة بها — **دون** تعديل صيغ التجميع أو نموذج الأمان أو
دورة الاعتماد. تسليم Content Creator v6 موثَّق في تقرير القبول (Phase 20/21).
