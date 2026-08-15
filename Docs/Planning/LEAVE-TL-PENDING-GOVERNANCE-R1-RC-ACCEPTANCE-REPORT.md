# LEAVE-TL-PENDING-GOVERNANCE-R1 — تقرير قبول RC

**التذكرة:** `LEAVE-TL-PENDING-GOVERNANCE-R1`
**النوع:** حوكمة ورؤية قراءة-فقط للطلبات المعلّقة عند خطوة قائد الفريق
**التاريخ:** 6 أغسطس 2026
**البيئة:** RC حصرًا (`khubara-reporting-rc` / منفذ 5092 / قاعدة `reporting_rc`)
**الحالة النهائية:** `RC PASS — READY FOR PRODUCTION APPROVAL`

---

## 1. الملخّص التنفيذي

نُفِّذت التذكرة بالكامل عبر مراحلها الأربع عشرة (P1–P14) وقُبِلت على بيئة RC.

الناتج هو **طابور حوكمة قراءة-فقط** يعرض لأصحاب الصلاحية الإدارية (Admin / CEO / GeneralManager / HR / CeoSupport) كلّ طلبات الإجازة والاستئذان **العالقة عند خطوة قائد الفريق** على مستوى الشركة، مع تصنيف تأخّر محسوب، وعدّادات، ومرشّحات، وسبب تأخّر نصّي لكلّ صفّ.

**ما لم يُنفَّذ عمدًا (خارج النطاق بنصّ التذكرة):** لا نقل للخصم إلى اعتماد قائد الفريق، لا تعديل Ledger، لا تعديل رصيد، لا Reversal، لا اعتماد/رفض لأيّ طلب، لا Scheduler، لا بريد، لا معالجة لحالة حبيبة، لا Auto Action، ولا نشر إنتاج.

المرشّح **مُجمَّد** في commit واحد فوق الأساس المتوقَّع، **بلا أيّ هجرة**، و**بلا أيّ مسار كتابة جديد**.

---

## 2. خطّ الأساس المتوقَّع والمُثبَت

| البند | المتوقَّع في التذكرة | المُثبَت |
|---|---|---|
| Backend SourceLink | `2d282cebf0a22f65b78cd751de17d6c927128d0d` | ✅ مطابق (أب المرشّح) |
| عدد الهجرات | 30 | ✅ 30 |
| رأس الهجرات | `20260724224053` | ✅ `20260724224053_AddReportApproverAndKpiReviewerOverrides` |

شجرة الأساس أُنشئت محليًّا للمقارنة: `/private/tmp/base-leave-tl-gov-2d282ce` (detached عند `2d282ce`) وبُنيت Release بنجاح (0 أخطاء / 4 تحذيرات).

---

## 3. تجميد المرشّح (P9)

```
SHA      = f2bd52c2664cd473f7aaf65f2a5a9953cbbf3099
PARENT   = 2d282cebf0a22f65b78cd751de17d6c927128d0d
TREE     = 77b9ce1d61b0f684cc82b58946ba6beb7b107822
PATCH_ID = 887022726c6ec1d6c2518d727d475491425c3e24
BRANCH   = candidate/leave-tl-pending-governance-r1-20260806
WORKTREE = /private/tmp/cand-leave-tl-gov-r1-20260806
```

**الإحصاء:** `11 files changed, 2074 insertions(+), 8 deletions(-)`
**الهجرات:** إجمالي 30 — وعدد ملفّات الهجرات التي مسّها الـcommit = **0**
**نظافة الشجرة:** `git status --porcelain` ⟶ `?? reporting-backend/publish-candidate/` فقط (مخرَج بناء مؤقّت، خارج الـcommit).

| الملفّ | ± |
|---|---|
| `Reporting.Api/Controllers/LeaveRequestsController.cs` | +8 |
| `Reporting.Api/Program.cs` | +4 |
| `Reporting.Application/Common/Roles.cs` | +21 |
| `Reporting.Application/Leave/ILeaveRequestService.cs` | +9 |
| `Reporting.Application/Leave/LeaveRequestModels.cs` | +85 |
| `Reporting.Infrastructure/Services/LeaveRequestService.cs` | +206 |
| `tests/…/LeaveTeamLeaderPendingGovernanceTests.cs` | +759 |
| `reporting-frontend/src/lib/format.ts` | +18 |
| `reporting-frontend/src/pages/LeaveGovernanceTab.test.tsx` | +549 |
| `reporting-frontend/src/pages/LeaveRequestsPage.tsx` | +373 |
| `reporting-frontend/src/types/api.ts` | +50 |

---

## 4. نموذج القراءة الخلفيّ (P1)

الخدمة `LeaveRequestService.TeamLeaderPendingGovernanceAsync` (+206 سطرًا) استعلام **قراءة-فقط بالكامل**:

- `AsNoTracking()` على كلّ الاستعلامات.
- الشرط الأساسيّ: `Status == Submitted && CurrentStep == TeamLeader`.
- **على مستوى الشركة** (لا `ScopeResolver`، النطاق مفروض بالسياسة لا بالشجرة الإداريّة).
- ترطيب القواميس (Users / Teams / TeamLeaders / Ledger counts) مسبقًا لتفادي N+1.
- لا `SaveChanges`، لا `Add`, لا `Update`, لا `Remove` في المسار كلّه.

الحقول المُشتقّة لكلّ صفّ: `employeeName`, `teamName`, `teamLeaderName`, `missingTeamLeader`, `teamLeaderActive`, `daysPending`, `daysUntilStart`, `hasStarted`, `hasEnded`, `ledgerCount`, `hasLedger`, `type`, `units`, `delayStatus`, `delayReason`.

---

## 5. تصنيف التأخّر

| التصنيف | الشرط | النصّ العربيّ |
|---|---|---|
| `Pending` | ضمن العتبة ولم يبدأ الموعد | «بانتظار بتّ قائد الفريق ضمن عتبة المتابعة.» |
| `Attention` | مضى > `AttentionAfterHours = 24` ساعة دون بتّ | «مضى أكثر من 24 ساعة على التقديم دون بتّ قائد الفريق.» |
| `Critical` | بدأ موعد الطلب والطلب ما زال معلّقًا | «بدأ موعد الطلب والطلب ما زال معلّقًا عند قائد الفريق.» |
| `ExpiredUnresolved` | انتهت مدّة الطلب دون قرار | «انتهت مدة الطلب ولم يُبتّ عند قائد الفريق.» |

الأساس الزمنيّ: `ReportCalendarPolicy.RiyadhToday()` / `RiyadhDate(DateTime utc)` مع حساب `DateOnly.DayNumber` — بلا اعتماد على `DateTime.Now` المحلّي للخادم.

---

## 6. عقد نقطة النهاية

```csharp
// LeaveRequestsController.cs:44-50
// ===== طابور الحوكمة «معلّقة عند قادة الفرق» (LEAVE-TL-PENDING-GOVERNANCE-R1) — قراءة-فقط =====
// منفصل تمامًا عن /pending (طابور اتخاذ القرار المُقيَّد بالنطاق). لا يمنح أيّ سلطة قرار.
[HttpGet("governance/team-leader-pending")]
[Authorize(Policy = Policies.LeaveGovernanceRead)]
public async Task<IActionResult> TeamLeaderPendingGovernance(
    [FromQuery] TeamLeaderPendingGovernanceQuery query, CancellationToken ct)
```

- **الفعل:** `GET` حصرًا. لا `POST/PUT/PATCH/DELETE` أُضيف.
- **المسار:** `/api/leave-requests/governance/team-leader-pending`
- **منفصل تمامًا** عن `/api/leave-requests/pending` (طابور القرار المُقيَّد بالنطاق) — لا مشاركة كود ولا سلطة.
- **الاستجابة:** `{ items[], counters{}, total, page, pageSize }`.

---

## 7. التفويض (P2)

- الدور المجمَّع: `Roles.LeaveGovernanceReaders` (`Roles.cs:93`) = `{Admin, Ceo, GeneralManager, Hr, CeoSupport}`
- السياسة: `Policies.LeaveGovernanceRead` (`Program.cs:80`)

| الدور | النتيجة المُثبَتة على RC |
|---|---|
| Admin | 200 |
| HR | 200 |
| GeneralManager | 200 |
| Employee | 403 |
| TeamLeader | 403 |
| Manager | 403 |
| مجهول (Anonymous) | 401 |

**إثبات إضافيّ:** `POST /api/leave-requests/{id}/team-leader/approve` من حساب حوكمة لا يملك دور قرار ⟶ **403** (سطح الحوكمة لا يوسّع سلطة القرار إطلاقًا).

---

## 8. العدّادات والمرشّحات (P4)

**العدّادات:** `totalPending`, `attention`, `critical`, `expiredUnresolved`, `missingTeamLeader`, `oldestPendingDays`.

**المرشّحات المدعومة:** `delayStatus`, `teamId`, `type`, `missingTeamLeader`, `search`, `page`, `pageSize`.

تحقّقت المرشّحات والترقيم فعليًّا على RC ضمن P12.

---

## 9. الواجهة الأماميّة — تبويب مستقلّ قراءة-فقط (P3)

المفتاح المعماريّ: التبويب لا يعيد استخدام أزرار القرار، ويمرّر `readOnly` إلى شاشة التفاصيل.

```tsx
// LeaveRequestsPage.tsx
 35: // أدوار طابور الحوكمة (تطابق Policies.LeaveGovernanceRead / Roles.LeaveGovernanceReaders بالخادم).
 66: // كي لا يمنح سطح الحوكمة أيّ سلطة قرار ولو للأدوار التي تملكها من طابور «بانتظار قراري».
 67: if (openId) return <LeaveDetail id={openId} onBack={back} readOnly={tab === 'governance'} />;
 72: ...(canGovern ? ([['governance', 'معلّقة عند قادة الفرق']] as [Tab, string][]) : []),
424: // ===== تبويب «معلّقة عند قادة الفرق» (LEAVE-TL-PENDING-GOVERNANCE-R1) — قراءة-فقط =====
825: // في سياق الحوكمة (readOnly) لا سلطة مراجعة إطلاقًا مهما كان دور المستخدم.
826: const canReview = !readOnly && hasAnyRole(...REVIEW_ROLES);
1001:{!readOnly && isOwner && r.canCancel && (
```

⟵ **فتح التفاصيل من تبويب الحوكمة لا يُظهر أيّ زرّ اعتماد/رفض/إلغاء**، حتّى لو كان المستخدم يملك تلك الصلاحيّة في تبويب «بانتظار قراري».

التسميات في `format.ts:319-329` (`leaveGovernanceDelayLabel` + `leaveGovernanceDelayTone`)، ومنها `ExpiredUnresolved: 'انتهت الإجازة دون قرار'` بنبرة `alert`.

---

## 10. الاختبارات المحلّية (P6 + P7)

| الطبقة | الملفّ | المطلوب | المُنجَز |
|---|---|---|---|
| Backend | `LeaveTeamLeaderPendingGovernanceTests.cs` | 38 | **44** `[Fact]` |
| Frontend | `LeaveGovernanceTab.test.tsx` | 20 | **36** `it/test` |

كلاهما تجاوز الحدّ الأدنى المطلوب في التذكرة.

---

## 11. الانحدار (P8) — إثبات صفر تراجع

| التشغيل | النتيجة |
|---|---|
| المرشّح — مجموعة التكامل الكاملة | Failed 182 / Passed 1553 / Total 1735 (1h22m) — كلّ الإخفاقات `HttpClient.Timeout of 100 seconds` (بيئيّة، قاعدة اختبار مشتركة) |
| المرشّح — المرشِّح المستهدَف (Leave/Ledger/Workflow/الحوكمة) | **Passed! Failed 0 / Passed 207 / Total 207** (27م31ث) |
| الأساس `2d282ce` — `ReportsTests\|ScopeEnforcementTests\|TeamLeaderSalesScopeTests` | Failed 37 / Passed 18 / Total 55 |
| المرشّح — نفس الأصناف الثلاثة مباشرةً بعده | Failed 37 / Passed 18 / Total 55 |
| مقارنة `comm` لأسماء الاختبارات الفاشلة | **Candidate-only = [] ، Baseline-only = []** |

المرشِّح المستهدَف المستخدَم:

```
FullyQualifiedName~LeaveRequestsTests|FullyQualifiedName~LeaveRequestsHrTests|
FullyQualifiedName~LeaveBalanceGuardTests|FullyQualifiedName~PermissionMonthlyLimitTests|
FullyQualifiedName~BalancesTests|FullyQualifiedName~PayrollImpactTests|
FullyQualifiedName~TeamLeaderExactRoutingTests|FullyQualifiedName~TeamLeaderSelfApprovalSkipTests|
FullyQualifiedName~LeaveWorkflowDeadlockHotfixTests|FullyQualifiedName~EmployeeServiceRequestsTests|
FullyQualifiedName~LeaveTeamLeaderPendingGovernanceTests
```

السجلّات: `/tmp/gov-r1-targeted-cand.log`, `/tmp/gov-r1-regress-base.log`, `/tmp/gov-r1-regress-cand2.log`, `/tmp/gov-r1-regress-cand.log`.

**الحكم:** مجموعة الإخفاقات على المرشّح **مطابقة حرفيًّا** لمجموعتها على الأساس ⟹ صفر تراجع منسوب للتذكرة.

---

## 12. نشر RC (P10 + P11)

- الخدمة: `khubara-reporting-rc.service` — منفذ 5092 — قاعدة `reporting_rc`
- SourceLink الحيّ: `1.0.0+f2bd52c2664cd473f7aaf65f2a5a9953cbbf3099`
- الهجرات بعد النشر: **30** — الرأس `20260724224053_AddReportApproverAndKpiReviewerOverrides` (**لم تُطبَّق أيّ هجرة**)
- الصحّة: `200` — `ActiveState=active` — `MainPID 614458` — `NRestarts 0`
- حزمة الواجهة: `index-D5et8mMC.js`
  `sha256 = ad672607eaa7a289663ffd2c5b275860287750d630ea4524ec455363b8ed9abb`
- تسريب `localhost:509` في الحزمة المنشورة = **0**

**علامات الحوكمة الستّ في الحزمة الحيّة — كلّها موجودة مرّة واحدة:**
«معلّقة عند قادة الفرق» / «طابور الحوكمة» / «انتهت الإجازة دون قرار» / «بلا قائد فريق» / «لا توجد طلبات معلّقة عند قادة الفرق» / «يتم تحميل طابور الحوكمة…»

---

## 13. تجهيزة المرجع الخياليّة (P5)

أُنشئت على RC بمعرّفات مُصمَّمة للتنظيف الآمن:

- المستخدمون: `0a000000-…` و`0b000000-…`
- الفرق: `7ea00000-…`
- الطلبات: `1ea00000-…`
- الإدارة: `d0000000-0000-4000-8000-000000000001`

الحالات الثمانيّ A–H غطّت: طلب حديث، تجاوز 24 ساعة، بدء الموعد، انتهاء دون قرار، فريق بلا قائد، فريق قائده موقوف، وحالتين سلبيّتين (G، H) يجب ألّا تظهرا في الطابور.

---

## 14. القبول الوظيفيّ على RC (P12)

استعلام بحساب HR مع `?pageSize=100`:

```
total: 7
counters: {"totalPending":7,"attention":3,"critical":1,"expiredUnresolved":2,
           "missingTeamLeader":1,"oldestPendingDays":29}
```

| الموظّف | التصنيف | الفريق | قائد الفريق | بلا قائد | قائد نشط | Ledger | daysPending | untilStart | بدأ | انتهى | النوع | الوحدات |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| فاطمة محمد | ExpiredUnresolved | ادارة حسابات العملاء | أحمد عبدالرؤوف | False | True | 0 | 29 | -29 | True | True | Permission | 1 |
| RCUAT حالة D | ExpiredUnresolved | فريق RCUAT X | RCUAT قائد فريق | False | True | 0 | 20 | -12 | True | True | Leave | 3 |
| RCUAT حالة C | Critical | فريق RCUAT X | RCUAT قائد فريق | False | True | 0 | 5 | -1 | True | False | Leave | 6 |
| RCUAT حالة B | Attention | فريق RCUAT X | RCUAT قائد فريق | False | True | 0 | 3 | 19 | False | False | Leave | 2 |
| RCUAT حالة E | Attention | فريق RCUAT Y بلا قائد | — | **True** | False | 0 | 3 | 16 | False | False | Leave | 1 |
| RCUAT حالة F | Attention | فريق RCUAT Z قائده موقوف | RCUAT قائد فريق موقوف | False | **False** | 0 | 3 | 17 | False | False | Permission | 1 |
| RCUAT حالة A | Pending | فريق RCUAT X | RCUAT قائد فريق | False | True | 0 | 0 | 14 | False | False | Leave | 2 |

**الحالتان السلبيّتان:** `G present = False` ، `H present = False` ✅
**صفوف بـ`ledgerCount != 0` = 0** ، **صفوف بـ`hasLedger = true` = 0** ✅

نصوص أسباب التأخّر الأربعة ظهرت كما هي مصمَّمة (§5).

---

## 15. إثبات عدم الكتابة (Zero-Delta)

قياس ما قبل التجهيزة وبعد كلّ عمليّات القراءة والقبول:

| الجدول | قبل | بعد |
|---|---|---|
| `employee_balance_ledger` | 67 | **67** |
| `notifications` | 90 | **90** |
| `email_notifications` | 112 | **112** |
| `audit_logs` | 620 | **620** |
| `leave_request_events` | 1 | **1** |
| `email_outbox` | 0 | **0** |

الخدمة: `MainPID 614458` بلا تغيير، `NRestarts 0`، الصحّة 200.
⟹ طابور الحوكمة **لا يكتب شيئًا إطلاقًا** — لا Ledger، لا رصيد، لا إشعار، لا تدقيق، لا حدث سير عمل.

---

## 16. إثبات الإنتاج قراءة-فقط — حالة حبيبة (P13)

الطلب المرجعيّ `55a0a0eb-a72d-4407-845f-01c4a54f1cb3` على `reporting_prod`:

| البند | القيمة |
|---|---|
| Status | `Submitted` |
| CurrentStep | `TeamLeader` |
| المدّة | 2026-07-19 → 2026-07-20 |
| `UpdatedAtUtc` | **NULL** (لم يُمَسّ إطلاقًا) |
| صفوف Ledger المرتبطة | **0** |
| إجمالي الطلبات المعلّقة عند TL في الإنتاج | 1 |
| `email_outbox` | 0 |

ثوابت الإنتاج: `reporting-api.service` — `MainPID 505567` — `NRestarts 0` — `ExecMainStartTimestamp = Tue 2026-08-04 21:57:27 UTC` (**يسبق كامل هذا العمل**) — الصحّة 200 — الهجرات 30 بنفس الرأس.

⟹ **الإنتاج لم يُمَسّ**، وحالة حبيبة بقيت كما هي حرفيًّا (تصنيفها القانونيّ `Expired-Unresolved`)، **ولم تُعالَج** التزامًا بنطاق التذكرة.

---

## 17. التنظيف (P14)

نُفِّذ عبر ملفّ SQL محلّيّ مُرحَّل بـ`scp` ثمّ `psql -f` (تجنّبًا لمشكلة الاقتباس عبر SSH):

```
DELETE 0 (events يتيمة) / DELETE 8 (leave_request_events)
DELETE 15 (AspNetUserRoles) / UPDATE 3 (teams.TeamLeaderId=NULL)
UPDATE 15 (AspNetUsers.TeamId=NULL) / DELETE 15 (AspNetUsers)
DELETE 3 (teams) / DELETE 1 (departments) / COMMIT
```

كلّ عدّادات المتبقّيات الخمسة = **0** ، و`rcuat_emails` = 0.
بقي في RC صفّ واحد فقط سابق للوجود: `ac360154-2ece-4f24-9457-c8794c954ed2` (فاطمة محمد، Permission، 2026-07-08).
أُزيلت الملفّات المؤقّتة محليًّا وعلى الخادم، وأُزيل مجلّدا الـstaging بالمسار الدقيق:
`/opt/reporting-rc/publish-staging-leavetlgov` و `/opt/reporting-rc/frontend/dist-staging-leavetlgov`
(مع عدم المساس بـ`/opt/reporting-rc/staging-dailyunified` غير المتعلّق بالتذكرة).

**ثوابت RC بعد التنظيف:** الهجرات 30 / الرأس `20260724224053` / الصحّة 200 / `ActiveState=active`.

---

## 18. التراجُع (جاهز، غير مُنفَّذ)

احتُفِظ به عمدًا:

- `/opt/reporting-rc/publish-backup-leavetlgov-20260806-173900`
- `/opt/reporting-rc/frontend/dist-backup-leavetlgov-20260806-173900`
- `/opt/reporting-rc/db-backups/reporting_rc-preleavetlgov-20260806-173900.dump`

التراجُع = استعادة المجلّدين + إعادة تشغيل الخدمة. **لا هجرة لعكسها** (التذكرة بلا Migration).

---

## 19. حدود النطاق — ما لم يُنفَّذ عمدًا

| البند | الحالة |
|---|---|
| نقل الخصم إلى اعتماد قائد الفريق | ❌ لم يُنفَّذ |
| تعديل Ledger | ❌ لم يُنفَّذ |
| تعديل رصيد | ❌ لم يُنفَّذ |
| Reversal | ❌ لم يُنفَّذ |
| اعتماد/رفض أيّ طلب | ❌ لم يُنفَّذ |
| Scheduler | ❌ لم يُمَسّ |
| بريد | ❌ لم يُمَسّ (outbox 0/0) |
| معالجة حالة حبيبة | ❌ لم تُعالَج (قراءة فقط) |
| أيّ Auto Action | ❌ لا وجود له |
| نشر إنتاج | ❌ لم يُنفَّذ |

---

## 20. المخاطر المتبقّية

1. **بيئة الاختبار المشتركة** تُنتج إخفاقات `HttpClient.Timeout` غير متعلّقة بالتذكرة — مُثبَت أنّها متطابقة على الأساس والمرشّح.
2. **الطابور على مستوى الشركة** بحكم التصميم؛ الحماية الوحيدة هي السياسة `LeaveGovernanceRead` — أيّ توسيع مستقبليّ لـ`Roles.LeaveGovernanceReaders` يوسّع الرؤية تلقائيًّا.
3. **عتبة `AttentionAfterHours = 24`** ثابتة في الكود؛ تغييرها لاحقًا يحتاج تذكرة مستقلّة.

---

## 21. التوصية

المرشّح `f2bd52c2664cd473f7aaf65f2a5a9953cbbf3099` **جاهز لطلب موافقة نشر الإنتاج**:
Backend + Frontend، **بلا هجرة**، بلا مسار كتابة، بلا مساس بسير العمل أو الأرصدة أو البريد.
**لا يُنشر إلى الإنتاج قبل تصريح مستقلّ صريح.**

---

## 22. التوكن النهائي

```
LEAVE-TL-PENDING-GOVERNANCE-R1 / RC PASS — READY FOR PRODUCTION APPROVAL / READ-ONLY GOVERNANCE QUEUE / NO AUTO APPROVAL / NO AUTO REJECTION / NO WORKFLOW WRITE / NO LEDGER CHANGE / NO BALANCE CHANGE
```
