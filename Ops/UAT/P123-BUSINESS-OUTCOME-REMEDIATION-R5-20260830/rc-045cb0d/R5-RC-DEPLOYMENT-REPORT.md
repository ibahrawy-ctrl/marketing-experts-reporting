# تقرير ترقية RC إلى المرشّح `045cb0d` — P123 / R5

- **النطاق:** بيئة RC وحدها. **الإنتاج خارج النطاق تمامًا** ولم يُمسّ (البند 12).
- **`RC_CANDIDATE_SHA` المصرَّح به:** `045cb0df3e296a86fff3c1677d40534b86231778`
- **خطّ الأساس السابق على RC:** `4b8902eec9b67513d115dbc49c20af0dd62de8b8`
- **التاريخ:** 31 أغسطس 2026 · الأدلّة: `evidence/` و`shots/` بجوار هذا الملفّ.

---

## 1) خطّ أساس RC قبل النشر (المرحلة 1)

| البند | القيمة المقيسة قبل أيّ كتابة |
|---|---|
| الخدمة | `khubara-reporting-rc.service` · `ActiveState=active` |
| `MainPID` | `1730282` |
| `NRestarts` | `0` |
| بدء التشغيل | `Sun 2026-08-30 15:47:15 UTC` |
| `/health` | `200` |
| الختم الحيّ | `1.0.0+4b8902eec9b67513d115dbc49c20af0dd62de8b8` |
| ملفّات الحزمة | `86` · `RC_PKG_ALL=e91e655b21aabd6b` · `RC_DLLS=4a51ec9020401a90` |
| قائمة الملفّات | `RC_LISTING_SHA=abd254ff07ec0ffd` · `RC_TOTAL_BYTES=115122739` |
| الواجهة | `7` ملفّات · `RC_FE_PKG=eb95e6a13538984a` (`index-CmcRamKF.css` · `index-D_wZQwMS.js`) |
| القاعدة | `reporting_rc` |
| الهجرات | **47 صفًّا** = 45 هجرة كود عند `4b8902ee` + **صفَّا جسر النَسَب** الموثَّقان |
| ملفّ البيئة | `ENV_FILE_SHA256_16=3e157d43bb73416d` · 38 مفتاحًا · `mtime=2026-08-26 13:34:46 UTC` |
| الأعلام | `ASPNETCORE_ENVIRONMENT=ReleaseCandidate` · `Phase2__{Employee360,Attendance,HrOperations,EmployeeChecklist}Enabled=true` · `Email__Enabled=false` · `Reminders/Scheduler/BackgroundJobs/Notifications__Realtime/Integrations=false` · `EmailNotifications__Mode=DryRun` · `App__BaseUrl=https://rc-report.emarketingacademy.net` |
| الصلاحيّات | `UserClaims_perm=0` · `RoleClaims_perm=0` · `UserClaims_all=0` · `RoleClaims_all=0` |
| حجم جداول الهجرة | `submission_field_values` = 445 صفًّا / 224 kB · `kpi_template_assignments` = 0 صفّ / 32 kB · `AspNetUsers` = 51 صفًّا |
| الأعمدة الأربعة الجديدة | **غائبة** (تأكيد أنّ الهجرتين لم تُطبَّقا بعد) |
| سجلّ الأخطاء (24 س) | `0` مدخلًا بمستوى `err` |

عدّادات البيانات وبصمات المرجع: `evidence/rc-baseline-pre.txt`. لم يُطبَع أيّ سرّ أو Connection String.

---

## 2) SHA المرشّح ونَسَبه (المرحلة 0)

- `origin/develop` = `045cb0df3e296a86fff3c1677d40534b86231778` = `RC_CANDIDATE_SHA` المصرَّح به (لم يتحرّك).
- إثبات السلف (`git merge-base --is-ancestor … 045cb0d`):

| السلف | النتيجة |
|---|---|
| `4b8902ee` (خطّ أساس RC/الإنتاج المنشور) | **YES** |
| `cc336a5` (المرشّح الذي خضع لـUAT على TEST) | **YES** |
| `93c8cb6` | **YES** |
| `ad5df1e` (التزام الدمج المعتمد) | **YES** |
| `812ae903` | **YES** |

- **دلتا التشغيل بين `cc336a5` و`045cb0d` = فارغة** عبر المحاور الأربعة:
  - `reporting-backend` — OID الشجرة `2eed59ff765b85150cc702eaf01684482aa90976` **متطابق**
  - `reporting-frontend` — OID الشجرة `1c6a5f03f0243cb9e51c5a8f58b400e5afd3e521` **متطابق**
  - الهجرات — متطابقة (47 هجرة كود)
  - إعدادات النشر — متطابقة
  - الفرق الوحيد: ملفّ واحد مُضاف تحت `Ops/` (`R5-DEVELOP-MERGE-CLOSURE-REPORT.md`) · `OUTSIDE_OPS=0`
- استُخرِج `045cb0d` إلى مسار معزول `/tmp/rc045` (`git archive`) = **1906 ملفًّا · 95 ملفّ هجرة `.cs` = 47 هجرة**. لم يُبنَ من شجرة عمل قابلة للتلوّث.

---

## 3) بصمات Local / Staging / Live (المرحلتان 3 و5)

| البصمة | Local (`/tmp/rc045`) | Staging (`/opt/reporting-rc/staging-045cb0d`) | Live (`/opt/reporting-rc/publish`) |
|---|---|---|---|
| `PKG_ALL` (86 ملفًّا) | `7e2f4cbb0660a022` | `7e2f4cbb0660a022` | `7e2f4cbb0660a022` |
| `DLLS` (48 ملفًّا) | `5860dbad19b61b3f` | `5860dbad19b61b3f` | `5860dbad19b61b3f` |
| `FE_PKG` (7 ملفّات) | `46affff8dacb9ac5` | `46affff8dacb9ac5` | `46affff8dacb9ac5` |
| الختم | `1.0.0+045cb0df3e296a86fff3c1677d40534b86231778` | نفسه | **نفسه (مقروء من الـDLL الحيّ)** |

- قائمتا ملفّات Staging وLive **متطابقتان حرفيًّا** (`diff` فارغ).
- الختم حاضر في **الأربعة جميعًا**: `Reporting.Api` · `Reporting.Application` · `Reporting.Domain` · `Reporting.Infrastructure`.
- أصول الواجهة الحيّة: `index-CMupalax.js` · `index-Cz7lCSe8.css` · `VITE_API_BASE_URL=https://rc-report.emarketingacademy.net/api` مثبَّت داخل الحزمة.
- بصمة ملفّات الهجرة `MIG_FP=111d477aec008d29` · مسح الأسرار على النواتج `ARTIFACT_SECRET_HITS=0`.

> **ملاحظة تصحيحيّة مسجَّلة:** رقم `DLLS` الذي دوَّنتُه في المرحلة 3 (`42ac553d…`) كان محسوبًا بنطاق `find` مختلف على macOS. عند إعادة الحساب بالأمر نفسه على المستويات الثلاثة صار `5860dbad19b61b3f` في الثلاثة. البصمة الحاكمة `PKG_ALL` (تغطّي الـ86 ملفًّا كلّها بما فيها الـDLLs) كانت ولا تزال `7e2f4cbb0660a022` في المستويات الثلاثة.

---

## 4) النسخة الاحتياطيّة والتحقّق منها (المرحلة 2)

المسار: `/opt/reporting-rc/backup-r5-20260831T0615` (47 MB · `db/` بملكيّة `postgres` و`chmod 700`).

| المكوّن | التحقّق المقيس (لا الاكتفاء برمز الخروج) |
|---|---|
| `db/reporting_rc.custom.dump` | `pg_restore --list` رمز `0` · `TOC_ENTRIES=506` · `TABLE_DATA_ENTRIES=84` |
| `db/reporting_rc.schema.sql.gz` | `gzip -t` سليم · `CREATE TABLE`=**84** |
| `db/reporting_rc.data.sql.gz` | `gzip -t` سليم · كتل `COPY`=**84** · صفوف `__EFMigrationsHistory` داخل النسخة=**47** |
| `publish-4b8902ee.tar.gz` | `106` مدخلًا |
| `frontend-dist-4b8902ee.tar.gz` | `9` مداخل |
| `khubara-reporting-rc.env.bak` | `0600` · `ENV_BAK_SHA16 == ENV_LIVE_SHA16 = 3e157d43bb73416d` |

---

## 5) الهجرتان: البوابة والتنفيذ والأثر (المرحلتان 4 و5)

سكربت التنفيذ الفعليّ المُعايَن قبل التطبيق: `evidence/pending.sql` (**26 سطرًا**).

| # | الهجرة | العمليّات |
|---|---|---|
| 1 | `20260826185232_AddSubmissionFieldValueJsonGinIndex` | `CREATE INDEX … USING gin ("ValueJson" jsonb_path_ops)` |
| 2 | `20260829214324_R5_DecOneCadenceEffectivityAndEmploymentWindow` | `ADD "EffectiveFrom" date` · `ADD "EffectiveTo" date` · `ADD "ExitDate" date` · `ADD "HireDate" date` · `CREATE INDEX IX_kpi_template_assignments_ScopeType…` |

- **صفر `DROP`** · **صفر `UPDATE`/`DELETE`/Backfill** · **صفر `NOT NULL` بلا افتراضيّ** · لا هجرة ثالثة غير متوقَّعة.
- كلّ هجرة داخل معاملتها المستقلّة (`START TRANSACTION … COMMIT`).
- **خطر قفل GIN مقيس ومهمَل:** `submission_field_values` = 445 صفًّا / 224 kB ⇒ قفل `ACCESS EXCLUSIVE` بالمللي ثانية.
- **الأثر بعد التطبيق:** الهجرات `47 → 49`، الرأسان الجديدان حاضران، الأعمدة الأربعة `EffectiveFrom`/`EffectiveTo`/`ExitDate`/`HireDate` موجودة، والفهرسان `ix_submission_field_values_value_json_gin` و`IX_kpi_template_assignments_ScopeType_ScopeId_EffectiveFrom_Ef~` موجودان. صفر أخطاء هجرة.

> **انحراف مقيس ومعلَن (غير حاجب):** التعليمات قالت «عدد الهجرات النهائيّ = 47». الرقم الفعليّ **49** لأنّ RC كان يحمل أصلًا **47 صفًّا** (45 هجرة كود + صفَّا جسر النَسَب الموثَّقان، وهو نفس وضع الإنتاج) لا 45. مجموعة الهجرات المطبَّقة هي **بالضبط** الاثنتان المتوقَّعتان وصفر ثالثة ⇒ ليست «هجرة غير متوقّعة» وليست شرط توقّف.

---

## 6) صحّة الخدمة قبل/بعد

| البند | قبل | بعد |
|---|---|---|
| `ActiveState` | `active` | `active` |
| `MainPID` | `1730282` | **`1765319`** (جديد كما هو متوقَّع) |
| `NRestarts` | `0` | `0` (لا حلقة إعادة تشغيل) |
| بدء التشغيل | `2026-08-30 15:47:15 UTC` | `2026-08-31 04:49:41 UTC` |
| `/health` | `200` | `200` — `{"status":"ok","service":"reporting-api"}` |
| الختم | `1.0.0+4b8902ee…` | `1.0.0+045cb0df…` |
| مداخل `err` في السجلّ | `0` | **`0`** (`journalctl -p err` = `-- No entries --`) |
| استجابات `5xx` | — | **`0`** |

طريقة إعادة التشغيل: `systemctl stop/start khubara-reporting-rc` حصرًا. **لم يُستعمل `pkill` ولا أيّ نمط قتل واسع.**

---

## 7) نتائج Smoke وUAT سيناريو بسيناريو (المرحلة 6)

### 7-أ) طبقة الـAPI — **25 PASS · 0 FAIL** (`evidence/uat-api.json`)

| المعرّف | السيناريو | النتيجة | الدليل المقيس |
|---|---|---|---|
| S00 | دخول الحسابين الاصطناعيَّين | PASS | `HTTP 200` للاثنين |
| S01 | وصول مجهول ⇒ **401** | PASS | `HTTP 401` على `/api/kpi/performance` |
| S02 | `/health` | PASS | `{"status":"ok"}` |
| S03 | `/api/kpi/performance` بلا `cadence` | PASS | `HTTP 200` · مفاتيح `periodResolved/cadence/company/departments/teams/employees` |
| S04 | المسار الأسبوعيّ الصريح `cadence=WeeklyPulse` | PASS | `HTTP 200` · `cadence=WeeklyPulse` |
| S05 | المسار الربعيّ الرسميّ `cadence=Quarterly` | PASS | `HTTP 200` |
| S06 | `/api/kpi/periods/resolve` | PASS | `2026-Q3` · `2026-07-01→2026-09-30` · `Asia/Riyadh` |
| S07 | `/api/kpi/drilldown` | PASS | `HTTP 200` |
| S08 | `/api/kpi/rankings` | PASS | `HTTP 200` |
| S10 | نطاق الموظّف على `/api/kpi/performance` | PASS | `HTTP 200` بلا 5xx |
| S11 | استقلال عدّادَي المسارَين لنفس الفترة | PASS | كائنا `company` مختلفان تمامًا |
| S12 | بلا `cadence` ⇒ الخادم يحسم تواتر كلّ موظّف (DEC-01/2) | PASS | `expected` الافتراضيّ **253** ≠ الأسبوعيّ **481** ≠ الربعيّ **37** ⇒ لا سقوط صامت ولا خلط |
| S13 | الفترة الافتراضيّة = الربع الجاري (DEC-01/1) | PASS | `2026-Q3` · `isOpen=true` · `Asia/Riyadh` |
| S14 | حقول `Expected/AdjustedExpected/Coverage/الحالة` حاضرة | PASS | `expectedEvaluationCount` · `adjustedExpectedCount` · `coverage` · `coveragePercent` · `dataQuality` · `journeyState` |
| S15 | التصدير الماليّ (قراءة فقط) | PASS | `HTTP 200` · `rowCount=0` · العقد `year/quarter/status/rows` |
| S16 | التصدير الماليّ ربعيّ حصرًا | PASS | لا معامل `cadence` في العقد إطلاقًا |
| S17 | مُصادَق غير مخوَّل ⇒ **403** · مجهول ⇒ **401** | PASS | `/api/email-control/status`=**403** · `/api/audit-logs`=**403** · `finance-export`=**403** · المجهول=**401** |
| S19 | استقلال المسارَين عدديًّا في `2026-Q3` | PASS | `WeeklyPulse expected=481 eligible=3` مقابل `Quarterly expected=37 eligible=0` |
| S20 | الحالة المسمّاة عند نقص التغطية | PASS | الأسبوعيّ `dataQuality=InsufficientCoverage` (`coverage%=0.62`) · الربعيّ `NoData` (`0`) |
| S21 | **النبض الأسبوعيّ لا يدخل المتوسّط الرسميّ** | PASS | قيمتان فرديّتان `98.25` و`94.25` مستبعدتان ⇒ `company.value=null` |
| S22 | سُلَّم الأولويّة يُطبَّق لكلّ مسار على حدة | PASS | `effectiveCadence=WeeklyPulse` · `cadenceSource=jobRole` لكلّ موظّف |
| S23 | عدّادات الموظّف نفسه مستقلّة بين المسارَين | PASS | «أحمد عبدالفتاح»: أسبوعيّ `expected=13/eligible=1` مقابل ربعيّ `expected=1/eligible=0` |
| S24 | Drill-down على المسار الأسبوعيّ | PASS | `HTTP 200` · `recomputedValue/rows/measure` |
| S25 | Rankings يستبعد غير المؤهّلين | PASS | `HTTP 200` · `eligibleForRanking=false` للمستبعَدين |
| S26 | التصدير الماليّ لا يتأثّر بمعامل `cadence` | PASS | الاستجابتان **متطابقتان** مع/بلا `cadence=WeeklyPulse` |

### 7-ب) طبقة الواجهة على حزمة RC الحيّة — **10 PASS · 0 FAIL** (`evidence/uat-browser.json` · `shots/`)

| المعرّف | السيناريو | النتيجة | الدليل |
|---|---|---|---|
| B01 | صفحة الدخول تُحمَّل من الحزمة الحيّة | PASS | `shots/B01-login.png` |
| B02 | الدخول والتحويل إلى `/app` | PASS | `shots/B02-home.png` |
| B03 | `/app/kpi` على الربع الجاري | PASS | «الربع 3 — 2026» ظاهر · `shots/B03-kpi.png` |
| B04 | المساران ظاهران كمسارَين مستقلَّين | PASS | مفردات «نبض/أسبوع» و«ربع» حاضرة معًا |
| B05 | مؤشّرات التغطية/المتوقَّع ظاهرة | PASS | «تغطية» ظاهرة مرّتين |
| B06 | صفحة التصدير الماليّ (قراءة فقط) | PASS | `shots/B06-finance.png` |
| B07 | سطح التسليمات (R22A) | PASS | `shots/B07-submissions.png` |
| B08 | سطح المشاريع/Project 360 (R22A) | PASS | `shots/B08-projects.png` |
| B09 | **صفر 5xx** | PASS | 36 نداءً · `5xx=0` |
| B10 | **صفر خطأ Console غير مفسَّر** | PASS | `0` |

> **طريقة القياس:** حزمة `dist` المُشغَّلة على RC نفسها (بصمة `46affff8dacb9ac5` مطابقة للخادم بايتًا ببايت) تُخدَم محلّيًّا، وكلّ نداء إلى `https://rc-report.emarketingacademy.net/api/**` يُعترَض ويُحوَّل عبر نفق SSH إلى `127.0.0.1:5092` على RC. السبب: `auth_basic` لنطاق RC تجزئته غير قابلة للاسترجاع وتغيير `htpasswd` محظور. **المنطق والبيانات كلّها من خادم RC الحقيقيّ — لا محاكاة.**

### 7-ج) الأعلام قبل/بعد
جدول الأعلام في §1 أُعيد قياسه بعد النشر وبعد الـUAT: **مطابق حرفيًّا**، و`ENV_SHA=3e157d43bb73416d` و`mtime=2026-08-26 13:34:46 UTC` بلا تغيير ⇒ **لم يُغيَّر أيّ Feature Flag**.

---

## 8) فحص انحدار أسطح R22A وما خارج R5 (المرحلة 7) — **21 PASS · 0 FAIL**

`evidence/uat-r22a.json`

| المعرّف | السطح | النتيجة |
|---|---|---|
| G01 | `/api/report-templates` (`ReportTemplateService`) | PASS · `n=41` |
| G02 | `/api/submissions` | PASS · `n=38` |
| G03 | `/api/projects` | PASS · `n=30` |
| G04 | `/api/clients` | PASS · `n=8` |
| G05 | `/api/dashboard/me` | PASS |
| G06 | `/api/reports/due/overview` | PASS · `n=10` |
| G07 | `/api/reports/submission-completeness` | PASS · `n=7` |
| G08 | `/api/kpi-templates` | PASS · `n=15` |
| G09 | `/api/directory/departments` | PASS · `n=6` |
| G10 | `/api/report-view-grants` | PASS |
| G11 | **Project 360 — نظرة عامّة** | PASS |
| G12 | `/api/reporting-calendar/my-cycles` | PASS · `n=7` |
| G13 | مسارات عمل المشروع | PASS |
| G14 | أهداف المشروع | PASS |
| G15 | **شريحة نظرة عامّة على التسليمات** | PASS · `n=435` |
| G16 | ملخّص التسليمات | PASS |
| G17 | **شريحة تسليمات المشروع 360** | PASS · `n=435` |
| G18 | التسليمات المعلّقة للاعتماد | PASS |
| G19 | امتثال التسليمات | PASS · `n=12` |
| G20 | اتّجاه الامتثال | PASS |
| G21 | استراتيجيّة المشروع (`204` = لا محتوى، سلوك صحيح) | PASS |

⇒ **استعادة النَسَب حفظت المحتوى عمليًّا لا نظريًّا فقط**: كلّ أسطح R22A تعمل على الحزمة الجديدة بالأرقام نفسها.

---

## 9) أثر البيانات والتنظيف (المرحلة 8)

### الكتابة الوحيدة على RC
لم توجد **أيّ** بيانات اعتماد RC متاحة ومصرَّح بها: كلّ حسابات `@rc.local`/`@rc-uat.local` عُطِّلت عمدًا في إغلاق 23 أغسطس، و`r22a-rc-admin` أُزيلت كلمة مروره في إغلاق R22A، و`/etc/khubara-reporting-rc.env` بلا `Seed__AdminPassword`. لذلك — ووفق ترخيص «حساب وبيانات اصطناعيّة موسومة بوضوح للسيناريوهات الحاسمة» — أُنشئ **حسابان اصطناعيّان موسومان فقط**:

- `r5-rc-verify-admin@rc-uat.local` (`Admin`) — «R5 RC Verify Admin (SYNTHETIC)»
- `r5-rc-verify-emp@rc-uat.local` (`Employee`) — «R5 RC Verify Employee (SYNTHETIC)»

لم يُمسّ أيّ مستخدم حقيقيّ، ولا هيكل تنظيميّ، ولا تعريف دور، ولا `perm`، ولا قالب منشور.

### المقارنة قبل/بعد

| العدّاد | قبل | بعد | الفرق |
|---|---|---|---|
| `clients` | 9 | 9 | 0 |
| `departments` | 6 | 6 | 0 |
| `teams` | 11 | 11 | 0 |
| `projects` | 36 | 36 | 0 |
| `kpi_templates` | 15 | 15 | 0 |
| `kpi_template_versions` | 23 | 23 | 0 |
| `kpi_template_assignments` | 0 | 0 | 0 |
| `kpi_evaluations` | 4 | 4 | 0 |
| `kpi_results` | 37 | 37 | 0 |
| `report_templates` | 41 | 41 | 0 |
| `report_submissions` | 40 | 40 | 0 |
| `submission_field_values` | 445 | 445 | 0 |
| `UserClaims_perm` / `RoleClaims_perm` | 0 / 0 | 0 / 0 | 0 |
| `users` | 51 | 53 | **+2** (الاصطناعيّان) |
| `userroles` | 57 | 59 | **+2** (الاصطناعيّان) |
| الهجرات | 47 | 49 | **+2** (المتوقّعتان) |

**بصمات المرجع (باستبعاد الصفوف الاصطناعيّة، بنفس تعريف الاستعلام الأصليّ) — متطابقة تمامًا:**

| البصمة | قبل | بعد |
|---|---|---|
| `USERS_MD5` | `0c606ab32db92aab12256d9c15a1aa03` | `0c606ab32db92aab12256d9c15a1aa03` |
| `ROLES_MD5` | `0f791989e79a53f310d3f030b28debbb` | `0f791989e79a53f310d3f030b28debbb` |
| `TEMPLATE_MD5` | `b5add49245ec9de80e23d4805e183699` | `b5add49245ec9de80e23d4805e183699` |
| `RPTTPL_MD5` | `a4d9df5a718695bc3de3da18e14b198c` | `a4d9df5a718695bc3de3da18e14b198c` |

### التنظيف المنفَّذ
- الحسابان الاصطناعيّان: `IsActive=false` · `PasswordHash=NULL` · `SecurityStamp` جديد · `LockoutEnd=2099-12-31` ⇒ **محاولة الدخول بعد التعطيل = `401` للاثنين (مقيس)**.
- صفَّاهما في `AspNetUsers`/`AspNetUserRoles` أُبقيا للتدقيق (سياسة append-only للحسابات، كسابقة R22A).
- كلمة المرور المؤقّتة حُذفت من القرص (`/tmp/rc045/.pw` غير موجود) و**لم تُطبَع في أيّ مخرَج**.
- الأدلّة المنسوخة مُطهَّرة: `REMAINING_JWT=0`.
- نفق SSH وخادم الملفّات المحلّيّ أُغلقا، وملفّات SQL المؤقّتة على الخادم (`/tmp/mkusers.sql` · `/tmp/cleanup.sql` · `/tmp/q1.sql` · `/tmp/q2.sql` · `/tmp/post*.sql`) حُذفت.
- **لم يُرسَل أيّ بريد**: `EmailNotifications__Mode=DryRun` و`Email__Enabled=false` بلا تغيير.

---

## 10) العيوب والسيناريوهات غير المنفَّذة

- **صفر عيوب حاجبة.** صفر `FAIL` في 56 سيناريو (25 API + 10 واجهة + 21 انحدار).
- **سيناريو الكتابة الوظيفيّة الكاملة (إنشاء تقييم KPI ودورة اعتماد) = `NOT EXECUTED` عمدًا** — الـUAT على RC قراءة فقط بالافتراض، ولم يكن أيّ سيناريو حاسم لـR5 يستلزم الكتابة: استقلال المسارَين وقاعدة استبعاد النبض الأسبوعيّ وحالة نقص التغطية كلّها أُثبِتت على **بيانات RC القائمة** (481 مقابل 37 متوقَّعًا، وقيمتان مستبعدتان فعليًّا).
- **محدوديّة بيانات مقيسة (ليست عيبًا في الكود):** `kpi_template_assignments = 0` و`kpi_evaluations = 4` على RC ⇒ المسار الربعيّ الرسميّ يعطي `NoData` والتصدير الماليّ `rowCount=0`. هذا **السلوك الصحيح** لبيانات شحيحة، لكنّه يعني أنّ «قيمة ربعيّة رسميّة غير صفريّة» لم تُشاهَد على RC. القيَم الربعيّة غير الصفريّة كانت قد شوهدت على TEST في UAT الـ30 سيناريو المغلق.
- **انحراف رقم الهجرات 49 بدل 47** — موثَّق ومبرَّر في §5، غير حاجب.
- **تصحيح بصمة `DLLS` المدوَّنة في المرحلة 3** — موثَّق في §3، غير حاجب.

---

## 11) خطّة التراجع (مُثبَتة، غير منفَّذة)

**الأصول جاهزة ومُتحقَّق منها الآن:**

| الأصل | الحالة المقيسة |
|---|---|
| `/opt/reporting-rc/publish.prev` | `PKG=e91e655b21aabd6b` · الختم `1.0.0+4b8902ee…` ⇒ **مطابق بايتًا للحزمة التي كانت تعمل** |
| `/opt/reporting-rc/frontend/dist.prev` | `eb95e6a13538984a` ⇒ مطابق للواجهة السابقة |
| `/opt/reporting-rc/backup-r5-20260831T0615` | 47 MB · النسخ الثلاث + الحزمتان + `env.bak` — كلّها مُتحقَّق منها (§4) |

**الإجراء عند فشل الخدمة/الصحّة (RTO مقدَّر ≈ 60–90 ثانية، مقيس من زمن دورة النشر الفعليّة):**
1. `systemctl stop khubara-reporting-rc`
2. `mv publish publish.045cb0d && mv publish.prev publish` · مثله لـ`frontend/dist`
3. `chown -R www-data:www-data` ثمّ `systemctl start khubara-reporting-rc`
4. تحقّق: `ActiveState=active` · `/health=200` · الختم يعود `1.0.0+4b8902ee…`

**لا تُستعاد القاعدة تلقائيًّا.** الهجرتان **إضافيّتان بحتًا** (4 أعمدة `date` قابلة للإفراغ + فهرسان) ⇒ الكود الأقدم `4b8902ee` يتجاهلها ويعمل فوق المخطَّط الجديد بلا كسر. استعادة القاعدة **مدمِّرة** ولا تُنفَّذ إلّا عند تلف مُثبَت أو هجرة فاشلة — ولم يقع أيّ منهما.

---

## 12) إثبات أنّ الإنتاج لم يُمسّ

| البند | القيمة |
|---|---|
| `reporting-api.service` | `active` · `MainPID=1735125` · `NRestarts=0` |
| بدء التشغيل | `Sun 2026-08-30 16:40:58 UTC` — **قبل بدء هذه العمليّة بيوم كامل** |
| `/health` (5090) | `200` |
| بصمة حزمة الإنتاج | `e91e655b21aabd6b` (حزمة `4b8902ee` — بلا تغيير) |
| `mtime` لـ`/opt/reporting/publish` | `2026-08-30 15:35:57 UTC` |
| هجرات `reporting_prod` | **47** (بلا تغيير — الهجرتان لم تُطبَّقا على الإنتاج) |
| `mtime` لملفّ بيئة الإنتاج | `2026-08-26 19:25:59 UTC` |

**لم يُنفَّذ أيّ أمر كتابة أو إعادة تشغيل أو هجرة على الإنتاج، ولم يُدفَع أيّ كود، ولم يُعدَّل `develop`.**

---

## 13) التوصية بشأن الإنتاج

**التوصية: المضيّ في ترقية الإنتاج إلى `045cb0d` — مشروطة بتصريح صريح جديد.**

الأسباب المقيسة:
1. المرشّح `045cb0d` **سليل مباشر** لخطّ الأساس المنشور `4b8902ee`، ودلتا التشغيل بينه وبين المرشّح الذي نجح في UAT على TEST (`cc336a5`) **فارغة** بإثبات OID الشجرة.
2. الترقية على RC نجحت بلا حادثة: `0` إعادة تشغيل · `0` خطأ · `0` استجابة `5xx` · `/health=200`.
3. الهجرتان **إضافيّتان بحتًا** بلا حذف ولا Backfill، وخطر القفل مقيس ومهمَل على حجم RC — **لكن يجب إعادة قياسه على حجم الإنتاج قبل التطبيق** (حجم `submission_field_values` في الإنتاج أكبر بكثير من 445 صفًّا).
4. صفر انحدار على أسطح R22A (21/21) ⇒ استعادة النَسَب لم تُسقط محتوى.
5. البيانات المرجعيّة على RC لم تتغيّر إطلاقًا (4 بصمات MD5 متطابقة، 12 عدّادًا بفرق صفر).

**شروط لازمة قبل نشر الإنتاج (لم تُنفَّذ ضمن هذا النطاق):**
- إعادة قياس حجم `submission_field_values` على الإنتاج وتقدير زمن قفل `CREATE INDEX` غير المتزامن، والنظر في `CONCURRENTLY` إن تجاوز العتبة المقبولة.
- نسخة احتياطيّة ثلاثيّة للإنتاج بنفس مستوى التحقّق المطبَّق في §4.
- التحقّق من `FileStorage__DocumentsRootPath` وإعدادات الإنتاج بعد النشر.
- **حسابات الإنتاج حقيقيّة ⇒ ممنوع منعًا باتًّا إنشاء أيّ حساب اصطناعيّ عليه؛** يجب توفير هويّة UAT رسميّة مسبقًا أو قبول Smoke قراءة فقط.

---

```
R5_RC_DEPLOYMENT_PASS
RC_CANDIDATE_SHA=045cb0d
R5_RC_CRITICAL_UAT_PASS
AWAITING_PRODUCTION_DEPLOYMENT_APPROVAL
```
