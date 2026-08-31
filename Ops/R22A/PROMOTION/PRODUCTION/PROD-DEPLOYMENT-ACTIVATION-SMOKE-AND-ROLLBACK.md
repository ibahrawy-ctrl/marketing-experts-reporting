# الإنتاج — النشر والتفعيل والدخان والتراجع (R22A · المراحل 9–11)

**البيئة:** `reports.emarketingacademy.net` · خدمة `reporting-api` · منفذ `5090` · قاعدة `reporting_prod`
**المصدر:** فرع `release/r22a-from-897c9b18` رأسه `4b8902eec9b67513d115dbc49c20af0dd62de8b8`

## 1) تكافؤ حزمة الواجهة مع RC (القرار المعتمد من مالك المنتج)

| مفتاح | القيمة |
|---|---|
| `PROD_FRONTEND_SOURCE_EQUALS_RC` | `YES` — نفس الالتزام `4b8902ee…`، `git status` للواجهة **فارغ** |
| `PROD_FRONTEND_LOCKFILE_EQUALS_RC` | `YES` — `package-lock.json` = `8c5c9342611118a6bc5bef59143b2354c133df54176780d24b5f41a19952b965` · `package.json` = `1e68e834f521360e09f96488847648a71b74f2471aab589d73b9a44d2d5f124a` · `vite.config.ts` = `050535b79e3d406083ec0eea0cda91ce2a9876c6eb89bb4a03b0e687e89c5ada` |
| `PROD_FRONTEND_TOOLCHAIN_EQUALS_RC` | `YES` — نفس الجهاز، Node `v26.0.0` · npm `11.12.1` · نفس `node_modules` |
| `PROD_FRONTEND_BUILD_COMMAND_EQUALS_RC` | `YES` — `npm run build` (`tsc -b && vite build`) |
| `PROD_FRONTEND_ENV_DIFF` | **`VITE_API_BASE_URL_ONLY`** |
| `PROD_FRONTEND_ARTIFACT_EQUALS_RC` | `NO_BY_DESIGN` |
| `PROD_BACKEND_ARTIFACT_EQUALS_RC` | **`YES`** |

### دليل الحتميّة (خطوتان مقيستان)

1. **إعادة إنتاج بناء RC:** أُعيد البناء من `4b8902ee` بـ`VITE_API_BASE_URL=https://rc-report.emarketingacademy.net/api`
   فأنتج بيانًا مطابقًا **بايتًا** لواجهة RC المنشورة:
   `eb95e6a13538984ad4cf0feb96999304369d8dad011c98337c0ad68d44e9be3f` (`diff` فارغ).
   ⟹ سلسلة البناء حتميّة، والمتغيّر الوحيد هو `VITE_API_BASE_URL`.

2. **إثبات أنّ الفرق لا يتجاوز العنوان — على مستوى البايت:**

```
occurrences RC-url  in RC bundle   = 2      occurrences PROD-url in PROD bundle = 2
PROD-url in RC bundle = 0           RC-url in PROD bundle = 0
replace(PROD_bundle, PROD_url → RC_url)  ==  RC_bundle   →   True
len = 1,645,208 = len(RC_bundle)
ملفّ CSS متطابق تمامًا: 64088facbaf2974cf7235216f346fb0b78f94785b279e63430bb35b7e244c37c
```

⟹ **لا HARD STOP**: الاختلاف الوحيد بين الحزمتين هو `VITE_API_BASE_URL` حرفيًّا.

### فحص الحزمة النهائيّة قبل النشر

```
https://reports.emarketingacademy.net  حاضر (مرّتان)
rc-report                = 0      test.emarketingacademy = 0      localhost:509x = 0
workItems حاضر · «+ إضافة بند عمل» حاضر · schemaVersion حاضر
مسح الأسرار داخل الحزمة  = 0 تطابق (الثلاث كلمات مرور + jwt/connectionstring = 0)
   الرموز الوحيدة المطابقة لنمط «password» هي أسماء حقول واجهة: password/newPassword/currentPassword/changePassword
`http://localhost` الظاهر مصدره مكتبتان داخليّتان (react-router + window.location.href fallback) لا عنوان API
```

## 2) النشر (المرحلة 9أ) — بايتات RC نفسها للخلفيّة، بلا إعادة بناء وبلا هجرة

النقل تمّ بـ`rsync` (محاولة `tar` من macOS أُلغيت لأنّها ولّدت 105 ملفّ `._*` من AppleDouble
فأنتجت بيانًا `b8402001…` مخالفًا — كُشِف بالمقارنة **قبل** أيّ تبديل ولم يُنشَر).

| مفتاح | قبل | بعد |
|---|---|---|
| `PROD_SOURCE_REVISION` | `897c9b187ab4216213b4f453ec65948cd06dff27` | `4b8902eec9b67513d115dbc49c20af0dd62de8b8` |
| `PROD_BACKEND_SHA256` (86 ملفًّا) | `fa2f9021…` | **`e91e655b21aabd6b63ecf7ab1824eb397e872aabfd4eecc74e18162060d4aff4`** = RC |
| `Reporting.Api.dll` | `0b8e636a…` | **`1ed95cfab17d0467b0e259a32e4bd76c9aa9da593dcd6a5ff6639a157706bad7`** = RC |
| `PROD_FRONTEND_SHA256` (7 ملفّات) | `d3b7f88c…` | `06e4bda669164a9f0d811c34137b051c10d12fe56eba0d91b47ff1808da96cb3` |
| عنوان الـAPI في الحزمة المنشورة | — | `https://reports.emarketingacademy.net/api` |
| `MainPID` | `1603719` | `1735125` · `NRestarts=0` · `ActiveState=active` |
| `/health` | `200` | `200` |
| عدد الهجرات | `47` | `47` ⟹ **`PROD_MIGRATION_APPLIED = NO`** |
| فهرس GIN | `0` | `0` ⟹ **`GIN_INDEX_MIGRATION_IN_R22A = EXCLUDED`** مؤكَّد |
| `/etc/reporting-api.env` | — | **لم يُمسّ**: `cmp` مع نسخة ما قبل النشر ⟹ متطابق · `afaac8b3…` |

نافذة النشر `16:40:54Z → 16:40:58Z`. النسخة السابقة محفوظة حيّة على الخادم:
`/opt/reporting/publish.pre-r22a` و`/opt/reporting/reporting-frontend/dist.pre-r22a`.
مفاتيح DataProtection في `/var/lib/reporting/.aspnet/DataProtection-Keys` **خارج** `publish` ⟹ لم تتأثّر.

## 3) تفعيل القالب (المرحلة 9ب) — أربعة نداءات رسميّة، بلا SQL

القالب اكتُشِف **بالاسم** في `reporting_prod`: `5e6ad325-b26c-4fd6-b4f8-d415dae44c89`
«تقرير كاتب المحتوى الأسبوعي». المصادقة بحساب المدير **المهيَّأ أصلًا** في `/etc/reporting-api.env`
عبر `POST /api/auth/login` (`200` · `roles=['Admin']`) — **لم تُعدَّل ولا تُعاد تعيين أيّ كلمة مرور، ولا لُمِس `PasswordHash`.**

| # | النداء | النتيجة |
|---|---|---|
| 1 | `POST /api/report-templates/{id}/versions` | `200` · مسودّة **v9** `ed4796e4-9d21-4443-8c3f-091efe6ffcb8` مستنسَخة من v8 المنشور (`cloned_projfields=5`) |
| 2 | `PUT /api/report-templates/fields/{fieldId}` | `200` · الحقل `8d756e33-6188-4ccf-9416-72d2b4b7ac5a` |
| 3 | `POST /api/report-templates/versions/{id}/publish` | `200` |
| 4 | `GET /api/report-templates/{id}/preview` | `200` · `PROD_EFFECTIVE_VERSION = 9` |

**معرّفات الإنتاج مختلفة تمامًا عن معرّفات RC** (`ed4796e4…` ≠ `e8bba9fc…` · `8d756e33…` ≠ حقل RC)
⟹ لم يُنسَخ أيّ GUID بين البيئتين.

### الشكل المطلوب حرفيًّا — مقيس من `jsonb`

```
schemaVersion = 2   projfields = 0   wi_fields = 5   minItems = 1   maxItems = 0   uniqueBy = 0
projectRequired = true   ·   minProjects = 1   ·   maxProjects = 0
FIELDS_MATCH_V8 = true    ← مساواة jsonb: الحقول الخمسة نُقِلت حرفيًّا من v8 بلا تحوير
content_type · content_goal · work_status · count · notes  بمفاتيحها وأنواعها وخياراتها وcatalogDomain
```

### عدم المساس بالتاريخ

```
الإصدارات v1..v9 كلّها قائمة — لم يُحذف ولا يُعدَّل أيّ إصدار ولا مسودّة قديمة
V8_CFG_MD5 (بعد) = 554bd2e116c7eff6c2e44a60aa261bb2   len=1105   schemaVersion=ABSENT   ← نفس ما قبل
توزيع ارتباط التسليمات لهذا القالب:  v5 → 4   ·   v6 → 5   ·   v8 → 1     ← بلا أيّ تغيير
TOTAL_SUBMISSIONS = 328 (قبل = بعد)
HIST_FULL_SHA256  = 40523f7fb4e5113d8d13e1f38ba820005a97b6d7500805abb75c0ffcd2271ff6 (قبل = بعد)
```

المسودّة الجارية لمستخدم حقيقيّ `bba208cd-9fb1-4c70-881e-abd91c8c57fe` (`2026-W35`) **بقيت مربوطة بـv8**
ولم تُفتح ولا تُلمَس إطلاقًا.

## 4) اختبار الدخان (المرحلة 10) — قراءة فقط

الجلسة أُدخِلت إلى المتصفّح بحقن الرمز الناتج عن نداء الدخول الرسميّ في `localStorage`
(`me_access`/`me_refresh`) ⟹ **لم تُدخَل كلمة مرور إنتاج في المتصفّح، ولم يُنشأ صفّ جلسة جديد.**

| # | الفحص | الدليل | النتيجة |
|---|---|---|---|
| 1 | `/health` | `200` · `{"status":"ok","service":"reporting-api"}` | PASS |
| 2 | الإصدار والمراجعة والبصمات | `4b8902ee…` · خلفيّة `e91e655b…` · واجهة `06e4bda6…` | PASS |
| 3 | الهجرات | `47` قبل = `47` بعد · لا هجرة جديدة | PASS |
| 4 | القالب والإصدار الفعّال | `5e6ad325…` · `EFFECTIVE = 9` | PASS |
| 5 | `schemaVersion` | `2` | PASS |
| 6 | `workItems.fields` | `5` | PASS |
| 7 | الإصدار السابق و`ConfigJson` | v8 `554bd2e1…` len 1105 بلا `schemaVersion` — بلا تغيير | PASS |
| 8 | التسليمات التاريخيّة قبل/بعد | `328` · `40523f7f…` متطابقان | PASS |
| 9 | فتح تقرير تاريخيّ من واجهة الإنتاج | `GET 200 /api/projects/2c1e6a2f…/reports/39dfdaeb…` — عُرِض كاملًا (سمر مجدي · 2026-W34 · v6 · «مُغلق» · 16 قطعة مطلوبة/16 مسلَّمة/15 معتمدة من أوّل مرّة) | PASS |
| 10 | Project 360 | `/app/projects/2c1e6a2f…/360` عُرِض سليمًا (المالك، الفريق، الصحّة، المؤشّرات) | PASS |
| 11 | مساحة عمل المشروع | **28** رابط تقرير تاريخيّ ظهرت سليمة | PASS |
| 12 | Console / Network | 15 نداءً · الحالات المرصودة **`[200]`** فقط · `5xx = 0` · أخطاء التطبيق `0` | PASS |
| 13 | سجلّ الخدمة | `journalctl -p err` منذ النشر = **لا مدخلات** | PASS |
| 14 | البريد والمجدولات | `/etc/reporting-api.env` غير ممسوس (`cmp` مطابق) — `Mode=Enabled` · المجدولات `true` كما هي | PASS |

### إثبات «صفر كتابة» — عدّادات الجداول قبل/بعد الدخان

| الجدول | قبل | بعد |
|---|---|---|
| `report_submissions` | 328 | **328** |
| `submission_field_values` | 3536 | **3536** |
| `notifications` | 1039 | **1039** |
| `email_outbox` | 0 | **0** |
| `email_notifications` | 742 | **742** |
| `audit_logs` | 1524 | **1524** |
| `refresh_tokens` | 3908 | **3908** |
| `report_view_grants` | 0 | **0** |

```
نداءات POST/PUT/PATCH/DELETE أثناء الدخان = []  (صفر)
PRODUCTION_SMOKE_MODE       = READ_ONLY
PRODUCTION_DATA_WRITES      = 0
PRODUCTION_EMAILS_TRIGGERED = 0
```

**إفصاح دقيق:** الكتابات الوحيدة على `reporting_prod` في هذه التذكرة كلّها هي كتابات المرحلة 9ب
عبر الواجهة الرسميّة: إنشاء الإصدار v9 وحقله ونشره، وصفّ `refresh_token` واحد ناتج عن نداء الدخول الرسميّ.
لا تسليم ولا إسناد ولا رسالة بريد ولا أيّ صفّ أعمال.

الأدلّة البصريّة: `evidence/P1-historical-report.png` · `P2-project-360.png` · `P3-project-workspace.png`
وسجلّ الشبكة المنقّح `evidence/smoke-network-log.json` (بلا رؤوس ولا أجسام ولا كوكيز).

## 5) التراجع (المرحلة 11)

### محفّزات التراجع

| # | المحفّز | القياس |
|---|---|---|
| 1 | `/health` ≠ 200 أو الخدمة `failed` | `systemctl show reporting-api` + `curl 127.0.0.1:5090/health` |
| 2 | ظهور أيّ `5xx` تطبيقيّ | `journalctl -u reporting-api -p err` |
| 3 | تعذّر فتح تقرير تاريخيّ أو Project 360 | فحص متصفّح كالمرحلة 10 |
| 4 | تغيّر `HIST_FULL_SHA256` عن `40523f7f…` بغير فعل مستخدم مشروع | استعلام خطّ الأساس |
| 5 | عطل في محرّر التسليم لمستخدمي القالب على v9 | بلاغ تشغيليّ + `journalctl` |
| 6 | إرسال بريد غير متوقَّع | `email_notifications` يتجاوز 742 بلا سبب تشغيليّ |

### الإجراء (تصاعديّ — الأخفّ أوّلًا)

1. **تراجع القالب وحده (لا يمسّ الكود):** أنشئ إصدارًا خَلَفًا v10 بالواجهة الرسميّة بنفس `ConfigJson` الخاصّ بـv8
   وانشره ⟹ يعود الإصدار الفعّال إلى شكل v1. التسليمات المرتبطة بـv9 (إن وُجدت) تبقى كما هي.
2. **تراجع الكود:**
   ```
   systemctl stop reporting-api
   rm -rf /opt/reporting/publish && mv /opt/reporting/publish.pre-r22a /opt/reporting/publish
   rm -rf /opt/reporting/reporting-frontend/dist && mv /opt/reporting/reporting-frontend/dist.pre-r22a /opt/reporting/reporting-frontend/dist
   chown -R www-data:www-data /opt/reporting/publish /opt/reporting/reporting-frontend/dist
   systemctl start reporting-api
   ```
   تحقّق: بيان الخلفيّة يعود إلى `fa2f9021…` والواجهة إلى `d3b7f88c…` والمراجعة إلى `897c9b18…` و`/health=200`.
   **بديل مكافئ** إن فُقدت مجلّدات `*.pre-r22a`: فكّ `backend-publish.tar.gz` و`frontend-dist.tar.gz` من مجلّد النسخ.
3. **استرجاع القاعدة — الملاذ الأخير فقط:** `pg_restore` من `reporting_prod.dump`
   (`3a93ef135d3c707c3fe253c369b329cfde43276b2a1fda6a8778960f9d6ebe8d`) بعد إيقاف الخدمة.
   يُفقِد كلّ عمل المستخدمين منذ `16:11:24Z` ⟹ لا يُنفَّذ إلّا بقرار صريح من مالك المنتج.

### جاهزيّة التراجع — مقيسة

```
/opt/reporting/backups/r22a-20260830T161124Z
  backend-publish.tar.gz  47,716,391 B  9e306b85c1661533595eedcce5ec77c027ca887033bcb10a5dd07109f2108eaa
  frontend-dist.tar.gz       409,201 B  efe741875938826ca1b807f5e3cf53a4a06f5048c13a9235307c41ebc49005e7
  reporting_prod.dump      1,498,610 B  3a93ef135d3c707c3fe253c369b329cfde43276b2a1fda6a8778960f9d6ebe8d
  reporting-api.env.bak (0600)
PG_RESTORE_LIST_EXIT = 0 · 520 مدخلًا · 84 TABLE DATA · 199 INDEX
نسخة حيّة إضافيّة: /opt/reporting/publish.pre-r22a  و  …/reporting-frontend/dist.pre-r22a
PROD_ROLLBACK_READY = YES
```
