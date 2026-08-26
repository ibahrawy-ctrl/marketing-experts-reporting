# تقرير نشر الإنتاج — علاج `DEF-P123-RC-001` والمرشّح `897c9b18`

**التاريخ:** 26 أغسطس 2026 · **البيئة:** الإنتاج `reports.emarketingacademy.net` · **الخدمة:** `reporting-api` (systemd، `www-data`، `127.0.0.1:5090`) · **القاعدة:** `reporting_prod`
**نافذة التنفيذ:** `2026-08-26T19:14Z` → `2026-08-26T19:47Z`

**الحكم النهائيّ:**

```
P123_PRODUCTION_DEPLOYMENT_PASS
```

---

## 0) المعرّفات الحاكمة

| المعرّف | القيمة |
|---|---|
| `HOTFIX_SHA` | `59f483ebd86211a793bd96a5b2a602fda123d36f` |
| `DEVELOP_MERGE_SHA` | `897c9b187ab4216213b4f453ec65948cd06dff27` |
| `RC_CANDIDATE_SHA` | `897c9b187ab4216213b4f453ec65948cd06dff27` |
| `PRODUCTION_CANDIDATE_SHA` | `897c9b187ab4216213b4f453ec65948cd06dff27` |
| الإصدار السابق على الإنتاج | `7e063b493b50ad90ba6131e47042c7cd035fb65b` (18 أغسطس) |
| عدد الإيداعات بين الإصدارين | **57** |
| `origin/develop` | `897c9b187ab4216213b4f453ec65948cd06dff27` (المرشّح **هو** رأس develop) |
| وسوم على المرشّح | **لا شيء** (لم يُنشأ وسم ولم يُدفَع) |

**تصحيح مقدّمة بائتة:** نصّ التكليف ذكر `8479d374238b71731996ad73d20d1485701d2053` بوصفه الـSHA الحاكم. ذلك المرشّح سبق دمج إصلاح `DEF-P123-RC-001` وسبق أيضًا `63b7d42` (إصلاح تنقّل تقارير Project 360). الشجرة الفعليّة الحاكمة عند بدء العمل كانت `897c9b18`، وهي التي بُنِيت وجُرِّبت ونُشِرت. لم يُمسّ `8479d374` ولم يُعدَّل أيّ تقرير سابق.

**المرجعان التاريخيّان (لم يُعدَّلا):**
`Ops/UAT/P123-RC-20260826/` · `Ops/UAT/P123-REMEDIATION-20260826/` · وبوّابة RC لهذه الجولة: `Ops/UAT/P123-RC-REVALIDATION-20260826/P123-RC-HOTFIX-VALIDATION-REPORT-20260826.md`.

## 1) بوّابة الإنتاج — لماذا فُتِحت

بوّابة الإنتاج لم تُفتَح باجتهاد، بل بتحقّق حرفيّ لشروط Phase 9 على RC الحيّ:

| الشرط | المقيس | الحكم |
|---|---|---|
| `0 FAIL` | 99 سيناريو: 97 PASS · 2 SUPERSEDED · **0 FAIL** | ✔ |
| `0 BLOCKED security scenarios` | 0 | ✔ |
| `0 open P0/P1/P2 defects` | `DEF-P123-RC-001` مغلق بـ10/10 على المِشدّ الموجَّه؛ لا عيب مفتوح آخر | ✔ |

ومن ثمّ صدر الحكم الداخليّ `P123_RC_HOTFIX_VALIDATION_PASS_PRODUCTION_GATE_OPEN`، وهو الشرط الذي علّق عليه مالك المنتج إذنه: «أكمل Phase 9 كاملة على RC؛ إذا كانت صفر FAIL وصفر BLOCKED وصفر P0/P1/P2 فافتح بوابة الإنتاج».

## 2) خطّ الأساس قبل النشر (Phase 10 — قراءة محضة)

| المقياس | القيمة |
|---|---|
| الخدمة | `active/running` · `MainPID=1556574` · `NRestarts=0` |
| ختم الخلفيّة | `1.0.0+7e063b493b50ad90ba6131e47042c7cd035fb65b` · `md5=ddf9598c0bf00f821a0aefe0c6cc1975` |
| حزمة الواجهة | `index-CMjXSPXr.js` · `md5=f37fd278b073dcf391549e4c1ab57318` |
| الهجرات | **42** · آخرها `20260817114129_AddProjectExecutionUpdateProposals` |
| المستخدمون | 34 (منهم 33 نشط) · الأدوار 12 · إسنادات الأدوار 39 |
| الإدارات / الفرق / التسليمات | 4 / 9 / 311 |
| سجلّ التدقيق | 1464 قيدًا |
| منح `perm` | **0** |
| طابور البريد | 0 |
| جداول الحضور | **غير موجودة** (الوحدة لم تصل الإنتاج بعد) |
| الأعلام التشغيليّة | `EmailNotifications__Mode=Enabled` · `Scheduler__Enabled=true` · `Reminders__Enabled=true` · `ReportReminderScheduler__Enabled=true` · `BackgroundJobs__Enabled=false` |
| أعلام `Phase2__*` | **غير موجودة في البيئة** ⇒ كلّها `false` بالافتراضيّ |

**قياس صريح للمخاطرة:** الفارق بين `7e063b49` و`897c9b18` ليس إصلاحًا نقطيًّا بل **إصدار كامل** (57 إيداعًا · 5 هجرات · وحدات Attendance وEmployee 360 وHR Operations وP2/P3 وشريحة P360). ونظرًا لأنّ البريد والمجدولات كانت مُشغَّلة فعليًّا على الإنتاج، جرى إطفاؤها قبل إعادة التشغيل ثمّ إعادتها تدريجيًّا (§5).

## 3) النسخة الاحتياطيّة (Phase 11)

`BACKUP_ID = /var/backups/reporting/20260826-P123` (47 ميغابايت إجمالًا)

| القطعة | الحجم (بايت) | SHA-256 |
|---|---:|---|
| `reporting_prod-20260826-P123.dump` | 1,424,971 | `7cd5e8888b97560f1e971890cda457d7b5a70a8298a5ed720169c304bcec4722` |
| `publish-20260826-P123.tar.gz` | 47,398,207 | `93e6469aabad368a870d95929bca22c65ae066196a0481bba55ee412cfc4fb43` |
| `frontend-dist-20260826-P123.tar.gz` | 394,527 | `cccbd4157ebcd2f4c41b0261641872aa2a080392bf635ff892acaed3efb53543` |
| `reporting-api.env.bak` (0600) | 1,553 | `afaac8b3ca9c61d09e16d7a75bc2bb7ea1f1d601eca75d9d631b8310860bd5c9` |
| `reporting-api.service.bak` | 363 | `1146b4f2e9674227a33a266f5fb35c6bdb207a2349fd8a705de770576064a1ff` |

**تحقّق الفهرس (TOC):** `pg_restore -l` أعطى 492 سطرًا و79 مدخل `TABLE DATA`، وفيها `AspNetUsers` و`report_submissions` و`__EFMigrationsHistory`. الفهرس مقروء ⇒ الملفّ ليس مبتورًا. لم يُكتفَ بوجود الملفّ.

## 4) البروفة الظلّيّة وتصنيف الهجرات (Phase 12)

استُعيدت نسخة اليوم في قاعدة `reporting_prod_shadow_p123`، ثمّ شُغِّلت **ثنائيّة المرشّح نفسها** عليها ببيئة الإنتاج الحرفيّة (بريد ومجدولات مطفأة، منفذ `5199`).

| المقياس | قبل الهجرة | بعد الهجرة |
|---|---|---|
| الهجرات | 42 | **47** |
| المستخدمون / الإدارات / الفرق / التسليمات | 34 / 4 / 9 / 311 | 34 / 4 / 9 / 311 |
| سجلّ التدقيق | 1464 | 1464 |
| `md5_users` | `a6385875…` | `a6385875…` |
| `md5_submissions` | `08defb3b…` | `08defb3b…` |
| منح `perm` | 0 | 0 |

زمن الإقلاع حتّى `/health` أخضر: **6.2 ثانية**. صفر استثناء حقيقيّ.

### تصنيف الهجرات الخمس

| # | الهجرة | العمليّات | التصنيف |
|---|---|---|---|
| 1 | `20260824195457_AddKpiTemplateVersionBelowTargetThreshold` | `AddColumn` | **Additive** |
| 2 | `20260824230015_AddManagementNoteSensitivity` | `AddColumn` | **Additive** |
| 3 | `20260824233938_AddAttendanceIncidents` | `CreateTable ×4` + `CreateIndex ×9` | **Additive** |
| 4 | `20260825111521_P2_HR010_EmployeeChecklistItems` | `CreateTable` + `CreateIndex ×2` | **Additive** |
| 5 | `20260826073223_P123DirectoryNameUniqueness` | حارس Preflight + `CreateIndex unique ×2` | **Compatible-with-guard** |

**Destructive = 0 · Data-transforming = 0.**

الهجرة الخامسة تحمل حارسًا يرفع استثناءً مفهومًا لو وُجد تكرار في أسماء الإدارات أو الفرق، **ولا يحذف ولا يدمج صفًّا واحدًا** — معالجة البيانات قرار تشغيليّ صريح خارج الهجرة. وقياس مسبق على بيانات الإنتاج الحيّة أعطى `dup_departments_NameAr = 0` و`dup_teams_Dept_NameAr = 0` ⇒ الحارس لن يُفعَّل.

خطّة التراجع الكاملة بدرجاتها الثلاث ومعايير إطلاقها الستّة: `P123-PROD-ROLLBACK-PLAN-20260826.md`.

## 5) التنفيذ (Phase 13) والأعلام (Phase 14)

### القِطع المنشورة

- **الخلفيّة:** رُقِّيت القطعة نفسها التي جُرِّبت على RC — `/opt/reporting-rc/publish` بختم `1.0.0+897c9b187ab4216213b4f453ec65948cd06dff27` و`md5=46e0a3a169c9ef2285aef3b8e9d7fe13` (86 ملفًّا). ترقية القطعة المختبَرة أقوى من إعادة بنائها لأنّها تلغي احتمال اختلاف البناء. و`appsettings.json` متطابق بين RC والإنتاج ⇒ لا إعداد بيئيّ مخبوز.
- **الواجهة:** **أُعيد بناؤها إلزامًا** من المرشّح نفسه بـ`VITE_API_BASE_URL=https://reports.emarketingacademy.net/api`، لأنّ حزمة RC تحمل عنوان RC مخبوزًا فيها وكان استعمالها سيكسر الإنتاج. الناتج `index-CTofEn_d.js` · `md5=218346d38e3103a2cf92914fdacd2f3d`، وفيه: عنوان الإنتاج ✔ · عنوان RC غائب ✔ · مسار `/app/projects/:projectId/reports/:reportId` موجود ✔.

### تسلسل التنفيذ

1. **تقليص أثر الانفجار:** أُطفئت `EmailNotifications__Mode` و`Scheduler__Enabled` و`Reminders__Enabled` و`ReportReminderScheduler__Enabled` قبل إعادة التشغيل، بعد حفظ نسخة `/etc/reporting-api.env.pre-p123`.
2. **التبديل:** أُوقفت الخدمة، ونُقلت الحزمة والواجهة القديمتان إلى `publish-pre-p123-20260826` و`dist-pre-p123-20260826` (لا حذف)، ورُكِّبت الجديدتان بملكيّة `www-data`.
3. **الإقلاع مع حارس تراجع تلقائيّ:** لو لم يخضرّ `/health` خلال 90 ثانية لكان السكربت أعاد كلّ شيء تلقائيًّا. **لم يُفعَّل الحارس.**

| المقياس | القيمة |
|---|---|
| `HEALTH_OK` | 1 · زمن الإقلاع **10 ثوانٍ** |
| الهجرات | 42 → **47** (الخمس المتوقَّعة، بالأسماء نفسها) |
| `NRestarts` | 0 |
| استثناءات حقيقيّة | **0** — المطابقتان الوحيدتان لكلمة `EXCEPTION` هما نصّ حارس `P123-PREFLIGHT` مطبوعًا داخل SQL، لا فشل تنفيذ |
| `ROLLED_BACK` | **لم يقع** |

### الأعلام والصلاحيات

| البند | قبل | بعد |
|---|---|---|
| `EmailNotifications__Mode` | `Enabled` | `Disabled` أثناء التبديل → `DryRun` للمراقبة → **`Enabled`** |
| `Scheduler__Enabled` / `Reminders__Enabled` / `ReportReminderScheduler__Enabled` | `true` | مطفأة أثناء التبديل → **`true`** |
| `BackgroundJobs__Enabled` | `false` | `false` |
| `Phase2__Employee360Enabled` / `AttendanceEnabled` / `HrOperationsEnabled` / `EmployeeChecklistEnabled` | غير موجودة ⇒ `false` | **غير موجودة ⇒ `false`** |
| منح `perm` للمستخدمين | 0 | **0** |
| منح `perm` للأدوار | 0 | **0** |

مقارنة آليّة بين ملفّ البيئة قبل النشر وبعده (الأعلام فقط، بلا طباعة أيّ سرّ) أعطت `IDENTICAL_TO_PRE_DEPLOY_FLAGS=1`: **لم يتغيّر علم تشغيليّ واحد بصافي الأثر.** ونافذة `DryRun` لمدّة 60 ثانية أعطت `outbox_total=0` و`unhandled_errors=0` ⇒ لا انفجار بريد عند إعادة التفعيل.

**سجلّ Phase 14 المطلوب حرفيًّا:**
- `Permission mechanism deployed` — نعم. سياسات `RequireClaim` على `HrOperationsView/Export` و`AttendanceReview/Export` و`EmployeeChecklistManage` مبنيّة على مطالبة `perm` صريحة، **لا يكتسبها أيّ دور مخزَّن تلقائيًّا ولا `Admin`**.
- `Real-user grants = 0` — نعم، مقيس مرّتين (`AspNetUserClaims` و`AspNetRoleClaims`).
- `Operational permission ownership = pending Product Owner decision` — نعم.

**لماذا بقيت أعلام المرحلة الثانية مطفأة:** إشعال `Phase2:AttendanceEnabled` مثلًا يكشف السطح، لكنّ العمل عليه يحتاج مطالبة `Attendance.Review` صريحة لموظّف حقيقيّ — وهو **ممنوع نصًّا** في تصريح التنفيذ. فإشعال العلم بلا مالك صلاحية يُنتج سطحًا لا يستطيع أحد تشغيله. الآليّة منشورة وجاهزة، والتفعيل قرار مالك المنتج.

## 6) التحقّق والمراقبة (Phases 15–16)

### التحقّق غير التدميريّ

- هويّة الخلفيّة الحيّة: `1.0.0+897c9b18…` ✔ · الواجهة التي يقدّمها نجينكس على HTTPS العامّ: `index-CTofEn_d.js` ✔ (أي أنّ ما يراه المستخدم فعلًا هو المرشّح، لا ما في القرص فقط).
- **لا نقطة محميّة تُجيب 200 لمجهول:** `/api/attendance→401` · `/api/audit-logs→401` · `/api/employees→404` · `/api/reports→404` · `/api/hr-operations/queues→404` · `/api/kpi/analytics→404`.
- **بصمات البيانات الحقيقيّة بعد النشر مطابقة بايتًا لما قبله:** `md5_users=a6385875…` · `md5_departments=7d805775…` · `md5_teams=a874a309…` · `md5_submissions=08defb3b…`.
- **سجلّ التدقيق لم يزد قيدًا واحدًا** (1464 → 1464) ⇒ النشر نفسه لم يكتب أثرًا تشغيليًّا.
- **الواجهة الحيّة (مجهولة الهويّة، 6 لقطات على 390/768/1440):** صفر خطأ Console · صفر فيض أفقي · `dir=rtl` في كلّ لقطة · وحارس المصادقة يحوّل المجهول من `/app` إلى `/login` في كلّ المقاسات.

> **حدّ صريح:** لم يُسجَّل دخول بأيّ حساب مستخدم حقيقيّ على الإنتاج. التحقّق الوظيفيّ العميق بالصفات الأربع جرى على RC (99 سيناريو) لا على الإنتاج، لأنّ استعمال بيانات اعتماد موظّف حقيقيّ خارج نطاق التصريح. هذا نقص مقصود ومُعلَن لا ثغرة في التقرير.

### المراقبة بعد النشر

ثماني عيّنات على مدى ثماني دقائق (`19:40Z` → `19:47Z`):

| المقياس | النتيجة |
|---|---|
| `ActiveState` | `active` في 8/8 |
| `NRestarts` | 0 في 8/8 |
| `MainPID` | `1603719` ثابت (لا إعادة تشغيل صامتة) |
| `/health` | `200` في 8/8 · أبطأ ردّ **3.7 ميلي ثانية** |
| استثناءات غير معالَجة | 0 في 8/8 |
| الذاكرة (RSS) | 311 → 324 ميغابايت (استقرار طبيعيّ بعد الإقلاع) |
| سجلّ بمستوى تحذير فأعلى | **0 سطر** |
| اتّصالات القاعدة / أطول استعلام | 2 / 0 ثانية |

## 7) المصالحة (Phase 17)

| المقياس | قبل (19:14Z) | بعد (19:36Z) | الحكم |
|---|---|---|---|
| الهجرات | 42 | 47 | +5 مقصودة |
| المستخدمون (الكلّ / النشط) | 34 / 33 | 34 / 33 | ثابت |
| الأدوار / إسنادات الأدوار | 12 / 39 | 12 / 39 | ثابت |
| الإدارات / الفرق / التسليمات | 4 / 9 / 311 | 4 / 9 / 311 | ثابت |
| سجلّ التدقيق | 1464 | 1464 | ثابت |
| منح `perm` (مستخدمون / أدوار) | 0 / 0 | 0 / 0 | ثابت |
| طابور البريد | 0 | 0 | ثابت |
| `md5_users` | `a6385875d3cfc436639864adfc3f4c0c` | نفسه | مطابق |
| `md5_departments` | `7d80557511c8efa0ca5616a4a59e8be7` | نفسه | مطابق |
| `md5_teams` | `a874a3098deb7b4746d2cf6e630adb55` | نفسه | مطابق |
| `md5_submissions` | `08defb3b860a6d4ad97ec31f0ee1b5cc` | نفسه | مطابق |
| `attendance_incidents` / `attendance_incident_events` | — (الجدول غير موجود) | 0 / 0 | جديد وفارغ |
| `attendance_incident_types` | — | **6** | كتالوج مرجعيّ يُبذَر عند الإقلاع (idempotent، إضافيّ بحت، لا بيانات موظّفين) |
| `employee_checklist_items` | — | 0 | جديد وفارغ |

**الصفوف الوحيدة التي أضافها النشر إلى الإنتاج هي 6 صفوف كتالوج مرجعيّ لأنواع وقائع الحضور** (`Absence`, `Disconnection`, `EarlyLeave`, `Late`, `Other`, `UnauthorizedAbsence`). لا صفّ بيانات موظّف واحد.

**فحص تسرّب البيانات الاصطناعيّة:** `users_matching_test_domains=0` · `departments_synthetic=0` · `teams_synthetic=0` ⇒ لم يتسرّب شيء من بيئات الاختبار.

### ما نُظِّف وما بقي عمدًا

| العنصر | المصير | السبب |
|---|---|---|
| `reporting_prod_shadow_p123` | **أُسقِطت** | قاعدة بروفة، انتهى دورها |
| `/opt/reporting/staging-p123-897c9b18` | **حُذِفت** | القطع صارت حيّة وموجودة في النسخة الاحتياطيّة |
| `/opt/reporting/publish-pre-p123-20260826` | **بقيت** | تراجع فوريّ من الدرجة أ |
| `/opt/reporting/reporting-frontend/dist-pre-p123-20260826` | **بقيت** | تراجع فوريّ من الدرجة أ |
| `/etc/reporting-api.env.pre-p123` | **بقيت** (0600) | تراجع من الدرجة ب |
| `/var/backups/reporting/20260826-P123` | **بقيت** (47MB) | تراجع من الدرجة ج |

## 8) مصفوفة السيناريوهات

`P123-PROD-DEPLOYMENT-SCENARIO-MATRIX-20260826.csv` — **46 سيناريو · 46 PASS · 0 FAIL**، كلٌّ منها مربوط بـ`ScenarioID` والوصف والمتوقَّع والمقيس والحالة والدليل و`CandidateSHA` والبيئة والطابع الزمنيّ.

المصفوفة **مولَّدة آليًّا** بـ`evidence/build-prod-matrix.py` من ملفّات الأدلّة الخام: لا صفّ فيها مكتوب يدويًّا، وكلّ قيمة «مقيسة» مستخرَجة بتعبير نمطيّ من ملفّ موجود. وإن غاب مفتاح تُكتب الحالة `FAIL` لا `PASS` — وهي الآليّة التي كشفت فعلًا تلفًا في ملفّ المراقبة (كاتبان متزامنان على الملفّ نفسه) فأُعيد قياس النافذة نظيفةً بدل ترقيع الرقم.

| المرحلة | عدد السيناريوهات |
|---|---:|
| Phase 10 — خطّ الأساس | 5 |
| Phase 11 — النسخة الاحتياطيّة | 3 |
| Phase 12 — البروفة الظلّيّة | 6 |
| Phase 13 — النشر | 5 |
| Phase 14 — الأعلام والصلاحيات | 5 |
| Phase 15 — التحقّق | 11 |
| Phase 16 — المراقبة | 5 |
| Phase 17 — المصالحة | 6 |
| **الإجماليّ** | **46** |

## 9) حالة العيب

| العيب | الخطورة | قبل | بعد |
|---|---|---|---|
| `DEF-P123-RC-001` — إفشاء وقائع الحضور السابقة للإرسال في مسارات القائمة | P2 | مفتوح على RC | **مغلق** — القاعدة موحّدة على مستوى الاستعلام قبل العدّ والتصفّح والإسقاط، و10/10 على المِشدّ الموجَّه على RC الحيّ، و`Attendance_List_And_Detail_UseEquivalentVisibilityRules` ضمن 14/14 |

**عيوب مفتوحة من فئة P0/P1/P2 عند إغلاق هذا التقرير: صفر.**

## 10) الأدلّة

```
Ops/UAT/P123-PROD-20260826/
├── P123-PRODUCTION-DEPLOYMENT-REPORT-20260826.md        (هذا الملفّ)
├── P123-PRODUCTION-DEPLOYMENT-REPORT-AR.docx / .pdf      (Word عربيّ RTL + PDF مطابق)
├── P123-PROD-CLOSURE-NOTE-20260826.md                    (مذكّرة الإغلاق + بوّابة الحكم)
├── P123-PROD-EVIDENCE-INDEX-20260826.csv                 (31 مدخلًا ببصمات md5)
├── P123-PROD-DEFECT-REGISTER-20260826.csv                (السجلّ المحدَّث)
├── P123-PROD-ROLLBACK-PLAN-20260826.md
├── P123-PROD-DEPLOYMENT-SCENARIO-MATRIX-20260826.csv     (46 صفًّا)
├── screenshots/  prod-{login,app}-{390,768,1440}.png + prod-ui-log.json
└── evidence/
    ├── prod-baseline-BEFORE.txt          prod-preflight-uniqueness.txt
    ├── prod-backup.txt                   prod-shadow-rehearsal.txt
    ├── shadow-attendance-seed.txt        prod-deploy-execution.txt
    ├── prod-deploy-errorlines.txt        prod-phase14-flags.txt
    ├── prod-phase15-verification.txt     prod-phase16-monitoring.txt
    ├── prod-phase17-reconciliation.txt   build-prod-matrix.py
    └── shadow-run.py · fix-shadow-owner.py (على الخادم في /tmp)
```

---

**الحكم النهائيّ:**

```
P123_PRODUCTION_DEPLOYMENT_PASS
```
