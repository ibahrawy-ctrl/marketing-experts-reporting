# R22A — تفعيل بنود العمل المتعدّدة على القالب التشغيليّ الحقيقيّ وقبول وظيفيّ مقيس

**التاريخ:** 29 أغسطس 2026 · **البيئة:** TEST / Staging حصرًا · **القرار:** `REAL_OPERATIONAL_TEMPLATE_ACCEPTANCE = PASS`

الملفّات المرافقة: `ACTUAL-TEMPLATE-UAT.json` (القياسات الخام) · `FINAL-FUNCTIONAL-ACCEPTANCE.json` (معايير القبول الأربعة عشر) · `screenshots/` · `network/` · `payloads/` · `checksums/`.

---

## 1) بوّابة العزل — نُفِّذت قبل أيّ كتابة

| المطلوب حرفيًّا | المقيس | النتيجة |
|---|---|---|
| `ENVIRONMENT = TEST / Staging` | `ASPNETCORE_ENVIRONMENT=Staging` | مطابق |
| `DATABASE = reporting_test_uat` | `reporting_test_uat` | مطابق |
| `BACKEND_PORT = 5091` | `ASPNETCORE_URLS=http://127.0.0.1:5091` | مطابق |
| `PRODUCTION_PORT = 5090` | `reporting-api` · MainPID 1603719 · `reporting_prod` | مطابق ولم يُمسّ |
| `RC_PORT = 5092` | `khubara-reporting-rc` · MainPID 1592241 · `reporting_rc` | مطابق ولم يُمسّ |
| `TARGET_USER = admin@marketingexperts.local` | `74c79c4b-d403-494c-aafe-a2d63caf3965` | مطابق |

مكبحا البريد على TEST: `EmailNotifications__Mode=DryRun` و`Email__Enabled=false`.
نسخة احتياطيّة متحقَّقة قبل أيّ كتابة: `/root/db-backups/reporting_test_uat-r22a-pre-uat-20260830T210947Z.dump` — 809,257 بايت، `sha256=ce780311…`، 84 مدخل `TABLE DATA`.

## 2) إعادة تعيين حساب TEST — بالآليّة الرسميّة وبأثر محصور بحساب واحد

نُفِّذت عبر أداة صيانة **خارج المستودع تمامًا** (`/tmp/r22a/maint`) تستهلك `Reporting.Infrastructure` بنفس إعدادات Identity الإنتاجيّة، وتستعمل `GeneratePasswordResetTokenAsync` ⟶ `ResetPasswordAsync` ⟶ `UpdateSecurityStampAsync`. **لا Hash يدويّ ولا SQL مباشر ولا بريد.**

بصمة `SHA256(Id|PasswordHash|SecurityStamp|Email|IsActive|LockoutEnd)` حُسبت لكلّ الحسابات الـ47 قبل وبعد:

- `changedAccountCount = 1` · `onlyTargetChanged = true`
- `securityStamp: 2B6CD99FF4B8 ⟶ 113312ED7CED` · `passwordHash: 82EF7035F23C ⟶ 1C84041A65F6`
- `refreshTokensRevoked = 104` (إبطال الجلسات القديمة)
- محفوظ بلا تغيير: `UserId` · `Roles=[Admin]` · `Claims=0` · `IsActive=true` · `LockoutEnabled=true` · `EmailConfirmed=true` · روابط الإدارة والفريق والمسمّى والمدير
- `passwordVerifiedWithNewValue = true`

كلمة المرور لم تُطبع في أيّ سجلّ ولا لقطة ولا ملفّ داخل المستودع؛ عاشت في `/tmp/r22a/.adminpw` بصلاحيّة `600` وحُذفت عند الإغلاق.

## 3) §5 — تفعيل v5 عبر المسارات الرسميّة

القالب: `9e375ad7-8a65-46f4-849f-886d5b795bfe` — «تقرير كاتب المحتوى الأسبوعي» (**تشغيليّ حقيقيّ، لا صناعيّ**).

| الخطوة | المسار | الحالة |
|---|---|---|
| إنشاء الخليفة | `POST /api/report-templates/{t}/versions` | **200** ⇒ v5 `794260c2-77a5-4d64-8c07-9de4db8639d3` |
| تحرير حقل القسم المتكرّر | `PUT /api/report-templates/fields/ec99f382-…` | **200** |
| النشر | `POST /api/report-templates/{t}/versions/{v5}/publish` | **200** ⇒ `EFFECTIVE_VERSION = 5` |
| الإسناد | `POST /api/report-templates/{t}/assignments` | `e294bd99-7dc0-4337-a5fb-14553d3b3518` (Employee · Include) |

بنية v5 بعد التحرير: `schemaVersion = 2` · حقول مستوى المشروع = **0** · `workItems.fields = 5` (`content_type, content_goal, work_status, count, notes`) · عدد الحقول 5 وترتيبها `0,1,2,3,4`.

**الحفاظ المُثبَت:** `V4_CONFIG_UNCHANGED = true` وv4 ما زال منشورًا · v1/v2/v3 ما زالت مسودات بحالتها · لم يُحذف أيّ إصدار.

نجاح `POST versions` بحدّ ذاته إثبات لإصلاح الحارس في `36a6a5b` — قبله كان النداء نفسه يرجع **409** بسبب مسودات البذر الأدنى من المنشور.

## 4) §6 — UAT متصفّحيّ على القالب الحقيقيّ

**بيئة القياس:** Chrome مرئيّ بملفّ تعريف مؤقّت، مُقاد عبر CDP. الواجهة المقدَّمة هي **نفس بايتات `dist` المنشورة على TEST** (`DIST_SHA256=8254c74a…`)، ونداءات `/api` تمرّ عبر وكيل محلّيّ ⟵ نفق SSH ⟵ **نفس خلفيّة TEST على 5091**. تسجيل الدخول تمّ رسميًّا عبر `POST /api/auth/login` داخل المتصفّح، ولم يُستخرج أيّ Token خارج الصفحة ولم يُكتب أيّ `session.json`.

التسليم: `591c4b63-790b-4801-9e06-6213768f32de` · `2026-W36` · المشروع `P360-R21-UAT-PROJECT`.

### الحمولة المخزَّنة — الدليل الحاسم

```json
[{"answers": {}, "projectId": "9e731196-f87f-4bbf-82cd-260d06d90b56",
  "workItems": [
    {"answers": {"content_type":"Carousel","content_goal":"Sales","work_status":"Approved","count":"3","notes":"بند 1 — كاروسيل لحملة الإطلاق."}},
    {"answers": {"content_type":"Reel Script","content_goal":"Awareness","work_status":"Draft","count":"5","notes":"بند 2 — سكربت ريلز للتوعية (مُعدَّل مستقلًّا: 5)."}}]}]
```

`reportTemplateVersionId = 794260c2-…` (v5) · **مدخل مشروع واحد** · **بندا عمل اثنان**.

### معايير القبول الأربعة عشر — 14/14 PASS

| المعيار | المقيس |
|---|---|
| `PROJECT_ENTRY_COUNT` | 1 |
| `WORK_ITEM_COUNT` | 2 |
| `PROJECT_ID_OCCURRENCES_AS_ENTRY` | 1 |
| `ADD_WORK_ITEM_BUTTON_VISIBLE` | «+ إضافة بند عمل» ظاهر |
| `SECOND_ITEM_ADDED_WITHOUT_DUPLICATING_PROJECT` | `addProjectButtons=1` · `deleteProjectButtons=1` |
| `BOTH_ITEMS_VISIBLE_TOGETHER` | «بند عمل 1» و«بند عمل 2» في لقطة واحدة |
| `SAVE_AND_RELOAD_PRESERVES_BOTH` | `PUT …/values = 200` ثمّ إعادة تحميل كاملة بكلّ القيم |
| `INDEPENDENT_EDIT_OF_EACH_ITEM` | البند 2: `2 ⟶ 5` والبند 1 كما هو حرفيًّا |
| `SUBMIT_WORKS` | `POST …/submit = 200` · الحالة `Closed` |
| `PROJECT360_SHOWS_BOTH_ITEMS` | الشريحة المقيَّدة تعرض «بنود العمل» ببندَيها |
| `NO_CROSS_PROJECT_LEAKAGE` | `404 project.not_found` من مشروع آخر |
| `ZERO_UNEXPECTED_CONSOLE_ERRORS` | 0 |
| `ZERO_5XX` | 0 من 24 نداءً (و0 من فئة 4xx) |
| `UNIQUE_SEQUENTIAL_SCREENSHOTS` | 10 ملفّات · 10 بصمات مختلفة |

### منع التسريب بين المشروعات

- `GET /api/projects/{المرتبط}/reports` ⇒ **200** ويحوي التسليم (صفّ واحد).
- `GET /api/projects/{المرتبط}/reports/{التسليم}` ⇒ **200** ويحوي `Carousel` و`Reel Script`.
- `GET /api/projects/{آخر}/reports/{التسليم}` ⇒ **404** `project.not_found` — التزامًا بسياسة «المرفوض يُرجِع 404 لا 403».
- `GET /api/projects/{آخر}/reports` ⇒ لا يحوي التسليم.
- الواجهة على المشروع الآخر: «التقرير غير متاح ضمن هذا المشروع».

## 5) النتيجة الجوهريّة — الحاجب كان بيانات لا شيفرة

خلفيّة TEST **لم تتغيّر بايتًا واحدًا** خلال §5 و§6:

| القياس | قبل | بعد |
|---|---|---|
| `InformationalVersion` | `1.0.0+36a6a5b5d8ff285f048c1f4b91c9a1f4db4d7f7f` | نفسه |
| بصمة كلّ ملفّات DLL (37) | `ceb6ba56ab1b8b7d` | نفسها |
| `khubara-reporting-test` MainPID | 1685370 | 1685370 |
| عدد الهجرات | 46 | 46 |
| `/health` | `{"status":"ok",…}` | نفسه |

ومع ذلك ظهرت الميزة فور نشر v5 ⟹ **`BACKEND_REDEPLOY_REQUIRED = NO`** و**`MIGRATION_APPLIED = NO`**، وهو تأكيد قاطع للتشخيص المسجَّل في `REAL-TEMPLATE-ROOT-CAUSE.md`.

## 6) حدود ثابتة — محترمة بالكامل

`RC_TOUCHED = NO` · `PRODUCTION_TOUCHED = NO` · `ORIGIN_MAIN_TOUCHED = NO` · `FORCE_PUSH = NO` · `HISTORICAL_REPORTS_MUTATED = NO` · `OLD_TEMPLATE_DRAFTS_DELETED = NO`.

معرّفا العمليّة لخدمتَي الإنتاج (1603719) وRC (1592241) مطابقان لما قيس في بوّابة العزل قبل بدء العمل ⇒ لم تُعَد تشغيلهما ولم تُمسّا.

## 7) ما لم يُستعمل دليلًا

القالب الصناعيّ `aed0016c-…` ونتيجته السابقة **57/57** لم يُستشهد بهما هنا إطلاقًا. كذلك البوّابات الآليّة 9/9 مذكورة سياقًا لا قبولًا: `AUTOMATED_TESTS_PASS ≠ FUNCTIONAL_ACCEPTANCE`.

عيب خطّ الأساس `BASELINE-UNIFIED-STATUS-CURRENT-CYCLE` (8 إخفاقات) يبقى مستقلًّا وغير حاجب ولم يُستعمل عذرًا.
