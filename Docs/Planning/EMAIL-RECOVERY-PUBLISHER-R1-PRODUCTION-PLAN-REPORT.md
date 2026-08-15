# EMAIL-RECOVERY-PUBLISHER-R1 — تقرير نشر الأداة على الإنتاج وتشغيل Plan (قراءة فقط)

**التاريخ:** 30 يوليو 2026 — **القرار:** `PRODUCTION TOOL DEPLOYED — PLAN COMPLETE — NO SEND EXECUTED`

**نطاق المهمّة:** نشر أداة الاسترداد على الإنتاج ثمّ تشغيل **Plan فقط**. لم يُنفَّذ `--execute`، ولم تُرسَل أيّ رسالة، ولم يُنشأ أيّ صفّ استرداد.

---

## 1. خطّ أساس الإنتاج (Production Baseline)

| البند | القيمة |
|---|---|
| SourceLink على الأربع DLLs | `f3ee32f24323d61258ef15844f66c66adaf279df` |
| حزمة الواجهة الحيّة | `index-Bq08cb54.js` |
| عدد الهجرات / الرأس | 30 / `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| `EmailNotifications__Mode` | `Enabled` |
| `ReportReminderScheduler__Enabled` | `true` |
| MainPID / NRestarts | `258585` / `0` |
| mtime لملفّ البيئة | `2026-07-26 19:49:58.990233000 +0000` |

خطّ الأساس **مطابق حرفيًّا** للمرجع المعتمَد في قبول RC ⇒ شرط المتابعة مستوفى.

---

## 2. المرشّح وبصمة الأثر (Candidate & Artifact Hash)

| البند | القيمة |
|---|---|
| Candidate SHA | `74fd98a8a6216c8a98a2ea7172a099b05f7292a5` |
| Parent | `f3ee32f24323d61258ef15844f66c66adaf279df` |
| Tree | `11dadefa6ea82da91c94abd8d55d6f8b5c74af0d` |
| سطح التغيير | 5 ملفّات، **+2004 / −0** |
| نظافة شجرة العمل | `git status --porcelain` = 0 سطر |
| **Artifact SHA256** | `0741d5d7f30552d9bfb87ea2cddaa5858beaa80ee99a915f98eaf79766fd0166` |

**إثباتات سلبيّة على المرشّح:** الأداة غير مُدرَجة في `Reporting.sln` (0 تطابق) — لا هجرة — لا Controller — لا Frontend — لا بيان إنتاجيّ داخل Git (0 تطابق) — لا أسرار (0 تطابق لأنماط Password/Token/Secret/ConnectionString) — لا منطق SMTP مُستنسَخ داخل `RecoveryPublisher.cs`.

> **ملاحظة دقيقة:** `Program.cs` يحوي سطرًا واحدًا لـ MailKit هو `services.AddScoped<IEmailSender, MailKitEmailSender>();` — **حقن تبعيّة يُعيد استخدام مُرسِل الإنتاج** من `Reporting.Infrastructure`، بلا أيّ منطق SMTP مكرَّر، ولا يُستدعى إطلاقًا تحت `--plan`.

بصمة الأثر المبنيّة محليًّا = البصمة المرجعيّة = البصمة المنشورة على الخادم، بايتًا ببايت.

---

## 3. الفحص القبْليّ (Preflight — قراءة فقط)

| الفحص | النتيجة |
|---|---|
| الخدمة `reporting-api` | `active (running)` |
| health داخليّ / عامّ | 200 / 200 |
| Pending / Processing / Failed | 0 / 0 / 0 |
| `email_outbox` | 0 |
| صفوف `recovery:%` | 0 |
| إجماليّ `email_notifications` | 213 (DryRun 139، Enabled 74، Sent 74) |
| أخطاء SMTP/Auth حديثة | 0 |
| عمليّة استرداد أو Job أخرى | لا شيء |

**نقطتان بُحِثتا حتّى الحسم:** (أ) ظهور «عمليّتَي dotnet» كان أثرًا لمرشِّح الاستبعاد — الحصر الكامل أعطى خمس خدمات معروفة (`reporting-test 170816`، `reporting-rc 246643`، `reporting 258585`، `academy-api-staging 263074`، `academy-api 272526`) **ولا عمليّة أداة**؛ (ب) وحدة systemd المطابقة لنمط `recovery|email` هي `snapd.recovery-chooser-trigger.service` (loaded/inactive/dead) — لا صلة لها.

**لم يُطبع أيّ سرّ، ولم يُستخدم `cat /etc/reporting-api.env` إطلاقًا** — استُخرجت كلمة قاعدة البيانات بمرشِّح سطر واحد داخل متغيّر بيئة غير معروض.

---

## 4. النسخ الاحتياطيّ (Backup)

مسار النشر `/opt/reporting/tools/` **لم يكن موجودًا قبل هذه المهمّة** ⇒ لا شيء أُتلِف أو أُستبدِل ⇒ لا حاجة لنسخة احتياطيّة. لم تُلمَس أيّ نسخة قائمة من `/opt/reporting/publish` أو `dist`.

---

## 5. مسار نشر الأداة

| البند | القيمة |
|---|---|
| المسار | `/opt/reporting/tools/email-recovery-publisher/` |
| عدد الملفّات | 39 |
| المالك / الصلاحيات | `root:root` / `700` |
| بصمة الأداة المنشورة | `0741d5d7f30552d9bfb87ea2cddaa5858beaa80ee99a915f98eaf79766fd0166` |
| ملفّات إعداد أو أسرار داخل المسار | 0 |

**لم تُمَسّ:** `/opt/reporting/publish` — واجهة `dist` — وحدة systemd — nginx — سكيمة قاعدة البيانات — إعداد الإنتاج.

---

## 6. إثبات عدم إعادة التشغيل وعدم الاستمراريّة

| الإثبات | النتيجة |
|---|---|
| MainPID قبل/بعد | `258585` = `258585` |
| NRestarts قبل/بعد | `0` = `0` |
| عمليّة أداة مقيمة | 0 |
| منفذ جديد مستمع | 0 |
| وحدة systemd للأداة | 0 |
| مهمّة cron | 0 |
| mtime لـ `/opt/reporting/publish` و`dist` و`/etc/reporting-api.env` | بلا تغيير |
| health بعد النشر | 200 / 200 |

الأداة **تعمل لمرّة واحدة عند الاستدعاء اليدويّ فقط** ثمّ تنتهي.

---

## 7. التحقّق من البيان (Manifest Validation)

| البند | القيمة |
|---|---|
| المسار | `/root/secure/email-recovery-r1-20260729-manifest.json` (**خارج Git**) |
| المالك / الصلاحيات | `root:root` / `600` |
| الحجم / SHA256 | 3797 بايت / `f0d04d40bf2d83d2508d84cb0ecf6ef779376a1fad3e1850c7fbdd942066cf94` |
| `schemaVersion` | 1 |
| `recoveryBatchId` | `r1-20260729` |
| `maxItems` | 12 |
| عدد العناصر | **12** |
| معرّفات إشعارات مكرّرة | 0 |
| مفاتيح ارتباط مكرّرة | 0 |
| الفئات المستخدَمة | `report-overdue` و`report-review-overdue-teamleader` **حصرًا** |
| أسرار/كلمات مرور/JWT/SMTP/Body/Subject حرّ | 0 |

تحقّق ثلاثيّ: (أ) تحقّق محلّي قبل الرفع، (ب) إعادة تحقّق للملفّ المثبَّت على الخادم، (ج) مقارنة قاعديّة أثبتت أنّ فئات التلخيص (`report-team-overdue-summary` / `report-executive-overdue-summary` / `report-department-overdue-summary`) موجودة في الجدول لكنّها **صفر مرّة** داخل البيان.

---

## 8. مطابقة الـ12 الأصليّة

الاثنتا عشرة مأخوذة حرفيًّا من `EMAIL-MISSED-NOTIFICATIONS-RECOVERY-R1-PHASE-2-CONTROLLED-SEND-FINAL-REPORT.md`، ومطابَقة صفًّا بصفّ مع `email_notifications` على الإنتاج (كلّها موجودة، كلّها `Mode=DryRun` وحالتها `DryRun`).

| # | معرّف الإشعار | الفئة | مفتاح الفترة |
|---|---|---|---|
| 1 | `abb880cd-…` | report-overdue | 2026-07-19 |
| 2 | `1e7bd45b-…` | report-overdue | 2026-07-20 |
| 3 | `4f6b1739-…` | report-overdue | 2026-07-21 |
| 4 | `3fde3b2d-…` | report-overdue | 2026-07-22 |
| 5 | `872def53-…` | report-overdue | 2026-07-23 |
| 6 | `92bcd8c3-…` | report-overdue | 2026-W30 |
| 7 | `0cf98453-…` | report-overdue | 2026-W30 |
| 8 | `4fc066dc-…` | report-overdue | 2026-W30 |
| 9 | `1ce30b52-…` | report-review-overdue-teamleader | 2026-W30 |
| 10 | `61c3396b-…` | report-overdue | 2026-07-25 |
| 11 | `a6de6596-…` | report-overdue | 2026-07-25 |
| 12 | `a14abd53-…` | report-overdue | 2026-07-25 |

**العدد = 12 بالضبط.** لا زيادة ولا نقصان.

---

## 9. نتائج Plan

**الأمر المُنفَّذ (بلا `--execute`):**

```
dotnet ./Reporting.EmailRecoveryPublisher.dll --plan \
  --manifest /root/secure/email-recovery-r1-20260729-manifest.json \
  --batch-id r1-20260729 --json
```

`EXIT=0` — `"RunMode":"plan"` — `"EmailMode":"Enabled"` — `"BatchId":"r1-20260729"` — `"Ok":true` — `"AbortReason":null` — `"Created":0` — `"Failed":0`.

مخرَج الأداة الختاميّ: **«Plan — قراءة فقط: لم يُكتب صفّ واحد ولم يُفتح أيّ اتصال SMTP.»**

| # | المستلِم | البريد (مُقنَّع) | نشط | الفئة | الفترة | مطابقة الأصل | الالتزام الحاليّ | Enabled قائم | Recovery قائم | القرار | ReasonCode |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | حبيبة | `bd***@marketingexperts.com.sa` | نعم | report-overdue | 2026-07-19 | مطابق (DryRun) | لا تسليم / لا إجازة | 0 | 0 | **Eligible** | `still_due` |
| 2 | ريم جاب الله | `re***@gmail.com` | نعم | report-overdue | 2026-07-20 | مطابق (DryRun) | لا تسليم / لا إجازة | 0 | 0 | **Eligible** | `still_due` |
| 3 | زينب محمد | `zi***@gmail.com` | نعم | report-overdue | 2026-07-21 | مطابق (DryRun) | لا تسليم / لا إجازة | 0 | 0 | **Eligible** | `still_due` |
| 4 | حبيبة | `bd***@marketingexperts.com.sa` | نعم | report-overdue | 2026-07-22 | مطابق (DryRun) | لا تسليم / لا إجازة | 0 | 0 | **Eligible** | `still_due` |
| 5 | عائشة كمال | `As***@gmail.com` | نعم | report-overdue | 2026-07-23 | مطابق (DryRun) | لا تسليم / لا إجازة | 0 | 0 | **Eligible** | `still_due` |
| 6 | أميرة محمد | `Am***@gmail.com` | نعم | report-overdue | 2026-W30 | مطابق (DryRun) | لا تسليم / لا إجازة | 0 | 0 | **Eligible** | `still_due` |
| 7 | إسراء حفصي | `es***@gmail.com` | نعم | report-overdue | 2026-W30 | مطابق (DryRun) | لا تسليم / لا إجازة | 0 | 0 | **Eligible** | `still_due` |
| 8 | بسنت محمد | `ba***@yahoo.com` | نعم | report-overdue | 2026-W30 | مطابق (DryRun) | لا تسليم / لا إجازة | 0 | 0 | **Eligible** | `still_due` |
| 9 | أميرة محمد | `Am***@gmail.com` | نعم | report-review-overdue-teamleader | 2026-W30 | مطابق (DryRun) | **لا مراجعات معلّقة على W30** | 0 | 0 | **Completed** | `no_pending_reviews` |
| 10 | مروة وهيب | `ma***@gmail.com` | نعم | report-overdue | 2026-07-25 | مطابق (DryRun) | لا تسليم / لا إجازة | 0 | 0 | **Eligible** | `still_due` |
| 11 | زينب محمد | `zi***@gmail.com` | نعم | report-overdue | 2026-07-25 | مطابق (DryRun) | لا تسليم / لا إجازة | 0 | 0 | **Eligible** | `still_due` |
| 12 | حبيبة | `bd***@marketingexperts.com.sa` | نعم | report-overdue | 2026-07-25 | مطابق (DryRun) | لا تسليم / لا إجازة | 0 | 0 | **Eligible** | `still_due` |

### تغيّر جوهريّ منذ تقرير المرحلة 2 — الحالة 9

الحالة 9 كانت في تقرير المرحلة 2 «ما زالت مستحقّة ولديها مراجعتان `Submitted` معلّقتان». **إعادة التحقّق اللحظيّة من قاعدة الإنتاج أظهرت أنّ المراجعات المعلّقة لدى هذا المعتمِد أصبحت على أسبوع `2026-W31` لا `2026-W30`** ⇒ لم يعد لأسبوع W30 مراجعة معلّقة ⇒ صنّفتها الأداة `Completed / no_pending_reviews` ورفضت اعتبارها مؤهَّلة.

هذا **بالضبط** هو الغرض من حارس إعادة التحقّق: العالَم تغيّر بين إعداد البيان وتشغيل الخطّة، فامتنعت الأداة عن الإرسال تلقائيًّا. **صافي المؤهَّل = 11 لا 12.**

---

## 10. عدد Eligible

**11** — الحالات 1–8 و10–12، كلّها بـ`ReasonCode = still_due`.

## 11. عدد Completed

**1** — الحالة 9، `ReasonCode = no_pending_reviews`.

## 12. عدد AlreadyApplied

**0** — لا يوجد أيّ صفّ `recovery:%` على الإنتاج (لا قبل ولا بعد).

## 13. عدد ManualReview

**0** — لا التباس في أيّ حالة؛ كلّ حالة حُسِمت بمعيار قاطع.

## 14. عدد Invalid

**0** — كلّ العناصر الاثني عشر مطابقة للصفّ الأصليّ (معرّف + مفتاح + مستلِم + فئة + فترة + وضع DryRun).

---

## 15. إثبات DO NOT SEND

الحالتان المحظورتان **غير موجودتين في البيان إطلاقًا**:

| الحالة | الفئة | الفترة | موجودة في البيان؟ |
|---|---|---|---|
| حبيبة | report-overdue | 2026-07-23 | **لا (0)** |
| خالد مجدي | report-review-overdue-teamleader | 2026-W30 | **لا (0)** |

أُثبِت بالبحث المباشر عن معرّفَي الإشعارَين داخل ملفّ البيان ⇒ 0 تطابق في كلّ منهما. لم تظهرا في مخرَج Plan.

> تنبيه تمييز: الصفّ رقم 5 في الجدول أعلاه هو **عائشة كمال** بتاريخ 2026-07-23، وليس حبيبة. حالة «حبيبة — 2026-07-23» المحظورة إشعارها مختلف تمامًا ومستبعَد.

---

## 16. إثبات WAIT FOR NATURAL WINDOW

الأربع عشرة حالة الموسومة «انتظار النافذة الطبيعيّة» **غير مُدرَجة**: البيان يقتصر على فئتين اثنتين فقط (`report-overdue` و`report-review-overdue-teamleader`)، وفئات التلخيص (`report-team-overdue-summary` / `report-executive-overdue-summary` / `report-department-overdue-summary`) تظهر **صفر مرّة** داخل البيان رغم وجودها في الجدول الإنتاجيّ. الحدّ الصلب `maxItems=12` والحارس الداخليّ (12 عنصرًا كحدّ أقصى) يمنعان أيّ تجاوز بنيويًّا.

---

## 17. ثوابت قاعدة البيانات قبل/بعد

| المقياس | BEFORE | AFTER |
|---|---|---|
| إجماليّ `email_notifications` | 213 | 213 |
| DryRun | 139 | 139 |
| Enabled | 74 | 74 |
| Sent | 74 | 74 |
| Pending / Processing / Failed | 0 / 0 / 0 | 0 / 0 / 0 |
| صفوف `recovery:%` | 0 | 0 |
| `email_outbox` | 0 | 0 |
| عدد الهجرات / الرأس | 30 / `20260724224053` | 30 / `20260724224053` |
| **بصمة الصفوف `md5_all`** | `537adbc256a43aab23997869c4d7e534` | `537adbc256a43aab23997869c4d7e534` |
| MainPID / NRestarts | 258585 / 0 | 258585 / 0 |
| mtime لملفّ البيئة | 2026-07-26 19:49:58.990233000 +0000 | بلا تغيير |

`diff` بين اللقطتين أعطى **مخرَجًا فارغًا** ⇒ `IDENTICAL — BEFORE == AFTER` على الأربعة عشر مقياسًا، بما فيها بصمة المحتوى على مستوى الصفوف لا العدد فقط.

---

## 18. إثبات عدم اتصال SMTP

- الأداة تحت `--plan` **لا تستدعي المُرسِل إطلاقًا**؛ التسجيل في الحاوية هو حقن تبعيّة فقط.
- أسطر سجلّ SMTP خلال النافذة (آخر 10 دقائق حول التشغيل) = **0**.
- المخرَج الختاميّ للأداة يصرّح: «لم يُفتح أيّ اتصال SMTP».
- عدد الرسائل المرسَلة (`Sent`) قبل = بعد = **74** بلا زيادة.

## 19. إثبات عدم الإرسال وعدم الكتابة

| الإثبات | القيمة |
|---|---|
| صفوف Recovery أُنشئت | **0** |
| رسائل أُرسلت | **0** |
| `Created` في مخرَج Plan | **0** |
| مهام مجدول شُغّلت | **0** |
| عمليّات أداة مقيمة بعد التشغيل | **0** |
| صفوف DryRun الأصليّة | بلا تغيير (بصمة md5 ثابتة) |

`SaveChanges` لم يُستدعَ في مسار Plan؛ المسار كلّه `AsNoTracking` للقراءة.

## 20. إثبات عدم وجود أسرار

- لم يُطبع أيّ Password / JWT / ConnectionString / SMTP credential / Secret / Token في أيّ خطوة.
- لم يُستخدم `cat /etc/reporting-api.env` إطلاقًا؛ الاستخراج بمرشِّح سطر واحد داخل متغيّر بيئة غير معروض.
- البيان خالٍ من الأسرار (فحص أنماط = 0)، وصلاحياته 600 ومالكه root، وهو **خارج Git**.
- مخرَج JSON للأداة مُقنَّع بالكامل (لا عناوين بريد كاملة ولا مفاتيح كاملة).
- هذا التقرير لا يحوي بريدًا كاملًا ولا مفتاح ارتباط كاملًا ولا معرّف مستخدم كاملًا.

---

## 21. مسار التراجُع (Rollback)

لم تحدث أيّ كتابة على الإنتاج ⇒ **لا شيء يُعكَس على مستوى البيانات أو الخدمة**. إزالة الأثر التشغيليّ تتمّ بخطوتين فقط:

1. حذف مجلّد الأداة: `/opt/reporting/tools/email-recovery-publisher/` (ومعه `/opt/reporting/tools/` إن أُريد، فلم يكن موجودًا قبل هذه المهمّة).
2. حذف البيان: `/root/secure/email-recovery-r1-20260729-manifest.json`.

بلا إعادة تشغيل، بلا استعادة نسخة احتياطيّة، بلا عكس هجرة، بلا تعديل إعداد. الخدمة والحزمة والقاعدة لم تتغيّر أصلًا.

---

## 22. القرار النهائيّ

```
EMAIL-RECOVERY-PUBLISHER-R1
PRODUCTION TOOL DEPLOYED — PLAN COMPLETE
NO SEND EXECUTED
```

- الأداة منشورة على الإنتاج ببصمة مطابقة للمرجع المعتمَد، بلا استمراريّة وبلا أثر تشغيليّ.
- Plan نُفِّذ بنجاح على الحالات الاثنتي عشرة: **11 مؤهَّلة** و**1 مكتملة** و**0 مطبَّقة سابقًا** و**0 للمراجعة اليدويّة** و**0 غير صالحة**.
- الإنتاج **قبل = بعد** على كلّ مقياس، وصفر إرسال وصفر كتابة وصفر SMTP.
- **الإرسال ممنوع** حتّى صدور تصريح مستقلّ صريح بعد مراجعة نتيجة Plan أعلاه.

**موعد حرج مُعاد التأكيد:** حالات W30 تخرج من النافذة الطبيعيّة **السبت 2026-08-01**.
