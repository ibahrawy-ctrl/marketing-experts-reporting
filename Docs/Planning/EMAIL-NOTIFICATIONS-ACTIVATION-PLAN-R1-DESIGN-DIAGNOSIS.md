# EMAIL NOTIFICATIONS & ACTIVATION PLAN R1 — تقرير التصميم والتشخيص (قراءة فقط)

**التاريخ:** 2026-07-16
**النطاق:** تصميم/تشخيص فقط — **صفر تعديل كود · صفر نشر · صفر إرسال بريد · صفر هجرة · صفر تعديل قاعدة**.
**مصدر الحقيقة (Production-parity):** النسخة المعزولة `/private/tmp/restore-archive-governance-r1/` (git head 92b8c01، 29 هجرة) — مطابقة للإنتاج.
**بيئة الإنتاج المفحوصة (قراءة فقط):** `reporting_prod` @ VPS 187.127.72.232، الخدمة active، رأس الهجرة `20260716015239_KpiEvaluationPartialUniqueIndex`.

---

## Phase 0 — إثبات مصدر الكود

- النسخة المعزولة `/private/tmp/restore-archive-governance-r1/` تحوي **29 ملف هجرة** ورأسها `20260716015239_KpiEvaluationPartialUniqueIndex` — مطابق تمامًا لرأس هجرة الإنتاج المقروء حيًّا من `__EFMigrationsHistory`.
- شجرة `develop` (Client 360) **ليست** مطابقة للإنتاج (تحوي محاور غير منشورة) — لذلك كل التحليل أدناه من النسخة المعزولة حصرًا.

> **SOURCE PROVENANCE = PROVEN**

---

## Phase 1 — جرد نظام البريد (الحالة الفعلية على الإنتاج)

### 1.1 المساران المتوازيان للبريد (اكتشاف معماريّ محوريّ)

يوجد **مساران مستقلّان تمامًا** لا يعرف أحدهما الآخر:

| البُعد | المسار القديم (OLD) | المسار الجديد (NEW) |
|---|---|---|
| الخيارات | `EmailOptions` (قسم `"Email"`) | `EmailNotificationOptions` (قسم `"EmailNotifications"`) |
| الجدول | `email_outbox` | `email_notifications` |
| البوّابة | `Email__Enabled` (bool) | `EmailNotifications__Mode` (enum: Disabled/DryRun/Enabled) |
| الخدمة الخلفية | `EmailOutboxDispatcher` (IHostedService) | لا خدمة خلفية (R1 يدويّ فقط) |
| المُغذّي | `NotificationService.EnqueueEmailsAsync` | `EmailNotificationService.EnqueueAsync` (لكل حدث) |
| منع التكرار | `IncludedTypes`/`ExcludedTypes` allowlist | `CorrelationKey` فريد |
| حارس الإرسال | `EmailOptions.Enabled` | `IEmailSender.IsConfigured` + `Mode==Enabled` |
| الحالة على الإنتاج | **معطّل** (`Email__Enabled=false`، الجدول 0 صف) | **DryRun** (16 صف، 0 إرسال فعليّ) |

### 1.2 الخدمات الخلفية المستضافة (Hosted Services) — عددها **2 فقط**

1. **`EmailOutboxDispatcher`** — يعالج `email_outbox` **فقط إن `EmailOptions.Enabled==true`**؛ على الإنتاج معطّل ⟹ حلقة خاملة.
2. **`SubmissionReminderService`** — يُنشئ **إشعارًا داخل التطبيق** (in-app) واحدًا لكل (مستخدم، أسبوع) **فقط إن `Reminders__Enabled==true`**؛ على الإنتاج معطّل. **لا يرسل بريدًا بنفسه إطلاقًا**.

**لا Quartz، لا Hangfire، لا Cron، لا Timer آخر.** `ReportReminderService` = خدمة scoped يدويّة (تُستدعى من نقطة نهاية admin)، ليست hosted.

### 1.3 مفاتيح الإعداد على الإنتاج (مقروءة حيًّا من `/etc/reporting-api.env`)

```
Email__Enabled=false            ← المسار القديم معطّل
Email__Provider=GoogleWorkspace
Email__SmtpHost=smtp.gmail.com
Email__SmtpPort=587
Email__UseSsl=false
Email__UseStartTls=true
Email__Username=info@marketingexperts.com.sa
Email__Password=<موجود، لم يُطبع>
Email__FromEmail=info@marketingexperts.com.sa
Email__FromName=نظام خبراء التسويق للتقارير والأداء

(لا مفتاح EmailNotifications__* إطلاقًا)  ← Mode يقع على الافتراضي DryRun
(لا مفتاح Reminders__* إطلاقًا)           ← Reminders__Enabled يقع على false
(لا مفتاح App__BaseUrl)                    ← روابط البريد ستكون فارغة/نسبية
```

**ملاحظة حرجة:** SMTP **مُهيّأ بالكامل** (Gmail/Google Workspace، STARTTLS 587، حساب `info@marketingexperts.com.sa`). أي `IsConfigured` = true. لذا **العائق الوحيد أمام الإرسال الفعليّ هو البوّابتان** (`Email__Enabled=false` للقديم، غياب `EmailNotifications__Mode=Enabled` للجديد)، لا الإعداد.

### 1.4 حالة جداول البريد على الإنتاج (عدّ حيّ، قراءة فقط)

| الجدول | العدّ | التفسير |
|---|---|---|
| `email_outbox` | **0** | المسار القديم لم يُستخدَم قطّ (Enabled=false) |
| `email_notifications` | **16** | كلها **DryRun** (Status=DryRun): 11 `leave-request-created` + 5 `leave-request-needs-hr-action` |
| `email_templates` | **10** | كل القوالب المبذورة (EmailControlSeeder) |
| `email_rules` | **7** | كل القواعد المبذورة |
| `notifications` | **163** | إشعارات داخل التطبيق فعّالة (المسار الحيّ الوحيد اليوم) |

**خلاصة الحالة الحالية:** **لم يُرسَل أيّ بريد إلكترونيّ فعليّ من النظام إطلاقًا.** كل النشاط اليوم = إشعارات داخل التطبيق (in-app + SignalR) + 16 صفّ DryRun من مسار البريد الجديد (سجلّ فقط، بلا إرسال).

### 1.5 السياسات (Authorization)

- `EmailNotificationLog` = `Roles.EmailNotificationLogViewers` = **{Admin, CEO, GeneralManager, CeoSupport}** — عرض سجلّ البريد + توليد التذكيرات اليدويّ.
- `EmailControlManage` = `Roles.EmailControlManagers` = **{Admin}** حصرًا — مركز التحكم (قوالب/قواعد/معاينة).

### 1.6 قالب البريد التقنيّ (Rendering)

- `EmailHtml.Build(title, body, link)` — قالب HTML عربيّ RTL، ترويسة navy `#1F2A44`، زر CTA برتقاليّ `#E8772E` («فتح في النظام»)، كل المتغيّرات `HtmlEncoded`. الرابط يعتمد على `AppOptions.BaseUrl` (**غير مضبوط على الإنتاج** ⟹ رابط ناقص).

---

## Phase 2 — اكتشاف الأحداث (Event Discovery)

**مفتاح الحالة:** 🟢 مربوط بمسار البريد الجديد (DryRun جاهز) · 🟡 in-app فقط (لا بريد) · 🔴 لا إشعار إطلاقًا.

### 2.1 التقارير (Submissions) — `SubmissionService`

| الحدث | المُطلِق | المستقبِل الحاليّ | الحالة |
|---|---|---|---|
| `submission.submitted` | إرسال تقرير للمراجعة | المراجع (TL/Manager حسب الخطوة) | 🟡 in-app فقط |
| `submission.returned` | إرجاع للتعديل مع تعليق | المُرسِل | 🟡 in-app فقط |
| `submission.escalated` | تصعيد | المستوى التالي | 🟡 in-app فقط |
| `submission.approved` | اعتماد | المُرسِل | 🟡 in-app فقط |

> **فجوة محوريّة:** كامل سير عمل التقارير **لا يستدعي `IEmailNotificationService` إطلاقًا** — in-app حصرًا.

### 2.2 KPI — `KpiEvaluationService`

| الحدث | الحالة |
|---|---|
| `kpi.review_requested` / `submitted` / `approved` / `needs_revision` / `rejected` / `flagged` / `reopen_requested` / `reopened` | 🟡 in-app فقط (كلها) |

> **فجوة محوريّة:** كامل سير عمل KPI in-app حصرًا، بلا أيّ ربط بريد.

### 2.3 الإجازات والاستئذانات — `LeaveRequestService`

| الحدث | المستقبِل | الحالة |
|---|---|---|
| `leave-request-created` | سلسلة الاعتماد | 🟢 (11 صف DryRun على الإنتاج) |
| `leave-request-needs-hr-action` | HR | 🟢 (5 صفوف DryRun) |
| `leave-request-approved` | الموظّف | 🟢 مربوط (0 صف بعد) |
| `leave-request-rejected` | الموظّف | 🟢 مربوط (0 صف بعد) |

### 2.4 طلبات HR — `EmployeeServiceRequestService`

| الحدث | المستقبِل | الحالة |
|---|---|---|
| `hr-request-created` | HR | 🟢 مربوط |
| `hr-request-completed` | الموظّف | 🟢 مربوط |

### 2.5 الحوكمة / التصعيدات / بنود المتابعة

| الحدث | الخدمة | الحالة |
|---|---|---|
| `governance-item-created/updated` | `GovernanceItemService` | 🟢 مربوط |
| `action-item-assigned/reassigned/completed` | `GovernanceActionItemService` | 🟢 مربوط |
| `escalation-created/assigned/closed` | `GovernanceEscalationService` | 🟢 مربوط |
| `escalation.raised/updated` (القديم) | `GovernanceService` | 🟡 in-app فقط (مسار قديم موازٍ) |

### 2.6 تذكيرات التقارير — `ReportReminderService` (9 أنواع، 🟢 كلها مربوطة، DryRun يدويّ)

`report-weekly-due`, `report-daily-due`, `report-overdue`, `report-team-overdue-summary`, `report-department-overdue-summary`, `report-executive-overdue-summary`, `report-review-overdue-teamleader`, `report-review-overdue-manager`, `report-review-pending-executive`.

### 2.7 أحداث بلا أيّ إشعار (🔴)

- **إدارة المستخدمين** (`DirectoryService`/`AuthService`): إنشاء مستخدم، إعادة تعيين كلمة مرور، تغيير دور/مسمّى — 🔴 لا إشعار.
- **تأكيد البريد** (`AUTH_EMAIL_CONFIRMATION`): القالب مبذور لكن **لا منطق تأكيد منفّذ** — 🔴 هيكل مستقبليّ فقط.
- **القوالب** (نشر/أرشفة report/kpi templates): 🔴 لا إشعار.
- **الأرشيف/الاسترجاع الإداريّ** (`AdminArchiveController`): 🔴 لا إشعار (مقصود — RESTORE/ARCHIVE R1 بلا إشعارات).
- **خطط التطوير** (`DevelopmentService`: `training_need.raised`, `improvement_plan.created`): 🟡 in-app فقط.

---

## Phase 3 — تحليل سير العمل (Workflow Analysis)

### 3.1 تقرير (Submission)
`Draft → Submitted → Returned? → ApprovedByDirectManager → ApprovedByNextLevel/Escalated → Closed → Visible`.
- نقاط الإشعار الحاليّة (in-app): submitted (للمراجع)، returned (للمُرسِل)، escalated، approved.
- تكامل Approval UX R1 (منشور): Toast + قفل زر + رسائل نوعية 403/409 — **عميل بحت، لا علاقة بالبريد**.
- **لا تذكير مؤتمت مربوط بالإرسال** (SubmissionReminderService in-app فقط ومعطّل).

### 3.2 KPI
`Draft → Submitted → NeedsRevision? → Approved/Rejected → (reopen)`. كل الانتقالات in-app فقط.

### 3.3 إجازة/استئذان
`Submitted → TeamLeaderApproved → ManagerApproved → HrApproved/Rejected → (Cancelled/Revoked)`.
- التوجيه الدقيق لقائد الفريق (T-WF2) + تخطّي القائد الذاتيّ (T-WF1) + حارس الرصيد + الحدّ الشهريّ — كلها منشورة.
- البريد (الجديد) يُطلَق عند: created (لسلسلة الاعتماد)، needs-hr-action (لـ HR)، approved/rejected (للموظّف) — **كلها DryRun اليوم**.

### 3.4 طلب HR
`Submitted → InReview → Completed/Rejected`. البريد الجديد عند created (لـ HR) + completed (للموظّف).

### 3.5 حوكمة/تصعيد/متابعة
بنود/تصعيدات/إجراءات لها إسناد + تحديث + إغلاق/إكمال؛ كلها مربوطة بالبريد الجديد (DryRun).

---

## Phase 4 — مصفوفة البريد (Email Matrix)

القاعدة الموحّدة: كل حدث يمرّ عبر `EmailNotificationService.EnqueueAsync` (القلب الآمن): `Disabled→لا صفّ` · تكرار CorrelationKey→`Duplicate` · بلا بريد مستلم→`Skipped` · `DryRun→صفّ بلا إرسال` · `Enabled→Pending ثم إرسال إن IsConfigured وإلا Failed`. **لا يرمي استثناءً للمستدعي أبدًا** (لا يعطّل سير العمل).

| الحدث | المُطلِق | المستلِم | القالب المبذور | التبريد | مفتاح Idempotency | يُرسَل؟ | متى |
|---|---|---|---|---|---|---|---|
| report.reminder | ReportReminderService | الموظّف | `REPORT_REMINDER` | 1440د | `report-weekly-due:{key}:{uid}` | ✅ (بعد التفعيل) | ضمن نافذة الاستحقاق |
| report.overdue | ReportReminderService | الموظّف+TL+Manager | `REPORT_OVERDUE` | 1440د | `report-overdue:{key}:{uid}:{delay}` | ✅ | بعد الموعد |
| report.review_ready | (فجوة — غير مربوط بـ Submission) | Manager/TL | `REPORT_REVIEW_READY` | بلا | — | 🔴 يحتاج ربط | عند submitted |
| governance.escalation | GovernanceEscalationService | Manager+Gov+Admin | `GOVERNANCE_ESCALATION` | بلا | escalation CorrelationKey | ✅ | عند التصعيد |
| governance.action_item | GovernanceActionItemService | الموظّف+Gov | `GOVERNANCE_ACTION_ITEM` | بلا | action-item CorrelationKey | ✅ | عند الإسناد |
| hr_request.created | EmployeeServiceRequestService | HR | `HR_REQUEST_CREATED` | بلا | hr-request-created key | ✅ | عند الإنشاء |
| hr_request.decision | EmployeeServiceRequestService | الموظّف | `HR_REQUEST_DECISION` | بلا | hr-request-completed key | ✅ | عند القرار |
| leave-request-* | LeaveRequestService | حسب الخطوة | (لا قالب control-center مخصّص) | بلا | leave CorrelationKey | ✅ | عند الحدث |
| manual.reminder | EmailControlService | مجموعة مختارة | `MANUAL_REMINDER` | بلا | `manual-reminder:{batch}:{uid}` | DryRun فقط | يدويّ |

**ملاحظات المصفوفة:**
- **لا CC/BCC** في التصميم الحاليّ — كل مستلم صفّ مستقلّ (أفضل للـ idempotency والخصوصيّة).
- **قواعد الكبت (Suppression):** بلا بريد للمستخدم غير النشط/بلا بريد ⟹ `Skipped`. التبريد على مستوى القاعدة (`CooldownMinutes`).
- **عدم التكرار:** مضمون بنيويًّا عبر `CorrelationKey` الفريد (فهرس فريد في `email_notifications`).
- **فجوة الربط:** قالبا `REPORT_REVIEW_READY` وقواعد `report.review_ready` موجودان في مركز التحكم، لكن **`SubmissionService` لا يستدعي مسار البريد** ⟹ لا يُنتَج فعليًّا حتى يُربَط (Phase 7).

---

## Phase 5 — تدقيق القوالب (Template Audit)

**10 قوالب مبذورة** (`EmailControlSeeder`، جميعها عربيّة RTL، `IsEnabled=true`, `DefaultMode=DryRun`):

| المفتاح | التصنيف | موجود؟ | المتغيّرات | ملاحظة |
|---|---|---|---|---|
| `AUTH_EMAIL_CONFIRMATION` | Confirmation | ✅ | UserName, ConfirmationLink, ExpiryHours | 🔴 لا منطق تأكيد منفّذ (هيكل مستقبليّ) |
| `AUTH_RESEND_CONFIRMATION` | Confirmation | ✅ | UserName, ConfirmationLink, ExpiryHours | 🔴 لا منطق |
| `REPORT_REMINDER` | Reports | ✅ | UserName, ReportTitle, PeriodLabel, DueDate, Link | جاهز، يحتاج تفعيل |
| `REPORT_OVERDUE` | Reports | ✅ | + DueDate | جاهز |
| `REPORT_REVIEW_READY` | Reports | ✅ | ReviewerName, EmployeeName, ... | 🔴 غير مربوط بـ Submission |
| `GOVERNANCE_ESCALATION` | Governance | ✅ | RecipientName, Title, Severity, Link | جاهز |
| `GOVERNANCE_ACTION_ITEM` | Governance | ✅ | RecipientName, Title, DueDate, Link | جاهز |
| `HR_REQUEST_CREATED` | HR | ✅ | RecipientName, RequesterName, RequestType, Link | جاهز |
| `HR_REQUEST_DECISION` | HR | ✅ | UserName, RequestType, Decision, Link | جاهز |
| `MANUAL_REMINDER` | Common | ✅ | UserName, Subject, Body, Link | جاهز (DryRun فقط) |

**قوالب ناقصة (Missing):** لا قالب مخصّص لأحداث `leave-request-*` (تعتمد على بناء الرسالة داخل `EmailNotificationService` مباشرة لا على مركز التحكم)، ولا قوالب لـ KPI ولا للتقارير submitted/returned/approved/escalated. **لا نُنشئ قوالب في هذه المرحلة (تصميم فقط).**

**كل القوالب تستخدم `{{Variable}}`** ويُطبَّق عليها `ApplyPlaceholders` (regex) + `EmailHtml.Build` للـ RTL. العنوان يدعم المتغيّرات أيضًا.

---

## Phase 6 — خطة التفعيل (Activation Plan) — مرحليّة، لكلٍّ مخاطر/تراجع/مراقبة/تحقّق

**المبدأ الحاكم:** التفعيل يتمّ **بتغيير الإعداد فقط** (env)، لا كود، طالما الحدث مربوط بالفعل بمسار البريد الجديد. الأحداث غير المربوطة (تقارير/KPI/review_ready) تحتاج **كود** (Phase 7) قبل تفعيلها.

### المرحلة 1 — تفعيل المسار الجديد بوضع Enabled للأحداث المربوطة أصلًا (إجازات/HR/حوكمة)
- **التغيير:** `EmailNotifications__Mode=Enabled` في env + إعادة تشغيل.
- **الأثر:** الأحداث المربوطة (leave/hr/governance/action-item/escalation) تبدأ الإرسال الفعليّ عبر SMTP المُهيّأ.
- **المخاطر:** انفجار بريد فوريّ إن وُجدت أحداث متراكمة؛ روابط ناقصة (لا `App__BaseUrl`).
- **التخفيف قبلها:** ضبط `App__BaseUrl=https://reports.emarketingacademy.net` أولًا؛ التحقّق من عدم وجود دفعة متراكمة.
- **التراجع:** إعادة `EmailNotifications__Mode=DryRun` + إعادة تشغيل (فوريّ، بلا هجرة).
- **المراقبة:** `SELECT Status, count(*) FROM email_notifications GROUP BY Status` — مراقبة Failed/Sent.
- **التحقّق:** إنشاء طلب إجازة اختباريّ ⟹ صفّ Sent + وصول بريد فعليّ.

### المرحلة 2 — تذكيرات التقارير المؤتمتة (report reminders)
- **مطلوب كود (R2):** لفّ `ReportReminderService.GenerateAsync` بـ BackgroundService مُجدوِل (لا يوجد اليوم). حتى ذلك، التذكيرات يدويّة عبر `POST /api/report-reminders/dry-run/generate`.
- **قبل التفعيل الحقيقيّ:** تشغيل يدويّ DryRun وفحص العدّ/عدم التكرار.
- **التراجع:** إيقاف المُجدوِل / DryRun.

### المرحلة 3 — أحداث التقارير في سير العمل (submitted/returned/approved/escalated + review_ready)
- **مطلوب كود (Phase 7):** ربط `SubmissionService` بـ `IEmailNotificationService` (اليوم in-app فقط). قوالب `REPORT_REVIEW_READY` جاهزة.
- **المخاطر:** أعلى حجم بريد (أكثر الأحداث تكرارًا).

### المرحلة 4 — أحداث KPI
- **مطلوب كود:** ربط `KpiEvaluationService` بمسار البريد + قوالب KPI جديدة.

### المرحلة 5 — تأكيد البريد (Auth confirmation)
- **مطلوب كود كامل:** منطق توليد رمز التأكيد + نقاط نهاية + دمج مع Identity. القالبان مبذوران فقط.

### المرحلة 6 — تقاعد المسار القديم (email_outbox)
- بعد استقرار المسار الجديد، يبقى `Email__Enabled=false` دائمًا (المسار القديم ميّت فعليًّا). قرار مستقبليّ: حذف `EmailOutboxDispatcher` + `NotificationService.EnqueueEmailsAsync`.

---

## Phase 7 — تحليل الفجوات (Gap Analysis)

| الفجوة | الخطورة | ملفات مُقدَّرة | هجرة؟ | المخاطر |
|---|---|---|---|---|
| `App__BaseUrl` غير مضبوط ⟹ روابط بريد ناقصة | 🔴 عالية | 0 كود (env فقط) | لا | روابط لا تعمل في كل الرسائل |
| التقارير (submission.*) غير مربوطة بالبريد | 🔴 عالية | `SubmissionService` (+حقن الخدمة) | لا | لا بريد لأهمّ سير عمل |
| `report.review_ready` قالب/قاعدة بلا مُطلِق | 🟠 متوسطة | `SubmissionService` | لا | القالب مبذور بلا استخدام |
| KPI غير مربوط بالبريد | 🟠 متوسطة | `KpiEvaluationService` + قوالب | لا | لا بريد KPI |
| لا مُجدوِل لتذكيرات التقارير (R2) | 🟠 متوسطة | BackgroundService جديد | لا | تذكيرات يدويّة فقط |
| قوالب `leave-request-*` تبني الرسالة في الكود لا مركز التحكم | 🟡 منخفضة | `EmailNotificationService` | لا | تناسق مركز التحكم |
| منطق تأكيد البريد غير منفّذ | 🟡 منخفضة | Auth كامل | ربما | ميزة مستقبليّة |
| المسار القديم (outbox) ميّت لكنه لا يزال مُسجَّلًا | 🟡 منخفضة | حذف Dispatcher | لا | دَين تقنيّ |
| لا مراقبة/تنبيه على Failed في email_notifications | 🟡 منخفضة | لوحة/تنبيه | لا | فشل صامت |

**البنية التحتيّة السليمة (لا فجوة):** SMTP مُهيّأ بالكامل · `email_notifications` + فهرس CorrelationKey الفريد موجودان · القلب الآمن (لا يرمي، DryRun/Enabled/Disabled) منفّذ · السياسات (log=4 أدوار، control=Admin) منفّذة · 10 قوالب + 7 قواعد مبذورة · واجهة السجلّ + مركز التحكم منفّذان.

---

## Phase 8 — تقرير التصميم النهائيّ

### CURRENT EMAIL STATUS
**صفر بريد فعليّ مُرسَل.** كل النشاط = in-app notifications (163) + 16 صفّ DryRun (leave). SMTP جاهز، البوّابتان مغلقتان (`Email__Enabled=false`, `EmailNotifications__Mode=DryRun`). المسار القديم (outbox) ميّت (0 صف).

### EVENT MATRIX
🟢 مربوط (إجازات/HR/حوكمة/تصعيد/متابعة/تذكيرات-يدويّة) · 🟡 in-app فقط (تقارير/KPI/تطوير/تصعيد-قديم) · 🔴 لا شيء (إدارة مستخدمين/قوالب/أرشيف/تأكيد-بريد).

### WORKFLOW MATRIX
كل سير عمل موثّق أعلاه (Phase 3). أهمّ سيرَي عمل (تقارير + KPI) **بلا بريد** اليوم.

### EMAIL / TEMPLATE STATUS
10 قوالب مبذورة + 7 قواعد (Phase 4/5). ناقص: قوالب report-workflow + KPI + leave-dedicated.

### ACTIVATION PLAN
6 مراحل (Phase 6): تفعيل env أولًا للأحداث المربوطة، ثم كود تدريجيًّا (تقارير→KPI→تأكيد)، ثم تقاعد القديم.

### IMPLEMENTATION PLAN (الترتيب المقترح للتنفيذ عند الموافقة)
1. ضبط `App__BaseUrl` (env، بلا كود).
2. تفعيل `EmailNotifications__Mode=Enabled` للأحداث المربوطة (env، مع مراقبة).
3. ربط `SubmissionService` بمسار البريد + استخدام `REPORT_REVIEW_READY`.
4. مُجدوِل تذكيرات التقارير (R2).
5. ربط KPI + قوالبه.
6. منطق تأكيد البريد.
7. تقاعد `email_outbox` + Dispatcher.

### RISK ANALYSIS
أعلى خطر = انفجار بريد عند التفعيل المفاجئ + روابط ناقصة. التخفيف: `App__BaseUrl` أولًا، تفعيل مرحليّ، مراقبة Failed، القلب الآمن يمنع تعطيل سير العمل.

### ROLLBACK PLAN
كل تفعيل env قابل للعكس فوريًّا (`Mode=DryRun`/`Email__Enabled=false` + restart، بلا هجرة). تغييرات الكود = استعادة publish backup + restart (نمط النشر المعزول المعتاد).

### EXECUTION ORDER
`App__BaseUrl → Mode=Enabled (مراقبة) → ربط التقارير → مُجدوِل → KPI → تأكيد → تقاعد القديم`.

---

> **EMAIL NOTIFICATIONS DESIGN = GO**

**STOP — انتهى التصميم/التشخيص (قراءة فقط). لا بدء تنفيذ، لا تعديل كود، لا نشر، لا إرسال بريد — بانتظار موافقة صريحة على الخطوة التالية.**
