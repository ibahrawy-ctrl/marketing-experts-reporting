# R22B — تقرير نشر TEST لإصلاح تعليقات الاعتماد متعدّدة الأسطر

**التاريخ:** 1 سبتمبر 2026 · **البيئة المستهدفة:** TEST فقط — `test.emarketingacademy.net`
**البيئتان الأخريان لم تُمسّا:** Production `reports.emarketingacademy.net` · RC (داخليّة على نفس الخادم)
**التصريح:** نشر TEST مُصرَّح به صراحةً · `RC_PROMOTION = NOT_AUTHORIZED` · `PROD_PROMOTION = NOT_AUTHORIZED`

---

## 1. المرشَّح المنشور

| الحقل | القيمة |
|---|---|
| `CANDIDATE_SHA` (المبنيّ والمنشور) | `00b1204fb2a248afd7d141d5632e5b6ed9ba1fd1` |
| الرسالة | `fix(r22b): أعِد تعليقات الاعتماد متعدّدة الأسطر إلى السلالة الحاكمة` |
| الأب | `986cc3b` (هويّة التقرير بنَسَب القالب) |
| الجدّ | `d25dc69` (حارس نشر القوالب — رأس البيئات الثلاث) |
| الفرع | `hotfix/r22b-multiline-comments-20260901` |
| المرشَّح المُلغى | `986cc3bb…` — **غير صالح للترقية** (لا يحمل الإصلاح) |
| رأس الفرع الحاليّ | `6925b31` — التزام توثيق وأدلّة **فوق** المنشور، بلا أيّ تغيير كود |

> **تمييز إلزاميّ:** المنشور على TEST هو `00b1204` وحده. الرأس الحاليّ `6925b31` يضيف سكربت رحلة القبول وأدلّتها وتقريرَي النشر والإغلاق فقط — `git diff --stat 00b1204 6925b31 -- reporting-frontend/src reporting-backend` = **فارغ** ⟹ الحزمة المنشورة لا تحتاج إعادة بناء.

**نَسَب سلسلة الإصلاح:** `d25dc69` → `986cc3b` → `00b1204`. أي أنّ إصلاح `DEFECT-IDEMPOTENCY-01` وهويّة القالب محفوظان بالكامل تحت المرشَّح الجديد، ولم تُعدَّل أيّ Migration ولا Seeder ولا كود خادم في هذا الإصلاح.

---

## 2. الحقول السبعة الإلزاميّة لبوّابة نَسَب الحزمة

طِبق `Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md` §2 القاعدة 3.

```
SOURCE_SHA             = 00b1204fb2a248afd7d141d5632e5b6ed9ba1fd1
APPROVED_ORIGIN_REF    = origin/hotfix/r22b-multiline-comments-20260901
FRONTEND_LOCKFILE_SHA  = 8c5c9342611118a6bc5bef59143b2354c133df54176780d24b5f41a19952b965
BUILD_ENVIRONMENT_KEYS = VITE_API_BASE_URL=https://test.emarketingacademy.net/api
BUILD_COMMAND          = npm ci && npm run build   (من شجرة عمل معزولة عبر git worktree add --detach)
DIST_MANIFEST_SHA      = fa4f7fdcc2bfdd44c1062985a4bd2170a33bfa4eabb3ea0843dc2f125eff5753
DEPLOYED_MANIFEST_SHA  = fa4f7fdcc2bfdd44c1062985a4bd2170a33bfa4eabb3ea0843dc2f125eff5753
```

**التطابق مُثبَت مرّتين** — لحظة النشر، ثمّ إعادة قياس مستقلّة على الخادم بعد اكتمال رحلة القبول:

```
$ ssh … 'cd /opt/reporting-test/frontend/dist && find . -type f | LC_ALL=C sort | xargs sha256sum | sha256sum'
fa4f7fdcc2bfdd44c1062985a4bd2170a33bfa4eabb3ea0843dc2f125eff5753
assets/index--j9POqBC.js   1666670 B   sha256 20650788c6d7947298880eff058e1c4590c2205ba3b66276bd08e6322bbb7fbf
assets/index-Dmr24Us9.css    35548 B   sha256 d8a88e177903d76a6a4cc2bfaeec9e3f3fc59e38c6c74958657f5338cfae08a4
```

`DIST_MANIFEST_SHA == DEPLOYED_MANIFEST_SHA == MIRROR_MANIFEST_SHA` (المرآة المحلّيّة التي شغّلت رحلة القبول) ⟹ **ما بُني هو ما نُشِر هو ما اختُبِر**، بالبايت.

---

## 3. أعلام بوّابة النَسَب

```
ARTIFACT_SOURCE_EXISTS_ON_ORIGIN      = YES   (00b1204 مدفوع؛ origin/hotfix/… == HEAD)
BUILD_SHA_ON_APPROVED_RELEASE_REF     = YES   (git merge-base --is-ancestor 00b1204 origin/hotfix/… ⇒ exit 0)
WORKTREE_CLEAN_AT_BUILD               = YES   (git status --porcelain فارغ لحظة البناء)
PRODUCTION_FIX_BACKMERGED_TO_DEVELOP  = PENDING_BY_DESIGN  (انظر §5)
REGRESSION_TESTS_IN_GOVERNING_BRANCH  = YES   (14 اختبار مكوّن + حارس الحزمة داخل 00b1204 نفسه)
DIRECT_ORPHAN_BRANCH_DEPLOYMENT       = FORBIDDEN — لم يقع (لا rsync من فرع يتيم ولا من شجرة غير مُلتزَمة)
```

> **`PRODUCTION_FIX_BACKMERGED_TO_DEVELOP = PENDING_BY_DESIGN`** لا `NO`: القاعدة 6 توجب الدمج على `origin/develop` **قبل إغلاق التذكرة وقبل السماح لإصدار لاحق باستبداله**. التذكرة ما زالت مفتوحة (RC/PROD غير مصرَّح بهما)، والدمج على `develop` يحتاج تصريحًا صريحًا جديدًا. هذا هو الالتزام المعلَّق الوحيد، وهو **حاجب لإغلاق التذكرة** لا لبوّابة TEST.

---

## 4. تفاصيل عمليّة النشر

- **النطاق:** الواجهة وحدها (`reporting-frontend/dist`). **الخادم لم يُعَد نشره**: TEST كان أصلًا على `986cc3bb…` وهو أب المرشَّح، ودلتا الخادم بين `986cc3b` و`00b1204` = **صفر ملفّ**.
- **النسخة الاحتياطيّة قبل الاستبدال:** `/opt/backups/frontend-test/dist-20260901T102255Z.tgz`
- **الهجرات:** `MIGRATIONS_ADDED = 0` · `MODEL_SNAPSHOT_DIFF = NONE` ⟹ لا كتابة على مخطّط القاعدة إطلاقًا.
- **الأسرار:** فحص `NO_SECRETS` في حارس الحزمة مرّ؛ لم تُطبَع أيّ أسرار في السجلّات.

---

## 5. قاعدة الحوكمة المعتمدة — موضعها

نصّ القواعد الثمانية والأعلام الستّة مُثبَت في **موضعين**:

1. `Docs/Runbooks/FRONTEND-ARTIFACT-PROVENANCE-GATE-R1.md` — **النسخة الحاكمة المتتبَّعة في Git** (داخل `00b1204`).
2. `Docs/Manual-Deployment-Guide-Reporting-System.md` §3.1 — نسخة مطابقة داخل الـRunbook التشغيليّ (هذا الملفّ خارج تتبّع Git بحكم `.gitignore: Docs/*`).

وليست محصورة في تقرير R22B، امتثالًا لطلب «أضِف القاعدة إلى تقرير النشر وإلى الـRunbook الحاكم».
