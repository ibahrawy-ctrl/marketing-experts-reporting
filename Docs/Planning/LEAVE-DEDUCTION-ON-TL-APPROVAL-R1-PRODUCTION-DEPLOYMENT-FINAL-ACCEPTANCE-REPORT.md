# LEAVE-DEDUCTION-ON-TL-APPROVAL-R1 — تقرير النشر على الإنتاج والقبول النهائيّ

**التاريخ:** 2026-08-07
**النطاق:** نشر Backend فقط على بيئة الإنتاج (`reports.emarketingacademy.net`)
**نافذة النشر (TS):** `20260807-085500`
**القرار النهائيّ:** `PRODUCTION PASS`

---

## 1) السياق

أُغلقت مرحلة القبول على بيئة RC بقرار `RC PASS — READY FOR PRODUCTION APPROVAL`، ثمّ صدر تصريح صريح بالنشر على الإنتاج. هذه التذكرة **تذكرة نشر حصرًا**: لا تغيير كود، لا واجهة، لا هجرة، لا معالجة تاريخيّة، لا Backfill، لا تعديل لأيّ طلب قائم، لا إصلاح Ledger أو رصيد، لا تغيير مجدول أو بريد أو إشعارات، ولا بدء أيّ تذكرة أخرى.

نُفِّذت المراحل 0–14 كاملةً. **صفر عائق (blocker)، وصفر تراجُع (rollback) مُنفَّذ.**

---

## 2) قاعدة العمل المعتمَدة

| العنصر | السلوك المعتمَد |
|---|---|
| تقديم الموظّف | لا خصم |
| **اعتماد قائد الفريق** | **خصم `Debit` واحد في الدفتر — فوريّ** |
| اعتماد المدير | خطوة إجرائيّة/إشرافيّة — **بلا خصم جديد وبلا تأخير** |
| الاعتماد النهائيّ (HR) | خطوة إجرائيّة/إشرافيّة — **بلا خصم جديد وبلا تأخير** |
| رفض المدير / رفض HR / إلغاء | `Reversal` واحد ⇒ يُعاد الرصيد **مرّة واحدة** |
| إعادة للتعديل (`ReturnedForEdit`) | حسب السلوك المُثبَت في RC (القاعدة 8 = عكس) |

**ممنوع بنيويًّا:** اعتماد آليّ، رفض آليّ، تعديل رصيد يدويّ، خصم مزدوج، عكس مزدوج.

---

## 3) نَسَب المرشّح (Provenance)

| المعرّف | القيمة | الحالة |
|---|---|---|
| HEAD (المرشّح) | `ce166662f46598ed3593beed0105ba67059fc3bc` | ✅ مطابق |
| Parent (أساس الإنتاج) | `f2bd52c2664cd473f7aaf65f2a5a9953cbbf3099` | ✅ مطابق |
| Tree | `9b7d3dfe37b2f3a95ae583e5e8df4a6308c93608` | ✅ مطابق |
| Patch-id (`--stable`) | `86c093ce19c188345791bac96cee9a4076575764` | ✅ مطابق |
| عنوان الـcommit | `feat(leave): deduct balance at team leader approval (LEAVE-DEDUCTION-ON-TL-APPROVAL-R1)` | ✅ |

الشجرة المجمَّدة: `/private/tmp/cand-leave-deduct-r1-20260806`
`git status --porcelain` = **فارغ**؛ لا `MERGE_HEAD`/`REBASE_HEAD`/`rebase-merge`/`rebase-apply`/`CHERRY_PICK_HEAD`/`BISECT_LOG`؛ لا ملفّات غير متتبَّعة.

**سطح التغيير — 9 ملفّات، +1197/−93:**

```
reporting-backend/src/Reporting.Application/Leave/ILeaveBalanceLifecycleService.cs      |  63 ++
reporting-backend/src/Reporting.Domain/Enums/Enums.cs                                  |   4 +-
reporting-backend/src/Reporting.Infrastructure/DependencyInjection.cs                  |   1 +
reporting-backend/src/Reporting.Infrastructure/Services/LeaveBalanceLifecycleService.cs| 132 +++
reporting-backend/src/Reporting.Infrastructure/Services/LeaveRequestService.cs         | 136 ++--
reporting-backend/tests/Reporting.IntegrationTests/LeaveBalanceGuardTests.cs           |  24 +-
reporting-backend/tests/Reporting.IntegrationTests/LeaveDeductionOnTeamLeaderApprovalTests.cs | 881 +++
reporting-backend/tests/Reporting.IntegrationTests/LeaveWorkflowDeadlockHotfixTests.cs |  22 +-
reporting-backend/tests/Reporting.IntegrationTests/PermissionMonthlyLimitTests.cs      |  27 +-
```

**حرّاس السطح:** ملفّات واجهة = **0** · ملفّات هجرة = **0** · سكربتات/manifests إنتاج = **0** · أسرار = **0** (الإيجابيّات الوحيدة في مسح الأسرار كانت `CancellationToken` في تواقيع الدوالّ).

---

## 4) ما قبل النشر على الإنتاج (المرحلة 0 — قراءة فقط)

| الفحص | القيمة |
|---|---|
| الوقت (UTC) | `2026-08-07 08:57:45` (الرياض 11:57:45) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| حالة الخدمة | `active / running` |
| MainPID | `623791` |
| NRestarts | `0` |
| Health داخليّ | `200` |
| Health عامّ | `200` |
| SourceLink الحيّ (4 DLLs) | `1.0.0+f2bd52c2664cd473f7aaf65f2a5a9953cbbf3099` ✅ = الأساس المتوقَّع |
| عدد الهجرات | `30` |
| رأس الهجرات | `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| حزمة الواجهة | `index-CG2a9RiH.js` sha256 `9229470f627d83c9d27889db3d3a59ee49b9fa3ad209c4378bb30aed8c8304e0` |
| `email_outbox` | `0` |
| `EmailNotifications__Mode` | `Enabled` |
| `Email__Enabled` | `false` (علم قديم غير مرجعيّ للقناة الجديدة) |
| `ReportReminderScheduler__Enabled` | `true` |
| عمليّات نشر/هجرة/إصلاح جارية | **لا شيء** |
| تشغيل أدوات تسوية | **لا شيء** |
| أخطاء journald منذ الإقلاع | `fail:`=0 · `crit:`=0 |

**نشاط آخر 24 ساعة (تدقيق):** `submission.submitted 8` · `kpi.submitted 8` · `leave_request.hr_approved 3` · `leave_request.submitted 2` · `submission.approved 2` · `submission.returned 2` · `leave_request.team_leader_approved 1` — كلّها عمليّات مستخدمين طبيعيّة، **لا أثر لأيّ أداة أو معالجة آليّة**.

**النتيجة: الأساس مطابق تمامًا لما تنصّ عليه التذكرة ⇒ لا شرط توقّف.**

---

## 5) النسخ الاحتياطيّة (المرحلة 2)

| النوع | المسار | التحقّق |
|---|---|---|
| Backend | `/opt/reporting/publish-backup-leave-deduct-20260807-085500` | 86 ملفًّا · `www-data:www-data` · SourceLink القديم `1.0.0+f2bd52c2…` على الأربع DLLs |
| قاعدة البيانات | `/root/db-backups/reporting_prod-preleavededuct-20260807-085500.dump` | 1,183,378 بايت · sha256 `190a95aee24c209cc757ecfa8fd1797b9ea1af898ff5dd703110c859a43b5c4f` · `Archive created 2026-08-07 08:53:20 UTC` · `dbname: reporting_prod` · TOC Entries 337 (333 مرقّمة) · Compression gzip · Dump Version 1.15-0 |

**بصمات DLLs الأساس (للتراجُع):**
```
af851d13b10d09fe873b2b84ab8f3cf7f82b6d3354111b31a79cf41c0e66ed95  Reporting.Api.dll
6a1199f0204587213341cb1902448eb7cc67d5b95d475a688c00b75a5b63ad4a  Reporting.Application.dll
6b09c31b834e01adabb4870c1797c23c4da2f7d0df468921c676ba47cfe734a5  Reporting.Domain.dll
3a6f7d579a5ba9ce42cc84d1bf3ea526fc1233829e1c46dc9d5e6b95a50a07c4  Reporting.Infrastructure.dll
```

**لم يُحذف أيّ نسخة احتياطيّة قائمة** (سلسلة النسخ من `publish-backup-twf1-20260623-122423` حتّى `publish-backup-weeklyfloor-20260723-182046` سليمة).

---

## 6) البناء (المرحلة 1/3)

```bash
export DOTNET_ROOT=/Users/ibrahimelbahrawi/.dotnet && export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
cd /private/tmp/cand-leave-deduct-r1-20260806/reporting-backend
find src -maxdepth 2 -type d \( -name bin -o -name obj \) -exec rm -rf {} +
SHA=ce166662f46598ed3593beed0105ba67059fc3bc
dotnet restore src/Reporting.Api/Reporting.Api.csproj
dotnet build   src/Reporting.Api/Reporting.Api.csproj -c Release --no-restore \
  -p:SourceRevisionId=$SHA -p:ContinuousIntegrationBuild=true
dotnet publish src/Reporting.Api/Reporting.Api.csproj -c Release -o /private/tmp/ldr1-prod-publish --no-restore \
  -p:SourceRevisionId=$SHA -p:ContinuousIntegrationBuild=true
```

| الفحص | النتيجة |
|---|---|
| نتيجة البناء | `Build succeeded. 0 Warning(s) 0 Error(s)` |
| SourceLink على الأربع DLLs | `1.0.0+ce166662f46598ed3593beed0105ba67059fc3bc` ✅ |
| عدد ملفّات الحزمة | 86 |
| `appsettings.Development.json` | 0 |
| ملفّات env | 0 |
| أصول واجهة | 0 |
| سكربتات `.sh` | 0 |
| أسرار | لا شيء |
| `appsettings.json` md5 | `d51e726f6d06e1fa41db71cf8ed9a4c9` — **مطابق حرفيًّا للحيّ على الإنتاج** ⇒ الإعداد لم يُمَسّ |

**بصمات المرشّح:**
```
5f25a0a5113ac2130a9362bb484f1b1538a85eac9c010af7460035243af198fe  Reporting.Api.dll
c8c01382d0666c9fd37c633f1f79977017f9f59bd1a86970bc2294e5210d0167  Reporting.Application.dll
eed4a99cf49a15f5fd328a27156f95ef19e18143404318958b8ef3092d6f1c1d  Reporting.Domain.dll
15cd6613ddcb811f3a37ae554051abb9b215c378fd3624f3b7666d1fc48606bd  Reporting.Infrastructure.dll
```

**ثوابت الهجرات في المرشّح:** مجلد الهجرات = 30 ملفًّا؛ `git diff --name-only f2bd52c2 HEAD -- '*Migrations*'` = **0**.

---

## 7) التجهيز (Staging)

```bash
rsync -az --delete -e "ssh -i ~/.ssh/academy_vps_ed25519" ./ \
  root@187.127.72.232:/opt/reporting/publish-staging-leave-deduct-20260807-085500/
```

مقارنة manifest محليّ ↔ بعيد (sha256 لكلّ ملفّ، بعد توحيد المسافات):
**`BYTE-FOR-BYTE IDENTICAL: 86/86 files`** — `diff` فارغ.
بصمة الـmanifest المحليّ: `ef0ea0235a47bdd02f18372d0843f3ffbe51c77f1f37a0d943ad58694abed1a9`.

---

## 8) الخطّ الزمنيّ للنشر (المرحلة 4)

| الحدث | التوقيت (UTC) |
|---|---|
| لقطة ما قبل التوقّف | `08:57:45.215590589` |
| `T_STOP` (أمر التوقّف) | `08:57:45.233931034` |
| توقّفت الخدمة | `08:57:45.461129369` |
| استبدال `publish` (rsync + chown + chmod) | بين `.461` و `.721` |
| `T_START` (أمر البدء) | `08:57:45.721380044` |
| بدأت الخدمة | `08:57:45.736803908` |

**إعادة تشغيل واحدة حصرًا.** ما نُفِّذ: `systemctl stop` واحد ⟵ `rsync -a --delete` من مسار التجهيز إلى `/opt/reporting/publish` ⟵ `chown -R www-data:www-data` + `chmod 644/755` ⟵ `systemctl start` واحد.

**لم يُمَسّ:** حزمة الواجهة `dist` · nginx · وحدة systemd · سكيمة قاعدة البيانات · إعداد المجدول · إعداد البريد · `/etc/reporting-api.env`.

---

## 9) مدّة الانقطاع

**`T_STOP` = 08:57:45.234 → `T_START` = 08:57:45.721 ⇒ الانقطاع ≈ 0.49 ثانية** (والفارق بين توقّف الخدمة فعليًّا وبدئها فعليًّا ≈ 0.276 ثانية). الخدمة صارت `Now listening` عند `08:57:50` بعد الإقلاع الطبيعيّ لـEF Core.

---

## 10) التحقّق من SourceLink

| DLL | SourceLink بعد النشر |
|---|---|
| `Reporting.Api.dll` | `1.0.0+ce166662f46598ed3593beed0105ba67059fc3bc` |
| `Reporting.Application.dll` | `1.0.0+ce166662f46598ed3593beed0105ba67059fc3bc` |
| `Reporting.Domain.dll` | `1.0.0+ce166662f46598ed3593beed0105ba67059fc3bc` |
| `Reporting.Infrastructure.dll` | `1.0.0+ce166662f46598ed3593beed0105ba67059fc3bc` |

**بصمات المنشور == بصمات المُجهَّز == بصمات البناء المحلّيّ** (`5f25a0a5…` / `c8c01382…` / `eed4a99c…` / `15cd6613…`) — تطابق تامّ على الثلاثة مستويات.

---

## 11) ثوابت الهجرات

| الفحص | القيمة |
|---|---|
| سجلّ الإقلاع | `info: Microsoft.EntityFrameworkCore.Migrations[20405] — No migrations were applied. The database is already up to date.` |
| عدد الهجرات في القاعدة | `30` (قبل = بعد) |
| رأس الهجرات | `20260724224053_AddReportApproverAndKpiReviewerOverrides` (قبل = بعد) |
| فهارس `employee_balance_ledger` | 3 فهارس بلا تغيير، منها الفريد الجزئيّ `IX_employee_balance_ledger_RelatedRequestId_Source` = `CREATE UNIQUE INDEX ... USING btree ("RelatedRequestId","Source") WHERE ("RelatedRequestId" IS NOT NULL)` |

**صفر تغيير سكيمة.**

---

## 12) الصحّة (Health) بعد النشر

| الفحص | النتيجة |
|---|---|
| حالة الخدمة | `active / running` |
| MainPID الجديد | `654185` (كان `623791`) |
| `NRestarts` | `0` |
| `ExecMainStartTimestamp` | `Fri 2026-08-07 08:57:45 UTC` |
| Health داخليّ | `200` — `{"status":"ok","service":"reporting-api"}` |
| Health عامّ | `200` |
| `Hosting environment` | `Production` |
| `Content root path` | `/opt/reporting/publish` |
| `Now listening on` | `http://127.0.0.1:5090` |
| `Database` في سلسلة الاتّصال | `reporting_prod` |
| `fail:` | `0` |
| `crit:` | `0` |
| `Unhandled exception` | `0` |
| `appsettings.json` md5 | `d51e726f6d06e1fa41db71cf8ed9a4c9` (بلا تغيير) |
| `/etc/reporting-api.env` mtime | `1785095398` (بلا تغيير) · mode `600` · `root:root` |
| `appsettings.Development.json` | غائب (0) |
| حزمة الواجهة | sha256 `9229470f…` — **بلا تغيير** |

التحذيرات الوحيدة في السجلّ هي تحذيرات `Microsoft.EntityFrameworkCore.Model.Validation[10622]` القائمة مسبقًا حول `global query filter` (KpiEvaluation / ReportSubmission) — سلوك قائم قبل النشر ولا علاقة له بالتذكرة.

---

## 13) الدخان القرائيّ (المرحلة 6)

**مجهول ⇒ 401 (5/5):**

| المسار | الرمز |
|---|---|
| `GET /api/leave-requests/pending` | 401 |
| `GET /api/leave-requests/my` | 401 |
| `GET /api/leave-requests/governance/team-leader-pending` | 401 |
| `GET /api/me/balances` | 401 |
| `GET /api/balances/employees` | 401 |

**مصادَق (Admin، التوكن لم يُطبع) ⇒ 200 (8/8):**

| المسار | الرمز |
|---|---|
| `GET /api/leave-requests/my` | 200 |
| `GET /api/leave-requests/pending` | 200 |
| `GET /api/leave-requests/governance/team-leader-pending` | 200 |
| `GET /api/me/balances` | 200 |
| `GET /api/balances/employees` | 200 |
| `GET /api/dashboard/me` | 200 |
| `GET /api/notifications` | 200 |
| `GET /api/employee-service-requests` | 200 |

**كلّ الطلبات `GET` حصرًا.** صفر `POST`/`PUT`/`PATCH`/`DELETE`، صفر اعتماد، صفر رفض، صفر إلغاء، صفر إنشاء طلب، صفر كتابة رصيد.

---

## 14) ثوابت البيانات — Zero Delta (المرحلة 7)

| المؤشّر | قبل | بعد | الدلتا |
|---|---|---|---|
| `leave_requests` | 19 | 19 | **0** |
| `leave_request_events` | 67 | 67 | **0** |
| `employee_balance_ledger` (إجماليّ) | 97 | 97 | **0** |
| `Debit` مصدره `ApprovedLeave` | 13 | 13 | **0** |
| `Debit` مصدره `ApprovedPermission` | 1 | 1 | **0** |
| صفوف `Reversal` | 0 | 0 | **0** |
| `audit_logs` | 1148 | 1148 | **0** |
| `notifications` | 628 | 628 | **0** |
| `email_notifications` | 362 | 362 | **0** |
| `email_outbox` | 0 | 0 | **0** |
| **بصمة الأرصدة (md5)** | `9fc4123855dfe7c217b3758b05e7b509` | `9fc4123855dfe7c217b3758b05e7b509` | **مطابقة** |
| مجموعات الأرصدة | 66 | 66 | **0** |
| أدنى رصيد متبقٍّ | `-1.00` | `-1.00` | **0** (سالب **قائم مسبقًا**، السياسة تسمح به، ولم يُعالَج) |
| خصم مزدوج (`dup_debit`) | 0 | 0 | **0** |
| عكس مزدوج (`dup_reversal`) | 0 | 0 | **0** |

**توزيع الحالات/الخطوات (قبل = بعد حرفيًّا):**

| Status | CurrentStep | العدد |
|---|---|---|
| `HrApproved` | `Completed` | 14 |
| `ManagerApproved` | `Hr` | 1 |
| `ReturnedForEdit` | `Employee` | 1 |
| `Submitted` | `TeamLeader` | 1 |
| `TeamLeaderApproved` | `Manager` | 2 |

**أقصى الطوابع الزمنيّة (قبل = بعد حرفيًّا):**

| الجدول | `MAX(CreatedAtUtc)` | `MAX(UpdatedAtUtc)` |
|---|---|---|
| `leave_requests` | `2026-08-06 11:56:01.355667+00` | `2026-08-06 09:10:00.457692+00` |
| `leave_request_events` | `2026-08-06 11:56:01.358434+00` | — |
| `employee_balance_ledger` | `2026-08-06 09:01:13.305293+00` | — |
| `audit_logs` | `2026-08-07 06:49:32.598568+00` | — |

**أقصى طابع في الدفتر وطلبات الإجازة سابق للنشر بأكثر من يوم كامل ⇒ إثبات مباشر أنّ النشر لم يكتب أيّ صفّ.**

**الحكم: دلتا بيانات الأعمال الناتجة عن النشر ذاته = صفر مطلق. الكود الجديد لم يعالج آليًّا أيّ طلب قديم.**

---

## 15) إثبات السلامة التاريخيّة (المرحلة 8)

| الفحص | النتيجة |
|---|---|
| خصم جديد على طلب قديم بسبب النشر | **0** |
| عكس جديد | **0** |
| Backfill | **لا شيء** |
| صفوف دفتر جديدة من الإقلاع | **0** (الإجماليّ ثابت 97) |
| تغيّر رصيد من الإقلاع | **0** (البصمة `9fc41238…` مطابقة) |

**الطلبات الخمسة قيد المسار — كلّها بـ`ledger_rows = 0` قبل النشر وبعده:**

| المعرّف | الحالة | الخطوة | النوع | صفوف الدفتر |
|---|---|---|---|---|
| `a96197b7-f6c5-4050-b236-6030deca9bb4` | `ReturnedForEdit` | `Employee` | Leave | 0 |
| `55a0a0eb-a72d-4407-845f-01c4a54f1cb3` | `Submitted` | `TeamLeader` | Leave | 0 |
| `7f3ed527-a4b6-40d3-b461-5f11b49a76b9` | `ManagerApproved` | `Hr` | Permission | 0 |
| `e7df1f62-bc03-4998-872f-dd12c74c72cf` | `TeamLeaderApproved` | `Manager` | Permission | 0 |
| `7f010c59-2815-4351-9686-c2d5ecaa325d` | `TeamLeaderApproved` | `Manager` | Leave | 0 |

**طلب حبيبة `55a0a0eb-a72d-4407-845f-01c4a54f1cb3` — قراءة فقط، لم يُمَسّ:**

| الحقل | قبل | بعد |
|---|---|---|
| `Status` | `Submitted` | `Submitted` |
| `CurrentStep` | `TeamLeader` | `TeamLeader` |
| `Type` | `Leave` | `Leave` |
| `CreatedAtUtc` | `2026-07-15 13:14:43.621433+00` | مطابق |
| `UpdatedAtUtc` | `NULL` | `NULL` |
| صفوف الدفتر | `0` | `0` |

**ملاحظة جوهريّة:** الطلبان `TeamLeaderApproved/Manager` اعتُمِدا تحت الكود **القديم** (الخصم كان عند HR) ⇒ لا خصم لهما، والكود الجديد **لم يخصم لهما رجعيًّا**. سيقع خصمهما طبيعيًّا عند أوّل انتقال اعتماد لاحق (`ManagerApproved`/`HrApproved`) عبر مسار `ApplyDebitOnTeamLeaderApprovalAsync` الـidempotent — أي خصم **واحد** لكلٍّ منهما، وهو السلوك الصحيح المصمَّم لا انحرافًا.

---

## 16) إثبات الكود المنشور وقت التشغيل (المرحلة 10)

فحص الميتاداتا في الـDLLs **المنشورة حيًّا** (`/opt/reporting/publish`):

| الرمز | العدد | البيان |
|---|---|---|
| `ILeaveBalanceLifecycleService` (Application) | 1 | العقد منشور |
| `ILeaveBalanceLifecycleService` (Infrastructure) | 1 | حقن التبعيّة مُسجَّل |
| `LeaveBalanceLifecycleService+<ApplyDebitOnTeamLeaderApprovalAsync>d__3` | ✅ | **مسار الخصم عند اعتماد قائد الفريق** |
| `LeaveBalanceLifecycleService+<ReverseDebitAsync>d__4` | ✅ | **مسار العكس** |
| `LeaveBalanceLifecycleService+<GetCurrentBalanceLifecycleStateAsync>d__5` | ✅ | قراءة الحالة |
| `LeaveBalanceLifecycleService+<HasEntryAsync>d__6` | ✅ | **حارس الـidempotency الطبقة 1** |
| `LeaveBalanceLifecycleService+<FindDebitAsync>d__7` | ✅ | إيجاد الخصم الأصليّ للعكس |
| `LeaveRequestService+<SaveWithLedgerConcurrencyGuardAsync>d__30` | ✅ | **حارس التزامن ⟵ 409** |
| `LeaveBalanceLifecycleOutcome` / `LeaveBalanceLifecycleState` | ✅ | أنواع النتائج |
| `leave_request.concurrent_decision.conflict` (UTF-16LE) | 1 | كود الخطأ `409` منشور |
| `manager_step_auto_folded_no_operational_manager` (UTF-16LE) | 1 | حارس P2 سليم بلا تغيير |
| `BalanceSource` (Domain) | `OpeningBalance, ApprovedLeave, ApprovedPermission, ManualAdjustment, CarryOver, Reversal` | كامل بلا حذف |

**مقارنة الأساس ↔ الحيّ (إثبات أنّ التغيير وقع فعلًا):**

| الرمز | نسخة الأساس `f2bd52c2` | الحيّ `ce166662` |
|---|---|---|
| `LeaveBalanceLifecycleService` في Infrastructure | **0** | **6** |
| `ILeaveBalanceLifecycleService` في Application | **0** | **1** |

**مواضع الاستدعاء في المصدر المجمَّد (`LeaveRequestService.cs`):**

| السطر | الاستدعاء | السياق |
|---|---|---|
| 861 | `ApplyDebitOnTeamLeaderApprovalAsync` | عند `toStatus ∈ {TeamLeaderApproved, ManagerApproved, HrApproved}` |
| 869 | `ReverseDebitAsync` | عند `toStatus ∈ {TeamLeaderRejected, ManagerRejected, HrRejected}` |
| 577 | `ReverseDebitAsync` | إلغاء الطلب من مقدّمه |
| 648 / 686 | `ReverseDebitAsync` | مساران إضافيّان (إعادة/إبطال) |
| 874 | `SaveWithLedgerConcurrencyGuardAsync` | **`SaveChanges` واحد للحالة والحركة معًا** |

**«لا خصم عند المدير/HR» — البرهان:** الاستدعاء يقع على كلّ انتقال اعتماد **عمدًا** (لأنّ الطلب قد يبدأ عند خطوة المدير أصلًا حين تُتخطّى خطوة قائد الفريق)، لكنّه **idempotent** بشرط `HasEntryAsync(entity.Id, source)` الذي يفحص **الصفوف المحفوظة في القاعدة و`.Local` غير المحفوظة معًا** ويُرجِع `AlreadyApplied` بلا كتابة. النتيجة العمليّة: **خصم واحد لكلّ طلب مهما تعدّدت خطوات الاعتماد** — أي أنّ اعتماد المدير واعتماد HR لا يُنتجان أيّ حركة جديدة.

**حدود المعاملة:** `LeaveBalanceLifecycleService` **لا يستدعي `SaveChanges` إطلاقًا** — يُدرِج في متتبّع التغيير فقط، ويملك المستدعي المعاملة فيحفظ تغيير الحالة والحركة في `SaveChanges` واحد ⇒ ذرّيّة تامّة.

**دلالات الفهرس الفريد بلا تغيير:** الطبقة الثانية للحماية هي الفهرس الفريد الجزئيّ على القاعدة، وقد أُثبت بقاؤه حرفيًّا في §11 — و`23505` يُترجَم إلى `409 leave_request.concurrent_decision.conflict`.

**لم يُعدَّل أيّ شيء أثناء هذا الفحص** (قراءة ميتاداتا فقط).

---

## 17) عدم التأثير على الحوكمة (المرحلة 11)

`GET /api/leave-requests/governance/team-leader-pending` ⇒ **200**، والحمولة الحيّة:

```json
{
  "requestId": "55a0a0eb-a72d-4407-845f-01c4a54f1cb3",
  "requestType": "Leave",
  "employeeName": "حبيبة",
  "departmentName": "المبيعات",
  "teamName": "فريق B2B",
  "teamLeaderName": "محمد عبدالقوي",
  "createdAtUtc": "2026-07-15T13:14:43.621433Z",
  "startDate": "2026-07-19",
  "endDate": "2026-07-20",
  "requestedUnits": 2,
  "currentStatus": "Submitted",
  "currentStep": "TeamLeader",
  "lastEventType": "submitted",
  "daysPending": 23,
  "startedAlready": true,
  "endedAlready": true,
  "delayStatus": "ExpiredUnresolved",
  "hasLedger": false,
  "ledgerCount": 0,
  "isEmployeeActive": true,
  "isTeamLeaderActive": true,
  "missingTeamLeaderAssignment": false
}
```

| الفحص | النتيجة |
|---|---|
| نقطة الحوكمة | `200` |
| حبيبة ما زالت في الطابور بـ`ExpiredUnresolved` | ✅ |
| `hasLedger=false` / `ledgerCount=0` | ✅ (لم يُخصَم لها شيء) |
| `total` / `totalPending` | `1` / `1` (بلا تغيير) |
| حالة حبيبة غُيِّرت | **لا** |
| انحدار في طابور القرارات (`/pending`) | **لا** (200) |
| انحدار واجهة | **لا** (حزمة الواجهة لم تُمَسّ، sha256 ثابتة) |

**`LEAVE-TL-PENDING-GOVERNANCE-R1` يعمل كما هو تمامًا.**

---

## 18) عدم التأثير على البريد والمجدول والإشعارات (المرحلة 12)

| الفحص | قبل | بعد |
|---|---|---|
| `EmailNotifications__Mode` | `Enabled` | `Enabled` |
| `Email__Enabled` | `false` | `false` |
| `ReportReminderScheduler__Enabled` | `true` | `true` |
| `/etc/reporting-api.env` mtime | `1785095398` | `1785095398` |
| `email_outbox` | 0 | **0** |
| `email_notifications` | 362 | **362** |
| توزيعها | `DryRun\|DryRun 139` + `Enabled\|Sent 223` | مطابق |
| `notifications` | 628 | **628** |
| أسطر SMTP منذ النشر | — | **0** |
| أسطر إرسال بريد / MailKit | — | **0** |
| تشغيل مجدول يدويّ | — | **لم يُنفَّذ** |
| مهامّ خلفيّة جديدة | — | **لا شيء** |

السطر الوحيد المطابق لكلمة «reminder» في السجلّ هو **استعلام قراءة** لفحص عدم التكرار في `SubmissionReminderService` القائم مسبقًا:
`WHERE n."Type" = 'submission.reminder' AND n."Link" = @__link_0 AND n."RecipientId" = ANY (@__candidates_1)` — استعلام `SELECT` بحت، لم يُنتج أيّ صفّ (`notifications` ثابت 628).

**لا دلتا غير مفسَّرة ⇒ لا شرط توقّف.**

---

## 19) عدم تأثّر الدفتر (Ledger) عند الإقلاع

| الفحص | النتيجة |
|---|---|
| إجماليّ صفوف الدفتر | `97` قبل = `97` بعد |
| `Debit / ApprovedLeave` | `13` = `13` |
| `Debit / ApprovedPermission` | `1` = `1` |
| صفوف `Reversal` | `0` = `0` |
| `MAX(CreatedAtUtc)` في الدفتر | `2026-08-06 09:01:13.305293+00` (قبل = بعد) — **أقدم من النشر بـ~24 ساعة** |
| خصم مزدوج | `0` |
| عكس مزدوج | `0` |
| فهارس الدفتر | 3، بلا تغيير، الفريد الجزئيّ قائم |

**الإقلاع لم يكتب أيّ صفّ في الدفتر — صفر Backfill.**

---

## 20) عدم تأثّر الأرصدة عند الإقلاع

| الفحص | قبل | بعد |
|---|---|---|
| بصمة الأرصدة md5 (`Σ Credit − Σ Debit` لكلّ `(EmployeeId, BalanceType, Year)`) | `9fc4123855dfe7c217b3758b05e7b509` | `9fc4123855dfe7c217b3758b05e7b509` |
| عدد المجموعات | 66 | 66 |
| أدنى رصيد متبقٍّ | `-1.00` | `-1.00` |

**تطابق تامّ.** الرصيد السالب الوحيد (`-1.00`) **قائم قبل النشر**، والسياسة تسمح بالسالب (`AllowNegativeBalance=true`)؛ **لم يُعالَج ولم يُصحَّح** التزامًا بحدود التذكرة.

---

## 21) جاهزيّة التراجُع (المرحلة 13)

| العنصر | الحالة |
|---|---|
| نسخة Backend الاحتياطيّة | `/opt/reporting/publish-backup-leave-deduct-20260807-085500` — 86 ملفًّا، `www-data:www-data`، SourceLink `1.0.0+f2bd52c2…` ✅ صالحة |
| نسخة قاعدة البيانات | `/root/db-backups/reporting_prod-preleavededuct-20260807-085500.dump` — 1,183,378 بايت، sha256 `190a95ae…`، `pg_restore -l` سليم (333 مُدخَلًا) ✅ صالحة |
| SourceLink القديم معروف | `f2bd52c2664cd473f7aaf65f2a5a9953cbbf3099` ✅ |
| مسار التجهيز محفوظ | `/opt/reporting/publish-staging-leave-deduct-20260807-085500` ✅ |
| هل يلزم عكس هجرة؟ | **لا** — صفر هجرة مُطبَّقة، الرأس ثابت `20260724224053` (30 هجرة) |

**خطوات التراجُع (موثَّقة، غير مُنفَّذة):**

```bash
systemctl stop reporting-api
rsync -a --delete /opt/reporting/publish-backup-leave-deduct-20260807-085500/ /opt/reporting/publish/
chown -R www-data:www-data /opt/reporting/publish
systemctl start reporting-api
curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5090/health          # متوقَّع 200
strings -a /opt/reporting/publish/Reporting.Infrastructure.dll | grep -oE '1\.0\.0\+[0-9a-f]{40}' | head -1
#   متوقَّع 1.0.0+f2bd52c2664cd473f7aaf65f2a5a9953cbbf3099
# صفر تغيير سكيمة ⇒ لا حاجة لاستعادة قاعدة البيانات إطلاقًا
```

**لم يُستدعَ التراجُع — لا عائق ظهر.**

---

## 22) استراتيجيّة الإثبات الوظيفيّ على الإنتاج (المرحلة 9)

**الفحص:** بحث عن أيّ حساب اختبار رسميّ أو fixture معتمَد على الإنتاج:

| النمط | العدد |
|---|---|
| `%@uat.local` | **0** |
| `%@test.local` | **0** |
| `%qa%` | **0** |
| إجماليّ المستخدمين | 33 (كلّهم موظّفون حقيقيّون) |

**النتيجة: لا يوجد أيّ fixture رسميّ على الإنتاج ⇒ ولم يُنشأ أيّ واحد**، عملًا بنصّ التذكرة حرفيًّا: «إن لم يوجد fixture رسميّ فلا تُنشئ واحدًا».

**لم يُنشأ أيّ طلب إجازة لموظّف حقيقيّ، ولم تُكتب أيّ بيانات اختبار على الإنتاج، ولم تُختلَق أيّ بيانات.**

**أسس القبول الوظيفيّ المعتمَدة بدلًا من ذلك — أربعة أعمدة مستقلّة:**

1. **الإثبات الوظيفيّ على RC:** الاختبارات المستهدَفة للإجازات/الأرصدة **137/137 PASS**، الوحدة **313/313 PASS**، فشل حصريّ على المرشّح = **0**، وقبول RC **51/52** (البند الوحيد غير المتحقّق `J1` كان تأكيد HTTP وُضِع أشدّ ممّا تطلبه التذكرة، والدفتر أثبت خصمًا واحدًا `3.00`).
2. **نَسَب الكود المنشور:** SourceLink الحيّ على الأربع DLLs = `ce166662…` **نفسه** الذي اجتاز RC، وبصمات sha256 مطابقة بايتًا ببايت من البناء المحلّيّ ⟵ التجهيز ⟵ الإنتاج.
3. **دلتا نشر صفريّة:** §14 — كلّ مؤشّرات الأعمال قبل = بعد، وبصمة الأرصدة مطابقة.
4. **دخان قرائيّ حيّ:** §13 — 5/5 مجهول 401 و8/8 مصادَق 200، وطابور الحوكمة يعمل بحمولة صحيحة.

**أيّ تحقّق فعليّ من الخصم عند اعتماد قائد الفريق على الإنتاج يجب أن يأتي من طلب جديد حقيقيّ خلال التشغيل الطبيعيّ — لا من fixture اصطناعيّ.**

---

## 23) المخاطر

| # | الخطر | التقييم | الضبط |
|---|---|---|---|
| R1 | الطلبان `TeamLeaderApproved/Manager` القائمان بلا خصم | **منخفض — سلوك مصمَّم** | سيقع لهما خصم **واحد** عند أوّل اعتماد لاحق عبر المسار الـidempotent؛ لا خصم رجعيّ ولا ازدواج (§15) |
| R2 | خصم مزدوج عند تعدّد خطوات الاعتماد | **مُنتفٍ** | طبقتان: فحص تطبيقيّ يشمل `.Local`+القاعدة، والفهرس الفريد الجزئيّ على القاعدة (§16، §11) |
| R3 | عكس مزدوج عند تعدّد مسارات الرفض/الإلغاء | **مُنتفٍ** | `HasEntryAsync(id, Reversal)` يُرجِع `AlreadyApplied`؛ وبلا خصم سابق ⇒ `NoDebitToReverse` بلا كتابة |
| R4 | تعارض تزامنيّ بين معاملتين | **مُدار** | `23505` ⟵ `409 leave_request.concurrent_decision.conflict` عبر `SaveWithLedgerConcurrencyGuardAsync` |
| R5 | رصيد سالب `-1.00` قائم | **مقبول — قائم مسبقًا** | السياسة تسمح بالسالب؛ لم يُمَسّ ولم يتغيّر (§20). أيّ معالجة تحتاج تصريحًا مستقلًّا |
| R6 | طلب حبيبة العالق منذ 23 يومًا | **خارج نطاق التذكرة** | قُرئ فقط؛ يظهر في طابور الحوكمة بـ`ExpiredUnresolved`؛ **ممنوع معالجته بلا تصريح جديد** |
| R7 | البريد حيّ فعليًّا على الإنتاج (`Mode=Enabled`) | **بلا تغيير** | صفر SMTP وصفر صفّ بريد جديد منذ النشر (§18) |
| R8 | فشل تشغيليّ بعد النشر | **منخفض** | تراجُع كامل جاهز خلال ثوانٍ بلا عكس هجرة (§21) |

---

## 24) القرار النهائيّ

كلّ المراحل 0–14 اجتازت بلا عائق. الانقطاع ≈ نصف ثانية، إعادة تشغيل واحدة، صفر هجرة، صفر تغيير سكيمة، صفر تغيير إعداد، صفر تغيير واجهة، وصفر دلتا في بيانات الأعمال.

```
LEAVE-DEDUCTION-ON-TL-APPROVAL-R1
PRODUCTION PASS
DEBIT AT TEAM LEADER APPROVAL DEPLOYED
NO DUPLICATE DEBIT
REVERSAL LOGIC DEPLOYED
NO AUTO APPROVAL
NO AUTO REJECTION
ZERO STARTUP BACKFILL
ZERO HISTORICAL DATA CHANGE
GOVERNANCE QUEUE UNAFFECTED
READY FOR NORMAL OPERATIONS
```

---

## توقّف مُلزَم بعد هذا التقرير

**ممنوع بلا تصريح جديد صريح:**
معالجة تاريخيّة · تعديل طلب حبيبة `55a0a0eb…` · Backfill · تسوية Ledger أو رصيد · معالجة الرصيد السالب `-1.00` · تغيير المجدول · تغيير البريد · تذكيرات أو تصعيد · أيّ إجراء آليّ · بدء أيّ تذكرة أخرى.

**أيّ اختبار خصم فعليّ على الإنتاج يجب أن ينبع من طلب جديد حقيقيّ خلال التشغيل الطبيعيّ، لا من fixture مصطنَع داخل الإنتاج.**
