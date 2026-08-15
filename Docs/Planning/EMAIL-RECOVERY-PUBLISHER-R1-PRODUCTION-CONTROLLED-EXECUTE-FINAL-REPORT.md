# EMAIL-RECOVERY-PUBLISHER-R1 — التنفيذ المحكوم على الإنتاج (تقرير الإغلاق النهائيّ)

**التاريخ:** 30 يوليو 2026 — نافذة التنفيذ `18:51:20 → 18:51:33 UTC` (`21:51 بتوقيت الرياض`)

**القرار:** `PRODUCTION CONTROLLED EXECUTE — 11 SENT — 1 COMPLETED — 0 FAILED — IDEMPOTENCY PASS — CLOSED`

---

## 1. المرجع المُصرَّح به

| البند | القيمة |
|---|---|
| Tool Candidate | `74fd98a8a6216c8a98a2ea7172a099b05f7292a5` |
| Artifact SHA256 | `0741d5d7f30552d9bfb87ea2cddaa5858beaa80ee99a915f98eaf79766fd0166` |
| البيان | `/root/secure/email-recovery-r1-20260729-manifest.json` (SHA256 `f0d04d40…`) |
| الدفعة | `r1-20260729` |
| النطاق المصرَّح به | إرسال الـ11 المؤهَّلة حصرًا — **حظر الحالة 9** (`Completed / no_pending_reviews`) |

---

## 2. بوّابة ما قبل التنفيذ (قراءة فقط) — **PASS 14/14**

| # | الفحص | النتيجة |
|---|---|---|
| 1 | الوقت | UTC `2026-07-30T18:49:50Z` — الرياض `2026-07-30T21:49:50+0300` |
| 2 | health داخليّ / عامّ | **200 / 200** |
| 3 | MainPID / NRestarts | `258585` / `0` (active/running) |
| 4 | `EmailNotifications__Mode` | **`Enabled`** |
| 5 | `ReportReminderScheduler__Enabled` | **`true`** |
| 6 | Pending / Processing / Failed | 0 / 0 / 0 |
| 7 | `email_outbox` | 0 |
| 8 | عدد الهجرات / الرأس | 30 / `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| 9 | بصمة الأداة المنشورة | `0741d5d7…` **مطابقة** |
| 10 | مالك البيان / صلاحياته | `root:root` / **600** (3797 بايت) |
| 11 | عدد عناصر البيان | **12** (schemaVersion 1، batch `r1-20260729`، maxItems 12) |
| 12 | نتيجة Plan قبيل التنفيذ | **Eligible 11 / Completed 1** / AlreadyApplied 0 / ManualReview 0 / Invalid 0 |
| 13 | صفوف Recovery القائمة | **0** |
| 14 | عمليّة استرداد أخرى تعمل | 0 عمليّة / 0 وحدة systemd / 0 cron |

**شرط التوقّف لم يُطلَق:** العدد المؤهَّل = **11** بالضبط، مطابق للمرجع.

### لقطة BEFORE

```
total=213  dryrun=139  enabled=74  sent=74
pending=0  processing=0  failed=0  recovery_rows=0  outbox=0
migrations=30  head=20260724224053_AddReportApproverAndKpiReviewerOverrides
md5_all=dba806e11008ffe98fe7a263bd38714a
env_mtime=2026-07-26 19:49:58.990233000 +0000
```

**إثبات عدم الانزياح منذ تقرير Plan:** أحدث صفّ في الجدول أُنشئ `2026-07-30 13:10:18 UTC` — أي **قبل** تشغيل Plan (17:09) — والإجماليّ 213 في القياسين. (بصمة md5 هنا تختلف رقميًّا عن بصمة تقرير Plan لاختلاف صيغة التجميع بين السكربتَين لا لتغيّر البيانات؛ اعتُمدت صيغة موحّدة لمقارنة قبل/بعد في هذا التشغيل.)

---

## 3. أمر التنفيذ

```
dotnet ./Reporting.EmailRecoveryPublisher.dll --execute \
  --manifest /root/secure/email-recovery-r1-20260729-manifest.json \
  --expected-count 11 \
  --batch-id r1-20260729 \
  --confirm EMAIL-RECOVERY-PUBLISHER-R1 \
  --json
```

`EXIT=0` — `Ok=true` — `AbortReason=null` — `RunMode=execute` — `EmailMode=Enabled`

| المقياس | القيمة |
|---|---|
| Total | 12 |
| Eligible | 11 |
| Completed | 1 |
| AlreadyApplied | 0 |
| ManualReview | 0 |
| Invalid | 0 |
| **Created** | **11** |
| **Failed** | **0** |

`stderr` فارغ تمامًا.

---

## 4. إثبات كلّ حالة ناجحة (11 معيارًا × 11 حالة)

| # | المستلِم | البريد (مُقنَّع) | الفئة | الفترة | مفتاح `recovery:` + لاحقة الدفعة | Mode | Status | Attempt | SentAt (UTC) | FailureReason |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | حبيبة | `bd***@marketingexperts.com.sa` | report-overdue | 2026-07-19 | ✔ | Enabled | **Sent** | 1 | 18:51:24 | فارغ |
| 2 | ريم جاب الله | `re***@gmail.com` | report-overdue | 2026-07-20 | ✔ | Enabled | **Sent** | 1 | 18:51:25 | فارغ |
| 3 | زينب محمد | `zi***@gmail.com` | report-overdue | 2026-07-21 | ✔ | Enabled | **Sent** | 1 | 18:51:26 | فارغ |
| 4 | حبيبة | `bd***@marketingexperts.com.sa` | report-overdue | 2026-07-22 | ✔ | Enabled | **Sent** | 1 | 18:51:27 | فارغ |
| 5 | عائشة كمال | `As***@gmail.com` | report-overdue | 2026-07-23 | ✔ | Enabled | **Sent** | 1 | 18:51:27 | فارغ |
| 6 | أميرة محمد | `Am***@gmail.com` | report-overdue | 2026-W30 | ✔ | Enabled | **Sent** | 1 | 18:51:28 | فارغ |
| 7 | إسراء حفصي | `es***@gmail.com` | report-overdue | 2026-W30 | ✔ | Enabled | **Sent** | 1 | 18:51:29 | فارغ |
| 8 | بسنت محمد | `ba***@yahoo.com` | report-overdue | 2026-W30 | ✔ | Enabled | **Sent** | 1 | 18:51:30 | فارغ |
| 9 | مروة وهيب | `ma***@gmail.com` | report-overdue | 2026-07-25 | ✔ | Enabled | **Sent** | 1 | 18:51:31 | فارغ |
| 10 | زينب محمد | `zi***@gmail.com` | report-overdue | 2026-07-25 | ✔ | Enabled | **Sent** | 1 | 18:51:32 | فارغ |
| 11 | حبيبة | `bd***@marketingexperts.com.sa` | report-overdue | 2026-07-25 | ✔ | Enabled | **Sent** | 1 | 18:51:33 | فارغ |

### تأكيدات مجمَّعة

| التأكيد | النتيجة |
|---|---|
| إجماليّ صفوف Recovery | **11** (صفّ واحد لكلّ حالة، لا أكثر) |
| كلّ المفاتيح تبدأ بـ`recovery:` وتنتهي بـ`:r1-20260729` | **true** |
| كلّها `Mode=Enabled` | **true** |
| كلّها `Status=Sent` | **true** |
| كلّها `AttemptCount=1` | **true** |
| كلّها `SentAt` موجود | **true** |
| كلّها `FailureReason` فارغ | **true** |
| مفاتيح ارتباط متمايزة | **11 / 11** ⇒ **لا تكرار** |
| مستلِمون متمايزون | 8 (حبيبة ×3، زينب ×2، والباقي مرّة واحدة) — مطابق للبيان |
| صفوف Recovery لفئة review-teamleader | **0** |
| صفوف Recovery لفئات التلخيص | **0** |

**التسلسليّة مُثبَتة زمنيًّا:** أوقات الإرسال متتابعة بفارق ثانية تقريبًا (`18:51:24 → 18:51:33`) ⇒ إرسال واحدًا تلو الآخر، **لا توازٍ**. ولم يقع أيّ فشل ⇒ لم يُختبَر التوقّف عند أوّل فشل عمليًّا، والحارس قائم كما أُثبِت في RC.

---

## 5. إثبات حظر الحالة رقم 9

| الإثبات | النتيجة |
|---|---|
| قرار الأداة للحالة 9 | `Completed / no_pending_reviews` (لم تدخل مسار الإرسال) |
| صفوف Recovery بفئة `report-review-overdue-teamleader` | **0** |
| رسائل أُرسلت لأميرة بخصوص مراجعة W30 | **0** |

أميرة محمد استلمت رسالة واحدة فقط هي **`report-overdue / 2026-W30`** (الحالة 6 المؤهَّلة)، وهي التزام مختلف تمامًا عن مراجعة W30 المحظورة.

---

## 6. ثوابت ما بعد التنفيذ

### لقطة AFTER

```
total=224 (+11)   dryrun=139 (بلا تغيير)   enabled=85 (+11)   sent=85 (+11)
pending=0  processing=0  failed=0   recovery_rows=11   outbox=0
migrations=30  head=20260724224053_AddReportApproverAndKpiReviewerOverrides
non_recovery_rows=213
md5_non_recovery=dba806e11008ffe98fe7a263bd38714a
```

| الثابت | BEFORE | AFTER | الحكم |
|---|---|---|---|
| Created | — | **11** | مطابق للمتوقَّع |
| Sent | 74 | **85** (+11) | مطابق |
| Failed / Pending / Processing | 0/0/0 | **0/0/0** | مطابق |
| صفوف DryRun الأصليّة | 139 | **139** | **لم تُمَسّ** |
| **بصمة الصفوف غير الاستردادية** | `dba806e11008ffe98fe7a263bd38714a` (على 213 صفًّا) | `dba806e11008ffe98fe7a263bd38714a` (على 213 صفًّا) | **مطابقة حرفيًّا** |
| `email_outbox` | 0 | **0** | لم يتغيّر (الأداة تُرسِل مباشرةً ولا تمرّ بالصندوق) |
| عدد الهجرات / الرأس | 30 / `20260724224053` | **30 / `20260724224053`** | لم يتغيّر |
| MainPID / NRestarts | 258585 / 0 | **258585 / 0** | **لا إعادة تشغيل** |
| mtime لملفّ البيئة | 2026-07-26 19:49:58 | **بلا تغيير** | لا تعديل إعداد |
| health داخليّ / عامّ | 200 / 200 | **200 / 200** | سليم |

**البصمة المطابقة على 213 صفًّا هي الإثبات الحاسم:** لم يتغيّر أيّ صفّ سابق — الإضافة كانت 11 صفًّا جديدًا **حصرًا**.

### إثباتات سلبيّة إضافيّة

| الإثبات | النتيجة |
|---|---|
| المجدول شُغِّل يدويًّا | **لا** — 0 سطر مجدول في السجلّ منذ 18:45 |
| عمليّة أداة مقيمة بعد الانتهاء | **0** |
| أخطاء `fail:` أو `crit:` في السجلّ | **0** |
| أخطاء SMTP / فشل مصادقة | **0** |
| مستلِم زائد خارج البيان | **0** |
| رسالة زائدة | **0** — 11 صفًّا مقابل 11 مؤهَّلة |
| SQL مباشر لتعديل البيانات | **لم يُستخدم** — كلّ استعلامات SQL كانت `SELECT` قراءة فقط |
| سكربت SMTP خارجيّ | **لم يُستخدم** |
| Restart / Config / Migration / Frontend | **لا شيء** |
| ظهور أيّ سرّ | **0** — لم يُطبع Password/JWT/ConnectionString/SMTP credential/Token، ولم يُستخدم `cat /etc/reporting-api.env` |

---

## 7. اختبار الحتميّة (Idempotency)

### التشغيل الثاني — نفس الأمر ونفس البيان

```
--execute --expected-count 11 --batch-id r1-20260729 --confirm EMAIL-RECOVERY-PUBLISHER-R1
```

`EXIT=4` — `Ok=false` — **`AbortReason=expected_count_mismatch`**

`Eligible=0` — `Created=0` — `Failed=0` — و12 عنصرًا وُسِمت `ManualReview / expected_count_mismatch`.

**التفسير:** بعد الإرسال الأوّل صار عدد المؤهَّلين **صفرًا** (كلّها استُردّت)، فاصطدم بوّابة العدد `0 ≠ 11` وأجهض التشغيل **قبل أيّ كتابة**. هذا **سلوك حماية مزدوج**: البوّابة أوقفت التشغيل قبل حتّى أن يحتاج الأمر إلى حارس التكرار.

### إثبات تصنيف `AlreadyApplied` بمسار قراءة-فقط

لتجنّب تشغيل `--execute` ثالث بلا داعٍ، أُثبِت التصنيف عبر `--plan` (قراءة فقط، صفر كتابة):

| المقياس | القيمة |
|---|---|
| Total | 12 |
| **Eligible** | **0** |
| **AlreadyApplied** | **11** — كلّها `recovery_already_applied` |
| **Completed** | **1** — الحالة 9 ما زالت `no_pending_reviews` |
| ManualReview / Invalid | 0 / 0 |
| Created / Failed | **0 / 0** |

**النتيجة:** الحالات الإحدى عشرة المرسَلة تُصنَّف الآن `AlreadyApplied`، والحالة المكتملة تبقى `Completed`، و**لا رسالة مكرّرة ممكنة** — محميّة بالفهرس الفريد على `CorrelationKey` وبالمفتاح الحتميّ `recovery:{original-key}:r1-20260729`.

### الحالة النهائيّة بعد كلّ التشغيلات

```
total=224   recovery_rows=11   sent=85   pending=0  processing=0  failed=0
outbox=0    dryrun=139         md5_non_recovery=dba806e11008ffe98fe7a263bd38714a
MainPID=258585   NRestarts=0   tool_procs=0
```

---

## 8. القرار النهائيّ

```
EMAIL-RECOVERY-PUBLISHER-R1
PRODUCTION CONTROLLED EXECUTE
11 SENT
1 COMPLETED
0 FAILED
IDEMPOTENCY PASS
CLOSED
```

- **11 رسالة استرداد أُرسلت فعليًّا** بنجاح، تسلسليًّا، بمحاولة واحدة لكلّ منها، بلا فشل وبلا تكرار.
- **الحالة 9 لم تُرسَل** كما أُمِر — بقيت `Completed / no_pending_reviews`.
- **صفر أثر جانبيّ:** لا إعادة تشغيل، لا هجرة، لا تعديل إعداد، لا نشر واجهة، لا SQL كتابة، لا مجدول يدويّ، لا سرّ ظهر، ولا صفّ سابق تغيّر (بصمة 213 صفًّا مطابقة حرفيًّا).
- **الحتميّة مُثبَتة:** إعادة التشغيل لا تُنتج ولا رسالة واحدة.

### المتبقّي المفتوح (خارج نطاق هذه المهمّة)

- الأربع عشرة حالة الموسومة «انتظار النافذة الطبيعيّة» لم تُمَسّ.
- الحالتان المحظورتان (`DO NOT SEND`) لم تُمَسّا.
- الأداة والبيان باقيان على الخادم؛ إزالتهما اختياريّة: حذف `/opt/reporting/tools/email-recovery-publisher/` و`/root/secure/email-recovery-r1-20260729-manifest.json`. لا تراجُع مطلوبًا على البيانات (الرسائل أُرسلت فعليًّا ولا تُستعاد).
