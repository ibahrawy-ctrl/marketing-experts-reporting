# P123 — فحص بيانات ما قبل الهجرة (Preflight) وخطّ الأساس

- **التاريخ:** 26 أغسطس 2026
- **المرشّح الأساس (الفاشل في UAT):** `9595c01937ca714deb80ebe6fc02c959f5c400d6`
- **فرع المعالجة:** `feature/p123-remediation-20260826` (worktree معزول، نظيف عند الإنشاء)
- **البيئة:** TEST حصرًا — `reporting_test_uat`
- **التفويض:** `APPROVE P123 UAT REMEDIATION, NEW CANDIDATE BUILD, TEST-ONLY DEPLOYMENT, AND TARGETED RE-UAT; … DO NOT TOUCH RC OR PRODUCTION`

## 1) بوّابة هوية البيئات (علامات مستقلّة — Phase A)

| العلامة | TEST | RC | Production |
|---|---|---|---|
| الخدمة | `khubara-reporting-test` | `khubara-reporting-rc` | `reporting-api` |
| MainPID عند خطّ الأساس | 1556576 | **1556575** | **1556574** |
| المنفذ | 127.0.0.1:5091 | 127.0.0.1:5092 | 127.0.0.1:5090 |
| القاعدة | `reporting_test_uat` | `reporting_rc` | `reporting_prod` |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | `ReleaseCandidate` | `Production` |
| جذر النشر | `/opt/reporting-test` | `/opt/reporting-rc` | `/opt/reporting` |
| `EmailNotifications__Mode` | `DryRun` | `DryRun` | `Enabled` |
| `Reminders__Enabled` | `false` | `false` | `true` |

**الحكم:** البيئات الثلاث منفصلة في الخدمة والمنفذ والقاعدة والجذر والبيئة ⇒ البوّابة **مجتازة**.
النسخة المنشورة على TEST عند خطّ الأساس: `1.0.0+9595c01937ca714deb80ebe6fc02c959f5c400d6` (مطابقة للمرشّح الفاشل ⇒ إعادة الإنتاج الحيّة صالحة).

## 2) النسخة الاحتياطيّة القابلة للاستعادة (قبل أيّ كتابة)

| البند | القيمة |
|---|---|
| المسار | `/var/lib/postgresql/reporting-test-p123-remed-20260826.dump` |
| الصيغة | `pg_dump -Fc` (custom, قابل لـ`pg_restore`) |
| الحجم | 544,169 بايت |
| التحقّق | `pg_restore -l` ⇒ **503** مدخل TOC مقروء |
| `md5` | `47a39a8543d965633162aff5d2a115f0` |

## 3) بصمات وعدّادات ما قبل الجولة — `reporting_test_uat`

| القياس | القيمة |
|---|---|
| `departments` (كلّي / اصطناعيّ `UAT-P123%`) | 15 / 7 |
| `teams` (كلّي / اصطناعيّ) | 16 / 5 |
| `AspNetUsers` (كلّي / اصطناعيّ `@uat123.test`) | 35 / 13 |
| `AspNetUserClaims` | **0** |
| `AspNetRoleClaims` | **0** |
| `attendance_incidents` | 17 |
| بصمة الإدارات غير الاصطناعيّة | `f8ccf7d97edc45f9b3c34895d28ea820` |
| بصمة الفرق غير الاصطناعيّة | `a2866e531e6d9c47039a1c123b04ff5b` |

## 4) اكتشاف التكرارات المانعة لقيد التفرّد (Preflight)

| التكرار | العدد | التصنيف |
|---|---|---|
| `departments.NameAr = 'UAT-P123-DEPT-ALPHA'` | 5 | **اصطناعيّ 100%** |
| `teams (DepartmentId=04627d73…, NameAr='UAT-P123-TEAM-A1')` | 4 | **اصطناعيّ 100%** |

**النتيجة الحاسمة:** **صفر تكرار غير اصطناعيّ**. لا يوجد أيّ مانع بيانات حقيقيّ أمام قيد التفرّد،
ولا حاجة إلى دمج أو حذف أيّ سجلّ غير اصطناعيّ. الحذف يقتصر على الصفوف الموسومة `UAT-P123` على TEST وحدها.

## 5) إعادة إنتاج العيوب على المرشّح `9595c01` (Phase B)

| العيب | الشدّة | طريقة إعادة الإنتاج | النتيجة المقيسة قبل الإصلاح |
|---|---|---|---|
| DEF-P123-003 | P2 | اختبار وحدويّ `ZzBaselineReproTests` على التوقيع القائم | `CanViewIncident(ctx_self, reporter)` = **`true`** بلا أيّ شرط على الحالة ⇒ الموظّف يرى `Draft`. التوقيع نفسه **لا يقبل الحالة إطلاقًا** فيستحيل التفريق. جذر السبب: `AttendanceAccess.cs:52`. |
| DEF-P123-001 | P2 | قراءة الكود + الحالة المقيسة على TEST | صفر تحقّق تفرّد في `DirectoryService.CreateDepartmentAsync:1220` و`UpdateDepartmentAsync:1243` و`CreateTeamAsync:942` و`UpdateTeamAsync:968`؛ وصفر فهرس فريد على `NameAr` في `OrgConfigurations.cs`. الأثر المقيس في القاعدة: 5 إدارات و4 فرق بأسماء متطابقة. |
| DEF-P123-002 | P3 | قراءة الكود | `OrgConfigurations.cs:17` يفرض فهرسًا فريدًا على `departments.Code`، ولا يوجد أيّ التقاط لـ`DbUpdateException`/`23505` في طبقة الدليل ⇒ الاستثناء يصعد غير مُترجَم ⇒ 500. |
| DEF-P123-005 | P3 | قياس UI موثَّق في جولة UAT | `adm`: `scrollWidth=520` مقابل `innerWidth=390` (فائض 130px). `leadA1`: 437 (فائض 47px). |
| DEF-P123-004 | P4 | تتبّع شبكة موثَّق في جولة UAT | `GET /api/reports/governance-summary` يُطلَق بلا شرط لدور Manager ⇒ 403 مكرّرة في كلّ تحميل. |

## 6) سؤال قرار مُسجَّل (لا يُخترَع جوابه)

**ملكيّة مفاتيح `perm` الافتراضيّة في الإنتاج غير محسومة في المصادر الحاكمة.**

- `AppPermissions.cs:6-9`: «لا تُمنَح أيّ منها لأيّ دور مخزَّن تلقائيًّا … والتعيين الفعليّ قرار نشر لاحق»، و«حتّى `Admin` لا يكتسبها ضمنًا».
- `Ops/P2-EMPLOYEE360-HR-OPERATIONS-IMPLEMENTATION-REPORT-20260825.md:313`: «إسناد الأدوار الفعليّ قرار نشر لاحق خارج نطاق هذه المرحلة».
- المصدر نفسه (سطر 419) يطلب «قرار حوكمة + سكربت منح مُراجَع» بلا تفاصيل تنفيذيّة.

**ما نُفِّذ:** بناء **الآليّة** فقط (منح/إلغاء فرديّ صريح، idempotent، مُدقَّق، قابل للإلغاء) — وهو ما يطلبه المصدر حرفيًّا.
**ما لم يُنفَّذ عمدًا:** أيّ ربط افتراضيّ دور↔مفتاح على مستوى الشركة. المنح على TEST يقتصر على مستخدمي UAT الاصطناعيّين.
