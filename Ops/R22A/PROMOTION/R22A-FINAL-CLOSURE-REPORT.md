# R22A — تقرير الإغلاق النهائيّ للترقية المتّصلة TEST → RC → الإنتاج

**التاريخ:** 2026-08-30 · **النطاق:** الخيار B — إصدار R22A فقط من `PRODUCTION_BASE_SHA = 897c9b18`
**فرع الإصدار المعزول:** `release/r22a-from-897c9b18` · **رأسه:** `4b8902eec9b67513d115dbc49c20af0dd62de8b8`
**الملفّات التفصيليّة:** `RC/RC-DEPLOYMENT-ACTIVATION-AND-UAT.md` · `PRODUCTION/PROD-MEASUREMENT-AND-BACKUP.md` ·
`PRODUCTION/PROD-DEPLOYMENT-ACTIVATION-SMOKE-AND-ROLLBACK.md` · `R22A-RELEASE-SCOPE-MANIFEST.md`

## 1) ما دخل الإصدار فعلًا

| البند | القرار |
|---|---|
| إصلاح الحارس `36a6a5b` + دعم بنود العمل المتعدّدة | مضمَّن |
| تفعيل `schemaVersion=2` / `workItems` على القالب التشغيليّ الحقيقيّ | مضمَّن |
| هجرة فهرس GIN | **مستبعدة** — `RC_MIGRATION_APPLIED = NO` · `PROD_MIGRATION_APPLIED = NO` (47 → 47) |
| تعديل `CLAUDE.md` | لم يُنفَّذ (بناء ونشر من فرع الإصدار المعزول حصرًا) |

**بوّابة اختبارات إضافيّة (اكتملت 2026-08-30 18:31 محلّيًّا):** حزمة تكامل الخلفيّة كاملة
`Failed: 0 · Passed: 2212 · Skipped: 0 · Total: 2212` · `Duration = 7m51s` · `EXIT = 0`
(شجرة العمل المعزولة `~/r22a-release` على `4b8902e`). لم تُشغَّل على أيّ قاعدة حيّة.

## 2) RC — النشر والتفعيل وUAT

نُشِرت بايتات محلّيّة مطابقة بلا بناء على الخادم؛ التفعيل بالواجهة الرسميّة حصرًا (4 نداءات، بلا SQL)،
والقالب اكتُشِف **بالاسم** لا بمعرّف بيئة أخرى. `v9` منشور و`EFFECTIVE_VERSION = 9`،
`schemaVersion=2 · projfields=0 · wi_fields=5 · minItems=1 · maxItems=0 · uniqueBy=0` و`FIELDS_MATCH_V8 = true`.
UAT تشغيليّ بحساب **Employee فقط** على المتصفّح: **14/14 PASS**، صفر أخطاء تطبيقيّة، صفر 5xx، صفر بريد.

## 3) الإنتاج — التوقّف الإلزاميّ وقراره

تعارض مقيس: `VITE_API_BASE_URL` يُدمَج **وقت البناء** ⟹ استحالة تطابق بايتات الواجهة بين بيئتين.
قرار مالك المنتج: إعادة بناء واجهة الإنتاج من نفس المصدر والأدوات مع اختلاف بيئيّ وحيد مصرَّح به.

```
PROD_FRONTEND_SOURCE_EQUALS_RC        = YES
PROD_FRONTEND_LOCKFILE_EQUALS_RC      = YES
PROD_FRONTEND_TOOLCHAIN_EQUALS_RC     = YES
PROD_FRONTEND_BUILD_COMMAND_EQUALS_RC = YES
PROD_FRONTEND_ENV_DIFF                = VITE_API_BASE_URL_ONLY   ← مُثبَت بايتيًّا
PROD_FRONTEND_ARTIFACT_EQUALS_RC      = NO_BY_DESIGN
PROD_BACKEND_ARTIFACT_EQUALS_RC       = YES
```

إثبات «الاختلاف الوحيد»: إعادة بناء RC من `4b8902ee` أعادت إنتاج البيان `eb95e6a1…` حرفيًّا،
ثمّ `replace(prod_bundle, PROD_URL → RC_URL) == rc_bundle` ⟹ `True` (الطول `1,645,208` للطرفين، وCSS متطابق `64088fac…`).
عدد مرّات ظهور عنوان RC داخل حزمة الإنتاج = `0`، وعنوان TEST = `0`، و`localhost` = `0`، ولا أسرار داخل الحزمة.

## 4) الإنتاج — الحالة بعد النشر والتفعيل

```
PROD_SOURCE_REVISION = 4b8902eec9b67513d115dbc49c20af0dd62de8b8   (كان 897c9b18…)
PROD_BACKEND_SHA256  = e91e655b21aabd6b63ecf7ab1824eb397e872aabfd4eecc74e18162060d4aff4  == RC
Reporting.Api.dll    = 1ed95cfab17d0467b0e259a32e4bd76c9aa9da593dcd6a5ff6639a157706bad7  == RC
PROD_FRONTEND_SHA256 = 06e4bda669164a9f0d811c34137b051c10d12fe56eba0d91b47ff1808da96cb3
MainPID 1603719 → 1735125 · NRestarts=0 · /health=200 · نافذة النشر 16:40:54Z–16:40:58Z
MIGRATIONS 47 → 47 · GIN=0 · /etc/reporting-api.env لم يُمسّ (cmp: afaac8b3…)
```

التفعيل بأربعة نداءات رسميّة فقط على القالب `5e6ad325-b26c-4fd6-b4f8-d415dae44c89`:

| النداء | النتيجة |
|---|---|
| `POST /report-templates/{id}/versions` | `200` · مسودّة **v9** `ed4796e4-9d21-4443-8c3f-091efe6ffcb8` |
| `PUT /report-templates/fields/{fieldId}` | `200` · الحقل `8d756e33-6188-4ccf-9416-72d2b4b7ac5a` |
| `POST /report-templates/versions/{id}/publish` | `200` |
| `GET /report-templates/{id}/preview` | `200` · `EFFECTIVE_VERSION = 9` |

`schemaVersion=2 · projfields=0 · wi_fields=5 · min=1 · max=0 · uniq=0 · FIELDS_MATCH_V8=t`.
معرّفات الإنتاج **مختلفة تمامًا** عن معرّفات RC ⟹ لا نسخ معرّفات بين البيئات.

## 5) Smoke قراءة فقط على الإنتاج

```
PRODUCTION_SMOKE_MODE     = READ_ONLY
PRODUCTION_DATA_WRITES    = 0        (صفر POST/PUT/PATCH/DELETE)
PRODUCTION_EMAILS_TRIGGERED = 0      (email_outbox = 0 قبل وبعد)
API_CALLS = 15 · الحالات المرصودة = [200] فقط · 5xx = 0 · consoleErrors = 0
journalctl -p err خلال النافذة = فارغ
```

فُتِح تقرير تاريخيّ قائم (`39dfdaeb…`) داخل نطاق مشروعه وعُرِض سليمًا، وفُتِحت صفحة Project 360
وصفحة المشروع وظهرت التقارير التاريخيّة سليمة (اللقطات في `PRODUCTION/evidence/`).
الجلسة حُقِنت برمز وصول من تسجيل دخول رسميّ على الخادم ⟹ **لا كلمة مرور إنتاج وصلت المتصفّح أو الجهاز**،
ولذلك بقي `refresh_tokens` على `3908` قبل وبعد.

عدّادات الجداول القابلة للكتابة قبل == بعد: `328 / 3536 / 1039 / 0 / 742 / 1524 / 3908 / 0`.

## 6) سلامة البيانات التاريخيّة — القياس الختاميّ

```
TOTAL_SUBMISSIONS                = 328   (بلا تغيير)
changed_since_deploy             = 0     (لا صفّ عُدِّل بعد 16:40Z)
created_since_deploy             = 0
توزيع الارتباط بالإصدارات        = v5→4 · v6→5 · v8→1   (بلا تغيير)
md5(ConfigJson) لـv8             = 554bd2e116c7eff6c2e44a60aa261bb2 · len=1105 · schemaVersion غائب
المسودّة الجارية bba208cd…       = ما زالت على v8، لم تُفتَح ولم تُمسّ
email_outbox                     = 0
```

## 7) الجاهزيّة للتراجع

```
النسخ الثلاثيّ: /opt/reporting/backups/r22a-20260830T161124Z
  backend-publish.tar.gz  9e306b85… · frontend-dist.tar.gz efe74187… · reporting_prod.dump 3a93ef13…
  reporting-api.env.bak (0600، لا يُنسَخ إلى المستودع)
نسخ حيّة إضافيّة: publish.pre-r22a · dist.pre-r22a
PG_RESTORE_LIST_EXIT = 0 · 520 مدخلًا · 84 TABLE DATA · 199 INDEX
```

التراجع تصاعديّ: (1) إرجاع الواجهة وحدها · (2) إرجاع `publish` وإعادة التشغيل ·
(3) نشر إصدار خلَف يعيد `schemaVersion=1` بالواجهة الرسميّة · (4) استرجاع القاعدة من `-Fc` (آخر ملاذ).
**لا هجرة طُبِّقت ⟹ لا تراجع مخطَّطيّ مطلوب.**

## 8) التنظيف الأمنيّ

| البند | النتيجة |
|---|---|
| `r22a-rc-uat@khubara.local` (`cd065768…`) | `PUT /api/directory/users/{id}` بـ`isActive=false` ⟹ `200` · تسجيل الدخول الآن **403** |
| `r22a-rc-admin@khubara.local` (`0ce50482…`) | **عُطِّل نهائيًّا** بأداة صيانة رسميّة تستعمل `UserManager` (التفاصيل في §10) |
| الحذف الصلب `DELETE /users/{id}` | **لم يُستعمل عمدًا** — يُتلف أدلّة UAT ومراجع السجلّ الملحقيّ |
| ملفّات الأسرار المحلّيّة `/tmp/r22a-secrets/` | حُذِفت بالكامل (`LOCAL_SECRETS_GONE`) |
| ملفّات الخادم المؤقّتة | `shred -u -z` لـ15 ملفًّا في `/root` (منها رمز الدخول ونسخة ملفّ بيئة RC) + إزالة `/opt/reporting/staging-r22a` |
| ملفّ بيئة RC الحيّ | سليم ومطابق لنسخته قبل الحذف · `RC_HEALTH=200` |
| فحص الأسرار في `Ops/R22A/PROMOTION` | `0` تطابق عبر 30 ملفًّا |
| البيئات الثلاث بعد التنظيف | `PROD=200 · TEST=200 · RC=200` |

## 9) إغلاق الحوكمة (30 أغسطس · بعد اعتماد الترقية التشغيليّة نهائيًّا · بلا Rollback)

### 9-أ) مراقبة الإنتاج بعد النشر — قراءة فقط، 15 دقيقة

النافذة `2026-08-30T17:12:38Z → 17:27:38Z` (**15:00 دقيقة**، أربع عيّنات: `T0` · `T+5` · `T+7` · `T+15`).
`diff` بين عيّنة البداية وعيّنة النهاية بعد استبعاد الطابع الزمنيّ = **`NO_DIFF`** (تطابق تامّ سطرًا بسطر).

| القياس | القيمة الثابتة عبر العيّنات الأربع |
|---|---|
| `/health` | `200` |
| الخدمة | `active/running` · `MainPID=1735125` · **`NRestarts=0`** · `ActiveEnter=16:40:58Z` |
| `journalctl -p err` منذ النشر | `0` · وتحذيرات `0` |
| 5xx على nginx (تراكميّ) | `0` |
| `nginx error.log` (تراكميّ) | `8` **ثابت** — كلّها **قبل** النشر وتخصّ تطبيق `emarketingacademy.net` الآخر ومحاولتَي `auth_basic` على RC ⟹ صفر خطأ جديد |
| `EFFECTIVE_VERSION` | `9` |
| `schemaVersion` / `workItems.fields` | `2` / `5` (و`projfields=0` · `min=1` · `max=0`) |
| الهجرات · فهارس GIN | `47` · `0` |
| `TOTAL_SUBMISSIONS` | `328` (لا نشاط أعمال جديد في النافذة) |
| توزيع ارتباط الإصدارات | `v5→4 · v6→5 · v8→1` — **لا إعادة ربط لأيّ تقرير تاريخيّ** |
| بصمة التسليمات `MON_HIST_SHA256` | `c24ca8f989b8bfaee8032ce3d3c11f918e3186f4d486e6aafde1fca54c61fc52` (ثابتة) |
| `V8_CFG_MD5` | `554bd2e116c7eff6c2e44a60aa261bb2` · `len=1105` · `schemaVersion` غائب |
| المسودّة `bba208cd…` | `VersionNumber=8` · `Draft` · `2026-W35` — لم تُمسّ |
| البريد | `EmailNotifications__Mode=Enabled` (الإعداد الإنتاجيّ الصحيح) · `email_outbox=0` · `email_notifications=742` **بلا زيادة** ⟹ صفر رسالة من R22A |
| عدّادات أخرى | `notifications=1039` · `submission_field_values=3536` · `projects=35` · `refresh_tokens=3908` · `AspNetUsers=34` — كلّها ثابتة |

> بصمة `MON_HIST_SHA256` معرّفة لهذه المرحلة صراحةً كـ`sha256` لسلسلة `Id|ReportTemplateVersionId|Status`
> لكلّ التسليمات مرتَّبةً بـ`Id`؛ وهي مستقلّة عن بصمة مرحلة النشر `40523f7f…` التي بقيت مرجعًا لتلك المرحلة.

### 9-ب) تعطيل حساب RC المؤقّت — بالآليّة الرسميّة

أداة صيانة مبنيّة من **نفس شجرة الإصدار** (`4b8902ee`) تستعمل `UserManager<ApplicationUser>`
وكيانات EF الرسميّة: **لا SQL خامّ · لا حذف صلب · لا إنشاء Admin دائم جديد · لا تعديل مباشر لـ`PasswordHash`**.

| الخطوة | الآليّة | النتيجة |
|---|---|---|
| 1 | `user.IsActive = false` ثمّ `UserManager.UpdateAsync` | `OK` |
| 2 | `UserManager.RemovePasswordAsync` | `OK` |
| 3 | `UserManager.UpdateSecurityStampAsync` | `OK` |
| 4 | `SetLockoutEnabledAsync` + `SetLockoutEndDateAsync(MaxValue)` | `OK` |
| 5 | إبطال رموز التجديد لهذا المستخدم وحده (كيان `RefreshToken`) | `revoked=0` — الأربعة كلّها كانت مُبطَلة/منتهية أصلًا (`user_live=0`) |

```
TARGET = r22a-rc-admin@khubara.local  ·  0ce50482-bc9b-4ccd-8102-792574cb0c9e  ·  الدور Admin
قبل :  IsActive=True   HasPassword=True   SecurityStamp=a51a3de42321…  LockoutEnd=-
بعد :  IsActive=False  HasPassword=False  SecurityStamp=d7c0c577fcc6…  LockoutEnd=infinity
رموز التجديد: user_total=4 user_live=0 (قبل = بعد)  ·  global_total=374  global_live=357 (قبل = بعد)
OTHERS_FINGERPRINT (50 حسابًا) = af4667ce96eeae39… قبل == بعد  ⟹  OTHERS_UNCHANGED = YES
```

**إثبات رفض الدخول** (نداء رسميّ مباشر إلى `127.0.0.1:5092/api/auth/login` تفاديًا لطبقة `auth_basic`):

```
POST /api/auth/login  →  401  {"type":"auth.invalid_credentials","status":401}
مُكرَّرًا بكلمتَي مرور مختلفتَين ⟹ الرفض بنيويّ لا عَرَضيّ: PasswordHash = NULL فأيّ كلمة تُرفَض،
وحتّى لو مرّت لكان الحارس التالي IsActive=false يردّ auth.account_disabled.
```

أداة الصيانة بُنِيت خارج المستودع وحُذِفت بعد الاستعمال مع كلّ ملفّات المراقبة المؤقّتة
(`/root/r22a-maint*` · `/root/r22a-mon-*` · `/tmp/r22a_mon.sql` على الخادم، و`/tmp/r22a-*` محلّيًّا
بما فيها حزم النشر المؤقّتة). ملفّا بيئة الإنتاج وRC سليمان (`74ecfd78…` · `81caf91e…`)،
و`staging-r22a` غير موجود، والخدمات الثلاث `active` بـ`NRestarts=0`.

الحساب الآخر `r22a-rc-uat@khubara.local` (`cd065768…`) كان **معطَّلًا مسبقًا** (`IsActive=false`) ولم يُمسّ في هذه المرحلة.
`RC_ACTIVE_ADMINS = 3` (المشروعون فقط) · `RC_USERS_TOTAL = 51 · ACTIVE = 35` · `RC_HEALTH = 200`.

### 9-ج) تثبيت نَسَب الإصدار على `origin`

```
origin/release/r22a-from-897c9b18 = 4b8902eec9b67513d115dbc49c20af0dd62de8b8   ← فرع جديد، بلا force
tag r22a-prod-20260830            = 3ab4c32d… (موسوم موثَّق)  →  ^{}  = 4b8902eec9b67513d115dbc49c20af0dd62de8b8
الوصوليّة: git branch -r --contains 4b8902ee  ⟹  origin/release/r22a-from-897c9b18
origin/develop                    = ff1e337 → 32e5fec  (تقديم سريع، بلا force)
origin/main                       = 508509ad8474b321c80cbdd48eb84ecb54bee212  ← لم يُمسّ
```

### 9-د) الالتزام التوثيقيّ

`Ops/R22A/PROMOTION/` **حصرًا**: 32 ملفًّا · 1963 سطرًا مضافًا · الالتزام `32e5fec`.
مستبعَد صراحةً وبإثبات `git diff --cached` = صفر خارج النطاق: `CLAUDE.md` · ملفّات `Ops/R21/… 2.md` ·
أيّ أسرار أو رموز أو جلسات · أيّ حزم مؤقّتة أو نسخ احتياطيّة.
فحص الأسرار قبل التجهيز: **`0` تطابق عبر 32 ملفًّا** (JWT · Bearer · إسنادات كلمات مرور · سلاسل اتصال ·
ترويسات Authorization · مصادقة أساسيّة داخل URL · Set-Cookie · مفاتيح خاصّة · `me_access`/`me_refresh`).

### 9-هـ) القياس النهائيّ للبيئات الثلاث

| البيئة | الخدمة | `/health` | بيان الحزمة | الإصدار الإعلاميّ | الهجرات |
|---|---|---|---|---|---|
| **الإنتاج** | `reporting-api` (5090 · `reporting_prod`) | `200` | `e91e655b21aabd6b…` | `1.0.0+4b8902eec9b67513…` | `47` |
| **RC** | `khubara-reporting-rc` (5092 · `reporting_rc`) | `200` | `e91e655b21aabd6b…` **مطابق للإنتاج** | `1.0.0+4b8902eec9b67513…` | `47` |
| **TEST** | `khubara-reporting-test` (5091 · `reporting_test_uat`) | `200` | `cb6d13571b253bdd…` | `1.0.0+5b211db61982011d…` | `47` |

واجهة الإنتاج `/opt/reporting/reporting-frontend/dist` = `06e4bda669164a9f…` (بلا تغيير عن لحظة النشر):
عنوان الإنتاج داخل الحزمة `2` · عنوان RC `0` · عنوان TEST `0` · `localhost:5090` `0` · `workItems` `18`.
TEST يحمل عمدًا خطّ مرشّح P123-R5 (`5b211db…`) وهو **خارج نطاق R22A** ولا يؤثّر على الإنتاج أو RC.

## 10) مفاتيح الإغلاق

```
R22A_RELEASE_SCOPE                = R22A_ONLY_FROM_897c9b18
GIN_INDEX_MIGRATION               = EXCLUDED
RC_PROMOTION_RESULT               = PASS
RC_FUNCTIONAL_ACCEPTANCE          = PASS
PRODUCTION_PROMOTION_RESULT       = PASS
PRODUCTION_OPERATIONAL_ACCEPTANCE = PASS
POST_DEPLOY_MONITORING            = PASS
TEMP_RC_ADMIN_DISABLED            = YES
RELEASE_SOURCE_ON_ORIGIN          = YES
PROMOTION_EVIDENCE_COMMITTED      = YES
HISTORICAL_DATA_INTEGRITY         = INTACT
ROLLBACK_READINESS                = READY
ORIGIN_MAIN_TOUCHED               = NO
FORCE_PUSH                        = NO
R22A_RELEASE_STATUS               = RELEASED
```
