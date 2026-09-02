# ملحق تدقيق هويّة الالتزام — R22B (قراءة فقط)

**التاريخ:** 2 سبتمبر 2026 · `MODE = READ_ONLY` · لم يقع أيّ `commit`/`push`/`reset`/`rebase`/نشر/كتابة على TEST.

> ## ⚠︎ تصحيحات لاحقة (2 سبتمبر 2026 — تسبق قراءة بقيّة الملفّ وتَجُبّ ما يخالفها)
>
> هذا الملفّ كُتب في لحظة زمنيّة سابقة، وأربع عبارات فيه صارت غير دقيقة. التصحيح توثيقيّ بحت ولا يمسّ
> أيّ ملفّ مصدر ولا أيّ نتيجة وظيفيّة.
>
> **(ت-1) عدد اختبارات الواجهة = `844/844` لا `843/843`.**
> ما ورد في §3 و§5 من أنّ «`844` غير موجود في أيّ أثر محفوظ» وأنّ `FRONTEND_COUNT_DELTA = UNREPRODUCIBLE_NO_844_ARTIFACT`
> **مُلغًى**. أُعيد القياس على شجرة الالتزام الوظيفيّ `bb37d1a`: `Test Files 73 passed (73)` ·
> `Tests 844 passed (844)` · `VITEST_EXIT=0`. سبب الخطأ الأصليّ: نقل رقم من سجلّ سابق بلا إعادة قياس.
> **`FRONTEND_TEST_COUNT_AUTHORITY = 844/844`.**
>
> **(ت-2) المرجعان الاحتياطيّان أُنشئا فعليًّا بأمر صريح.**
> ما ورد في §3 من أنّ `backup/unexpected-r22b-commit-1db114d` «المرجع غائب، ولم يُنشَأ امتثالًا للتعليمات»
> كان صحيحًا **وقت التدقيق فقط**. أُنشئ المرجعان بعده بأمر صريح، وهما قائمان الآن محلّيًّا:
> ```
> refs/heads/backup/unexpected-r22b-commit-1db114d → 1db114d
> refs/heads/backup/unexpected-r22b-commit-7c269df → 7c269df
> BACKUP_REFS_EXIST = YES   ·   BACKUP_REFS_PUSHED_TO_ORIGIN = NO (محلّيّان)
> ```
>
> **(ت-3) الفرع القديم مدفوع — والادّعاء بخلاف ذلك مُصحَّح.**
> ```
> PUSHED = YES_ON_LEGACY_BRANCH_ONLY
> origin/hotfix/r22b-multiline-idempotency-reconciliation-20260901 = 7c269df   ← مدفوع
> origin/develop = cd09b67a…  ·  origin/main = 508509ad…                       ← لم يُمسّا
> ```
> أي أنّ `1db114d` و`7c269df` منشوران على البعيد داخل **الفرع القديم وحده**، ولم يدخلا `develop` ولا `main`.
>
> **(ت-4) فرق الـRunbook ملفّ كامل من 75 سطرًا لا «سطر واحد».**
> عبارة §6 و«سطر واحد فقط» في تقارير هذه الجولة تصف **سجلًّا واحدًا في `name-status`** لا سطر محتوى واحدًا.
> القياس الصريح:
> ```
> git diff --numstat bb37d1a 1db114d
>   75  0  Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md
> git diff --shortstat bb37d1a 1db114d   ⟹ 1 file changed, 75 insertions(+)
> ```
> فالتلوّث الذي بُنيت عليه `PREMERGE_COMMIT_INTEGRITY = FAIL` هو **إدراج ملفّ توثيقيّ كامل (75 سطرًا)**
> داخل التزام وظيفيّ، وهو أثقل ممّا توحي به صياغة «سجلّ واحد».

---

## 1. كائن الالتزام `1db114d` كما هو

```
tree    d625aeaefc3ed09802eb5c196e18da7cd118246a
parent  cd09b67a0924a9932a1c5411a75d5dfb848f130d
author/committer  Ibrahim Elbahrawi  Tue Sep 1 23:13:44 2026 +0300  (متطابقان)
```

### الأعلام المطلوبة

| العَلَم | القيمة | المصدر |
|---|---|---|
| `COMMIT_PARENT_EQUALS_cd09b67` | **YES** | `parent` في كائن الالتزام |
| `RUNBOOK_PRESENT_IN_1db114d` | **YES** | `A Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md` |
| `NEGATIVE_CONTROL_NUMBERS_IN_COMMIT_MESSAGE` | **YES** — «(7 و3 و5 و6 حالات فشل على التوالي)» | نصّ الرسالة |
| `CMP_CLAIM_PRESENT_IN_COMMIT_MESSAGE` | **YES** — «مُثبَت بـcmp» | نصّ الرسالة |
| `SHA256_CLAIM_PRESENT_IN_COMMIT_MESSAGE` | **YES** — «وsha256» | نصّ الرسالة |

### نطاق الملفّات (13)
```
A Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md      ← وثيقة داخل التزام وظيفيّ
M reporting-backend/src/.../Notifications/EmailModels.cs
M reporting-backend/src/.../Reporting.Infrastructure.csproj
M reporting-backend/src/.../Services/SubmissionService.cs
A reporting-backend/tests/.../SubmissionIdempotencyContractTests.cs
A reporting-backend/tests/.../SubmissionIdempotencyIsolatedFactory.cs
A reporting-backend/tests/.../EmailHtmlMultilineTests.cs
A reporting-frontend/scripts/verify-multiline-bundle.mjs
M reporting-frontend/src/components/NotificationsBell.tsx
A reporting-frontend/src/pages/AdminArchiveMultiline.test.tsx
M reporting-frontend/src/pages/AdminArchivePage.tsx
A reporting-frontend/src/pages/ApprovalCommentsMultiline.test.tsx
M reporting-frontend/src/pages/SubmissionsPage.tsx
```

---

## 2. هل أُعيد بناء `1db114d`؟ — لا. ولم يُدَّع ذلك

`git reflog` يُظهر أنّ الالتزام أُنشئ **مرّة واحدة** ولم يُعدَّل بعدها:

```
7c269df  2026-09-01 23:48:06  commit: docs(r22b) …
1db114d  2026-09-01 23:13:44  commit: fix(r22b) …
cd09b67  2026-09-01 22:47:36  checkout: … → hotfix/r22b-multiline-idempotency-reconciliation-20260901
```

لا يظهر أيّ `commit (amend)` ولا `reset:` عند هذا الموضع. **الحالة B لا تنطبق**: لم أزعم قطّ أنّ
`1db114d` أُعيد بناؤه، والتقرير السابق قدّمه بوصفه المرشَّح الوحيد المُنشأ في تلك الجولة. ثبات الـSHA
هنا ليس تناقضًا لأنّه لم تقع إعادة بناء أصلًا.

## 3. ما تعذّر إثباته — يُسجَّل صراحةً

بحثٌ نصّيّ في المستودع كلّه (شجرة العمل + `a1feab2`) لم يجد أيًّا ممّا يلي:

```
UNEXPECTED_COMMIT_SHA                 = غير موجود في أيّ ملفّ
"unexpected-r22b" / "الالتزام الغامض"  = غير موجود
backup/unexpected-r22b-commit-1db114d = المرجع غير موجود
refs/heads|tags مطابقة لـbackup|unexpected = لا شيء
FULL_FRONTEND_SUITE = 844/844          = غير موجود في أيّ ملفّ (الوارد الوحيد: 843 على 73 ملفًّا)
```

لذلك لم يُنفَّذ `git diff … backup/… 1db114d` (المرجع غائب، ولم يُنشَأ امتثالًا للتعليمات)، ولا أستطيع
تأكيد أنّ «أرقام Negative Controls خاطئة» أو أنّ «`cmp`/`sha256` ادُّعِيا قبل تنفيذهما»: الأرقام
`7·3·5·6` في الرسالة **تطابق** المسجَّل في `DEPLOYMENT-AND-GATES.md`، وgit لا يحمل دليلًا زمنيًّا على
لحظة تنفيذ `cmp`. أسجّل هذين البندين `UNVERIFIABLE_FROM_ARTIFACTS` بدل تصديقهما أو نفيهما.

**البند الوحيد المؤكَّد قياسًا هو تلوّث النطاق:** وثيقة `Docs/Runbooks/…` داخل التزام وظيفيّ/اختباريّ.

---

## 4. تدقيق التزام الأدلّة `7c269df`

```
git diff --name-status 1db114d 7c269df   → 16 ملفًّا، كلّها A
   Ops/R22B/PHASE4-TEST/…ADDENDUM…md · Ops/R22B/RECONCILIATION-20260901/** (تقارير وأدلّة ولقطات)
   Ops/R22B/tools/r22r-uat-{provision,journey,cleanup}.py
   reporting-frontend/e2e/r22r-multiline-surfaces.mjs        ← أداة قياس، خارج src ولا تستوردها الحزمة

git diff --exit-code 1db114d 7c269df -- reporting-frontend/src reporting-backend/src  → exit 0
```

```
EVIDENCE_COMMIT_CONTAINS_APPLICATION_SOURCE = NO
FUNCTIONAL_TREE_CHANGED_AFTER_UAT           = NO
```

⟹ الشجرة الوظيفيّة التي جرى عليها UAT ونُشرت على TEST هي `d625aeae` نفسها بلا تغيير بعد الاختبار.

---

## 5. فرق 843 مقابل 844 — القياس

مقارنة شجرة الفرع القديم `a1feab2` بشجرة المرشَّح `1db114d`:

```
git diff --name-status a1feab2 1db114d -- '**/*.test.ts' '**/*.test.tsx'
   A  reporting-frontend/src/pages/AdminArchiveMultiline.test.tsx        (الوحيد)

عدّ ملفّات الاختبار وحالات it/test الساكنة:
   a1feab2 : 72 ملفًّا / 800 حالة
   1db114d : 73 ملفًّا / 804 حالات        (+1 ملفّ · +4 حالات · −0)
```

كلّ فروق `reporting-frontend` بين الشجرتين ثلاثة فقط:
`D e2e/r22b-multiline-acceptance.mjs` (ملفّ Playwright، ليس ضمن vitest) ·
`A src/pages/AdminArchiveMultiline.test.tsx` · `M src/pages/AdminArchivePage.tsx`.

**الحكم:** لا اختبار vitest حُذف ولا صار غير مكتشَف — الشجرة **كسبت** ملفًّا وأربع حالات ولم تفقد شيئًا،
ولا ملفّ اختبار قائم عُدِّل. لا يقع الفارق في `multiline` ولا `idempotency` ولا `provenance`، إذ إنّ
الملفّ الوحيد المستجدّ هو اختبار أرشيف الإدارة (multiline) وهو **إضافة** لا نقص.

الرقم `844` غير موجود في أيّ أثر محفوظ، والرقم المسجَّل الوحيد هو `843/843` على `73` ملفًّا وهو متّسق مع
عدد ملفّات الاختبار في `1db114d` بالضبط. لذلك: `FRONTEND_COUNT_DELTA = UNREPRODUCIBLE_NO_844_ARTIFACT`.
(ملاحظة قياسيّة: العدّ الساكن 804 أقلّ من عدد التشغيل 843 لأنّ `it.each` يولّد حالات عند التشغيل؛ فلا
يصحّ طرح رقمَي تشغيلين إلّا من سجلّين محفوظين، وهما غير متوفّرين.)

---

## 6. الحكم

```
PREMERGE_COMMIT_INTEGRITY = FAIL
MERGE_TO_DEVELOP          = REJECTED
```

**السبب المُثبَت وحده:** تلوّث نطاق الالتزام الوظيفيّ بوثيقة `Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md`.
أمّا ادّعاءات «الالتزام الغامض» و«الأرقام الخاطئة» و«الادّعاء قبل التنفيذ» فلا أثر لها في المستودع
وتُسجَّل `UNVERIFIABLE_FROM_ARTIFACTS` لا مؤكَّدة ولا منفيّة.

### خطّة الفرع النهائيّ (لم تُنفَّذ — تحتاج تصريحًا)

```
1) git fetch origin && git rev-parse origin/develop            ⟹ يجب أن يظلّ cd09b67
2) git switch --detach cd09b67 && git switch -c hotfix/r22b-multiline-idempotency-final-20260902
3) git checkout 1db114d -- <12 ملفًّا وظيفيًّا/اختباريًّا فقط، بلا Docs/Runbooks>
   ⟹ COMMIT#1 وظيفيّ/اختباريّ، رسالة بلا أرقام غير مقيسة وقت الالتزام
4) git checkout 1db114d -- Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md
   git checkout 7c269df -- Ops/R22B/** reporting-frontend/e2e/r22r-multiline-surfaces.mjs
   ⟹ COMMIT#2 أدلّة/Runbook منفصل
5) تحقّق: git diff --exit-code <COMMIT#2> 1db114d -- reporting-frontend/src reporting-backend/src
   ⟹ يجب أن يكون 0 (الشجرة الوظيفيّة المختبَرة على TEST لم تتغيّر)
6) لا force-push ولا إعادة كتابة للفرع المنشور: يُترك
   hotfix/r22b-multiline-idempotency-reconciliation-20260901 كما هو سجلًّا تاريخيًّا.
```

الالتزامان الناتجان سيحملان **SHAين جديدين** حتمًا (الأب نفسه لكن الشجرة والرسالة والزمن مختلفة).

**الشجرة الوظيفيّة المختبَرة `d625aeae` لا تتغيّر بهذه الخطّة** — يتغيّر تقسيم الالتزامات ورسائلها فقط،
فنتائج UAT وTEST تبقى سارية ولا تلزم إعادة نشر ولا إعادة تشغيل السويتات.
