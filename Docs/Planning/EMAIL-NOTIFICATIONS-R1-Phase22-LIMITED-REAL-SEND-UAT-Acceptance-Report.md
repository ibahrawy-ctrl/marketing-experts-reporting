# EMAIL NOTIFICATIONS R1 — Phase 22: LIMITED REAL SEND UAT (RC ONLY)
## تقرير القبول النهائي

**التاريخ:** 2026-07-16 → 2026-07-17 (UTC)
**البيئة:** Release Candidate حصرًا (`rc-report.emarketingacademy.net`، الخدمة `khubara-reporting-rc.service`، المنفذ 5092، القاعدة `reporting_rc`). **لم تُمَسّ بيئة الإنتاج إطلاقًا.**
**الحكم النهائي:** ✅ **EMAIL NOTIFICATIONS R1 — LIMITED REAL SEND UAT = PASS**

---

## 1. الهدف
التحقّق من تسليم البريد الحقيقي على RC باستخدام **ثلاثة صناديق معتمَدة فقط** عبر حارس إعادة توجيه عام (RecipientSafetyMode)، دون أي إرسال لأي عنوان خارج القائمة، ودون أي مساس بالإنتاج أو بأي حساب/دور/مدير/فريق/كلمة مرور حقيقية.

**الصناديق المعتمَدة (حصرًا):**
| الدور | العنوان |
|---|---|
| Employee | `i.bahrawy@marketingexperts.com.sa` |
| Manager / TeamLeader / Reviewer | `ahraoufa@gmail.com` |
| Admin / HR / Governance / Default | `bhrawy@gmail.com` |

---

## 2. تنفيذ القيود العشرة

| # | القيد | الحالة | الدليل |
|---|---|---|---|
| 1 | نسخة احتياطية جديدة قبل النشر + تعديلات env | ✅ | Backups DB + env (`.backup-smtp-20260716-230449` الأصل النظيف) |
| 2 | نشر backend على RC فقط بأدلّة (health=200، migrations بلا تغيير، Scheduler/Reminders/BackgroundJobs=false، Mode=DryRun قبل الإرسال) | ✅ | health 200؛ آخر هجرة `20260716015239` بلا تغيير؛ الجداول الثلاثة false |
| 3 | نسخ مفاتيح SMTP اللازمة فقط prod→RC مؤقتًا بلا طباعة أي سرّ وبلا نسخ ConnectionStrings/JWT | ✅ | نُسخت مؤقتًا ثم أُزيلت؛ لم يُطبع أي سرّ |
| 4 | تفعيل بوابة الأمان أولًا: RecipientSafetyMode=Enabled + allowlist(3) + خرائط الأدوار | ✅ | الحارس فُعّل قبل أي إرسال |
| 5 | قبل الإرسال: إثبات unauthorized_recipient_count=0 + الثلاثة schedulers false + العدّ/الأحداث/3 متلقّين فقط | ✅ | Safety Gate أثبت 0 غير مصرّح |
| 6 | Mode=Enabled مؤقتًا + إرسال بريد واحد لكل حدث معتمَد بلا عنوان رابع | ✅ | 17 صفًّا Sent، كلها ضمن الثلاثة المعتمَدة |
| 7 | اختبار Dedup بإعادة استخدام نفس CorrelationKey بلا إنشاء رسالة ثانية | ✅ | دورة حالة عنصر حوكمة (Open→InReview مرّتين ⟶ صفّ واحد) |
| 8 | بعد الاختبار: استعادة Mode=DryRun، تعطيل RecipientSafety، استعادة env الأصلي، إزالة SMTP من RC، تنظيف fixtures/scripts/tokens، تنظيف صفوف DryRun القديمة (نسخة CSV، معاملة محروسة، معرّفات صريحة) | ✅ | env مطابق byte-for-byte للأصل؛ CSV backup؛ حذف محروس |
| 9 | التحقّق النهائي | ✅ | القسم 4 أدناه |
| 10 | تقرير نهائي بالحكم PASS/FAIL | ✅ | هذا التقرير |

---

## 3. نتائج الإرسال الحقيقي (القيد 6) — مصفوفة الأحداث

17 صفًّا بحالة **Sent**، **كلها** إلى العناوين الثلاثة المعتمَدة حصرًا (**unauthorized_recipient_count بين Sent = 0**):

| الحدث | المتلقّي (بعد إعادة التوجيه) |
|---|---|
| report.submitted ×2 | bhrawy@gmail.com |
| report.returned | bhrawy@gmail.com |
| report.escalated | bhrawy@gmail.com |
| report.approved | bhrawy@gmail.com |
| kpi.review_requested | bhrawy@gmail.com |
| kpi.approved | bhrawy@gmail.com |
| kpi.reopened | bhrawy@gmail.com |
| leave-request-created | ahraoufa@gmail.com |
| hr-request-created | bhrawy@gmail.com |
| hr-request-completed | **i.bahrawy@marketingexperts.com.sa** |
| governance-item-created | bhrawy@gmail.com |
| governance-escalation-created | bhrawy@gmail.com |
| governance-item-updated ×4 | bhrawy@gmail.com |

> إعادة التوجيه طُبِّقت على `RecipientEmail` **قبل** `SendAsync` ⟶ كل إرسال بنيويًّا ∈ القائمة المعتمَدة. تحقّق من إصابة الصناديق الثلاثة جميعًا (employee/reviewer/admin).

**اختبار Dedup (القيد 7) = PASS:** دورة حالة عنصر الحوكمة `f50febb0` (Open→InReview→Open→InReview) أنتجت `CorrelationKey` تكراريًّا حتميًّا `...:status:Open->InReview` أُطلق مرّتين، لكنّ حارس `AnyAsync(CorrelationKey)` أنتج **صفًّا واحدًا فقط** — لا رسالة مكرّرة.

---

## 4. التحقّق النهائي (القيدان 8 و9)

**env مُستعاد (مطابق byte-for-byte للنسخة الأصلية النظيفة):**
- `Email__Mode` = غائب (كما في الأصل — الافتراضي الآمن)
- `Email__Enabled` = false
- مفاتيح RecipientSafety = 0، Redirect = 0، Allowlist = 0
- `Email__SmtpHost` / `Username` / `Password` = فارغة (SMTP أُزيل من RC)
- `Reminders__Enabled` = false، `Scheduler__Enabled` = false، `BackgroundJobs__Enabled` = false

**الخدمة والقاعدة:**
- `khubara-reporting-rc.service` = active، health HTTP **200**
- آخر هجرة = `20260716015239_KpiEvaluationPartialUniqueIndex` (**بلا تغيير** — Phase 22 كان code/config فقط)
- `email_notifications` = **0** صف (بعد نسخة CSV احتياطية لكل الـ27: 10 DryRun أساسية + 17 QA Sent)
- مستخدمو QA (`%@qa.local`) = **0**
- كل fixtures QA (submission/kpi/leave/hr/gov item + كل الأبناء) = **0**

**التنظيف المُنفَّذ:**
- **Fixtures نطاق الأعمال (معاملة محروسة، حارس @qa.local، معرّفات صريحة، عدّ قبل/بعد=0):** submission `5012c0e4` + 3 approval_steps؛ KPI `f22abdc1` + 3 results + 3 review events؛ leave `e05e154d` + 2 events؛ HR `fb42e308` + 3 events؛ gov item `f50febb0` + 4 updates؛ 14 refresh_tokens + 11 notifications + 17 audit_logs (أفعال QA) + 4 roles + 4 users.
- **email_notifications:** نسخة CSV `/root/email_notifications-uat-backup-20260716-234050.csv` (COPY 27) ثم حذف محروس بـ27 معرّفًا صريحًا (27→0).
- **Scripts/tokens:** حُذفت كل سكربتات وتوكنات Phase 22 من الخادم والمحلي (rc-*.mjs/.sh/.py/.sql، rc-uat-admin-pw.txt، rc-uat-state.json، إلخ). **حُفِظت** النسخ الاحتياطية (DB dumps، env backups، CSV).

---

## 5. المحظورات — كلها مُلتزَمة
- ❌ لم يُمَسّ أي حساب/دور/مدير/فريق/كلمة مرور حقيقية.
- ❌ لم تُمَسّ بيئة الإنتاج إطلاقًا.
- ❌ لم يُفعَّل أي Scheduler.
- ❌ لم يُرسَل أي بريد لأي عنوان خارج القائمة المعتمَدة (unauthorized بين Sent = 0).
- ❌ لم يُطبع أي سرّ (SMTP/JWT/ConnectionStrings).

---

## الحكم
✅ **EMAIL NOTIFICATIONS R1 — LIMITED REAL SEND UAT = PASS**

حارس RecipientSafetyMode أثبت فعاليته: كل إرسال حقيقي أُعيد توجيهه بنيويًّا إلى الصناديق الثلاثة المعتمَدة، مع حتمية CorrelationKey ومنع التكرار، وبيئة RC استُعيدت بالكامل إلى حالتها الأصلية (env مطابق byte-for-byte، لا SMTP، لا صفوف بريد، لا fixtures QA، الهجرة بلا تغيير).
