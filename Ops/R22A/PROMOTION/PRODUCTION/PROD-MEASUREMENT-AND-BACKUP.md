# الإنتاج — القياس قراءةً فقط (المرحلة 7) والنسخ الاحتياطيّ (المرحلة 8)

**البيئة:** `reports.emarketingacademy.net` · خدمة `reporting-api` · منفذ `5090` · قاعدة `reporting_prod`
**تاريخ القياس:** 2026-08-30 · **لم تُكتَب أيّ بيانات على الإنتاج في هذه المرحلة.**

## 1) قياس البيئة (المرحلة 7)

| مفتاح | القيمة المقيسة |
|---|---|
| `PROD_ENVIRONMENT` | `Production` (`ASPNETCORE_ENVIRONMENT`) |
| `PROD_HOST` | `reports.emarketingacademy.net` |
| `PROD_BACKEND_PORT` | `5090` (`ASPNETCORE_URLS=http://127.0.0.1:5090`) |
| `PROD_FRONTEND_ROOT` | `/opt/reporting/reporting-frontend/dist` |
| `PROD_DATABASE_NAME` | `reporting_prod` |
| `PROD_SERVICE_NAME` | `reporting-api` (`/etc/systemd/system/reporting-api.service`، مستخدم `www-data`) |
| `PROD_MAIN_PID` | `1603719` · `NRestarts=0` · `ActiveEnterTimestamp=2026-08-26 19:26:00Z` |
| `PROD_INFORMATIONAL_VERSION` | `1.0.0+897c9b187ab4216213b4f453ec65948cd06dff27` |
| `PROD_SOURCE_REVISION` | `897c9b187ab4216213b4f453ec65948cd06dff27` ⟹ **مطابق لـ`PRODUCTION_BASE_SHA`** |
| `PROD_BACKEND_SHA256` (بيان 86 ملفًّا) | `fa2f902154117e5219eb4f2d781b27650144abbc7e4104005b6e2930f8f1352a` |
| `Reporting.Api.dll` | `0b8e636a6d26df17463df66b4631548dbe16192f2ff30dc1256918054f61ee17` |
| `PROD_FRONTEND_SHA256` (بيان 7 ملفّات) | `d3b7f88cb59938178eae5a8fb0a4b7dbce3766ad56757b24eddd2df86c665672` |
| `PROD_MIGRATION_COUNT` | `47` |
| فهرس GIN | `0` (غائب) |
| `PROD_HEALTH` | `200` · `{"status":"ok","service":"reporting-api"}` |
| `PROD_EMAIL_MODE` | **`EmailNotifications__Mode=Enabled`** · `RecipientSafetyMode=Disabled` · `Email__Provider=GoogleWorkspace` |
| `PROD_SCHEDULERS` | `Scheduler__Enabled=true` · `Reminders__Enabled=true` · `ReportReminderScheduler__Enabled=true` · `BackgroundJobs__Enabled=false` |

`appsettings.json` داخل `publish` **متطابق بايتًا** بين الإنتاج والحزمة المرشَّحة
(`e6e29206658745f9f3f61a04dd226e840cb93499d18559bd4808d6653cdcbaee`) ⟹ حزمة الخلفيّة محايدة بيئيًّا،
والتهيئة كلّها من `/etc/reporting-api.env` وقت التشغيل.

### عزل البيئات الثلاث وقت القياس

| الخدمة | `MainPID` | `ActiveEnterTimestamp` |
|---|---|---|
| `reporting-api` (إنتاج · 5090) | `1603719` | `2026-08-26 19:26:00Z` |
| `khubara-reporting-test` (5091) | `1723290` | `2026-08-30 14:57:07Z` |
| `khubara-reporting-rc` (5092) | `1730282` | `2026-08-30 15:47:15Z` |

إقلاعا RC عند `15:47` هما دورتا حقن/استرجاع ملفّ البيئة الموثَّقتان في مرحلة RC (`NRestarts=0`، لا انهيار).

## 2) القالب الحقيقيّ — مُكتشَف بالاسم لا بمعرّف بيئة أخرى

بحث بالاسم `Title LIKE '%كاتب المحتوى%'` في `reporting_prod`:

| مفتاح | القيمة |
|---|---|
| `PROD_TEMPLATE_ID` | `5e6ad325-b26c-4fd6-b4f8-d415dae44c89` — «تقرير كاتب المحتوى الأسبوعي» |
| نوع الدوريّة / الحالة | `Weekly` · `Published` · `IsActive=t` · `Classification=Primary` |
| `PROD_EFFECTIVE_VERSION` | `8` |
| `PROD_EFFECTIVE_VERSION_ID` | `d934b8b0-4654-4b0c-8f53-a43d279b4e84` |
| عدد الإصدارات القائمة | `8` (v1..v8؛ **v8 وحده** `IsPublished=t`) |
| حقل القسم المتكرّر | `3a772ff2-5e32-4b0a-8646-42ffe210ba21` (`ProjectRepeatableSection`) |
| `PROD_CONFIG_SHA256` (كلّ حقول v8) | `0acac75f16efe385a639a26d8748869b543b87cd7fbf1463d1fff11065cd1579` |
| `md5(ConfigJson)` للقسم المتكرّر | `554bd2e116c7eff6c2e44a60aa261bb2` · `len=1105` |
| `PROD_SCHEMA_VERSION` | **غائب** (⟹ 1 ضمنًا) · `projfields=5` |
| `PROD_ASSIGNMENT_COUNT` | `0` في `report_template_assignments` — الإسناد عبر `JobRoleId=3ddd7c4b-0756-4179-9dca-931bf4fed43b` |
| `PROD_SUBMISSION_COUNT` (لهذا القالب) | `10` |

**ملاحظة حاكمة:** `md5(ConfigJson)` لقسم v8 على الإنتاج = `554bd2e1…` وهو **نفسه بالضبط** على RC
⟹ نقطة الانطلاق للتفعيل متطابقة بايتًا بين البيئتين، والتحويل المُثبَت على RC ينطبق حرفيًّا.

### الحقول الخمسة على الإنتاج (v8) — مطابقة لما رُحِّل على RC

```
content_type  Select    required=true   catalogDomain=content_type  options=12
content_goal  Select    required=true   catalogDomain=content_goal  options=9
work_status   Select    required=true   catalogDomain=work_status   options=4
count         Number    required=true   —                            —
notes         LongText  required=false  —                            —
رأس الإعداد: {"maxProjects": 0, "minProjects": 1, "projectRequired": true}
```

## 3) خطّ أساس التاريخ (قبل أيّ تغيير)

```
PROD_TOTAL_SUBMISSIONS                 = 328
PROD_HISTORICAL_SUBMISSION_IDS_SHA256  = 1583e802817500b772f23c57f6d4655a6b71008b6f9d6e6120ee23033b8f1174
PROD_HISTORICAL_FULL_SHA256            = 40523f7fb4e5113d8d13e1f38ba820005a97b6d7500805abb75c0ffcd2271ff6
   (Id|ReportTemplateVersionId|Status|UpdatedAtUtc مرتّبة بـId)
آخر إنشاء / آخر تعديل                   = 2026-08-30 12:20:46Z / 2026-08-30 12:22:59Z
EMAIL_OUTBOX_TOTAL = 0   ·   EMAIL_SENT = 0
```

توزيع ارتباط التسليمات بالإصدارات (كلّ القوالب): `v1→191 · v2→60 · v3→1 · v4→6 · v5→51 · v6→14 · v7→4 · v8→1`.
ولهذا القالب تحديدًا: `v5→4 · v6→5 · v8→1`.

⟹ **كلّ تقرير تاريخيّ مربوط بمعرّف الإصدار الذي أُنشئ عليه** (`ReportSubmission.ReportTemplateVersionId`)،
ونشر إصدار لاحق **لا يمسّ** هذا العمود — وهو ما أُثبِت مقيسًا على RC (`v5→2 · v9→1` بعد نشر v9).

### تسليم واحد قيد التنفيذ الآن (يخصّ مستخدمًا حقيقيًّا)

```
bba208cd-9fb1-4c70-881e-abd91c8c57fe   Status=Draft   Version=8   PeriodKey=2026-W35
```
سيبقى مرتبطًا بـv8 بعد أيّ نشر لاحق، ويُعرَض بمسار المخطّط v1 (`WorkItems is null ⇒ continue`).
على مستوى النظام كلّه: `Draft=10` · `Submitted=68`.

## 4) النسخ الاحتياطيّ الثلاثيّ (المرحلة 8) — نُفِّذ

**المجلّد:** `/opt/reporting/backups/r22a-20260830T161124Z`

| الملفّ | الحجم | SHA-256 |
|---|---|---|
| `backend-publish.tar.gz` (106 مدخلًا) | `47,716,391` | `9e306b85c1661533595eedcce5ec77c027ca887033bcb10a5dd07109f2108eaa` |
| `frontend-dist.tar.gz` (9 مداخل) | `409,201` | `efe741875938826ca1b807f5e3cf53a4a06f5048c13a9235307c41ebc49005e7` |
| `reporting_prod.dump` (`-Fc`) | `1,498,610` | `3a93ef135d3c707c3fe253c369b329cfde43276b2a1fda6a8778960f9d6ebe8d` |
| `reporting-api.env.bak` (0600) | `1,553` | — (يحوي أسرارًا؛ لا يُطبَع ولا يُنسَخ إلى المستودع) |

```
PG_RESTORE_LIST_EXIT = 0   ·   520 مدخلًا   ·   84 TABLE DATA   ·   199 INDEX
مساحة القرص بعد النسخ: 49G متاحة من 96G (50%)
```

### بوّابات المرحلة 8

```
PROD_ISOLATION_GATE        = PASS
PROD_BACKUP_GATE           = PASS
PROD_RESTORE_LIST_GATE     = PASS
PROD_ROLLBACK_READY        = YES
PROD_HISTORICAL_BASELINE   = CAPTURED
PROD_CHANGE_SCOPE          = R22A_ONLY
PROD_ARTIFACT_EQUALS_RC    = PARTIAL   ← خلفيّة: نعم · واجهة: مستحيل بنيويًّا (انظر §5)
```

## 5) توقّف إلزاميّ — تعارض بين شرط «نفس البايتات» وقاعدة «لا خلط بين البيئات»

**المقيس:** حزمة الواجهة تُثبِّت عنوان الـAPI **وقت البناء** لا وقت التشغيل:

```ts
// reporting-frontend/src/lib/api.ts:4
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5090/api';
```

| الحزمة | العنوان المضمَّن داخل `assets/*.js` |
|---|---|
| واجهة RC المنشورة (`eb95e6a1…`) | `https://rc-report.emarketingacademy.net/api` |
| واجهة الإنتاج الحاليّة (`d3b7f88c…`) | `https://reports.emarketingacademy.net/api` |
| واجهة TEST | `https://test.emarketingacademy.net/api` |

⟹ نسخ بايتات واجهة RC كما هي إلى الإنتاج يجعل متصفّحات **مستخدمي الإنتاج الحقيقيّين**
تنادي **واجهة RC البرمجيّة** — خلط مباشر بين بيئتين، وهو ممنوع بالقاعدة غير القابلة للتفاوض رقم 1،
كما أنّه يُعطِّل الإنتاج فعليًّا (RC خلف `auth_basic`).

**الخلفيّة لا تعاني هذا القيد إطلاقًا:** `publish` محايد بيئيًّا ويمكن نشره بنفس البايتات حرفيًّا
(`e91e655b21aabd6b63ecf7ab1824eb397e872aabfd4eecc74e18162060d4aff4`).

القرار مطلوب من مالك المنتج قبل أيّ كتابة على الإنتاج.
