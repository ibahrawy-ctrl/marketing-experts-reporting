# REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — تقرير نشر RC واختبار القبول (UAT)

> التاريخ: 2026-07-20 · البيئة: **Release Candidate فقط** (لا إنتاج) · النطاق: Backend + Frontend، **بلا Migration/Seeder** · الحكم النهائي في نهاية التقرير.

---

## 0) الحكم النهائي
**RC DEPLOYED AND UAT PASSED — READY FOR PRODUCTION APPROVAL.**

النشر على RC تمّ بنجاح، وجميع سيناريوهات UAT الحيّة مرّت على الـbinary المنشور فعليًّا. **لا ينتقل للإنتاج إلا بعد مراجعتكم لهذا التقرير وموافقتكم الصريحة.** لا Moderation في هذه الجولة.

---

## 1) المصدر المنشور (Provenance)
- **Commit:** `3eee204` (parent `bd84115` = لقطة حالة الإنتاج الحاليّة: Restore/Archive Governance R1 + Fatma Direct + Admin Governance + BypassTeamLeader + KpiPartialIndex).
- سبب إعادة التوطين (من الجلسات السابقة): التذكرة كانت مُلتزَمة `b783117` فوق أساس متقادم (`role-aware-calendar-r1-final`) ينقصه Fatma-Direct + KpiPartialIndex الحيّان على الإنتاج ⇒ نشره كان سيُسقِط ميزتين. العلاج النظيف = cherry-pick دلتا التذكرة (18 ملفًا) فوق `bd84115` = **`3eee204`**، مع حسم تعارض `CustomWebApplicationFactory.cs` لصالح التذكرة (تجاوز `TEST_DB_CONNECTION`).
- **سلامة الهجرات:** التذكرة تضيف **0 هجرة**. رأس هجرات `3eee204` = `20260716015239_KpiEvaluationPartialUniqueIndex` (نفس رأس RC الحيّ).

## 2) الاختبارات على الأساس الجديد (قبل النشر)
- Backend build: 0 errors. Unit: **156/156**. Integration (Unified + Calendar المستهدَفة): **27/28** على DB معزولة، الفشل الوحيد `SalesDailyCompliance_AggregatesDays_FlagsIncompleteWeek` سابق-الوجود (تواريخ يونيو ثابتة مقابل today، يمسّ `ReportCalendarService` لا مسار الحالة الموحّدة). Frontend: vitest **195/195**، build نظيف (تحذير signalr الحميد فقط).

---

## 3) PHASE G — نشر RC (Backend + Frontend)

### 3.1 النسخ الاحتياطية قبل الاستبدال (شرط إلزامي — تمّ)
- TS = `20260720-054439`.
- Backend backup: `/opt/reporting-rc/publish-backup-<TS>` (107M).
- Frontend backup: `/opt/reporting-rc/frontend/dist-backup-<TS>` (1.5M).

### 3.2 إثبات Hash/Bundle قبل وبعد (شرط إلزامي — تمّ)
**Backend DLL hashes — بعد النشر (تطابق artifact `3eee204` بايت-ببايت):**

| DLL | SHA-256 (بعد النشر على RC) |
|---|---|
| Reporting.Api | `abe565f212fee117e73ebbc8c53525203bdd21ff1c2d794ae31719637a52ffcb` |
| Reporting.Application | `eff4d5d84b0a758614488133b0e2f90aed15dd24d866f77568d446a573db7853` |
| Reporting.Domain | `006d4509ca7b457fbaae42a49c4ada1ec468b3b0ad369b9c5026e04903a40379` |
| Reporting.Infrastructure | `1fdf5d4c48721b20e16803185c081134ab86c36c3fe6846d14e91be799dd48ae` |

- Backend hashes تغيّرت عن السابق (`6c6b1240…`→`abe565f2…`) وتطابق نسخة `publish-rc` المحليّة تمامًا.
- **Frontend bundle:** قبل `index-Bpd--Clz.js` → بعد **`index-6J6SJ-kI.js`**؛ 0 تسريب localhost، markers الحالة الموحّدة حاضرة، `appsettings.Development.json` غائب عن publish.

### 3.3 سلامة الإقلاع والهجرات (شرط: بلا Migration — تمّ)
- سجلّ الإقلاع: **«No migrations were applied. The database is already up to date.»** (Pending=0، لا SIGABRT).
- رأس هجرات RC = `20260716015239_KpiEvaluationPartialUniqueIndex` (لم يتغيّر).
- الخدمة `khubara-reporting-rc.service` = **active**؛ health داخلي (`127.0.0.1:5092/health`) = **200**.

---

## 4) PHASE H — Smoke Test + UAT الحيّ على الـbinary المنشور

### 4.1 المصادقة
- `reporting_rc` نسخة من قاعدة الإنتاج (35 مستخدمًا حقيقيًّا، بلا كلمة seed افتراضية). الدخول تمّ عبر break-glass الإنتاجي (`Seed__Admin*` من `/etc/reporting-api.env`) ⇒ HTTP 200. الافتراضي `Admin#12345` = 401 (متوقّع).

### 4.2 بيانات UAT المؤقتة المصرَّح بها (أُنشئت ثم نُظِّفت بالكامل)
- موظّف مؤقت واحد: `uat-unified-1784515934@test.local` (id `696fa9bb-…`)، مربوط بقالب أسبوعي حقيقي (JobRole `3ddd7c4b`).
- 5 تسليمات مزروعة عبر psql عبر أسابيع ماضية لتغطية كل الحالات.

### 4.3 نتائج الاشتقاق الحيّة (`GET /api/reporting-calendar/my-cycles`) — كلها مطابقة للمتوقّع

| الدورة | الحالة الموحّدة | الشدّة | isLate | الملاحظة |
|---|---|---|---|---|
| **W28 (Closed)** | **Closed** («مُغلَق») | success | true | **السيناريو الأساسي H1: مُغلَق سُلّم متأخّرًا ⇒ يظهر «مُغلَق» ولا يظهر «تجاوز الموعد»** ✓ |
| W27 (مفقود بعد الموعد) | OverdueNotSubmitted («متأخّر — لم يُسلَّم») | alert | true | ناقص بعد الموعد ✓ |
| W26 (مسودّة بعد الموعد) | OverdueDraft | warn | false | ✓ |
| W25 (معاد بعد الموعد) | OverdueReturned | alert | false | ✓ |
| W24 (مُسلَّم متأخّرًا) | SubmittedLate | warn | true | ✓ |
| W23 (بانتظار الاعتماد) | PendingApproval | info | false | ✓ |
| W30 | DueNow | info | — | ✓ |
| W31 | NotDue | none | — | ✓ |
| W22 | OverdueNotSubmitted، **isCurrentPriority=true** | alert | true | دورة واحدة فقط ذات أولوية إجراء ✓ |

### 4.4 سلوك اللافتة (البانر) — تأكيد المخاوف الأساسية
- `selectBannerCycleUnified` يختار دورة `isCurrentPriority` (وهي دورة إجراء قابلة كـOverdueNotSubmitted، رتبة ActionRequired ≤6).
- **الدورة المُغلَقة W28 لا تصبح أولويّة أبدًا** (رتبة Closed = 10، ليست ≤6) ⇒ لا تُلغي اللافتة الفعّالة ولا تُظهِر «تم اعتماد تقريرك بالكامل» زورًا بينما توجد دورات متأخّرة تحتاج إجراءً. ✓
- الحقول القديمة (isOverdue/isCurrent/…) **تتعايش** مع الحقل الموحّد `unified` على كل دورة (توافق خلفي مُثبَت). ✓

### 4.5 البوّابات (شرط: بلا Email/Schedulers — تمّ)
`Email__Enabled=false` · `Reminders__Enabled=false` · `Scheduler__Enabled=false` (مؤكَّدة في env بعد UAT).

---

## 5) تنظيف بيانات UAT (شرط إلزامي — تمّ بالكامل)
- حُذفت 5 تسليمات + قيَمها (`submission_field_values`)، ثم refresh_tokens + AspNetUserRoles + audit_logs المرتبطة، ثم المستخدم المؤقت.
- **التحقّق بعد التنظيف (كلها = 0):** submissions=0، user=0، userroles=0، refresh_tokens=0، uat_test_users_remaining=0.
- **إجمالي المستخدمين عاد إلى 35** (خط أساس الإنتاج، بلا زيادة).
- رأس الهجرات لم يتغيّر: `20260716015239`.
- السكربتات المؤقتة أُزيلت من الخادم والمحلّي (`/tmp/rc-uat.sh`).

---

## 6) عدم المساس (No-Impact)
لم يُمَسّ أيّ من: الهجرات (0 مطبَّقة)، Seeder، بيانات إنتاجية (RC فقط)، Email/Reminders/Scheduler (كلها false)، Moderation، Production. النشر من artifact `3eee204` حصرًا مع تطابق hash مثبت.

## 7) Rollback (إن لزم)
استعادة `/opt/reporting-rc/publish-backup-20260720-054439` + `dist-backup-20260720-054439` وإعادة تشغيل `khubara-reporting-rc.service` (لا هجرة لعكسها).

---

## 8) الحكم النهائي وما بعده
**RC DEPLOYED AND UAT PASSED — READY FOR PRODUCTION APPROVAL.**

الخطوة التالية تتطلّب **موافقتكم الصريحة** بعد مراجعة هذا التقرير: نشر الإنتاج (من نفس artifact `3eee204`، بلا migration، مع نفس بروتوكول Backup/Hash/Smoke). **توقّفت هنا — لا إنتاج، لا Moderation، قبل موافقتكم.**
