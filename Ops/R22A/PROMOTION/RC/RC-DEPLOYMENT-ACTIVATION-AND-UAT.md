# RC — النشر والتفعيل وقبول UAT التشغيليّ (R22A)

**البيئة:** `rc-report.emarketingacademy.net` · خدمة `khubara-reporting-rc` · منفذ `5092` · قاعدة `reporting_rc`
**المصدر:** فرع `release/r22a-from-897c9b18` رأسه `4b8902eec9b67513d115dbc49c20af0dd62de8b8`

## 1) النشر (المرحلة 4أ) — بايتات محليّة مطابقة، بلا إعادة بناء على الخادم

| مفتاح | القيمة |
|---|---|
| `RC_INFORMATIONAL_VERSION` | `1.0.0+4b8902eec9b67513d115dbc49c20af0dd62de8b8` |
| `Reporting.Api.dll` (محلّيّ = منشور) | `1ed95cfab17d0467b0e259a32e4bd76c9aa9da593dcd6a5ff6639a157706bad7` |
| بيان الخلفيّة (86 ملفًّا) | `e91e655b21aabd6b63ecf7ab1824eb397e872aabfd4eecc74e18162060d4aff4` |
| بيان الواجهة (7 ملفّات) | `eb95e6a13538984ad4cf0feb96999304369d8dad011c98337c0ad68d44e9be3f` |
| `MainPID` قبل / بعد | `1592241` → `1729280` · `NRestarts=0` |
| `/health` | `200` · `{"status":"ok","service":"reporting-api"}` |
| عدد الهجرات قبل/بعد | `47` → `47` ⟹ `RC_MIGRATION_APPLIED = NO` |
| فهرس GIN | `pg_indexes = 0` ⟹ غائب كما أمر القرار 2 |

البيانان المحسوبان على الخادم بنفس صيغة الأمر طابقا المحلّيّين **بايتًا**
(`e91e655b…` و`eb95e6a1…`) ⇒ لم يُبنَ شيء على الخادم.

### عزل البيئات الثلاث — مقيس بأختام الإقلاع

| الخدمة | `MainPID` | `ActiveEnterTimestamp` | `/health` |
|---|---|---|---|
| `reporting-api` (إنتاج · 5090) | `1603719` | `2026-08-26 19:26:00Z` | 200 |
| `khubara-reporting-test` (5091) | `1723290` | `2026-08-30 14:57:07Z` | 200 |
| `khubara-reporting-rc` (5092) | `1729280` | `2026-08-30 15:40:40Z` | 200 |

نافذة النشر `15:40:37Z–15:40:40Z` ⟹ **الإنتاج وTEST لم يُمسّا** (ختماهما أقدم من النافذة).

## 2) تفعيل القالب (المرحلة 4ب) — بالواجهة الرسميّة حصرًا، بلا SQL

القالب على RC اكتُشِف **بالاسم لا بمعرّف TEST**: `5e6ad325-b26c-4fd6-b4f8-d415dae44c89`
«تقرير كاتب المحتوى الأسبوعي» — وهو **معرّف مختلف تمامًا** عن معرّف TEST.

| # | النداء الرسميّ | النتيجة |
|---|---|---|
| 1 | `POST /api/report-templates/{id}/versions` | `200` · مسودّة **v9** `e8bba9fc-aa72-4851-81e7-0f7ee67a9ea5` مستنسَخة من v8 المنشور |
| 2 | `PUT /api/report-templates/fields/{fieldId}` | `200` · تحديث `ConfigJson` للقسم المتكرّر |
| 3 | `POST /api/report-templates/versions/{id}/publish` | `200` · v9 منشور |
| 4 | `GET /api/report-templates/{id}/preview` | `200` · `EFFECTIVE_VERSION = 9` |

### إثبات الشكل المطلوب حرفيًّا (مقيس من `jsonb` في القاعدة)

```
V9_SCHEMAVER=2  projfields=0  wi_fields=5  minItems=1  maxItems=0  uniqueBy=0
FIELDS_MATCH_V8=true      ← مساواة jsonb: الحقول الخمسة نُقِلت حرفيًّا بلا أيّ تحوير
```
الخصائص الأصليّة محفوظة: `projectRequired: true` · `minProjects: 1` · `maxProjects: 0`.
الحقول الخمسة بمفاتيحها وأنواعها وخياراتها و`catalogDomain` كما هي:
`content_type` · `content_goal` · `work_status` · `count` · `notes`.

### عدم المساس بالتاريخ

```
الإصدارات v1..v9 كلّها قائمة — لم يُحذف ولا يُعدَّل أيّ إصدار ولا مسودّة قديمة
V8_CFG_MD5=554bd2e116c7eff6c2e44a60aa261bb2  len=1105  schemaVersion=ABSENT  projfields=5
توزيع ارتباط التسليمات:  v5 → 2   ·   v9 → 1 (تسليم UAT الجديد وحده)
```
⟹ نشر v9 **لم يُعِد ربط أيّ تقرير تاريخيّ**.

## 3) UAT التشغيليّ (المرحلة 5) — 14 خطوة بحساب موظّف على المتصفّح

**الفاعل:** `r22a-rc-uat@khubara.local` (`cd065768-592f-4103-9514-c2d726907e48`) — دوره **`Employee` فقط**،
أُنشئ بالواجهة الرسميّة `POST /api/directory/users` (لا تعديل `PasswordHash` يدويًّا، ولا مسّ أيّ حساب قائم).
**التسليم الوحيد:** `a7d51298-5461-4ac1-bc41-5d7c45dd7181` · **المشروع:** `R22A-UAT-MAIN` `64210828-…`.

| # | الخطوة | الدليل المقيس | النتيجة |
|---|---|---|---|
| 1 | مسودّة على القالب الحقيقيّ | `?open=a7d51298…` · «حد 1–∞» ظاهر | PASS |
| 2 | إضافة مشروع مرّة واحدة | `حذف المشروع` = 1 | PASS |
| 3 | البند الأوّل | Carousel · Awareness · Approved · 5 | PASS |
| 4 | بند ثانٍ في البطاقة نفسها | Blog · Sales · Draft · 3 | PASS |
| 5 | حفظ | `POST` 200 | PASS |
| 6 | Reload كامل | إعادة تحميل الصفحة بالكامل | PASS |
| 7 | بقاء مشروع واحد وبندين | `projects=1` · `items=2` | PASS |
| 8 | تعديل `count` للبند الثاني فقط | 3 → 9 | PASS |
| 9 | حفظ + Reload | `POST` 200 ثمّ إعادة تحميل | PASS |
| 10 | ثبات البند الأوّل | `nums=[5,9]` · ملاحظات البندين سليمة | PASS |
| 11 | إرسال للاعتماد | «✅ تم إرسال التقرير للاعتماد» · `Status=Submitted` | PASS |
| 12 | فتح التقرير من المشروع الصحيح | `GET 200 /api/projects/64210828…/reports/a7d51298…` · البندان ظاهران | PASS |
| 13 | Project 360 يُظهر البندين | `GET 200 /api/projects/…/reports` اكتشف التقرير من **الربط المتداخل**؛ الفتح بقي داخل نطاق المشروع وأظهر «بند عمل 1» و«بند عمل 2» | PASS |
| 14 | نفس المعرّف من مشروعين آخرين | `GET 404` ×2 · **صفر تسريب** لأيّ نصّ من البندين | PASS |

**ضابط إضافيّ (ليس ضمن الـ14):** نفس التقرير **لا يُكتشَف** تحت `R22A-UAT-OTHER-1` (`report_links = 0`).

### المؤشّرات الإلزاميّة

```
RC_ACTOR_IS_EMPLOYEE             = PASS   (ACTOR_ROLES = Employee فقط)
RC_ACTOR_IS_ADMIN                = NO
RC_SINGLE_SUBMISSION_END_TO_END  = PASS
RC_PROJECT_ENTRY_COUNT           = 1
RC_WORK_ITEM_COUNT               = 2      (jsonb_array_length(workItems) = 2)
RC_SAVE_RELOAD                   = PASS
RC_INDEPENDENT_EDIT              = PASS
RC_SUBMIT                        = PASS
RC_PROJECT360_SHOWS_BOTH         = PASS
RC_CROSS_PROJECT_404             = PASS
RC_APPLICATION_CONSOLE_ERRORS    = 0
RC_SERVER_5XX                    = 0
RC_HISTORICAL_REPORTS_MUTATED    = 0
RC_ACTUAL_EMAILS_SENT            = 0
```

**تفسير `RC_APPLICATION_CONSOLE_ERRORS = 0`:** أخطاء التطبيق صفر في كلّ الجولات.
الرسالتان الوحيدتان في وحدة التحكّم كانتا إشعارَي المتصفّح التلقائيَّين بالرمز **404 المقصود**
في الخطوة 14 — أي **سلوك الحماية نفسه** لا عطل تطبيقيّ.

### شواهد قاعدة البيانات بعد UAT

```
TOTAL_SUBMISSIONS = 40   (39 سابقة + 1 تسليم UAT)
HISTORICAL_MUTATED = 0   (لا صفّ سابق تغيّر بعد 15:30Z)
EMAIL_OUTBOX_TOTAL = 0   ·  EMAIL_SENT_TODAY = 0
MIGRATIONS = 47          ·  GIN_INDEX_PRESENT = 0
```
`EmailNotifications__Mode = DryRun` و`Email__Enabled=false` و`BackgroundJobs__Enabled=false`
⟹ `RC_ACTUAL_EMAILS_SENT = 0` **بنيويًّا** لا احتماليًّا.

## 4) مسح الأسرار

سُجِّل كلّ نداء API (145 نداءً، الحالات المرصودة `200` و`404` فقط) في `evidence/network-log.json`
**بلا رؤوس ولا أجسام ولا كوكيز**. فحص الأدلّة كلّها بحثًا عن كلمات المرور الثلاث المستعملة
(المدير المؤقّت · الموظّف · Basic-Auth) ⟹ `0` تطابق. `RC_SECRET_SCAN = PASS`.

## 5) بوابة المرحلة 6

```
R22A_MINIMAL_RELEASE_FEASIBLE     = YES
RC_ISOLATION_GATE                 = PASS
RC_BACKUP_GATE                    = PASS
RC_CODE_DEPLOYMENT                = PASS
RC_HEALTH_AFTER_DEPLOY            = PASS
RC_MIGRATION_COUNT_UNCHANGED      = YES
RC_TEMPLATE_ACTIVATION            = PASS
RC_FUNCTIONAL_ACCEPTANCE          = PASS
RC_OPERATIONAL_ACCEPTANCE         = PASS
RC_HISTORICAL_DATA_UNCHANGED      = YES
RC_SECURITY_ISOLATION             = PASS
RC_SECRET_SCAN                    = PASS
RC_ROLLBACK_READY                 = YES
PRODUCTION_PROMOTION_GO           = YES
```

## 6) إغلاق الحسابات المؤقّتة على RC (بعد اكتمال الترقية)

| الحساب | الإجراء الرسميّ | الحالة النهائيّة |
|---|---|---|
| `r22a-rc-uat@khubara.local` `cd065768…` | `PUT /api/directory/users/{id}` بـ`isActive=false` ⟹ `200` | `IsActive=false` · تسجيل الدخول **403** |
| `r22a-rc-admin@khubara.local` `0ce50482…` | التعطيل الذاتيّ مرفوض بحارس الخدمة (`user.self_deactivate.conflict` · 409) ⟹ `POST /users/{id}/reset-password` بقيمة عشوائيّة **غير محفوظة** (تُبطِل رموز التجديد ذرّيًّا) | تسجيل الدخول بالكلمة السابقة **401** |

`DELETE /api/directory/users/{id}` **لم يُستعمل عمدًا**: حذف صلب يُتلف أدلّة UAT ومراجع السجلّ الملحقيّ
(`DirectoryService.cs:782` يحذف رموز التجديد ويُفرِغ مراجع قيادة الفرق/الإدارات).

```
RC_TEMP_EMPLOYEE_DEACTIVATED = YES
RC_TEMP_ADMIN_DEACTIVATED    = NO   (حارس بنيويّ: لا تعطيل ذاتيّ للمدير)
RC_TEMP_ADMIN_LOGIN_REVOKED  = YES
RC_UAT_EVIDENCE_PRESERVED    = YES
RC_HEALTH_AFTER_CLEANUP      = 200
```
