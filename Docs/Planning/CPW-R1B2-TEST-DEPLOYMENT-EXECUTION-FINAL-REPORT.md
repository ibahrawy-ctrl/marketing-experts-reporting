# CPW-R1B2 — TEST DEPLOYMENT EXECUTION — التقرير النهائيّ

- **التذكرة:** CPW-R1B2 — Client Documents & Assets Foundation
- **الفرع:** `feature/cpw-r1b2-document-service-20260807`
- **الـHEAD المنشور:** `1121e5776c9a7b428763db25dcfa0ec9bd996eef`
- **البيئة الوحيدة المتأثّرة:** TEST (`khubara-reporting-test` / `Staging` / 5091 / `reporting_test_uat` / `test.emarketingacademy.net`)
- **التاريخ:** 2026-08-07
- **الطابع الزمنيّ للنافذة (TS):** `20260807-154325` (مخزَّن في `/root/cpw-r1b2-test-ts.txt`)
- **القرار العامّ:** ✅ **TEST DEPLOYMENT PASS**
- **الجاهزيّة للإنتاج:** ⛔ **NO-GO (ثابتة بأمر المالك)**

---

## 1. النسخة الاحتياطيّة (§2)

| البند | القيمة |
|---|---|
| المسار | `/root/db-backups/cpw-r1b2-test-20260807-154325/` |
| الحجم | 46 MB — 12 ملفًّا |
| قاعدة البيانات | `pg_dump` بصيغة custom لـ`reporting_test_uat` — `pg_restore --list` مقروء بنجاح |
| Backend | أرشيف `/opt/reporting-test/publish` قبل الاستبدال |
| Frontend | أرشيف `/opt/reporting-test/frontend/dist` قبل الاستبدال |
| الإعداد | نسخة `/etc/khubara-reporting-test.env` قبل التعديل |
| nginx | نسخة إعداد النطاق |
| Document Storage Root | `N/A / first deployment` (لم يكن المسار موجودًا) |
| سجلّ الهجرات + حالة الخدمة + البصمات | ضمن الأرشيف + `SHA256SUMS` + `MANIFEST` |

**نسخ التراجُع الإضافيّة (جاهزة، غير مُنفَّذة):**
- `/opt/reporting-test/publish-backup-cpwr1b2-20260807-154325`
- `/opt/reporting-test/frontend/dist-backup-cpwr1b2-20260807-154325`

---

## 2. جذر التخزين (§3 + §12)

| البند | القيمة |
|---|---|
| المسار | `/var/lib/reporting-test/documents` |
| المالك/المجموعة | `www-data:www-data` |
| الصلاحيّات | `750` |
| خارج publish/dist | ✅ نعم |
| مُقدَّم عبر nginx (`root`/`alias`/StaticFiles) | ❌ لا — صفر تطابق |
| الحالة قبل الدخان | فارغ (0 ملفّ) |
| الحالة بعد التنظيف | **فارغ (0 ملفّ، 0 عنصر، 0 بايت)**، الجذر قائم بصلاحيّاته |

**لم يُنشأ أيّ مسار تخزين في Production أو RC.**

---

## 3. دلتا إعداد TEST (§4)

الملفّ الوحيد المعدَّل: `/etc/khubara-reporting-test.env` — 22 ⟶ **29 مفتاحًا** (816 B ⟶ 1207 B، الوضع `600 root:root`).
البصمة: `cc098470…` ⟶ `263a07ff7257049fe893dc2a56465ba09a7eb42f0d2b4c46bfdeb4843c3c7e28`.

المفاتيح السبعة المضافة (لا أكثر ولا أقلّ):

```
FileStorage__DocumentsRootPath=/var/lib/reporting-test/documents
FileStorage__MaxUploadSizeBytes=26214400
FileStorage__ResourceStorageQuotaBytes=2147483648
FileStorage__UploadRateLimitPermitLimit=20
FileStorage__UploadRateLimitWindowSeconds=60
FileStorage__ScanEngine=None
FileStorage__RequireCleanScanBeforeDownload=false
```

`AllowedExtensions` و`AllowedMimeTypes` تُركا بلا ضبط (الافتراض المُصلَّب في الكود). **لم يُغيَّر أيّ مفتاح آخر، ولم يُطبع أيّ سرّ.**

---

## 4. نتائج البناء (§5–§7)

**المصدر:** شجرة نظيفة معزولة على `1121e577…` بالضبط — `git status` نظيف، السلسلة = `c157829` + الـ7 commits الخاصّة بـR1B2 حصرًا. **لم يُبنَ من شجرة العمل المخلوطة.**

| البوّابة | النتيجة |
|---|---|
| `dotnet restore` + `dotnet build -c Release` | ✅ 0 خطأ |
| Unit Tests | ✅ **69/69** |
| R1B2 المستهدَفة (`ClientDocumentsTests` + `ClientExternalLinksTests`) | ✅ **28/28** |
| AdminGovernance / KPI (للهجرة 32) | ✅ بلا انحدار — والعيبان الأساسيّان `BASELINE-DEFECT-01` و`BASELINE-DEFECT-02` **لم يُصلَحا** ولا يُحتسبان انحدارًا لـR1B2 |
| `has-pending-model-changes` | ✅ **No changes** |
| SourceLink على الأربع DLLs | ✅ `1.0.0+1121e5776c9a7b428763db25dcfa0ec9bd996eef` |
| `npx tsc -b` | ✅ EXIT=0 |
| `npx vitest run` | ✅ **241/241** بلا انحدار |
| `npm run build` (VITE_API_BASE_URL=TEST) | ✅ ناجح |

**تحقّق تسريب الواجهة:** TEST API موجود ✅ / Production API = **0** / RC API = **0** / localhost = **0**.

---

## 5. بصمات القِطَع المنشورة (§8–§9)

| القطعة | القيمة |
|---|---|
| Backend | `/opt/reporting-test/publish` — 86 ملفًّا، `www-data:www-data`، بصمات DLL مطابقة بايتًا ببايت لمخرَج البناء |
| Frontend | `/opt/reporting-test/frontend/dist` — 7 ملفّات |
| الحزمة الحيّة | `index-uSFMb9aF.js` + `index-Cgt-yJT6.css` |
| `index.html` | يشير إلى الحزمة الجديدة ✅ |

---

## 6. إعادة التشغيل والهجرات (§10–§11)

| البند | القيمة |
|---|---|
| عدد عمليّات إعادة التشغيل | **واحدة فقط** — `systemctl restart khubara-reporting-test` |
| MainPID | 353557 ⟶ **684344** |
| NRestarts | **0** |
| زمن التوقّف | ≈ **0.65 ثانية** |
| Health داخليّ / عامّ | **200 / 200** |
| `reporting-api` (Production) | **لم يُعَد تشغيلها** |
| `khubara-reporting-rc` (RC) | **لم يُعَد تشغيلها** |

**الهجرات: 31 ⟶ 33** طُبِّقت عند الإقلاع بالترتيب:
1. `20260713171040_AdminGovernanceReportKpiCorrection`
2. `20260807033602_ClientDocumentsAndExternalLinks`

الرأس النهائيّ: **`20260807033602_ClientDocumentsAndExternalLinks`** — العدّ **33**.
الجداول المُتحقَّق منها: `kpi_evaluation_review_events` ✅، `client_documents` ✅، `client_document_versions` ✅، `client_external_links` ✅.
**لم يُشغَّل `Down` إطلاقًا.**

---

## 7. اختبارات الدخان (§13)

### 7.1 التشغيل الأوّل
`SMOKE_TOTAL=35 / PASS=26 / FAIL=9`.

**التسعة الفاشلة كانت عيوبًا في أداة الاختبار لا في المنتَج.** الاستجابة الحقيقيّة للرفع/التفاصيل هي المغلّف `{ "document": {…}, "versions": [ … ] }` بينما السكربت كان يقرأ `.id` من الجذر. الدليل الحاسم: البند 27 سرّب الجسم الخام `{"document":{"id":"427edf28-…","clientId":"cc877dc2-…","title":"CPWR…`. **كلّ عمليّات الرفع أعادت HTTP 200 وأنشأت صفوفًا حقيقيّة فعلًا.** والبند 19 رُفض فعليًّا لكن بكود `client_external_link.secret_forbidden` بدل `external_link.secret_detected` — لأنّ عنوان الاختبار نفسه احتوى كلمة `apikey` فأطلق حارس البيانات الوصفيّة قبل سياسة الرابط (خطأ في التسمية داخل الأداة).

### 7.2 إعادة التشغيل المصحَّحة
بعد إصلاح فكّ المغلّف + تحييد العنوان + تشديد البند 30 كي لا ينجح فراغًا:

`RERUN_TOTAL=11 / PASS=11 / FAIL=0`

| # | البند | النتيجة | الدليل |
|---|---|---|---|
| 06 | رفع PDF | ✅ | `200` / `size=547` / `v=1` / `application/pdf` |
| 07 | رفع PNG | ✅ | `200` / `image/png` / `size=264` |
| 08 | رفع DOCX | ✅ | `200` / MIME صحيح / `size=404` |
| 12 | النسخة 1 سارية | ✅ | `versions=1` / `no=1` / `isCurrent=true` / `sha256=2d097137…` |
| 13 | النسخة 2 تُلغي 1 | ✅ | `versions=2` / current=v2 (1059B) / superseded=v1 (547B) |
| 14 | تنزيل النسخة الملغاة | ✅ | 547 بايت مطابقة v1 بايتًا ببايت |
| 15 | تنزيل النسخة السارية | ✅ | 1059 بايت مطابقة v2 + `inline; filename*=UTF-8''…` |
| 16 | أرشفة/إلغاء أرشفة | ✅ | مخفيّ افتراضيًّا / ظاهر بـ`includeArchived` / مُستعاد |
| 19 | رابط بـ`?api_key=` مرفوض | ✅ | `400 external_link.secret_detected` |
| 30 | `ScanStatus=NotScanned` (C-01) | ✅ | `v2:NotScanned/None v1:NotScanned/None` على نسختين حقيقيّتين |
| 31 | `StorageKey` لا يظهر | ✅ | 9700 حرفًا — `storageKey=false`، مسار الجذر `false` |

### 7.3 النتائج المُثبَتة في التشغيل الأوّل (لم تتغيّر)

**التحقّق السلبيّ:**
- `.exe` ⟶ `400 document.extension_not_allowed`
- PDF مُعلَن `image/png` ⟶ `400 document.mime_mismatch`
- بايتات `%PDF-` باسم `.png` ⟶ `400 document.magic_number_mismatch`
- `?token=` ⟶ `400 external_link.secret_detected`
- `user:pass@host` ⟶ `400 external_link.embedded_credentials`
- رابط Google Drive نظيف ⟶ مقبول (`isActive=true`)

**مصفوفة الصلاحيّات (مُثبَتة حيًّا ومطابقة تمامًا لـ`AuthorizeReadAsync`/`AuthorizeWriteAsync`):**

| الفاعل | قراءة ألفا | كتابة ألفا | الملاحظة |
|---|---|---|---|
| Admin | 200 | 200 | ضمن `ClientCoreManagers` |
| مدير حساب ألفا | 200 | 200 | `AccountManagerId == uid` |
| مدير حساب ألفا ⟵ بيتا | **404** | **404 `client.not_found`** | خارج النطاق — لا كشف وجود |
| Manager | 200 | 200 | `ClientCoreManagers` + ضمن النطاق |
| TeamLeader | 200 | **403 `auth.forbidden`** | يرى ولا يُدير |
| Viewer | **404** | **404** | لا يرى العميل أصلًا |
| مجهول | 401 | 401 | |
| IDOR عبر عميل آخر | **404** | — | منع استكشاف |

**عقد `storage-usage` يثبت أنّ مفاتيح §4 حيّة طرفًا لطرف:**
`quota=2147483648` / `maxUpload=26214400` / `scanEngine=None` / `scannerConfigured=false` / `used=0`.

**الانحدار (كلّها 200):** `/submissions` (13) — `/report-templates` (34) — `/clients` (4) — `/contacts` — `/digital-channels` — `POST /projects` — `/dashboard/me` — `/notifications`.

**السلامة:** ~6 تسجيلات دخول و~13 عمليّة رفع إجماليًّا — دون حدّ `20/60s` لكلّ مستخدم. **لم تُختبَر حصّة الـ2GB بملفّات ضخمة، ولم يُختبَر حدّ المعدّل بعدوانيّة.**

---

## 8. سلامة البريد والإشعارات

| العدّاد | قبل الدخان | بعد الدخان | بعد التنظيف |
|---|---|---|---|
| `email_outbox` | 0 | **0** | **0** |
| `email_notifications` | 0 | **0** | **0** |
| `notifications` | 0 | **0** | **0** |

**صفر رسالة، صفر إشعار، صفر صفّ صادر — في كلّ المراحل.**

---

## 9. التنظيف (§14)

عدّادات ما قبل الدخان: `client_documents=0` / `client_document_versions=0` / `client_external_links=0` / `clients=4` / `projects=5` / `users=17` / `submissions=13` / التخزين 0 ملفّ.

الحصر الكامل قبل الحذف أثبت أنّ **كلّ** الصفوف المُنشأة تحمل البادئة `CPWR1B2-QA-` بلا استثناء: 8 مستندات، 9 نسخ، رابط واحد، مشروع واحد.

الحذف الجراحيّ (بالبادئة حصرًا، داخل معاملة واحدة):
`UPDATE 8` (تحرير `CurrentVersionId` لقيد RESTRICT) ⟶ `DELETE 9` نسخ ⟶ `DELETE 8` مستندات ⟶ `DELETE 1` رابط ⟶ `DELETE 1` مشروع ⟶ `COMMIT`.

ثمّ حُذفت الملفّات التسعة من جذر التخزين.

**التحقّق بعد التنظيف:**

| العدّاد | القيمة |
|---|---|
| `client_documents` | **0** |
| `client_document_versions` | **0** |
| `client_external_links` | **0** |
| `projects` | **5** (عاد إلى الأساس) |
| `clients` | **4** (بلا تغيير) |
| `users` | **17** (بلا تغيير) |
| `submissions` | **13** (بلا تغيير) |
| ملفّات التخزين | **0** — الجذر فارغ ومتّسق مع القاعدة |

**لم يُستخدَم أيّ Cleanup واسع.** لم تُحذف صفوف `audit_logs` (أثر التدقيق يبقى — 169 صفًّا).
الخدمة بعد التنظيف: `active` / MainPID 684344 / NRestarts 0 / health 200 / `fail:|crit:|Unhandled` = **0**.
حُذفت السكربتات المؤقّتة؛ بقيت ملفّات النتائج `/root/cpw-r1b2-smoke*-results.json` كأدلّة.

---

## 10. جاهزيّة اختبار المالك (§15)

**لم تُنشأ أيّ بيانات تخصّ المالك.** البيئة جاهزة للاختبار اليدويّ:

| البند | الحالة |
|---|---|
| `https://test.emarketingacademy.net/health` | **200** |
| الجذر بلا BasicAuth | **401** (بوّابة nginx تعمل كما هو مخطَّط) |
| الحزمة المُقدَّمة | `index-uSFMb9aF.js` + `index-Cgt-yJT6.css` |
| `/api/clients/{id}/documents` مجهولًا | **401** |
| جذر التخزين | قائم، فارغ، `750 www-data` |

المسارات المتروكة للمالك: رفع عرض فنّيّ، عقد، خطّة تسويق، إضافة نسخة ثانية، تنزيل، تاريخ النسخ، روابط Google Drive وFigma، رفض رابط يحمل سرًّا، ووصول مدير الحساب.

---

## 11. عزل Production و RC (§16) — **ZERO DELTA**

### Production

| البند | الأساس (§1) | الآن | الحالة |
|---|---|---|---|
| الخدمة | active | **active** | ✅ |
| MainPID | 654185 | **654185** | ✅ لم تُعَد التشغيل |
| NRestarts | 0 | **0** | ✅ |
| بدء التشغيل | 2026-08-07 08:57:45 UTC | **مطابق** | ✅ |
| Health | 200 | **200** | ✅ |
| عدد الهجرات | 30 | **30** | ✅ |
| رأس الهجرة | `20260724224053_AddReportApproverAndKpiReviewerOverrides` | **مطابق** | ✅ |
| بصمة `Reporting.Infrastructure.dll` | `15cd6613…` | **`15cd6613ddcb811f3a37ae554051abb9b215c378fd3624f3b7666d1fc48606bd`** | ✅ |
| حزمة الواجهة | `index-CG2a9RiH.js` | **مطابقة** | ✅ |
| `mtime` لـ`/etc/reporting-api.env` | 1785095398 | **1785095398** | ✅ |
| `email_outbox` | 0 | **0** | ✅ |
| `leave_requests` | 19 | **19** | ✅ |
| `email_notifications` | 362 | **362** | ✅ |

**انزياح عدّادَين طبيعيّ ومُفسَّر:** `audit_logs` 1148⟶**1150** و`notifications` 628⟶**630**. مصدرهما نشاط مستخدمين حقيقيّ على الإنتاج: صفَّا `submission.submitted` عند `12:09:43` و`13:03:26` UTC — **قبل** نافذة النشر، ولم أُصادِق على الإنتاج ولا كتبتُ عليه إطلاقًا. البصمات البنيويّة كلّها (الخدمة، الهجرات، الـDLL، الحزمة، الإعداد) صفريّة الدلتا.

### RC

| البند | الأساس (§1) | الآن | الحالة |
|---|---|---|---|
| الخدمة | active | **active** | ✅ |
| MainPID | 647747 | **647747** | ✅ لم تُعَد التشغيل |
| NRestarts | 0 | **0** | ✅ |
| بدء التشغيل | 2026-08-07 07:07:39 UTC | **مطابق** | ✅ |
| عدد الهجرات | 30 | **30** | ✅ |
| رأس الهجرة | `20260724224053_…` | **مطابق** | ✅ |
| حزمة الواجهة | `index-D5et8mMC.js` | **مطابقة** | ✅ |

---

## 12. جاهزيّة التراجُع (§17) — جاهزة، غير مُنفَّذة

| المسار | المحتوى |
|---|---|
| `/opt/reporting-test/publish-backup-cpwr1b2-20260807-154325` | Backend السابق |
| `/opt/reporting-test/frontend/dist-backup-cpwr1b2-20260807-154325` | Frontend السابق |
| `/root/db-backups/cpw-r1b2-test-20260807-154325/` | القاعدة + الإعداد + nginx + المانيفست |

**القاعدة:** عند فشل Health أو Migration أو دخان حرج ⟶ **لا يُشغَّل `EF Down` إطلاقًا**؛ تُستعاد النسخ أعلاه ثمّ يُعاد تشغيل TEST وحدها؛ وإن لزم إرجاع القاعدة فبـ`pg_dump` الكامل حصرًا. لم يُستدعَ التراجُع.

---

## 13. البوّابة النهائيّة — GO / NO-GO

| # | البند | القرار |
|---|---|---|
| 1 | النسخة الاحتياطيّة كاملة وقابلة للاستعادة | ✅ **GO** |
| 2 | جذر التخزين مُهيَّأ وآمن وغير مكشوف | ✅ **GO** |
| 3 | دلتا الإعداد = 7 مفاتيح TEST حصرًا | ✅ **GO** |
| 4 | البناء من مصدر نظيف على الـHEAD المعتمَد | ✅ **GO** |
| 5 | بوّابة Backend (69/69 + 28/28 + No changes) | ✅ **GO** |
| 6 | بوّابة Frontend (tsc 0 + 241/241 + لا تسريب) | ✅ **GO** |
| 7 | النشر ببصمات مطابقة وملكيّة صحيحة | ✅ **GO** |
| 8 | إعادة تشغيل واحدة لـTEST فقط | ✅ **GO** |
| 9 | الهجرتان 32 و33 طُبِّقتا والرأس صحيح | ✅ **GO** |
| 10 | التحقّق من التخزين وقت التشغيل | ✅ **GO** |
| 11 | اختبارات الدخان (بعد تصحيح الأداة: صفر فشل منتَج) | ✅ **GO** |
| 12 | تنظيف QA جراحيّ وكامل + تخزين متّسق | ✅ **GO** |
| 13 | سلامة البريد (0/0/0 في كلّ المراحل) | ✅ **GO** |
| 14 | Production و RC بصفر دلتا بنيويّة | ✅ **GO** |
| — | **الجاهزيّة للإنتاج** | ⛔ **NO-GO (ثابتة)** |

---

## 14. الحدود المُلتزَم بها

لم يُنفَّذ ولم يُقترَب من: نشر على Production أو RC، Push، Merge، PR، Tag، تعديل `main` أو `develop`، إصلاح `BASELINE-DEFECT-01/02`، بدء ميزة جديدة، أيّ Backfill، أو `EF Down`.

**الشرط C-03 (Runbook النسخ/الاسترجاع للملفّات) ما زال مُلزِمًا قبل أيّ نشر إنتاجيّ.**

**توقّف.**
