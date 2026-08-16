# PRODUCTION-READINESS-ARTIFACT-MANIFEST
## RECONCILE-PROD-DEVELOP-LINEAGE — بيان مصنوعات جاهزيّة الإنتاج (Phase Z — تحضير فقط)

**التاريخ:** 16 أغسطس 2026 · **الحالة: تحضير فقط. لم تُنفَّذ أيّ عمليّة على الإنتاج.**

---

## 1) هويّة الإصدار المرشَّح

| العنصر | القيمة |
|---|---|
| شيفرة التطبيق (Application Release SHA) | **`4fddc20`** |
| الوسم | `rc-lineage-unified-20260816` |
| رأس الحوكمة (Governance Head) | `develop` — توثيق و`Ops` فوق `4fddc20`، **زائد إصلاح `DEFECT-RC-01`** |
| الفارق الوحيد في شجرة المنتج بعد الوسم | `reporting-frontend/tsconfig.app.json` (إضافة `"node"` إلى `types`) |
| صفر شيفرة خادم متغيّرة | `git diff --name-only 4fddc20..HEAD -- reporting-backend` = **0 ملفّ** |

**لماذا يبقى المصنوع المنشور على RC صالحًا رغم هذا الفارق:** التغيير إعداد **بناء** لا شيفرة
تنفيذ، وأثره على الحزمة المشحونة **مقيس لا مفترَض**: بُنِيت الحزمة قبل الإصلاح وبعده وقُورنت
بايتًا ⟹ `sha256` متطابق (`64eb45573d31c2d1342750eeeb5724e5…`) و7 ملفّات في الحالتَين، و`diff -rq`
بلا فروق. أي أنّ ما يُنشَر من الرأس الحاليّ مطابق حرفيًّا لما نُشِر من `4fddc20`.

## 2) حالة الإنتاج الحاليّة (قراءة فقط — لم يُكتَب شيء)

| القياس | القيمة |
|---|---|
| الخدمة `reporting-api` | `active` |
| `/health` | `200` |
| قاعدة البيانات | `reporting_prod` |
| عدد الهجرات | **30** |
| رأس السجلّ | `20260724224053_AddReportApproverAndKpiReviewerOverrides` |
| المستخدمون | 34 |
| العملاء | 10 |
| المشاريع | 33 |
| التسليمات | 258 |
| بصمة `Reporting.Api.dll` | `md5 7248e193ea1cbbac6268f98e362306b6` |
| `auth_basic` في إعداد nginx للإنتاج | **0** (فلا ينطبق `DEFECT-RC-02`) |

## 3) دلتا الهجرات المتوقَّعة على الإنتاج

الإنتاج و RC كانا متطابقَين حرفيًّا قبل هذا النشر (30 هجرة، نفس الرأس، نفس الـSHA)، فما
جرى على RC هو **بالضبط** ما سيجري على الإنتاج:

```
30  خطّ أساس الإنتاج الحاليّ
+ 2  صفّا الجسر الاسميّان (بلا أيّ تغيير بنيويّ)
+ 8  هجرات النَسَب الموحّد
= 40  المتوقَّع بعد النشر
```

الرأس المتوقَّع: `20260811142239_AddProject360Foundation`.
البصمة البنيويّة المتوقَّعة: `3b3eb6b04fc0e6b1898468bd2cfed546` (78 جدولًا · 928 عمودًا).

## 4) المصنوعات المطلوبة للنشر

| المصنوع | المصدر | التحقّق |
|---|---|---|
| نشر الخادم (`publish`) | بناء `4fddc20` من نسخة معزولة | `md5` قبل الرفع وبعده |
| حزمة الواجهة (`dist`) | `tsc -b && vite build` مع `VITE_API_BASE_URL` **الإنتاجيّ** | 7 ملفّات · `md5` |
| أداة الجسر | `Ops/MigrationHistoryBridge/bridge.sh` | تشغيل جافّ أوّلًا |
| سكربت البصمة | `Ops/MigrationHistoryBridge/fingerprint.sql` | قبل/بعد |
| بوّابات التحقّق | `Ops/TestUatGates/*` (كلّها مُعامَلة بالبيئة) | تُمرَّر لها قيم الإنتاج |

> **تنبيه بناء حاسم:** حزمة الواجهة تحمل `VITE_API_BASE_URL` **مخبوزًا وقت البناء**. حزمة RC
> تشير إلى `rc-report.emarketingacademy.net/api` ⟹ **لا تُنسَخ حزمة RC إلى الإنتاج إطلاقًا**؛
> يجب بناء حزمة إنتاج مستقلّة من نفس الـSHA.

## 5) حاجب جاهزيّة مفتوح — `PROD-READINESS-01`

**ملفّ بيئة الإنتاج لا يحتوي `FileStorage__DocumentsRootPath`** (يحتوي
`FileStorage__EmployeeServiceFinalDocumentsPath` فقط)، بينما TEST و RC يحتويانه.

**الأثر إن نُشِر كما هو:** `LocalFileStorage.Root` يسقط إلى الافتراضيّ
`ContentRoot/App_Data/documents` — أي **داخل مجلّد `publish` الذي يُستبدَل في كلّ نشر** ⟹
فقدان صامت لمستندات العملاء المرفوعة عند أوّل نشر لاحق.

**العلاج المطلوب قبل تفعيل CPW-R2 على الإنتاج:** ضبط `FileStorage__DocumentsRootPath` على
مسار دائم **خارج جذر الويب وخارج `publish`** (على غرار `/var/lib/reporting/documents`)،
بملكيّة `www-data` وصلاحيّة `750`، مع بقيّة مفاتيح CPW-R2 الموجودة على TEST:
`MaxUploadSizeBytes` · `ResourceStorageQuotaBytes` · `UploadRateLimitPermitLimit` ·
`UploadRateLimitWindowSeconds` · `ScanEngine` · `RequireCleanScanBeforeDownload`.

**هذا حاجب إعداد لا حاجب شيفرة، ولم يُنفَّذ لأنّ أيّ تغيير إعداد على الإنتاج ممنوع بلا تصريح.**

## 6) العيوب المعروفة عند التسليم

| المعرّف | الوصف | الحالة |
|---|---|---|
| `DEFECT-RC-01` | `tsc -b` يفشل بثلاثة أخطاء في `src/routeRegistry.test.ts` | **مُصلَح** — والحزمة المشحونة مطابقة بايتًا |
| `DEFECT-RC-02` | `auth_basic` في nginx لـRC يخنق `/api` و`/hubs` | **مُصلَح على RC** · الإنتاج غير متأثّر (0 `auth_basic`) |
| `PROD-READINESS-01` | غياب `FileStorage__DocumentsRootPath` على الإنتاج | **مفتوح — حاجب إعداد قبل النشر** |
| `BASELINE-DEFECT-01` | `AdminGovernanceTests.Hr_CanFlagCommentRequestReopen_…` | مفتوح · خارج النطاق · تذكرة مستقلّة |
| `BASELINE-DEFECT-02` | `EmployeeProfileScopeTests.Profile_Summary_Reflects_Submitted_Kpi` | مفتوح · خارج النطاق · تذكرة مستقلّة |

## 7) النسخ الاحتياطيّة والاستعادة

نسخ RC الكاملة في `/root/backups/20260816-rc-deploy/` مع `CHECKSUMS.sha256`، وقد **جُرِّبت
صلاحيّة الاستعادة فعليًّا** لا بمجرّد وجود الملفّ (Phase Q). نسخ الإنتاج تُؤخذ **جديدة**
مباشرة قبل النشر وفق الكرّاس المرافق.
