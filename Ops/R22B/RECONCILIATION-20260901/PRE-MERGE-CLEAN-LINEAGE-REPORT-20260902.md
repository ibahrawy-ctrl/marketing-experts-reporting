# تقرير ما قبل الدمج — سلالة نظيفة بالتزامين (R22B)

**التاريخ:** 2 سبتمبر 2026 · **الفرع:** `hotfix/r22b-multiline-idempotency-final-20260902`
**لم يقع:** دمج على `develop` · ترقية RC · نشر إنتاج · نشر TEST · `force-push` · تعديل الفرع المنشور القديم.

---

## 1. الحالة الحيّة قبل البدء (خطوة 1)

```
git ls-remote origin develop main
cd09b67a0924a9932a1c5411a75d5dfb848f130d  refs/heads/develop   ⟹ مطابق للمطلوب
508509ad8474b321c80cbdd48eb84ecb54bee212  refs/heads/main
```

الفرع الجديد أُنشئ من `cd09b67a…` مباشرةً. الفرع القديم `…reconciliation-20260901` والالتزام `1db114d`
**بقيا كما هما سجلًّا تاريخيًّا** بلا أيّ تعديل أو إعادة كتابة.

```
PUSHED = YES_ON_LEGACY_BRANCH_ONLY
  origin/hotfix/…reconciliation-20260901 = 7c269df   (يحوي 1db114d سلفًا)  ← مدفوع سابقًا
  origin/develop · origin/main                                            ← لم يُمسّا
BACKUP_REFS_EXIST  = YES  (backup/unexpected-r22b-commit-1db114d · …-7c269df — محلّيّان، غير مدفوعين)
```

---

## 2. الالتزامان

| | SHA | النطاق |
|---|---|---|
| **COMMIT#1 وظيفيّ** | `bb37d1a3de4490e5444a80ae15c2d2d710982225` | 12 ملفًّا (مصدر تطبيق + اختبارات + حارس الحزمة) |
| **COMMIT#2 أدلّة/حوكمة** | `5f0e1367eec423cf3532f0b9bc60b939b52bc66a` | 18 ملفًّا (Runbook + تقارير Ops + أدوات الرحلة والأدلّة) |

### COMMIT#1 — `git diff --name-status cd09b67 bb37d1a`
```
M reporting-backend/src/Reporting.Application/Notifications/EmailModels.cs
M reporting-backend/src/Reporting.Infrastructure/Reporting.Infrastructure.csproj
M reporting-backend/src/Reporting.Infrastructure/Services/SubmissionService.cs
A reporting-backend/tests/Reporting.IntegrationTests/SubmissionIdempotencyContractTests.cs
A reporting-backend/tests/Reporting.IntegrationTests/SubmissionIdempotencyIsolatedFactory.cs
A reporting-backend/tests/Reporting.UnitTests/EmailHtmlMultilineTests.cs
A reporting-frontend/scripts/verify-multiline-bundle.mjs
M reporting-frontend/src/components/NotificationsBell.tsx
A reporting-frontend/src/pages/AdminArchiveMultiline.test.tsx
M reporting-frontend/src/pages/AdminArchivePage.tsx
A reporting-frontend/src/pages/ApprovalCommentsMultiline.test.tsx
M reporting-frontend/src/pages/SubmissionsPage.tsx
```
عدد ملفّات `Docs/Runbooks/` أو `Ops/` داخله = **0**.
رسالة الالتزام تحمل الأعلام المصرَّح بها فقط (`MULTILINE_TESTS = 26/26` · `NEGATIVE_CONTROLS = 4/4` ·
`IDEMPOTENCY_TESTS = 9/9` · `MIGRATIONS_ADDED = 0` · `EF_MODEL_SNAPSHOT_DIFF = NONE`) **بلا أيّ أرقام
فشل تفصيليّة لكلّ ضابط سلبيّ** وبلا ادّعاءات `cmp`/`sha256`.

### COMMIT#2 — `git diff --name-status bb37d1a 5f0e136`
```
A Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md
A Ops/R22B/PHASE4-TEST/R22B-MULTILINE-GATE-CORRECTIVE-ADDENDUM-20260901.md
A Ops/R22B/RECONCILIATION-20260901/COMMIT-IDENTITY-AUDIT-ADDENDUM.md
A Ops/R22B/RECONCILIATION-20260901/DEPLOYMENT-AND-GATES.md
A Ops/R22B/RECONCILIATION-20260901/UAT-AND-CLEANUP.md
A Ops/R22B/RECONCILIATION-20260901/evidence/{api-uat-evidence,cleanup-apply,cleanup-dryrun,uat-fixture-state,ui-surfaces}.json
A Ops/R22B/RECONCILIATION-20260901/screenshots/S1..S4 (4 لقطات)
A Ops/R22B/tools/r22r-uat-{provision,journey,cleanup}.py
A reporting-frontend/e2e/r22r-multiline-surfaces.mjs
```
عدد ملفّات `reporting-frontend/src` أو `reporting-backend/src` داخله = **0**، و
`git diff --exit-code bb37d1a 5f0e136 -- reporting-frontend/src reporting-backend/src` ⟹ **exit 0**.

---

## 3. إثبات مطابقة التطبيق للمرشَّح المختبَر — بلا مساواة شجرة كاملة

نُقل الـRunbook إلى التزام آخر، فشجرة المستودع تختلف حتمًا عن `1db114d`. لذلك القياس على ثلاث طبقات:

### (أ) تطابق بايتيّ لكلّ ملفّ من الاثني عشر (تجزئة كائن git لكلّ ملفّ)
```
b31c1942…  EmailModels.cs                       IDENTICAL
5f98f8f2…  Reporting.Infrastructure.csproj      IDENTICAL
9fa28ff1…  SubmissionService.cs                 IDENTICAL
dc145811…  SubmissionIdempotencyContractTests   IDENTICAL
ae46ba14…  SubmissionIdempotencyIsolatedFactory IDENTICAL
979211c6…  EmailHtmlMultilineTests.cs           IDENTICAL
e1414065…  verify-multiline-bundle.mjs          IDENTICAL
0770cb0c…  NotificationsBell.tsx                IDENTICAL
689a3f81…  AdminArchiveMultiline.test.tsx       IDENTICAL
f8d222d0…  AdminArchivePage.tsx                 IDENTICAL
377359f2…  ApprovalCommentsMultiline.test.tsx   IDENTICAL
5700bab4…  SubmissionsPage.tsx                  IDENTICAL
                                    12/12 مطابقة
```

### (ب) تطابق الأشجار الفرعيّة للتطبيق
```
reporting-frontend/src      3440f8bf964a53b6595aee806514dccc5bf5b81a = نفسه في 1db114d
reporting-backend/src       4e07927937afdac985f46ff1e0dad343095e2926 = نفسه
reporting-backend/tests     30859e1d769c7c160c4cf3f4ae716741051c9a8c = نفسه
reporting-frontend/scripts  f4a9a68166ab9d277e5efacd34847aac100eae36 = نفسه
```
وفوق ذلك `git diff --name-status bb37d1a 1db114d` على **كامل المستودع** يُخرج **سجلًّا واحدًا** فقط:
`A Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md`. وللدقّة: هذا سجلّ واحد في `name-status`
لكنّه **ملفّ كامل من 75 سطرًا** (`git diff --numstat` ⟹ `75 0 …` · `git diff --shortstat` ⟹
`1 file changed, 75 insertions(+)`) وليس سطر محتوى واحدًا. أي أنّ الفارق الوحيد هو تلوّث النطاق
الذي جرى تصحيحه، ولا شيء غيره من مصدر التطبيق.

### (ج) مدخلات البناء غير متغيّرة
```
package.json · package-lock.json · vite.config.ts · tsconfig.json · index.html · tailwind.config.js
                                    ستّتها تجزئات متطابقة مع 1db114d
FRONTEND_LOCKFILE_SHA(sha256) = 8c5c9342611118a6bc5bef59143b2354c133df54176780d24b5f41a19952b965
                              = المسجَّل في DEPLOYMENT-AND-GATES.md
```

---

## 4. البناء النظيف من COMMIT#1 ومقارنة الحزمة بالمنشور على TEST

```
git worktree add --detach /tmp/final-build bb37d1a      (شجرة معزولة، لا شجرة العمل)
npm ci                                                   ⟹ OK
VITE_API_BASE_URL=https://test.emarketingacademy.net/api npm run build   ⟹ OK

node scripts/verify-multiline-bundle.mjs                 ⟹ GATE_EXIT=0 · BUNDLE_MULTILINE_GATE=PASS
  MULTILINE_ELEMENT · RESIZE_Y · WHITESPACE_PRE_WRAP(26) · BREAK_WORDS(3)
  COMMENT_FIELD_PRESENT · NO_ENTER_BLOCKER · NO_SECRETS      = 7/7 PASS

cd dist && find . -type f | sort | xargs shasum -a 256 | shasum -a 256
REBUILT_MANIFEST_SHA  = 17e300adee3460316bdabf484948a11bc3eded89058c46849f5af534ae3d632b
DEPLOYED_MANIFEST_SHA = 17e300adee3460316bdabf484948a11bc3eded89058c46849f5af534ae3d632b   (TEST)
                        ⟹ متطابقان بايتًا ببايت
```

⟹ الحزمة المبنيّة من `bb37d1a` هي **نفس بايتات** الحزمة المنشورة والمختبَرة على TEST.

**الخادم:** لم يُعَد بناؤه لأنّ `reporting-backend/src` شجرةً وملفًّا ملفًّا مطابق لـ`1db114d`، و
`dotnet publish` غير قابل لإعادة الإنتاج بايتيًّا افتراضيًّا (MVID يتغيّر في كلّ بناء)، فالقياس الصحيح
هو تطابق المصدر لا تطابق الـDLL.

---

## 5. أثر ذلك على UAT وTEST (خطوة 7)

```
TEST_REDEPLOYMENT             = NOT_REQUIRED
PREVIOUS_TEST_UAT_REMAINS_VALID = YES
UAT_RERUN                     = NOT_PERFORMED
TEST_WRITES_IN_THIS_ROUND     = 0
```

---

## 6. التحقّق بعد الدفع (خطوة 8)

```
git ls-remote origin …
cd09b67a0924a9932a1c5411a75d5dfb848f130d  refs/heads/develop                        ← لم يتغيّر
508509ad8474b321c80cbdd48eb84ecb54bee212  refs/heads/main                           ← لم يتغيّر
5f0e1367eec423cf3532f0b9bc60b939b52bc66a  refs/heads/hotfix/…final-20260902         ← الفرع الجديد
7c269df127f3092f9d032ecf5bb707dfadfb757d  refs/heads/hotfix/…reconciliation-20260901 ← لم يُمسّ

git rev-list --left-right --count origin/develop...HEAD  ⟹ 0  2   (AHEAD=2 · BEHIND=0)
git merge-base --is-ancestor 1db114d HEAD                ⟹ IS_ANCESTOR=NO
git log --oneline cd09b67..HEAD                          ⟹ 5f0e136 · bb37d1a  (SHAان جديدان)
```

---

## 7. التسليم

```
NEW_FUNCTIONAL_SHA                       = bb37d1a3de4490e5444a80ae15c2d2d710982225
NEW_EVIDENCE_SHA                         = 5f0e1367eec423cf3532f0b9bc60b939b52bc66a
FUNCTIONAL_COMMIT_SCOPE                  = CLEAN
EVIDENCE_COMMIT_CONTAINS_APPLICATION_SOURCE = NO
APPLICATION_TREE_MATCHES_TESTED_CANDIDATE   = YES
BUILT_ARTIFACT_MATCHES_TEST_DEPLOYMENT      = YES
TEST_UAT_REUSE_JUSTIFIED                    = YES
ORIGIN_DEVELOP_UNCHANGED                    = YES
ORIGIN_MAIN_UNCHANGED                       = YES
SAFE_TO_MERGE_TO_DEVELOP                    = YES
```

`SAFE_TO_MERGE_TO_DEVELOP = YES` وصفٌ لجاهزيّة السلالة فنّيًّا، **لا تنفيذًا**:
`MERGE_TO_DEVELOP` و`RC_PROMOTION` و`PROD_PROMOTION` تبقى **بلا تصريح** حتّى أمر جديد صريح.
