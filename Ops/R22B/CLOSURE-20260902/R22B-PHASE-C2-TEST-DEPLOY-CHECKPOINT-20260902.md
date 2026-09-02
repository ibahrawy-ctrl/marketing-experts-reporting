# R22B — نقطة تفتيش المرحلة C2: النشر على TEST وإثبات أثره

**التاريخ:** 2 سبتمبر 2026 · **النطاق:** TEST وحدها. `RC_PROMOTION=FORBIDDEN` · `PROD_PROMOTION=FORBIDDEN` — ولم يُمَسّ أيٌّ منهما.

---

## 1) هويّة ما نُشر

| البند | القيمة |
|---|---|
| `BASE_SHA` | `865c0edde96163325d00af248d0a3b8df96878dd` |
| `CANDIDATE_SHA` | `dc47c0beda65b72bc7c6d56eb3c6936162f06358` |
| الفرع | `fix/r22b-reporting-visual-operational-closure-20260902` (لم يُدفَع بعدُ عند كتابة هذه النقطة) |
| شجرة العمل | `/private/tmp/r22b-closure-wUIoo4tt/repo` (معزولة · `SHARED_WORKTREE_WRITE=NO`) |

### تصحيح حاكم لمعطيات بيئة TEST المخزَّنة
الذاكرة كانت تصف TEST بمعطيات الإنتاج. القِيَم الصحيحة **مقروءةً من الخادم الحيّ**:

| | TEST (الصحيح) | الإنتاج (للتمييز) |
|---|---|---|
| الخدمة | `khubara-reporting-test.service` | `reporting-api.service` |
| ملفّ البيئة | `/etc/khubara-reporting-test.env` | `/etc/reporting-api.env` |
| مجلّد التشغيل | `/opt/reporting-test/publish` | — |
| الواجهة | `/opt/reporting-test/frontend/dist` | — |
| المنفذ | `http://127.0.0.1:5091` | `5090` |
| القاعدة | `reporting_test_uat` | — |

خدمة RC منفصلة ثالثة: `khubara-reporting-rc.service`. **الخلط بين هذه الثلاث هو أخطر ما في نشرٍ يدويّ**، ولذلك يُثبَّت الجدول هنا لا في الذاكرة وحدها.

---

## 2) النسخ الاحتياطيّة قبل أيّ كتابة (`TS = 20260902T135217Z`)

| النسخة | الموضع |
|---|---|
| حزمة الخادم | `/opt/reporting-test/publish-backup-20260902T135217Z` |
| حزمة الواجهة | `/opt/reporting-test/frontend/dist-backup-20260902T135217Z` |
| قاعدة البيانات | `/root/db-backups/reporting_test_uat-r22bclosure-20260902T135217Z.dump` (689,120 بايت) |

> `pg_dump` يعمل بمستخدم `postgres` الذي لا يملك الكتابة في `/root` ⟹ التفريغ إلى `/tmp` ثمّ `mv`. تسجيلها هنا يمنع تكرار الاصطدام.

---

## 3) الأثر: خطّ الأساس مقابل المرشَّح

| | قبل | بعد |
|---|---|---|
| بصمة SourceLink للخادم | `1.0.0+1db114db…` | `1.0.0+dc47c0be…` |
| بصمة بيان الأثر (manifest sha256) | `17e300ad…` | `bb487f62620b4dc0a5151b9a0f9e4c8ee8aa7b879dbf13d07f2d3770faee6f125b` |
| أصل حزمة الواجهة | `index-BvE5cDGO.js` | حزمة جديدة من بناء `dc47c0b` |

**بيان الأثر المنشور == بيان الأثر المبنيّ محلّيًّا، تطابقًا حرفيًّا** — أي أنّ ما يعمل على TEST هو بعينه ما قِيسَت عليه بوّابات المرحلة B، لا بناءً آخر.

### فحص الحزمة المتصفَّحة
- `"test.emarketingacademy.net/api"` — **مرّة واحدة**
- `"localhost:5090"` — **صفر**

هذا هو الحارس ضدّ سقوط `VITE_API_BASE_URL` إلى احتياطيّ `api.ts`؛ بدونه تظهر الواجهة سليمة ثمّ تعطي `Network Error` في المتصفّح وحده.

**الخدمة:** `khubara-reporting-test.service` نشطة وتستمع على `http://127.0.0.1:5091` بعد إعادة التشغيل.

---

## 4) إثبات VIS-05 **على قاعدة TEST نفسها** (لا على قاعدة اختبار)

الترقية `UpgradeSeoArticlesTemplateAsync` تعمل عند الإقلاع. حالة إصدارات «تقرير متابعة مقالات SEO الأسبوعي» بعد إقلاع `dc47c0b`:

| الإصدار | منشور؟ | `catalogDomain":"work_status` | `delivery_date` |
|---|---|---|---|
| 1 | ✗ | ✗ | ✗ |
| 2 | ✗ | ✗ | ✓ |
| **3** | **✓** | **✓** | **✓** |

- **إصدار منشور واحد بالضبط**، وهو الأحدث، وهو المحكوم. الإصداران 1 و2 باقيان مقروءَين (لا حذف · لا هجرة · 0 كتابة SQL خام).
- بنية قسم المشاريع في الإصدار 3 (ثمانية حقول): `article_title` ShortText مطلوب · `keyword` ShortText مطلوب · **`work_status` Select مسنود بـ`catalogDomain: "work_status"` مطلوب** · `reviewer` ShortText · **`delivery_date` Date مطلوب** · `published_url` ShortText · `word_count` Number · `notes` LongText.
- **الخيارات لم تُخترَع**: `["Draft","Revision","Approved","Published"]` هي حرفيًّا قيم `execution_taxonomy_values` النشطة لنطاق `work_status` على TEST مرتَّبةً بـ`SortOrder` (10/20/30/40). قرار «استعارة `work_status`» قرار مالك المنتج، ولم يُستحدَث نطاق.
- **لا جدول حرّ باقٍ داخل قسم المشاريع.** الجدولان الباقيان في القالب (`المتأخرة` و`خطة الأسبوع القادم`) خارج القسم المتكرّر تحت ترويسة «📋 الجداول العامة»، وليسا موضع العيب: العيب كان تسجيل **حالة المقال وتاريخ تسليمه** كأعمدة نصّ حرّ، وهذان انتقلا إلى حقلين مكتوبَي النوع.

### ملاحظة بيانات (خارج النطاق، لا تُصلَح هنا)
قيم `NameAr` لنطاق `work_status` على TEST مكتوبة بالإنجليزيّة (`Draft` … `Published`). هذه **جودة بيانات في بذرة TEST لا عيب كود**؛ الترقية تنقل الكتالوج كما هو أمانةً، وأيّ تعديل عليه كتابةٌ على بيانات حيّة بلا تصريح.

---

`PHASE_C2_STATUS = COMPLETE` · `TEST_DEPLOY = dc47c0be` · `MANIFEST_MATCH = EXACT` · `MIGRATIONS_RUN = 0` · `RAW_SQL_WRITE = 0` · `RC_TOUCHED = NO` · `PROD_TOUCHED = NO`
