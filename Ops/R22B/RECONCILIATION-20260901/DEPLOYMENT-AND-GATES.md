# R22B — مصالحة الأسطر المتعدّدة وIdempotency: النشر والبوّابات (TEST فقط)

**التاريخ:** 1 سبتمبر 2026 · **البيئة:** TEST وحدها — `test.emarketingacademy.net`
**التصريح:** `NEW_RECONCILIATION_BRANCH/SOURCE_CHANGES/BUILD/COMMIT/PUSH/TEST_DEPLOYMENT/TEST_UAT_WRITES/TEST_CLEANUP = AUTHORIZED`
**غير مصرَّح به ولم يقع:** `MERGE_TO_DEVELOP` · `RC_PROMOTION` · `PROD_PROMOTION`

---

## 1. السلالة

| الحقل | القيمة |
|---|---|
| `RECONCILIATION_BASE_SHA` | `cd09b67a0924a9932a1c5411a75d5dfb848f130d` = `origin/develop` (مُقاس حيًّا بـ`git ls-remote`) |
| `RECONCILIATION_CANDIDATE_SHA` | `1db114db32ecf8e5ff4e6625d72601520899287d` |
| الفرع | `hotfix/r22b-multiline-idempotency-reconciliation-20260901` (مدفوع إلى origin) |
| موقع المرشَّح | `AHEAD=1 · BEHIND=0` فوق `origin/develop` مباشرةً |
| `origin/main` | `508509ad…` لم يُمسّ |

**النقل جراحيّ بالملفّ لا بالـcommit** — لم يُدمَج فرعٌ يتيم ولم تُنقل سلسلة التزامات.

### `PORT_FROM_986cc3b` (DEFECT-IDEMPOTENCY-01)
أربعة ملفّات، وهي **كامل** الالتزام؛ دُقِّقت مقطعًا مقطعًا فلا مقطع خارج النطاق:
- `SubmissionService.cs` (+80/−12) — ثلاثة مقاطع: `using Npgsql` · بحث النَسَب (`FindByTemplateLineageAsync`) بدل مطابقة `ReportTemplateVersionId` مع تقديم حارس إسناد القالب · ابتلاع `23505` المفسَّر وحده على `PeriodUniqueIndexName`.
- `Reporting.Infrastructure.csproj` (+6) — `InternalsVisibleTo` للاختبارات حصرًا.
- `SubmissionIdempotencyContractTests.cs` (384) — 9 اختبارات.
- `SubmissionIdempotencyIsolatedFactory.cs` (22) — قاعدة معزولة `reporting_idem_iso`.

**تحقّق عدم التعارض:** `git log 00b1204..origin/develop` على هذه الملفّات = **فارغ** ⟹ نسخ نسخة `986cc3b` = تطبيق دلتاه وحدها، بلا دهس لأيّ عمل على `develop`.

### `PORT_FROM_00b1204` (MULTILINE-COMMENTS)
- `SubmissionsPage.tsx` (+14/−2) — `textarea rows=4 resize-y` بدل `Input` · `whitespace-pre-wrap break-words` في سجلّ الاعتماد.
- `NotificationsBell.tsx` (+4/−2) — نفس الصنفين في جسم الإشعار.
- `ApprovalCommentsMultiline.test.tsx` (346) — 14 اختبارًا.
- `verify-multiline-bundle.mjs` (134) — حارس الحزمة المبنيّة.
- `Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md` (75) — قاعدة نَسَب الحزمة الحاكمة.

**لم يُنقل** من `00b1204`: تقرير التشخيص (دليل، يذهب لالتزام الأدلّة).

### السطحان المُكمَّلان (جديدان في هذا المرشَّح)
- `EmailModels.cs` (+15/−2) — `EncodeWithLineBreaks`: **`HtmlEncode` أوّلًا** ⟹ `CRLF`/`CR` إلى `LF` ⟹ `LF` إلى `<br />`. الترتيب عقدٌ أمنيّ: العكس يجعل الوسم نصًّا مرئيًّا، وتمرير HTML خام يفتح XSS. لا اعتماد على `white-space` في CSS لأنّ عملاء البريد لا يدعمونه موثوقًا.
- `AdminArchivePage.tsx` (+4/−2) — `whitespace-pre-wrap break-words` على خليّة التعليق في «لقطة سير العمل».
- `EmailHtmlMultilineTests.cs` (138) — 8 اختبارات · `AdminArchiveMultiline.test.tsx` (150) — 4 اختبارات.

`FILES_CHANGED = 13` · `MIGRATIONS_ADDED = 0` · `MODEL_SNAPSHOT_DIFF = NONE`.

---

## 2. البوّابات المحلّيّة — كلّها خضراء قبل النشر

```
FRONTEND_COMPONENT_TESTS = PASS   (14 تعليق + 4 أرشيف)
FULL_FRONTEND_SUITE      = PASS   73 ملفًّا / 843 اختبارًا
TSC_BUILD                = PASS   exit 0
PRODUCTION_BUILD         = PASS
BACKEND_BUILD            = PASS   0 Error(s)
FULL_BACKEND_SUITE       = PASS   2301/2301 تكامل + 618/618 وحدة
IDEMPOTENCY_TESTS        = 9/9 PASS
EF_MODEL_SNAPSHOT_DIFF   = NONE
MIGRATIONS_ADDED         = 0
HOTFIX_DIFF              = IN_SCOPE_ONLY  (13 ملفًّا؛ أُلغيت تهيئة ملفّ Runbook غير ذي صلة كنسه git add)
BUNDLE_MULTILINE_GATE    = PASS   7/7
```

> حزمة التكامل جرت على قاعدة **معزولة نظيفة** `reporting_recon_20260901` (لا `reporting_test` الملوّثة)، بعد تطبيق تصحيح `Classification='Supplementary'` الموثَّق. صفر فشل — أفضل من خطّ الأساس التاريخيّ (8 حالات فشل).

---

## 3. Negative Controls — إثبات أنّ الاختبارات تلتقط العيب

| # | العيب المحقون | الالتقاط | الاسترجاع |
|---|---|---|---|
| NC-01 | `textarea` ⟵ `Input` في `SubmissionsPage` | **7 فشل** من 14 | `cmp` OK · `sha256 40f820e1…` |
| NC-02 | إزالة `pre-wrap`/`break-words` من `AdminArchivePage` | **3 فشل** | `cmp` OK · `sha256 97bf545a…` |
| NC-03 | `EncodeWithLineBreaks` ⟵ `HtmlEncode` في البريد | **5 فشل** من 8 | `cmp` OK · `sha256 0af1df1b…` |
| NC-04 | مطابقة النَسَب ⟵ معرّف الإصدار وحده | **6 فشل** من 9 | `cmp` OK · `sha256 b28e6fa8…` |

الأربعة استُرجعت **قبل** الالتزام؛ لا كود معيب باقٍ. بعد إعادة البناء على المصدر المسترجَع: البريد 8/8 وIdempotency 9/9.

> ملاحظة تشخيصيّة مسجَّلة: قياسٌ وسيط أظهر 6 فشل في Idempotency بعد الاسترجاع — سببه `--no-build` على DLL قديمة محقونة، لا انحدارًا. أُعيد البناء وقاعدة معزولة نظيفة فعادت 9/9.

---

## 4. النشر على TEST — بوّابة نَسَب الحزمة

```
SOURCE_SHA             = 1db114db32ecf8e5ff4e6625d72601520899287d
APPROVED_ORIGIN_REF    = origin/hotfix/r22b-multiline-idempotency-reconciliation-20260901
FRONTEND_LOCKFILE_SHA  = 8c5c9342611118a6bc5bef59143b2354c133df54176780d24b5f41a19952b965
BUILD_ENVIRONMENT_KEYS = VITE_API_BASE_URL=https://test.emarketingacademy.net/api
BUILD_COMMAND          = npm ci && npm run build ⊕ dotnet publish -c Release   (git worktree add --detach /tmp/recon-build)
DIST_MANIFEST_SHA      = 17e300adee3460316bdabf484948a11bc3eded89058c46849f5af534ae3d632b
DEPLOYED_MANIFEST_SHA  = 17e300adee3460316bdabf484948a11bc3eded89058c46849f5af534ae3d632b
```

**أعلام البوّابة:**
```
ARTIFACT_SOURCE_EXISTS_ON_ORIGIN      = YES  (1db114d على origin؛ ls-remote يؤكّده)
BUILD_SHA_ON_APPROVED_RELEASE_REF     = YES
WORKTREE_CLEAN_AT_BUILD               = YES  (شجرة معزولة، git status فارغ)
DIRECT_ORPHAN_BRANCH_DEPLOYMENT       = لم يقع
PRODUCTION_FIX_BACKMERGED_TO_DEVELOP  = NOT_AUTHORIZED (حاجب لإغلاق التذكرة لا لبوّابة TEST)
```

### تطابق الخادم بايتيًّا (نُشر هذه المرّة، خلافًا للجولة السابقة)

| المكتبة | قبل (بناء `986cc3b`) | بعد = المبنيّ محلّيًّا |
|---|---|---|
| `Reporting.Api.dll` | `6d0f7f9a…` | `b685a824117361d6cc928ace2ef366d90a6dc38f7e95e74f8d16d50e7bb3f013` |
| `Reporting.Infrastructure.dll` | `43762db3…` | `de4b47349dd88dbfe7da6b86f2af30502edfb4730b5d628869ea336b5163545d` |
| `Reporting.Application.dll` | `344c8ed6…` | `f7c37a067d73a73ac4ed39dd4fec92bdf4c6cf128a52b8dd91d92a59f2a08fc9` |

### النسخ الاحتياطيّة قبل الاستبدال
- `/opt/backups/api-test/publish-20260901T201550Z.tgz` (47,759,195 B)
- `/opt/backups/frontend-test/dist-20260901T201550Z.tgz` (415,098 B)

### ما بعد إعادة التشغيل
```
SERVICE            = active (khubara-reporting-test.service)
HEALTH             = 200 على 127.0.0.1:5091
TEMPLATE_VERSIONS  = 45/58 قبل  →  45/58 بعد
max(UpdatedAtUtc)  = 2026-08-31 22:36:07.986392+00 قبل  →  نفسه بعد
```
⟹ `NO_TEMPLATE_PUBLICATION_ON_RESTART = PROVEN` · `MIGRATIONS_APPLIED = 0`.

### البيئتان الأخريان
```
PRODUCTION_TOUCHED = NO   (reporting-api على 5090 active؛ بصمة DLL 248ef468… لم تتغيّر)
RC_TOUCHED         = NO   (5092 يعمل؛ لم يُرسَل إليه شيء)
```
