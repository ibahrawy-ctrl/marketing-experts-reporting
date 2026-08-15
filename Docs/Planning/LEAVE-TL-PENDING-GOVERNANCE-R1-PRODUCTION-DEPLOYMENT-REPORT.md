# LEAVE-TL-PENDING-GOVERNANCE-R1 — تقرير النشر على الإنتاج والقبول الإنتاجيّ

- **التاريخ:** 6 أغسطس 2026
- **الطبيعة:** تذكرة نشر فقط — **بلا تطوير جديد، بلا تعديل كود، بلا إصلاحات، بلا ميزات إضافية**
- **القرار النهائيّ:** `PRODUCTION PASS`
- **معرّف نافذة النشر (TS):** `20260806-181806`
- **المرجع السابق:** `Docs/Planning/LEAVE-TL-PENDING-GOVERNANCE-R1-RC-ACCEPTANCE-REPORT.md` (القرار `RC PASS — READY FOR PRODUCTION APPROVAL`)

---

## 1. الملخّص التنفيذيّ

نُشِر المرشّح `f2bd52c2664cd473f7aaf65f2a5a9953cbbf3099` على بيئة الإنتاج فوق الأساس الحيّ `2d282cebf0a22f65b78cd751de17d6c927128d0d`، Backend وFrontend معًا، **بإعادة تشغيل واحدة حصرًا** وبلا أيّ هجرة قاعدة بيانات وبلا تعديل إعداد.

النتيجة: طابور حوكمة قراءة-فقط للطلبات المعلّقة عند قائد الفريق أصبح متاحًا حيًّا لأدوار الحوكمة، وحالة حبيبة `55a0a0eb-a72d-4407-845f-01c4a54f1cb3` ظهرت داخل الطابور مصنَّفة `ExpiredUnresolved` **دون أن يُكتب حرف واحد على قاعدة بيانات الإنتاج**. جميع عدّادات الأثر التجاريّ (Ledger / Notifications / Email / Audit / Workflow / Outbox / Leave) متطابقة تمامًا قبل النشر وبعده.

---

## 2. حدود النطاق والالتزام بالمحظورات

| المحظور | الحالة | الدليل |
|---|---|---|
| تعديل Workflow | لم يحدث | لا تغيير على `LeaveRequestService`؛ 0 حدث جديد في `leave_request_events` (67 = 67) |
| اعتماد / رفض | لم يحدث | صفر استدعاء لأيّ مسار `POST`؛ حالة حبيبة `Submitted/TeamLeader` و`UpdatedAtUtc = NULL` |
| خصم / Ledger | لم يحدث | `employee_balance_ledger` = 97 قبل = 97 بعد |
| رصيد | لم يُمسّ | لا حركة Ledger ⟹ لا تغيير رصيد |
| إشعارات | لم تُمسّ | `notifications` = 621 قبل = 621 بعد |
| Scheduler | لم يُمسّ | `ReportReminderScheduler__Enabled=true` بلا تغيير، mtime الإعداد ثابت |
| بريد | لم يُمسّ | `email_notifications` = 354 قبل = 354 بعد؛ `email_outbox` = 0 قبل = 0 بعد |
| معالجة حبيبة | لم تحدث | قراءة فقط؛ الصفّ بلا أيّ تعديل |
| معالجة أيّ طلب | لم تحدث | `leave_requests` = 19 قبل = 19 بعد |
| SQL | لم تُنفَّذ أيّ عبارة كاتبة | نُفِّذت **قراءات `SELECT` حصرًا** لإثبات رأس الهجرة وعدّادات Zero Delta، وهي شرط أصيل في المراحل 0/6/7 من التذكرة |
| Hotfix إضافيّ | لم يحدث | سطح النشر = المرشّح المُجمَّد بلا زيادة ولا نقصان |

---

## 3. المرحلة 0 — فحص ما قبل النشر (Production Preflight)

| البند | القيمة قبل النشر |
|---|---|
| MainPID | `505567` |
| بدء الخدمة | `2026-08-04 21:57:27 UTC` |
| NRestarts | `0` |
| Health داخليّ / عامّ | `200` / `200` |
| SourceLink الحيّ (الأربع DLLs) | `1.0.0+2d282cebf0a22f65b78cd751de17d6c927128d0d` |
| الحزمة الحيّة | `index-Bq08cb54.js` + `index-BVnJtRhL.css` |
| عدد الهجرات | `30` |
| رأس الهجرة | `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| `email_outbox` | `0` |
| `Email__Enabled` | `false` |
| `EmailNotifications__Mode` | `Enabled` |
| `ReportReminderScheduler__Enabled` | `true` |
| mtime لملفّ الإعداد | `1785095398` |
| عمليّة نشر جارية | **لا شيء** (`pgrep -a -f "rsync -az\|dotnet publish\|vite build"` ⇒ `none`) |

### 3.1 خطّ الأساس الرقميّ (مرجع Zero Delta)

| الجدول | العدد |
|---|---|
| `employee_balance_ledger` | 97 |
| `notifications` | 621 |
| `email_notifications` | 354 |
| `audit_logs` | 1141 |
| `leave_request_events` | 67 |
| `email_outbox` | 0 |
| `leave_requests` | 19 |
| طابور TL-Pending (على مستوى الشركة) | 1 |
| صفوف Ledger لحبيبة | 0 |
| أحداث حبيبة | 1 |

**درس تقنيّ:** فحص SourceLink بـ`grep -oE "^1\.0\.0\+[0-9a-f]{40}$"` يُرجِع فراغًا كاذبًا لأنّ مخرجات `strings` تحمل محارف محيطة؛ الصواب إسقاط مرساتَي `^`/`$`.

---

## 4. المرحلة 1 — التحقّق من المرشّح

```
SHA      = f2bd52c2664cd473f7aaf65f2a5a9953cbbf3099
PARENT   = 2d282cebf0a22f65b78cd751de17d6c927128d0d   (= الأساس الحيّ للإنتاج)
TREE     = 77b9ce1d61b0f684cc82b58946ba6beb7b107822
PATCH_ID = 887022726c6ec1d6c2518d727d475491425c3e24
BRANCH   = candidate/leave-tl-pending-governance-r1-20260806
WORKTREE = /private/tmp/cand-leave-tl-gov-r1-20260806
```

- **الأب مطابق للإنتاج الحيّ** ⟹ النشر تقدّم خطّيّ لا تراجع مقنَّع.
- **سطح التغيير:** 11 ملفًّا، `+2074/−8`.
- **صفر هجرة:** مجلد الهجرات بلا إضافة؛ الإجمالي يبقى 30 والرأس `20260724224053`.
- **لا مفاجآت في الواجهة:** الحزمة الحيّة قبل النشر بُنِيت من `f3ee32f2`، بينما الـBackend الحيّ `2d282ceb`. أُثبِت أنّ `git diff --stat f3ee32f2 2d282ceb -- reporting-frontend/` **فارغ**، وأنّ `git merge-base --is-ancestor f3ee32f2 HEAD` = نعم ⟹ واجهة المرشّح = واجهة الإنتاج الحيّة + إضافات الحوكمة فقط (4 ملفّات، `+982/−8`، أحدها ملفّ اختبار).

---

## 5. المرحلة 2 — النسخ الاحتياطيّ ومسار التراجُع

| النوع | المسار | التفاصيل |
|---|---|---|
| Backend | `/opt/reporting/publish-backup-leavetlgov-20260806-181806` | 107M، SourceLink `2d282ceb…` |
| Frontend | `/opt/reporting/reporting-frontend/dist-backup-leavetlgov-20260806-181806` | 1.4M، الحزمة `index-Bq08cb54.js` |
| قاعدة البيانات | `/root/db-backups/reporting_prod-preleavetlgov-20260806-181806.dump` | 1,174,100 بايت |
| علامة النافذة | `/root/leavetlgov-prod-deploy-ts.txt` | `20260806-181806` |

**التراجُع:** استعادة مجلّدَي النسخ + `systemctl restart reporting-api`. **لا هجرة لعكسها.** لم يُستدعَ التراجُع.

---

## 6. المرحلة 3 — النشر

- البناء بختم صريح: `-p:SourceRevisionId=f2bd52c2… -p:ContinuousIntegrationBuild=true` بعد `rm -rf bin obj`.
- Backend: `rsync -az --delete --exclude appsettings.Development.json` → `/opt/reporting/publish` ثمّ `chown -R www-data:www-data`.
- **إعادة تشغيل واحدة حصرًا:** `systemctl restart reporting-api` عند `2026-08-06T18:19:21Z`.
- Frontend: بناء بـ`VITE_API_BASE_URL=https://reports.emarketingacademy.net/api` ثمّ استبدال ذرّيّ لمجلّد `dist` (بلا إعادة تشغيل).
- **الترتيب المقصود:** Backend أوّلًا ثمّ Frontend، كي لا تُقدَّم واجهة جديدة تستدعي نقطة نهاية غير موجودة.

### بصمات الأصول

| الأصل | قبل | بعد |
|---|---|---|
| `Reporting.Api.dll` | `8669bca2…` | `af851d13b10d09fe873b2b84ab8f3cf7f82b6d3354111b31a79cf41c0e66ed95` |
| `Reporting.Application.dll` | `424f73bb…` | `6a1199f0204587213341cb1902448eb7cc67d5b95d475a688c00b75a5b63ad4a` |
| `Reporting.Domain.dll` | `d36701b5…` | `6b09c31b834e01adabb4870c1797c23c4da2f7d0df468921c676ba47cfe734a5` |
| `Reporting.Infrastructure.dll` | `83c30928…` | `3a6f7d579a5ba9ce42cc84d1bf3ea526fc1233829e1c46dc9d5e6b95a50a07c4` |
| حزمة JS | `index-Bq08cb54.js` | `index-CG2a9RiH.js` — sha256 `9229470f627d83c9d27889db3d3a59ee49b9fa3ad209c4378bb30aed8c8304e0` |
| حزمة CSS | `index-BVnJtRhL.css` | `index-COKFKQO9.css` — sha256 `984580488552e5b95c88b2cd3e6f5d2fb80f9b339820aa2bfff17fbd87ffa1b7` |

الحزمة القديمة `index-Bq08cb54.js` تُرجِع الآن **404** (استبدال تامّ لا تعايش).

---

## 7. المرحلة 4 — التحقّق الفوريّ

| البند | النتيجة |
|---|---|
| Health داخليّ / عامّ | `200` / `200` |
| SourceLink الحيّ (الأربع DLLs) | `1.0.0+f2bd52c2664cd473f7aaf65f2a5a9953cbbf3099` |
| تطابق بصمات DLL الحيّة مع المبنيّة محليًّا | مطابقة تامّة (4/4) |
| الحزمة المُقدَّمة عبر HTTPS | `index-CG2a9RiH.js` |
| MainPID الجديد | `623791` |
| NRestarts | `0` |
| بدء الخدمة | `2026-08-06 18:19:21 UTC` |
| ActiveState | `active` |
| سجلّ الإقلاع — الهجرات | `No migrations were applied. The database is already up to date.` |
| أخطاء `fail:` / `crit:` منذ النشر | `0` |
| mtime لملفّ الإعداد | `1785095398` (بلا تغيير) |

---

## 8. المرحلة 5 — اختبارات الدخان

### 8.1 المجهول (Authentication)

| المسار | النتيجة | المتوقَّع |
|---|---|---|
| `GET /api/leave-requests/governance/team-leader-pending` (داخليّ) | `401` | ✅ |
| `GET /api/leave-requests/governance/team-leader-pending` (عامّ HTTPS) | `401` | ✅ |
| `GET /api/leave-requests/pending` | `401` | ✅ |
| `GET /api/leave-requests` | `405` | ✅ (لا فعل GET على المسار الجذر — التوجيه القائم `/my` و`/pending`) |

### 8.2 المصادَق (Authorization + عدم الانحدار)

| المسار | النتيجة |
|---|---|
| `GET /api/leave-requests/governance/team-leader-pending` | `200` |
| `GET /api/leave-requests/pending` (الطابور القائم) | `200` |
| `GET /api/leave-requests/my` (صفحات الإجازات القائمة) | `200` |
| `GET /api/dashboard/me` | `200` |
| `GET /api/notifications` | `200` |
| `GET /api/me/balances` | `200` |

- المسار موجود في الـDLL الحيّ: `governance/team-leader-pending` = 1.
- السياسة موجودة في الـDLL الحيّ: `LeaveGovernanceRead` = 2.
- **ملاحظة منهجيّة:** مصفوفة RBAC الكاملة (Admin/HR/GM = 200؛ Employee/TL/Manager = 403؛ `POST …/team-leader/approve` من حساب حوكمة = 403) أُثبِتت حيًّا على RC لنفس الـcommit، ولا يمكن تكرارها على الإنتاج دون إنشاء حسابات اختبار — وهو **كتابة محظورة** في هذه التذكرة. ختم SourceLink يُثبِت أنّ الشيفرة الحيّة على الإنتاج هي بعينها التي خضعت لتلك المصفوفة.

---

## 9. المرحلة 6 — التحقّق الإنتاجيّ قراءة-فقط (حالة حبيبة)

استجابة `GET /api/leave-requests/governance/team-leader-pending` على الإنتاج:

```json
{
  "counters": {
    "totalPending": 1,
    "attention": 0,
    "critical": 0,
    "expiredUnresolved": 1,
    "missingTeamLeader": 0,
    "oldestPendingDays": 22
  },
  "total": 1,
  "items": [
    {
      "requestId": "55a0a0eb-a72d-4407-845f-01c4a54f1cb3",
      "requestType": "Leave",
      "employeeUserId": "7a9a6919-2768-4961-8e8a-4e0e15797704",
      "employeeName": "حبيبة",
      "departmentName": "المبيعات",
      "teamName": "فريق B2B",
      "teamLeaderUserId": "9141ee82-4d3f-48eb-9e7b-ff9e160947a4",
      "teamLeaderName": "محمد عبدالقوي",
      "createdAtUtc": "2026-07-15T13:14:43.621433Z",
      "startDate": "2026-07-19",
      "endDate": "2026-07-20",
      "requestedUnits": 2,
      "currentStatus": "Submitted",
      "currentStep": "TeamLeader",
      "lastEventType": "submitted",
      "lastEventAtUtc": "2026-07-15T13:14:43.676946Z",
      "daysPending": 22,
      "daysUntilStart": -18,
      "startedAlready": true,
      "endedAlready": true,
      "delayStatus": "ExpiredUnresolved",
      "delayReason": "انتهت مدة الطلب ولم يُبتّ عند قائد الفريق.",
      "hasLedger": false,
      "ledgerCount": 0,
      "isEmployeeActive": true,
      "isTeamLeaderActive": true,
      "missingTeamLeaderAssignment": false
    }
  ]
}
```

### 9.1 مطابقة الاستجابة مع القاعدة (قراءة مباشرة)

| البند | القاعدة | الاستجابة | مطابق |
|---|---|---|---|
| Status | `Submitted` | `Submitted` | ✅ |
| CurrentStep | `TeamLeader` | `TeamLeader` | ✅ |
| StartDate → EndDate | `2026-07-19` → `2026-07-20` | مطابق | ✅ |
| `UpdatedAtUtc` | `NULL` | — | ✅ (لا كتابة إطلاقًا) |
| صفوف Ledger | `0` | `ledgerCount: 0` | ✅ |
| الأحداث | `1` | `lastEventType: submitted` | ✅ |
| طابور TL-Pending | `1` | `totalPending: 1` | ✅ |

**الاستنتاج:** الطابور **يعرض** الحالة ولا **يعالجها**. التصنيف `ExpiredUnresolved` صحيح قانونيًّا (انتهت المدّة 2026-07-20 دون بتّ، و«اليوم» بتوقيت الرياض 2026-08-06 ⟹ 22 يومًا معلّقة، `daysUntilStart = −18`).

---

## 10. المرحلة 7 — انحدار قراءة-فقط: Zero Delta

| المقياس | قبل النشر | بعد النشر | الفرق |
|---|---|---|---|
| `employee_balance_ledger` | 97 | 97 | **0** |
| `notifications` | 621 | 621 | **0** |
| `email_notifications` | 354 | 354 | **0** |
| `audit_logs` | 1141 | 1141 | **0** |
| `leave_request_events` | 67 | 67 | **0** |
| `email_outbox` | 0 | 0 | **0** |
| `leave_requests` | 19 | 19 | **0** |
| طابور TL-Pending | 1 | 1 | **0** |
| Ledger لحبيبة | 0 | 0 | **0** |
| أحداث حبيبة | 1 | 1 | **0** |
| عدد الهجرات | 30 | 30 | **0** |
| رأس الهجرة | `20260724224053…` | `20260724224053…` | **بلا تغيير** |
| mtime الإعداد | 1785095398 | 1785095398 | **0** |
| NRestarts | 0 | 0 | **0** |

**الحُكم: Zero Delta تامّ على Ledger و Balance و Workflow و Notifications و Email و Outbox.**

---

## 11. المرحلة 8 — التنظيف

| البند | الإجراء |
|---|---|
| `/tmp/prod-preflight-govr1.sql` (الخادم) | حُذف |
| `/tmp/gov-resp.json` (الخادم) | حُذف |
| `/tmp/prod-preflight-govr1.sql` (محليًّا) | حُذف |
| `reporting-backend/publish-prod` (شجرة المرشّح) | حُذف |
| شجرة عمل المرشّح | باقية نظيفة (`git status` فارغ) على `f2bd52c…` |
| النسخ الاحتياطيّة الثلاث | **مُستبقاة** (لا تُحذف — مسار التراجُع) |
| `/root/leavetlgov-prod-deploy-ts.txt` | مُستبقى |

### حالة الخدمة النهائيّة بعد التنظيف

```
MainPID              = 623791
NRestarts            = 0
ActiveState          = active
ExecMainStartTimestamp = Thu 2026-08-06 18:19:21 UTC
health internal      = 200
health public        = 200
fail:/crit: since deploy = 0
Email__Enabled                    = false
EmailNotifications__Mode          = Enabled
ReportReminderScheduler__Enabled  = true
/etc/reporting-api.env mtime      = 1785095398
```

---

## 12. المخاطر المتبقّية والتوصيات

1. **حالة حبيبة لا تزال معلّقة فعليًّا** — الطابور يُظهرها بوضوح لكنّه لا يمنح أيّ زرّ قرار (`readOnly` معماريًّا في `LeaveRequestsPage.tsx:67` و`:826`). معالجتها قرار تشغيليّ يحتاج **تصريحًا مستقلًّا**.
2. **`AttentionAfterHours = 24` ثابت في الكود** لا إعداد. تغييره يستلزم إصدارًا جديدًا.
3. **مصفوفة RBAC الحيّة على الإنتاج غير مُكرَّرة** لتعذّر إنشاء حسابات اختبار دون كتابة؛ التغطية عبر RC + ختم SourceLink + إثبات 401 المجهول حيًّا.
4. لا يوجد أثر تدقيق لقراءة الطابور (مسار قراءة صرف)؛ إن طُلبت تلميتريّة استخدام لاحقًا فهي تذكرة مستقلّة.

---

## 13. ما هو **ممنوع** بلا تصريح جديد

نشر أيّ شيء آخر، معالجة حبيبة، أيّ اعتماد أو رفض، نقل الخصم إلى خطوة قائد الفريق، تعديل Ledger أو الرصيد، Reversal، تشغيل Scheduler، إرسال بريد، أيّ SQL كاتب، وأيّ hotfix إضافيّ.

---

## 14. القرار النهائيّ

```
LEAVE-TL-PENDING-GOVERNANCE-R1
PRODUCTION PASS
READ-ONLY GOVERNANCE QUEUE
ZERO BUSINESS IMPACT
READY FOR NEXT FEATURE
```
