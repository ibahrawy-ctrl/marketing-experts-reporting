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
| `r22a-rc-admin@khubara.local` (`0ce50482…`) | التعطيل الذاتيّ ممنوع بحارس الخدمة (`user.self_deactivate.conflict`) ⟹ أُعيد تعيين كلمته لقيمة عشوائيّة **غير محفوظة** وأُبطِلت رموز التجديد ⟹ تسجيل الدخول بكلمته السابقة **401** |
| الحذف الصلب `DELETE /users/{id}` | **لم يُستعمل عمدًا** — يُتلف أدلّة UAT ومراجع السجلّ الملحقيّ |
| ملفّات الأسرار المحلّيّة `/tmp/r22a-secrets/` | حُذِفت بالكامل (`LOCAL_SECRETS_GONE`) |
| ملفّات الخادم المؤقّتة | `shred -u -z` لـ15 ملفًّا في `/root` (منها رمز الدخول ونسخة ملفّ بيئة RC) + إزالة `/opt/reporting/staging-r22a` |
| ملفّ بيئة RC الحيّ | سليم ومطابق لنسخته قبل الحذف · `RC_HEALTH=200` |
| فحص الأسرار في `Ops/R22A/PROMOTION` | `0` تطابق عبر 30 ملفًّا |
| البيئات الثلاث بعد التنظيف | `PROD=200 · TEST=200 · RC=200` |

`TEMP_ADMIN_DEACTIVATED = NO (بحارس بنيويّ)` · `TEMP_ADMIN_LOGIN_REVOKED = YES` — نفس السابقة الموثَّقة في R21.

## 9) مفاتيح الإغلاق

```
R22A_RELEASE_SCOPE                = R22A_ONLY_FROM_897c9b18
GIN_INDEX_MIGRATION               = EXCLUDED
RC_PROMOTION_RESULT               = PASS
RC_FUNCTIONAL_ACCEPTANCE          = PASS
PRODUCTION_PROMOTION_RESULT       = PASS
PRODUCTION_OPERATIONAL_ACCEPTANCE = PASS
HISTORICAL_DATA_INTEGRITY         = INTACT
ROLLBACK_READINESS                = READY
R22A_RELEASE_STATUS               = RELEASED
```
