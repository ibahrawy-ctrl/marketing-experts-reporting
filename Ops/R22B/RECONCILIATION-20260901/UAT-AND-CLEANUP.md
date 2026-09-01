# R22B — مصالحة الأسطر المتعدّدة: قبول UAT على TEST وتنظيفه (§5 · §8 · §9 · §10)

**التاريخ:** 1 سبتمبر 2026 · **البيئة:** TEST وحدها — `test.emarketingacademy.net`
**النشر والبوّابات المحلّيّة:** `DEPLOYMENT-AND-GATES.md` بنفس المجلّد (`CANDIDATE_SHA = 1db114db…`).
**غير مصرَّح به ولم يقع:** `MERGE_TO_DEVELOP` · `RC_PROMOTION` · `PROD_PROMOTION`.

---

## 1. كيف قِيست الواجهة على حزمة TEST المنشورة

نطاق TEST محميّ بـ`auth_basic` وتجزئة `htpasswd` غير قابلة للاسترجاع وتغييرها محظور. لذلك — وبنفس النمط
المعتمَد سابقًا في `Ops/UAT/P360-NAVFIX-20260826` — قِيست الواجهة على **نفس بايتات `dist` المنشورة**:

```
DIST_SOURCE            = rsync من /var/www/frontend-test  →  /tmp/r22r-dist
LOCAL_MANIFEST_SHA     = 17e300adee3460316bdabf484948a11bc3eded89058c46849f5af534ae3d632b
DEPLOYED_MANIFEST_SHA  = 17e300adee3460316bdabf484948a11bc3eded89058c46849f5af534ae3d632b   ⟹ متطابقان
SERVED_AT              = http://127.0.0.1:4420   (خادم ثابت + سقوط SPA)
API_ROUTING            = page.route('https://test.emarketingacademy.net/api/**') → نفق SSH 15091 → 5091
```

المنطق والبيانات من خادم TEST الحيّ حصرًا؛ المحلّيّ هو تقديم الملفّات الساكنة لا أكثر. `route.fulfill`
لا `route.continue` لأنّ الأخير يكسر CORS. الأداة: `reporting-frontend/e2e/r22r-multiline-surfaces.mjs`.

**القياس ليس بوجود صنف CSS** بل بعدد صناديق الأسطر التي رسمها المتصفّح فعليًّا
(`Range.getClientRects`) مع القيمة المحسوبة لـ`white-space`.

---

## 2. عقد الأسطح الخمسة — النتيجة المقيسة

| السطح | الدليل المقيس |
|---|---|
| `APPROVAL_TEXTAREA` | `tagName=TEXTAREA · rows=4 · resize=vertical` · الكتابة بثلاثة أسطر بضغط `Enter` أنتجت `newlineCount=2` و`enterPreserved=true` والنموذج لم يُرسَل |
| `SUBMISSION_DETAIL_AND_HISTORY` | `whiteSpace=pre-wrap · overflowWrap=break-word` · **3 قمم رسم متمايزة** `tops=[1806,1826,1846]`، `height=60px = 3×20px` |
| `NOTIFICATION_BELL` | نفس النصّ الثلاثيّ · `whiteSpace=pre-wrap · overflowWrap=break-word` |
| `EMAIL_HTML` | معاينة رسميّة بلا إرسال: `brCount=2` لجسم من ثلاثة أسطر · `&lt;script&gt;` حاضر و`<script>` غائب |
| `ADMIN_ARCHIVE` | خليّة تعليق في «لقطة سير العمل» لعنصر محذوف إداريًّا · `whiteSpace=pre-wrap · overflowWrap=break-word` · ثلاثة أسطر |

### الأعلام السبعة
```
INPUT_MULTILINE_PRESERVED        = YES   (enterPreserved=true · newlineCount=2)
API_MULTILINE_PRESERVED          = YES   (حمولة POST /return تحمل \n مهرَّبة)
DATABASE_MULTILINE_PRESERVED     = YES   (كلّ التعليقات المحفوظة = 3 أسطر بقياس string_to_array)
DETAIL_VIEW_MULTILINE_RENDERED   = YES
NOTIFICATION_MULTILINE_RENDERED  = YES
EMAIL_MULTILINE_RENDERED         = YES
ADMIN_ARCHIVE_MULTILINE_RENDERED = YES
```

### لماذا المعاينة الرسميّة هي دليل البريد
`NotificationService.EnqueueEmailsAsync` يبدأ بـ`if (!_email.Enabled) return;`، وعلى TEST
`Email__Enabled=false` و`EmailNotifications__Mode=DryRun` ⟹ `email_outbox` **صفر صفًّا بالتصميم**،
فلا يصلح دليلًا. المسار الرسميّ البديل بلا إرسال خارجيّ هو
`POST /api/email-control/templates/{key}/preview`، وهو يستدعي **نفس** `EmailHtml.Build` على الثنائيّة
المنشورة على TEST. النتيجة أعلاه مأخوذة من هذا المسار على الخادم الحيّ.

---

## 3. رحلة الـAPI الكاملة — خمسة قوالب حقيقيّة

الفترة `2026-W36`. كلّ سطر أدناه رمز حالة حيّ من `evidence/api-uat-evidence.json`:

```
eligibility-{slug}              assigned=1 own=True      (الاستحقاق عبر JobRoleId)
create-{slug}                   200
save-values-{slug}              200
submit-{slug}                   200  Submitted
return-{slug}                   200  Returned            (تعليق إعادة من ثلاثة أسطر)
api-reread-{slug}               200  lines=3 exact=True
bell-{slug}                     200  lines=3 full=True
independent-reader-{slug}       200  exact=True          (مدير خارج سلسلة الاعتماد، قراءة مسموحة)
resubmit-after-return           200  Submitted
approve-with-injection-comment  200  Closed
approve-api-reread              200  exact=True
admin-soft-delete               200
archive-detail                  200  steps=2 approveExact=True returnExact=True
email-preview                   200  br=2 scriptEncoded=True
```

معرّفات التسليمات: محتوى `4ab22892…` · تصميم `e5ef7c88…` · فيديو `a1a5129f…` · مديرشن `4ca2eea2…` · SEO `b26e1244…`.

تعليق الاعتماد حمل حقنًا متعمّدًا للتحقّق من ترتيب الترميز:
`اعتماد/س2: <script>alert('xss')</script> & "اقتباس"` — عاد مطابقًا حرفيًّا من الـAPI، ومرمَّزًا في HTML البريد.

### القياس البارد على قاعدة TEST (قراءة فقط)
```
4ab22892…|1|Returned|112 حرفًا|3 أسطر     4ab22892…|2|Approved|121|3
4ca2eea2…|1|Returned|105|3                a1a5129f…|1|Returned|106|3
b26e1244…|1|Returned|108|3                e5ef7c88…|1|Returned| 98|3
notifications = 5 صفوف، 5 منها تحتوي chr(10)
email_outbox  = 0 صفّ  (متوقَّع — القناة معطّلة على TEST)
```

### حدود التصنيف كما نصّ عليها التكليف
```
NEW_PERSISTED_COLD_READ         = DONE
PRODUCTION_HISTORICAL_COMMENTS  = NOT_TESTED_NOT_AUTHORIZED
ACCOUNT_MANAGER_MULTILINE       = N/A_BY_DESIGN   (مدير الحساب خارج سلسلة الاعتماد)
```

---

## 4. §10 — تنظيف TEST بعد UAT

الأداة `Ops/R22B/tools/r22r-uat-cleanup.py` بطورين: `dry` يقيس ويُثبت ثمّ يقف، و`apply` ينفّذ الخطّة
المُثبَتة نفسها. **لا SQL خام للكتابة · لا حذف صلب · لا مساس بالأدمن ولا بأيّ كيان سابق للتزويد.**

### البرهان قبل أيّ كتابة
البصمة الملتقَطة **قبل** التزويد (`evidence/uat-fixture-state.json`) قورنت بالحالة الحيّة:

| المورد | قبل | الآن | المُستحدَث | يطابق سجلّ التزويد | السابق سليم |
|---|---|---|---|---|---|
| المستخدمون | 33 | 40 | 7 | ✅ | ✅ |
| العملاء | 10 | 11 | 1 | ✅ | ✅ |
| المشروعات | 18 | 23 | 5 | ✅ | ✅ |

وبرهان ثانٍ مستقلّ: كلّ كيان مستهدَف يحمل بصمة `R22R` نصّيًّا في بريده أو اسمه. التسليمات لم تُؤخذ من
قائمة عامّة (قائمة الأدمن مُنطاقة بالمُطالِب فتعود فارغة) بل من سجلّ الرحلة، مع **تحقّق ملكيّة فرديّ**
لكلّ معرّف: `submitterId ∈ حسابات التجهيزات`. أيّ فارق كان سيوقف السكربت قبل أوّل كتابة.

### ما نُفِّذ — كلّه عبر الـAPI الرسميّة (52 نداءً، كلّها 2xx)
| العمليّة | المسار الرسميّ | العدد |
|---|---|---|
| استعادة القوالب إلى حالتها المسجَّلة | `PUT /api/report-templates/{id}` | 5 |
| حذف إداريّ **ناعم** للتسليمات | `POST /api/submissions/{id}/admin-delete` | 4 (+1 محذوف سلفًا) |
| أرشفة المشروعات | `POST /api/projects/{id}/archive` | 5 |
| أرشفة العميل | `POST /api/clients/{id}/archive` | 1 |
| إبطال رموز التجديد | `POST /api/directory/users/{id}/reset-password` | 7 |
| تعطيل الحسابات | `PUT /api/directory/users/{id}` بـ`isActive:false` | 7 |
| أرشفة المسمّيات | `POST /api/directory/job-roles/{id}/archive` | 5 |
| تعطيل الفريق ثمّ الإدارة | `PUT …/teams/{id}` · `PUT …/departments/{id}` | 2 |

**لماذا لم يُستعمل `DELETE /api/directory/users/{id}`:** قراءة `DirectoryService.DeleteUserAsync` تُظهر أنّه
ينتهي بـ`_users.DeleteAsync(user)` — **حذف صلب** يخالف §10. وكذلك `DELETE` للفريق والإدارة
(`_db.Teams.Remove` / `_db.Departments.Remove`). فاستُعمل التعطيل والأرشفة حصرًا.

**الأثر غير القابل للعكس (مُقرّ به):** إبطال رموز التجديد الجماعيّ منطقيًّا حصريّ داخل
`ResetPasswordAsync`/`ChangePasswordAsync`، وهو يوجب كلمة مرور جديدة. وُلِّدت كلمة عشوائيّة في الذاكرة
ولم تُكتَب ولم تُطبَع ⟹ الحسابات السبعة **معطّلة ومفقودة كلمة المرور نهائيًّا**، وهو الأثر الذي نصّ
التكليف على تسجيله لا على محاولة عكسه.

### التحقّق البعديّ (قراءة فقط على قاعدة TEST)
```
حسابات R22R          = 7 صفوف موجودة · IsActive=false ×7 · محذوفة صلبًا = 0
ACTIVE_REFRESH_TOKENS = 0   (كان 20 قبل التنظيف؛ الصفوف العشرون باقية ومُبطَلة لا محذوفة)
المشروعات الخمسة      = Closed ×5
العميل                = Closed
الفريق والإدارة       = IsActive=false
المسمّيات الخمسة       = IsActive=false ×5
التسليمات الخمسة      = IsDeleted=true ×5 (الصفوف باقية — حذف ناعم)
القوالب الخمسة        = JobRoleId IS NULL ×5  ⟹ عادت إلى jobRoleIdBefore المسجَّل
المستخدمون/العملاء/المشروعات السابقة للتزويد = سليمة بالكامل
report_template_versions = 45/58 · max(UpdatedAtUtc) = 2026-08-31 22:36:07.986392+00 (لم يتغيّر)
```
⟹ استعادة القوالب لم تمسّ جدول الإصدارات إطلاقًا.

---

## 5. §9 — ما لا يجوز إعلانه

```
MULTILINE_COMMENT_TEST_GATE   = PASS          ← الوحيد المُعلَن
R22B_VIS_03 / VIS_04 / VIS_05 = OPEN
R22B_FULL_VISUAL_GATE         = NOT_REEVALUATED
R22B_OPERATIONAL_ACCEPTANCE   = NO
ROOT_CAUSE_REMEDIATED         = NO            ← يبقى حتّى مراجعة المالك
```

`ROOT_CAUSE_REMEDIATED` يبقى `NO` لأنّ السبب الجذريّ للانحدار ليس نقص `pre-wrap` وحده بل أنّ
`00b1204`/`986cc3b` **ليسا سلفًا لـ`develop`**؛ والمرشَّح `1db114db` يزيل هذا الافتراق تقنيًّا لكنّ
**الدمج إلى `develop` غير مصرَّح به** ⟹ خطر تكرار الدهس قائم حتّى يُصرَّح بالدمج ويقع.

---

## 6. التسليم النهائيّ (§11)

```
RECONCILIATION_BASE_SHA          = cd09b67a0924a9932a1c5411a75d5dfb848f130d
RECONCILIATION_CANDIDATE_SHA     = 1db114db32ecf8e5ff4e6625d72601520899287d
FILES_CHANGED                    = 13
MIGRATIONS_ADDED                 = 0
EF_MODEL_SNAPSHOT_DIFF           = NONE
IDEMPOTENCY_TESTS                = 9/9 PASS
MULTILINE_TESTS                  = 26/26 PASS  (14 تعليق + 4 أرشيف + 8 بريد)
NEGATIVE_CONTROLS                = 4/4 التقطت العيب واسترُجعت بإثبات cmp/sha256
TEST_DEPLOYMENT_SHA              = 1db114db32ecf8e5ff4e6625d72601520899287d
DEPLOYED_MANIFEST_SHA            = 17e300adee3460316bdabf484948a11bc3eded89058c46849f5af534ae3d632b
FIVE_SURFACE_UAT                 = PASS (5/5)
TEST_CLEANUP                     = DONE_NO_HARD_DELETE_NO_RAW_SQL
ACTIVE_REFRESH_TOKENS_AFTER      = 0
MULTILINE_COMMENT_TEST_GATE      = PASS
ROOT_CAUSE_REMEDIATED            = NO
MERGE_TO_DEVELOP / RC / PROD     = NOT_AUTHORIZED — لم يقع
```

**دليل عدم المساس بالبيئتين الأخريين:** الإنتاج `reporting-api` على 5090 وبصمة DLL `248ef468…` لم تتغيّر
· RC على 5092 لم يُرسَل إليه شيء.
