# LEAVE-WORKFLOW-DEADLOCK-HOTFIX — تقرير قبول RC

**القرار النهائي: `RC PASS — READY FOR PRODUCTION DECISION`**
**التاريخ:** 2026-08-04 · **البيئة:** Release Candidate حصرًا · **الإنتاج: لم يُمَسّ.**

---

## 1. الملخّص التنفيذيّ

طلبات الإجازة كانت تتوقّف نهائيًّا عند خطوة المدير حين يكون **المدير المباشر للموظّف هو نفسه قائد فريقه**
(حالتا الإنتاج: محمد إبراهيم/بسنت، ونور الدين/شيماء). بعد اعتماد خطوة قائد الفريق يمنع حارس
«لا يمكنك اتخاذ قرار على خطوة أخرى لنفس الطلب» نفسَ الشخص من اعتماد خطوة المدير ⇒ الطلب لا يصل إلى
الموارد البشرية ⇒ **لا خصم رصيد ولا حركة Ledger**.

الحلّ المعتمَد **P2 — طيّ خطوة المدير تشغيليًّا بضوابط**: تُطوى خطوة المدير تلقائيًّا **فقط** إذا كان
المُعتمِد هو المدير المباشر لمقدّم الطلب **ولا يوجد مدير تشغيليّ بديل** داخل شجرته الإدارية.
أدوار الحوكمة العامّة (Admin/CEO/GeneralManager/CeoSupport) **لا تُعدّ بديلًا تشغيليًّا**.

**نتيجة القبول: كلّ بنود بوّابة الانحدار وكلّ سيناريوهات UAT الحيّة على RC نجحت، بصفر انحدار وصفر هجرة.**

---

## 2. المرشّح المُجمَّد

| البند | القيمة |
|---|---|
| Commit | `2d282cebf0a22f65b78cd751de17d6c927128d0d` |
| Parent (خط أساس الإنتاج) | `f3ee32f24323d61258ef15844f66c66adaf279df` |
| Tree | `2074db3d1993511671c4b559d5b20786997b9d81` |
| `git patch-id --stable` | `f5dea3c5247a9d6fd80015f0dce65e759117aced` |
| الفرع | `candidate/leave-workflow-deadlock-hotfix-r1-20260803` |
| حجم الباتش | ملفّان، +625 / −3 |
| **الهجرات** | **0 تغيير** — 30 هجرة، الرأس `20260724224053` |
| **الواجهة (Frontend)** | **0 تغيير** |
| البناء | `Release — Build succeeded. 0 Warning(s)` |

**الملفّان:**
- `reporting-backend/src/Reporting.Infrastructure/Services/LeaveRequestService.cs` (+74/−3) — ملفّ الإنتاج الوحيد.
- `reporting-backend/tests/Reporting.IntegrationTests/LeaveWorkflowDeadlockHotfixTests.cs` (جديد، 21 اختبارًا).

**جوهر التغيير:**
1. بعد اعتماد خطوة قائد الفريق، إن كان `Requester.ManagerId == uid` **و** `HasOperationalManagerAlternativeAsync == false`
   ⇒ `Status=ManagerApproved` / `CurrentStep=Hr` + حدث `manager_step_auto_folded_no_operational_manager`.
2. `HasOperationalManagerAlternativeAsync` يصعد سلسلة `ManagerId` بحثًا عن مستخدم **نشط + يحمل دور `Manager` + مختلف عن المُعتمِد**؛
   يمنع الحلقات، ويتعامل بأمان مع `ManagerId` المفقود. أدوار الحوكمة لا يلتقطها البحث ⇒ ليست بديلًا.
3. مصدر `switch` الإشعارات غُيِّر من `toStatus` إلى `entity.Status` ليصل إشعار الخطوة المطويّة إلى HR بشكل صحيح.

---

## 3. بوّابة الانحدار (Regression Gate)

| المرحلة | المطلوب | النتيجة |
|---|---|---|
| **Phase 5** — `LeaveWorkflowDeadlockHotfixTests` | 21/21 PASS | ✅ `Total: 21 / Passed: 21 / Test Run Successful` (5.13 دقيقة) |
| **الخطوة 2** — `TeamLeaderExactRoutingTests` + `LeaveRequestsHrTests` + `LeaveRequestsTests` | اختفاء السبعة Candidate-only بالكامل | ✅ `Failed: 0, Passed: 41, Total: 41` (3د17ث) |
| **الخطوة 3** — مجموعة الانحدار (237 اختبارًا / 43 صنفًا) | 13 Failed مطابقة حرفيًّا لـBaseline، صفر Candidate-only | ✅ **Candidate-only = []** و**Baseline-only = []** |

**تفصيل الخطوة 3:** شُغِّل التجميع كاملًا (1691 اختبارًا، 174 فشلًا، 1س09د) ثمّ حُصر التقاطع مع
عضويّة مجموعة الانحدار الأصليّة (237 اختبارًا) المستخرَجة من `cand-regression.trx` السابق:

```
--- FAILURES within regression suite (candidate, new run): 13
--- BASELINE failures: 13
Candidate-only (in suite, not in baseline): []
Baseline-only (missing from candidate): []
```

الـ13 فشلًا هي حرفيًّا فشول خطّ الأساس نفسها (`ReportsTests` ×7 Rollup، `ScopeEnforcementTests` ×4،
`TeamLeaderSalesScopeTests` ×2) — كلّها **بيئيّة سابقة الوجود** ولا علاقة لها بمسار الإجازات.

**إضافةً**: في التشغيل الكامل (1691) **صفر فشل** في أيّ صنف
`Leave*` / `TeamLeader*Routing` / `Balances` / `Payroll` / `Permission*` / `LeaveWorkflowDeadlockHotfix`.

**لم تُعدَّل أيّ توقّعات اختبار لإخفاء انحدار** (التزامًا بالبند §10).

---

## 4. نشر RC

| البند | القيمة |
|---|---|
| TS | `20260804-004328` |
| الخدمة | `khubara-reporting-rc.service` — 127.0.0.1:5092 |
| النسخة الاحتياطيّة | `/opt/reporting-rc/publish-backup-leavedeadlock-20260804-004328` (107M) |
| النشر | `rsync -az --delete --exclude appsettings.Development.json` ⇒ `RSYNC_OK` + `chown www-data` |
| `Reporting.Infrastructure.dll` SHA256 | `f5df4416c651be0e4990fa5c8a99f934d04cdd8633f8e31cb4ff2ca5df53ff83` |
| `Reporting.Api.dll` SHA256 | `1b876e2f01bbcb15d770f2858fa99a349b1a233c9100f4cacc6e5300aaf1cfad` |
| بصمة P2 داخل DLL | `manager_step_auto_folded_no_operational_manager` (UTF-16LE) = **1** |
| `appsettings.Development.json` | غائب ✅ |
| إعادة التشغيل | **واحدة حصرًا** — MainPID `473520`، بدء `2026-08-04 00:46:37 UTC` |

**بعد إعادة التشغيل:**
- `ActiveState=active` / `SubState=running` / **`NRestarts=0`**
- `rc_health=200`
- سجلّ الإقلاع: **`No migrations were applied. The database is already up to date.`**
- الهجرات: **30**، الرأس `20260724224053_AddReportApproverAndKpiReviewerOverrides` (بلا تغيير)
- `/var/log/reporting-rc/rc-api.err.log` — **صفر سطر جديد**

---

## 5. قبول UAT الحيّ على RC

بيئة مؤقّتة ببادئة `p2uat-`: إدارة واحدة + فريقان + 7 مستخدمين (hash هويّة Identity v3 مولَّد محليًّا)،
أُنشئت بمعاملة واحدة تحت دور المالك، ونُظِّفت بالكامل بعد الاختبار.

### السيناريو A — لا مدير تشغيليّ بديل ⇒ **الطيّ متوقَّع**

بنية: `EA` (موظّف) → مديره `MA`؛ و`MA` هو **قائد فريق** `team-ta` نفسه؛ و`MA.ManagerId = GA` (**GeneralManager فقط، ليس Manager**).

| # | الخطوة | النتيجة |
|---|---|---|
| A1 | `EA` ينشئ طلب إجازة (3 أيام) | 200 — `Submitted / TeamLeader` |
| A2 | `MA` يعتمد خطوة قائد الفريق | **200 — `ManagerApproved / Hr`**، مراجع المدير = `MA UAT` ✅ **طيّ** |
| A3 | `HR` يعتمد نهائيًّا | 200 — `HrApproved / Completed` |

**سجلّ الأحداث (EA):**
```
submitted                                       Draft              → Submitted           (EA)
team_leader_approved                            Submitted          → TeamLeaderApproved  (MA)
manager_step_auto_folded_no_operational_manager TeamLeaderApproved → ManagerApproved     (MA)
hr_approved                                     ManagerApproved    → HrApproved          (HR)
```
**Ledger:** `AnnualLeave / Debit / 3.00 / ApprovedLeave` ⇒ **الخصم تمّ فعليًّا** (كان مستحيلًا قبل الإصلاح).
⇒ **حالة الجمود الأصليّة زالت من طرفها إلى طرفها.**

### السيناريو B — يوجد مدير تشغيليّ حقيقيّ ⇒ **لا طيّ**

بنية: `EB` → مديره `MB`؛ و`MB` قائد فريق `team-tb`؛ و`MB.ManagerId = XB` (**يحمل دور `Manager` ونشط**).

| # | الخطوة | النتيجة |
|---|---|---|
| B1 | `EB` ينشئ الطلب | 200 — `Submitted / TeamLeader` |
| B2 | `MB` يعتمد خطوة قائد الفريق | **200 — `TeamLeaderApproved / Manager`** ✅ **لا طيّ** (المسار الطبيعيّ محفوظ، ضمانة T-WF2 سليمة) |
| B3 | `MB` يحاول اعتماد خطوة المدير | **403 `auth.forbidden`** — «لا يمكنك اتخاذ قرار على خطوة أخرى لنفس الطلب.» ✅ الحارس الأصليّ لم يُضعَف |
| B4 | `XB` (المدير التشغيليّ) يعتمد | 200 — `ManagerApproved / Hr` |
| B5 | `HR` يعتمد نهائيًّا | 200 — `HrApproved / Completed` |

**سجلّ الأحداث (EB):** `submitted → team_leader_approved (MB) → manager_approved (XB) → hr_approved (HR)`
— **بلا أيّ حدث طيّ.**

### الخلاصة
الطيّ **مشروط ودقيق**: يقع حين لا بديل تشغيليّ، ولا يقع حين يوجد بديل. حارس «نفس الفاعل»
باقٍ كما هو، ومسار T-WF2 (خطوة قائد الفريق لقائد الفريق الفعليّ حصرًا) لم يُمَسّ.

---

## 6. التنظيف والحالة النهائيّة

**تنظيف UAT (بترتيب المفاتيح الأجنبيّة، معاملة واحدة):**
`leave_request_events` → `employee_balance_ledger` → `leave_requests` → `notifications` →
`refresh_tokens` → `audit_logs` → `AspNetUserRoles` → تصفير `TeamLeaderId`/`TeamId`/`ManagerId`/`DepartmentId`
→ `teams` → `departments` → `AspNetUsers`.

**بقايا = صفر:** `users=0 teams=0 depts=0 leaves=0 events=0`. كلّ السكربتات المؤقّتة أُزيلت من الخادم.

| | RC | Production |
|---|---|---|
| الحالة | active / running | active |
| MainPID | 473520 | **353548 (بلا تغيير)** |
| NRestarts | 0 | 0 |
| health | 200 | **200** |
| الهجرات | 30 / `20260724224053` | لم تُمَسّ |

**الإنتاج لم يُنشَر عليه ولم يُعَد تشغيله ولم تُعالَج أيّ طلبات عالقة ولم يُخصَم أيّ رصيد.**

---

## 7. الالتزام بالمحظورات (§1 و§10)

| المحظور | الحالة |
|---|---|
| لا هجرة | ✅ 0 تغيير هجرة، الرأس ثابت |
| لا تعديل Frontend | ✅ 0 ملف واجهة |
| لا مساس بالإنتاج | ✅ MainPID/health/هجرات بلا تغيير |
| لا تعديل توقّعات اختبار لإخفاء انحدار | ✅ لم تُعدَّل أيّ توقّعات |
| لا معالجة طلبات عالقة قديمة | ✅ لم يُمَسّ أيّ طلب إنتاجيّ |
| لا خصم أرصدة | ✅ الخصم الوحيد على مستخدمَي UAT المؤقّتَين على RC، وقد حُذف مع البيانات |
| لا بدء تذكرة أخرى | ✅ |

---

## 8. التراجُع (جاهز، غير مُنفَّذ)

```bash
rsync -az --delete /opt/reporting-rc/publish-backup-leavedeadlock-20260804-004328/ /opt/reporting-rc/publish/
chown -R www-data:www-data /opt/reporting-rc/publish
systemctl restart khubara-reporting-rc.service
```
**لا هجرة لعكسها** (الإصلاح code-only).

---

## 9. التوصية

**`RC PASS`** — المرشّح `2d282ce` جاهز تقنيًّا لقرار نشر إنتاجيّ مستقلّ.
النشر الإنتاجيّ **Backend فقط، بلا هجرة، بلا واجهة، بإعادة تشغيل واحدة**، ويتطلّب **تصريحًا صريحًا جديدًا**.

**بند مؤجَّل بعد النشر الإنتاجيّ (قرار إداريّ منفصل):** الطلبان العالقان (محمد إبراهيم، نور الدين)
لن يُطويا رجعيًّا — الإصلاح يعمل عند اتخاذ القرار لا بأثر رجعيّ. تحتاج معالجتهما خطّةً مستقلّة
(إعادة تقديم أو معالجة مُصرَّح بها)، **لا تُنفَّذ ضمن هذه التذكرة**.

---

## ملحق أ — تحقيق في تشغيل كامل للمجموعة (1691 اختبارًا) — النتيجة: لا انحدار

بعد إغلاق البوّابة ظهرت نتيجة تشغيل كامل للمجموعة على المرشّح: **174 فشلًا / 1517 نجاحًا / 1691**
(163 اسمًا فريدًا، المدّة 1س09د). خضعت للتحقيق قبل أيّ استنتاج، والحكم **لا انحدار**:

**1) لا علاقة بنطاق التذكرة.** صفر فشل يطابق
`leave|deadlock|balance|payroll|permission|TeamLeaderExact|TeamLeaderSelf`.
الملفّ الإنتاجيّ الوحيد المتغيّر `LeaveRequestService.cs` لا يمكنه بنيويًّا أرشفة قالب تقرير
ولا تغيير تجميعات المبيعات/التقارير.

**2) الـ13 المعروفة حاضرة كلّها** ⇒ المجموعة الجديدة superset لخطّ الأساس لا مجموعة مغايرة.

**3) بصمة الأخطاء بيئيّة**: `BadRequest→NotFound` ×21، `OK→NotFound` ×18، `Submitted→404` ×9،
`Forbidden→NotFound` ×7، و**`Published→Archived` ×6** (الدليل الحاسم: قوالب مشتركة أُرشِفت)،
إضافةً إلى 58 حالة مهلة/إلغاء/HTTP.

**4) الإثبات القاطع — مقارنة مضبوطة على عيّنة.** أُعيد تشغيل أكبر ثلاثة تجمّعات فاشلة
(`ErdsPhase55WorkUnitTests`، `ProjectRepeatableGridTests`، `B2bByServiceTemplateTests`)
**تسلسليًّا ومنفردة** على الشجرتين:

| الشجرة | النتيجة |
|---|---|
| المرشّح `2d282ce` | Failed **40** / Passed 5 / Total 45 |
| الأساس `f3ee32f2` | Failed **40** / Passed 5 / Total 45 |

وبمقارنة **أسماء** الفشل: **Candidate-only = [] و Baseline-only = []** (40 اسمًا متطابقًا حرفيًّا).

**السبب الجذريّ**: قاعدة الاختبارات المشتركة الدائمة `reporting_test` متضخّمة وملوّثة
(**97,144** صفًّا في `AspNetUsers` و**19,061** قالبًا، منها 106 مؤرشفة). تصنيف القوالب القانونيّة
سليم (`Supplementary`=5) فليس هو العلّة. التشغيل المتوازي لثلاث مجموعات ثقيلة على القاعدة نفسها
ضاعف التلف وأبقاه مكتوبًا فيها.

**الأثر على القرار: لا شيء.** بوّابة الانحدار المعتمَدة كانت **مقارنة مضبوطة** (أساس مقابل مرشّح
على المجموعة نفسها) وأعطت `Candidate-only=[]`، والعيّنة أعلاه تؤكّد النتيجة ذاتها.
**`RC PASS` يبقى قائمًا.**

**تذكرة مستقلّة موصى بها (لم تُبدأ): `TEST-DB-HYGIENE-R1`** — إعادة تهيئة/تنظيف
`reporting_test`، ومنع التشغيل المتوازي لمجموعات التكامل على قاعدة واحدة مشتركة.
