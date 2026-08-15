# EMAIL NOTIFICATIONS R1 — Phase 23 — تقرير النشر الإنتاجي (DryRun Validation Only)

- **الميزة:** نظام إشعارات البريد R1 (`email_notifications`) — النسخة المعتمدة في RC
- **البيئة المستهدفة:** الإنتاج — `reports.emarketingacademy.net`
- **نمط النشر:** Backend فقط، `EmailNotifications__Mode=DryRun` (تحقّق بلا إرسال فعليّ)
- **الصلاحية:** إبراهيم البحراوي — **الإرسال الفعليّ غير مصرّح به في هذه الجولة**
- **التاريخ:** 17 يوليو 2026
- **معرّف النافذة (TS):** `20260717-091044`

---

## الحكم النهائي (Verdict)

| البند | النتيجة |
|---|---|
| **PRODUCTION DEPLOYMENT** | ✅ **PASS** |
| **PRODUCTION DRYRUN VALIDATION** | ✅ **PASS** |
| **REAL EMAIL ACTIVATION** | ⛔ **NOT PERFORMED** (غير مصرّح به) |
| **SCHEDULER ACTIVATION** | ⛔ **NOT PERFORMED** (المجدولات معطّلة) |

> **لم يُرسَل أيّ بريد فعليّ. لم تُفعَّل أيّ مجدولات. لم تُطبَّق أيّ هجرة جديدة. الواجهة لم تتغيّر. البيانات الإنتاجية الأساسية (16 صفًّا) محفوظة.**

---

## ملخّص المراحل

| المرحلة | الوصف | النتيجة |
|---|---|---|
| 23.0 | Source Provenance (تتبّع المصدر) | ✅ PASS (جلسة سابقة) |
| 23.1 | Pre-Deployment Gate | ✅ PASS (جلسة سابقة) |
| 23.2 | Local Build/Tests | ✅ PASS (جلسة سابقة) |
| 23.3 | Artifact Verification + Production Backups | ✅ PASS (جلسة سابقة) |
| 23.4 | Configuration (Mode=DryRun) | ✅ PASS (جلسة سابقة) |
| 23.5 | Deployment (backend-only) | ✅ PASS (جلسة سابقة) |
| 23.6 | Immediate Smoke | ✅ PASS (جلسة سابقة) |
| **23.7** | **Zero-Delivery DryRun Validation** | ✅ **PASS** |
| **23.8** | **Dedup (CorrelationKey)** | ✅ **PASS** |
| **23.9** | **Cleanup (QA fixtures + this-run rows)** | ✅ **PASS** |
| **23.10** | **Final State Verification** | ✅ **PASS** |

---

## اكتشاف معماريّ حاسم — نظامَا بريد مستقلّان

يوجد في r1 **نظامان مستقلّان تمامًا** للبريد:

1. **النظام القديم (OLD):** `NotificationService.NotifyAsync` → جدول `email_outbox`، محكوم ببوابة `Email__Enabled=false` على الإنتاج. يستهلكه **SubmissionService + KpiEvaluationService** (وأيضًا يدفع SignalR).
2. **النظام الجديد / R1 (NEW):** `EmailNotificationService` → جدول `email_notifications`، محكوم بـ`EmailNotifications__Mode=DryRun`. يستهلكه: `ReportReminderService`، `EmailControlService`، `LeaveRequestService`، `EmployeeServiceRequestService`، `GovernanceItemService`، `GovernanceActionItemService`، `GovernanceEscalationService`.

**الأثر:** أحداث KPI والتقارير (submission) **لا تتدفّق إلى `email_notifications`** في r1 (تستعمل الصندوق القديم المعطّل) ⇒ تُنتج **صفرًا** من صفوف النظام الجديد — وهذا **صحيح**. لذا تحقّق 23.7 الأمين اختبر الأحداث التي **تصل فعليًّا** إلى `email_notifications`: `manual.reminder`، `governance-item-created`، `governance-item-updated`.

**نقطة الاختناق الوحيدة `EnqueueAsync` (EmailNotificationService.cs ~529):** Mode=Disabled→تخطّي؛ CorrelationKey مكرّر→Duplicate؛ لا بريد مستلِم→Skipped؛ **Mode=DryRun→Status=DryRun، AttemptCount=0، SentAt=null، إضافة صفّ، SaveChanges، بلا SMTP إطلاقًا**؛ Mode=Enabled→RecipientSafety ثم SMTP.

---

## Phase 23.7 — Zero-Delivery DryRun Validation ✅ PASS

أُنشئ حساب QA فقط (`qa-emailr1-...@qa.marketingexperts.local`) وأُطلقت 3 مسارات أحداث حقيقية:

| الحدث | المسار | الصفوف |
|---|---|---|
| `manual.reminder` | `POST /api/email-control/manual-reminders/dry-run` | 1 (مستلِم QA) |
| `governance-item-created` | `POST /api/governance/items` (assignedToUserId=QA, relatedUserId=QA) | 1 (المكلَّف QA) |
| `governance-item-updated` | `POST /api/governance/items/{id}/status` (Open→InReview) | 2 (المكلَّف QA + منشئ Admin) |

**كل الصفوف (4):** `Status=DryRun`، `Mode=DryRun`، `AttemptCount=0`، `SentAt=NULL`، تحوي رابط الإنتاج `https://reports.emarketingacademy.net` (PRODLINK)، **بلا localhost**، CorrelationKey فريد غير فارغ. `email_outbox=0`، `real_sent_anywhere=0`. الإجمالي 16→20.

> **ملاحظة تصحيح:** `ApplicationScope=User(3)` تتطلّب `relatedUserId` (لا `assignedToUserId`) في `ValidateScopeAsync` — لذا فشلت المحاولة الأولى بـ400 وصُحّحت بإضافة `relatedUserId`.

---

## Phase 23.8 — Dedup (CorrelationKey) ✅ PASS

المفتاح الحتميّ `governance-item-updated:{itemId}:{recipientId}:status:Open->InReview`. دُوِّرت الحالة InReview→Open ثم Open→InReview (إعادة إطلاق نفس المفتاح):

- `rows_before` (مفتاح Open→InReview) = **2**
- `rows_after` = **2**
- `duplicate_created` = **false** ✅

المفتاح المكرّر رُفض عبر `AnyAsync` في `EnqueueAsync`. (خطوة InReview→Open الوسيطة أنشأت مفتاحًا مختلفًا = صفّان جديدان، نُظِّفا في 23.9.) لا Sent، الصندوق الصادر 0.

---

## Phase 23.9 — Cleanup ✅ PASS

سكربت محروس (يُجهِض إن كان this-run > 10). نسخة CSV احتياطية أولًا (`/root/emailr1-23-9-dryrun-backup.csv`، 6 صفوف = دليل حذف):

```
total_before=22 this_run_rows=6
DELETE 4 (governance_item_updates) / DELETE 1 (governance_items)
email_notifications_deleted=6
delete_qa_user_status=200 / qa_user_remaining=0
=== PROOF ===
email_notifications_total_after=16 (expect 16)  ✅
this_run_pattern_remaining=0 (expect 0)          ✅
real_sent_anywhere=0 (expect 0)                  ✅
email_outbox=0 (expect 0)                        ✅
qa_users_leftover=0 (expect 0)                   ✅
```

**حُذفت فقط صفوف هذه الجولة** (المطابقة لأنماط batchId/itemId هذه الجولة). **الصفوف الإنتاجية الأساسية (16) لم تُمَسّ.**

---

## Phase 23.10 — Final State Verification ✅ PASS

```
=== CONFIG ===
EmailNotifications__Mode=DryRun                  ✅
EmailNotifications__RecipientSafetyMode=Disabled ✅
Reminders__Enabled=false                         ✅
EmailNotifications__ReminderScheduler__Enabled=(unset) ✅
Email__Enabled=false                             ✅
=== SERVICE + HEALTH ===
service=active / health_http=200                 ✅
=== DB STATE ===
email_notifications_total=16                      ✅
email_notifications_sent=0                         ✅
email_notifications_nonzero_attempt=0             ✅
email_notifications_sentat_notnull=0              ✅
email_outbox=0                                    ✅
migration_head=20260716015239_KpiEvaluationPartialUniqueIndex
migration_count=29                                 ✅ (لا هجرة جديدة)
qa_users_leftover=0                                ✅
=== FRONTEND BUNDLE (unchanged) ===
frontend_index_bundle=assets/index-BbXihVZO.js    ✅
frontend_bundle_sha256=640095a5e7b7bf3b49f15b037062f15425d5c8a3a06d2152db07974481a9fef7 ✅
```

> **ملاحظة:** `EmailNotifications__BaseUrl` غير مضبوط بهذا الاسم في env، لكن كل صفوف DryRun احتوت رابط الإنتاج الصحيح (PRODLINK بلا localhost) كما أُثبت في 23.7 — الرابط يُبنى من مصدر صحيح (رابط صريح في التذكير اليدويّ + `BuildLink` للحوكمة). لا أثر وظيفيّ.

---

## النسخ الاحتياطية والاسترجاع (Rollback)

- DB dump: `/root/db-backups/reporting_prod-pre-email-r1-20260717-091044.dump`
- Publish backup: `/opt/reporting/publish-backup-email-r1-20260717-091044`
- Env backup: `/etc/reporting-api.env.backup-email-r1-20260717-091044`
- **Rollback فوريّ:** ضبط `EmailNotifications__Mode=Disabled` + `systemctl restart reporting-api` (لا هجرة لعكسها؛ الجداول إضافية بحتة).

**دليل حذف صفوف الجولة:** `/root/emailr1-23-9-dryrun-backup.csv` (مُبقًى عمدًا).

---

## التنظيف المنفَّذ

- سكربتات الخادم (`emailr1-23-7*.mjs`, `23-8`, `23-9`, `23-10`, `emailr1-23-7-ids.json`) — **حُذفت**.
- سكربتات محلية مؤقتة (`/tmp/emailr1-*.mjs`) — **حُذفت**.
- حساب QA + بند الحوكمة التجريبيّ — **حُذفا**.
- لا أسرار/توكنات طُبِعت في أيّ خطوة.

---

## ما لم يُمَسّ (No-Impact)

KPI evaluations / ComputeScore / Workflow / ScopeResolver / CurrentApproverId / قوالب التقارير / JobRole / ResetPassword / خصم الراتب الآلي / الصندوق القديم `email_outbox` / الواجهة (bundle) — **كلها بلا تغيير**.

---

## القرار والخطوة التالية

نشر Email Notifications R1 إلى الإنتاج في نمط **DryRun** اكتمل ونُجِح التحقّق منه دون أيّ إرسال فعليّ. النظام في حالة آمنة: يسجّل الإشعارات كـ`DryRun` بلا SMTP.

**⛔ توقُّف:** أيّ **تفعيل إرسال فعليّ محكوم على الإنتاج (PRODUCTION CONTROLLED REAL-SEND ACTIVATION)** يتطلّب **موافقة صريحة منفصلة**. لا إجراء إضافيّ قبلها.
