# ROLE-AWARE CALENDAR R1 — PRODUCTION DEPLOYMENT & FINAL ACCEPTANCE REPORT

> **الحزمة:** التقويم المُدرِك للدور (Role-Aware Reporting Calendar R1) — أسبوعي + KPI أسبوعي + يوميّ (مبيعات).
> **النطاق:** نشر الحزمة المعتمَدة على RC إلى **الإنتاج فقط** — بلا إعادة بناء من `develop`، بلا تعديل مصدر، بلا merge/push، بلا هجرة جديدة.
> **البيئة:** `reporting-api.service` @ `127.0.0.1:5090` (ASPNETCORE_ENVIRONMENT=Production)، قاعدة `reporting_prod`، مجال `reports.emarketingacademy.net`، VPS `srv1747233` (187.127.72.232).
> **تاريخ النشر:** 2026-07-15، نافذة `20260715-100325`.
> **الدورة المرجعية للاختبار:** 2026-W29 (السبت 11 يوليو → الجمعة 17 يوليو)، اليوم = الأربعاء 2026-07-15.

---

## 1. الحكم النهائي

**✅ PRODUCTION DEPLOYMENT SUCCESSFUL**

الحزمة المعتمَدة على RC (المطابقة بالبصمة بايتًا-ببايت) نُشرت على الإنتاج بنجاح؛ اجتازت 30/30 اختبار دخان بصفر أثر على البيانات؛ الأساس مُستعاد تمامًا؛ لا drift في البصمات/الهجرات؛ الخدمة مستقرّة وصحّية. **لم يُستدعَ Rollback.**

---

## 2. هوية المرشّح وبوابة البصمات (Phase 1)

| المكوّن | البصمة المعتمَدة (SHA-256، مختصرة) | المنشور على الإنتاج | الحالة |
|---|---|---|---|
| `Reporting.Api.dll` | `a6e8ea985dc7386f…2596e7` | `a6e8ea985dc7386f…` | ✅ مطابق |
| `Reporting.Application.dll` | `7b0a6900dae62c01…2861da` | `7b0a6900dae62c01…` | ✅ مطابق |
| `Reporting.Infrastructure.dll` | `aa460ece650054be…88596d` | `aa460ece650054be…` | ✅ مطابق |
| Frontend bundle `index-B_lNG4Zb.js` | `2b5bfb00e01538a2…dff85f` | `2b5bfb00e01538a2…` (مُقدَّم عبر HTTPS) | ✅ مطابق |
| عدد الهجرات / الرأس | 27 / `20260713171040_AdminGovernanceReportKpiCorrection` | 27 / نفسه | ✅ ثابت |

المصدر: `/opt/reporting-rc/publish` + `/opt/reporting-rc/frontend/dist` (المرشّح المُتحقَّق على RC). **لا CANDIDATE HASH MISMATCH.**

---

## 3. النسخ الاحتياطية وخطة الرجوع (Phase 2)

- **DB dump:** `/root/db-backups/reporting_prod-pre-role-calendar-r1-20260715-100325.dump` (SHA `2c14aa4d…12096de017d6fbd2f91412d7`، `pg_restore --list` سليم).
- **Backend:** `/opt/reporting/publish-backup-role-calendar-r1-20260715-100325` (107M).
- **Frontend:** `/opt/reporting/reporting-frontend/dist-backup-role-calendar-r1-20260715-100325`.
- **حزمة الرجوع:** `/root/role-calendar-r1-rollback-20260715-100325/` (ROLLBACK-MANIFEST + backend-prev-hashes + baseline-counts + env-key-names-only[20 مفتاحًا، أسماء فقط] + frontend-prev-bundle/index.html + migration-count/history + nginx conf + env snapshot + service unit).
- **بصمات الإنتاج قبل النشر (للرجوع):** Api `4d4a50bc…`، App `d6586418…`، Infra `0393640d…`، bundle سابق `index-CxkQwgGI.js` SHA `35c98824…`.

---

## 4. نشر الـ Backend (Phase 3)

- `rsync -az --delete` من `/opt/reporting-rc/publish` → `/opt/reporting/publish` + `chown www-data` + restart.
- سجلّ الإقلاع: **«No migrations were applied. The database is already up to date.»** + `Hosting environment: Production` + `Now listening on: http://127.0.0.1:5090`.
- بصمات DLL المنشورة = المرشّح تمامًا؛ `appsettings.Development.json` غائب؛ عدد الهجرات بعد الإقلاع = 27.
- **لا هجرة إنتاج غير متوقّعة، لا 42501، لا استثناء seeder، لا نشاط Email/Reminder/Scheduler في الإقلاع.**

---

## 5. نشر الـ Frontend (Phase 4)

- `rsync -a --delete` من `/opt/reporting-rc/frontend/dist` → `/opt/reporting/reporting-frontend/dist` + `chown www-data`.
- `index.html` يشير إلى `index-B_lNG4Zb.js`؛ الـbundle المُقدَّم عبر HTTPS = HTTP 200، SHA `2b5bfb00…` (مطابق المعتمد).
- **فحص التسريب (صفر):** لا `localhost:PORT`، لا مضيف RC/TEST (`5091/5092/rc-report/khubara-reporting`)، لا مضيف إنتاج مُثبَّت. API base نسبيّ `/api`.
- **Markers التقويم موجودة:** `my-cycles` / `my-days` / `reporting-calendar`.
- الـbundle القديم `index-CxkQwgGI.js` أُزيل نظيفًا؛ النسخة الاحتياطية محفوظة.

---

## 6. اختبارات الدخان على الإنتاج — **30/30 PASS** (Phase 5)

المصادقة: admin المبذور من env (`Seed__AdminEmail/Password`) — لم يُطبع أيّ توكن/سرّ. **كل الاختبارات قراءة أو رفض 400 — صفر تخزين.**

### A — الأسبوعي (Report) 10/10
- A1-A3: `my-cycles?context=report`=200، `currentCycleKey=2026-W29`، الدورة الحالية `cycleKey=2026-W29`.
- A4: الحالية السبت→الجمعة `2026-07-11 → 2026-07-17`. ✅
- A5: W28 السبت→الجمعة `2026-07-04 → 2026-07-10`. ✅
- A6: `isCurrent=true` للدورة الحالية (اختيار تلقائيّ).
- A7: تاريخ استحقاق الدور محسوب خادميًّا = `2026-07-20` («الاثنين 20 يوليو»، off9 لطبقة الإدارة).
- A8: prev/current/next موجودة. A9: التالية مستقبلية/مقفلة (`isFuture=true, isLocked=true, isOpen=false`).
- A10: كل مفاتيح الدورات بصيغة خادميّة `YYYY-Www` (لا إدخال يدويّ).

### B — KPI الأسبوعي 7/7
- B1-B2: `my-cycles?context=kpi`=200، `currentCycleKey=2026-W29`، الحالية تبدأ السبت `2026-07-11`.
- **حُرّاس الرفض الخادميّة (400 بلا تخزين):**
  - B3: مفتاح بصيغة خاطئة `2026-XX` → 400 `kpi_eval.period_format_invalid`.
  - B4: دورة غير صالحة `2026-W99` → 400 `kpi.cycle_key_invalid`.
  - B5: دورة مستقبلية `2026-W35` → 400 `calendar.cycle_not_open`.
  - B6: نوع فترة غير أسبوعيّ `Monthly` → 400 `kpi_eval.period_type_not_supported`.
- B7: قائمة حوكمة KPI (AdminGovernance) = 200، سليمة. **لا تغيير درجات صامت** (لا تقييم أُنشئ).

### C — اليوميّ (Daily) 8/8
- C1: `my-days`=200، `currentDayKey=2026-07-15` (اليوم).
- C2: الجمعة `2026-07-10` = `isHoliday=true, isSelectable=false`. ✅
- C3: **السبت `2026-07-11` = `isHoliday=false, isSelectable=true`** (تصحيح السياسة مُثبَت). ✅
- C4: الجمعة التالية `2026-07-17` = `isHoliday=true`.
- C5: اليوم `2026-07-15` = `isToday=true, isDueToday=true`.
- C6: الخميس المستقبل `2026-07-16` = `isFuture=true, isOpenForDraft=false` (مقفل).
- C7: كل مفاتيح الأيام بصيغة خادميّة `YYYY-MM-DD` (لا `input[type=date]`، لا إدخال يدويّ).
- C8: كل أيام العمل الماضية/الحالية (10) قابلة للاختيار.

### D — Regression 5/5
- D1 submissions / D2 dashboard/me / D3 notifications / D4 report-templates / D5 kpi-templates = **كلها 200**.

**لم يُرسَل أيّ بريد** (Email معطّل، والدخان كلّه قراءة/رفض).

---

## 7. تنظيف بيانات الدخان + الأساس (Phase 6 + 7)

الدخان لم يُخزِّن أيّ صفّ (قراءة + رفض 400 فقط) ⇒ التنظيف = تأكيد ثبات الأساس + إزالة سكربتات الخادم.

| العدّاد | أساس Phase 0 | بعد الدخان/التنظيف | الحالة |
|---|---|---|---|
| `AspNetUsers` | 35 | **35** | ✅ ثابت |
| `report_submissions` | 57 | **57** | ✅ ثابت |
| `kpi_evaluations` | 17 | **17** | ✅ ثابت |
| `kpi_evaluation_review_events` | 1 | **1** | ✅ ثابت |
| `notifications` | 101 | **101** | ✅ ثابت |
| `audit_logs` | 593 | **593** | ✅ ثابت |
| `email_outbox` | 0 | **0** | ✅ ثابت |
| الهجرات | 27 | **27** | ✅ ثابت |

- بيانات `CAL-R1-PROD-SMOKE` = **0** (لم يُنشأ شيء بالبادئة).
- لا admin مؤقّت أُنشئ (استُخدم المبذور القائم من env، قراءة فقط).
- سكربتات الدخان (`/root/cal-prod-*.mjs`) أُزيلت بالكامل.

**التحقّق بعد النشر (Phase 7):** خدمة active/running، NRestarts=0، health داخلي=200/عام=200، بصمات backend=المرشّح، bundle=المعتمد، migrations=27 (الرأس ثابت)، كل الأعلام false (`Email__Enabled=false`، Reminders/Scheduler/BackgroundJobs/Integrations/Notifications.Realtime غائبة=false)، email_outbox=0، `appsettings.Development.json` غائب.

---

## 8. مراقبة الساعة الأولى (Phase 8)

- 5× health متتالية = 200/200 كلها.
- NRestarts=0، الذاكرة ~236MB، active منذ 2026-07-15 10:05:15 UTC.
- سجلّ الخدمة منذ النشر: **لا 5xx، لا 42501، لا استثناء، لا deadlock، لا نشاط seeder/Email-send/Reminder/Scheduler.**
- مسارات التقويم حيّة (`my-cycles?context=report/kpi`, `my-days` = 401 بلا مصادقة ⇒ المسار مفعّل والمصادقة مفروضة).
- `email_outbox=0`، آخر `audit_log` = 08:18 UTC (قبل النشر ⇒ يؤكّد صفر كتابة من الدخان).
- حِمل المضيف = 0.00.

---

## 9. الالتزام بقواعد التشغيل الإلزامية

- ✅ العمل على الإنتاج فقط في مراحل النشر؛ RC/TEST لم تُمَسّ إلا قراءةً للمقارنة.
- ✅ بلا merge/push/تعديل مصدر؛ **بلا إعادة بناء من `develop`** — نُشرت أعيان RC المُتحقَّقة حصرًا (بصمة مطابقة قبل النشر).
- ✅ بلا هجرة جديدة («No migrations were applied»)؛ لم تظهر Pending Migration.
- ✅ بلا تصحيح بيانات W27/W28؛ بلا تغيير Navigation؛ Email/Reminders/Scheduler/BackgroundJobs = false.
- ✅ بلا بيانات اختبار دائمة؛ الدخان صفر-أثر؛ لا طباعة أسرار/توكنات/سلاسل اتصال.
- ✅ لا drift في البصمات/النسب؛ لا health fail/500/42501 ⇒ **لم يُستدعَ Rollback.**

---

## 10. خطة الرجوع (لو لزم مستقبلًا)

1. **Backend/Frontend:** استعادة `publish-backup-role-calendar-r1-20260715-100325` + `dist-backup-…` + `chown www-data` + restart.
2. **قاعدة البيانات:** لا هجرة في هذه الحزمة ⇒ **لا عكس مخطط**. الحزمة code-only ⇒ الرجوع = إرجاع DLLs/bundle فقط (أو استعادة الـdump عند الحاجة).
3. **التحقّق بعد الرجوع:** health=200، عدد الهجرات ثابت=27، السلوك القديم للتقويم.

---

## 11. الحكم

# ✅ PRODUCTION DEPLOYMENT SUCCESSFUL

**تَوقُّف تام.** لا تصحيح W27/W28، لا Navigation hotfix، لا Restore/Archive، لا تعديل قوالب، لا تفعيل Email/Scheduler — كلّها خارج نطاق هذه المهمّة وتنتظر موافقة صريحة منفصلة.
