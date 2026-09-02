# تدقيق مستقلّ قراءةً فقط — سلالة R22B قبل الدمج وبعده

**التاريخ:** 2 سبتمبر 2026 · **الدور:** `INDEPENDENT_READ_ONLY_AUDITOR` · **الجلسة المدقِّقة:** `b95142fc-1187-4c9a-9052-826fb962923b`
**الجلسة المنفِّذة (موضوع التدقيق):** `3ecd3c72-e76c-4204-9988-b9f3605bfc2c` — أُوقفت يدويًّا قبل حفظ هذا التقرير.

**ما لم يقع في هذا التدقيق:** أيّ `commit` أو `push` أو `merge` أو `reset` أو `rebase` · أيّ تعديل لملفّ مصدر أو توثيق داخل الالتزامات · أيّ نشر أو كتابة على TEST أو RC أو الإنتاج · أيّ إنهاء لعمليّة.
**ما وقع:** قياسات `git` قرائيّة على الشجرة المشتركة، وبناء محلّيّ في `worktree` معزول (`/tmp/r22b-audit-bb37d1a`، حُذف بعد الانتهاء)، وقراءة المانيفست المنشور على TEST عبر SSH، وتشغيل حزم اختبارات محلّيّة، وفحص سجلّات الجلسات المحلّيّة.

---

## 1. المراجع البعيدة — مقيسة حيًّا

| المرجع | القيمة | ملاحظة |
|---|---|---|
| `origin/develop` | `dbe2518f0648a81cbb9fc0a6f1054576527c1c9a` | تحرّك من `cd09b67` بدفع Fast-Forward في `00:56:40.862Z` |
| `origin/main` | `508509ad8474b321c80cbdd48eb84ecb54bee212` | لم يُمسّ إطلاقًا |
| `origin/hotfix/r22b-multiline-idempotency-final-20260902` | `9e504af1726d0367f0ef9072ad4e7e72dd8bb2e0` | رأس المرشَّح بعد تصحيحين توثيقيّين |
| `origin/hotfix/r22b-multiline-idempotency-reconciliation-20260901` | `7c269df127f3092f9d032ecf5bb707dfadfb757d` | فرع قديم يبقى سجلًّا تاريخيًّا |

قياسان متتاليان (`01:11:28Z` و`01:11:34Z`) أعطيا القيم نفسها بلا فارق.

---

## 2. سلسلة النسب

```
cd09b67 (الأساس)
  └─ bb37d1a  fix(r22b)   ← الالتزام الوظيفيّ الوحيد
       └─ 5f0e136  docs   ← أدلّة المصالحة والـRunbook
            └─ dbe2518  docs   ← تصحيح 844 + حفظ تقريرَي الحوكمة   = origin/develop
                 └─ 9e504af  docs   ← تصحيح ادّعاء عدم الدفع        = رأس الفرع
```

```
git merge-base --is-ancestor cd09b67 origin/develop   ⟹ YES
git rev-list --merges --count cd09b67..origin/develop ⟹ 0      (خطّ مستقيم · لا merge commit ولا squash)
git merge-base --is-ancestor 1db114d origin/develop   ⟹ NO     (1db114d و7c269df ليسا سلفَين)
```

---

## 3. تدقيق النطاق

**COMMIT#1 — `bb37d1a` (وظيفيّ):** 12 ملفًّا.
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
- ملفّات `Docs/Runbooks/` أو `Ops/` داخله = **0**
- هجرات (`Persistence/Migrations`) = **0** · ملفّات `*ModelSnapshot.cs` = **0**
- التغيير الوحيد خارج الكود والاختبارات هو سطر `<InternalsVisibleTo Include="Reporting.IntegrationTests" />` في `Reporting.Infrastructure.csproj` — يوسّع سطح المكتبة لمشروع الاختبارات فقط، ومبرَّر بقياس مرشِّح استثناء الازدواج.

**COMMIT#2 — `5f0e136` (أدلّة وحوكمة):** 18 ملفًّا (Runbook + تقارير Ops + أدلّة JSON + 4 لقطات + 3 أدوات Python + سكربت e2e).
```
git diff --exit-code bb37d1a 5f0e136 -- reporting-frontend/src reporting-backend/src  ⟹ exit 0
```
عدد ملفّات `reporting-*/src` داخله = **0**.

**COMMIT#3/#4 — `dbe2518` و`9e504af` (توثيقيّان):** 4 ملفّات ثمّ ملفّان · **صفر** ملفّ تحت `src`/`tests`/`scripts` في كليهما ⟹ الشجرة الوظيفيّة `bb37d1a` لم تتغيّر منذ إنشائها.

---

## 4. المطابقة البايتيّة المستقلّة مع المرشَّح المختبَر `1db114d`

**(أ) كلّ ملفّ من الاثني عشر — تجزئة كائن git لكلّ مسار في الالتزامين:**
```
b31c194  EmailModels.cs                        IDENTICAL
5f98f8f  Reporting.Infrastructure.csproj       IDENTICAL
9fa28ff  SubmissionService.cs                  IDENTICAL
dc14581  SubmissionIdempotencyContractTests    IDENTICAL
ae46ba1  SubmissionIdempotencyIsolatedFactory  IDENTICAL
979211c  EmailHtmlMultilineTests.cs            IDENTICAL
e141406  verify-multiline-bundle.mjs           IDENTICAL
0770cb0  NotificationsBell.tsx                 IDENTICAL
689a3f8  AdminArchiveMultiline.test.tsx        IDENTICAL
f8d222d  AdminArchivePage.tsx                  IDENTICAL
377359f  ApprovalCommentsMultiline.test.tsx    IDENTICAL
5700bab  SubmissionsPage.tsx                   IDENTICAL
                                       12/12 مطابقة
```
**تسمية الأداة بدقّة:** القياس أعلاه بـ`git rev-parse <commit>:<path>` (تجزئة كائن git للمحتوى). **لم أستعمل `cmp` ولا `sha256sum`** على هذه الملفّات، ولا أدّعي ذلك.

**(ب) الأشجار الفرعيّة الأربع** — متطابقة بين `bb37d1a` و`1db114d`:
`reporting-frontend/src` · `reporting-backend/src` · `reporting-backend/tests` · `reporting-frontend/scripts`.

**(ج) مدخلات البناء الستّة** متطابقة: `package.json` · `package-lock.json` · `vite.config.ts` · `tsconfig.json` · `index.html` · `tailwind.config.js`.
`FRONTEND_LOCKFILE_SHA(sha256) = 8c5c9342611118a6bc5bef59143b2354c133df54176780d24b5f41a19952b965` = المسجَّل في `DEPLOYMENT-AND-GATES.md`.

**(د) فارق الـRunbook — صياغة غير ملتبسة:**
`git diff --name-status bb37d1a 1db114d` على كامل المستودع يُخرج **سجلًّا واحدًا**:
```
A  Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md
```
**هذا سجلّ واحد في `name-status` يمثّل ملفًّا كاملًا من 75 سطرًا (+75 سطر محتوى)، وليس سطر محتوى واحدًا.** بلغة `numstat`: `75  0  Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md`.
ومحتوى الملفّ نفسه متطابق بين `1db114d` و`5f0e136` (نفس كائن git `6c68437980aa061832f14ee09c66ce911cf024f6`) — أي أنّ الإصلاح لم يفقد شيئًا، بل نُقل من التزام وظيفيّ إلى التزام توثيقيّ.

**السبب الوحيد المثبَت لرفض `1db114d`:** تلوّث النطاق (ابتلاع Runbook داخل التزام وظيفيّ). لا خلل في الكود ولا في نتيجة اختبار.

---

## 5. البناء المستقلّ ومقارنة الأثر المبنيّ بالمنشور على TEST

```
git worktree add --detach /tmp/r22b-audit-bb37d1a bb37d1a     (شجرة معزولة · لا الشجرة المشتركة)
npm ci                                                         ⟹ نجح · القفل مطابق 8c5c9342…
VITE_API_BASE_URL=https://test.emarketingacademy.net/api npm run build   ⟹ نجح

EXPECTED_API_BASE=… node scripts/verify-multiline-bundle.mjs
  MULTILINE_ELEMENT · RESIZE_Y · WHITESPACE_PRE_WRAP · BREAK_WORDS
  COMMENT_FIELD_PRESENT · NO_ENTER_BLOCKER · API_BASE_URL · NO_SECRETS   = 8/8 PASS
  BUNDLE_MULTILINE_GATE = PASS · GATE_EXIT = 0
```

**المانيفست** (داخل `dist`: `find . -type f | LC_ALL=C sort | xargs shasum -a 256 | shasum -a 256`):
```
AUDIT_DIST_MANIFEST_SHA     = 17e300adee3460316bdabf484948a11bc3eded89058c46849f5af534ae3d632b
RECORDED_DIST_MANIFEST_SHA  = 17e300adee3460316bdabf484948a11bc3eded89058c46849f5af534ae3d632b
TEST_DEPLOYED_MANIFEST_SHA  = 17e300adee3460316bdabf484948a11bc3eded89058c46849f5af534ae3d632b   (قراءة SSH فقط)
                              ⟹ ثلاثتها متطابقة
7 ملفّات في كلّ جانب · index-BvE5cDGO.js = 1,666,702 بايت · index-Dmr24Us9.css = 35,548 بايت
```

**الخادم:** لم يُعَد بناؤه عمدًا. `dotnet publish` غير قابل لإعادة الإنتاج بايتيًّا (MVID يتغيّر في كلّ بناء)، فالقياس الصحيح هو تطابق شجرة المصدر — وقد ثبت في §4.

---

## 6. تصنيف نتائج الاختبارات بدرجات ثقة

**لم أعتمد أرقامًا من رسائل الالتزامات.** كلّ نتيجة مصنَّفة بما قِسته أنا:

### `REPRODUCED_NOW` — أُعيد قياسها في هذا التدقيق
| النتيجة | القياس |
|---|---|
| حزمة الواجهة كاملة | **73 ملفًّا / 844 اختبارًا ناجحة · `VITEST_EXIT=0` · 17.42 ثانية** (`/tmp/r22b-audit-vitest.log`) |
| اختبارات الأسطر المتعدّدة المكوِّنيّة | **18/18** (14 `ApprovalCommentsMultiline` + 4 `AdminArchiveMultiline`) |
| اختبارات بريد الأسطر المتعدّدة | **8/8** · `Failed: 0` · `DOTNET_EXIT=0` (`/tmp/r22b-audit-emailtests.log`) |
| اختبارات الازدواجيّة (Idempotency) | **9/9** على قاعدة نظيفة `reporting_idem_audit` أُنشئت ثمّ أُسقطت (`/tmp/r22b-audit-idem.log`) |

`MULTILINE_TESTS = 26/26` المسجَّل في رسالة الالتزام يتفكّك عندي إلى **18 واجهة + 8 بريد** — مطابق.

### `SUPPORTED_BY_PRESERVED_ARTIFACT` — أُثبتت بأثر محفوظ لا بإعادة تنفيذ
- تطابق المانيفست مع المنشور على TEST (الأثر المنشور قُرئ حيًّا).
- تجزئة ملفّ القفل ومدخلات البناء.
- `MIGRATIONS_ADDED = 0` و`EF_MODEL_SNAPSHOT_DIFF = NONE` (مقيسان من الشجرة مباشرةً).

### `CLAIM_ONLY_NOT_INDEPENDENTLY_VERIFIED` — لم أُعِد قياسها
- **حزمة الخادم الكاملة** (≈2301 اختبار تكامل + 618 وحدة). الاستناد هنا إلى **تطابق المصدر بايتيًّا** لا إلى إعادة قياس.
- **الضوابط السالبة الأربعة** (`NEGATIVE_CONTROLS = 4/4`).
- **الـUAT على TEST والتنظيف بعده** — وقعا في الجلسة المنفِّذة لا في جلستي؛ لا أشهد عليهما ولا أنفيهما.

---

## 7. تباينات بين الأدلّة الملتزَمة والحالة الحيّة

| # | التباين | الحالة |
|---|---|---|
| 1 | `FULL_FRONTEND_SUITE = 843` بينما القياس الحيّ **844** | **صُحِّح** في `dbe2518` مع `FRONTEND_TEST_COUNT_AUTHORITY = 844/844` |
| 2 | `UNEXPECTED_COMMIT_PUSHED = NO` بينما `7c269df` موجود على `origin` | **صُحِّح** في `9e504af` |
| 3 | ادّعاء أنّ المرجعين `backup/unexpected-r22b-commit-*` لم يُنشآ | **صُحِّح** في `9e504af`؛ المرجعان قائمان محلّيًّا ولم يُدفعا |

الثلاثة توثيقيّة بحتة، وصفرٌ منها يمسّ سلوك المنتج.

---

## 8. تدقيق الإسناد — من نفّذ ماذا (سجلّات الجلسات المحلّيّة)

كلّ الأفعال الكتابيّة على Git صدرت عن الجلسة `3ecd3c72-e76c-4204-9988-b9f3605bfc2c` (كانت `PID 43004/43005` · `--permission-mode bypassPermissions` · `cwd` = مجلّد المستودع)، عبر نداءات `Bash` صريحة:

| الطابع الزمنيّ (UTC) | الفعل |
|---|---|
| `2026-09-01T20:13:44.075Z` | `git commit` ⟹ `1db114d` |
| `2026-09-01T20:48:06.608Z` | `git commit` ⟹ `7c269df` |
| `2026-09-02T00:56:01.729Z` | `git commit` ⟹ `dbe2518` |
| `2026-09-02T00:56:06.744Z` | `git push origin hotfix/…final-20260902` |
| `2026-09-02T00:56:40.862Z` | `git push origin HEAD:refs/heads/develop` ⟹ **الدمج Fast-Forward** |
| `2026-09-02T01:03:57.616Z` | `git commit` ⟹ `9e504af` |
| `2026-09-02T01:04:03.821Z` | `git push origin hotfix/…final-20260902` |

الطوابع تطابق `AuthorDate` للالتزامات **بالثانية** (فارق +03:00).
**نُفي سببان بديلان نفيًا مثبَتًا:** خطّافات Git (`.git/hooks` كلّه `*.sample` ⟹ خامد بالتعريف · `core.hooksPath` افتراضيّ · لا `commit.template` ولا `alias`)، وخطّافات Claude Code (`.claude/settings.local.json` يعرّف `SessionStart` و`PreToolUse:Read` فقط، ولا أحدهما يلمس Git).
**لا شيء من ذلك تلقائيّ**، ولا شيء منه صادر عن جلسة التدقيق.

---

## 9. بوّابة السكون قبل حفظ هذا التقرير

```
PID 43004 / 43005                         ⟹ غير موجودتين (ps بلا صفوف)
أيّ عمليّة تحمل --resume 3ecd3c72         ⟹ لا شيء
git commit/push/merge/rebase/fetch جارية  ⟹ لا شيء
آخر كتابة في سجلّ 3ecd3c72                ⟹ 04:04:08 مقابل 04:11:14 وقت الفحص (سكون ≈7 دقائق)
MERGE_HEAD / REBASE_HEAD                  ⟹ none / none
git status --short -uno                   ⟹ فارغ
git diff --name-status                    ⟹ فارغ
git diff --cached --name-status           ⟹ فارغ (الفهرس نظيف)
HEAD المشترك                              ⟹ hotfix/…final-20260902 = 9e504af
```

---

## 10. الحكم

```
REMOTE_BRANCH_IDENTITY                  = PASS
PARENT_CHAIN                            = PASS   (cd09b67 → bb37d1a → 5f0e136 → dbe2518 → 9e504af · 1db114d ليس سلفًا)
FUNCTIONAL_SCOPE                        = PASS   (12 ملفًّا · 0 Ops/Docs · 0 هجرة · 0 ModelSnapshot)
EVIDENCE_SCOPE                          = PASS   (0 ملفّ مصدر في الالتزامات التوثيقيّة الثلاثة)
APPLICATION_BYTE_IDENTITY               = PASS   (12/12 كائن git + 4 أشجار + 6 مدخلات بناء)
RUNBOOK_DIFFERENCE                      = ملفّ كامل واحد (75 سطرًا) يظهر سجلًّا واحدًا في name-status
BUILT_ARTIFACT_MATCH                    = PASS   (17e300ad… أُعيد إنتاجه من bb37d1a)
TEST_DEPLOYMENT_MATCH                   = PASS   (المنشور على TEST = المبنيّ = المختبَر)
TEST_UAT_REUSE_VALID                    = PASS   (الشجرة الوظيفيّة لم تتغيّر بعد الـUAT)
DOCUMENTED_DISCREPANCIES                = 3 · كلّها توثيقيّة · كلّها مُصحَّحة في dbe2518 و9e504af
BACKEND_SUITE_REVERIFIED                = NO     (تحفّظ مسجَّل — الاستناد إلى تطابق المصدر)
NEGATIVE_CONTROLS_REVERIFIED            = NO     (تحفّظ مسجَّل)
DEVELOP_PUSH_SESSION_IDENTIFIED         = YES · 3ecd3c72-e76c-4204-9988-b9f3605bfc2c
CONCURRENT_WRITER_STOPPED               = YES
SHARED_INDEX_CLEAN                      = YES
```

**التحفّظان مُلزِمان عند أيّ ترقية لاحقة:** حزمة الخادم الكاملة والضوابط السالبة الأربعة لم يُعَد قياسها في هذا التدقيق. الحكم عليها مستند إلى أنّ شجرة المصدر لم تتغيّر بايتًا واحدًا عن المرشَّح الذي اختُبر ونُشر على TEST — وهو استناد سليم منطقيًّا، لكنّه **ليس** إعادة قياس.

**دلتا التشغيل:** `origin/develop` = `dbe2518` صار يحمل 8 ملفّات مصدر مختلفة عن الإنتاج `d25dc69`. أيّ ترقية RC أو نشر إنتاج ستحمل هذا الفارق، وكلاهما **بلا تصريح** حتّى أمر جديد صريح.

---

*هذا الملفّ غير متتبَّع عمدًا: لم يُنفَّذ عليه `git add` ولا `commit` ولا `push`.*
